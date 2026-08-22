using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
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
        // Persisted settings are written back on close, but were never read back at startup:
        // UIScalingFactor, DarkMode, CurrentCulture and the rest always came up at their defaults
        // (scaling reverted to 1.0 every restart). Load them before anything reads them.
        try { DWSIM.GlobalSettings.Settings.LoadSettings("dwsim_newui.ini"); } catch { }

        // Honor the persisted DarkMode flag. Engine wrote it last session; we read it here
        // before any window is shown so the choice is reflected from the splash onward.
        if (DWSIM.GlobalSettings.Settings.DarkMode)
            RequestedThemeVariant = ThemeVariant.Dark;

        // Apply persisted locale before any string is Localize()'d.
        var savedCulture = DWSIM.GlobalSettings.Settings.CurrentCulture;
        if (!string.IsNullOrEmpty(savedCulture))
            DWSIM.UI.Shared.Avalonia.Localization.SetCulture(savedCulture);

        // Apply the persisted UI scaling factor before any window is built, so the whole interface -
        // fonts, control heights and menu icons - comes up at the chosen size (issue #17).
        ApplyUIScaling();

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
            var filePath = desktop.Args?.FirstOrDefault(IsFlowsheetFile);

            var main = new MainWindow();
            desktop.MainWindow = main;

            // a file passed on the command line opens as the first document of the shell
            if (filePath != null)
                main.Opened += (_, _) => main.OpenFlowsheetFile(filePath);

            InstallFileActivationHandler(main);
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Scales the whole interface by the persisted UI scaling factor. Every control reads
    /// FontSizeNormal (see App.axaml), so multiplying the font-size resources and the text-control
    /// minimum height scales fonts, buttons, tabs and input fields together; menu icons follow via
    /// IconHelper. Base values match those defined in App.axaml. Applied once at startup (issue #17).
    /// </summary>
    private void ApplyUIScaling()
    {
        var scale = DWSIM.GlobalSettings.Settings.UIScalingFactor;
        if (scale <= 0) scale = 1.0;
        scale = System.Math.Max(0.5, System.Math.Min(3.0, scale));

        Resources["ControlContentThemeFontSize"] = 12.0 * scale;
        Resources["FontSizeNormal"] = 12.0 * scale;
        Resources["FontSizeSmall"] = 11.0 * scale;
        Resources["TextControlThemeMinHeight"] = 24.0 * scale;

        // Control geometry that App.axaml styles keep fixed would otherwise stay at its
        // unscaled size while the fonts grow, so buttons, tabs, grids and toolbars read these
        // resources and grow with the same factor (issue: scaling looked cramped and icons
        // stayed small).
        Resources["ControlMinHeight"] = 28.0 * scale;
        Resources["ControlPadding"] = new Thickness(12.0 * scale, 5.0 * scale);
        Resources["TabHeaderMinHeight"] = 34.0 * scale;
        Resources["TabStripHeight"] = 28.0 * scale;
        Resources["DataGridRowHeight"] = 28.0 * scale;
        Resources["DataGridHeaderHeight"] = 24.0 * scale;
        Resources["ToolbarIconSize"] = 16.0 * scale;
        Resources["ToolbarButtonMinSize"] = 32.0 * scale;
        Resources["SubToolbarButtonMinSize"] = 24.0 * scale;
        Resources["VdividerHeight"] = 18.0 * scale;

        IconHelper.IconFontSize = 14.0 * scale;
    }

    private static bool IsFlowsheetFile(string path)
    {
        return !string.IsNullOrEmpty(path) && File.Exists(path) &&
               (path.EndsWith(".dwxmz", System.StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".dwxml", System.StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".xml", System.StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Opens the files the operating system hands to a running application. macOS never puts a
    /// double-clicked document on the command line: it sends it to the application delegate, which
    /// reaches us as a file activation. Without this, opening a flowsheet from Finder or from a
    /// link in a report starts DWSIM with nothing loaded.
    /// </summary>
    private void InstallFileActivationHandler(MainWindow main)
    {
        if (TryGetFeature(typeof(IActivatableLifetime)) is not IActivatableLifetime activatable) return;

        var ready = main.IsLoaded;
        var pending = new List<string>();

        main.Opened += (_, _) =>
        {
            ready = true;
            foreach (var path in pending) main.OpenFlowsheetFile(path);
            pending.Clear();
        };

        activatable.Activated += (_, e) =>
        {
            if (e is not FileActivatedEventArgs fileArgs) return;

            foreach (var item in fileArgs.Files)
            {
                var path = item.TryGetLocalPath();
                if (!IsFlowsheetFile(path)) continue;

                // the activation can arrive before the shell window exists, so hold on to it
                if (ready) main.OpenFlowsheetFile(path); else pending.Add(path);
            }
        };
    }
}
