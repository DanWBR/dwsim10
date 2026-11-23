using Avalonia;
using Avalonia.Controls;

namespace DWSIM.UI.Desktop.Avalonia;

public sealed class LocalizationMenu : AvaloniaObject
{
    public static readonly AttachedProperty<string?> SourceHeaderProperty =
        AvaloniaProperty.RegisterAttached<LocalizationMenu, MenuItem, string?>("SourceHeader");

    public static string GetSourceHeader(MenuItem item) => item.GetValue(SourceHeaderProperty)
        ?? item.Header?.ToString() ?? "";

    public static void SetSourceHeader(MenuItem item, string value) => item.SetValue(SourceHeaderProperty, value);
}
