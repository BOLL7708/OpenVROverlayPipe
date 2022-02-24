using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

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
            if (_settings.LaunchMinimized)
            {
                Hide();
                WindowState = WindowState.Minimized;
                ShowInTaskbar = !_settings.Tray;
            }
            _controller.SetPort(_settings.Port);
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
    }
}
