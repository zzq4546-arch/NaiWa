using System;
using System.IO;
using System.Reflection;

namespace NaiWa
{
    public static class Log
    {
        private static readonly object Gate = new object();

        private static string[] CandidateDirs()
        {
            string exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            return new string[]
            {
                Path.Combine(exeDir, "log"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NaiWa"),
                Path.GetTempPath()
            };
        }

        public static void Write(string message)
        {
            lock (Gate)
            {
                string line = DateTime.Now.ToString("HH:mm:ss.fff") + " " + message + Environment.NewLine;
                string[] dirs = CandidateDirs();
                for (int i = 0; i < dirs.Length; i++)
                {
                    try
                    {
                        if (string.IsNullOrEmpty(dirs[i])) continue;
                        Directory.CreateDirectory(dirs[i]);
                        File.AppendAllText(Path.Combine(dirs[i], "NaiWa.log"), line);
                        return;
                    }
                    catch { }
                }
            }
        }
    }
}