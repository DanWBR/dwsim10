using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums.GraphicObjects;
using DWSIM.Thermodynamics.Streams;
using sc = DWSIM.Thermodynamics.ShortcutUtilities;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// True Critical Point utility. Avalonia counterpart of
/// DWSIM.UI.Desktop.Editors.Utilities.TrueCriticalPointView.
/// </summary>
public partial class TrueCriticalPointWindow : Window
{
    private readonly IFlowsheet _flowsheet;
    private List<string> _streams = new();

    // Parameterless ctor required by Avalonia's XAML compiler (designer-only).
    public TrueCriticalPointWindow() : this(null!, null) { }

    public TrueCriticalPointWindow(IFlowsheet flowsheet, string? preselectedStream = null)
    {
        _flowsheet = flowsheet!;
        InitializeComponent();
        IconHelper.ApplyWindowIcon(this);
        if (flowsheet == null) return;

        _streams = UtilityHelpers.MaterialStreamTags(_flowsheet);
        foreach (var s in _streams) CbStream.Items.Add(s);
        CbStream.SelectedIndex = UtilityHelpers.IndexOfStream(_streams, preselectedStream);

        BtnCalculate.Click += async (_, _) => await CalculateAsync();
        BtnCopy.Click += async (_, _) =>
        {
            var top = GetTopLevel(this);
            if (top?.Clipboard != null && !string.IsNullOrEmpty(TbResults.Text))
            {
                await top.Clipboard.SetTextAsync(TbResults.Text!);
                StatusLabel.Text = "Results copied to the clipboard.";
            }
        };
    }

    private async Task CalculateAsync()
    {
        if (CbStream.SelectedIndex < 0)
        {
            StatusLabel.Text = "Select a material stream first.";
            return;
        }

        var tag = _streams[CbStream.SelectedIndex];
        if (_flowsheet.GetFlowsheetSimulationObject(tag) is not MaterialStream ms)
        {
            StatusLabel.Text = $"'{tag}' is not a material stream.";
            return;
        }

        BtnCalculate.IsEnabled = false;
        StatusLabel.Text = "Calculating...";
        TbResults.Text = "Please wait...";

        try
        {
            var calc = new sc.Calculation(ms) { CalcType = sc.CalculationType.CriticalPoint };
            var results = await Task.Run(() => calc.Calculate());

            TbResults.Text = results.ExceptionResult == null
                ? results.TextOutput
                : results.ExceptionResult.Message;
            StatusLabel.Text = results.ExceptionResult == null ? "Done." : "Calculation failed.";
        }
        catch (Exception ex)
        {
            TbResults.Text = ex.Message;
            StatusLabel.Text = "Calculation failed.";
        }
        finally
        {
            BtnCalculate.IsEnabled = true;
        }
    }
}

/// <summary>Shared lookups for the three envelope/critical-point utility windows.</summary>
internal static class UtilityHelpers
{
    public static List<string> MaterialStreamTags(IFlowsheet fs)
    {
        return fs.SimulationObjects.Values
            .Where(x => x.GraphicObject != null && x.GraphicObject.ObjectType == ObjectType.MaterialStream)
            .Select(x => x.GraphicObject.Tag)
            .OrderBy(x => x)
            .ToList();
    }

    /// <summary>Index of <paramref name="tag"/>, or 0 when it is absent and the list is non-empty.</summary>
    public static int IndexOfStream(List<string> streams, string? tag)
    {
        if (streams.Count == 0) return -1;
        if (string.IsNullOrEmpty(tag)) return 0;
        var idx = streams.IndexOf(tag!);
        return idx >= 0 ? idx : 0;
    }

    /// <summary>Parses a user-entered number, accepting both the current and invariant culture.</summary>
    public static bool TryVal(string? text, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        return double.TryParse(text, System.Globalization.NumberStyles.Any,
                   System.Globalization.CultureInfo.CurrentCulture, out value)
            || double.TryParse(text, System.Globalization.NumberStyles.Any,
                   System.Globalization.CultureInfo.InvariantCulture, out value);
    }

    /// <summary>Reads a curve out of the engine result dictionary, or null when it is absent/empty.</summary>
    public static List<double>? Curve(sc.CalculationResults results, string key)
    {
        if (results.Data == null) return null;
        if (!results.Data.TryGetValue(key, out var list)) return null;
        return list == null || list.Count == 0 ? null : list;
    }
}
