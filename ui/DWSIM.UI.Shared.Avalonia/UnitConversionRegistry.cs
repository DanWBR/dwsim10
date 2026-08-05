using System;
using System.Collections.Generic;

namespace DWSIM.UI.Shared.Avalonia;

/// <summary>
/// Allows the .NET-Framework host (which has access to DWSIM.SharedClasses) to inject
/// the unit-conversion delegates that AvaloniaEditorExtensions.WireUnitContextMenu needs.
/// Keeping this as a static registry lets the netstandard2.0 shared library stay free of
/// a DWSIM.SharedClasses reference (which targets net472 and pulls in heavy dependencies).
/// </summary>
public static class UnitConversionRegistry
{
    /// <summary>
    /// Given a unit symbol (e.g. "K", "Pa", "kg/h"), returns every alternative unit in the
    /// same measure family. Return empty/null when the unit is unrecognized.
    /// </summary>
    public static Func<string, IReadOnlyList<string>>? GetAlternatives { get; set; }

    /// <summary>
    /// Converts <paramref name="value"/> from <paramref name="fromUnit"/> to <paramref name="toUnit"/>.
    /// Hosts wire this to DWSIM.SharedClasses.SystemsOfUnits.Converter.Convert.
    /// </summary>
    public static Func<string, string, double, double>? Convert { get; set; }

    internal static bool IsConfigured => GetAlternatives != null && Convert != null;
}
