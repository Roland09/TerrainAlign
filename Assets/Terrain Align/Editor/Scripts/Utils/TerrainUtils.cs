using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Rowlan.TerrainAlign
{
    public class TerrainUtils
    {
        /// <summary>
        /// Set terrain height to 0
        /// </summary>
        /// <param name="terrain"></param>
        public static void FlattenTerrain(Terrain terrain)
        {
            SetTerrainHeight(terrain, 0f);
        }

        /// <summary>
        /// Set terrain height to specified height
        /// </summary>
        /// <param name="terrain"></param>
        /// <param name="height"></param>
        public static void SetTerrainHeight(Terrain terrain, float height)
        {

            TerrainData terrainData = terrain.terrainData;

            int w = terrainData.heightmapResolution;
            int h = terrainData.heightmapResolution;
            float[,] allHeights = terrainData.GetHeights(0, 0, w, h);

            float terrainMin = terrain.transform.position.y + 0f;
            float terrainMax = terrain.transform.position.y + terrain.terrainData.size.y;
            float totalHeight = terrainMax - terrainMin;

            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    allHeights[y, x] = height;
                }
            }

            terrain.terrainData.SetHeights(0, 0, allHeights);
        }
    }

}