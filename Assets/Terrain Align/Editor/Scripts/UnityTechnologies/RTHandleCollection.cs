/// This class is based on the Unity Technologies Terrain Tools.
/// License:
/// 
///   Terrain Tools copyright © 2020 Unity Technologies ApS
///   Licensed under the Unity Companion License for Unity-dependent projects--see Unity Companion License.
///   Unless expressly provided otherwise, the Software under this license is made available strictly on an “AS IS” BASIS WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED. Please review the license for details on these and other terms and conditions.
///
/// https://docs.unity3d.com/Packages/com.unity.terrain-tools@3.0/license/LICENSE.html
///
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Rowlan.TerrainAlign
{
    /// <summary>
    /// Collection class for mapping string and integer values to RTHandles
    /// </summary>
    public class RTHandleCollection : System.IDisposable
    {
        private bool m_Disposed;
        private Dictionary< int, RTHandle > m_Handles;
        private Dictionary< int, GraphicsFormat > m_Formats;
        private Dictionary< string, int > m_NameToHash;
        private Dictionary< int, string > m_HashToName;
        private List< int > m_Hashes;

        /// <summary>
        /// Access a RTHandle using an integer hash
        /// </summary>
        public RTHandle this[ int hash ]
        {
            get
            {
                if( m_Handles.ContainsKey( hash ) )
                {
                    return m_Handles[ hash ];
                }

                return null;
            }

            set
            {
                m_Handles[ hash ] = value;
            }
        }

        /// <summary>
        /// Access a RTHandle using a string value
        /// </summary>
        public RTHandle this[ string name ]
        {
            get
            {
                if( m_NameToHash.ContainsKey( name ) )
                {
                    return m_Handles[ m_NameToHash[ name ] ];
                }

                return null;
            }

            set
            {
                m_Handles[ m_NameToHash[ name ] ] = value;
            }
        }
        
        /// <summary>
        /// RTHandleCollection constructor
        /// </summary>
        public RTHandleCollection()
        {
            m_Handles = new Dictionary< int, RTHandle >();
            m_Formats = new Dictionary< int, GraphicsFormat >();
            m_NameToHash = new Dictionary< string, int >();
            m_HashToName = new Dictionary< int, string >();
            m_Hashes = new List< int >();
        }

        /// <summary>
        /// Add a RTHandle description to the RTHandleCollection for later use when calling GatherRTHandles
        /// <param name="hash">The hash or integer value used to identify the RTHandle</param>
        /// <param name="name">The name used to identify the RTHandle</param>
        /// <param name="format">The GraphicsFormat to use for the RTHandle description</param>
        /// </summary>
        public void AddRTHandle( int hash, string name, GraphicsFormat format )
        {
            if( !m_Handles.ContainsKey( hash ) )
            {
                m_NameToHash.Add( name, hash );
                m_HashToName.Add( hash, name );
                m_Handles.Add( hash, null );
                m_Formats.Add( hash, format );
                m_Hashes.Add( hash );
            }
            else
            {
                // if the RTHandle already exists, assume they are changing the descriptor
                m_Formats[ hash ] = format;
                m_NameToHash[ name ] = hash;
                m_HashToName[ hash ] = name;
            }
        }

        /// <summary>
        /// Check to see if a RTHandle with the provided name exists already
        /// <param name="name">The name used to identify a RTHandle in this RTHandleCollection</param>
        /// </summary>
        public bool ContainsRTHandle( string name )
        {
            return m_NameToHash.ContainsKey( name );
        }
        
        /// <summary>
        /// Check to see if a RTHandle with the provided hash value exists already
        /// <param name="hash">The hash or integer value used to identify a RTHandle in this RTHandleCollection</param>
        /// <returns>The RTHandle reference associated with the provided hash or integer value. NULL if the key is not found</returns>
        /// </summary>
        public RTHandle GetRTHandle( int hash )
        {
            if(m_Handles.ContainsKey( hash ))
            {
                return m_Handles[ hash ];
            }

            return null;
        }

        /// <summary>
        /// Gather/Create all added RTHandles using the provided width, height, and depth value, if provided
        /// <param name="width">The width of the RTHandles to gather</param>
        /// <param name="height">The height of the RTHandles to gather</param>
        /// <param name="depth">The optional depth of the RTHandles to gather</param>
        /// <returns></returns>
        /// </summary>
        public void GatherRTHandles( int width, int height, int depth = 0 )
        {
            foreach( int key in m_Hashes )
            {
                var desc = new RenderTextureDescriptor( width, height, m_Formats[ key ], depth );
                m_Handles[ key ] = RTUtils.GetNewHandle( desc );
                m_Handles[ key ].RT.Create();
            }
        }

        /// <summary>
        /// Release the RTHandle resources that have been gathered
        /// </summary>
        public void ReleaseRTHandles()
        {
            foreach( int key in m_Hashes )
            {
                if( m_Handles[ key ] != null )
                {
                    var handle = m_Handles[ key ];
                    RTUtils.Release( handle );
                    m_Handles[ key ] = null;
                }
            }
        }

        /// <summary>
        /// Render debug GUI in the SceneView that displays all the RTHandles in this RTHandleCollection
        /// <param name="size">The size that is used to draw the textures</param>
        /// </summary>
        public void OnSceneGUI( float size )
        {
            const float padding = 10;

            Handles.BeginGUI();
            {
                Color prev = GUI.color;
                Rect rect = new Rect( padding, padding, size, size );

                foreach( KeyValuePair<int, RTHandle> p in m_Handles )
                {
                    GUI.color = new Color( 1, 0, 1, 1 );
                    GUI.DrawTexture( rect, Texture2D.whiteTexture, ScaleMode.ScaleToFit );

                    GUI.color = Color.white;
                    if(p.Value != null)
                    {
                        GUI.DrawTexture( rect, p.Value, ScaleMode.ScaleToFit, false );
                    }
                    else
                    {
                        GUI.Label( rect, "NULL" );
                    }

                    Rect labelRect = rect;
                    labelRect.y = rect.yMax;
                    labelRect.height = EditorGUIUtility.singleLineHeight;
                    GUI.Box( labelRect, m_HashToName[ p.Key ], Styles.box );

                    rect.y += padding + size + EditorGUIUtility.singleLineHeight;

                    if( rect.yMax + EditorGUIUtility.singleLineHeight > Screen.height - EditorGUIUtility.singleLineHeight * 2 )
                    {
                        rect.y = padding;
                        rect.x = rect.xMax + padding;
                    }
                }

                GUI.color = prev;
            }
            Handles.EndGUI();
        }

        /// <summary>
        /// Dispose method for this class
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Overridable Dispose method for this class. Override this if you create a class that derives from RTHandleCollection
        /// <param name="dispose">Whether or not resources should be disposed</param>
        /// </summary>
        public virtual void Dispose(bool dispose)
        {
            if(m_Disposed) return;
            
            if(!dispose) return;

            ReleaseRTHandles();
            m_Handles.Clear();

            m_Disposed = true;
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
    }
}
