using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NaiWa
{
    public class SettingsWindow : Window
    {
        private readonly PetWindow pet;
        private readonly Settings settings;
        private bool initializing = true;

        public SettingsWindow(PetWindow pet, Settings settings)
        {
            this.pet = pet;
            this.settings = settings;

            Title = "NaiWa 设置";
            Width = 360;
            Height = 600;
            WindowStyle = WindowStyle.ToolWindow;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Topmost = true;
            ShowInTaskbar = false;
            Background = new SolidColorBrush(Color.FromRgb(255, 249, 236));

            var panel = new StackPanel();
            panel.Margin = new Thickness(20, 16, 20, 16);
            Content = panel;

            panel.Children.Add(TitleBlock("NaiWa 设置"));

            panel.Children.Add(LabelBlock("你的称呼（会出现在提醒气泡里）"));
            var nameBox = new TextBox();
            nameBox.Text = settings.UserName;
            nameBox.FontSize = 14;
            nameBox.Padding = new Thickness(5, 3, 5, 3);
            nameBox.Margin = new Thickness(0, 2, 0, 8);
            panel.Children.Add(nameBox);

            panel.Children.Add(LabelBlock("大小"));
            var sizeBox = new ComboBox();
            sizeBox.Items.Add("小");
            sizeBox.Items.Add("标准");
            sizeBox.Items.Add("大");
            sizeBox.SelectedIndex = settings.SizeMode;
            sizeBox.Margin = new Thickness(0, 2, 0, 10);
            panel.Children.Add(sizeBox);

            panel.Children.Add(LabelBlock("音量"));
            var volumePanel = new StackPanel();
            volumePanel.Orientation = Orientation.Horizontal;
            var volume = new Slider();
            volume.Minimum = 0;
            volume.Maximum = 100;
            volume.Value = settings.Volume;
            volume.Width = 240;
            volume.VerticalAlignment = VerticalAlignment.Center;
            var volumeText = new TextBlock();
            volumeText.Text = settings.Volume.ToString();
            volumeText.Margin = new Thickness(10, 0, 0, 0);
            volumeText.VerticalAlignment = VerticalAlignment.Center;
            volumePanel.Children.Add(volume);
            volumePanel.Children.Add(volumeText);
            panel.Children.Add(volumePanel);

            panel.Children.Add(LabelBlock("待办提醒间隔"));
            var intervalBox = new ComboBox();
            intervalBox.Items.Add("5 分钟");
            intervalBox.Items.Add("10 分钟");
            intervalBox.Items.Add("15 分钟");
            intervalBox.Items.Add("20 分钟");
            intervalBox.Items.Add("30 分钟");
            intervalBox.Items.Add("45 分钟");
            intervalBox.Items.Add("60 分钟");
            int[] intervalValues = { 5, 10, 15, 20, 30, 45, 60 };
            int sel = 4;
            for (int i = 0; i < intervalValues.Length; i++) { if (intervalValues[i] == settings.TodoIntervalMinutes) sel = i; }
            intervalBox.SelectedIndex = sel;
            intervalBox.Margin = new Thickness(0, 2, 0, 8);
            panel.Children.Add(intervalBox);

            var topmost = AddCheck("窗口置顶", settings.Topmost);
            var clickThrough = AddCheck("透明区域点击穿透", settings.ClickThrough);
            var idle = AddCheck("待机动画（呼吸 / 眨眼 / 歪头）", settings.IdleAnimation);
            var walk = AddCheck("自由走动", settings.WalkEnabled);
            var autoStart = AddCheck("开机自启", settings.AutoStart);
            panel.Children.Add(topmost);
            panel.Children.Add(clickThrough);
            panel.Children.Add(idle);
            panel.Children.Add(walk);
            panel.Children.Add(autoStart);

            panel.Children.Add(LabelBlock("皮肤"));
            var skinBox = new ComboBox();
            skinBox.Items.Add("原皮");
            skinBox.Items.Add("蓝翼");
            skinBox.Items.Add("女生");
            skinBox.Items.Add("兜帽娘");
            skinBox.Items.Add("牛牛");
            skinBox.SelectedIndex = settings.Skin;
            skinBox.Margin = new Thickness(0, 2, 0, 12);
            panel.Children.Add(skinBox);

            var buttons = new StackPanel();
            buttons.Orientation = Orientation.Horizontal;
            var reset = new Button();
            reset.Content = "重置位置";
            reset.Width = 90;
            reset.Margin = new Thickness(0, 0, 8, 0);
            reset.Click += delegate { pet.ResetPosition(); };
            var todo = new Button();
            todo.Content = "待办…";
            todo.Width = 80;
            todo.Margin = new Thickness(0, 0, 8, 0);
            todo.Click += delegate { pet.OpenTodos(); };
            var close = new Button();
            close.Content = "关闭";
            close.Width = 80;
            close.Click += delegate { Close(); };
            buttons.Children.Add(reset);
            buttons.Children.Add(todo);
            buttons.Children.Add(close);
            panel.Children.Add(buttons);

            sizeBox.SelectionChanged += delegate
            {
                if (initializing) return;
                pet.SetSizeMode(sizeBox.SelectedIndex);
            };
            volume.ValueChanged += delegate
            {
                if (initializing) return;
                settings.Volume = (int)volume.Value;
                volumeText.Text = settings.Volume.ToString();
                pet.ApplyVolume(settings.Volume);
                settings.Save();
            };
            topmost.Click += delegate
            {
                if (initializing) return;
                pet.ApplyTopmost(topmost.IsChecked == true);
                settings.Save();
            };
            clickThrough.Click += delegate
            {
                if (initializing) return;
                pet.ApplyClickThrough(clickThrough.IsChecked == true);
                settings.Save();
            };
            idle.Click += delegate
            {
                if (initializing) return;
                pet.ApplyIdleAnimation(idle.IsChecked == true);
                settings.Save();
            };
            walk.Click += delegate
            {
                if (initializing) return;
                pet.ApplyWalkEnabled(walk.IsChecked == true);
            };
            autoStart.Click += delegate
            {
                if (initializing) return;
                settings.AutoStart = autoStart.IsChecked == true;
                pet.ApplyAutoStart(settings.AutoStart);
                settings.Save();
            };
            skinBox.SelectionChanged += delegate
            {
                if (initializing) return;
                pet.ApplySkin(skinBox.SelectedIndex);
            };
            nameBox.TextChanged += delegate
            {
                if (initializing) return;
                settings.UserName = nameBox.Text.Trim();
                settings.Save();
            };
            intervalBox.SelectionChanged += delegate
            {
                if (initializing) return;
                int[] vals = { 5, 10, 15, 20, 30, 45, 60 };
                pet.SetReminderInterval(vals[intervalBox.SelectedIndex]);
            };

            initializing = false;
        }

        private static TextBlock TitleBlock(string text)
        {
            var t = new TextBlock();
            t.Text = text;
            t.FontSize = 18;
            t.FontWeight = FontWeights.Bold;
            t.Margin = new Thickness(0, 0, 0, 12);
            return t;
        }

        private static TextBlock LabelBlock(string text)
        {
            var t = new TextBlock();
            t.Text = text;
            t.Foreground = new SolidColorBrush(Color.FromRgb(90, 74, 52));
            t.Margin = new Thickness(0, 6, 0, 0);
            return t;
        }

        private static CheckBox AddCheck(string text, bool value)
        {
            var c = new CheckBox();
            c.Content = text;
            c.IsChecked = value;
            c.Margin = new Thickness(0, 7, 0, 0);
            return c;
        }
    }
}