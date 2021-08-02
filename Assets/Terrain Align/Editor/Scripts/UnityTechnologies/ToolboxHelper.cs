/// This class is based on the Unity Technologies Terrain Tools.
/// License:
/// 
///   Terrain Tools copyright © 2020 Unity Technologies ApS
///   Licensed under the Unity Companion License for Unity-dependent projects--see Unity Companion License.
///   Unless expressly provided otherwise, the Software under this license is made available strictly on an “AS IS” BASIS WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED. Please review the license for details on these and other terms and conditions.
///
/// https://docs.unity3d.com/Packages/com.unity.terrain-tools@3.0/license/LICENSE.html
///
using UnityEngine;

namespace Rowlan.TerrainAlign
{
    public class ToolboxHelper
    {
        public static Material GetHeightBlitMaterial()
        {
            return new Material(Shader.Find("Hidden/Rowlan/TerrainAlign/HeightBlit"));
        }
        public static float kNormalizedHeightScale => 32766.0f / 65535.0f;

        public static void CopyTextureToTerrainHeight(TerrainData terrainData, RenderTexture heightmap, Vector2Int indexOffset, int resolution, int numTiles, float baseLevel, float remap)
        {
            terrainData.heightmapResolution = resolution + 1;

            float hWidth = heightmap.height;
            float div = hWidth / numTiles;

            float scale = ((resolution / (resolution + 1.0f)) * (div + 1)) / hWidth;
            float offset = ((resolution / (resolution + 1.0f)) * div) / hWidth;

            Vector2 scaleV = new Vector2(scale, scale);
            Vector2 offsetV = new Vector2(offset * indexOffset.x, offset * indexOffset.y);

            Material blitMaterial = GetHeightBlitMaterial();
            blitMaterial.SetFloat("_Height_Offset", baseLevel * kNormalizedHeightScale);
            blitMaterial.SetFloat("_Height_Scale", remap * kNormalizedHeightScale);
            RenderTexture heightmapRT = RenderTexture.GetTemporary(terrainData.heightmapTexture.descriptor);

            Graphics.Blit(heightmap, heightmapRT, blitMaterial);

            Graphics.Blit(heightmapRT, terrainData.heightmapTexture, scaleV, offsetV);

            terrainData.DirtyHeightmapRegion(new RectInt(0, 0, terrainData.heightmapTexture.width, terrainData.heightmapTexture.height), TerrainHeightmapSyncControl.HeightAndLod);

            RenderTexture.ReleaseTemporary(heightmapRT);

        }
    }
}