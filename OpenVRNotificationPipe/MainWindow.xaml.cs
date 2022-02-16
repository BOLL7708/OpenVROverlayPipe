using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Wpf;
using OpenTK_Animation_Testing;
using Valve.VR;
using System.IO;
using BOLL7708;
using OpenTK;
using Brushes = System.Windows.Media.Brushes;
using PixelFormat = OpenTK.Graphics.OpenGL.PixelFormat;
using WindowState = System.Windows.WindowState;

namespace OpenVRNotificationPipe
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainController _controller;
        private readonly Properties.Settings _settings = Properties.Settings.Default;
        private readonly System.Windows.Forms.NotifyIcon _notifyIcon;
        private static Mutex _mutex = null; // Used to detect other instances of the same application

        public MainWindow()
        {
            InitializeComponent();

            // Prevent multiple instances
            _mutex = new Mutex(true, Properties.Resources.AppName, out bool createdNew);
            if (!createdNew)
            {
                System.Windows.MessageBox.Show(
                System.Windows.Application.Current.MainWindow,
                "This application is already running!",
                Properties.Resources.AppName,
                MessageBoxButton.OK,
                MessageBoxImage.Information
                );
                System.Windows.Application.Current.Shutdown();
            }

            // Tray icon
            var icon = Properties.Resources.Icon.Clone() as System.Drawing.Icon;
            _notifyIcon = new System.Windows.Forms.NotifyIcon();
            _notifyIcon.Click += NotifyIcon_Click;
            _notifyIcon.Text = $"Click to show the {Properties.Resources.AppName} window";
            _notifyIcon.Icon = icon;
            _notifyIcon.Visible = true;

            Title = Properties.Resources.AppName;

            LoadSettings();
#if DEBUG
            Label_Version.Content = $"{Properties.Resources.Version}d";
#else
            Label_Version.Content = Properties.Resources.Version;
#endif
            // Controller
            _controller = new MainController((status, state) => {
                Dispatcher.Invoke(() =>
                {
                    switch (status)
                    {
                        case SuperServer.ServerStatus.Connected:
                            label_ServerStatus.Background = Brushes.OliveDrab;
                            label_ServerStatus.Content = "Connected";
                            break;
                        case SuperServer.ServerStatus.Disconnected:
                            label_ServerStatus.Background = Brushes.Tomato;
                            label_ServerStatus.Content = "Disconnected";
                            break;
                        case SuperServer.ServerStatus.Error:
                            label_ServerStatus.Background = Brushes.Gray;
                            label_ServerStatus.Content = "Error";
                            break;
                    }
                });
            },
                (status) => {
                    Dispatcher.Invoke(() => {
                        if (status)
                        {
                            label_OpenVRStatus.Background = Brushes.OliveDrab;
                            label_OpenVRStatus.Content = "Connected";
                        }
                        else
                        {
                            label_OpenVRStatus.Background = Brushes.Tomato;
                            label_OpenVRStatus.Content = "Disconnected";
                            if (_settings.ExitWithSteam)
                            {
                                _controller.Shutdown();
                                if (_notifyIcon != null) _notifyIcon.Dispose();
                                System.Windows.Application.Current.Shutdown();
                            }
                        }
                    });
                }
            );
            if (_settings.LaunchMinimized)
            {
                Hide();
                WindowState = WindowState.Minimized;
                ShowInTaskbar = !_settings.Tray;
            }
            _controller.SetPort(_settings.Port);

            while (!EasyOpenVRSingleton.Instance.IsInitialized())
            {
                Thread.Sleep(500);
            }
            
            // OpenTK Initialization
            var settings = new GLWpfControlSettings
            {
                RenderContinuously = true,
                GraphicsContextFlags = GraphicsContextFlags.Offscreen | GraphicsContextFlags.Default,
            };
            OpenTKControl.Start(settings);
        }

        private void LoadSettings()
        {
            checkBox_MinimizeOnLaunch.IsChecked = _settings.LaunchMinimized;
            checkBox_MinimizeToTray.IsChecked = _settings.Tray;
            checkBox_ExitWithSteamVR.IsChecked = _settings.ExitWithSteam;
            textBox_Port.Text = _settings.Port.ToString();
        }

        #region interface
        private void Button_Edit_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new InputDialog(_settings.Port, "Port:")
            {
                Owner = this
            };
            dlg.ShowDialog();
            var result = dlg.DialogResult;
            if(result == true)
            {
                _settings.Port = dlg.value;
                _settings.Save();
                textBox_Port.Text = _settings.Port.ToString();
                _controller.SetPort(_settings.Port);
            }
        }
        
        private void ClickedURL(object sender, RoutedEventArgs e)
        {
            var link = (Hyperlink)sender;
            Process.Start(link.NavigateUri.ToString());
        }
        private bool CheckboxValue(RoutedEventArgs e)
        {
            var name = e.RoutedEvent.Name;
            return name == "Checked";
        }


        #endregion


        private void CheckBox_MinimizeOnLaunch_Checked(object sender, RoutedEventArgs e)
        {
            _settings.LaunchMinimized = CheckboxValue(e);
            _settings.Save();
        }

        private void CheckBox_MinimizeToTray_Checked(object sender, RoutedEventArgs e)
        {
            _settings.Tray = CheckboxValue(e);
            _settings.Save();
        }

        private void CheckBox_ExitWithSteamVR_Checked(object sender, RoutedEventArgs e)
        {
            _settings.ExitWithSteam = CheckboxValue(e);
            _settings.Save();
        }

        private void NotifyIcon_Click(object sender, EventArgs e)
        {
            WindowState = WindowState.Normal;
            ShowInTaskbar = true;
            Show();
            Activate();
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            switch (WindowState)
            {
                case WindowState.Minimized: ShowInTaskbar = !_settings.Tray; break; // Setting here for tray icon only
                default: ShowInTaskbar = true; Show(); break;
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_notifyIcon != null) _notifyIcon.Dispose();
        }
        
        // Rendering Variables
        private int _textureId;
        private string _spriteSheetName;
        private int _spriteWidth;
        private int _spriteHeight;
        private Shader _shader;
        private int _animationFrames;
        private int _currentFrame;
        private double _frameInterval;
        private double _elapsedTime;
        private ulong _vrOverlayHandle;
        private Texture_t _vrTexture;

        private int _frameBuffer;
        private int _renderedTexture;
        
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

        private void OpenTKControl_OnRender(TimeSpan delta)
        {
            if (_firstRender)
            {
                GL.ClearColor(1, 1, 1, 0);
                GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
                GL.Finish();
                _firstRender = false;
            }

            _elapsedTime += delta.TotalSeconds;

            if (_elapsedTime > this._frameInterval)
            {
                _elapsedTime = 0;
                _currentFrame = (_currentFrame + 1) % _animationFrames;
                _shader.SetInt("tex_index", _currentFrame);

                GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

                GL.BindFramebuffer(FramebufferTarget.Framebuffer, this._frameBuffer);
                FitToScreen(1024, 1024, this._spriteWidth, this._spriteHeight);

                _shader.Use();

                GL.DrawBuffer(DrawBufferMode.ColorAttachment0);
                GL.DrawElements(PrimitiveType.Triangles, _indices.Length, DrawElementsType.UnsignedInt, 0);

            }

            GL.Finish();
            
            var error = OpenVR.Overlay.SetOverlayTexture(_vrOverlayHandle, ref _vrTexture);
            OpenVR.Overlay.ShowOverlay(_vrOverlayHandle);
        }
        
        // Fit to screen
        private static void FitToScreen(FrameworkElement screen, int width, int height)
        {
            FitToScreen((int) screen.Width, (int) screen.Height, width, height);
        }
        
        private static void FitToScreen(int screenWidth, int screenHeight, int width, int height)
        {
            // Calculate a width and height that fits the screen
            var aspectRatio = (float) width / height;
            var newWidth = screenWidth;
            var newHeight = (int) (newWidth / aspectRatio);
            if (newHeight > screenHeight)
            {
                newHeight = screenHeight;
                newWidth = (int) (newHeight * aspectRatio);
            }
            
            // Center the image
            var x = (int) (screenWidth - newWidth) / 2;
            var y = (int) (screenHeight - newHeight) / 2;
            
            // Set the viewport
            GL.Viewport(x, y, newWidth, newHeight);
        }

        private void OpenTKControl_OnReady()
        {
            SpriteSheet(@"Tiles\sheet1.png", 233, 233, 0.1);
            
            _vrOverlayHandle = EasyOpenVRSingleton.Instance.CreateOverlay("randomkeyiguess", "Anim Test",
                EasyOpenVRSingleton.Utils.GetEmptyTransform());
            
            if (_vrOverlayHandle == 0)
            {
                throw new Exception("Failed to create VR overlay");
            }
            
            var bounds = new VRTextureBounds_t
            {
                uMax = 1,
                vMax = 1,
                uMin = 0,
                vMin = 0
            };
            
            OpenVR.Overlay.SetOverlayTextureBounds(_vrOverlayHandle, ref bounds);
            
            var anchorIndex = EasyOpenVRSingleton.Instance.GetIndexesForTrackedDeviceClass(ETrackedDeviceClass.HMD)[0];
            var deviceTransform = EasyOpenVRSingleton.Instance.GetDeviceToAbsoluteTrackingPose()[anchorIndex == uint.MaxValue ? 0 : anchorIndex].mDeviceToAbsoluteTracking;
            
            EasyOpenVRSingleton.Instance.SetOverlayTransform(_vrOverlayHandle, deviceTransform.Translate(0, 0, -2), uint.MaxValue);
            
            _vertexArrayObject = GL.GenVertexArray();
            GL.BindVertexArray(_vertexArrayObject);
            
            _vertexBufferObject = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vertexBufferObject);
            GL.BufferData(BufferTarget.ArrayBuffer, _vertices.Length * sizeof(float), _vertices, BufferUsageHint.StaticDraw);
            
            _elementBufferObject = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _elementBufferObject);
            GL.BufferData(BufferTarget.ElementArrayBuffer, _indices.Length * sizeof(uint), _indices, BufferUsageHint.StaticDraw);
            
            _shader = new Shader("Shaders/shader.vert", "Shaders/shader.frag");
            _shader.Use();
            
            var vertexLocation = _shader.GetAttribLocation("aPosition");
            GL.EnableVertexAttribArray(vertexLocation);
            GL.VertexAttribPointer(vertexLocation, 3, VertexAttribPointerType.Float, false, 5 * sizeof(float), 0);
            
            var texCoordLocation = _shader.GetAttribLocation("aTexCoord");
            GL.EnableVertexAttribArray(texCoordLocation);
            GL.VertexAttribPointer(texCoordLocation, 2, VertexAttribPointerType.Float, false, 5 * sizeof(float), 3 * sizeof(float));
            
            _shader.SetInt("tex_index", _currentFrame);
            
            // Render Texture Preparation
            this._frameBuffer = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, this._frameBuffer);
            
            this._renderedTexture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, this._renderedTexture);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, 1024, 1024, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            
            GL.FramebufferTexture(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, this._renderedTexture, 0);
            GL.DrawBuffer(DrawBufferMode.ColorAttachment0);
            
            _vrTexture = new Texture_t
            {
                handle = (IntPtr)_renderedTexture,
                eType = ETextureType.OpenGL,
                eColorSpace = EColorSpace.Auto
            };
            
            var error = OpenVR.Overlay.SetOverlayTexture(_vrOverlayHandle, ref _vrTexture);
            
            if (error != EVROverlayError.None)
            {
                throw new Exception("Failed to set overlay texture, error: " + error);
            }
            
            OpenVR.Overlay.ShowOverlay(_vrOverlayHandle);
        }
        
        public void SpriteSheet(string filename, int spriteWidth, int spriteHeight, double frameInterval)
        {
            Console.WriteLine("Loading sprite sheet...");
            // Assign ID and get name
            this._textureId = GL.GenTexture();
            this._spriteSheetName = Path.GetFileNameWithoutExtension(filename);
            this._frameInterval = frameInterval;
            this._spriteWidth = spriteWidth;
            this._spriteHeight = spriteHeight;

            // Bind the Texture Array and set appropriate parameters
            GL.BindTexture(TextureTarget.Texture2DArray, _textureId);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMinFilter,
                (int) TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMagFilter,
                (int) TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapS,
                (int) TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapT,
                (int) TextureWrapMode.ClampToEdge);

            // Load the image file
            Bitmap image = new Bitmap(filename);
            image.RotateFlip(RotateFlipType.RotateNoneFlipY);
            BitmapData data = image.LockBits(new System.Drawing.Rectangle(0, 0, image.Width, image.Height),
                ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            
            // Determine columns and rows
            int spriteSheetwidth = image.Width;
            int spriteSheetheight = image.Height;
            int columns = spriteSheetwidth / spriteWidth;
            int rows = spriteSheetheight / spriteHeight;

            _animationFrames = columns * rows;
            
            // Allocate storage
            GL.TexStorage3D(TextureTarget3d.Texture2DArray, 1, SizedInternalFormat.Rgba8, spriteWidth, spriteHeight,
                rows * columns);
            
            // Split the loaded image into individual Texture2D slices
            GL.PixelStore(PixelStoreParameter.UnpackRowLength, spriteSheetwidth);
            for (int i = 0; i < columns * rows; i++)
            {
                GL.TexSubImage3D(TextureTarget.Texture2DArray,
                    0, 0, 0, i, spriteWidth, spriteHeight, 1,
                    OpenTK.Graphics.OpenGL.PixelFormat.Bgra, PixelType.UnsignedByte,
                    data.Scan0 + (spriteWidth * 4 * (i % columns)) +
                    (spriteSheetwidth * 4 * spriteHeight * (i / columns))); // 4 bytes in an Bgra value.
            }
            
            image.UnlockBits(data);
        }
    }
}
