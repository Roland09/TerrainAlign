using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace com.rowlan.terrainalign
{
    [Serializable]
    public class TerrainAlignSettings 
    {
        [Serializable]
        public class BlurSettings
        {

            [Range(0f, 0.5f)]
            public float blurSize = 0f;

            [Range(1, 100)]
            public int blurSamples = 10;

            public bool gauss = false;

            [Range(0.00f, 0.3f)]
            public float gaussStandardDeviation = 0.02f;
        }

        /// <summary>
        /// These enum ids correspond to the shader constants
        /// TODO: find unified solution, ie if something changing here doesn't break the shader settings
        /// </summary>
        public enum BlendMode
        {
            Exclusive,
            Value,
            Add,
        }

        public enum Direction
        {
            TopDown,
            BottomUp
        }

        public bool featureEnabled = true;

        /// <summary>
        /// The terrain to perform the operations on
        /// </summary>
        public Terrain terrain;

        /// <summary>
        /// The direction the projection camera is facing
        /// </summary>
        public Direction direction = Direction.TopDown;

        /// <summary>
        /// Cut-off for bottom-up projection. Currently manual. You don't want to eg have a roof considered in the bottom-up projection
        /// </summary>
        [Range(0, 1)]
        public float bottomUpCutOff = 0.2f;

        /// <summary>
        /// Optional y offset on the projection. If you project eg a road, you want the road above of the terrain.
        /// This increases the y size of the camera projection
        /// </summary>
        public float terrainOffsetY = 0f;

        public BlendMode blendMode = BlendMode.Exclusive;

        [Tooltip("The factor used in blend mode Value")]
        [Range(0f, 1f)]
        public float valueBlend = 0.5f;

        public bool blur = false;

        public BlurSettings blurSettings = new BlurSettings();


        public bool debug = true;
    }
}
