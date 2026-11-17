using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace DWSIM.UI.Shared.Avalonia;

/// <summary>
/// File open/save pickers for editors that only hold a control (a VB.NET unit operation's
/// editor panel, for instance) and not the window that owns it. The window is found from the
/// control. Each call returns the local path picked, or null when the user cancels.
/// </summary>
public static class AvaloniaFileDialogs
{
    public static async Task<string?> OpenFileAsync(Control anchor, string title,
        string filterName, string pattern, string? startFolder = null)
    {
        var top = TopLevel.GetTopLevel(anchor);
        if (top == null) return null;
        var options = new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType(filterName) { Patterns = new[] { pattern } },
                FilePickerFileTypes.All
            }
        };
        if (!string.IsNullOrEmpty(startFolder) && System.IO.Directory.Exists(startFolder))
            options.SuggestedStartLocation = await top.StorageProvider.TryGetFolderFromPathAsync(startFolder!);
        var files = await top.StorageProvider.OpenFilePickerAsync(options);
        return files?.FirstOrDefault()?.Path?.LocalPath;
    }

    public static async Task<string?> SaveFileAsync(Control anchor, string title,
        string filterName, string extension, string suggestedName)
    {
        var top = TopLevel.GetTopLevel(anchor);
        if (top == null) return null;
        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedName,
            DefaultExtension = extension,
            FileTypeChoices = new[] { new FilePickerFileType(filterName) { Patterns = new[] { "*." + extension } } }
        });
        return file?.Path?.LocalPath;
    }

    /// <summary>Attaches a hover tip to a control.</summary>
    public static void SetTip(Control control, string? tip)
    {
        if (control != null && !string.IsNullOrEmpty(tip)) ToolTip.SetTip(control, tip);
    }
}
