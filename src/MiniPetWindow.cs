using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace NaiWa
{
    public class MiniPetWindow : Window
    {
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TRANSPARENT = 0x00000020;

        private readonly PetWindow owner;
        private readonly BitmapSource[] frames;
        private readonly Image image;
        private readonly DispatcherTimer moveTimer;
        private readonly DispatcherTimer frameTimer;
        private readonly DateTime started = DateTime.Now;
        private double x;
        private double y;
        private double vx;
        private double vy;
        private int frameIndex;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        public MiniPetWindow(PetWindow owner, BitmapSource[] frames, double left, double top,
            double width, double height, double vx, double vy)
        {
            this.owner = owner;
            this.frames = frames;
            this.x = left;
            this.y = top;
            this.vx = vx;
            this.vy = vy;

            Width = width;
            Height = height;
            Left = left;
            Top = top;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = null;
            Topmost = true;
            ShowInTaskbar = false;
            ShowActivated = false;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.Manual;
            IsHitTestVisible = false;

            image = new Image();
            image.Source = frames[0];
            image.Stretch = Stretch.Uniform;
            Content = image;

            frameTimer = new DispatcherTimer();
            frameTimer.Interval = TimeSpan.FromMilliseconds(1000.0 / 15.0);
            frameTimer.Tick += delegate
            {
                frameIndex++;
                if (frameIndex >= frames.Length) frameIndex = 0;
                image.Source = frames[frameIndex];
            };

            moveTimer = new DispatcherTimer();
            moveTimer.Interval = TimeSpan.FromMilliseconds(16);
            moveTimer.Tick += delegate { Step(); };

            Loaded += delegate
            {
                frameTimer.Start();
                moveTimer.Start();
            };
            Closed += delegate { owner.RemoveMini(this); };
        }

        private void Step()
        {
            double dt = 0.016;
            x += vx * dt;
            y += vy * dt;
            vy += 1100 * dt;
            Left = x;
            Top = y;
            double elapsed = (DateTime.Now - started).TotalMilliseconds;
            if (elapsed > 1700)
            {
                Opacity = Math.Max(0, 1 - (elapsed - 1700) / 500.0);
            }
            if (elapsed > 2200)
            {
                Close();
            }
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