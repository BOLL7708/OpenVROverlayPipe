using OpenTK.Graphics;
using OpenTK.Wpf;
using OpenTK;
using OpenTK_Animation_Testing;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenVRNotificationPipe
{
    class GraphicsSingleton
    {
        private static GraphicsSingleton __instance = null;
        private GraphicsSingleton() { }
        public static GraphicsSingleton Instance
        {
            get
            {
                if (__instance == null) __instance = new GraphicsSingleton();
                return __instance;
            }
        }

        #region OpenTK from Window
        // Rendering Variables
        private Shader _shader3d;
        private Shader _shader2d;
        private const double FrameInterval = 0.01;
        private double _elapsedTime;

        private readonly float[] _vertices =
        {
            // Position         Texture coordinates
            1.0f,  1.0f, 0.0f, 1.0f, 1.0f, // top right
            1.0f, -1.0f, 0.0f, 1.0f, 0.0f, // bottom right
            -1.0f, -1.0f, 0.0f, 0.0f, 0.0f, // bottom left
            -1.0f,  1.0f, 0.0f, 0.0f, 1.0f  // top left
        };
        private readonly uint[] _indices =
        {
            0, 1, 3,
            1, 2, 3
        };

        private int _elementBufferObject;
        private int _vertexArrayObject;
        private int _vertexBufferObject;

        private bool _firstRender = true;

        internal void OnRender(TimeSpan delta)
        {
            if (_firstRender)
            {
                GL.ClearColor(1, 1, 1, 0);
                GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
                GL.Finish();
                _firstRender = false;
            }

            _elapsedTime += delta.TotalSeconds;

            if (_elapsedTime > FrameInterval)
            {
                GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

                foreach (var overlay in Session.Overlays.Values)
                {
                    if (overlay.Animator.GetTextureTarget() == TextureTarget.Texture2D)
                    {
                        _shader2d.Use();
                    }
                    else
                    {
                        _shader3d.Use();
                        _shader3d.SetInt("tex_index", overlay.Animator.GetFrame());
                    }

                    bool textureToDraw = overlay.Animator.OnRender(_elapsedTime);
                    GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
                    GL.DrawBuffer(DrawBufferMode.ColorAttachment0);
                    if (textureToDraw) GL.DrawElements(PrimitiveType.Triangles, _indices.Length, DrawElementsType.UnsignedInt, 0);
                }

                _elapsedTime = 0;
            }

            GL.Finish();

            foreach (var overlay in Session.Overlays.Values)
            {
                overlay.Animator.PostRender();
            }
        }

        internal void OnReady()
        {
            _vertexArrayObject = GL.GenVertexArray();
            GL.BindVertexArray(_vertexArrayObject);

            _vertexBufferObject = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vertexBufferObject);
            GL.BufferData(BufferTarget.ArrayBuffer, _vertices.Length * sizeof(float), _vertices, BufferUsageHint.StaticDraw);

            _elementBufferObject = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _elementBufferObject);
            GL.BufferData(BufferTarget.ElementArrayBuffer, _indices.Length * sizeof(uint), _indices, BufferUsageHint.StaticDraw);

            _shader3d = new Shader("Shaders/shader.vert", "Shaders/shader3d.frag");
            _shader2d = new Shader("Shaders/shader.vert", "Shaders/shader2d.frag");
            _shader2d.Use();

            var vertexLocation = _shader2d.GetAttribLocation("aPosition");
            GL.EnableVertexAttribArray(vertexLocation);
            GL.VertexAttribPointer(vertexLocation, 3, VertexAttribPointerType.Float, false, 5 * sizeof(float), 0);

            var texCoordLocation = _shader2d.GetAttribLocation("aTexCoord");
            GL.EnableVertexAttribArray(texCoordLocation);
            GL.VertexAttribPointer(texCoordLocation, 2, VertexAttribPointerType.Float, false, 5 * sizeof(float), 3 * sizeof(float));
        }
        #endregion
    }

    class GraphicsCompanion
    {
        static internal void StartOpenTK(MainWindow mainWindow)
        {
            // OpenTK Initialization
            var settings = new GLWpfControlSettings
            {
                RenderContinuously = true,
                GraphicsContextFlags = GraphicsContextFlags.Offscreen | GraphicsContextFlags.Default,
            };
            mainWindow.OpenTKControl.Start(settings);
        }

        static internal void SetViewportDimensions(int screenWidth, int screenHeight, int width, int height)
        {
            // Calculate a width and height that fits the screen
            var aspectRatio = (float)width / height;
            var newWidth = screenWidth;
            var newHeight = (int)(newWidth / aspectRatio);
            if (newHeight > screenHeight)
            {
                newHeight = screenHeight;
                newWidth = (int)(newHeight * aspectRatio);
            }

            // Center the image
            var x = (int)(screenWidth - newWidth) / 2;
            var y = (int)(screenHeight - newHeight) / 2;

            // Set the viewport
            GL.Viewport(x, y, newWidth, newHeight);
        }
    }
}
