using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

        public static string CreateMD5(string input) // https://stackoverflow.com/a/24031467
        {
            // Use input string to calculate MD5 hash
            using (System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create())
            {
                byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(input);
                byte[] hashBytes = md5.ComputeHash(inputBytes);

                // Convert the byte array to hexadecimal string
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    sb.Append(hashBytes[i].ToString("X2"));
                }
                return sb.ToString();
            }
        }

        public HmdVector2_t Load(string base64png)
        {
            if (_glWindow == null) _glWindow = new GameWindow(); // Init OpenGL
            
            var size = new HmdVector2_t();
            try
            {
                // Loading image from incoming base64 encoded string
                var imageBytes = Convert.FromBase64String(base64png);
                Debug.WriteLine($"Image hash: {CreateMD5(base64png)}");
                var bmp = new Bitmap(new MemoryStream(imageBytes));
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

                // Create SteamVR texture
                Texture_t texture = new Texture_t
                {
                    eType = ETextureType.OpenGL,
                    eColorSpace = EColorSpace.Auto,
                    handle = (IntPtr)textureId
                };

                // Assign texture
                var error = OpenVR.Overlay.SetOverlayTexture(_overlayHandle, ref texture); // Overlay handle exist and works when setting the overlay directly from file instead of with texture.
                if (error != EVROverlayError.None) Debug.WriteLine($"SetOverlayTexture error: {Enum.GetName(error.GetType(), error)}");
                else
                {
                    size.v0 = bmp.Width;
                    size.v1 = bmp.Height;
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Exception when loading texture: {e.Message}");
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
