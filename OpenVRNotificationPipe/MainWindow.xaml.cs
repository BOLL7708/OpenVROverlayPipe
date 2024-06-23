using EasyFramework;
using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Forms;
using Brushes = System.Windows.Media.Brushes;
using MouseEventArgs = System.Windows.Forms.MouseEventArgs;
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
        private readonly GraphicsSingleton _graphics = GraphicsSingleton.Instance;
        private static Mutex _mutex = null; // Used to detect other instances of the same application

        public MainWindow()
        {
            InitializeComponent();
            if (!_settings.LaunchMinimized) {
                // Position in last known location, unless negative, then center on screen.
                var wa = Screen.PrimaryScreen.WorkingArea;
                var b = Screen.PrimaryScreen.Bounds;
                if (
                    _settings.WindowTop >= wa.Y
                    && _settings.WindowLeft >= wa.X
                    && _settings.WindowTop < (b.Height - Height)
                    && _settings.WindowLeft < (b.Width - Width)
                ) {
                    Top = _settings.WindowTop; 
                    Left = _settings.WindowLeft; 
                }
                else WindowStartupLocation = WindowStartupLocation.CenterScreen; 
            }

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
            _notifyIcon.MouseClick += NotifyIcon_Click;
            
            _notifyIcon.ContextMenuStrip = new System.Windows.Forms.ContextMenuStrip(new System.ComponentModel.Container());
            _notifyIcon.ContextMenuStrip.SuspendLayout();
            var openMenuItem = new System.Windows.Forms.ToolStripMenuItem("Open");
            openMenuItem.Click += (sender, args) => NotifyIcon_Click(sender, new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0));
            var exitMenuItem = new System.Windows.Forms.ToolStripMenuItem("Exit");
            exitMenuItem.Click += (sender, args) => System.Windows.Application.Current.Shutdown();
            _notifyIcon.ContextMenuStrip.Items.Add(openMenuItem);
            _notifyIcon.ContextMenuStrip.Items.Add(exitMenuItem);
            _notifyIcon.ContextMenuStrip.Name = "NotifyIconContextMenu";
            _notifyIcon.ContextMenuStrip.ResumeLayout(false);

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
                            label_ServerStatus.Content = "Online";
                            break;
                        case SuperServer.ServerStatus.Disconnected:
                            label_ServerStatus.Background = Brushes.Tomato;
                            label_ServerStatus.Content = "Offline";
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

            GraphicsCompanion.StartOpenTk(this);
            _controller.SetPort(_settings.Port);

            if (_settings.LaunchMinimized)
            {
                Loaded += (sender, args) =>
                {
                    var wa = Screen.PrimaryScreen.WorkingArea;
                    var b = Screen.PrimaryScreen.Bounds;
                    if ( // If we have a valid stored location, use it
                        _settings.WindowTop >= wa.Y
                        && _settings.WindowLeft >= wa.X
                        && _settings.WindowTop < (b.Height - Height)
                        && _settings.WindowLeft < (b.Width - Width) 
                    ) {
                        Top = _settings.WindowTop;
                        Left = _settings.WindowLeft;
                    } else { // Otherwise center on screen
                        Top = wa.Y + (wa.Height / 2 - Height / 2);
                        Left = wa.X + (wa.Width / 2 - Width / 2);
                    }
                    
                    WindowState = WindowState.Minimized;
                    ShowInTaskbar = !_settings.Tray;
                };
            }

            
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

        private void Button_Editor_Click(object sender, RoutedEventArgs e) {
            Process.Start("editor.html");
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

        private void NotifyIcon_Click(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right) return;
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
            var wa = Screen.PrimaryScreen.WorkingArea;
            var b = Screen.PrimaryScreen.Bounds;
            if (Top >= wa.Y && Top < (b.Height - Height)) _settings.WindowTop = Top;
            if(Left >= wa.X && Left < (b.Width - Width)) _settings.WindowLeft = Left;
            _settings.Save();
            if (_notifyIcon != null) _notifyIcon.Dispose();
        }
        
        // Open GL/TK stuff
        public static void FitToScreen(FrameworkElement screen, int width, int height)
        {
            GraphicsCompanion.SetViewportDimensions((int) screen.Width, (int) screen.Height, width, height);
        }

        private void OpenTKControl_OnRender(TimeSpan delta)
        {
            _graphics.OnRender(delta);
        }
        private void OpenTKControl_OnReady() {
            _graphics.OnReady();
        }
    }
}
