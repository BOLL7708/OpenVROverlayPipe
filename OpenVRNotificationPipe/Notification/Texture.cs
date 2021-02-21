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
        private static GameWindow _glWindow; // So GL will work at all
        private IntPtr _oldTextureId = IntPtr.Zero;
        private ulong _overlayHandle = 0;
        
        public Texture(ulong overlayHandle) {
            _overlayHandle = overlayHandle;
        }

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
                size.v0 = bmp.Width;
                size.v1 = bmp.Height;
                bmp.RotateFlip(RotateFlipType.RotateNoneFlipY); // Flip it for OpenGL
                
                // Lock bits so we can supply them to the texture
                var bmpBits = bmp.LockBits(
                    new Rectangle(0, 0, bmp.Width, bmp.Height),
                    System.Drawing.Imaging.ImageLockMode.ReadOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb
                );

                // Create OpenGL texture
                var textureId = GL.GenTexture();
                if (_oldTextureId != IntPtr.Zero)
                {
                    OpenVR.Overlay.ClearOverlayTexture(_overlayHandle);
                    GL.DeleteTexture((int) _oldTextureId);
                    // The below fixes the texture problems but makes a bump on the CPU graph...
                    // _glWindow.Dispose();
                    // _glWindow = new GameWindow();
                }
                _oldTextureId = (IntPtr)textureId;
                GL.BindTexture(TextureTarget.Texture2D, textureId);
                GL.TexStorage2D(TextureTarget2d.Texture2D, 1, SizedInternalFormat.Rgba8, bmp.Width, bmp.Height);
                GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, bmp.Width, bmp.Height, PixelFormat.Bgra, PixelType.UnsignedByte, bmpBits.Scan0);
                bmp.UnlockBits(bmpBits);
                bmp.Dispose();

                // Create SteamVR texture
                Texture_t texture = new Texture_t
                {
                    eType = ETextureType.OpenGL,
                    eColorSpace = EColorSpace.Auto,
                    handle = (IntPtr)textureId
                };

                // Assign texture
                var error = OpenVR.Overlay.SetOverlayTexture(_overlayHandle, ref texture);
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
            if(_oldTextureId != IntPtr.Zero) GL.DeleteTexture((int) _oldTextureId);
        }
    }
}
