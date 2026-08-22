namespace DWSIM.UI.Shared.Avalonia;

/// <summary>
/// The active UI scaling factor, set once at startup by the application
/// (App.ApplyUIScaling). Controls built in code with hard-coded font sizes,
/// icon sizes and geometry read it so they grow with the persisted
/// Preferences → Scaling setting exactly like the XAML-styled controls do.
/// </summary>
public static class UiScale
{
    /// <summary>The persisted scaling factor (1.0 = no scaling).</summary>
    public static double Factor { get; set; } = 1.0;

    /// <summary>A font size scaled by the active factor.</summary>
    public static double Font(double size) => size * Factor;

    /// <summary>Any pixel dimension scaled by the active factor.</summary>
    public static double Size(double size) => size * Factor;
}
