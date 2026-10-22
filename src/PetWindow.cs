using System;
using System.Collections.Generic;
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
using System.Windows.Shapes;
using Path = System.IO.Path;
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

        private const double SideMargin = 65;
        private const double IdleBottomMargin = 16;
        private const double ActionAspect = 0.625;

        private readonly Settings settings;
        private readonly Image pet;
        private readonly Canvas overlay;
        private readonly ScaleTransform breath;
        private readonly ScaleTransform clickScale;
        private readonly TranslateTransform idleShift;
        private readonly TranslateTransform clickShift;
        private readonly RotateTransform tilt;
        private readonly RotateTransform walkTilt;

        private BitmapSource idleSource;
        private BitmapSource defaultIdle;
        private BitmapSource skinIdle;
        private double idleW;
        private double idleH;
        private BitmapSource[] actionFrames;
        private DispatcherTimer actionTimer;
        private int actionIndex;
        private bool actionPlaying;
        private double actionW;
        private double actionH;

        private MediaPlayer mediaPlayer;
        private SoundPlayer fallbackPlayer;
        private Stream soundStream;
        private string soundPath;

        private Ellipse pupilL;
        private Ellipse pupilR;
        private Rectangle lidL;
        private Rectangle lidR;
        private Line browL;
        private Line browR;
        private Canvas accessory;
        private Canvas angerMark;

        private DispatcherTimer blinkTimer;
        private DispatcherTimer lifeTimer;
        private DispatcherTimer eyeTimer;
        private DispatcherTimer tiltTimer;
        private DateTime lastInteraction = DateTime.Now;
        private DateTime lastShake = DateTime.Now;
        private int angerLevel;
        private readonly Queue<DateTime> clickTimes = new Queue<DateTime>();
        private readonly List<MiniPetWindow> minis = new List<MiniPetWindow>();
        private SettingsWindow settingsWindow;
        private TodoWindow todoWindow;

        private List<TodoItem> todos;
        private DispatcherTimer reminderTimer;
        private DateTime nextReminderAt;
        private bool focusActive;
        private DateTime focusUntil;
        private TodoItem focusTask;
        private BubbleWindow bubble;
        private int angerThresholdMinutes = 10;
        private bool angryNow;

        private DispatcherTimer behaviorTimer;
        private bool walking;
        private DateTime walkUntil;
        private DateTime nextDecision = DateTime.Now.AddSeconds(4);
        private int walkDir = 1;
        private double walkSpeed = 45;
        private readonly Random random = new Random();

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

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT pt);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        public PetWindow(Settings settings)
        {
            this.settings = settings;

            Title = "NaiWa";
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = null;
            Topmost = settings.Topmost;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.Manual;

            var canvas = new Canvas();
            Content = canvas;

            pet = new Image();
            defaultIdle = LoadPetBitmap();
            idleSource = defaultIdle;
            pet.Source = idleSource;
            pet.Stretch = Stretch.Fill;
            RenderOptions.SetBitmapScalingMode(pet, BitmapScalingMode.HighQuality);
            pet.RenderTransformOrigin = new Point(0.5, 1.0);

            var group = new TransformGroup();
            breath = new ScaleTransform(1, 1);
            clickScale = new ScaleTransform(1, 1);
            idleShift = new TranslateTransform(0, 0);
            clickShift = new TranslateTransform(0, 0);
            tilt = new RotateTransform(0, 0, 0);
            walkTilt = new RotateTransform(0, 0, 0);
            group.Children.Add(breath);
            group.Children.Add(clickScale);
            group.Children.Add(idleShift);
            group.Children.Add(clickShift);
            group.Children.Add(tilt);
            group.Children.Add(walkTilt);
            pet.RenderTransform = group;
            canvas.Children.Add(pet);

            overlay = new Canvas();
            canvas.Children.Add(overlay);
            BuildOverlay();
            BuildAccessory();

            BuildAlphaMask();
            BuildContextMenu();
            ApplySize(false);
            PlaceWindow();
            StartIdle();
            LoadActionAssets();
            CreateActionTimer();
            PrepareSound();
            ApplyTopmost(settings.Topmost);
            todos = TodoStore.Load();
            nextReminderAt = DateTime.Now.AddMinutes(settings.TodoIntervalMinutes);

            pet.MouseLeftButtonDown += OnPetMouseDown;
            pet.MouseMove += OnPetMouseMove;
            pet.MouseLeftButtonUp += OnPetMouseUp;

            Closed += OnClosed;
            Loaded += OnLoaded;
        }

        public Settings Config
        {
            get { return settings; }
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

        private void BuildOverlay()
        {
            pupilL = new Ellipse();
            pupilL.Fill = new SolidColorBrush(Color.FromRgb(45, 30, 15));
            pupilR = new Ellipse();
            pupilR.Fill = pupilL.Fill;
            lidL = new Rectangle();
            lidL.Fill = new SolidColorBrush(Color.FromRgb(243, 198, 79));
            lidL.RadiusX = 4;
            lidL.RadiusY = 4;
            lidR = new Rectangle();
            lidR.Fill = lidL.Fill;
            lidR.RadiusX = 4;
            lidR.RadiusY = 4;
            browL = new Line();
            browL.Stroke = new SolidColorBrush(Color.FromRgb(74, 52, 24));
            browL.StrokeThickness = 2.5;
            browL.StrokeStartLineCap = PenLineCap.Round;
            browL.StrokeEndLineCap = PenLineCap.Round;
            browL.Opacity = 0;
            browR = new Line();
            browR.Stroke = browL.Stroke;
            browR.StrokeThickness = 2.5;
            browR.StrokeStartLineCap = PenLineCap.Round;
            browR.StrokeEndLineCap = PenLineCap.Round;
            browR.Opacity = 0;

            overlay.Children.Add(pupilL);
            overlay.Children.Add(pupilR);
            overlay.Children.Add(lidL);
            overlay.Children.Add(lidR);
            overlay.Children.Add(browL);
            overlay.Children.Add(browR);
            overlay.Visibility = Visibility.Visible;

            pupilL.Visibility = Visibility.Hidden;
            pupilR.Visibility = Visibility.Hidden;
            lidL.Visibility = Visibility.Hidden;
            lidR.Visibility = Visibility.Hidden;
            browL.Visibility = Visibility.Hidden;
            browR.Visibility = Visibility.Hidden;

            angerMark = new Canvas();
            angerMark.Opacity = 0;
            BuildAngerMark();
            overlay.Children.Add(angerMark);
        }

        private void BuildAngerMark()
        {
            angerMark.Children.Clear();
            var stroke = new SolidColorBrush(Color.FromRgb(210, 48, 32));
            double[][] segs = new double[][]
            {
                new double[] { 0, -9, 0, -3 },
                new double[] { 0, 3, 0, 9 },
                new double[] { -9, 0, -3, 0 },
                new double[] { 3, 0, 9, 0 },
                new double[] { -6, -6, -3, -3 },
                new double[] { 3, 3, 6, 6 },
                new double[] { 3, -3, 6, -6 },
                new double[] { -6, 6, -3, 3 }
            };
            for (int i = 0; i < segs.Length; i++)
            {
                var ln = new Line();
                ln.X1 = segs[i][0];
                ln.Y1 = segs[i][1];
                ln.X2 = segs[i][2];
                ln.Y2 = segs[i][3];
                ln.Stroke = stroke;
                ln.StrokeThickness = 2.4;
                ln.StrokeStartLineCap = PenLineCap.Round;
                ln.StrokeEndLineCap = PenLineCap.Round;
                angerMark.Children.Add(ln);
            }
        }

        private void BuildAccessory()
        {
            if (accessory != null) overlay.Children.Remove(accessory);
            accessory = new Canvas();
            overlay.Children.Add(accessory);
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

            var todoMenuItem = new MenuItem();
            todoMenuItem.Header = "待办…";
            todoMenuItem.Click += delegate { OpenTodos(); };
            menu.Items.Add(todoMenuItem);

            var settingsItem = new MenuItem();
            settingsItem.Header = "设置…";
            settingsItem.Click += delegate { OpenSettings(); };
            menu.Items.Add(settingsItem);

            menu.Items.Add(new Separator());

            var muteItem = new MenuItem();
            muteItem.Header = "静音";
            muteItem.IsCheckable = true;
            muteItem.Click += delegate
            {
                settings.Muted = !settings.Muted;
                settings.Save();
            };
            menu.Items.Add(muteItem);

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
                RegisterInteraction();
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

        public void SetSizeMode(int mode)
        {
            if (mode < 0) mode = 0;
            if (mode > 2) mode = 2;
            settings.SizeMode = mode;
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
            double winW = w + SideMargin * 2;
            double winH = h + 40;

            if (keepAnchor && !double.IsNaN(Left))
            {
                Left = Left + (oldW - winW) / 2;
                Top = Top + (oldH - winH);
            }

            Width = winW;
            Height = winH;
            ApplyIdleLayout();

            actionW = h * ActionAspect;
            actionH = h;
            if (actionW > winW - 4)
            {
                actionW = winW - 4;
                actionH = actionW / ActionAspect;
            }

            overlay.Width = winW;
            overlay.Height = winH;
            UpdateOverlayLayout();

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

        public void ResetPosition()
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

        public void ApplyAutoStart(bool on)
        {
            SetAutoStart(on);
        }

        private void StartIdle()
        {
            if (!settings.IdleAnimation) return;
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

        private void StopIdle()
        {
            breath.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            idleShift.BeginAnimation(TranslateTransform.YProperty, null);
            breath.ScaleY = 1;
            idleShift.Y = 0;
        }

        public void ApplyIdleAnimation(bool on)
        {
            settings.IdleAnimation = on;
            if (on) StartIdle(); else StopIdle();
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
        }

        private void Squash()
        {
            var sy = new DoubleAnimationUsingKeyFrames();
            sy.Duration = TimeSpan.FromMilliseconds(420);
            sy.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            sy.KeyFrames.Add(new EasingDoubleKeyFrame(0.9, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(90)),
                new QuadraticEase { EasingMode = EasingMode.EaseOut }));
            sy.KeyFrames.Add(new EasingDoubleKeyFrame(1.03, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(220))));
            sy.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(420))));
            clickScale.BeginAnimation(ScaleTransform.ScaleYProperty, sy, HandoffBehavior.SnapshotAndReplace);
            var sx = new DoubleAnimationUsingKeyFrames();
            sx.Duration = TimeSpan.FromMilliseconds(420);
            sx.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            sx.KeyFrames.Add(new EasingDoubleKeyFrame(1.04, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(90))));
            sx.KeyFrames.Add(new EasingDoubleKeyFrame(0.99, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(220))));
            sx.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(420))));
            clickScale.BeginAnimation(ScaleTransform.ScaleXProperty, sx, HandoffBehavior.SnapshotAndReplace);
        }

        private void OnPetMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            RegisterInteraction();
            StopWalking();
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
                tilt.Angle = Clamp(dx * 0.35, -12, 12);
            }
        }

        private void OnPetMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            pet.ReleaseMouseCapture();
            dragging = false;
            RegisterInteraction();
            if (!moved)
            {
                bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
                if (ctrl)
                {
                    StartAction();
                    SpawnMinis(8);
                }
                else
                {
                    RegisterClick();
                    StartAction();
                }
            }
            else
            {
                settings.Left = Left;
                settings.Top = Top;
                settings.Save();
                tilt.BeginAnimation(RotateTransform.AngleProperty,
                    new DoubleAnimation(0, TimeSpan.FromMilliseconds(220)));
                Squash();
            }
            e.Handled = true;
        }

        private void RegisterClick()
        {
            DateTime now = DateTime.Now;
            clickTimes.Enqueue(now);
            while (clickTimes.Count > 0 && (now - clickTimes.Peek()).TotalSeconds > 4.0) clickTimes.Dequeue();
            if (clickTimes.Count >= 5)
            {
                clickTimes.Clear();
                StartCombo();
            }
        }

        private void StartCombo()
        {
            SpawnMinis(10);
            Bounce();
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
                for (int i = 0; i < count; i++)
                {
                    var frame = new CroppedBitmap(strip, new Int32Rect(i * 300, 0, 300, 480));
                    frame.Freeze();
                    actionFrames[i] = frame;
                }
            }
            catch (Exception ex)
            {
                Log.Write("action assets failed: " + ex.Message);
            }
        }

        private void PrepareSound()
        {
            try
            {
                Assembly asm = Assembly.GetExecutingAssembly();
                Stream stream = asm.GetManifestResourceStream("naiwa.laugh.wav");
                if (stream == null)
                {
                    string wavPath = Path.Combine(Path.Combine(Settings.ExeDir, "assets"), "naiwa-laugh.wav");
                    if (File.Exists(wavPath)) stream = File.OpenRead(wavPath);
                }
                if (stream == null) return;

                byte[] bytes;
                using (stream)
                {
                    var ms = new MemoryStream();
                    stream.CopyTo(ms);
                    bytes = ms.ToArray();
                }

                string[] candidates = new string[]
                {
                    Path.Combine(Settings.ExeDir, "NaiWa-sound.tmp.wav"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NaiWa", "sound.wav"),
                    Path.Combine(Path.GetTempPath(), "NaiWa-sound.wav")
                };

                for (int i = 0; i < candidates.Length; i++)
                {
                    try
                    {
                        string dir = Path.GetDirectoryName(candidates[i]);
                        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                        File.WriteAllBytes(candidates[i], bytes);
                        soundPath = candidates[i];
                        break;
                    }
                    catch { }
                }

                if (soundPath != null)
                {
                    mediaPlayer = new MediaPlayer();
                    mediaPlayer.Open(new Uri(soundPath));
                    mediaPlayer.Volume = settings.Volume / 100.0;
                }
                else
                {
                    fallbackPlayer = new SoundPlayer(new MemoryStream(bytes));
                    fallbackPlayer.Load();
                }
            }
            catch (Exception ex)
            {
                Log.Write("sound prepare failed: " + ex.Message);
            }
        }

        private void PlaySound()
        {
            if (settings.Muted || settings.Volume <= 0) return;
            try
            {
                if (mediaPlayer != null)
                {
                    mediaPlayer.Stop();
                    mediaPlayer.Position = TimeSpan.Zero;
                    mediaPlayer.Volume = settings.Volume / 100.0;
                    mediaPlayer.Play();
                }
                else if (fallbackPlayer != null)
                {
                    fallbackPlayer.Play();
                }
            }
            catch (Exception ex)
            {
                Log.Write("sound play failed: " + ex.Message);
            }
        }

        public void ApplyVolume(int volume)
        {
            if (mediaPlayer != null) mediaPlayer.Volume = Math.Max(0, Math.Min(100, volume)) / 100.0;
        }

        private void StartAction()
        {
            StopWalking();
            if (actionPlaying || actionFrames == null || actionFrames.Length == 0) return;
            actionPlaying = true;
            actionIndex = 0;
            ShowOverlays(false);
            ShowActionFrame();
            actionTimer.Start();
            PlaySound();
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
            ApplyIdleLayout();
            UpdateOverlayLayout();
            ShowOverlays(true);
        }

        private void ShowOverlays(bool show)
        {
            Visibility v = show ? Visibility.Visible : Visibility.Hidden;
            if (accessory != null) accessory.Visibility = (settings.Skin == 0 && show) ? Visibility.Visible : Visibility.Hidden;
            if (angerMark != null) angerMark.Visibility = v;
            UpdateAngerVisual();
        }

        private double ScaleFactor
        {
            get { return PetHeights[settings.SizeMode] / 300.0; }
        }

        private void UpdateOverlayLayout()
        {
            if (pet == null || overlay == null) return;
            double w = pet.Width;
            double h = pet.Height;
            double left = Canvas.GetLeft(pet);
            double top = Canvas.GetTop(pet);
            if (double.IsNaN(left)) left = SideMargin;
            if (double.IsNaN(top)) top = IdleBottomMargin;
            double s = ScaleFactor;

            double ex1 = left + 78.0 / 240.0 * w;
            double ey1 = top + 28.0 / 480.0 * h;
            double ex2 = left + 94.0 / 240.0 * w;
            double ey2 = top + 26.0 / 480.0 * h;

            pupilL.Width = 4.2 * s;
            pupilL.Height = 5.0 * s;
            pupilR.Width = 4.2 * s;
            pupilR.Height = 5.0 * s;
            SetPos(pupilL, ex1, ey1);
            SetPos(pupilR, ex2, ey2);

            lidL.Width = 13.0 * s;
            lidR.Width = 13.0 * s;
            SetPos(lidL, ex1 - 6.5 * s, ey1 - 8.0 * s);
            SetPos(lidR, ex2 - 6.5 * s, ey2 - 8.0 * s);

            browL.X1 = ex1 - 6.5 * s;
            browL.Y1 = ey1 - 10.5 * s;
            browL.X2 = ex1 + 5.5 * s;
            browL.Y2 = ey1 - 6.0 * s;
            browR.X1 = ex2 + 6.5 * s;
            browR.Y1 = ey2 - 10.5 * s;
            browR.X2 = ex2 - 5.5 * s;
            browR.Y2 = ey2 - 6.0 * s;

            BuildAccessoryShapes(ex1, ey1, ex2, ey2, s);

            if (angerMark != null)
            {
                double ax = left + w * 0.66;
                double ay = top + h * 0.09;
                angerMark.RenderTransform = new ScaleTransform(s, s);
                SetPos(angerMark, ax, ay);
            }
        }

        private static void SetPos(UIElement el, double x, double y)
        {
            Canvas.SetLeft(el, x);
            Canvas.SetTop(el, y);
        }

        private void BuildAccessoryShapes(double ex1, double ey1, double ex2, double ey2, double s)
        {
            if (accessory == null) return;
            accessory.Children.Clear();
            return;
            double hx = (ex1 + ex2) / 2.0;
            double headTop = ey1 - 18.0 * s;

            if (settings.Skin == 1)
            {
                var hat = new Polygon();
                hat.Points.Add(new Point(hx - 16 * s, headTop + 6 * s));
                hat.Points.Add(new Point(hx + 16 * s, headTop + 6 * s));
                hat.Points.Add(new Point(hx + 4 * s, headTop - 18 * s));
                hat.Fill = new SolidColorBrush(Color.FromRgb(200, 40, 40));
                accessory.Children.Add(hat);
                var band = new Rectangle();
                band.Width = 34 * s;
                band.Height = 5 * s;
                band.Fill = Brushes.White;
                band.RadiusX = 2.5 * s;
                band.RadiusY = 2.5 * s;
                SetPos(band, hx - 17 * s, headTop + 3 * s);
                accessory.Children.Add(band);
                var pompom = new Ellipse();
                pompom.Width = 7 * s;
                pompom.Height = 7 * s;
                pompom.Fill = Brushes.White;
                SetPos(pompom, hx + 2 * s, headTop - 15 * s);
                accessory.Children.Add(pompom);
            }
            else if (settings.Skin == 2)
            {
                var center = new Point(hx - 6 * s, headTop + 2 * s);
                for (int i = 0; i < 5; i++)
                {
                    double a = Math.PI * 2 * i / 5.0;
                    var petal = new Ellipse();
                    petal.Width = 6 * s;
                    petal.Height = 6 * s;
                    petal.Fill = new SolidColorBrush(Color.FromRgb(250, 130, 160));
                    SetPos(petal, center.X + Math.Cos(a) * 5 * s - 3 * s, center.Y + Math.Sin(a) * 5 * s - 3 * s);
                    accessory.Children.Add(petal);
                }
                var core = new Ellipse();
                core.Width = 5 * s;
                core.Height = 5 * s;
                core.Fill = new SolidColorBrush(Color.FromRgb(250, 210, 80));
                SetPos(core, center.X - 2.5 * s, center.Y - 2.5 * s);
                accessory.Children.Add(core);
            }
            else if (settings.Skin == 3)
            {
                var lens1 = new Rectangle();
                lens1.Width = 14 * s;
                lens1.Height = 9 * s;
                lens1.RadiusX = 4 * s;
                lens1.RadiusY = 4 * s;
                lens1.Fill = new SolidColorBrush(Color.FromArgb(200, 25, 25, 25));
                SetPos(lens1, ex1 - 7 * s, ey1 - 5 * s);
                accessory.Children.Add(lens1);
                var lens2 = new Rectangle();
                lens2.Width = 14 * s;
                lens2.Height = 9 * s;
                lens2.RadiusX = 4 * s;
                lens2.RadiusY = 4 * s;
                lens2.Fill = lens1.Fill;
                SetPos(lens2, ex2 - 7 * s, ey2 - 5 * s);
                accessory.Children.Add(lens2);
                var bridge = new Line();
                bridge.X1 = ex1 + 7 * s;
                bridge.Y1 = ey1 - 1 * s;
                bridge.X2 = ex2 - 7 * s;
                bridge.Y2 = ey2 - 1 * s;
                bridge.Stroke = Brushes.Black;
                bridge.StrokeThickness = 1.5 * s;
                accessory.Children.Add(bridge);
            }
        }


        public void ApplyTopmost(bool on)
        {
            settings.Topmost = on;
            Topmost = on;
        }

        public void ApplyClickThrough(bool on)
        {
            settings.ClickThrough = on;
        }

        public void OpenSettings()
        {
            RegisterInteraction();
            if (settingsWindow != null)
            {
                settingsWindow.Activate();
                return;
            }
            settingsWindow = new SettingsWindow(this, settings);
            settingsWindow.Closed += delegate { settingsWindow = null; };
            settingsWindow.Show();
            settingsWindow.Activate();
        }

        private void SpawnMinis(int count)
        {
            if (actionFrames == null || actionFrames.Length == 0) return;
            double h = PetHeights[settings.SizeMode];
            double w = h * 0.5;
            double mw = w / 3.0;
            double mh = h / 3.0;
            double startLeft = Left + (Width - mw) / 2.0;
            double startTop = Top + Height * 0.42;
            var rnd = new Random();
            for (int i = 0; i < count; i++)
            {
                double angle = -Math.PI / 2.0 + (rnd.NextDouble() - 0.5) * 2.2;
                double speed = 90 + rnd.NextDouble() * 150;
                var mini = new MiniPetWindow(this, actionFrames, startLeft, startTop, mw, mh,
                    Math.Cos(angle) * speed, Math.Sin(angle) * speed);
                minis.Add(mini);
                mini.Closed += delegate { };
                mini.Show();
            }
        }

        internal void RemoveMini(MiniPetWindow mini)
        {
            minis.Remove(mini);
        }

        private void StartLifeTimer()
        {
            lifeTimer = new DispatcherTimer();
            lifeTimer.Interval = TimeSpan.FromSeconds(5);
            lifeTimer.Tick += delegate { UpdateAnger(); };
            lifeTimer.Start();
        }

        private void UpdateAnger()
        {
            if (!IsVisible) return;
            double mins = (DateTime.Now - lastInteraction).TotalMinutes;
            if (!angryNow && mins >= angerThresholdMinutes)
            {
                angryNow = true;
                angerLevel = 2;
                UpdateAngerVisual();
                AngryShake();
                int next = angerThresholdMinutes >= 10 ? 5 : Math.Max(1, angerThresholdMinutes - 1);
                angerThresholdMinutes = next;
                if (!actionPlaying) ShowBubble("哼！" + DisplayName + "都不理我…");
            }
            if (angryNow && (DateTime.Now - lastShake).TotalSeconds > 16)
            {
                lastShake = DateTime.Now;
                AngryShake();
            }
        }

        private void UpdateAngerVisual()
        {
            if (angerMark == null) return;
            if (actionPlaying)
            {
                angerMark.Opacity = 0;
                return;
            }
            double op = angerLevel >= 2 ? 1.0 : angerLevel == 1 ? 0.55 : 0.0;
            angerMark.Opacity = op;
        }

        private void AngryShake()
        {
            var shake = new DoubleAnimationUsingKeyFrames();
            shake.Duration = TimeSpan.FromMilliseconds(420);
            shake.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(0))));
            shake.KeyFrames.Add(new EasingDoubleKeyFrame(-3, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(80))));
            shake.KeyFrames.Add(new EasingDoubleKeyFrame(3, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(180))));
            shake.KeyFrames.Add(new EasingDoubleKeyFrame(-2, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(280))));
            shake.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(420))));
            idleShift.BeginAnimation(TranslateTransform.XProperty, shake);
        }

        private void RegisterInteraction()
        {
            lastInteraction = DateTime.Now;
            angryNow = false;
            if (angerLevel != 0)
            {
                angerLevel = 0;
                UpdateAngerVisual();
            }
        }

        private void StartEyeTracking()
        {
            eyeTimer = new DispatcherTimer();
            eyeTimer.Interval = TimeSpan.FromMilliseconds(120);
            eyeTimer.Tick += delegate { UpdateEyes(); };
            eyeTimer.Start();
        }

        private void UpdateEyes()
        {
            if (!IsLoaded) return;
            if (actionPlaying || !settings.IdleAnimation || dragging || walking) return;
            POINT p;
            if (!GetCursorPos(out p)) return;
            try
            {
                Point local = PointFromScreen(new Point(p.X, p.Y));
                double w = pet.Width;
                double h = pet.Height;
                double left = Canvas.GetLeft(pet);
                double top = Canvas.GetTop(pet);
                double cx = left + w / 2.0;
                double cy = top + h * 0.35;
                double dx = local.X - cx;
                double target = Clamp(dx / 55.0, -4.0, 4.0);
                if (Math.Abs(dx) < 40) target = 0;
                tilt.Angle = tilt.Angle + (target - tilt.Angle) * 0.2;
            }
            catch { }
        }

        private void StartBlinkTimer()
        {
            blinkTimer = new DispatcherTimer();
            blinkTimer.Interval = TimeSpan.FromSeconds(3 + new Random().NextDouble() * 3);
            blinkTimer.Tick += delegate
            {
                blinkTimer.Stop();
                DoBlink();
                blinkTimer.Interval = TimeSpan.FromSeconds(3 + new Random().NextDouble() * 3);
                blinkTimer.Start();
            };
            blinkTimer.Start();
        }

        private void DoBlink()
        {
            if (actionPlaying || !settings.IdleAnimation) return;
            double s = ScaleFactor;
            double full = 16.0 * s;
            var a = new DoubleAnimation();
            a.From = 0;
            a.To = full;
            a.Duration = TimeSpan.FromMilliseconds(70);
            a.AutoReverse = true;
            lidL.BeginAnimation(HeightProperty, a);
            lidR.BeginAnimation(HeightProperty, a);
        }

        private void StartTiltTimer()
        {
            tiltTimer = new DispatcherTimer();
            tiltTimer.Interval = TimeSpan.FromSeconds(8 + new Random().NextDouble() * 6);
            tiltTimer.Tick += delegate
            {
                tiltTimer.Stop();
                if (!actionPlaying && settings.IdleAnimation && !dragging)
                {
                    double angle = (new Random().NextDouble() - 0.5) * 5.0;
                    var a = new DoubleAnimation(angle, TimeSpan.FromSeconds(1.1));
                    a.AutoReverse = true;
                    a.EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut };
                    tilt.BeginAnimation(RotateTransform.AngleProperty, a);
                }
                tiltTimer.Interval = TimeSpan.FromSeconds(8 + new Random().NextDouble() * 6);
                tiltTimer.Start();
            };
            tiltTimer.Start();
        }

        private string DisplayName
        {
            get { return string.IsNullOrEmpty(settings.UserName) ? "你" : settings.UserName; }
        }

        public List<TodoItem> Todos
        {
            get { return todos; }
        }

        public bool FocusActive
        {
            get { return focusActive; }
        }

        public DateTime FocusUntil
        {
            get { return focusUntil; }
        }

        public DateTime NextReminderAt
        {
            get { return nextReminderAt; }
        }

        public int ReminderMinutes
        {
            get { return settings.TodoIntervalMinutes; }
        }

        public void SetReminderInterval(int minutes)
        {
            settings.TodoIntervalMinutes = Math.Max(1, Math.Min(1440, minutes));
            settings.Save();
            nextReminderAt = DateTime.Now.AddMinutes(settings.TodoIntervalMinutes);
        }

        public void AddTodo(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            todos.Add(new TodoItem(text.Trim()));
            TodoStore.Save(todos);
        }

        public void SetTodoDone(TodoItem item, bool done)
        {
            if (item == null) return;
            item.Done = done;
            TodoStore.Save(todos);
            if (!done) return;
            nextReminderAt = DateTime.Now.AddMinutes(settings.TodoIntervalMinutes);
            angerThresholdMinutes = 10;
            if (focusActive && focusTask == item)
            {
                focusActive = false;
                focusTask = null;
                ShowBubble("完成啦！" + DisplayName + "真棒！");
            }
            else
            {
                ShowBubble("完成一件！继续保持～");
            }
            Bounce();
        }

        public void DeleteTodo(TodoItem item)
        {
            if (item == null) return;
            todos.Remove(item);
            TodoStore.Save(todos);
        }

        public void StartFocus(TodoItem item, int minutes)
        {
            if (item == null || item.Done) return;
            focusActive = true;
            focusTask = item;
            focusUntil = DateTime.Now.AddMinutes(Math.Max(1, minutes));
            ShowBubble(DisplayName + "，专注 " + minutes + " 分钟，我陪你～");
        }

        private void EndFocus()
        {
            focusActive = false;
            TodoItem t = focusTask;
            focusTask = null;
            nextReminderAt = DateTime.Now.AddMinutes(settings.TodoIntervalMinutes);
            if (t != null && !t.Done)
            {
                ShowBubble(ReminderText(t));
                StartAction();
            }
            else
            {
                ShowBubble("专注结束，辛苦啦～");
            }
        }

        public void CancelFocus()
        {
            focusActive = false;
            focusTask = null;
            nextReminderAt = DateTime.Now.AddMinutes(settings.TodoIntervalMinutes);
        }

        public void OpenTodos()
        {
            RegisterInteraction();
            if (todoWindow != null)
            {
                todoWindow.Activate();
                return;
            }
            todoWindow = new TodoWindow(this);
            todoWindow.Closed += delegate { todoWindow = null; };
            todoWindow.Show();
            todoWindow.Activate();
        }

        public void ShowBubble(string text)
        {
            try
            {
                BubbleWindow old = bubble;
                bubble = new BubbleWindow(this, text);
                bubble.Show();
                if (old != null) old.Close();
            }
            catch (Exception ex)
            {
                Log.Write("bubble failed: " + ex.Message);
            }
        }

        public void OnOnboardingName(string name)
        {
            ShowBubble("你好，" + name + "！我是 NaiWa，以后请多关照～");
            Bounce();
        }

        private void StartReminderTimer()
        {
            reminderTimer = new DispatcherTimer();
            reminderTimer.Interval = TimeSpan.FromSeconds(10);
            reminderTimer.Tick += delegate { ReminderTick(); };
            reminderTimer.Start();
        }

        private void ReminderTick()
        {
            if (!IsVisible) return;
            if (focusActive)
            {
                if (DateTime.Now >= focusUntil) EndFocus();
                return;
            }
            if (actionPlaying) return;
            if (DateTime.Now < nextReminderAt) return;

            var undone = new List<TodoItem>();
            for (int i = 0; i < todos.Count; i++)
            {
                if (!todos[i].Done) undone.Add(todos[i]);
            }
            if (undone.Count == 0)
            {
                nextReminderAt = DateTime.Now.AddMinutes(settings.TodoIntervalMinutes);
                return;
            }
            TodoItem t = undone[new Random().Next(undone.Count)];
            nextReminderAt = DateTime.Now.AddMinutes(settings.TodoIntervalMinutes);
            ShowBubble(ReminderText(t));
            StartAction();
        }

        private string ReminderText(TodoItem t)
        {
            return DisplayName + "小宝贝😂😂😂怎么还没有做完「" + t.Text + "」";
        }

        public void ApplyWalkEnabled(bool on)
        {
            settings.WalkEnabled = on;
            settings.Save();
            if (!on) StopWalking();
        }

        private void StartWalkingEngine()
        {
            behaviorTimer = new DispatcherTimer();
            behaviorTimer.Interval = TimeSpan.FromMilliseconds(200);
            behaviorTimer.Tick += delegate { BehaviorTick(); };
            behaviorTimer.Start();
        }

        private void BehaviorTick()
        {
            if (!IsLoaded || !settings.WalkEnabled || dragging || actionPlaying || focusActive || !IsVisible)
            {
                if (walking) StopWalking();
                return;
            }
            DateTime now = DateTime.Now;
            if (walking)
            {
                double mult = angerLevel >= 2 ? 1.9 : angerLevel == 1 ? 1.4 : 1.0;
                Left += walkDir * walkSpeed * mult * 0.2;
                double minLeft = SystemParameters.VirtualScreenLeft;
                double maxLeft = minLeft + SystemParameters.VirtualScreenWidth - Width;
                if (Left <= minLeft) { Left = minLeft; walkDir = 1; }
                if (Left >= maxLeft) { Left = maxLeft; walkDir = -1; }
                walkTilt.Angle = Math.Sin(now.TimeOfDay.TotalMilliseconds / 170.0) * 2.6 * mult;
                if (now >= walkUntil)
                {
                    StopWalking();
                    ScheduleNextDecision(now);
                }
            }
            else if (now >= nextDecision)
            {
                walkTilt.BeginAnimation(RotateTransform.AngleProperty, null);
                tilt.Angle = 0;
                walking = true;
                walkDir = random.Next(2) == 0 ? -1 : 1;
                walkSpeed = 38 + random.NextDouble() * 28;
                walkUntil = now.AddSeconds(2.5 + random.NextDouble() * 4.5);
            }
        }

        private void ScheduleNextDecision(DateTime now)
        {
            double min = angerLevel >= 2 ? 1.2 : angerLevel == 1 ? 2.2 : 3.5;
            double span = angerLevel >= 2 ? 1.8 : angerLevel == 1 ? 2.8 : 4.5;
            nextDecision = now.AddSeconds(min + random.NextDouble() * span);
        }

        private void StopWalking()
        {
            if (!walking) return;
            walking = false;
            walkTilt.BeginAnimation(RotateTransform.AngleProperty,
                new DoubleAnimation(0, TimeSpan.FromMilliseconds(260)));
        }

        private void ApplyIdleLayout()
        {
            double h = PetHeights[settings.SizeMode];
            double w = h * 0.5;
            double iw = w;
            double ih = h;
            if (settings.Skin > 0 && skinIdle != null)
            {
                double aspect = (double)skinIdle.PixelWidth / (double)skinIdle.PixelHeight;
                ih = h;
                iw = ih * aspect;
                double maxW = Width - 4;
                if (iw > maxW)
                {
                    iw = maxW;
                    ih = iw / aspect;
                }
            }
            idleW = iw;
            idleH = ih;
            pet.Width = iw;
            pet.Height = ih;
            Canvas.SetLeft(pet, (Width - iw) / 2.0);
            Canvas.SetTop(pet, Height - ih - IdleBottomMargin);
        }

        public void ApplySkin(int skin)
        {
            ApplySkin(skin, true);
        }

        private void ApplySkin(int skin, bool save)
        {
            if (skin < 0) skin = 0;
            if (skin > 4) skin = 4;
            settings.Skin = skin;
            if (save) settings.Save();
            skinIdle = null;
            if (skin == 0)
            {
                idleSource = defaultIdle;
            }
            else
            {
                BitmapSource bmp = LoadSkinBitmap(skin);
                if (bmp != null)
                {
                    skinIdle = bmp;
                    idleSource = bmp;
                }
                else
                {
                    idleSource = defaultIdle;
                }
            }
            if (!actionPlaying)
            {
                pet.Source = idleSource;
                ApplyIdleLayout();
                BuildAlphaMask();
                UpdateOverlayLayout();
            }
            if (accessory != null) accessory.Visibility = (skin == 0 && !actionPlaying) ? Visibility.Visible : Visibility.Hidden;
        }

        private static BitmapSource LoadSkinBitmap(int skin)
        {
            try
            {
                Assembly asm = Assembly.GetExecutingAssembly();
                Stream stream = asm.GetManifestResourceStream("naiwa.skin" + skin + ".png");
                if (stream == null)
                {
                    string p = Path.Combine(Path.Combine(Settings.ExeDir, "assets"), "skin" + skin + ".png");
                    if (File.Exists(p)) stream = File.OpenRead(p);
                }
                if (stream == null) return null;
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
            catch { return null; }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ApplySkin(settings.Skin, false);
            Topmost = settings.Topmost;
            StartLifeTimer();
            StartReminderTimer();
            StartWalkingEngine();
            StartEyeTracking();
        }

        private void OnClosed(object sender, EventArgs e)
        {
            settings.Left = Left;
            settings.Top = Top;
            settings.Save();
            for (int i = 0; i < minis.Count; i++) { try { minis[i].Close(); } catch { } }
            if (bubble != null) { try { bubble.Close(); } catch { } }
            if (mediaPlayer != null) { try { mediaPlayer.Close(); } catch { } }
            if (soundPath != null) { try { File.Delete(soundPath); } catch { } }
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
            if (msg == WM_NCHITTEST && IsLoaded && settings.ClickThrough)
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