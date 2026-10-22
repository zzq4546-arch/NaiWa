using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace NaiWa
{
    public class TodoItem
    {
        public string Text = "";
        public bool Done = false;

        public TodoItem() { }

        public TodoItem(string text)
        {
            Text = text;
        }
    }

    public static class TodoStore
    {
        public static string[] CandidateFiles
        {
            get
            {
                return new string[]
                {
                    Path.Combine(Settings.ExeDir, "NaiWa-todos.txt"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NaiWa", "todos.txt"),
                    Path.Combine(Path.GetTempPath(), "NaiWa-todos.txt")
                };
            }
        }

        public static List<TodoItem> Load()
        {
            var list = new List<TodoItem>();
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
                        if (string.IsNullOrEmpty(line)) continue;
                        int tab = line.IndexOf('\t');
                        if (tab < 0) continue;
                        string flag = line.Substring(0, tab);
                        string text = line.Substring(tab + 1).Replace("\\t", "\t").Replace("\\n", " ");
                        if (text.Trim().Length == 0) continue;
                        var item = new TodoItem(text.Trim());
                        item.Done = flag == "1";
                        list.Add(item);
                    }
                    return list;
                }
                catch { }
            }
            return list;
        }

        public static void Save(List<TodoItem> items)
        {
            string[] files = CandidateFiles;
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < items.Count; i++)
            {
                string text = items[i].Text.Replace("\t", " ").Replace("\r", " ").Replace("\n", " ");
                sb.Append(items[i].Done ? "1" : "0").Append('\t').Append(text).Append("\r\n");
            }
            for (int i = 0; i < files.Length; i++)
            {
                try
                {
                    string dir = Path.GetDirectoryName(files[i]);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    File.WriteAllText(files[i], sb.ToString());
                    return;
                }
                catch { }
            }
        }
    }
}