using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using OpenTK.Graphics.OpenGL;
using OpenVROverlayPipe.Extensions;
using OpenVROverlayPipe.Input;

namespace OpenVROverlayPipe.Notification
{
    [SupportedOSPlatform("windows7.0")]
    internal class Texture : IDisposable
    {
        private bool _disposed;
        private int[] _frameTimes;
        public int Width { get; }
        public int Height { get; }
        public int TextureDepth { get; }
        public int TextureId { get; }

        public Texture(int textureId, int width, int height, int textureDepth = 0, int[]? frameTimes = null)
        {
            TextureId = textureId;
            Width = width;
            Height = height;
            TextureDepth = textureDepth;
            _frameTimes = frameTimes ?? [];
        }
        
        public static Texture? LoadImageFile(string path, InputDataOverlay? input = null)
        {
            Bitmap image;
            try
            {
                image = new Bitmap(path);
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Texture: Exception loading image file: {e.Message}");
                return null;
            }
            return LoadImage(image, input);
        }
        
        public static Texture? LoadImageBase64(string bytes, InputDataOverlay? input = null)
        {
            return LoadImageBytes(Convert.FromBase64String(bytes), input);
        }
        
        public static Texture? LoadImageBytes(byte[] bytes, InputDataOverlay? input = null)
        {
            Bitmap image;
            try
            {
                image = new Bitmap(new MemoryStream(bytes));
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Texture: Exception loading image bytes: {e.Message}");
                return null;
            }            
            return LoadImage(image, input);
        }
        
        public static Texture? LoadImage(Bitmap image, InputDataOverlay? input = null)
        {
            var frameCount = image.FrameDimensionsList.Any(d => d == FrameDimension.Time.Guid)
                ? image.GetFrameCount(FrameDimension.Time)
                : 1;

            Debug.WriteLine($"Texture: The image has {frameCount} frames.");

            if (frameCount <= 0) return null;
            
            var frames = new Bitmap[frameCount];
            var frameTimes = new int[frameCount];

            if (frameCount > 1)
            {
                var times = image.GetPropertyItem(0x5100)?.Value ?? [];
                for (var i = 0; i < frameCount; i++)
                {
                    var dur = BitConverter.ToInt32(times, (i * 4) % times.Length) * 10;
                    frameTimes[i] = dur;
                    image.SelectActiveFrame(FrameDimension.Time, i);
                    frames[i] = new Bitmap(image.Size.Width, image.Size.Height);
                    var g = Graphics.FromImage(frames[i]);
                    g.DrawImage(image, new Point(0, 0));
                    g.Dispose();
                    if (input?.TextAreas is not null) frames[i].DrawTextAreas(input.TextAreas, input.SideBySide3D);
                    frames[i].RotateFlip(RotateFlipType.RotateNoneFlipY);
                }
            }

            var textureId = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2DArray, textureId);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapS, (int) TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapT, (int) TextureWrapMode.ClampToEdge);

            var depth = frameCount;
            GL.TexStorage3D(TextureTarget3d.Texture2DArray, 1, SizedInternalFormat.Rgba8, image.Width, image.Height, depth);
            GL.PixelStore(PixelStoreParameter.UnpackRowLength, image.Width);
                
            if (frameCount > 1)
            {
                for (var i = 0; i < depth; i++)
                {
                    // image.SelectActiveFrame(FrameDimension.Time, i);
                    var data = frames[i].LockBits(
                        new System.Drawing.Rectangle(0, 0, image.Width, image.Height),
                        ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                    GL.TexSubImage3D(
                        // 4 bytes in an Bgra value.
                        TextureTarget.Texture2DArray,
                        0, 0, 0, i, image.Size.Width, image.Size.Height, 1,
                        OpenTK.Graphics.OpenGL.PixelFormat.Bgra, PixelType.UnsignedByte,
                        data.Scan0
                    ); 
                    frames[i].UnlockBits(data);
                }
            }
            else
            {
                if (input?.TextAreas is not null) image.DrawTextAreas(input.TextAreas, input.SideBySide3D);
                image.RotateFlip(RotateFlipType.RotateNoneFlipY);
                var data = image.LockBits(
                    new System.Drawing.Rectangle(0, 0, image.Width, image.Height),
                    ImageLockMode.ReadOnly, 
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb
                );
                GL.TexSubImage3D(
                    // 4 bytes in an Bgra value.
                    TextureTarget.Texture2DArray,
                    0, 0, 0, 0, image.Size.Width, image.Size.Height, 1,
                    OpenTK.Graphics.OpenGL.PixelFormat.Bgra, PixelType.UnsignedByte,
                    data.Scan0
                ); 
                image.UnlockBits(data);
            }
            return new Texture(textureId, image.Width,  image.Height, depth, frameTimes);
        }
        
        public void Bind()
        {
            GL.BindTexture(TextureTarget.Texture2DArray, TextureId);
        }

        public int Duration => _frameTimes.Sum();

        public int GetFrame(double time)
        {
            if (TextureDepth == 1) return 0;
            
            var elapsedTime = (int)((time * 1000) + 0.5) % Duration;
            var totalTime = 0;
            for (var i = 0; i < TextureDepth; i++)
            {
                totalTime += _frameTimes[i];
                if (elapsedTime < totalTime)
                {
                    return i;
                }
            }
            return 0;
        }

        public void Dispose()
        {
            if (_disposed) return;
            MainController.UiDispatcher?.Invoke(() =>
            {
                GL.DeleteTexture(TextureId);
            });
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}