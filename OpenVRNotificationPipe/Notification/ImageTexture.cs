using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using OpenTK.Graphics.OpenGL;
using OpenVRNotificationPipe.Extensions;

namespace OpenVRNotificationPipe.Notification
{
    internal class ImageTexture
    {
        private readonly int _textureId;
        private readonly int _width;
        private readonly int _height;
        private readonly int _textureDepth;
        private readonly TextureTarget _textureTarget;
        
        public TextureTarget TextureTarget => _textureTarget;

        public ImageTexture(int textureId, int width, int height, TextureTarget textureTarget = TextureTarget.Texture2D, int textureDepth = 0)
        {
            _textureId = textureId;
            _width = width;
            _height = height;
            _textureTarget = textureTarget;
            _textureDepth = textureDepth;
        }

        public static ImageTexture LoadSpritesheetFile(string path, int spriteWidth, int spriteHeight)
        {
            Bitmap image;
            try
            {
                image = new Bitmap(path);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                return null;
            }
            
            return LoadSpritesheet(image, spriteWidth, spriteHeight);
        }
        
        public static ImageTexture LoadSpritesheetBase64(string bytes, int spriteWidth, int spriteHeight, IEnumerable<Payload.TextArea> textAreas = null)
        {
            return LoadSpritesheetBytes(Convert.FromBase64String(bytes), spriteWidth, spriteHeight, textAreas);
        }
        
        public static ImageTexture LoadSpritesheetBytes(byte[] bytes, int spriteWidth, int spriteHeight, IEnumerable<Payload.TextArea> textAreas = null)
        {
            Bitmap image;
            try
            {
                image = new Bitmap(new MemoryStream(bytes));
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                return null;
            }
            
            return LoadSpritesheet(image, spriteWidth, spriteHeight, textAreas);
        }
        
        public static ImageTexture LoadSpritesheet(Bitmap image, int spriteWidth, int spriteHeight, IEnumerable<Payload.TextArea> textAreas = null)
        {
            image.RotateFlip(RotateFlipType.RotateNoneFlipY);
            var textureId = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2DArray, textureId);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMinFilter,
                (int) TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMagFilter,
                (int) TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapS,
                (int) TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapT,
                (int) TextureWrapMode.ClampToEdge);
            
            BitmapData data = image.LockBits(new System.Drawing.Rectangle(0, 0, image.Width, image.Height),
                ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            
            int columns = image.Width / spriteWidth;
            int rows = image.Height / spriteHeight;
            int depth = columns * rows;
            
            GL.TexStorage3D(TextureTarget3d.Texture2DArray, 1, SizedInternalFormat.Rgba8, spriteWidth, spriteHeight,
                depth);
            
            GL.PixelStore(PixelStoreParameter.UnpackRowLength, image.Width);
            for (int i = 0; i < depth; i++)
            {
                GL.TexSubImage3D(TextureTarget.Texture2DArray,
                    0, 0, 0, i, spriteWidth, spriteHeight, 1,
                    OpenTK.Graphics.OpenGL.PixelFormat.Bgra, PixelType.UnsignedByte,
                    data.Scan0 + (spriteWidth * 4 * (i % columns)) +
                    (image.Width * 4 * spriteHeight * (i / columns))); // 4 bytes in an Bgra value.
            }
            
            image.UnlockBits(data);
            
            return new ImageTexture(textureId, spriteWidth, spriteHeight, TextureTarget.Texture2DArray, depth);
        }
        
        public static ImageTexture LoadImageFile(string path, IEnumerable<Payload.TextArea> textAreas = null)
        {
            Bitmap image;
            try
            {
                image = new Bitmap(path);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                return null;
            }
            
            return LoadImage(image, textAreas);
        }
        
        public static ImageTexture LoadImageBase64(string bytes, IEnumerable<Payload.TextArea> textAreas = null)
        {
            return LoadImageBytes(Convert.FromBase64String(bytes), textAreas);
        }
        
        public static ImageTexture LoadImageBytes(byte[] bytes, IEnumerable<Payload.TextArea> textAreas = null)
        {
            Bitmap image;
            try
            {
                image = new Bitmap(new MemoryStream(bytes));
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                return null;
            }
            
            return LoadImage(image, textAreas);
        }
        
        public static ImageTexture LoadImage(Bitmap image, IEnumerable<Payload.TextArea> textAreas = null)
        {
            int frameCount = 1;

            try
            {
                frameCount = image.GetFrameCount(FrameDimension.Time);
            }
            catch (Exception e)
            {
                frameCount = 1;
            }
            
            Debug.WriteLine($"The image has {frameCount} frames.");

            if (frameCount > 1)
            {
                Bitmap[] Frames = new Bitmap[frameCount];
                
                for (int i = 0; i < frameCount; i++)
                {
                    image.SelectActiveFrame(FrameDimension.Time, i);
                    Frames[i] = new Bitmap(image.Size.Width, image.Size.Height);
                    Graphics.FromImage(Frames[i]).DrawImage(image, new Point(0, 0));
                    if (!(textAreas is null)) Frames[i].DrawTextAreas(textAreas);
                    Frames[i].RotateFlip(RotateFlipType.RotateNoneFlipY);
                }
                
                var textureId = GL.GenTexture();
                GL.BindTexture(TextureTarget.Texture2DArray, textureId);
                GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMinFilter,
                    (int) TextureMinFilter.Nearest);
                GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMagFilter,
                    (int) TextureMagFilter.Nearest);
                GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapS,
                    (int) TextureWrapMode.ClampToEdge);
                GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapT,
                    (int) TextureWrapMode.ClampToEdge);

                int depth = frameCount;
            
                GL.TexStorage3D(TextureTarget3d.Texture2DArray, 1, SizedInternalFormat.Rgba8, image.Size.Width, image.Size.Height,
                    depth);
            
                GL.PixelStore(PixelStoreParameter.UnpackRowLength, image.Width);
                for (int i = 0; i < depth; i++)
                {
                    // image.SelectActiveFrame(FrameDimension.Time, i);
                    BitmapData data = Frames[i].LockBits(new System.Drawing.Rectangle(0, 0, image.Width, image.Height),
                        ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    
                    GL.TexSubImage3D(TextureTarget.Texture2DArray,
                        0, 0, 0, i, image.Size.Width, image.Size.Height, 1,
                        OpenTK.Graphics.OpenGL.PixelFormat.Bgra, PixelType.UnsignedByte,
                        data.Scan0); // 4 bytes in an Bgra value.
                    
                    Frames[i].UnlockBits(data);
                }

                return new ImageTexture(textureId, image.Size.Width,  image.Size.Height, TextureTarget.Texture2DArray, depth);
            }
            else
            {
                if (!(textAreas is null)) image.DrawTextAreas(textAreas);
                image.RotateFlip(RotateFlipType.RotateNoneFlipY);
                var textureId = GL.GenTexture();
                GL.BindTexture(TextureTarget.Texture2D, textureId);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter,
                    (int) TextureMinFilter.Nearest);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter,
                    (int) TextureMagFilter.Nearest);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS,
                    (int) TextureWrapMode.ClampToEdge);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT,
                    (int) TextureWrapMode.ClampToEdge);
            
                BitmapData data = image.LockBits(new System.Drawing.Rectangle(0, 0, image.Width, image.Height),
                    ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                GL.TexStorage2D(TextureTarget2d.Texture2D, 1, SizedInternalFormat.Rgba8, image.Width, image.Height);
            
                GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, image.Width, image.Height, OpenTK.Graphics.OpenGL.PixelFormat.Bgra, PixelType.UnsignedByte, data.Scan0);

                image.UnlockBits(data);
            
                return new ImageTexture(textureId, image.Width, image.Height, TextureTarget.Texture2D, 1);
            }
        }
        
        public void Bind()
        {
            GL.BindTexture(_textureTarget, _textureId);
        }
        
        public void Delete()
        {
            GL.DeleteTexture(_textureId);
        }
        
        public int Width => _width;
        public int Height => _height;
        public int TextureDepth => _textureDepth;
    }
}