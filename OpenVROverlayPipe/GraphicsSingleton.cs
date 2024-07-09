using OpenTK_Animation_Testing;
using OpenTK.Graphics.OpenGL;
using System;

namespace OpenVROverlayPipe
{
    internal class GraphicsSingleton
    {
        private static GraphicsSingleton? _instance;
        private GraphicsSingleton() { }
        public static GraphicsSingleton Instance
        {
            get
            {
                if (_instance == null) _instance = new GraphicsSingleton();
                return _instance;
            }
        }

        #region OpenTK from Window
        // Rendering Variables
        private Shader? _shader3d;
        private const double FrameInterval = 0.01;
        private double _elapsedTime;

        private readonly float[] _vertices =
        [
            // Position         Texture coordinates
            1.0f,  1.0f, 0.0f, 1.0f, 1.0f, // top right
            1.0f, -1.0f, 0.0f, 1.0f, 0.0f, // bottom right
            -1.0f, -1.0f, 0.0f, 0.0f, 0.0f, // bottom left
            -1.0f,  1.0f, 0.0f, 0.0f, 1.0f  // top left
        ];
        private readonly uint[] _indices =
        [
            0, 1, 3,
            1, 2, 3
        ];

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
                    _shader3d?.Use();
                    _shader3d?.SetInt("tex_index", overlay.Animator.GetFrame());

                    var textureToDraw = overlay.Animator.OnRender(_elapsedTime);
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

            var vertexLocation = _shader3d.GetAttribLocation("aPosition");
            GL.EnableVertexAttribArray(vertexLocation);
            GL.VertexAttribPointer(vertexLocation, 3, VertexAttribPointerType.Float, false, 5 * sizeof(float), 0);

            var texCoordLocation = _shader3d.GetAttribLocation("aTexCoord");
            GL.EnableVertexAttribArray(texCoordLocation);
            GL.VertexAttribPointer(texCoordLocation, 2, VertexAttribPointerType.Float, false, 5 * sizeof(float), 3 * sizeof(float));
        }
        #endregion
    }
}
