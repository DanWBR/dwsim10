using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Resources;

namespace DWSIM.UI.Shared.Avalonia;

/// <summary>
/// Cross-cutting Localize() helper. Mirrors the Eto pattern (every string in the UI is
/// piped through <c>"Foo".Localize()</c>); the lookup goes against the existing engine
/// resource bundle so we share the translations the Classic UI already ships.
///
/// Resolution order:
///   1. A registered fallback dictionary (for ad-hoc Avalonia-only strings).
///   2. <c>DWSIM.UI.Forms.Localization.Strings</c> (loaded reflectively to avoid a
///      compile-time dependency on the .NET 4.7.2 UI.Forms project from netstandard).
///   3. The original string (so missing keys never blank the UI).
///
/// Call <see cref="SetCulture"/> from PreferencesWindow when the user changes the locale.
/// </summary>
public static class Localization
{
    private static readonly ConcurrentDictionary<string, string> _fallback = new();
    private static ResourceManager? _resources;
    private static CultureInfo _culture = CultureInfo.CurrentUICulture;
    private static bool _resourceProbed;

    /// <summary>Hard-code translations for strings the resource bundle doesn't carry.</summary>
    public static void Register(string key, string value) => _fallback[key] = value;

    /// <summary>Switches the active culture for subsequent Localize() calls.</summary>
    public static void SetCulture(string cultureName)
    {
        try
        {
            _culture = CultureInfo.GetCultureInfo(cultureName);
            CultureInfo.DefaultThreadCurrentUICulture = _culture;
        }
        catch { /* invalid culture string — keep current */ }
    }

    public static string Localize(this string source)
    {
        if (string.IsNullOrEmpty(source)) return source;

        if (_fallback.TryGetValue(source, out var direct)) return direct;

        EnsureResourcesProbed();
        if (_resources != null)
        {
            try
            {
                var translated = _resources.GetString(source, _culture);
                if (!string.IsNullOrEmpty(translated)) return translated!;
            }
            catch { /* missing key, fall through */ }
        }
        return source;
    }

    private static void EnsureResourcesProbed()
    {
        if (_resourceProbed) return;
        _resourceProbed = true;
        try
        {
            // The engine ships its translation bundle under DWSIM.UI.Forms.dll. We load via
            // reflection so the netstandard shared lib stays decoupled from net472.
            var asm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "DWSIM.UI.Forms");
            if (asm == null)
            {
                try { asm = Assembly.Load("DWSIM.UI.Forms"); } catch { }
            }
            if (asm == null) return;

            var stringsType = asm.GetType("DWSIM.UI.Forms.Localization.Strings");
            if (stringsType == null) return;

            var rmProp = stringsType.GetProperty("ResourceManager", BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
            _resources = rmProp?.GetValue(null) as ResourceManager;
        }
        catch { /* engine resources unavailable — fallback to source string */ }
    }
}
