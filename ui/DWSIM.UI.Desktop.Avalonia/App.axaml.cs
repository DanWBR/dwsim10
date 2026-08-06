using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace DWSIM.UI.Desktop.Avalonia;

public class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void RegisterServices()
    {
        base.RegisterServices();

        // the canvas lives in the netstandard bridge and cannot reference the engine itself
        DWSIM.UI.Shared.Avalonia.FlowsheetCanvas.KeyboardStateSink =
            (shift, ctrl, alt) => DWSIM.GlobalSettings.KeyboardState.SetState(shift, ctrl, alt);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Honor the persisted DarkMode flag. Engine wrote it last session; we read it here
        // before any window is shown so the choice is reflected from the splash onward.
        if (DWSIM.GlobalSettings.Settings.DarkMode)
            RequestedThemeVariant = ThemeVariant.Dark;

        // Apply persisted locale before any string is Localize()'d.
        var savedCulture = DWSIM.GlobalSettings.Settings.CurrentCulture;
        if (!string.IsNullOrEmpty(savedCulture))
            DWSIM.UI.Shared.Avalonia.Localization.SetCulture(savedCulture);

        // no window may come up bigger than the screen, or its title bar ends up out of reach
        WindowFit.Install();

        // copying flowsheet objects also puts the XML on the system clipboard, so it can be pasted
        // elsewhere. Reading back is what the engine keeps in process: the Avalonia clipboard is
        // asynchronous and the engine asks for the text from the UI thread, where waiting deadlocks.
        DWSIM.FlowsheetBase.FlowsheetBase.ClipboardTextWriter = text =>
        {
            var clipboard = (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?
                .MainWindow?.Clipboard;

            _ = clipboard?.SetTextAsync(text);
        };

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var filePath = desktop.Args?
                .FirstOrDefault(a => File.Exists(a) &&
                    (a.EndsWith(".dwxmz", System.StringComparison.OrdinalIgnoreCase) ||
                     a.EndsWith(".dwxml", System.StringComparison.OrdinalIgnoreCase) ||
                     a.EndsWith(".xml",   System.StringComparison.OrdinalIgnoreCase)));

            var main = new MainWindow();
            desktop.MainWindow = main;

            // a file passed on the command line opens as the first document of the shell
            if (filePath != null)
                main.Opened += (_, _) => main.OpenFlowsheetFile(filePath);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
