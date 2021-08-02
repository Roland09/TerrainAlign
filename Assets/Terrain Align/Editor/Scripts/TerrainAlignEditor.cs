using UnityEngine;
using UnityEditor;
using com.rowlan.terrainalign;

namespace Rowlan.TerrainAlign
{
    [CustomEditor(typeof(TerrainAlign))]
    public class TerrainAlignEditor : Editor
    {
        private TerrainAlignEditor editor;
        private TerrainAlign editorTarget;

        private SceneView sceneView = null;

        public MeshProjector meshProjector;

        private bool sceneInitialized = false;

        public void OnEnable()
        {
            this.editor = this;
            this.editorTarget = (TerrainAlign)this.target;

            // get the sceneview window. don't focus it, there's an occasional bug where it would get recreated and then you have multiple ones
            sceneView = EditorWindow.GetWindow<SceneView>( "Scene View Window", false);

            // try getting a terrain in case there is none
            if (!editorTarget.settings.terrain)
            {
                editorTarget.settings.terrain = UnityEngine.Object.FindObjectOfType<Terrain>();
            }

            meshProjector = new MeshProjector( editorTarget.settings, editorTarget.gameObject);
            meshProjector.OnEnable();

        }

        void OnDisable()
        {
            meshProjector.OnDisable();

            sceneView = null;

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
                if (!sceneInitialized || editorTarget.transform.hasChanged)
                {
                    editorTarget.transform.hasChanged = false;

                    meshProjector.UpdateRenderTextures();

                    sceneInitialized = true;

                }
            }

            // update the debug view in case debug is enabled
            UpdateDebugView();

        }

        private void UpdateDebugView()
        {
            if (!editorTarget.settings.debug)
                return;

            // dont render preview if this isnt a repaint. losing performance if we do
            if (Event.current.type == EventType.Repaint)
            {
                meshProjector.GetDebugView().DrawDebugTextures(sceneView.position.height / 4);
            }
        }

        public override void OnInspectorGUI()
        {

            EditorGUILayout.HelpBox("This is experimental, please backup your terrain", MessageType.Info);

            EditorGUI.BeginChangeCheck();

            DrawDefaultInspector();

            if (EditorGUI.EndChangeCheck())
            {
                // eg when local scale setting changed we need to update the render textures
                if (IsEnabled())
                {
                    meshProjector.UpdateRenderTextures();
                }

            }

            EditorGUILayout.BeginVertical();
            {

                EditorGUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("Save Source Terrain"))
                    {
                        meshProjector.CreateHeightMapBackup();

                        // update the debug view in case debug is enabled
                        UpdateDebugView();

                    }

                    if (GUILayout.Button("Flatten entire terrain"))
                    {
                        TerrainUtils.FlattenTerrain(editorTarget.settings.terrain);

                        meshProjector.ResetHeightMap();

                        // update the debug view in case debug is enabled
                        UpdateDebugView();

                    }
                }
                EditorGUILayout.EndHorizontal();

            }
            EditorGUILayout.EndVertical();

            if (editorTarget.settings.debug)
            {
                EditorGUILayout.BeginVertical();
                {

                    EditorGUILayout.BeginHorizontal();
                    {
                        if (GUILayout.Button("Save to Files"))
                        {
                            meshProjector.GetDebugView().SaveToFile();
                        }

                        if (GUILayout.Button("Clone Projector"))
                        {
                            // this creates an instance of the projector including the camera
                            // please note that the dimensions will show up wrong since there's no target texture assigned to render to
                            meshProjector.CreateProjector();
                            Debug.Log("Projector created for debug purposes. Please note that the dimensions will show up wrong since there's no target texture assigned to render to, you have to assign one yourself");

                        }
                    }
                    EditorGUILayout.EndHorizontal();

                }
                EditorGUILayout.EndVertical();

            }
        }

   }
}