using Rowlan.TerrainAlign;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using static com.rowlan.terrainalign.TerrainAlignSettings;

namespace com.rowlan.terrainalign
{
    public class MeshProjector
    {
        [SerializeField]
        TerrainAlignSettings settings;

        [SerializeField]
        GameObject gameObject;


        [SerializeField]
        RenderTexture heightMapBackupRt;

        public RTHandleCollection m_rtCollection;

        [System.NonSerialized] private bool m_initialized = false;

        public class RenderTextureDescription
        {

            public int Hash { get; }
            public string Name { get; }
            public GraphicsFormat Format { get; }
            public bool debug { get; }

            public RenderTextureDescription(string name, GraphicsFormat format, bool debug)
            {
                this.Hash = name.GetHashCode();
                this.Name = name;
                this.Format = format;
                this.debug = debug;
            }
        }

        static class RenderTextureIDs
        {
            public static string heightMapOriginal = "heightMapOriginal";
            public static string meshHeight = "meshHeight";
            public static string combinedHeightMap = "combinedHeightMap";
            public static string heightMapCurrent = "heightMapCurrent";
            public static string cameraDepthRT = "cameraDepthRT";
        }

        public List<RenderTextureDescription> renderTextureDescriptions = new List<RenderTextureDescription>();

        private DebugView debugView;

        public class Projector
        {
            public GameObject gameObject;
            public Camera camera;

            RenderTexture prevCameraRT;

            public Projector(GameObject gameObject, Camera camera)
            {
                this.gameObject = gameObject;
                this.camera = camera;
            }

            public void PushTargetTexture()
            {
                prevCameraRT = camera.targetTexture;
            }

            public void PopTargetTexture()
            {
                camera.targetTexture = prevCameraRT;
            }
        }

        public static Material GetBlendMaterial()
        {
            return new Material(Shader.Find("Hidden/Rowlan/TerrainAlign/TerrainBlend"));
        }

        public static Material GetBlurMaterial()
        {
            return new Material(Shader.Find("Hidden/Rowlan/TerrainAlign/Blur"));
        }

        public static Material GetBottomUpToTopDownMaterial()
        {
            return new Material(Shader.Find("Hidden/Rowlan/TerrainAlign/BottomUpToTopDown"));
        }

        public MeshProjector( TerrainAlignSettings settings, GameObject gameObject)
        {
            this.gameObject = gameObject;
            this.settings = settings;
        }

        public void OnEnable()
        {
            Init();
            UpdateRenderTextures();

            // instantiate the debug view
            debugView = new DebugView(this);
            debugView.OnEnable();
        }

        public void OnDisable()
        {
            ReleaseHeightMap();
            m_rtCollection.ReleaseRTHandles();
            debugView.OnDisable();
        }



        private void Init()
        {
            if (m_initialized)
                return;

            renderTextureDescriptions.Add(new RenderTextureDescription(RenderTextureIDs.heightMapOriginal, GraphicsFormat.R16_UNorm, true));
            renderTextureDescriptions.Add(new RenderTextureDescription(RenderTextureIDs.meshHeight, GraphicsFormat.R16_UNorm, true));
            renderTextureDescriptions.Add(new RenderTextureDescription(RenderTextureIDs.combinedHeightMap, GraphicsFormat.R16_UNorm, true));
            renderTextureDescriptions.Add(new RenderTextureDescription(RenderTextureIDs.heightMapCurrent, GraphicsFormat.R16_UNorm, true));
            renderTextureDescriptions.Add(new RenderTextureDescription(RenderTextureIDs.cameraDepthRT, GraphicsFormat.DepthAuto, false));

            // create RT handles
            m_rtCollection = new RTHandleCollection();
            foreach (RenderTextureDescription renderTextureDescription in renderTextureDescriptions)
            {
                m_rtCollection.AddRTHandle(renderTextureDescription.Hash, renderTextureDescription.Name, renderTextureDescription.Format);
            }

            m_initialized = true;
        }

        public Projector CreateProjector()
        {
            float orthoSize = settings.terrain.terrainData.size.x / 2;
            float terrainHeight = settings.terrain.terrainData.size.y;

            Vector3 position;
            Quaternion rotation;

            switch (settings.direction)
            {
                case Direction.TopDown:

                    position = new Vector3(
                        Terrain.activeTerrain.transform.position.x + orthoSize,
                        Terrain.activeTerrain.transform.position.y + terrainHeight + settings.terrainOffsetY,
                        Terrain.activeTerrain.transform.position.z + orthoSize
                        );

                    rotation = Quaternion.Euler(90, 0, 0);

                    break;

                case Direction.BottomUp:

                    position = new Vector3(
                        Terrain.activeTerrain.transform.position.x + orthoSize,
                        Terrain.activeTerrain.transform.position.y /* + terrainHeight */ + settings.terrainOffsetY,
                        Terrain.activeTerrain.transform.position.z + orthoSize
                        );

                    rotation = Quaternion.Euler(-90, 0, 0);

                    break;

                default:
                    throw new ArgumentOutOfRangeException( "Unsupported direction: " + settings.direction); 
            }


            // gameobject
            GameObject gameObject = new GameObject("Projector [Temp]");
            gameObject.transform.position = position;
            gameObject.transform.rotation = rotation;
            gameObject.hideFlags = HideFlags.DontSave;

            // camera
            Camera camera = gameObject.AddComponent<Camera>();

            camera.orthographic = true;
            camera.orthographicSize = orthoSize;
            camera.nearClipPlane = 0f;
            camera.farClipPlane = terrainHeight;
            camera.renderingPath = RenderingPath.Forward;
            camera.allowMSAA = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            //camera.backgroundColor.a = 0f;
            camera.cullingMask = 0; // don't render any layer, we render our gameobjects
            //projectorCamera.targetTexture = m_rtCollection[RenderTextureIDs.cameraDepthRT].RT;

            Projector projector = new Projector(gameObject, camera);
            return projector;
        }

        private void ReleaseProjector(Projector projector)
        {
            UnityEngine.Object.DestroyImmediate(projector.camera);
            UnityEngine.Object.DestroyImmediate(projector.gameObject);
        }

        public void UpdateRenderTextures()
        {

            if (!settings.terrain)
            {
                Debug.LogError("Terrain required");
                return;
            }

            m_rtCollection.ReleaseRTHandles();

            TerrainData terrainData = settings.terrain.terrainData;
            int size = terrainData.heightmapResolution;

            m_rtCollection.GatherRTHandles(size, size, 16);

            RenderTexture prevRt = RenderTexture.active;
            {
                RenderMesh();
            }
            RenderTexture.active = prevRt;

            if ( debugView != null)
            {
                debugView.StoreDebugTextures();
            }

            m_rtCollection.ReleaseRTHandles();

        }

        public void RenderMesh()
        {

            #region Heightmap Backup
            {
                // create heightmap backup
                if (!heightMapBackupRt)
                {
                    CreateHeightMapBackup();
                }

                // heightmap backup debug view
                Graphics.Blit(heightMapBackupRt, m_rtCollection[RenderTextureIDs.heightMapOriginal], new Vector2(1f, 1f), new Vector2(0f, 0f));
            }
            #endregion Heightmap Backup

            #region Mesh Projection
            {
                Projector projector = CreateProjector();
                {
                    // ensure the render matrix will be the dimensions of the render texture
                    projector.PushTargetTexture();
                    {
                        projector.camera.targetTexture = m_rtCollection[RenderTextureIDs.cameraDepthRT].RT;

                        RenderTexture prevRT = RenderTexture.active;
                        {
                            // change camera, otherwise scene view camera would be used
                            Camera prevCamera = Camera.current;
                            Camera.SetupCurrent(projector.camera);
                            {
                                RenderTexture rt = m_rtCollection[RenderTextureIDs.cameraDepthRT].RT;

                                Graphics.SetRenderTarget(rt);

                                GL.Viewport(new Rect(0, 0, rt.width, rt.height));
                                GL.Clear(true, true, projector.camera.backgroundColor, 1f);

                                GL.PushMatrix();
                                {
                                    GL.LoadProjectionMatrix(projector.camera.projectionMatrix);

                                    GL.PushMatrix();
                                    {
                                        RenderGameObjectNow( gameObject, (int)0);
                                    }
                                    GL.PopMatrix();
                                }
                                GL.PopMatrix(); // restore matrix

                                Graphics.ClearRandomWriteTargets();
                            }
                            Camera.SetupCurrent(prevCamera);

                        }
                        // Calling SetRenderTarget with just a RenderTexture argument is the same as setting RenderTexture.active property.
                        // https://docs.unity3d.com/ScriptReference/Graphics.SetRenderTarget.html
                        RenderTexture.active = prevRT;
                    }
                    projector.PopTargetTexture();

                    Graphics.Blit(m_rtCollection[RenderTextureIDs.cameraDepthRT], m_rtCollection[RenderTextureIDs.meshHeight]);
                }

                #region Perspective correction
                if (settings.direction == Direction.BottomUp)
                {
                    ConvertBottomUpToTopDown(m_rtCollection[RenderTextureIDs.meshHeight], m_rtCollection[RenderTextureIDs.meshHeight], projector, gameObject);
                }
                #endregion Perspective correction

                ReleaseProjector(projector);
            }
            #endregion Mesh Projection


            #region Effects
            {
                if (settings.blur)
                {
                    ApplyBlur(m_rtCollection[RenderTextureIDs.meshHeight], m_rtCollection[RenderTextureIDs.meshHeight]);
                }
            }
            #endregion Effects

            #region Create Heightmap
            {
                RenderTexture prev = RenderTexture.active;
                {

                    RenderTexture.active = m_rtCollection[RenderTextureIDs.combinedHeightMap];

                    Material mat = GetBlendMaterial();
                    mat.SetTexture("_BlendTex", m_rtCollection[RenderTextureIDs.meshHeight]);
                    mat.SetInt("_BlendMode", (int)settings.blendMode);
                    mat.SetFloat("_Blend", settings.valueBlend);

                    Graphics.Blit(m_rtCollection[RenderTextureIDs.heightMapOriginal], m_rtCollection[RenderTextureIDs.combinedHeightMap], mat);

                    if (settings.featureEnabled)
                    {
                        ToolboxHelper.CopyTextureToTerrainHeight(
                            settings.terrain.terrainData,
                            m_rtCollection[RenderTextureIDs.combinedHeightMap],
                            new Vector2Int(0, 0),
                            m_rtCollection[RenderTextureIDs.combinedHeightMap].RT.width,
                            1,
                            0f,
                            1f);
                    }
                }
                RenderTexture.active = prev;
            }
            #endregion Create Heightmap

            #region Debug View
            {
                if (settings.debug)
                {
                    Graphics.Blit(settings.terrain.terrainData.heightmapTexture, m_rtCollection[RenderTextureIDs.heightMapCurrent]);
                }
            }
            #endregion Debug View

        }

        /// <summary>
        /// Render a gameobject hierarchy. In case of LOD use the one with the specified level
        /// </summary>
        /// <param name="go"></param>
        /// <param name="sourceLODLevel"></param>
        public static void RenderGameObjectNow(GameObject go, int sourceLODLevel)
        {
            GameObject root = go;

            LODGroup lodGroup = go.GetComponent<LODGroup>();
            if (lodGroup && lodGroup.lodCount > 0)
            {
                root = lodGroup.GetLODs()[sourceLODLevel].renderers[0].gameObject;
            }

            MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                MeshFilter meshFilter = renderers[i].gameObject.GetComponent<MeshFilter>();
                if (meshFilter)
                {
                    Matrix4x4 matrix = Matrix4x4.TRS(
                        renderers[i].transform.position,
                        renderers[i].transform.rotation,
                        renderers[i].transform.lossyScale);

                    Mesh mesh = meshFilter.sharedMesh;

                    for (int j = 0; j < renderers[i].sharedMaterials.Length; j++)
                    {
                        Material material = renderers[i].sharedMaterials[j];
                        material.SetPass(0);
                        Graphics.DrawMeshNow(mesh, matrix, j);
                    }
                }
            }
        }

        /// <summary>
        /// Convert the rendertexture of the bottom-up projection to one that's aligned to top-down projection
        /// </summary>
        /// <param name="source"></param>
        /// <param name="destination"></param>
        /// <param name="projector"></param>
        /// <param name="gameObject"></param>
        private void ConvertBottomUpToTopDown(RenderTexture source, RenderTexture destination, Projector projector, GameObject gameObject)
        {
            Material material = GetBottomUpToTopDownMaterial();

            var temporaryTexture = RenderTexture.GetTemporary(source.width, source.height);

            // TODO: lowest y-position of all children of container
            float gameObjectPositionY = gameObject.transform.position.y;
            float cameraFarClipPlane = projector.camera.farClipPlane;

            material.SetFloat("_GameObjectPositionY", gameObjectPositionY);
            material.SetFloat("_CameraFarClipPlane", cameraFarClipPlane);
            material.SetFloat("_CutOff", settings.bottomUpCutOff);

            Graphics.Blit(source, temporaryTexture, material);
            Graphics.Blit(temporaryTexture, destination);

            RenderTexture.ReleaseTemporary(temporaryTexture);

        }

        #region Effects

        // https://www.ronja-tutorials.com/post/023-postprocessing-blur/
        // https://github.com/ronja-tutorials/ShaderTutorials/blob/master/Assets/023_PostprocessingBlur/PostprocessingBlur.cs
        // https://raw.githubusercontent.com/ronja-tutorials/ShaderTutorials/master/Assets/023_PostprocessingBlur/PostprocessingBlur.shader

        private void ApplyBlur(RenderTexture source, RenderTexture destination)
        {

            Material material = GetBlurMaterial();
            material.SetFloat("_BlurSize", settings.blurSettings.blurSize);
            material.SetInt("_Samples", settings.blurSettings.blurSamples);
            material.SetFloat("_Gauss", settings.blurSettings.gauss ? 1 : 0);
            material.SetFloat("_StandardDeviation", settings.blurSettings.gaussStandardDeviation);


            var temporaryTexture = RenderTexture.GetTemporary(source.width, source.height);
            Graphics.Blit(source, temporaryTexture, material, 0);
            Graphics.Blit(temporaryTexture, destination, material, 1);
            RenderTexture.ReleaseTemporary(temporaryTexture);

        }

        #endregion Effects

        public void CreateHeightMapBackup()
        {

            RenderTexture rt = settings.terrain.terrainData.heightmapTexture;

            Vector2Int dim = new Vector2Int(rt.width, rt.height);

            heightMapBackupRt = new RenderTexture(dim.x, dim.y, 0, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear);
            heightMapBackupRt.enableRandomWrite = true;
            heightMapBackupRt.autoGenerateMips = false;

            heightMapBackupRt.Create();

            Graphics.Blit(settings.terrain.terrainData.heightmapTexture, heightMapBackupRt);

            Debug.Log("Heightmap Backup Created");

        }

        private void ReleaseHeightMap()
        {
            Debug.Log("Release Heightmap");

            UnityEngine.Object.DestroyImmediate(heightMapBackupRt);
            heightMapBackupRt = null;
        }

        public void ResetHeightMap()
        {
            ReleaseHeightMap();
            UpdateRenderTextures();
        }

        public Terrain GetCurrentTerrain()
        {
            return settings.terrain;
        }

        public DebugView GetDebugView()
        {
            return debugView;
        }
    }
}
