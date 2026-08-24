using System;
using DWSIM.SharedClasses;
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
/// Flowsheet Optimizer. Edits <see cref="OptimizationCase"/> objects held in
/// FlowsheetBase.OptimizationCollection, which SaveToXML/LoadFromXML persist inside the
/// simulation file, and runs them through the same DotNumerics solvers the Classic and
/// Eto UIs use, so a case behaves identically wherever it is opened.
/// </summary>
public partial class OptimizerWindow : Window
{
    // Display order of the method picker, mapped onto OptimizationCase.SolvingMethod.
    private static readonly OptimizationCase.SolvingMethod[] Methods =
    {
        OptimizationCase.SolvingMethod.DN_NELDERMEAD_SIMPLEX,
        OptimizationCase.SolvingMethod.DN_LBFGS,
        OptimizationCase.SolvingMethod.DN_TRUNCATED_NEWTON,
        OptimizationCase.SolvingMethod.DN_NELDERMEAD_SIMPLEX_B,
        OptimizationCase.SolvingMethod.DN_LBFGS_B,
        OptimizationCase.SolvingMethod.DN_TRUNCATED_NEWTON_B
    };

    private static readonly string[] MethodNames =
    {
        "Simplex", "L-BFGS", "Truncated Newton",
        "Simplex (Bounded)", "L-BFGS (Bounded)", "Truncated Newton (Bounded)"
    };

    private readonly IFlowsheet _flowsheet;
    private readonly DWSIM.FlowsheetBase.FlowsheetBase? _fsbase;

    private OptimizationCase? _case;
    private OPTVariable? _selectedVar;
    private bool _loading;
    private bool _running;
    private bool _onGradient;

    private readonly StringBuilder _log = new();

    // Parameterless ctor required by Avalonia's XAML compiler (designer-only).
    public OptimizerWindow() : this(null!) { }

    public OptimizerWindow(IFlowsheet flowsheet)
    {
        _flowsheet = flowsheet!;
        _fsbase = flowsheet as DWSIM.FlowsheetBase.FlowsheetBase;
        InitializeComponent();
        IconHelper.ApplyWindowIcon(this);
        if (flowsheet == null) return;

        PopulateStaticCombos();
        WireEvents();
        RefreshCaseList(selectLast: false);
    }

    private List<OptimizationCase> Cases =>
        _fsbase != null ? _fsbase.OptimizationCollection : new List<OptimizationCase>();

    // -------------------------------------------------------------------------
    // Case management
    // -------------------------------------------------------------------------

    private void RefreshCaseList(bool selectLast)
    {
        if (_fsbase == null)
        {
            TbProgress.Text = "This flowsheet does not support stored optimization cases.";
            return;
        }

        if (Cases.Count == 0) Cases.Add(new OptimizationCase { name = "Case 1" });

        _loading = true;
        CbCase.Items.Clear();
        for (int i = 0; i < Cases.Count; i++) CbCase.Items.Add(CaseLabel(Cases[i], i));
        _loading = false;

        CbCase.SelectedIndex = selectLast ? Cases.Count - 1 : 0;
    }

    private static string CaseLabel(OptimizationCase c, int index) =>
        string.IsNullOrWhiteSpace(c.name) ? $"Case {index + 1}" : c.name;

    private void LoadCase(OptimizationCase c)
    {
        _loading = true;
        try
        {
            _case = c;

            TbCaseName.Text = c.name ?? "";
            TbCaseDescription.Text = c.description ?? "";
            CbType.SelectedIndex = (int)c.type;
            CbObjFuncType.SelectedIndex = (int)c.objfunctype;
            TbExpression.Text = c.expression ?? "";

            var mi = Array.IndexOf(Methods, c.solvm);
            CbMethod.SelectedIndex = mi >= 0 ? mi : 0;

            TbMaxIts.Text = c.maxits.ToString(CultureInfo.InvariantCulture);
            TbTolerance.Text = c.tolerance.ToString("G6", CultureInfo.InvariantCulture);
            TbEpsilon.Text = c.epsilon.ToString("G6", CultureInfo.InvariantCulture);
            TbBarrier.Text = c.barriermultiplier.ToString("G6", CultureInfo.InvariantCulture);

            RefreshVariableList();
            TbResults.Text = c.stats ?? "";
            TbProgress.Text = "";
        }
        finally { _loading = false; }
    }

    private void RefreshVariableList()
    {
        LbVariables.Items.Clear();
        if (_case == null) return;
        foreach (var v in _case.variables.Values) LbVariables.Items.Add(VarLabel(v));
        SelectVariable(null);
    }

    private static string VarLabel(OPTVariable v)
    {
        var type = v.type switch
        {
            OPTVariableType.Independent => "IND",
            OPTVariableType.Dependent => "DEP",
            OPTVariableType.Auxiliary => "AUX",
            _ => "CON"
        };
        var target = string.IsNullOrEmpty(v.objectTAG) ? "(unassigned)" : $"{v.objectTAG}.{v.propID}";
        return $"[{type}] {v.name} = {target}";
    }

    // -------------------------------------------------------------------------
    // Setup
    // -------------------------------------------------------------------------

    private void PopulateStaticCombos()
    {
        foreach (var s in new[] { "Minimization", "Maximization" }) CbType.Items.Add(s);
        foreach (var s in new[] { "Variable", "Expression" }) CbObjFuncType.Items.Add(s);
        foreach (var s in MethodNames) CbMethod.Items.Add(s);
        foreach (var s in new[] { "DEP", "IND", "AUX", "CON" }) CbVarType.Items.Add(s);

        foreach (var o in _flowsheet.SimulationObjects.Values
                     .Where(o => o.GraphicObject != null)
                     .OrderBy(o => o.GraphicObject.Tag))
        {
            CbVarObject.Items.Add(new ObjItem(o.GraphicObject.Tag, o.Name));
        }
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
            Cases.Add(new OptimizationCase { name = $"Case {Cases.Count + 1}" });
            RefreshCaseList(selectLast: true);
            TbProgress.Text = "New case created.";
        };
        BtnCloneCase.Click += (_, _) =>
        {
            if (_fsbase == null || _case == null) return;
            var copy = (OptimizationCase)_case.Clone();
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
                _loading = true;
                CbCase.Items[idx] = CaseLabel(_case, idx);
                CbCase.SelectedIndex = idx;
                _loading = false;
            }
        };
        TbCaseDescription.TextChanged += (_, _) => { if (!_loading && _case != null) _case.description = TbCaseDescription.Text ?? ""; };
        TbExpression.TextChanged += (_, _) => { if (!_loading && _case != null) _case.expression = TbExpression.Text ?? ""; };

        CbType.SelectionChanged += (_, _) => { if (!_loading && _case != null && CbType.SelectedIndex >= 0) _case.type = (OPTType)CbType.SelectedIndex; };
        CbObjFuncType.SelectionChanged += (_, _) => { if (!_loading && _case != null && CbObjFuncType.SelectedIndex >= 0) _case.objfunctype = (OPTObjectiveFunctionType)CbObjFuncType.SelectedIndex; };
        CbMethod.SelectionChanged += (_, _) =>
        {
            if (_loading || _case == null) return;
            var i = CbMethod.SelectedIndex;
            if (i >= 0 && i < Methods.Length) _case.solvm = Methods[i];
        };

        TbMaxIts.TextChanged += (_, _) => { if (!_loading && _case != null && UtilityHelpers.TryVal(TbMaxIts.Text, out var v) && v >= 1) _case.maxits = (int)v; };
        TbTolerance.TextChanged += (_, _) => { if (!_loading && _case != null && UtilityHelpers.TryVal(TbTolerance.Text, out var v) && v > 0) _case.tolerance = v; };
        TbEpsilon.TextChanged += (_, _) => { if (!_loading && _case != null && UtilityHelpers.TryVal(TbEpsilon.Text, out var v) && v > 0) _case.epsilon = v; };
        TbBarrier.TextChanged += (_, _) => { if (!_loading && _case != null && UtilityHelpers.TryVal(TbBarrier.Text, out var v)) _case.barriermultiplier = v; };

        // ---- variables ----
        BtnAddVar.Click += (_, _) =>
        {
            if (_case == null) return;
            var v = new OPTVariable
            {
                id = Guid.NewGuid().ToString(),
                name = "var" + _case.variables.Count,
                type = OPTVariableType.Independent
            };
            _case.variables.Add(v.id, v);
            RefreshVariableList();
            LbVariables.SelectedIndex = _case.variables.Count - 1;
        };
        BtnRemoveVar.Click += (_, _) =>
        {
            if (_case == null || _selectedVar == null) return;
            _case.variables.Remove(_selectedVar.id);
            RefreshVariableList();
        };

        LbVariables.SelectionChanged += (_, _) =>
        {
            if (_loading || _case == null) return;
            var idx = LbVariables.SelectedIndex;
            SelectVariable(idx >= 0 && idx < _case.variables.Count
                ? _case.variables.Values.ElementAt(idx)
                : null);
        };

        TbVarName.TextChanged += (_, _) => { if (!_loading && _selectedVar != null) { _selectedVar.name = TbVarName.Text ?? ""; UpdateSelectedVarLabel(); } };
        CbVarType.SelectionChanged += (_, _) =>
        {
            if (_loading || _selectedVar == null || CbVarType.SelectedIndex < 0) return;
            _selectedVar.type = (OPTVariableType)CbVarType.SelectedIndex;
            UpdateSelectedVarLabel();
        };
        CbVarObject.SelectionChanged += (_, _) =>
        {
            if (_loading) return;
            PopulateVarProps();
            if (_selectedVar == null) return;
            var obj = GetSelectedObject(CbVarObject);
            if (obj == null) return;
            _selectedVar.objectID = obj.Name;
            _selectedVar.objectTAG = obj.GraphicObject.Tag;
            UpdateSelectedVarLabel();
        };
        CbVarProp.SelectionChanged += (_, _) =>
        {
            if (_loading || _selectedVar == null) return;
            var obj = GetSelectedObject(CbVarObject);
            var prop = SelectedPropID(CbVarProp);
            if (obj == null || prop == null) return;
            var su = _flowsheet.FlowsheetOptions.SelectedUnitSystem;
            _selectedVar.propID = prop;
            _selectedVar.unit = obj.GetPropertyUnit(prop, su);
            TbVarUnit.Text = "Units: " + (string.IsNullOrEmpty(_selectedVar.unit) ? "-" : _selectedVar.unit);
            UpdateSelectedVarLabel();
        };
        TbVarInitial.TextChanged += (_, _) => { if (!_loading && _selectedVar != null && UtilityHelpers.TryVal(TbVarInitial.Text, out var v)) _selectedVar.initialvalue = v; };
        TbVarLower.TextChanged += (_, _) => { if (!_loading && _selectedVar != null && UtilityHelpers.TryVal(TbVarLower.Text, out var v)) _selectedVar.lowerlimit = v; };
        TbVarUpper.TextChanged += (_, _) => { if (!_loading && _selectedVar != null && UtilityHelpers.TryVal(TbVarUpper.Text, out var v)) _selectedVar.upperlimit = v; };

        BtnRun.Click += async (_, _) => await RunAsync();
        BtnAbort.Click += (_, _) =>
        {
            DWSIM.GlobalSettings.Settings.CalculatorStopRequested = true;
            TbProgress.Text = "Aborting...";
        };
        BtnRestore.Click += (_, _) => RestoreInitialValues();
        BtnCopyResults.Click += async (_, _) =>
        {
            var top = GetTopLevel(this);
            if (top?.Clipboard != null && !string.IsNullOrEmpty(TbResults.Text))
            {
                await top.Clipboard.SetTextAsync(TbResults.Text!);
                TbProgress.Text = "Log copied to the clipboard.";
            }
        };
    }

    private void SelectVariable(OPTVariable? v)
    {
        _loading = true;
        try
        {
            _selectedVar = v;
            VarEditor.IsEnabled = v != null;
            if (v == null)
            {
                TbVarName.Text = "";
                CbVarType.SelectedIndex = -1;
                CbVarObject.SelectedIndex = -1;
                CbVarProp.Items.Clear();
                TbVarUnit.Text = "Units: -";
                TbVarInitial.Text = TbVarLower.Text = TbVarUpper.Text = "";
                return;
            }

            TbVarName.Text = v.name ?? "";
            CbVarType.SelectedIndex = (int)v.type;
            SelectObject(CbVarObject, v.objectID);
            PopulateVarProps();
            var pidx = IndexOfPropID(CbVarProp, v.propID ?? "");
            if (pidx >= 0) CbVarProp.SelectedIndex = pidx;
            TbVarUnit.Text = "Units: " + (string.IsNullOrEmpty(v.unit) ? "-" : v.unit);
            TbVarInitial.Text = v.initialvalue.ToString("G6", CultureInfo.InvariantCulture);
            TbVarLower.Text = v.lowerlimit.GetValueOrDefault().ToString("G6", CultureInfo.InvariantCulture);
            TbVarUpper.Text = v.upperlimit.GetValueOrDefault().ToString("G6", CultureInfo.InvariantCulture);
        }
        finally { _loading = false; }
    }

    private void UpdateSelectedVarLabel()
    {
        if (_case == null || _selectedVar == null) return;
        var idx = _case.variables.Values.ToList().IndexOf(_selectedVar);
        if (idx < 0 || idx >= LbVariables.Items.Count) return;
        var keepLoading = _loading;
        _loading = true;
        LbVariables.Items[idx] = VarLabel(_selectedVar);
        LbVariables.SelectedIndex = idx;
        _loading = keepLoading;
    }

    /// <summary>
    /// The picker shows the translated caption but carries the raw property ID, which is what
    /// gets written into the case (and what the engine's Get/SetPropertyValue expect).
    /// </summary>
    private void PopulateVarProps()
    {
        var selected = SelectedPropID(CbVarProp);
        CbVarProp.Items.Clear();
        var obj = GetSelectedObject(CbVarObject);
        if (obj == null) return;
        // ALL, because DEP/AUX/CON variables only need to be readable.
        var props = obj.GetProperties(PropertyType.ALL) ?? Array.Empty<string>();
        foreach (var p in props.OrderBy(x => _flowsheet.GetTranslatedString(x)))
            CbVarProp.Items.Add(new PropItem(p, _flowsheet.GetTranslatedString(p)));
        if (selected != null)
        {
            var idx = IndexOfPropID(CbVarProp, selected);
            if (idx >= 0) CbVarProp.SelectedIndex = idx;
        }
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
    // Run
    // -------------------------------------------------------------------------

    private async Task RunAsync()
    {
        if (_running || _case == null) return;
        var c = _case;

        var indvars = c.variables.Values.Where(x => x.type == OPTVariableType.Independent).ToList();
        var depvars = c.variables.Values.Where(x => x.type == OPTVariableType.Dependent).ToList();

        if (indvars.Count == 0)
        { TbProgress.Text = "Add at least one IND variable."; return; }
        if (c.objfunctype == OPTObjectiveFunctionType.Variable && depvars.Count == 0)
        { TbProgress.Text = "A DEP variable is required when the objective function is a variable."; return; }
        if (c.objfunctype == OPTObjectiveFunctionType.Expression && string.IsNullOrWhiteSpace(c.expression))
        { TbProgress.Text = "Enter an expression for the objective function."; return; }
        if (c.variables.Values.Any(v => !string.IsNullOrEmpty(v.objectID) && !_flowsheet.SimulationObjects.ContainsKey(v.objectID)))
        { TbProgress.Text = "One of the variables points at an object that is no longer on the flowsheet."; return; }
        if (c.variables.Values.Any(v => string.IsNullOrEmpty(v.objectID) || string.IsNullOrEmpty(v.propID)))
        { TbProgress.Text = "Every variable needs an object and a property assigned."; return; }

        _running = true;
        _log.Clear();
        BtnRun.IsEnabled = false;
        BtnAbort.IsEnabled = true;
        DWSIM.GlobalSettings.Settings.CalculatorStopRequested = false;

        var methodIdx = Math.Max(0, Array.IndexOf(Methods, c.solvm));
        AppendLog($"Optimization '{c.name}': {c.type} using {MethodNames[methodIdx]}");
        AppendLog($"{indvars.Count} IND variable(s), tolerance {c.tolerance:G4}, max {c.maxits} function evaluations.");
        AppendLog("");
        TbProgress.Text = "Running...";

        try
        {
            await Task.Run(() => RunSolver(c, indvars));
            UpdateVariableValues(c);
            AppendLog("");
            AppendLog("Final values:");
            foreach (var v in c.variables.Values)
                AppendLog($"  [{v.type}] {v.name} = {v.currentvalue:G8} {v.unit}");
            TbProgress.Text = "Complete.";
        }
        catch (OperationCanceledException)
        {
            AppendLog("Run aborted by the user.");
            TbProgress.Text = "Aborted.";
        }
        catch (Exception ex)
        {
            var inner = ex;
            while (inner.InnerException != null) inner = inner.InnerException;
            AppendLog(inner is OperationCanceledException
                ? "Run aborted by the user."
                : "ERROR: " + inner.Message);
            TbProgress.Text = inner is OperationCanceledException ? "Aborted." : "Failed.";
        }
        finally
        {
            DWSIM.GlobalSettings.Settings.CalculatorStopRequested = false;
            c.stats = _log.ToString();
            SelectVariable(_selectedVar);
            BtnRun.IsEnabled = true;
            BtnAbort.IsEnabled = false;
            _running = false;
        }
    }

    /// <summary>Same solver selection as the Classic/Eto optimizer.</summary>
    private void RunSolver(OptimizationCase c, List<OPTVariable> indvars)
    {
        var unbound = indvars
            .Select(v => new DotNumerics.Optimization.OptVariable(v.initialvalue))
            .ToArray();
        var bound = indvars
            .Select(v => new DotNumerics.Optimization.OptBoundVariable(v.initialvalue,
                v.lowerlimit.GetValueOrDefault(), v.upperlimit.GetValueOrDefault()))
            .ToArray();

        double Objective(double[] x) => FunctionValue(c, x);
        double[] Gradient(double[] x) => FunctionGradient(c, x);

        switch (c.solvm)
        {
            case OptimizationCase.SolvingMethod.DN_NELDERMEAD_SIMPLEX:
            case OptimizationCase.SolvingMethod.AL_BRENT:
            case OptimizationCase.SolvingMethod.IPOPT:
            {
                var s = new DotNumerics.Optimization.Simplex { Tolerance = c.tolerance, MaxFunEvaluations = c.maxits };
                s.ComputeMin(Objective, unbound);
                break;
            }
            case OptimizationCase.SolvingMethod.DN_LBFGS:
            case OptimizationCase.SolvingMethod.AL_LBFGS:
            {
                var s = new DotNumerics.Optimization.L_BFGS_B { Tolerance = c.tolerance, MaxFunEvaluations = c.maxits };
                s.ComputeMin(Objective, Gradient, unbound);
                break;
            }
            case OptimizationCase.SolvingMethod.DN_TRUNCATED_NEWTON:
            {
                var s = new DotNumerics.Optimization.TruncatedNewton { Tolerance = c.tolerance, MaxFunEvaluations = c.maxits };
                s.ComputeMin(Objective, Gradient, unbound);
                break;
            }
            case OptimizationCase.SolvingMethod.DN_NELDERMEAD_SIMPLEX_B:
            case OptimizationCase.SolvingMethod.AL_BRENT_B:
            {
                var s = new DotNumerics.Optimization.Simplex { Tolerance = c.tolerance, MaxFunEvaluations = c.maxits };
                s.ComputeMin(Objective, bound);
                break;
            }
            case OptimizationCase.SolvingMethod.DN_LBFGS_B:
            case OptimizationCase.SolvingMethod.AL_LBFGS_B:
            {
                var s = new DotNumerics.Optimization.L_BFGS_B { Tolerance = c.tolerance, MaxFunEvaluations = c.maxits };
                s.ComputeMin(Objective, Gradient, bound);
                break;
            }
            default: // DN_TRUNCATED_NEWTON_B
            {
                var s = new DotNumerics.Optimization.TruncatedNewton { Tolerance = c.tolerance, MaxFunEvaluations = c.maxits };
                s.ComputeMin(Objective, Gradient, bound);
                break;
            }
        }
    }

    private double FunctionValue(OptimizationCase c, double[] x)
    {
        if (DWSIM.GlobalSettings.Settings.CalculatorStopRequested)
            throw new OperationCanceledException();

        var indvars = c.variables.Values.Where(v => v.type == OPTVariableType.Independent).ToList();
        var depvars = c.variables.Values.Where(v => v.type == OPTVariableType.Dependent).ToList();
        var auxvars = c.variables.Values.Where(v => v.type == OPTVariableType.Auxiliary).ToList();

        for (int i = 0; i < indvars.Count && i < x.Length; i++)
        {
            var item = indvars[i];
            if (string.IsNullOrEmpty(item.objectID)) continue;
            _flowsheet.SimulationObjects[item.objectID]
                .SetPropertyValue(item.propID, cv.ConvertToSI(item.unit, x[i]));
        }

        _flowsheet.RequestCalculationAndWait();

        UpdateVariableValues(c);

        double objval;
        if (c.objfunctype == OPTObjectiveFunctionType.Expression)
        {
            var vars = new ExpressionEvaluator.VariableTable();
            foreach (var item in indvars.Concat(depvars).Concat(auxvars))
            {
                if (!string.IsNullOrEmpty(item.name))
                    vars.SetValue(item.name, item.currentvalue);
            }
            objval = ExpressionEvaluator.Compile(c.expression).Evaluate(vars) + PenaltyValue(c);
        }
        else
        {
            objval = depvars[0].currentvalue + PenaltyValue(c);
        }

        if (!_onGradient) AppendLogFromWorker($"  objective = {objval:G8}");

        return c.type == OPTType.Maximization ? -objval : objval;
    }

    private double[] FunctionGradient(OptimizationCase c, double[] x)
    {
        _onGradient = true;
        try
        {
            var g = new double[x.Length];
            var f1 = FunctionValue(c, x);
            for (int i = 0; i < x.Length; i++)
            {
                var x2 = (double[])x.Clone();
                x2[i] = Math.Abs(x[i]) < double.Epsilon ? c.epsilon : x[i] * (1 + c.epsilon);
                var f2 = FunctionValue(c, x2);
                g[i] = (f2 - f1) / (x2[i] - x[i]);
            }
            return g;
        }
        finally { _onGradient = false; }
    }

    private double PenaltyValue(OptimizationCase c)
    {
        var convars = c.variables.Values.Where(v => v.type == OPTVariableType.Constraint).ToList();
        double penval = 0.0;
        foreach (var item in convars)
        {
            var delta1 = item.currentvalue - item.lowerlimit.GetValueOrDefault();
            var delta2 = item.currentvalue - item.upperlimit.GetValueOrDefault();
            if (delta1 < 0.0) penval += -delta1 * 1e6;
            else if (delta2 > 1.0) penval += delta2 * 1e6;
            else penval += 1 / delta1 + 1 / delta2;
        }
        penval *= c.barriermultiplier;
        return double.IsNaN(penval) ? 0.0 : penval;
    }

    private void UpdateVariableValues(OptimizationCase c)
    {
        foreach (var item in c.variables.Values)
        {
            if (string.IsNullOrEmpty(item.objectID)) continue;
            if (!_flowsheet.SimulationObjects.TryGetValue(item.objectID, out var o)) continue;
            item.currentvalue = cv.ConvertFromSI(item.unit, Convert.ToDouble(o.GetPropertyValue(item.propID)));
        }
    }

    private void RestoreInitialValues()
    {
        if (_case == null) return;
        foreach (var v in _case.variables.Values.Where(x => x.type == OPTVariableType.Independent))
        {
            if (string.IsNullOrEmpty(v.objectID)) continue;
            if (!_flowsheet.SimulationObjects.TryGetValue(v.objectID, out var o)) continue;
            o.SetPropertyValue(v.propID, cv.ConvertToSI(v.unit, v.initialvalue));
        }
        TbProgress.Text = "Initial values restored. Re-solve to update results.";
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private void AppendLog(string line)
    {
        lock (_log) { _log.AppendLine(line); TbResults.Text = _log.ToString(); }
    }

    /// <summary>
    /// Appends to the log from the solver thread. The posted action re-reads the buffer rather
    /// than carrying a snapshot: a snapshot queued by the solver would otherwise land after the
    /// final results were written and roll the text box back.
    /// </summary>
    private void AppendLogFromWorker(string line)
    {
        lock (_log) _log.AppendLine(line);
        Dispatcher.UIThread.Post(() => { lock (_log) TbResults.Text = _log.ToString(); });
    }

    private ISimulationObject? GetSelectedObject(ComboBox cb)
    {
        if (cb.SelectedItem is not ObjItem item) return null;
        return _flowsheet.SimulationObjects.TryGetValue(item.InternalName, out var o) ? o : null;
    }

    private static void SelectObject(ComboBox cb, string internalName)
    {
        if (string.IsNullOrEmpty(internalName)) { cb.SelectedIndex = -1; return; }
        for (int i = 0; i < cb.Items.Count; i++)
        {
            if (cb.Items[i] is ObjItem it && it.InternalName == internalName) { cb.SelectedIndex = i; return; }
        }
        cb.SelectedIndex = -1;
    }

    private sealed class ObjItem
    {
        public string Tag { get; }
        public string InternalName { get; }
        public ObjItem(string tag, string name) => (Tag, InternalName) = (tag, name);
        public override string ToString() => Tag;
    }
}
