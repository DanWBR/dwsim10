using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Avalonia.Controls;
using DWSIM.Automation.FluentAPI.Diagnostics;
using DWSIM.Interfaces;
using DWSIM.SharedClasses.SystemsOfUnits;
using S = DWSIM.GlobalSettings.Settings;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// What is wrong with the flowsheet, explained, and what each object still needs before it can
/// be solved.
/// </summary>
/// <remarks>
/// Two tabs over the engine's diagnostics. Findings lists what <see cref="FlowsheetDiagnostics"/>
/// found, with the written explanation of the selected code beside it and a link to the
/// tutorials. Degrees of Freedom lists, per object, the specifications its calculation mode
/// reads and which are missing, from <see cref="DegreesOfFreedomAnalysis"/>. Values are shown in
/// the simulation's own unit system.
/// </remarks>
public partial class FlowsheetCheckWindow : Window
{
    private readonly IFlowsheet _flowsheet;
    private readonly Action<string>? _locate;

    private IReadOnlyList<Finding> _findings = Array.Empty<Finding>();
    private FlowsheetDegreesOfFreedom? _dof;
    private string _title = "";

    private sealed class FindingRow
    {
        public FindingRow(Finding finding) { Finding = finding; }
        public Finding Finding { get; }
        public string Severity => Finding.Severity.ToString();
        public string Object => Finding.ObjectTag;
        public string Code => Finding.Code;
        public string Message => Finding.Message;
    }

    private sealed class ObjectRow
    {
        public ObjectRow(ObjectDegreesOfFreedom dof) { Dof = dof; }
        public ObjectDegreesOfFreedom Dof { get; }
        public string Object => Dof.ObjectTag;
        public string Type => Dof.ObjectType;
        public string Missing => Dof.Supported ? Dof.Remaining.ToString(CultureInfo.InvariantCulture) : "?";
    }

    private sealed class SlotRow
    {
        public string Name { get; init; } = "";
        public string Value { get; init; } = "";
        public string Status { get; init; } = "";
        public string Note { get; init; } = "";
    }

    // Parameterless ctor required by the Avalonia XAML compiler (designer only).
    public FlowsheetCheckWindow() : this(null!, null) { }

    /// <param name="flowsheet">The flowsheet to check.</param>
    /// <param name="locate">Selects an object on the canvas by its internal name and opens its editor; may be null.</param>
    public FlowsheetCheckWindow(IFlowsheet flowsheet, Action<string>? locate)
    {
        _flowsheet = flowsheet!;
        _locate = locate;
        InitializeComponent();
        HelpLinks.AttachF1(this, "flowsheet-check");
        IconHelper.ApplyWindowIcon(this);
        if (flowsheet == null) return;

        BtnCheck.Click += (_, _) => RunCheck();
        BtnDiagnose.Click += (_, _) => RunDiagnose();
        BtnCopy.Click += async (_, _) =>
        {
            var top = GetTopLevel(this);
            if (top?.Clipboard != null) await top.Clipboard.SetTextAsync(BuildReport());
        };

        GridFindings.SelectionChanged += (_, _) => ShowExplanation((GridFindings.SelectedItem as FindingRow)?.Finding);
        GridObjects.SelectionChanged += (_, _) => ShowObject((GridObjects.SelectedItem as ObjectRow)?.Dof);

        BtnLocate.Click += (_, _) => Locate((GridFindings.SelectedItem as FindingRow)?.Finding.ObjectTag);
        BtnOpenObject.Click += (_, _) => Locate((GridObjects.SelectedItem as ObjectRow)?.Dof.ObjectTag);
        BtnLearnMore.Click += (_, _) =>
        {
            var finding = (GridFindings.SelectedItem as FindingRow)?.Finding;
            if (finding != null) OpenUrl(FindingExplanations.LearnMoreUrl(finding.Code, TutorialLanguage()));
        };

        // A solved flowsheet is worth diagnosing straight away; one still being built, checking.
        var solved = _flowsheet.SimulationObjects.Values.Any(o => o.Calculated);
        if (solved) RunDiagnose(); else RunCheck();
    }

    // ── Runs ─────────────────────────────────────────────────────────────

    private void RunCheck()
    {
        _title = "Setup check";
        _findings = FlowsheetDiagnostics.Check(_flowsheet);
        _dof = DegreesOfFreedomAnalysis.Analyze(_flowsheet);
        Render();
    }

    private void RunDiagnose()
    {
        _title = "Result diagnosis";
        _findings = FlowsheetDiagnostics.Diagnose(_flowsheet, null);
        _dof = DegreesOfFreedomAnalysis.Analyze(_flowsheet);
        Render();
    }

    private void Render()
    {
        var blockers = _findings.Count(f => f.Severity == DiagnosticSeverity.Blocker);
        var warnings = _findings.Count(f => f.Severity == DiagnosticSeverity.Warning);
        var infos = _findings.Count(f => f.Severity == DiagnosticSeverity.Info);
        var open = _dof?.Remaining ?? 0;

        LblSummary.Text = _findings.Count == 0
            ? _title + ": nothing known to be wrong."
            : _title + ": " + blockers + " blocker(s), " + warnings + " warning(s), " + infos + " note(s)" +
              (open > 0 ? "; " + open + " specification(s) still missing." : ".");

        GridFindings.ItemsSource = _findings.Select(f => new FindingRow(f)).ToList();
        ShowExplanation(null);

        var objects = _dof?.Objects ?? new List<ObjectDegreesOfFreedom>();
        GridObjects.ItemsSource = objects.Select(o => new ObjectRow(o)).ToList();
        LblDofSummary.Text = _dof == null ? "" :
            (_dof.IsFullySpecified
                ? "Every catalogued object has all the values its calculation mode reads."
                : _dof.Remaining + " specification(s) missing across " + objects.Count(o => o.Remaining > 0) + " object(s).") +
            (_dof.Unsupported > 0 ? " " + _dof.Unsupported + " object(s) of types not catalogued; check their editors." : "");
        ShowObject(null);
    }

    // ── Findings tab ─────────────────────────────────────────────────────

    private void ShowExplanation(Finding? finding)
    {
        var has = finding != null;
        HdrThis.IsVisible = has; HdrMeaning.IsVisible = has; HdrWhy.IsVisible = has; HdrHow.IsVisible = has;
        BtnLearnMore.IsVisible = has;
        BtnLocate.IsVisible = has && !string.IsNullOrEmpty(finding!.ObjectTag) && _locate != null;

        if (finding == null)
        {
            LblTitle.Text = _findings.Count == 0
                ? "Nothing to report."
                : "Select a finding to read what it means.";
            LblCode.Text = ""; LblMessage.Text = ""; LblFix.Text = "";
            LblMeaning.Text = ""; LblWhy.Text = ""; LblHow.Text = "";
            return;
        }

        var explanation = FindingExplanations.For(finding);
        LblTitle.Text = explanation.Title;
        LblCode.Text = finding.Code + "  [" + finding.Severity.ToString().ToLowerInvariant() + "]" +
                       (string.IsNullOrEmpty(finding.ObjectTag) ? "" : "  on " + finding.ObjectTag);
        LblMessage.Text = finding.Message;
        LblFix.Text = string.IsNullOrEmpty(finding.Fix) ? "" : "Fix: " + finding.Fix;
        LblMeaning.Text = explanation.Meaning;
        LblWhy.Text = explanation.Why;
        LblHow.Text = explanation.HowToFix;
    }

    // ── Degrees of freedom tab ───────────────────────────────────────────

    private void ShowObject(ObjectDegreesOfFreedom? dof)
    {
        BtnOpenObject.IsVisible = dof != null && _locate != null;

        if (dof == null)
        {
            LblObjectTitle.Text = "Select an object to see the specifications its calculation mode reads.";
            LblObjectMode.Text = ""; LblObjectNote.Text = "";
            GridSlots.ItemsSource = null;
            return;
        }

        LblObjectTitle.Text = dof.ObjectTag + ": " +
            (dof.Supported
                ? dof.Specified + " of " + dof.Required + " required specification(s) set"
                : "specifications not catalogued for this type");
        LblObjectMode.Text = dof.ObjectType + (string.IsNullOrEmpty(dof.Mode) ? "" : ", mode " + dof.Mode);
        LblObjectNote.Text = dof.Note;

        var su = _flowsheet.FlowsheetOptions.SelectedUnitSystem;
        GridSlots.ItemsSource = dof.Slots.Select(s => new SlotRow
        {
            Name = s.Name,
            Value = Display(s, su),
            Status = s.Required ? (s.IsSet ? "set" : "MISSING") : "optional",
            Note = s.Note
        }).ToList();
    }

    /// <summary>The slot's value in the simulation's unit system, or empty when it has none.</summary>
    private static string Display(SpecificationSlot slot, IUnitsOfMeasure su)
    {
        if (!slot.Value.HasValue) return "";
        var value = slot.Value.Value;

        string units;
        switch (slot.Units)
        {
            case "K": units = su.temperature; break;
            case "Pa": units = su.pressure; break;
            case "kW": units = su.heatflow; break;
            case "kg/s": units = su.massflow; break;
            case "mol/s": units = su.molarflow; break;
            case "m3/s": units = su.volumetricFlow; break;
            case "m3": units = su.volume; break;
            case "m": units = su.distance; break;
            case "m2": units = su.area; break;
            case "kJ/kg": units = su.enthalpy; break;
            case "kJ/[kg.K]": units = su.entropy; break;
            case "W/[m2.K]": units = su.heat_transf_coeff; break;
            default: units = slot.Units; break;
        }

        if (units != slot.Units && !string.IsNullOrEmpty(units))
        {
            try { value = Converter.ConvertFromSI(units, value); } catch (Exception) { units = slot.Units; }
        }

        return (value.ToString("G6", CultureInfo.InvariantCulture) + " " + units).Trim();
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private void Locate(string? tag)
    {
        if (_locate == null || string.IsNullOrEmpty(tag)) return;
        var obj = _flowsheet.SimulationObjects.Values.FirstOrDefault(o =>
            o.GraphicObject != null && string.Equals(o.GraphicObject.Tag, tag, StringComparison.OrdinalIgnoreCase));
        if (obj != null) _locate(obj.Name);
    }

    private static string TutorialLanguage()
    {
        var culture = S.CultureInfo ?? "en";
        return culture.StartsWith("pt", StringComparison.OrdinalIgnoreCase) ? "pt-BR" : "en";
    }

    private string BuildReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine(_title.ToUpperInvariant());
        sb.AppendLine(new string('-', 60));
        if (_findings.Count == 0) sb.AppendLine("Nothing known to be wrong.");
        foreach (var f in _findings) sb.AppendLine(f.ToString());
        sb.AppendLine();

        if (_dof != null)
        {
            sb.AppendLine("DEGREES OF FREEDOM");
            sb.AppendLine(new string('-', 60));
            var su = _flowsheet.FlowsheetOptions.SelectedUnitSystem;
            foreach (var o in _dof.Objects)
            {
                sb.AppendLine(o.ObjectTag + " (" + o.ObjectType + (string.IsNullOrEmpty(o.Mode) ? "" : ", " + o.Mode) + "): " +
                    (o.Supported ? o.Specified + "/" + o.Required + " set" : "not catalogued"));
                foreach (var s in o.Slots)
                {
                    sb.AppendLine("    " + s.Name + " = " + Display(s, su) + "  [" +
                        (s.Required ? (s.IsSet ? "set" : "MISSING") : "optional") + "]");
                }
            }
        }
        return sb.ToString();
    }

    private static void OpenUrl(string url)
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
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); }
            catch (Exception) { }
        }
    }
}
