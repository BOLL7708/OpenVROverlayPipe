using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using OpenTK.Graphics.OpenGL;
using OpenVRNotificationPipe.Extensions;

namespace OpenVRNotificationPipe.Notification
{
    internal class Texture : IDisposable
    {
        private bool _disposed = false;
        private int[] _frameTimes;
        public int Width { get; }
        public int Height { get; }
        public int TextureDepth { get; }
        public int TextureId { get; }

        public Texture(int textureId, int width, int height, int textureDepth = 0, int[] frameTimes = null)
        {
            TextureId = textureId;
            Width = width;
            Height = height;
            TextureDepth = textureDepth;
            _frameTimes = frameTimes;
        }
        
        public static Texture LoadImageFile(string path, IEnumerable<Payload.TextAreaObject> textAreas = null)
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
            return LoadImage(image, textAreas);
        }
        
        public static Texture LoadImageBase64(string bytes, IEnumerable<Payload.TextAreaObject> textAreas = null)
        {
            return LoadImageBytes(Convert.FromBase64String(bytes), textAreas);
        }
        
        public static Texture LoadImageBytes(byte[] bytes, IEnumerable<Payload.TextAreaObject> textAreas = null)
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
            return LoadImage(image, textAreas);
        }
        
        public static Texture LoadImage(Bitmap image, IEnumerable<Payload.TextAreaObject> textAreas = null)
        {
            int frameCount = image.FrameDimensionsList.Any(d => d == FrameDimension.Time.Guid)
                ? image.GetFrameCount(FrameDimension.Time)
                : 1;

            Debug.WriteLine($"Texture: The image has {frameCount} frames.");

            if (frameCount > 0)
            {
                Bitmap[] Frames = new Bitmap[frameCount];
                int[] frameTimes = new int[frameCount];

                if (frameCount > 1)
                {
                    byte[] times = image.GetPropertyItem(0x5100).Value;
                    for (int i = 0; i < frameCount; i++)
                    {
                        int dur = BitConverter.ToInt32(times, (i * 4) % times.Length) * 10;
                        frameTimes[i] = dur;
                        image.SelectActiveFrame(FrameDimension.Time, i);
                        Frames[i] = new Bitmap(image.Size.Width, image.Size.Height);
                        var g = Graphics.FromImage(Frames[i]);
                        g.DrawImage(image, new Point(0, 0));
                        g.Dispose();
                        if (!(textAreas is null)) Frames[i].DrawTextAreas(textAreas);
                        Frames[i].RotateFlip(RotateFlipType.RotateNoneFlipY);
                    }
                }

                var textureId = GL.GenTexture();
                GL.BindTexture(TextureTarget.Texture2DArray, textureId);
                GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.Nearest);
                GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Nearest);
                GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapS, (int) TextureWrapMode.ClampToEdge);
                GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapT, (int) TextureWrapMode.ClampToEdge);

                int depth = frameCount;
                GL.TexStorage3D(TextureTarget3d.Texture2DArray, 1, SizedInternalFormat.Rgba8, image.Width, image.Height, depth);
                GL.PixelStore(PixelStoreParameter.UnpackRowLength, image.Width);
                
                if (frameCount > 1)
                {
                    for (int i = 0; i < depth; i++)
                    {
                        // image.SelectActiveFrame(FrameDimension.Time, i);
                        BitmapData data = Frames[i].LockBits(
                            new System.Drawing.Rectangle(0, 0, image.Width, image.Height),
                            ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                        GL.TexSubImage3D(
                            // 4 bytes in an Bgra value.
                            TextureTarget.Texture2DArray,
                            0, 0, 0, i, image.Size.Width, image.Size.Height, 1,
                            OpenTK.Graphics.OpenGL.PixelFormat.Bgra, PixelType.UnsignedByte,
                            data.Scan0
                        ); 
                        Frames[i].UnlockBits(data);
                    }
                }
                else
                {
                    if (!(textAreas is null)) image.DrawTextAreas(textAreas);
                    image.RotateFlip(RotateFlipType.RotateNoneFlipY);
                    BitmapData data = image.LockBits(
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
            return null;
        }
        
        public void Bind()
        {
            GL.BindTexture(TextureTarget.Texture2DArray, TextureId);
        }

        public int Duration => _frameTimes.Sum();

        public int GetFrame(double time)
        {
            if (TextureDepth == 1) return 0;
            
            int elapsedTime = (int)((time * 1000) + 0.5) % Duration;
            int totalTime = 0;
            for (int i = 0; i < TextureDepth; i++)
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
            MainController.UiDispatcher.Invoke(() =>
            {
                GL.DeleteTexture(TextureId);
            });
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}