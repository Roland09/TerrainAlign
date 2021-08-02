using com.rowlan.terrainalign;
using System;
using UnityEngine;

namespace Rowlan.TerrainAlign
{
    [Serializable]
    public class TerrainAlign : MonoBehaviour
    {
        [SerializeField]
        public TerrainAlignSettings settings = new TerrainAlignSettings();

    }
}