using System;
using Valve.VR;
using OpenTK.Graphics.OpenGL;
using System.Diagnostics;
using OpenTK;
using System.Drawing;
using System.IO;

namespace OpenVRNotificationPipe.Notification
{
    class Texture
    {
        private GameWindow _glWindow; // So GL will work at all
        private IntPtr _textureId = IntPtr.Zero;
        private ulong _overlayHandle = 0;
        private readonly int _textureMaxSide = 1024; // What seems like a good idea right now.
        
        public Texture(ulong overlayHandle) {
            _overlayHandle = overlayHandle;
        }

        /**
         * This got complicated due to SteamVR not using new textures even with the old ones deleted,
         * which is why we create an empty image at the maximum size allowed, and then we draw the
         * incoming bitmap onto it, then tighten the UV to the original image dimensions. Phew.
         */
        public HmdVector2_t Load(string base64png)
        {
            if (_glWindow == null) _glWindow = new GameWindow(); // Init OpenGL
            
            var size = new HmdVector2_t();
            try
            {
                // Loading image from incoming base64 encoded string
                Debug.WriteLine($"Image hash: {MainController.CreateMD5(base64png)}");
                var imageBytes = Convert.FromBase64String(base64png);
                var bmp = new Bitmap(new MemoryStream(imageBytes));
                Debug.WriteLine($"Bitmap size: {bmp.Size.ToString()}");
                bmp.RotateFlip(RotateFlipType.RotateNoneFlipY); // Flip it for OpenGL
                if(bmp.Width > _textureMaxSide || bmp.Height > _textureMaxSide)
                {
                    if (bmp.Width > bmp.Height) 
                    {
                        size.v0 = _textureMaxSide;
                        size.v1 = (float) _textureMaxSide / bmp.Width * bmp.Height;
                    } 
                    else
                    {
                        size.v0 = (float) _textureMaxSide / bmp.Height * bmp.Width;
                        size.v1 = _textureMaxSide;
                    }
                } 
                else
                {
                    size.v0 = bmp.Width;
                    size.v1 = bmp.Height;
                }

                // Initiate canvas to draw incoming bitmap on to match original size, 
                // this is the to avoid the texture not updating problem.
                var bmpMax = new Bitmap(_textureMaxSide, _textureMaxSide, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                var canvas = Graphics.FromImage(bmpMax);

                var destRect = new Rectangle() { 
                    Width = (int) size.v0, 
                    Height = (int) size.v1, 
                    X = (bmpMax.Width - (int) size.v0)/2, 
                    Y = (bmpMax.Height - (int) size.v1)/2
                };
                canvas.DrawImage(bmp, destRect, 0, 0, bmp.Width, bmp.Height, GraphicsUnit.Pixel);
                canvas.Save();
                bmp.Dispose();

                // Lock bits so we can supply them to the texture
                var bmpBits = bmpMax.LockBits(
                    new Rectangle(0, 0, bmpMax.Width, bmpMax.Height),
                    System.Drawing.Imaging.ImageLockMode.ReadOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb
                );

                // Create OpenGL texture
                if (_textureId == IntPtr.Zero)
                {
                    _textureId = (IntPtr) GL.GenTexture();
                    GL.BindTexture(TextureTarget.Texture2D, (int)_textureId);
                    GL.TexStorage2D(TextureTarget2d.Texture2D, 1, SizedInternalFormat.Rgba8, bmpMax.Width, bmpMax.Height);
                }
                GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, bmpMax.Width, bmpMax.Height, PixelFormat.Bgra, PixelType.UnsignedByte, bmpBits.Scan0);
                bmpMax.UnlockBits(bmpBits);
                bmpMax.Dispose();

                // Create SteamVR texture
                Texture_t texture = new Texture_t
                {
                    eType = ETextureType.OpenGL,
                    eColorSpace = EColorSpace.Auto,
                    handle = (IntPtr)_textureId
                };

                // Assign texture
                var error = OpenVR.Overlay.SetOverlayTexture(_overlayHandle, ref texture);

                // Adjust UV if needed to remove padding
                var side = Math.Max(size.v0, size.v1);
                var bounds = new VRTextureBounds_t();
                if (side < _textureMaxSide) {
                    bounds.uMin = bounds.vMin = (1f - (side / _textureMaxSide)) / 2;
                    bounds.uMax = bounds.vMax = 1f - bounds.uMin;
                } else bounds.uMax = bounds.vMax = 1;
                Debug.WriteLine($"Bounds: ({bounds.uMin}, {bounds.vMin}) => ({bounds.uMax}, {bounds.vMax})");
                OpenVR.Overlay.SetOverlayTextureBounds(_overlayHandle, ref bounds);

                // Check for error
                if (error != EVROverlayError.None)
                {
                    size = new HmdVector2_t();
                    Debug.WriteLine($"SetOverlayTexture error: {Enum.GetName(error.GetType(), error)}");
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Texture Exception: {e.ToString()}");
            }
            return size;
        }
        
        // If we unload too quickly we get an exception and crash.
        public void Unload() {
            OpenVR.Overlay.ClearOverlayTexture(_overlayHandle);
            if (_textureId != IntPtr.Zero) GL.DeleteTexture((int) _textureId);
            _textureId = IntPtr.Zero;
        }
    }
}
