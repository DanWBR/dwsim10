using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Avalonia.Controls;

namespace DWSIM.UI.Desktop.Avalonia;

internal static class RecentFilesManager
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DWSIM", "Avalonia", "recent.json");

    private const int Max = 15;

    public static List<string> Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new();
            var list = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(FilePath)) ?? new();
            list.RemoveAll(p => !File.Exists(p));
            return list;
        }
        catch { return new(); }
    }

    public static void Add(string path)
    {
        var list = Load();
        list.Remove(path);
        list.Insert(0, path);
        if (list.Count > Max) list.RemoveRange(Max, list.Count - Max);
        Save(list);
    }

    /// <summary>Forgets every file, the way the list can be emptied on the Windows launcher.</summary>
    public static void Clear()
    {
        Save(new List<string>());
    }

    private static void Save(List<string> list)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(list));
        }
        catch { }
    }
}

/// <summary>
/// The recent files as menu entries: the file name, the full path on the tooltip, and a way to
/// forget them all. Both the launcher and the flowsheet window hang this off their File menu.
/// </summary>
internal static class RecentFilesMenu
{

    public static void Fill(MenuItem parent, Action<string> open)
    {
        parent.Items.Clear();

        var files = RecentFilesManager.Load();

        if (files.Count == 0)
        {
            parent.Items.Add(new MenuItem { Header = "(no recent files)", IsEnabled = false });
            return;
        }

        foreach (var path in files)
        {
            // an underscore in a file name would otherwise be read as the access key marker
            var item = new MenuItem { Header = Path.GetFileName(path).Replace("_", "__") };

            ToolTip.SetTip(item, path);

            var chosen = path;
            item.Click += (_, _) => open(chosen);

            parent.Items.Add(item);
        }

        parent.Items.Add(new Separator());

        var clear = new MenuItem { Header = "Clear List" };
        clear.Click += (_, _) =>
        {
            RecentFilesManager.Clear();
            Fill(parent, open);
        };

        parent.Items.Add(clear);
    }

}
