using System;
using UnityEngine;

namespace Rowlan.TerrainAlign
{
    public class TerrainAlign : MonoBehaviour
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

        public bool featureEnabled = true;

        /// <summary>
        /// The terrain to perform the operations on
        /// </summary>
        public Terrain terrain;

        public BlendMode blendMode = BlendMode.Exclusive;

        [Tooltip("The factor used in blend mode Value")]
        [Range(0f, 1f)]
        public float valueBlend = 0.5f;

        public bool blur = false;

        public BlurSettings blurSettings = new BlurSettings();

        public bool debug = true;

    }
}