using System;
using System.Globalization;
using System.Reflection;
using Avalonia.Controls;
using DWSIM.Interfaces;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// Minimal Avalonia compound creator. Builds a ConstantProperties instance via reflection
/// (the concrete type lives in DWSIM.Thermodynamics, which is loaded lazily by the engine
/// resolver) and registers it in IFlowsheet.AvailableCompounds. Once present, the user
/// can pick it from the Simulation Settings dialog like any other compound.
///
/// This is intentionally a thin first cut: just the essential constants needed by PR/SRK
/// and a few helpers. Joback estimation, NIST/KDB import and DIPPR coefficients are
/// follow-up scope.
/// </summary>
public partial class CompoundCreatorWindow : Window
{
    private readonly IFlowsheet _flowsheet;

    // Parameterless ctor required by the Avalonia XAML compiler.
    public CompoundCreatorWindow() : this(null!) { }

    public CompoundCreatorWindow(IFlowsheet flowsheet)
    {
        _flowsheet = flowsheet!;
        InitializeComponent();
        IconHelper.ApplyWindowIcon(this);
        if (flowsheet == null) return;

        BtnAdd.Click    += (_, _) => TryAdd();
        BtnCancel.Click += (_, _) => Close();
    }

    private void TryAdd()
    {
        var name = TbName.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(name))
        {
            LblStatus.Text = "Name is required.";
            return;
        }
        if (_flowsheet.AvailableCompounds.ContainsKey(name))
        {
            LblStatus.Text = $"A compound called '{name}' already exists in the database.";
            return;
        }

        if (!TryParse(TbMW.Text, out var mw) || mw <= 0) { LblStatus.Text = "Molecular Weight must be > 0."; return; }
        if (!TryParse(TbTc.Text, out var tc) || tc <= 0) { LblStatus.Text = "Critical Temperature must be > 0."; return; }
        if (!TryParse(TbPc.Text, out var pc) || pc <= 0) { LblStatus.Text = "Critical Pressure must be > 0."; return; }
        if (!TryParse(TbOmega.Text, out var omega)) { LblStatus.Text = "Acentric Factor must be numeric."; return; }
        TryParse(TbVc.Text,   out var vc);
        TryParse(TbTb.Text,   out var tb);
        TryParse(TbRhoL.Text, out var rho);
        TryParse(TbHform.Text, out var hform);

        ICompoundConstantProperties? compound = TryCreateConstantProperties();
        if (compound == null)
        {
            LblStatus.Text = "Could not instantiate ConstantProperties type. The engine assembly may not be loaded yet.";
            return;
        }

        compound.Name = name;
        compound.CAS_Number = TbCAS.Text ?? "";
        compound.Formula    = TbFormula.Text ?? "";
        compound.SMILES     = TbSmiles.Text ?? "";
        compound.Molar_Weight        = mw;
        compound.Critical_Temperature = tc;
        compound.Critical_Pressure   = pc;
        compound.Critical_Volume     = vc;
        compound.Acentric_Factor     = omega;

        // Set "Normal_Boiling_Point" and others reflectively because they are not all on the
        // base interface (only on the concrete ConstantProperties class).
        SetIfWritable(compound, "Normal_Boiling_Point", tb);
        SetIfWritable(compound, "Standard_Density",     rho);
        SetIfWritable(compound, "IG_Enthalpy_of_Formation_25C", hform);
        SetIfWritable(compound, "OriginalDB", "Avalonia Creator");
        SetIfWritable(compound, "CurrentDB",  "Avalonia Creator");

        try
        {
            _flowsheet.AvailableCompounds[name] = compound;
            LblStatus.Text = $"'{name}' added to AvailableCompounds. Open Simulation Settings → Compounds to attach it.";
        }
        catch (Exception ex)
        {
            LblStatus.Text = "Failed to register: " + ex.Message;
        }
    }

    private static bool TryParse(string? text, out double value)
    {
        if (!double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out value))
        {
            value = 0;
            return false;
        }
        return true;
    }

    /// <summary>
    /// Looks up DWSIM.Thermodynamics.BaseClasses.ConstantProperties via reflection — that's
    /// the concrete type implementing ICompoundConstantProperties that every database loads.
    /// </summary>
    private static ICompoundConstantProperties? TryCreateConstantProperties()
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType("DWSIM.Thermodynamics.BaseClasses.ConstantProperties");
            if (t != null && typeof(ICompoundConstantProperties).IsAssignableFrom(t))
                return (ICompoundConstantProperties)Activator.CreateInstance(t)!;
        }
        // Force-load DWSIM.Thermodynamics through the assembly resolver and retry.
        try
        {
            var asm = Assembly.Load("DWSIM.Thermodynamics");
            var t = asm.GetType("DWSIM.Thermodynamics.BaseClasses.ConstantProperties");
            if (t != null) return (ICompoundConstantProperties)Activator.CreateInstance(t)!;
        }
        catch { }
        return null;
    }

    private static void SetIfWritable(object target, string propName, object? value)
    {
        var p = target.GetType().GetProperty(propName);
        if (p == null || !p.CanWrite) return;
        try { p.SetValue(target, Convert.ChangeType(value, p.PropertyType, CultureInfo.InvariantCulture)); }
        catch { /* best-effort: ignore type-mismatch */ }
    }
}
