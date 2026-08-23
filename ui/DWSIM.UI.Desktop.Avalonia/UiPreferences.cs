using System;
using System.IO;
using System.Text.Json;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// UI-only preferences that have no home in the engine's settings file.
///
/// <see cref="HoverTableScale"/> sizes the floating property table the drawing engine pops up when
/// the pointer rests on a flowsheet object. That table is drawn at a fixed base font times
/// <c>Settings.DpiScale</c>, and it deliberately cancels the flowsheet zoom out, so it is the one
/// piece of the canvas that never grows when you zoom in - at the engine's base size it reads far
/// smaller than the rest of the interface. <c>Settings.DpiScale</c> is consumed by nothing else in
/// the drawing engine, so the shell sets it to the display's render scaling times this factor.
/// </summary>
internal static class UiPreferences
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DWSIM", "Avalonia", "ui.json");

    private sealed class Model
    {
        public double HoverTableScale { get; set; } = DefaultHoverTableScale;
        public bool UsePaletteIconsOnCanvas { get; set; } = true;
    }

    /// <summary>The engine's own base size, which is what the classic UI shows.</summary>
    public const double DefaultHoverTableScale = 1.0;

    public static double HoverTableScale { get; set; } = DefaultHoverTableScale;

    /// <summary>
    /// Draws flowsheet blocks with the Objects palette artwork instead of the schematic outline.
    /// See <see cref="FlowsheetObjectIcons"/>.
    /// </summary>
    public static bool UsePaletteIconsOnCanvas { get; set; } = true;

    public static void Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return;
            var model = JsonSerializer.Deserialize<Model>(File.ReadAllText(FilePath));
            if (model == null) return;
            HoverTableScale = Math.Clamp(model.HoverTableScale, 0.5, 4.0);
            UsePaletteIconsOnCanvas = model.UsePaletteIconsOnCanvas;
        }
        catch { }
    }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(new Model
            {
                HoverTableScale = HoverTableScale,
                UsePaletteIconsOnCanvas = UsePaletteIconsOnCanvas,
            }));
        }
        catch { }
    }

    /// <summary>
    /// Pushes the current factor into the engine setting the floating table reads. Called at
    /// startup and whenever the preference or the window's render scaling changes.
    /// </summary>
    public static void ApplyHoverTableScale(double renderScaling)
    {
        if (renderScaling <= 0) renderScaling = 1.0;
        GlobalSettings.Settings.DpiScale = renderScaling * Math.Clamp(HoverTableScale, 0.5, 4.0);
    }
}
