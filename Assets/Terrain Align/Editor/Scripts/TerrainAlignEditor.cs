using UnityEngine;
using UnityEditor;
using UnityEngine.Experimental.Rendering;
using System.Collections.Generic;

namespace Rowlan.TerrainAlign
{
    [CustomEditor(typeof(TerrainAlign))]
    public class TerrainAlignEditor : Editor
    {
        private TerrainAlignEditor editor;
        private TerrainAlign editorTarget;

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

            public RenderTextureDescription( string name, GraphicsFormat format, bool debug)
            {
                this.Hash = name.GetHashCode();
                this.Name = name;
                this.Format = format;
                this.debug = debug;
            }
        }

        static class RenderTextureIDs
        {
            public static string heightMap = "heightMap";
            public static string heightMapBackup = "heightMapBackup";
            public static string meshHeight = "meshHeight";
            public static string combinedHeightMap = "combinedHeightMap";
            public static string cameraDepthRT = "cameraDepthRT";
        }

        public List<RenderTextureDescription> renderTextureDescriptions = new List<RenderTextureDescription>();

        private DebugView debugView;

        private SceneView sceneView = null;

        private class Projector
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
        
        public void OnEnable()
        {
            this.editor = this;
            this.editorTarget = (TerrainAlign)this.target;

            // get the sceneview window. don't focus it, there's an occasional bug where it would get recreated and then you have multiple ones
            sceneView = EditorWindow.GetWindow<SceneView>( "Scene View Window", false);

            Init(); 

            // try getting a terrain in case there is none
            if (!editorTarget.terrain)
            {
                editorTarget.terrain = UnityEngine.Object.FindObjectOfType<Terrain>();
            }

            UpdateRenderTextures();
        }

        void OnDisable()
        {
            m_rtCollection.ReleaseRTHandles();

            debugView.ReleaseDebugTextures();

            sceneView = null;

        }


        private void Init()
        {
            if (m_initialized)
                return;

            renderTextureDescriptions.Add(new RenderTextureDescription(RenderTextureIDs.heightMap, GraphicsFormat.R16_UNorm, true));
            renderTextureDescriptions.Add(new RenderTextureDescription(RenderTextureIDs.heightMapBackup, GraphicsFormat.R16_UNorm, true));
            renderTextureDescriptions.Add(new RenderTextureDescription(RenderTextureIDs.meshHeight, GraphicsFormat.R16_UNorm, true));
            renderTextureDescriptions.Add(new RenderTextureDescription(RenderTextureIDs.combinedHeightMap, GraphicsFormat.R16_UNorm, true));
            renderTextureDescriptions.Add(new RenderTextureDescription(RenderTextureIDs.cameraDepthRT, GraphicsFormat.DepthAuto, false));

            // create RT handles
            m_rtCollection = new RTHandleCollection();
            foreach( RenderTextureDescription renderTextureDescription in renderTextureDescriptions) {
                m_rtCollection.AddRTHandle(renderTextureDescription.Hash, renderTextureDescription.Name, renderTextureDescription.Format);
            }

            // instantiate the debug view
            debugView = new DebugView(this);
            debugView.SetupDebugTextures();

            m_initialized = true;
        }

        private Projector CreateProjector()
        {
            float orthoSize = editorTarget.terrain.terrainData.size.x / 2;
            float terrainHeight = editorTarget.terrain.terrainData.size.y;

            // position camera
            Vector3 position = new Vector3(
                Terrain.activeTerrain.transform.position.x + orthoSize,
                Terrain.activeTerrain.transform.position.y + terrainHeight,
                Terrain.activeTerrain.transform.position.z + orthoSize
                );

            Quaternion rotation = Quaternion.Euler(90, 0, 0);

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

        private void ReleaseProjector( Projector projector)
        {
            DestroyImmediate(projector.camera);
            DestroyImmediate(projector.gameObject);
        }    

        public void UpdateRenderTextures()
        {

            if (!IsEnabled())
                return;

            if (!editorTarget.terrain)
            {
                Debug.LogError("Terrain required");
                return;
            }

            m_rtCollection.ReleaseRTHandles();

            TerrainData terrainData = editorTarget.terrain.terrainData;
            int size = terrainData.heightmapResolution;

            m_rtCollection.GatherRTHandles(size, size, 16);

            RenderTexture prevRt = RenderTexture.active;
            {
                RenderMesh();
            }
            RenderTexture.active = prevRt;

            debugView.StoreDebugTextures();

            m_rtCollection.ReleaseRTHandles();

        }

        private Mesh GetActiveMesh()
        {

            Mesh mesh = null;

            MeshFilter meshFilter = editorTarget.GetComponent<MeshFilter>();
            if (meshFilter)
            {
                mesh = meshFilter.sharedMesh;
            }

            if (!mesh)
            {
                // TODO: used for eg irval
                SkinnedMeshRenderer smr = editorTarget.GetComponent<SkinnedMeshRenderer>();
                if (smr)
                {
                    mesh = smr.sharedMesh;
                }
            }

            return mesh;
        }

        public void RenderMesh()
        {
            Mesh activeMesh = GetActiveMesh();
            if (!activeMesh)
            {
                Debug.Log("Mesh required");
                return;
            }

            #region Heightmap Backup
            {
                // create heightmap backup
                if (!heightMapBackupRt)
                {
                    CreateHeightMapBackup();
                }

                // heightmap backup debug view
                Graphics.Blit(heightMapBackupRt, m_rtCollection[RenderTextureIDs.heightMapBackup], new Vector2(1f, 1f), new Vector2(0f, 0f));
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
                                        RenderGameObjectNow(editorTarget.gameObject, (int)0);
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
                ReleaseProjector(projector);
            }
            #endregion Mesh Projection

            #region Effects
            {
                if (editorTarget.blur)
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
                    mat.SetInt("_BlendMode", (int)editorTarget.blendMode);
                    mat.SetFloat("_Blend", editorTarget.blend);

                    Graphics.Blit(m_rtCollection[RenderTextureIDs.heightMapBackup], m_rtCollection[RenderTextureIDs.combinedHeightMap], mat);

                    if (editorTarget.featureEnabled)
                    {
                        ToolboxHelper.CopyTextureToTerrainHeight(
                            editorTarget.terrain.terrainData,
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
                if (editorTarget.debug)
                {
                    Graphics.Blit(editorTarget.terrain.terrainData.heightmapTexture, m_rtCollection[RenderTextureIDs.heightMap]);
                }
            }
            #endregion Debug View

        }

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

        // https://www.ronja-tutorials.com/post/023-postprocessing-blur/
        // https://github.com/ronja-tutorials/ShaderTutorials/blob/master/Assets/023_PostprocessingBlur/PostprocessingBlur.cs
        // https://raw.githubusercontent.com/ronja-tutorials/ShaderTutorials/master/Assets/023_PostprocessingBlur/PostprocessingBlur.shader

        private void ApplyBlur(RenderTexture source, RenderTexture destination) {

            Material material = GetBlurMaterial();
            material.SetFloat("_BlurSize", editorTarget.blurSettings.blurSize);
            material.SetInt("_Samples", editorTarget.blurSettings.blurSamples);
            material.SetFloat("_Gauss", editorTarget.blurSettings.gauss ? 1 : 0);
            material.SetFloat("_StandardDeviation", editorTarget.blurSettings.gaussStandardDeviation);


            var temporaryTexture = RenderTexture.GetTemporary(source.width, source.height);
            Graphics.Blit(source, temporaryTexture, material, 0);
            Graphics.Blit(temporaryTexture, destination, material, 1);
            RenderTexture.ReleaseTemporary(temporaryTexture);

        }

        private bool IsEnabled()
        {
            return editorTarget.isActiveAndEnabled;
        }

        public void OnSceneGUI()
        {
            if (!IsEnabled())
                return;

            // check if transform has changed and perform action
            if (Event.current.type == EventType.Repaint)
            {
                if (editorTarget.transform.hasChanged)
                {

                    editorTarget.transform.hasChanged = false;

                    UpdateRenderTextures();

                }
            }

            // update the debug view in case debug is enabled
            UpdateDebugView();

        }

        private void UpdateDebugView()
        {
            if (!editorTarget.debug)
                return;

            // dont render preview if this isnt a repaint. losing performance if we do
            if (Event.current.type == EventType.Repaint)
            {
                debugView.DrawDebugTextures(sceneView.position.height / 4);
            }
        }

        public override void OnInspectorGUI()
        {

            EditorGUI.BeginChangeCheck();

            DrawDefaultInspector();

            if (EditorGUI.EndChangeCheck())
            {
                // eg when local scale setting changed we need to update the render textures
                UpdateRenderTextures();

            }

            EditorGUILayout.BeginVertical();
            {

                EditorGUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("Save Source Terrain"))
                    {
                        CreateHeightMapBackup();

                        // update the debug view in case debug is enabled
                        UpdateDebugView();

                    }

                    if (GUILayout.Button("Flatten entire terrain"))
                    {
                        TerrainUtils.FlattenTerrain(editorTarget.terrain);

                        heightMapBackupRt = null;
                        UpdateRenderTextures();

                        // update the debug view in case debug is enabled
                        UpdateDebugView();

                    }
                }
                EditorGUILayout.EndHorizontal();

            }
            EditorGUILayout.EndVertical();

            if (editorTarget.debug)
            {
                EditorGUILayout.BeginVertical();
                {

                    EditorGUILayout.BeginHorizontal();
                    {
                        if (GUILayout.Button("Save to Files"))
                        {
                            debugView.SaveToFile();
                        }

                        if (GUILayout.Button("Clone Projector"))
                        {
                            // this creates an instance of the projector including the camera
                            // please note that the dimensions will show up wrong since there's no target texture assigned to render to
                            Projector projector = CreateProjector();
                            Debug.Log("Projector created for debug purposes. Please note that the dimensions will show up wrong since there's no target texture assigned to render to, you have to assign one yourself");

                        }
                    }
                    EditorGUILayout.EndHorizontal();

                }
                EditorGUILayout.EndVertical();

            }
        }



        private Texture2D GetTexture(RenderTexture rt)
        {

            RenderTexture prevRt = RenderTexture.active;

            RenderTexture.active = rt;

            Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.R16, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);

            RenderTexture.active = prevRt;

            return tex;
        }

        private void CreateHeightMapBackup()
        {

            RenderTexture rt = editorTarget.terrain.terrainData.heightmapTexture;

            Vector2Int dim = new Vector2Int(rt.width, rt.height);

            heightMapBackupRt = new RenderTexture(dim.x, dim.y, 0, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear);
            heightMapBackupRt.enableRandomWrite = true;
            heightMapBackupRt.autoGenerateMips = false;

            heightMapBackupRt.Create();

            Graphics.Blit(editorTarget.terrain.terrainData.heightmapTexture, heightMapBackupRt);

            Debug.Log("Heightmap Backup Created");

        }
   }
}