using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using H.NotifyIcon;
using NvidiaColorSwitcher.Services;
using NvidiaColorSwitcher.ViewModels;
using NvidiaColorSwitcher.Views;

namespace NvidiaColorSwitcher
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private TaskbarIcon? _notifyIcon;
        private NvidiaService? _nvidiaService;
        private ProfileStorageService? _storageService;
        private MainViewModel? _viewModel;
        private EditorWindow? _editorWindow;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                System.IO.File.WriteAllText("crash.log", ex?.ToString() ?? "Unknown exception");
            };

            try
            {
                // Check if launched with --autostart or --minimized argument
                bool startMinimized = false;
                if (e.Args != null)
                {
                    foreach (var arg in e.Args)
                    {
                        if (string.Equals(arg, "--autostart", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(arg, "--minimized", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(arg, "-autostart", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(arg, "-minimized", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(arg, "/autostart", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(arg, "/minimized", StringComparison.OrdinalIgnoreCase))
                        {
                            startMinimized = true;
                            break;
                        }
                    }
                }

                // 1. Initialize Core Services & ViewModel
                _nvidiaService = new NvidiaService();
                _storageService = new ProfileStorageService();
                _viewModel = new MainViewModel(_nvidiaService, _storageService);

                // If auto-startup is enabled in registry, ensure the registry path includes --autostart flag
                if (_viewModel.IsAutoStartupEnabled)
                {
                    StartupService.SetAutoStartup(true);
                }

                // 2. Initialize Editor Window (Floating WPF UI)
                _editorWindow = new EditorWindow(_viewModel);

                // 3. Initialize System Tray Icon
                try
                {
                    InitializeSystemTray();
                }
                catch (Exception trayEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[App] Tray Icon init error: {trayEx.Message}");
                }

                // 4. Listen for profile list & startup updates to refresh Tray Context Menu
                _viewModel.ProfilesUpdated += (s, args) => RefreshTrayContextMenu();
                _viewModel.PropertyChanged += (s, args) =>
                {
                    if (args.PropertyName == nameof(MainViewModel.IsAutoStartupEnabled))
                    {
                        RefreshTrayContextMenu();
                    }
                };

                // 5. Show Editor Window on startup only if not launching minimized to tray
                if (!startMinimized)
                {
                    ShowEditorWindow();
                }
            }
            catch (Exception ex)
            {
                System.IO.File.WriteAllText("startup_crash.log", ex.ToString());
                MessageBox.Show($"Startup Error: {ex.Message}\n\nDetails:\n{ex}", "NVIDIA Color Switcher Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void InitializeSystemTray()
        {
            _notifyIcon = new TaskbarIcon
            {
                Icon = CreateTrayIcon(),
                ToolTipText = "NVIDIA Color Switcher",
                ContextMenu = BuildTrayContextMenu()
            };

            _notifyIcon.ForceCreate();

            // Left-click opens/toggles the Custom Color Editor Window
            _notifyIcon.TrayLeftMouseDown += (s, args) => ToggleEditorWindow();
        }

        private void RefreshTrayContextMenu()
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.ContextMenu = BuildTrayContextMenu();
            }
        }

        private ContextMenu BuildTrayContextMenu()
        {
            var menu = new ContextMenu();

            // Header Title Item
            var headerItem = new MenuItem
            {
                Header = "NVIDIA Color Switcher",
                IsEnabled = false,
                FontWeight = FontWeights.Bold
            };
            menu.Items.Add(headerItem);

            // Check for Update Notification Item
            if (_viewModel != null && _viewModel.IsUpdateAvailable)
            {
                var updateItem = new MenuItem
                {
                    Header = $"⚡ Update Available (v{_viewModel.LatestVersion})",
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 230, 118))
                };
                updateItem.Click += (s, args) => _viewModel.PerformUpdate();
                menu.Items.Add(updateItem);
            }

            menu.Items.Add(new Separator());

            // List of Saved Color Profiles
            if (_viewModel != null)
            {
                foreach (var profile in _viewModel.Profiles)
                {
                    var p = profile;
                    var item = new MenuItem
                    {
                        Header = p.Name,
                        Icon = p.IsActive ? new TextBlock 
                        { 
                            Text = "✓", 
                            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(118, 185, 0)), 
                            FontWeight = FontWeights.Bold,
                            FontSize = 14
                        } : null
                    };
                    item.Click += (s, args) => _viewModel.SelectAndApplyProfile(p);
                    menu.Items.Add(item);
                }
            }

            menu.Items.Add(new Separator());

            // Open Custom Color Editor UI
            var editorItem = new MenuItem
            {
                Header = "Custom Color Editor...",
                FontWeight = FontWeights.SemiBold
            };
            editorItem.Click += (s, args) => ShowEditorWindow();
            menu.Items.Add(editorItem);

            // Toggle Windows Auto Startup
            var startupItem = new MenuItem
            {
                Header = "Run on Windows Startup",
                IsCheckable = true,
                IsChecked = _viewModel?.IsAutoStartupEnabled ?? StartupService.IsAutoStartupEnabled()
            };
            startupItem.Click += (s, args) =>
            {
                if (_viewModel != null)
                {
                    _viewModel.IsAutoStartupEnabled = !_viewModel.IsAutoStartupEnabled;
                }
            };
            menu.Items.Add(startupItem);

            menu.Items.Add(new Separator());

            // Exit Application
            var exitItem = new MenuItem
            {
                Header = "Exit",
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 82, 82))
            };
            exitItem.Click += (s, args) => ExitApplication();
            menu.Items.Add(exitItem);

            return menu;
        }

        private void ShowEditorWindow()
        {
            if (_editorWindow == null) return;

            if (!_editorWindow.IsVisible)
            {
                _editorWindow.Show();
            }

            if (_editorWindow.WindowState == WindowState.Minimized)
            {
                _editorWindow.WindowState = WindowState.Normal;
            }

            _editorWindow.Activate();
            _editorWindow.Focus();
        }

        private void ToggleEditorWindow()
        {
            if (_editorWindow == null) return;

            if (_editorWindow.IsVisible && _editorWindow.WindowState != WindowState.Minimized)
            {
                _editorWindow.Hide();
            }
            else
            {
                ShowEditorWindow();
            }
        }

        private void ExitApplication()
        {
            if (_editorWindow != null)
            {
                _editorWindow.AllowClose = true;
                _editorWindow.Close();
            }

            _notifyIcon?.Dispose();
            Shutdown();
        }

        /// <summary>
        /// Loads the custom NVIDIA logo Icon for System Tray.
        /// </summary>
        private static Icon CreateTrayIcon()
        {
            try
            {
                var sri = GetResourceStream(new Uri("pack://application:,,,/Assets/app_icon.ico"));
                if (sri != null)
                {
                    return new Icon(sri.Stream);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[App] Failed to load tray icon resource: {ex.Message}");
            }

            using var bitmap = new Bitmap(32, 32);
            using var g = Graphics.FromImage(bitmap);

            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(System.Drawing.Color.Transparent);

            using (var bgBrush = new SolidBrush(System.Drawing.Color.FromArgb(18, 18, 22)))
            {
                g.FillEllipse(bgBrush, 2, 2, 28, 28);
            }

            using (var ringPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(118, 185, 0), 2.5f))
            {
                g.DrawEllipse(ringPen, 3, 3, 26, 26);
            }

            using (var centerBrush = new SolidBrush(System.Drawing.Color.FromArgb(0, 230, 118)))
            {
                g.FillEllipse(centerBrush, 11, 11, 10, 10);
            }

            IntPtr hIcon = bitmap.GetHicon();
            return Icon.FromHandle(hIcon);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _notifyIcon?.Dispose();
            base.OnExit(e);
        }
    }
}
