using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace NaiWa
{
    public class TodoWindow : Window
    {
        private readonly PetWindow pet;
        private readonly StackPanel listPanel;
        private readonly TextBox addBox;
        private readonly ComboBox minutesBox;
        private readonly TextBlock status;
        private readonly DispatcherTimer refresh;
        private List<TodoItem> items;

        public TodoWindow(PetWindow pet)
        {
            this.pet = pet;

            Title = "NaiWa 待办";
            Width = 560;
            Height = 560;
            WindowStyle = WindowStyle.ToolWindow;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Topmost = true;
            ShowInTaskbar = false;
            Background = new SolidColorBrush(Color.FromRgb(255, 249, 236));

            var root = new StackPanel();
            root.Margin = new Thickness(22, 18, 22, 16);
            Content = root;

            var title = new TextBlock();
            title.Text = "待办与专注";
            title.FontSize = 20;
            title.FontWeight = FontWeights.Bold;
            title.Margin = new Thickness(0, 0, 0, 12);
            root.Children.Add(title);

            var addRow = new DockPanel();
            addRow.Margin = new Thickness(0, 0, 0, 10);
            addBox = new TextBox();
            addBox.FontSize = 14;
            addBox.Padding = new Thickness(6, 4, 6, 4);
            addBox.Height = 32;
            addBox.Width = 380;
            DockPanel.SetDock(addBox, Dock.Left);
            addRow.Children.Add(addBox);
            var addButton = new Button();
            addButton.Content = "添加";
            addButton.Width = 70;
            addButton.Height = 32;
            addButton.Margin = new Thickness(8, 0, 0, 0);
            addButton.Click += delegate { AddTodo(); };
            DockPanel.SetDock(addButton, Dock.Right);
            addRow.Children.Add(addButton);
            root.Children.Add(addRow);

            var scroll = new ScrollViewer();
            scroll.Height = 300;
            scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            scroll.Margin = new Thickness(0, 0, 0, 10);
            scroll.BorderBrush = new SolidColorBrush(Color.FromRgb(226, 210, 184));
            scroll.BorderThickness = new Thickness(1);
            scroll.Background = new SolidColorBrush(Color.FromRgb(255, 252, 244));
            listPanel = new StackPanel();
            scroll.Content = listPanel;
            root.Children.Add(scroll);

            var focusRow = new StackPanel();
            focusRow.Orientation = Orientation.Horizontal;
            focusRow.Margin = new Thickness(0, 0, 0, 8);
            var focusLabel = new TextBlock();
            focusLabel.Text = "限时完成：";
            focusLabel.VerticalAlignment = VerticalAlignment.Center;
            focusRow.Children.Add(focusLabel);
            minutesBox = new ComboBox();
            minutesBox.Width = 80;
            minutesBox.Items.Add("5 分钟");
            minutesBox.Items.Add("10 分钟");
            minutesBox.Items.Add("15 分钟");
            minutesBox.Items.Add("25 分钟");
            minutesBox.Items.Add("30 分钟");
            minutesBox.Items.Add("45 分钟");
            minutesBox.Items.Add("60 分钟");
            minutesBox.SelectedIndex = 3;
            focusRow.Children.Add(minutesBox);
            var focusHint = new TextBlock();
            focusHint.Text = "  点任务右侧「限时」开始；期间不会自动大笑";
            focusHint.VerticalAlignment = VerticalAlignment.Center;
            focusHint.Foreground = new SolidColorBrush(Color.FromRgb(120, 100, 76));
            focusRow.Children.Add(focusHint);
            root.Children.Add(focusRow);

            status = new TextBlock();
            status.FontSize = 13;
            status.Foreground = new SolidColorBrush(Color.FromRgb(90, 74, 52));
            status.Margin = new Thickness(0, 6, 0, 8);
            root.Children.Add(status);

            var close = new Button();
            close.Content = "关闭";
            close.Width = 90;
            close.HorizontalAlignment = HorizontalAlignment.Left;
            close.Click += delegate { Close(); };
            root.Children.Add(close);

            addBox.KeyDown += delegate(object sender, System.Windows.Input.KeyEventArgs e)
            {
                if (e.Key == System.Windows.Input.Key.Enter) AddTodo();
            };

            refresh = new DispatcherTimer();
            refresh.Interval = TimeSpan.FromSeconds(1);
            refresh.Tick += delegate { UpdateStatus(); };
            refresh.Start();

            Closed += delegate { refresh.Stop(); };
            Loaded += delegate { RefreshList(); };
        }

        private void AddTodo()
        {
            string text = addBox.Text.Trim();
            if (text.Length == 0) return;
            pet.AddTodo(text);
            addBox.Text = "";
            RefreshList();
        }

        private void RefreshList()
        {
            items = pet.Todos;
            listPanel.Children.Clear();
            if (items.Count == 0)
            {
                var empty = new TextBlock();
                empty.Text = "还没有待办，先加一条吧。";
                empty.Margin = new Thickness(10);
                empty.Foreground = new SolidColorBrush(Color.FromRgb(150, 130, 105));
                listPanel.Children.Add(empty);
                UpdateStatus();
                return;
            }
            for (int i = 0; i < items.Count; i++)
            {
                listPanel.Children.Add(BuildRow(items[i]));
            }
            UpdateStatus();
        }

        private UIElement BuildRow(TodoItem item)
        {
            var grid = new Grid();
            grid.Margin = new Thickness(6, 4, 6, 4);
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(62) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });

            var check = new CheckBox();
            check.IsChecked = item.Done;
            check.VerticalAlignment = VerticalAlignment.Center;
            check.Click += delegate
            {
                pet.SetTodoDone(item, check.IsChecked == true);
                RefreshList();
            };
            Grid.SetColumn(check, 0);
            grid.Children.Add(check);

            var text = new TextBlock();
            text.Text = item.Text;
            text.FontSize = 14;
            text.VerticalAlignment = VerticalAlignment.Center;
            text.TextTrimming = TextTrimming.CharacterEllipsis;
            if (item.Done)
            {
                text.TextDecorations = TextDecorations.Strikethrough;
                text.Foreground = new SolidColorBrush(Color.FromRgb(150, 140, 125));
            }
            Grid.SetColumn(text, 1);
            grid.Children.Add(text);

            var focus = new Button();
            focus.Content = "限时";
            focus.Height = 26;
            focus.Margin = new Thickness(2);
            focus.IsEnabled = !item.Done;
            focus.Click += delegate
            {
                int minutes = MinutesFromIndex(minutesBox.SelectedIndex);
                pet.StartFocus(item, minutes);
                UpdateStatus();
            };
            Grid.SetColumn(focus, 2);
            grid.Children.Add(focus);

            var del = new Button();
            del.Content = "删除";
            del.Height = 26;
            del.Margin = new Thickness(2);
            del.Click += delegate
            {
                pet.DeleteTodo(item);
                RefreshList();
            };
            Grid.SetColumn(del, 3);
            grid.Children.Add(del);

            return grid;
        }

        private static int MinutesFromIndex(int index)
        {
            int[] values = { 5, 10, 15, 25, 30, 45, 60 };
            if (index < 0 || index >= values.Length) return 25;
            return values[index];
        }

        private void UpdateStatus()
        {
            if (status == null) return;
            if (pet.FocusActive)
            {
                TimeSpan left = pet.FocusUntil - DateTime.Now;
                if (left.TotalSeconds < 0) left = TimeSpan.Zero;
                status.Text = "专注中：还剩 " + (int)left.TotalMinutes + " 分 " + left.Seconds + " 秒（期间不会自动大笑）";
            }
            else
            {
                TimeSpan left = pet.NextReminderAt - DateTime.Now;
                if (left.TotalSeconds < 0) left = TimeSpan.Zero;
                status.Text = "下次提醒：约 " + (int)Math.Ceiling(left.TotalMinutes) + " 分钟后（间隔 " + pet.ReminderMinutes + " 分钟）";
            }
        }
    }
}