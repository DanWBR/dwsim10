using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums;
using DWSIM.SharedClasses.Flowsheet.Optimization;
using cv = DWSIM.SharedClasses.SystemsOfUnits.Converter;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// Sensitivity Analysis. Edits <see cref="SensitivityAnalysisCase"/> objects held in
/// FlowsheetBase.SensAnalysisCollection, which SaveToXML/LoadFromXML persist inside the
/// simulation file, so cases round-trip with the Classic and Eto UIs.
/// </summary>
public partial class SensitivityAnalysisWindow : Window
{
    private readonly IFlowsheet _flowsheet;
    private readonly DWSIM.FlowsheetBase.FlowsheetBase? _fsbase;

    private SensitivityAnalysisCase? _case;
    private bool _loading;
    private bool _running;
    private bool _abort;

    private double _savedIndepValue;
    private string? _savedIndepObjID;
    private string? _savedIndepProp;

    // Parameterless ctor required by Avalonia's XAML compiler (designer-only).
    public SensitivityAnalysisWindow() : this(null!) { }

    public SensitivityAnalysisWindow(IFlowsheet flowsheet)
    {
        _flowsheet = flowsheet!;
        _fsbase = flowsheet as DWSIM.FlowsheetBase.FlowsheetBase;
        InitializeComponent();
        IconHelper.ApplyWindowIcon(this);
        if (flowsheet == null) return;

        PopulateObjectCombos();
        WireEvents();
        RefreshCaseList(selectLast: false);
    }

    private List<SensitivityAnalysisCase> Cases =>
        _fsbase != null ? _fsbase.SensAnalysisCollection : new List<SensitivityAnalysisCase>();

    // -------------------------------------------------------------------------
    // Case management
    // -------------------------------------------------------------------------

    private void RefreshCaseList(bool selectLast)
    {
        if (_fsbase == null)
        {
            TbProgress.Text = "This flowsheet does not support stored analysis cases.";
            return;
        }

        if (Cases.Count == 0)
        {
            Cases.Add(new SensitivityAnalysisCase { name = "Case 1" });
        }

        _loading = true;
        CbCase.Items.Clear();
        for (int i = 0; i < Cases.Count; i++)
            CbCase.Items.Add(CaseLabel(Cases[i], i));
        _loading = false;

        CbCase.SelectedIndex = selectLast ? Cases.Count - 1 : 0;
    }

    private static string CaseLabel(SensitivityAnalysisCase c, int index) =>
        string.IsNullOrWhiteSpace(c.name) ? $"Case {index + 1}" : c.name;

    private void LoadCase(SensitivityAnalysisCase c)
    {
        _loading = true;
        try
        {
            _case = c;

            TbCaseName.Text = c.name ?? "";
            TbCaseDescription.Text = c.description ?? "";

            SelectObject(CbIndepObject, c.iv1.objectID);
            PopulateIndepProps();
            var pidx = IndexOfPropID(CbIndepProp, c.iv1.propID ?? "");
            if (pidx >= 0) CbIndepProp.SelectedIndex = pidx;

            TbIndepUnit.Text = "Units: " + (string.IsNullOrEmpty(c.iv1.unit) ? "-" : c.iv1.unit);
            TbMin.Text = c.iv1.lowerlimit.GetValueOrDefault().ToString("G6", CultureInfo.InvariantCulture);
            TbMax.Text = c.iv1.upperlimit.GetValueOrDefault().ToString("G6", CultureInfo.InvariantCulture);
            NudPoints.Value = Math.Max(2, c.iv1.points);

            LbObserved.Items.Clear();
            foreach (var dv in c.depvariables.Values)
                LbObserved.Items.Add(DepLabel(dv));

            TbResults.Text = c.stats ?? "";
            TbProgress.Text = "";
        }
        finally { _loading = false; }

        // A brand new case has no independent variable yet: adopt whatever the pickers are
        // showing, so the case is runnable without having to touch the combo boxes first.
        if (string.IsNullOrEmpty(c.iv1.objectID)) StoreIndepObject();
        if (string.IsNullOrEmpty(c.iv1.propID)) StoreIndepProp();
    }

    private static string DepLabel(SAVariable v) =>
        $"{v.objectTAG}.{v.propID}" + (string.IsNullOrEmpty(v.unit) ? "" : $" ({v.unit})");

    // -------------------------------------------------------------------------
    // Setup
    // -------------------------------------------------------------------------

    private void PopulateObjectCombos()
    {
        var objs = _flowsheet.SimulationObjects.Values
            .Where(o => o.GraphicObject != null)
            .OrderBy(o => o.GraphicObject.Tag)
            .ToList();

        foreach (var o in objs)
        {
            CbIndepObject.Items.Add(new ObjItem(o.GraphicObject.Tag, o.Name));
            CbObsObject.Items.Add(new ObjItem(o.GraphicObject.Tag, o.Name));
        }

        if (CbIndepObject.Items.Count > 0) { CbIndepObject.SelectedIndex = 0; CbObsObject.SelectedIndex = 0; }
    }

    private void WireEvents()
    {
        BtnClose.Click += (_, _) => Close();

        CbCase.SelectionChanged += (_, _) =>
        {
            if (_loading) return;
            var idx = CbCase.SelectedIndex;
            if (idx >= 0 && idx < Cases.Count) LoadCase(Cases[idx]);
        };

        BtnNewCase.Click += (_, _) =>
        {
            if (_fsbase == null) return;
            Cases.Add(new SensitivityAnalysisCase { name = $"Case {Cases.Count + 1}" });
            RefreshCaseList(selectLast: true);
            TbProgress.Text = "New case created.";
        };

        BtnCloneCase.Click += (_, _) =>
        {
            if (_fsbase == null || _case == null) return;
            var copy = (SensitivityAnalysisCase)_case.Clone();
            copy.name = (_case.name ?? "Case") + " (copy)";
            Cases.Add(copy);
            RefreshCaseList(selectLast: true);
            TbProgress.Text = "Case duplicated.";
        };

        BtnDeleteCase.Click += (_, _) =>
        {
            if (_fsbase == null || _case == null) return;
            Cases.Remove(_case);
            _case = null;
            RefreshCaseList(selectLast: false);
            TbProgress.Text = "Case removed.";
        };

        TbCaseName.TextChanged += (_, _) =>
        {
            if (_loading || _case == null) return;
            _case.name = TbCaseName.Text ?? "";
            var idx = CbCase.SelectedIndex;
            if (idx >= 0 && idx < CbCase.Items.Count)
            {
                // Keep the picker label in sync without re-entering SelectionChanged.
                _loading = true;
                CbCase.Items[idx] = CaseLabel(_case, idx);
                CbCase.SelectedIndex = idx;
                _loading = false;
            }
        };
        TbCaseDescription.TextChanged += (_, _) =>
        {
            if (_loading || _case == null) return;
            _case.description = TbCaseDescription.Text ?? "";
        };

        CbIndepObject.SelectionChanged += (_, _) =>
        {
            if (_loading) return;
            PopulateIndepProps();
            StoreIndepObject();
        };
        CbIndepProp.SelectionChanged += (_, _) =>
        {
            if (_loading) return;
            StoreIndepProp();
        };
        CbObsObject.SelectionChanged += (_, _) => PopulateObsProps();

        TbMin.TextChanged += (_, _) =>
        {
            if (_loading || _case == null) return;
            if (TryVal(TbMin.Text, out var v)) _case.iv1.lowerlimit = v;
        };
        TbMax.TextChanged += (_, _) =>
        {
            if (_loading || _case == null) return;
            if (TryVal(TbMax.Text, out var v)) _case.iv1.upperlimit = v;
        };
        NudPoints.ValueChanged += (_, _) =>
        {
            if (_loading || _case == null) return;
            _case.iv1.points = (int)(NudPoints.Value ?? 10);
        };

        BtnAddObs.Click += (_, _) => AddDependent();
        BtnRemoveObs.Click += (_, _) => RemoveDependent();
        BtnRun.Click += async (_, _) => await RunAnalysisAsync();
        BtnAbort.Click += (_, _) => { _abort = true; TbProgress.Text = "Aborting..."; };
        BtnRestore.Click += (_, _) => RestoreOriginalValue();
        BtnCopyResults.Click += async (_, _) =>
        {
            var top = GetTopLevel(this);
            if (top?.Clipboard != null && !string.IsNullOrEmpty(TbResults.Text))
            {
                await top.Clipboard.SetTextAsync(TbResults.Text!);
                TbProgress.Text = "Results copied to the clipboard.";
            }
        };

        PopulateIndepProps();
        PopulateObsProps();
    }

    private void StoreIndepObject()
    {
        if (_case == null) return;
        var obj = GetSelectedObject(CbIndepObject);
        if (obj == null) return;
        _case.iv1.objectID = obj.Name;
        _case.iv1.objectTAG = obj.GraphicObject.Tag;
    }

    private void StoreIndepProp()
    {
        if (_case == null) return;
        var obj = GetSelectedObject(CbIndepObject);
        var prop = SelectedPropID(CbIndepProp);
        if (obj == null || prop == null) return;
        var su = _flowsheet.FlowsheetOptions.SelectedUnitSystem;
        _case.iv1.propID = prop;
        _case.iv1.unit = obj.GetPropertyUnit(prop, su);
        TbIndepUnit.Text = "Units: " + (string.IsNullOrEmpty(_case.iv1.unit) ? "-" : _case.iv1.unit);
    }

    private void PopulateIndepProps()
    {
        var selected = SelectedPropID(CbIndepProp);
        CbIndepProp.Items.Clear();
        var obj = GetSelectedObject(CbIndepObject);
        if (obj == null) return;
        FillProps(CbIndepProp, obj, PropertyType.WR);
        if (CbIndepProp.Items.Count == 0) return;
        var idx = selected == null ? -1 : IndexOfPropID(CbIndepProp, selected);
        CbIndepProp.SelectedIndex = idx >= 0 ? idx : 0;
    }

    private void PopulateObsProps()
    {
        CbObsProp.Items.Clear();
        var obj = GetSelectedObject(CbObsObject);
        if (obj == null) return;
        FillProps(CbObsProp, obj, PropertyType.ALL);
        if (CbObsProp.Items.Count > 0) CbObsProp.SelectedIndex = 0;
    }

    /// <summary>
    /// Property pickers show the translated caption but carry the raw property ID, which is
    /// what gets written into the case (and what the engine's SetPropertyValue expects).
    /// </summary>
    private void FillProps(ComboBox cb, ISimulationObject obj, PropertyType type)
    {
        var props = obj.GetProperties(type) ?? Array.Empty<string>();
        foreach (var p in props.OrderBy(x => _flowsheet.GetTranslatedString(x)))
            cb.Items.Add(new PropItem(p, _flowsheet.GetTranslatedString(p)));
    }

    private static string? SelectedPropID(ComboBox cb) =>
        cb.SelectedItem is PropItem p ? p.ID : null;

    private static int IndexOfPropID(ComboBox cb, string id)
    {
        for (int i = 0; i < cb.Items.Count; i++)
            if (cb.Items[i] is PropItem p && p.ID == id) return i;
        return -1;
    }

    private sealed class PropItem
    {
        public string ID { get; }
        private readonly string _caption;
        public PropItem(string id, string caption) => (ID, _caption) = (id, string.IsNullOrEmpty(caption) ? id : caption);
        public override string ToString() => _caption;
    }

    // -------------------------------------------------------------------------
    // Dependent variables
    // -------------------------------------------------------------------------

    private void AddDependent()
    {
        if (_case == null) return;
        var obj = GetSelectedObject(CbObsObject);
        var prop = SelectedPropID(CbObsProp);
        if (obj == null || prop == null) return;

        var su = _flowsheet.FlowsheetOptions.SelectedUnitSystem;
        var v = new SAVariable
        {
            id = Guid.NewGuid().ToString(),
            objectID = obj.Name,
            objectTAG = obj.GraphicObject.Tag,
            propID = prop,
            unit = obj.GetPropertyUnit(prop, su)
        };
        _case.depvariables.Add(v.id, v);
        LbObserved.Items.Add(DepLabel(v));
    }

    private void RemoveDependent()
    {
        if (_case == null) return;
        var idx = LbObserved.SelectedIndex;
        if (idx < 0 || idx >= _case.depvariables.Count) return;
        var key = _case.depvariables.Keys.ElementAt(idx);
        _case.depvariables.Remove(key);
        LbObserved.Items.RemoveAt(idx);
    }

    // -------------------------------------------------------------------------
    // Run
    // -------------------------------------------------------------------------

    private async Task RunAnalysisAsync()
    {
        if (_running || _case == null) return;

        var c = _case;
        if (string.IsNullOrEmpty(c.iv1.objectID) || string.IsNullOrEmpty(c.iv1.propID))
        { TbProgress.Text = "Select an independent variable first."; return; }
        if (!_flowsheet.SimulationObjects.ContainsKey(c.iv1.objectID))
        { TbProgress.Text = "The independent variable's object is no longer on the flowsheet."; return; }
        if (c.depvariables.Count == 0)
        { TbProgress.Text = "Add at least one dependent variable."; return; }

        // Limits are stored in the unit recorded on the case, matching the Classic/Eto editors.
        double llSI = cv.ConvertToSI(c.iv1.unit, c.iv1.lowerlimit.GetValueOrDefault());
        double ulSI = cv.ConvertToSI(c.iv1.unit, c.iv1.upperlimit.GetValueOrDefault());
        int points = Math.Max(2, c.iv1.points);

        var indep = _flowsheet.SimulationObjects[c.iv1.objectID];
        _savedIndepObjID = c.iv1.objectID;
        _savedIndepProp = c.iv1.propID;
        _savedIndepValue = Convert.ToDouble(indep.GetPropertyValue(c.iv1.propID));

        _running = true;
        _abort = false;
        BtnRun.IsEnabled = false;
        BtnAbort.IsEnabled = true;
        TbProgress.Text = "Running...";

        var depvars = c.depvariables.Values.ToList();

        var sb = new StringBuilder();
        sb.AppendLine($"Sensitivity Analysis: {c.name}");
        if (!string.IsNullOrWhiteSpace(c.description)) sb.AppendLine(c.description);
        sb.AppendLine();
        sb.Append($"{c.iv1.objectTAG + "." + c.iv1.propID + " (" + c.iv1.unit + ")",-30}");
        foreach (var dv in depvars) sb.Append($"  {DepLabel(dv),-30}");
        sb.AppendLine();
        sb.AppendLine(new string('-', 30 + depvars.Count * 32));

        var rows = new List<double[]>();

        try
        {
            for (int i = 0; i < points; i++)
            {
                if (_abort) break;

                double siValue = llSI + i * (ulSI - llSI) / (points - 1);

                await Task.Run(() =>
                {
                    indep.SetPropertyValue(c.iv1.propID, siValue);
                    _flowsheet.RequestCalculationAndWait();
                });

                var row = new double[depvars.Count];
                for (int k = 0; k < depvars.Count; k++)
                {
                    var dv = depvars[k];
                    if (_flowsheet.SimulationObjects.TryGetValue(dv.objectID, out var o))
                    {
                        dv.currentvalue = cv.ConvertFromSI(dv.unit, Convert.ToDouble(o.GetPropertyValue(dv.propID)));
                        row[k] = dv.currentvalue;
                    }
                    else row[k] = double.NaN;
                }
                rows.Add(row);

                sb.Append($"{cv.ConvertFromSI(c.iv1.unit, siValue),-30:G6}");
                foreach (var v in row) sb.Append($"  {v,-30:G6}");
                sb.AppendLine();

                var done = i + 1;
                var snapshot = sb.ToString();
                Dispatcher.UIThread.Post(() =>
                {
                    TbProgress.Text = $"Point {done}/{points}...";
                    TbResults.Text = snapshot;
                });
            }

            // results is [XmlIgnore] in the engine model: it lives for this session only, while
            // stats is persisted along with the case definition.
            c.results = new System.Collections.ArrayList();
            foreach (var row in rows) c.results.Add(row);
            c.stats = sb.ToString();

            TbProgress.Text = _abort ? "Aborted." : "Complete.";
        }
        catch (Exception ex)
        {
            sb.AppendLine("ERROR: " + ex.Message);
            TbProgress.Text = "Failed.";
        }
        finally
        {
            // Always put the flowsheet back where it started.
            await Task.Run(() =>
            {
                try
                {
                    indep.SetPropertyValue(c.iv1.propID, _savedIndepValue);
                    _flowsheet.RequestCalculationAndWait();
                }
                catch { }
            });

            TbResults.Text = sb.ToString();
            BtnRun.IsEnabled = true;
            BtnAbort.IsEnabled = false;
            _running = false;
        }
    }

    private void RestoreOriginalValue()
    {
        if (_savedIndepObjID == null || _savedIndepProp == null) return;
        if (!_flowsheet.SimulationObjects.TryGetValue(_savedIndepObjID, out var o)) return;
        o.SetPropertyValue(_savedIndepProp, _savedIndepValue);
        TbProgress.Text = "Original value restored. Re-solve to update results.";
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static bool TryVal(string? text, out double value) =>
        UtilityHelpers.TryVal(text, out value);

    private ISimulationObject? GetSelectedObject(ComboBox cb)
    {
        if (cb.SelectedItem is not ObjItem item) return null;
        return _flowsheet.SimulationObjects.TryGetValue(item.InternalName, out var o) ? o : null;
    }

    private static void SelectObject(ComboBox cb, string internalName)
    {
        if (string.IsNullOrEmpty(internalName)) return;
        for (int i = 0; i < cb.Items.Count; i++)
        {
            if (cb.Items[i] is ObjItem it && it.InternalName == internalName) { cb.SelectedIndex = i; return; }
        }
    }

    private sealed class ObjItem
    {
        public string Tag { get; }
        public string InternalName { get; }
        public ObjItem(string tag, string name) => (Tag, InternalName) = (tag, name);
        public override string ToString() => Tag;
    }
}
