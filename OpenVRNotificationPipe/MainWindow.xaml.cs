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
using OpenVRNotificationPipe.Notification;
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
        private Shader _shader3d;
        private Shader _shader2d;
        private const double FrameInterval = 0.1;
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

            if (_elapsedTime > FrameInterval)
            {
                _elapsedTime = 0;

                GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

                foreach (var overlay in _controller.Overlays.Values)
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
                    
                    overlay.Animator.OnRender();
                    GL.DrawBuffer(DrawBufferMode.ColorAttachment0);
                    GL.DrawElements(PrimitiveType.Triangles, _indices.Length, DrawElementsType.UnsignedInt, 0);
                }
            }

            GL.Finish();
            
            foreach (var overlay in _controller.Overlays.Values)
            {
                overlay.Animator.PostRender();
            }
        }
        
        // Fit to screen
        public static void FitToScreen(FrameworkElement screen, int width, int height)
        {
            FitToScreen((int) screen.Width, (int) screen.Height, width, height);
        }
        
        public static void FitToScreen(int screenWidth, int screenHeight, int width, int height)
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
    }
}
