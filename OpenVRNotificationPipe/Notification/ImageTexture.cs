﻿using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using OpenTK.Graphics.OpenGL;

namespace OpenVRNotificationPipe.Notification
{
    public class ImageTexture
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
                image.RotateFlip(RotateFlipType.RotateNoneFlipY);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                return null;
            }
            
            return LoadSpritesheet(image, spriteWidth, spriteHeight);
        }
        
        public static ImageTexture LoadSpritesheetBase64(string bytes, int spriteWidth, int spriteHeight)
        {
            return LoadSpritesheetBytes(Convert.FromBase64String(bytes), spriteWidth, spriteHeight);
        }
        
        public static ImageTexture LoadSpritesheetBytes(byte[] bytes, int spriteWidth, int spriteHeight)
        {
            Bitmap image;
            try
            {
                image = new Bitmap(new MemoryStream(bytes));
                image.RotateFlip(RotateFlipType.RotateNoneFlipY);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                return null;
            }
            
            return LoadSpritesheet(image, spriteWidth, spriteHeight);
        }
        
        public static ImageTexture LoadSpritesheet(Bitmap image, int spriteWidth, int spriteHeight)
        {
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
        
        public static ImageTexture LoadImageFile(string path)
        {
            Bitmap image;
            try
            {
                image = new Bitmap(path);
                image.RotateFlip(RotateFlipType.RotateNoneFlipY);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                return null;
            }
            
            return LoadImage(image);
        }
        
        public static ImageTexture LoadImageBase64(string bytes)
        {
            return LoadImageBytes(Convert.FromBase64String(bytes));
        }
        
        public static ImageTexture LoadImageBytes(byte[] bytes)
        {
            Bitmap image;
            try
            {
                image = new Bitmap(new MemoryStream(bytes));
                image.RotateFlip(RotateFlipType.RotateNoneFlipY);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                return null;
            }
            
            return LoadImage(image);
        }
        
        public static ImageTexture LoadImage(Bitmap image)
        {
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