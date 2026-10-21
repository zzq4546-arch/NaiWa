using System;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace NaiWa
{
    public class Settings
    {
        public double Left = double.NaN;
        public double Top = double.NaN;
        public int SizeMode = 1;        // 0 small, 1 normal, 2 large
        public bool Muted = false;
        public bool AutoStart = false;
        public bool TipShown = false;

        public static string ExeDir
        {
            get { return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location); }
        }

        public static string[] CandidateFiles
        {
            get
            {
                return new string[]
                {
                    Path.Combine(ExeDir, "NaiWa-settings.ini"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NaiWa", "settings.ini"),
                    Path.Combine(Path.GetTempPath(), "NaiWa-settings.ini")
                };
            }
        }

        public static Settings Load()
        {
            var s = new Settings();
            string[] files = CandidateFiles;
            for (int i = 0; i < files.Length; i++)
            {
                try
                {
                    if (!File.Exists(files[i])) continue;
                    string[] lines = File.ReadAllLines(files[i]);
                    for (int j = 0; j < lines.Length; j++)
                    {
                        string line = lines[j];
                        int eq = line.IndexOf('=');
                        if (eq <= 0) continue;
                        string key = line.Substring(0, eq).Trim();
                        string value = line.Substring(eq + 1).Trim();
                        s.Read(key, value);
                    }
                    return s;
                }
                catch { }
            }
            return s;
        }

        private void Read(string key, string value)
        {
            double d;
            int n;
            bool b;
            switch (key)
            {
                case "left":
                    if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) Left = d;
                    break;
                case "top":
                    if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) Top = d;
                    break;
                case "size":
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out n) && n >= 0 && n <= 2) SizeMode = n;
                    break;
                case "muted":
                    if (bool.TryParse(value, out b)) Muted = b;
                    break;
                case "autostart":
                    if (bool.TryParse(value, out b)) AutoStart = b;
                    break;
                case "tip":
                    if (bool.TryParse(value, out b)) TipShown = b;
                    break;
            }
        }

        public void Save()
        {
            string[] files = CandidateFiles;
            string text =
                "left=" + Left.ToString("0.##", CultureInfo.InvariantCulture) + "\r\n" +
                "top=" + Top.ToString("0.##", CultureInfo.InvariantCulture) + "\r\n" +
                "size=" + SizeMode.ToString(CultureInfo.InvariantCulture) + "\r\n" +
                "muted=" + Muted.ToString() + "\r\n" +
                "autostart=" + AutoStart.ToString() + "\r\n" +
                "tip=" + TipShown.ToString() + "\r\n";
            Exception last = null;
            for (int i = 0; i < files.Length; i++)
            {
                try
                {
                    string dir = Path.GetDirectoryName(files[i]);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    File.WriteAllText(files[i], text);
                    return;
                }
                catch (Exception ex)
                {
                    last = ex;
                }
            }
            if (last != null) Log.Write("settings save failed: " + last.Message);
        }
    }
}