using com.rowlan.terrainalign;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static com.rowlan.terrainalign.MeshProjector;

namespace Rowlan.TerrainAlign
{
    public class DebugView
    {
        private Dictionary<int, RenderTexture> debugMap = new Dictionary<int, RenderTexture>();

        private MeshProjector meshProjector;

        public DebugView(MeshProjector meshProjector) {
            this.meshProjector = meshProjector;
        }

        public void OnEnable()
        {
            SetupDebugTextures();
        }

        public void OnDisable()
        {
            ReleaseDebugTextures();
        }

        private void ReleaseDebugTextures()
        {
            foreach (KeyValuePair<int, RenderTexture> p in debugMap)
            {
                p.Value.Release();
                TerrainAlignEditor.DestroyImmediate(p.Value);
            }

            debugMap.Clear();
        }

        private void SetupDebugTextures()
        {

            ReleaseDebugTextures();

            // create actually used render textures, those are mapped to the debug textures
            meshProjector.m_rtCollection.ReleaseRTHandles();


            if (meshProjector.GetCurrentTerrain())
            {
                TerrainData terrainData = meshProjector.GetCurrentTerrain().terrainData;

                int size = terrainData.heightmapResolution;
                meshProjector.m_rtCollection.GatherRTHandles(size, size, 16);

                foreach (RenderTextureDescription rtDesc in meshProjector.renderTextureDescriptions)
                {
                    RTHandle rtHandle = meshProjector.m_rtCollection.GetRTHandle(rtDesc.Hash);
                    RenderTexture rt = rtHandle.RT;

                    RenderTexture debugRT = new RenderTexture(rt.width, rt.height, rt.depth, rt.graphicsFormat);
                    debugRT.Create();

                    debugMap.Add(rtDesc.Hash, debugRT);
                }

                meshProjector.m_rtCollection.ReleaseRTHandles();
            }
        }

        public void StoreDebugTextures()
        {

            RenderTexture prevRt = RenderTexture.active;
            {
                foreach (RenderTextureDescription rtDesc in meshProjector.renderTextureDescriptions)
                {
                    RTHandle handle = meshProjector.m_rtCollection.GetRTHandle(rtDesc.Hash);
                    RenderTexture.active = handle.RT;

                    RenderTexture target = debugMap[rtDesc.Hash];

                    Graphics.Blit(handle.RT, target);
                }
            }
            RenderTexture.active = prevRt;

        }


        /// <summary>
        /// Render debug GUI in the SceneView that displays all the RTHandles in this RTHandleCollection
        /// <param name="size">The size that is used to draw the textures</param>
        /// </summary>
        public void DrawDebugTextures(float size)
        {
            const float padding = 10;

            Handles.BeginGUI();
            {
                Color prev = GUI.color;
                Rect rect = new Rect(padding, padding, size, size);

                foreach (KeyValuePair<int, RenderTexture> p in debugMap)
                {
                    string text = "";
                    bool debug = true;

                    foreach (RenderTextureDescription d in meshProjector.renderTextureDescriptions)
                    {
                        if (p.Key == d.Hash)
                        {
                            text = d.Name;
                            debug = d.debug;
                            break;
                        }
                    }

                    if (!debug)
                        continue;

                    GUI.color = new Color(1, 0, 1, 1);
                    GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.ScaleToFit);

                    GUI.color = Color.white;
                    if (p.Value != null)
                    {
                        GUI.DrawTexture(rect, p.Value, ScaleMode.ScaleToFit, false);
                    }
                    else
                    {
                        GUI.Label(rect, "NULL");
                    }

                    Rect labelRect = rect;
                    labelRect.y = rect.yMax;
                    labelRect.height = EditorGUIUtility.singleLineHeight;

                    GUI.Box(labelRect, text, Styles.box);

                    rect.y += padding + size + EditorGUIUtility.singleLineHeight;

                    if (rect.yMax + EditorGUIUtility.singleLineHeight > Screen.height - EditorGUIUtility.singleLineHeight * 2)
                    {
                        rect.y = padding;
                        rect.x = rect.xMax + padding;
                    }
                }

                GUI.color = prev;
            }
            Handles.EndGUI();
        }



        private static class Styles
        {
            public static GUIStyle box;

            static Styles()
            {
                box = new GUIStyle(EditorStyles.helpBox);
                box.normal.textColor = Color.white;
            }
        }

        public void SaveToFile() 
        {
            FileUtils fileUtils = new FileUtils();

            foreach (RenderTextureDescription rtDesc in meshProjector.renderTextureDescriptions)
            {
                string name = rtDesc.Name;
                RenderTexture RT = debugMap[rtDesc.Hash];
                
                if (!RT)
                    continue;

                fileUtils.SaveTexture(RT, name, true);
            }
        }
    }
}