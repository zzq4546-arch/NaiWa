using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NaiWa
{
    public class WelcomeWindow : Window
    {
        private readonly PetWindow pet;
        private readonly Settings settings;
        private readonly TextBox nameBox;

        public WelcomeWindow(PetWindow pet, Settings settings)
        {
            this.pet = pet;
            this.settings = settings;

            Title = "欢迎使用 NaiWa";
            Width = 520;
            Height = 480;
            WindowStyle = WindowStyle.ToolWindow;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Topmost = true;
            ShowInTaskbar = true;
            Background = new SolidColorBrush(Color.FromRgb(255, 249, 236));

            var panel = new StackPanel();
            panel.Margin = new Thickness(26, 20, 26, 20);
            Content = panel;

            var title = new TextBlock();
            title.Text = "你好，我是 NaiWa";
            title.FontSize = 22;
            title.FontWeight = FontWeights.Bold;
            title.Margin = new Thickness(0, 0, 0, 12);
            panel.Children.Add(title);

            panel.Children.Add(Line("先花一分钟认识我："));
            panel.Children.Add(Line("· 单击我：我会大笑；按住拖动可以换位置。"));
            panel.Children.Add(Line("· 右键我：大小、设置、待办、退出都在菜单里。"));
            panel.Children.Add(Line("· 托盘图标在任务栏右下角的 ^ 里。"));
            panel.Children.Add(Line("· 在「设置」里可以调音量、大小和提醒间隔。"));
            panel.Children.Add(Line("· 在「待办」里加任务，我会定时提醒你；专注期间不打扰。"));

            var tip = new TextBlock();
            tip.Text = "最后，告诉我怎么称呼你（会出现在提醒气泡里）：";
            tip.Margin = new Thickness(0, 18, 0, 6);
            panel.Children.Add(tip);

            nameBox = new TextBox();
            nameBox.FontSize = 15;
            nameBox.Padding = new Thickness(6, 4, 6, 4);
            nameBox.Margin = new Thickness(0, 0, 0, 14);
            nameBox.Text = settings.UserName;
            panel.Children.Add(nameBox);

            var start = new Button();
            start.Content = "开始使用";
            start.FontSize = 15;
            start.Width = 140;
            start.Height = 36;
            start.HorizontalAlignment = HorizontalAlignment.Left;
            start.Click += delegate { Start(); };
            panel.Children.Add(start);

            nameBox.KeyDown += delegate(object sender, System.Windows.Input.KeyEventArgs e)
            {
                if (e.Key == System.Windows.Input.Key.Enter) Start();
            };
            Loaded += delegate { nameBox.Focus(); };
        }

        private static TextBlock Line(string text)
        {
            var t = new TextBlock();
            t.Text = text;
            t.FontSize = 14;
            t.Foreground = new SolidColorBrush(Color.FromRgb(90, 74, 52));
            t.Margin = new Thickness(0, 3, 0, 3);
            t.TextWrapping = TextWrapping.Wrap;
            return t;
        }

        private void Start()
        {
            string name = nameBox.Text.Trim();
            if (name.Length == 0) name = "小可爱";
            settings.UserName = name;
            settings.OnboardingDone = true;
            settings.Save();
            pet.OnOnboardingName(name);
            Close();
        }
    }
}