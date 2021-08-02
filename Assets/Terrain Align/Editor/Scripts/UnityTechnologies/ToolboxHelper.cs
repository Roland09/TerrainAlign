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

        // Post about the [0 to 0.5] terrain limit:
        // This is correct. The heightmap implementation itself is signed but is treated as unsigned when rendering so we only have half the
        // precision available to use for height values.That's why all of our Terrain painting shaders clamp
        // the returned value between 0f and .5f so that we don't end up writing signed values into the heightmap.
        // If you were to put in values greater than .5, you'll see the Terrain surface "wrap" to negative height values.
        // I can't say why this was done but it probably has stayed this way because it would take a lot of code changes to make either of them signed or unsigned to match.
        // The values are normalized so that we can get the most precision we can out of the .5f for a given Terrain's max height.
        // 0 being a world height offset of 0 and .5f being terrain.terrainData.size.y (the max height) 
        // https://forum.unity.com/threads/terraindata-heightmaptexture-float-value-range.672421/#post-4516975

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