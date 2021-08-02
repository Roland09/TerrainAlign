using System;
using UnityEngine;
using System.IO;

namespace Rowlan.TerrainAlign
{
    public class FileUtils
    {
        public enum ImageResolution
        {
            _256 = 256,
            _512 = 512,
            _1024 = 1024,
            _2048 = 2048,
            _4096 = 4096,
            _8192 = 8192
        }

        public enum Channel
        {
            Red,
            Green,
            Blue,
            All
        }

        /// <summary>
        /// The color channel to use
        /// </summary>
        public Channel channel = Channel.Red;

        /// <summary>
        /// The resolution of the saved mask image
        /// </summary>
        public ImageResolution imageResolution = ImageResolution._2048; // TODO: currently unused; use in combination with alpha mask creation

        /// <summary>
        /// Relative path to the Assets path
        /// </summary>
        public string texturePath = "Textures";

        /// <summary>
        /// Save the mask image to a file
        /// </summary>
        /// <returns></returns>
        public string SaveTexture(RenderTexture renderTexture, string fileName, bool appendTimestamp)
        {
            // ensure the path exists
            string texturePath = SetupTexturePath();
            string filepath = Path.Combine(texturePath, GetFilename(fileName, appendTimestamp));

            int rw = renderTexture.width;
            int rh = renderTexture.height;

            Texture2D maskTexture = new Texture2D(rw, rh, TextureFormat.R16, false);

            RenderTexture prevRt = RenderTexture.active;
            {
                RenderTexture.active = renderTexture;
                maskTexture.ReadPixels(new Rect(0, 0, rw, rh), 0, 0);
            }
            RenderTexture.active = prevRt;

            // invalidate color channels depending on setting
            // TODO OPTIMIZE using &
            if (channel != Channel.All)
            {
                for (int i = 0; i < maskTexture.width; i++)
                {
                    for (int j = 0; j < maskTexture.height; j++)
                    {
                        Color pixel = maskTexture.GetPixel(i, j);

                        switch (channel)
                        {
                            case Channel.Red:
                                // pixel.r = 0f;
                                pixel.g = 0f;
                                pixel.b = 0f;
                                break;
                            case Channel.Green:
                                pixel.r = 0f;
                                // pixel.g = 0f;
                                pixel.b = 0f;
                                break;
                            case Channel.Blue:
                                pixel.r = 0f;
                                pixel.g = 0f;
                                // pixel.b = 0f;
                                break;
                            case Channel.All:
                                // nothing to do, default is all channels
                                break;
                        }


                        maskTexture.SetPixel(i, j, pixel);
                    }
                }
                maskTexture.Apply();
            }

            // save file
            byte[] bytes = maskTexture.EncodeToPNG();
            System.IO.File.WriteAllBytes(filepath, bytes);

            Debug.Log(string.Format("[<color=blue>File Utils</color>] File saved:\n<color=grey>{0}</color>", filepath));

            return filepath;

        }

        /// <summary>
        /// Filename is a string formatted as "MyScene - 2018.12.09 - 08.12.28.08.png"
        /// </summary>
        /// <returns></returns>
        private static string GetFilename(string fileName, bool appendTimestamp)
        {
            // string objectName = "Texture"; //  SceneManager.GetActiveScene().name;

            if (appendTimestamp)
                return string.Format("{0} - {1:yyyy.MM.dd - HH.mm.ss.ff}.png", fileName, DateTime.Now);
            else
                return string.Format("{0}.png", fileName);
        }

        /// <summary>
        /// Path for the alpha masks: AlphaMasks in parallel to the Assets folder
        /// </summary>
        /// <returns></returns>
        public string GetPath()
        {
            string path = Application.dataPath;

            //path = Path.Combine("Assets", path, texturePath);
            path = path.Substring(0, path.Length - "/Assets".Length);
            path = Path.Combine(path, texturePath);

            return path;
        }

        /// <summary>
        /// Set the screenshot path variable and ensure the path exists.
        /// </summary>
        private string SetupTexturePath()
        {
            string texturePath = GetPath();

            if (!Directory.Exists(texturePath))
            {
                Directory.CreateDirectory(texturePath);
            }

            return texturePath;
        }
    }
}