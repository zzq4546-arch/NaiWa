using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace NaiWa
{
    public class TrayIcon : IDisposable
    {
        private readonly NotifyIcon notify;
        private readonly PetWindow window;

        public TrayIcon(PetWindow window)
        {
            this.window = window;

            notify = new NotifyIcon();
            notify.Icon = LoadIcon();
            notify.Text = "NaiWa";
            notify.Visible = true;

            var menu = new ContextMenuStrip();
            var show = new ToolStripMenuItem("显示 NaiWa");
            show.Click += delegate { ShowPet(); };
            var hide = new ToolStripMenuItem("隐藏");
            hide.Click += delegate { window.Hide(); };
            var exit = new ToolStripMenuItem("退出");
            exit.Click += delegate { window.Close(); };
            menu.Items.Add(show);
            menu.Items.Add(hide);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(exit);
            notify.ContextMenuStrip = menu;
            notify.DoubleClick += delegate { ShowPet(); };
        }

        public void ShowStartupTip()
        {
            try
            {
                notify.ShowBalloonTip(4000, "NaiWa 已启动",
                    "右键桌宠可以打开菜单；托盘图标在任务栏的 ^ 里。",
                    ToolTipIcon.Info);
            }
            catch { }
        }

        private void ShowPet()
        {
            window.Show();
            window.Activate();
        }

        private static Icon LoadIcon()
        {
            try
            {
                Icon icon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);
                if (icon != null) return icon;
            }
            catch { }
            return SystemIcons.Application;
        }

        public void Dispose()
        {
            notify.Visible = false;
            notify.Dispose();
        }
    }
}