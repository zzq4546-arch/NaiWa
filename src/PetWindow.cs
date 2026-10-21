using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Media;
using Microsoft.Win32;

namespace NaiWa
{
    public class PetWindow : Window
    {
        private const int WM_NCHITTEST = 0x0084;
        private const int HTTRANSPARENT = -1;

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_NOACTIVATE = 0x08000000;

        private static readonly double[] PetHeights = { 220, 300, 380 };
        private static readonly string AutostartKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        private readonly Settings settings;
        private readonly Image pet;
        private readonly ScaleTransform breath;
        private readonly ScaleTransform clickScale;
        private readonly TranslateTransform idleShift;
        private readonly TranslateTransform clickShift;

        private BitmapSource idleSource;
        private BitmapSource[] actionFrames;
        private SoundPlayer sound;
        private Stream soundStream;
        private DispatcherTimer actionTimer;
        private int actionIndex;
        private bool actionPlaying;
        private double actionW;
        private double actionH;
        private const double ActionAspect = 0.625;

        private const double IdleBottomMargin = 16;

        private byte[] alphaMask;
        private int maskW;
        private int maskH;

        private bool dragging;
        private bool moved;
        private Point dragStartScreen;
        private double dragLeft;
        private double dragTop;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        public PetWindow(Settings settings)
        {
            this.settings = settings;

            Title = "NaiWa";
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = null;
            Topmost = true;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.Manual;

            var canvas = new Canvas();
            Content = canvas;

            pet = new Image();
            idleSource = LoadPetBitmap();
            pet.Source = idleSource;
            pet.Stretch = Stretch.Fill;
            RenderOptions.SetBitmapScalingMode(pet, BitmapScalingMode.HighQuality);
            pet.RenderTransformOrigin = new Point(0.5, 1.0);

            var group = new TransformGroup();
            breath = new ScaleTransform(1, 1);
            clickScale = new ScaleTransform(1, 1);
            idleShift = new TranslateTransform(0, 0);
            clickShift = new TranslateTransform(0, 0);
            group.Children.Add(breath);
            group.Children.Add(clickScale);
            group.Children.Add(idleShift);
            group.Children.Add(clickShift);
            pet.RenderTransform = group;
            canvas.Children.Add(pet);

            BuildAlphaMask();
            BuildContextMenu();
            ApplySize(false);
            PlaceWindow();
            StartIdle();
            LoadActionAssets();
            CreateActionTimer();

            pet.MouseLeftButtonDown += OnPetMouseDown;
            pet.MouseMove += OnPetMouseMove;
            pet.MouseLeftButtonUp += OnPetMouseUp;

            Closed += OnClosed;
            Loaded += OnLoaded;
        }

        private static BitmapImage LoadPetBitmap()
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            Stream stream = asm.GetManifestResourceStream("naiwa.idle.png");
            if (stream == null)
            {
                string dir = Path.GetDirectoryName(asm.Location);
                string path = Path.Combine(Path.Combine(dir, "assets"), "naiwa-idle.png");
                if (File.Exists(path)) stream = File.OpenRead(path);
            }
            if (stream == null) throw new InvalidOperationException("naiwa-idle.png not found");
            using (stream)
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = stream;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
        }

        private void BuildAlphaMask()
        {
            BitmapSource src = pet.Source as BitmapSource;
            if (src == null) return;
            try
            {
                var conv = new FormatConvertedBitmap();
                conv.BeginInit();
                conv.Source = src;
                conv.DestinationFormat = PixelFormats.Bgra32;
                conv.EndInit();
                maskW = conv.PixelWidth;
                maskH = conv.PixelHeight;
                int stride = maskW * 4;
                byte[] pixels = new byte[stride * maskH];
                conv.CopyPixels(pixels, stride, 0);
                alphaMask = new byte[maskW * maskH];
                for (int i = 0; i < alphaMask.Length; i++)
                {
                    alphaMask[i] = pixels[i * 4 + 3];
                }
            }
            catch (Exception ex)
            {
                Log.Write("alpha mask failed: " + ex.Message);
            }
        }

        private void BuildContextMenu()
        {
            var menu = new ContextMenu();

            var sizeMenu = new MenuItem();
            sizeMenu.Header = "大小";
            string[] labels = { "小", "标准", "大" };
            for (int i = 0; i < labels.Length; i++)
            {
                var item = new MenuItem();
                item.Header = labels[i];
                item.IsCheckable = true;
                item.Tag = i;
                item.Click += OnSizeClick;
                sizeMenu.Items.Add(item);
            }
            menu.Items.Add(sizeMenu);

            var muteItem = new MenuItem();
            muteItem.Header = "静音";
            muteItem.IsCheckable = true;
            muteItem.Click += delegate
            {
                settings.Muted = !settings.Muted;
                settings.Save();
            };
            menu.Items.Add(muteItem);

            var autoItem = new MenuItem();
            autoItem.Header = "开机自启";
            autoItem.IsCheckable = true;
            autoItem.Click += delegate
            {
                settings.AutoStart = !settings.AutoStart;
                SetAutoStart(settings.AutoStart);
                settings.Save();
            };
            menu.Items.Add(autoItem);

            menu.Items.Add(new Separator());

            var resetItem = new MenuItem();
            resetItem.Header = "重置位置";
            resetItem.Click += delegate { ResetPosition(); };
            menu.Items.Add(resetItem);

            var exitItem = new MenuItem();
            exitItem.Header = "退出";
            exitItem.Click += delegate { Close(); };
            menu.Items.Add(exitItem);

            menu.Opened += delegate
            {
                for (int i = 0; i < sizeMenu.Items.Count; i++)
                {
                    var mi = sizeMenu.Items[i] as MenuItem;
                    if (mi != null && mi.Tag is int) mi.IsChecked = ((int)mi.Tag == settings.SizeMode);
                }
                muteItem.IsChecked = settings.Muted;
                autoItem.IsChecked = settings.AutoStart;
            };

            pet.ContextMenu = menu;
        }

        private void OnSizeClick(object sender, RoutedEventArgs e)
        {
            var mi = sender as MenuItem;
            if (mi == null || !(mi.Tag is int)) return;
            settings.SizeMode = (int)mi.Tag;
            ApplySize(true);
            settings.Save();
        }

        private void ApplySize(bool keepAnchor)
        {
            if (actionPlaying) EndAction();
            double oldW = Width;
            double oldH = Height;
            int mode = settings.SizeMode;
            if (mode < 0) mode = 0;
            if (mode > 2) mode = 2;
            double h = PetHeights[mode];
            double w = h * 0.5;
            double winW = w + 30;
            double winH = h + 40;

            if (keepAnchor && !double.IsNaN(Left))
            {
                Left = Left + (oldW - winW) / 2;
                Top = Top + (oldH - winH);
            }

            Width = winW;
            Height = winH;
            pet.Width = w;
            pet.Height = h;
            Canvas.SetLeft(pet, 15);
            Canvas.SetTop(pet, winH - h - IdleBottomMargin);

            double maxW = winW - 4;
            double ah = h;
            double aw = ah * ActionAspect;
            if (aw > maxW)
            {
                aw = maxW;
                ah = aw / ActionAspect;
            }
            actionW = aw;
            actionH = ah;

            if (keepAnchor)
            {
                ClampToScreen();
                settings.Left = Left;
                settings.Top = Top;
            }
        }

        private void PlaceWindow()
        {
            Rect wa = SystemParameters.WorkArea;
            double left = settings.Left;
            double top = settings.Top;
            if (double.IsNaN(left) || double.IsNaN(top))
            {
                left = wa.Right - Width - 28;
                top = wa.Bottom - Height - 4;
            }
            Left = left;
            Top = top;
            ClampToScreen();
        }

        private void ResetPosition()
        {
            Rect wa = SystemParameters.WorkArea;
            Left = wa.Right - Width - 28;
            Top = wa.Bottom - Height - 4;
            ClampToScreen();
            settings.Left = Left;
            settings.Top = Top;
            settings.Save();
        }

        private void ClampToScreen()
        {
            double minLeft = SystemParameters.VirtualScreenLeft;
            double minTop = SystemParameters.VirtualScreenTop;
            double maxLeft = minLeft + SystemParameters.VirtualScreenWidth - Width;
            double maxTop = minTop + SystemParameters.VirtualScreenHeight - Height;
            Left = Clamp(Left, minLeft, maxLeft);
            Top = Clamp(Top, minTop, maxTop);
        }

        private static double Clamp(double v, double min, double max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }

        private static void SetAutoStart(bool on)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(AutostartKey, true))
                {
                    if (key == null) return;
                    if (on)
                    {
                        key.SetValue("NaiWa", "\"" + Assembly.GetExecutingAssembly().Location + "\"");
                    }
                    else
                    {
                        key.DeleteValue("NaiWa", false);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Write("autostart failed: " + ex.Message);
            }
        }

        private void StartIdle()
        {
            var breathe = new DoubleAnimation();
            breathe.From = 1.0;
            breathe.To = 1.015;
            breathe.Duration = TimeSpan.FromSeconds(2.2);
            breathe.AutoReverse = true;
            breathe.RepeatBehavior = RepeatBehavior.Forever;
            breathe.EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut };
            breath.BeginAnimation(ScaleTransform.ScaleYProperty, breathe);

            var bob = new DoubleAnimation();
            bob.From = 0;
            bob.To = -3;
            bob.Duration = TimeSpan.FromSeconds(2.8);
            bob.AutoReverse = true;
            bob.RepeatBehavior = RepeatBehavior.Forever;
            bob.EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut };
            idleShift.BeginAnimation(TranslateTransform.YProperty, bob);
        }

        private void Bounce()
        {
            var sx = new DoubleAnimationUsingKeyFrames();
            sx.Duration = TimeSpan.FromMilliseconds(360);
            sx.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            sx.KeyFrames.Add(new EasingDoubleKeyFrame(1.06, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(80)),
                new QuadraticEase { EasingMode = EasingMode.EaseOut }));
            sx.KeyFrames.Add(new EasingDoubleKeyFrame(0.97, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(190))));
            sx.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(360))));
            clickScale.BeginAnimation(ScaleTransform.ScaleXProperty, sx, HandoffBehavior.SnapshotAndReplace);
            clickScale.BeginAnimation(ScaleTransform.ScaleYProperty, sx, HandoffBehavior.SnapshotAndReplace);

            var sy = new DoubleAnimationUsingKeyFrames();
            sy.Duration = TimeSpan.FromMilliseconds(400);
            sy.KeyFrames.Add(new EasingDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            sy.KeyFrames.Add(new EasingDoubleKeyFrame(-14.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(110)),
                new QuadraticEase { EasingMode = EasingMode.EaseOut }));
            sy.KeyFrames.Add(new EasingDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(400)),
                new QuadraticEase { EasingMode = EasingMode.EaseIn }));
            clickShift.BeginAnimation(TranslateTransform.YProperty, sy, HandoffBehavior.SnapshotAndReplace);
        }

        private void OnPetMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            dragging = true;
            moved = false;
            dragStartScreen = PointToScreen(e.GetPosition(this));
            dragLeft = Left;
            dragTop = Top;
            if (actionPlaying) EndAction();
            pet.CaptureMouse();
            e.Handled = true;
        }

        private void OnPetMouseMove(object sender, MouseEventArgs e)
        {
            if (!dragging) return;
            Point p = PointToScreen(e.GetPosition(this));
            double dx = p.X - dragStartScreen.X;
            double dy = p.Y - dragStartScreen.Y;
            if (!moved && Math.Abs(dx) + Math.Abs(dy) > 4) moved = true;
            if (moved)
            {
                Left = dragLeft + dx;
                Top = dragTop + dy;
                ClampToScreen();
            }
        }

        private void OnPetMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            pet.ReleaseMouseCapture();
            dragging = false;
            Log.Write("click moved=" + moved + " playing=" + actionPlaying);
            if (!moved)
            {
                if (actionFrames != null && actionFrames.Length > 0) StartAction();
                else Bounce();
            }
            else
            {
                settings.Left = Left;
                settings.Top = Top;
                settings.Save();
            }
            e.Handled = true;
        }

        private void CreateActionTimer()
        {
            actionTimer = new DispatcherTimer();
            actionTimer.Interval = TimeSpan.FromMilliseconds(1000.0 / 15.0);
            actionTimer.Tick += delegate
            {
                actionIndex++;
                if (actionFrames == null || actionIndex >= actionFrames.Length)
                {
                    EndAction();
                    return;
                }
                pet.Source = actionFrames[actionIndex];
            };
        }

        private void LoadActionAssets()
        {
            try
            {
                Assembly asm = Assembly.GetExecutingAssembly();

                Stream stream = asm.GetManifestResourceStream("naiwa.laugh.png");
                if (stream == null)
                {
                    string path = Path.Combine(Path.Combine(Settings.ExeDir, "assets"), "naiwa-laugh-strip.png");
                    if (File.Exists(path)) stream = File.OpenRead(path);
                }
                if (stream == null) return;

                BitmapImage strip;
                using (stream)
                {
                    strip = new BitmapImage();
                    strip.BeginInit();
                    strip.CacheOption = BitmapCacheOption.OnLoad;
                    strip.StreamSource = stream;
                    strip.EndInit();
                    strip.Freeze();
                }

                int count = strip.PixelWidth / 300;
                if (count <= 0) return;
                actionFrames = new BitmapSource[count];
                Log.Write("action frames loaded: " + count + " strip=" + strip.PixelWidth + "x" + strip.PixelHeight);
                for (int i = 0; i < count; i++)
                {
                    var frame = new CroppedBitmap(strip, new Int32Rect(i * 300, 0, 300, 480));
                    frame.Freeze();
                    actionFrames[i] = frame;
                }

                soundStream = asm.GetManifestResourceStream("naiwa.laugh.wav");
                if (soundStream == null)
                {
                    string wavPath = Path.Combine(Path.Combine(Settings.ExeDir, "assets"), "naiwa-laugh.wav");
                    if (File.Exists(wavPath)) soundStream = File.OpenRead(wavPath);
                }
                if (soundStream != null)
                {
                    sound = new SoundPlayer(soundStream);
                    try { sound.Load(); Log.Write("sound loaded"); } catch (Exception ex) { Log.Write("sound load failed: " + ex.Message); }
                }
            }
            catch (Exception ex)
            {
                Log.Write("action assets failed: " + ex.Message);
            }
        }

        private void StartAction()
        {
            Log.Write("start action, frames=" + (actionFrames == null ? 0 : actionFrames.Length));
            if (actionPlaying || actionFrames == null || actionFrames.Length == 0) return;
            actionPlaying = true;
            actionIndex = 0;
            ShowActionFrame();
            actionTimer.Start();
            if (!settings.Muted && sound != null)
            {
                try { sound.Play(); Log.Write("sound play"); } catch (Exception ex) { Log.Write("sound play failed: " + ex.Message); }
            }
        }

        private void ShowActionFrame()
        {
            pet.Source = actionFrames[actionIndex];
            pet.Width = actionW;
            pet.Height = actionH;
            Canvas.SetLeft(pet, (Width - actionW) / 2.0);
            Canvas.SetTop(pet, Height - actionH - IdleBottomMargin);
        }

        private void EndAction()
        {
            if (!actionPlaying) return;
            actionPlaying = false;
            if (actionTimer != null) actionTimer.Stop();
            pet.Source = idleSource;
            double h = PetHeights[settings.SizeMode];
            double w = h * 0.5;
            pet.Width = w;
            pet.Height = h;
            Canvas.SetLeft(pet, 15);
            Canvas.SetTop(pet, Height - h - IdleBottomMargin);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Topmost = true;
        }

        private void OnClosed(object sender, EventArgs e)
        {
            settings.Left = Left;
            settings.Top = Top;
            settings.Save();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            int style = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, style | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);

            HwndSource source = HwndSource.FromHwnd(hwnd);
            if (source != null) source.AddHook(WndProc);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_NCHITTEST && IsLoaded)
            {
                int lp = lParam.ToInt32();
                int x = (short)(lp & 0xFFFF);
                int y = (short)((lp >> 16) & 0xFFFF);
                if (!IsPetPixelAt(x, y))
                {
                    handled = true;
                    return new IntPtr(HTTRANSPARENT);
                }
            }
            return IntPtr.Zero;
        }

        private bool IsPetPixelAt(int screenX, int screenY)
        {
            try
            {
                Point p = pet.PointFromScreen(new Point(screenX, screenY));
                if (p.X < 0 || p.Y < 0 || p.X >= pet.ActualWidth || p.Y >= pet.ActualHeight) return false;
                if (alphaMask == null) return true;
                int ix = (int)(p.X / pet.ActualWidth * maskW);
                int iy = (int)(p.Y / pet.ActualHeight * maskH);
                if (ix < 0) ix = 0;
                if (iy < 0) iy = 0;
                if (ix >= maskW) ix = maskW - 1;
                if (iy >= maskH) iy = maskH - 1;
                return alphaMask[iy * maskW + ix] > 25;
            }
            catch
            {
                return true;
            }
        }
    }
}