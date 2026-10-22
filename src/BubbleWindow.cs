using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Runtime.InteropServices;

namespace NaiWa
{
    public class BubbleWindow : Window
    {
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TRANSPARENT = 0x00000020;

        private readonly PetWindow owner;
        private readonly DispatcherTimer followTimer;
        private readonly DispatcherTimer closeTimer;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        public BubbleWindow(PetWindow owner, string text)
        {
            this.owner = owner;

            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = null;
            Topmost = true;
            ShowInTaskbar = false;
            ShowActivated = false;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.Manual;

            var textBlock = new TextBlock();
            textBlock.Text = text;
            textBlock.FontSize = 14;
            textBlock.TextWrapping = TextWrapping.Wrap;
            textBlock.MaxWidth = 230;
            textBlock.Foreground = new SolidColorBrush(Color.FromRgb(74, 52, 24));
            textBlock.Measure(new Size(230, double.PositiveInfinity));
            Size desired = textBlock.DesiredSize;

            double textW = Math.Min(230, Math.Max(60, desired.Width));
            double textH = Math.Max(20, desired.Height);
            double boxW = textW + 26;
            double boxH = textH + 20;
            double tailH = 9;
            Width = boxW;
            Height = boxH + tailH;

            var grid = new Grid();
            var border = new Border();
            border.Width = boxW;
            border.Height = boxH;
            border.Background = new SolidColorBrush(Color.FromRgb(255, 251, 240));
            border.BorderBrush = new SolidColorBrush(Color.FromRgb(226, 210, 184));
            border.BorderThickness = new Thickness(1);
            border.CornerRadius = new CornerRadius(12);
            border.VerticalAlignment = VerticalAlignment.Top;
            border.Child = textBlock;
            border.Padding = new Thickness(13, 9, 13, 9);
            grid.Children.Add(border);

            var tail = new Polygon();
            tail.Points.Add(new Point(boxW / 2 - 8, boxH - 1));
            tail.Points.Add(new Point(boxW / 2 + 8, boxH - 1));
            tail.Points.Add(new Point(boxW / 2, boxH + tailH));
            tail.Fill = border.Background;
            tail.Stroke = border.BorderBrush;
            tail.StrokeThickness = 1;
            grid.Children.Add(tail);

            Content = grid;

            followTimer = new DispatcherTimer();
            followTimer.Interval = TimeSpan.FromMilliseconds(120);
            followTimer.Tick += delegate { Follow(); };
            followTimer.Start();

            closeTimer = new DispatcherTimer();
            closeTimer.Interval = TimeSpan.FromSeconds(8);
            closeTimer.Tick += delegate { CloseBubble(); };
            closeTimer.Start();

            Loaded += delegate
            {
                Follow();
                var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160));
                BeginAnimation(OpacityProperty, fade);
            };
        }

        private void Follow()
        {
            if (!owner.IsVisible)
            {
                Close();
                return;
            }
            double left = owner.Left + owner.Width / 2.0 - Width / 2.0;
            double top = owner.Top + owner.Height * 0.10 - Height - 4;
            Left = left;
            Top = top;
        }

        private void CloseBubble()
        {
            closeTimer.Stop();
            followTimer.Stop();
            var fade = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(180));
            fade.Completed += delegate { try { Close(); } catch { } };
            BeginAnimation(OpacityProperty, fade);
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            int style = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, style | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TRANSPARENT);
        }
    }
}