/// This class is based on the Unity Technologies Terrain Tools.
/// License:
/// 
///   Terrain Tools copyright © 2020 Unity Technologies ApS
///   Licensed under the Unity Companion License for Unity-dependent projects--see Unity Companion License.
///   Unless expressly provided otherwise, the Software under this license is made available strictly on an “AS IS” BASIS WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED. Please review the license for details on these and other terms and conditions.
///
/// https://docs.unity3d.com/Packages/com.unity.terrain-tools@3.0/license/LICENSE.html
///
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine.Experimental.TerrainAPI;
using UnityEngine;
using UObject = UnityEngine.Object;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

namespace Rowlan.TerrainAlign
{
    public struct ActiveRenderTextureScope : System.IDisposable
    {
        RenderTexture m_Prev;

        public ActiveRenderTextureScope(RenderTexture rt)
        {
            m_Prev = RenderTexture.active;
            RenderTexture.active = rt;
        }

        public void Dispose()
        {
            // restore prev active RT
            RenderTexture.active = m_Prev;
        }
    }

    
    /// <summary>
    /// Utility class for safely managing the lifetime of a RenderTexture
    /// </summary>
    public class RTHandle
    {
        private RenderTexture   m_RT;
        private bool           m_IsTemp;
        
        /// <summary>
        /// The RenderTexture for this RTHandle
        /// </summary>
        public RenderTexture                RT      => m_RT;

        /// <summary>
        /// The descriptor for the RTHandle and RenderTexture
        /// </summary>
        public RenderTextureDescriptor     Desc    => m_RT?.descriptor ?? default;

        internal bool IsTemp => m_IsTemp;

        /// <summary>
        /// The name for the RTHandle and RenderTexture
        /// </summary>
        public string Name
        {
            get => m_RT?.name ?? default;
            set => m_RT.name = value;
        }

        internal RTHandle()
        {

        }

        /// <summary>
        /// Sets the name of the RenderTexture and returns the reference to the RTHandle
        /// </summary>
        /// <param name="name">The name of the underlying RenderTexture</param>
        /// <returns>Reference to the RTHandle</returns>
        public RTHandle WithName(string name)
        {
            Name = name;
            return this;
        }

        public static implicit operator RenderTexture(RTHandle handle)
        {
            return handle.RT;
        }

        public static implicit operator Texture(RTHandle handle)
        {
            return handle.RT;
        }

        internal void SetRenderTexture(RenderTexture rt, bool isTemp)
        {
            m_RT = rt;
            m_IsTemp = isTemp;
        }

        /// <summary>
        /// Structure for handling the lifetime of a RTHandle within a using block. RenderTexture is released when this structure is disposed
        /// </summary>
        public struct RTHandleScope : System.IDisposable
        {
            RTHandle m_Handle;

            internal RTHandleScope(RTHandle handle)
            {
                m_Handle = handle;
            }

            public void Dispose()
            {
                RTUtils.Release(m_Handle);
            }
        }

        /// <summary>
        /// Get a new disposable RTHandleScope instance to use in using blocks
        /// </summary>
        public RTHandleScope Scoped() => new RTHandleScope(this);
    }

    /// <summary>
    /// Utility class for getting and releasing RenderTextures handles.
    /// Lifetimes of these RenderTextures are tracked and any that have not been released within several frames are
    /// regarded as leaked RenderTexture resources and will generate warnings in the Console.
    /// </summary>
    public static class RTUtils
    {
        class Log
        {
            public int Frames;
            public string StackTrace;
        }
        
        internal static bool s_EnableStackTrace = false;
        private static Stack<Log> s_LogPool = new Stack<Log>();
        private static Dictionary<RTHandle, Log> s_Logs = new Dictionary<RTHandle, Log>();

        internal static int s_CreatedHandleCount;
        internal static int s_TempHandleCount;

        private static bool m_AgeCheckAdded;

        private static void AgeCheck()
        {
            if(!m_AgeCheckAdded)
            {
                Debug.LogError("Checking lifetime of RenderTextures but m_AgeCheckAdded = false");
            }

            foreach (var kvp in s_Logs)
            {
                var log = kvp.Value;

                if (log.Frames >= 4)
                {
                    var trace = !s_EnableStackTrace ? string.Empty : "\n" + log.StackTrace;
                    Debug.LogWarning($"RTHandle \"{kvp.Key.Name}\" has existed for more than 4 frames. Possible memory leak.{trace}");
                }
                
                log.Frames++;
            }
        }

        private static void CheckAgeCheck()
        {
            if(s_TempHandleCount != 0 || s_CreatedHandleCount != 0) return;

            Debug.Assert(s_Logs.Count == 0, "Internal RTHandle type counts for temporary and non-temporary RTHandles are both 0 but the containers for tracking leaked RTHandles have counts that are not 0");

            if(!m_AgeCheckAdded)
            {
                EditorApplication.update += AgeCheck;
                m_AgeCheckAdded = true;
            }
            else
            {
                EditorApplication.update -= AgeCheck;
                m_AgeCheckAdded = false;
            }
        }

        private static void AddLogForHandle(RTHandle handle)
        {
            var log = s_LogPool.Any() ? s_LogPool.Pop() : new Log();
            if(s_EnableStackTrace) log.StackTrace = System.Environment.StackTrace;
            s_Logs.Add(handle, log);
        }
        
        /// <summary>
        /// Get a RenderTextureDescriptor set up for RenderTexture operations on GPU
        /// <param name="width">Width of the RenderTexture</param>
        /// <param name="height">Height of the RenderTexture</param>
        /// <param name="format">RenderTextureFormat of the RenderTexture</param>
        /// <param name="depth">Depth of the RenderTexture</param>
        /// <param name="mipCount">MipCount of the RenderTexture. Default is 0</param>
        /// <param name="srgb">Flag determining whether RenderTextures created using this descriptor should be sRGB or Linear space</param>
        /// </summary>
        public static RenderTextureDescriptor GetDescriptor(int width, int height, int depth, RenderTextureFormat format, int mipCount = 0, bool srgb = false)
        {
            return GetDescriptor(width, height, depth, GraphicsFormatUtility.GetGraphicsFormat(format, srgb), mipCount, srgb);
        }

        /// <summary>
        /// Get a RenderTextureDescriptor set up for RenderTexture operations on GPU with the enableRandomWrite flag set to true
        /// <param name="width">Width of the RenderTexture</param>
        /// <param name="height">Height of the RenderTexture</param>
        /// <param name="format">RenderTextureFormat of the RenderTexture</param>
        /// <param name="depth">Depth of the RenderTexture</param>
        /// <param name="mipCount">MipCount of the RenderTexture. Default is 0</param>
        /// <param name="srgb">Flag determining whether RenderTextures created using this descriptor should be sRGB or Linear space</param>
        /// </summary>
        public static RenderTextureDescriptor GetDescriptorRW(int width, int height, int depth, RenderTextureFormat format, int mipCount = 0, bool srgb = false)
        {
            return GetDescriptorRW(width, height, depth, GraphicsFormatUtility.GetGraphicsFormat(format, srgb), mipCount, srgb);
        }
        
        /// <summary>
        /// Get a RenderTextureDescriptor set up for RenderTexture operations on GPU with the enableRandomWrite flag set to true
        /// <param name="width">Width of the RenderTexture</param>
        /// <param name="height">Height of the RenderTexture</param>
        /// <param name="format">GraphicsFormat of the RenderTexture</param>
        /// <param name="depth">Depth of the RenderTexture</param>
        /// <param name="mipCount">MipCount of the RenderTexture. Default is 0</param>
        /// <param name="srgb">Flag determining whether RenderTextures created using this descriptor should be sRGB or Linear space</param>
        /// </summary>
        public static RenderTextureDescriptor GetDescriptorRW(int width, int height, int depth, GraphicsFormat format, int mipCount = 0, bool srgb = false)
        {
            var desc = GetDescriptor(width, height, depth, format, mipCount, srgb);
            desc.enableRandomWrite = true;
            return desc;
        }

        /// <summary>
        /// Get a RenderTextureDescriptor set up for RenderTexture operations on GPU
        /// <param name="width">Width of the RenderTexture</param>
        /// <param name="height">Height of the RenderTexture</param>
        /// <param name="format">RenderTextureFormat of the RenderTexture</param>
        /// <param name="depth">Depth of the RenderTexture</param>
        /// <param name="mipCount">MipCount of the RenderTexture. Default is 0</param>
        /// <param name="srgb">Flag determining whether RenderTextures created using this descriptor should be sRGB or Linear space</param>
        /// </summary>
        public static RenderTextureDescriptor GetDescriptor(int width, int height, int depth, GraphicsFormat format, int mipCount = 0, bool srgb = false)
        {
            var desc = new RenderTextureDescriptor(width, height, format, depth)
            {
                sRGB = srgb,
                mipCount = mipCount,
                useMipMap = mipCount != 0,
            };

            return desc;
        }

        private static RTHandle GetHandle(RenderTextureDescriptor desc, bool isTemp)
        {
            CheckAgeCheck();

            if (isTemp) s_TempHandleCount++;
            else s_CreatedHandleCount++;
            
            var handle = new RTHandle();
            handle.SetRenderTexture(isTemp ? RenderTexture.GetTemporary(desc) : new RenderTexture(desc), isTemp);
            AddLogForHandle(handle);
            
            return handle;
        }
        
        /// <summary>
        /// Get a RTHandle for a RenderTexture acquired from RenderTexture.GetTemporary. Free using RTUtils.Release
        /// <param name="desc">RenderTextureDescriptor for the RenderTexture</param>
        /// </summary>
        public static RTHandle GetTempHandle(RenderTextureDescriptor desc)
        {
            return GetHandle(desc, true);
        }
        
        /// <summary>
        /// Get a RTHandle for a RenderTexture acquired with RenderTexture.GetTemporary. Free using RTUtils.Release
        /// </summary>
        /// <param name="width">Width of the RenderTexture</param>
        /// <param name="height">Height of the RenderTexture</param>
        /// <param name="depth">Depth of the RenderTexture</param>
        /// <param name="format">Format of the RenderTexture</param>
        public static RTHandle GetTempHandle(int width, int height, int depth, GraphicsFormat format)
        {
            return GetHandle(GetDescriptor(width, height, depth, format), true);
        }

        /// <summary>
        /// Get a RTHandle for a RenderTexture acquired with 'new RenderTexture(desc)'. Free using RTUtils.Release
        /// <param name="desc">RenderTextureDescriptor for the RenderTexture</param>
        /// </summary>
        public static RTHandle GetNewHandle(RenderTextureDescriptor desc)
        {
            return GetHandle(desc, false);
        }
        
        /// <summary>
        /// Get a RTHandle for a RenderTexture acquired with 'new RenderTexture(desc)'. Free using RTUtils.Release
        /// </summary>
        /// <param name="width">Width of the RenderTexture</param>
        /// <param name="height">Height of the RenderTexture</param>
        /// <param name="depth">Depth of the RenderTexture</param>
        /// <param name="format">Format of the RenderTexture</param>
        /// <returns></returns>
        public static RTHandle GetNewHandle(int width, int height, int depth, GraphicsFormat format)
        {
            return GetHandle(GetDescriptor(width, height, depth, format), false);
        }

        /// <summary>
        /// Release the RenderTexture resource associated with the specified RTHandle
        /// <param name="handle">RTHandle from which RenderTexture resources will be released</param>
        /// </summary>
        public static void Release(RTHandle handle)
        {
            if(handle.RT == null) return;
            
            if(!s_Logs.ContainsKey(handle)) throw new InvalidOperationException("Attemping to release a RTHandle that is not currently tracked by the system. This should never happen");

            var log = s_Logs[handle];
            s_Logs.Remove(handle);
            log.Frames = 0;
            log.StackTrace = null;
            
            s_LogPool.Push(log);

            if(handle.IsTemp)
            {
                --s_TempHandleCount;
                RenderTexture.ReleaseTemporary(handle.RT);
            }
            else
            {
                --s_CreatedHandleCount;
                handle.RT.Release();
                Destroy(handle.RT);
            }
            
            CheckAgeCheck();
        }

        /// <summary>
        /// Destroy a RenderTexture created using 'new RenderTexture()'
        /// <param name="rt">RenderTexture to destroy</param>
        /// </summary>
        public static void Destroy(RenderTexture rt)
        {
            if(rt == null) return;

#if UNITY_EDITOR
            if (Application.isPlaying)
                UObject.Destroy(rt);
            else
                UObject.DestroyImmediate(rt);
#else
            UObject.Destroy(rt);
#endif
        }

        /// <summary>
        /// Get the number of RTHandles that have been requested and not released yet.
        /// </summary>
        /// <returns>Number of RTHandles that have been requested and not released</returns>
        public static int GetHandleCount() => s_Logs.Count;
    }

    public static class Utility
    {
        static Material m_DefaultPreviewMat = null;
        public static Material GetDefaultPreviewMaterial()
        {
            if (m_DefaultPreviewMat == null)
            {
                m_DefaultPreviewMat = new Material(Shader.Find("Hidden/TerrainTools/BrushPreview"));
            }
            return m_DefaultPreviewMat;
        }

        private static Material m_TexelValidityMaterial;
        private static Material GetTexelValidityMaterial()
        {
            if(m_TexelValidityMaterial == null)
            {
                m_TexelValidityMaterial = new Material(Shader.Find("Hidden/TerrainTools/TexelValidityBlit"));
            }

            return m_TexelValidityMaterial;
        }

        public static void SetupMaterialForPainting(PaintContext paintContext, BrushTransform brushTransform, Material material)
        {
            TerrainPaintUtility.SetupTerrainToolMaterialProperties(paintContext, brushTransform, material);
            
            material.SetVector("_Heightmap_Tex",
	            new Vector4(
		            1f / paintContext.targetTextureWidth,
		            1f / paintContext.targetTextureHeight,
		            paintContext.targetTextureWidth,
		            paintContext.targetTextureHeight)
            );

            material.SetVector("_PcPixelRect",
	            new Vector4(paintContext.pixelRect.x,
		            paintContext.pixelRect.y,
		            paintContext.pixelRect.width,
		            paintContext.pixelRect.height)
            );

            material.SetVector("_PcUvVectors",
	            new Vector4(paintContext.pixelRect.x / (float)paintContext.targetTextureWidth,
				            paintContext.pixelRect.y / (float)paintContext.targetTextureHeight,
				            paintContext.pixelRect.width / (float)paintContext.targetTextureWidth,
				            paintContext.pixelRect.height / (float)paintContext.targetTextureHeight)
            );
        }

        public static void SetupMaterialForPaintingWithTexelValidityContext(PaintContext paintContext, PaintContext texelCtx, BrushTransform brushTransform, Material material)
        {
            SetupMaterialForPainting(paintContext, brushTransform, material);
            material.SetTexture("_PCValidityTex", texelCtx.sourceRenderTexture);
        }

        public static PaintContext CollectTexelValidity(Terrain terrain, Rect boundsInTerrainSpace, int extraBorderPixels = 0)
        {
            var res = terrain.terrainData.heightmapResolution;
            // use holes format because we really only need to know if the texel value is 0 or 1
            var ctx = PaintContext.CreateFromBounds(terrain, boundsInTerrainSpace, res, res, extraBorderPixels);
            ctx.CreateRenderTargets(Terrain.holesRenderTextureFormat);
            ctx.Gather(
                t => t.terrain.terrainData.heightmapTexture, // just provide heightmap texture. no need to create a temp one
                new Color(0, 0, 0, 0),
                blitMaterial : GetTexelValidityMaterial()
            );

            return ctx;
        }
        
        //assume this a 1D texture that has already been created
        public static Vector2 AnimationCurveToRenderTexture(AnimationCurve curve, ref Texture2D tex) {

            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            float val = curve.Evaluate(0.0f);
            Vector2 range = new Vector2(val, val);

            Color[] pixels = new Color[tex.width * tex.height];
            pixels[0].r = val;
            for (int i = 1; i < tex.width; i++) {
                float pct = (float)i / (float)tex.width;
                pixels[i].r = curve.Evaluate(pct);
                range[0] = Mathf.Min(range[0], pixels[i].r);
                range[1] = Mathf.Max(range[1], pixels[i].r);
            }
            tex.SetPixels(pixels);
            tex.Apply();

            return range;
        }

        /// <summary>
        /// Set the filter render texture for transformation brushes
        /// </summary>
        public static void SetFilterRT(IBrushUIGroup commonUI, RenderTexture sourceRenderTexture, RenderTexture destinationRenderTexture, Material mat)
        {
            commonUI.GetBrushMask(sourceRenderTexture, destinationRenderTexture);
            mat.SetTexture("_FilterTex", destinationRenderTexture);
        }
    }

    public static class MeshUtils
    {
        public enum ShaderPass
        {
            Height = 0,
            Mask = 1,
        }

        private static Material m_defaultProjectionMaterial;
        public static Material defaultProjectionMaterial
        {
            get
            {
                if( m_defaultProjectionMaterial == null )
                {
                    m_defaultProjectionMaterial = new Material( Shader.Find( "Hidden/Rowlan/TerrainAlign/MeshUtility" ) );
                }

                return m_defaultProjectionMaterial;
            }
        }

        public static Quaternion QuaternionFromMatrix(Matrix4x4 m)
        {
            // Adapted from: http://www.euclideanspace.com/maths/geometry/rotations/conversions/matrixToQuaternion/index.htm
            Quaternion q = new Quaternion();
            q.w = Mathf.Sqrt( Mathf.Max( 0, 1 + m[0,0] + m[1,1] + m[2,2] ) ) / 2; 
            q.x = Mathf.Sqrt( Mathf.Max( 0, 1 + m[0,0] - m[1,1] - m[2,2] ) ) / 2; 
            q.y = Mathf.Sqrt( Mathf.Max( 0, 1 - m[0,0] + m[1,1] - m[2,2] ) ) / 2; 
            q.z = Mathf.Sqrt( Mathf.Max( 0, 1 - m[0,0] - m[1,1] + m[2,2] ) ) / 2; 
            q.x *= Mathf.Sign( q.x * ( m[2,1] - m[1,2] ) );
            q.y *= Mathf.Sign( q.y * ( m[0,2] - m[2,0] ) );
            q.z *= Mathf.Sign( q.z * ( m[1,0] - m[0,1] ) );
            return q;
        }

        public static Bounds TransformBounds( Matrix4x4 m, Bounds bounds )
        {
            Vector3[] points = new Vector3[ 8 ];

            // get points for each corner of the bounding box
            points[ 0 ] = new Vector3( bounds.max.x, bounds.max.y, bounds.max.z );
            points[ 1 ] = new Vector3( bounds.min.x, bounds.max.y, bounds.max.z );
            points[ 2 ] = new Vector3( bounds.max.x, bounds.min.y, bounds.max.z );
            points[ 3 ] = new Vector3( bounds.max.x, bounds.max.y, bounds.min.z );
            points[ 4 ] = new Vector3( bounds.min.x, bounds.min.y, bounds.max.z );
            points[ 5 ] = new Vector3( bounds.min.x, bounds.min.y, bounds.min.z );
            points[ 6 ] = new Vector3( bounds.max.x, bounds.min.y, bounds.min.z );
            points[ 7 ] = new Vector3( bounds.min.x, bounds.max.y, bounds.min.z );

            Vector3 min = Vector3.one * float.PositiveInfinity;
            Vector3 max = Vector3.one * float.NegativeInfinity;

            for( int i = 0; i < points.Length; ++i )
            {
                Vector3 p = m.MultiplyPoint( points[ i ] );

                // update min values
                if( p.x < min.x )
                {
                    min.x = p.x;
                }

                if( p.y < min.y )
                {
                    min.y = p.y;
                }

                if( p.z < min.z )
                {
                    min.z = p.z;
                }

                // update max values
                if( p.x > max.x )
                {
                    max.x = p.x;
                }

                if( p.y > max.y )
                {
                    max.y = p.y;
                }

                if( p.z > max.z )
                {
                    max.z = p.z;
                }
            }

            return new Bounds() { max = max, min = min };
        }

        private static string GetPrettyVectorString( Vector3 v )
        {
            return string.Format( "( {0}, {1}, {2} )", v.x, v.y, v.z );
        }

        public static void RenderTopdownProjection(Mesh mesh, Matrix4x4 model, RenderTexture destination, Material mat, ShaderPass pass)
        {
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = destination;

            Bounds modelBounds = TransformBounds(model, mesh.bounds);

            float nearPlane = (modelBounds.max.y - modelBounds.center.y) * 4;
            float farPlane = (modelBounds.min.y - modelBounds.center.y);

            Vector3 viewFrom = new Vector3(modelBounds.center.x, modelBounds.center.z, -modelBounds.center.y);
            Vector3 viewTo = viewFrom + Vector3.down;
            Vector3 viewUp = Vector3.forward;

            //             Debug.Log(
            // $@"Bounds =
            // [
            //     center: { modelBounds.center }
            //     max: { modelBounds.max }
            //     extents: { modelBounds.extents }
            // ]
            // nearPlane: { nearPlane }
            // farPlane: { farPlane }
            // diff: { nearPlane - farPlane }
            // view: [ from = { GetPrettyVectorString( viewFrom ) }, to = { GetPrettyVectorString( viewTo ) }, up = { GetPrettyVectorString( viewUp ) } ]"
            //             );

            // reset the view to accomodate for the transformed bounds
            Matrix4x4 view = Matrix4x4.LookAt(viewFrom, viewTo, viewUp);
            Matrix4x4 proj = Matrix4x4.Ortho(-1, 1, -1, 1, nearPlane, farPlane);
            Matrix4x4 mvp = proj * view * model;

            GL.Clear(true, true, Color.black);

            mat.SetMatrix("_Matrix_M", model);
            mat.SetMatrix("_Matrix_MV", view * model);
            mat.SetMatrix("_Matrix_MVP", mvp);

            mat.SetPass((int)pass);
            GL.PushMatrix();
            {
                Graphics.DrawMeshNow(mesh, Matrix4x4.identity);
            }
            GL.PopMatrix();

            RenderTexture.active = prev;
        }
    }
}