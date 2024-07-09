using System;
using OpenTK.Graphics.OpenGL;

namespace OpenVROverlayPipe.Notification
{
    public class RenderTexture
    {
        private readonly int _frameBufferId;
        private readonly int _textureId;
        private readonly int _width;
        private readonly int _height;
        
        public RenderTexture(int width, int height)
        {
            _width = width;
            _height = height;
            
            _frameBufferId = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _frameBufferId);
            
            _textureId = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _textureId);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, width, height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            
            GL.FramebufferTexture(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, _textureId, 0);
        }

        public int GetFrameBuffer()
        {
            return _frameBufferId;
        }
        
        public int GetTexture()
        {
            return _textureId;
        }
        
        public void Bind()
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _frameBufferId);
        }
        
        public void Unbind()
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }
        
        public void Dispose()
        {
            GL.DeleteFramebuffer(_frameBufferId);
            GL.DeleteTexture(_textureId);
        }
        
        public int GetWidth()
        {
            return _width;
        }
        
        public int GetHeight()
        {
            return _height;
        }
    }
}