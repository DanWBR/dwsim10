using System;
using Avalonia.Controls;
using Avalonia.Input;
using DWSIM.Automation.FluentAPI.Diagnostics;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>F1 help for the tool windows: the tutorials page that explains the tool, in the site's language.</summary>
internal static class HelpLinks
{
    /// <summary>The tutorials site language for the current culture (en or pt-BR).</summary>
    public static string Language()
    {
        var culture = DWSIM.GlobalSettings.Settings.CultureInfo ?? "en";
        return culture.StartsWith("pt", StringComparison.OrdinalIgnoreCase) ? "pt-BR" : "en";
    }

    /// <summary>Makes F1 in the window open the page of the given tool (flowsheet-check, explain-result, mccabe-thiele, ...).</summary>
    public static void AttachF1(Window window, string tool)
    {
        window.KeyDown += (_, e) =>
        {
            if (e.Key != Key.F1) return;
            OpenUrl(ContextualHelp.UrlForTool(tool, Language()));
            e.Handled = true;
        };
    }

    public static void OpenUrl(string url)
    {
        try
        {
            if (OperatingSystem.IsWindows())
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", "\"" + url + "\"") { UseShellExecute = false });
            else if (OperatingSystem.IsMacOS())
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("open", url) { UseShellExecute = false });
            else
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("xdg-open", url) { UseShellExecute = false });
        }
        catch (Exception)
        {
            // the browser is the user's business; nothing to do when none opens
        }
    }
}
