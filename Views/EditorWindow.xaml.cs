using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using NvidiaColorSwitcher.ViewModels;

namespace NvidiaColorSwitcher.Views
{
    /// <summary>
    /// Interaction logic for EditorWindow.xaml
    /// </summary>
    public partial class EditorWindow : Window
    {
        public bool AllowClose { get; set; } = false;

        public EditorWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;

            // Pressing Escape key hides the window to System Tray
            KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    Hide();
                }
            };
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!AllowClose)
            {
                e.Cancel = true;
                Hide();
            }
            else
            {
                base.OnClosing(e);
            }
        }
        private void GitHubLink_MouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://github.com/PrmWiny/Nvidia-Color-Profile-Setting",
                    UseShellExecute = true
                });
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EditorWindow] Error opening GitHub link: {ex.Message}");
            }
        }
    }
}
