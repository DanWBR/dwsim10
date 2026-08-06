using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Avalonia.Controls;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums.GraphicObjects;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// Closed-system mass and energy balance summary across the flowsheet boundary.
///
/// "Inlet" stream  = MaterialStream / EnergyStream whose InputConnector  is not attached
/// "Outlet" stream = MaterialStream / EnergyStream whose OutputConnector is not attached
///
/// The report shows totals in SI, totals in the active unit system, the absolute
/// difference, and the relative residual (% of total inflow). It is purely diagnostic
/// — it does NOT enforce closure or modify the simulation.
/// </summary>
public partial class BalanceSummaryWindow : Window
{
    private readonly IFlowsheet _flowsheet;

    // Parameterless ctor required by Avalonia XAML compiler (designer-only).
    public BalanceSummaryWindow() : this(null!) { }

    public BalanceSummaryWindow(IFlowsheet flowsheet)
    {
        _flowsheet = flowsheet!;
        InitializeComponent();
        IconHelper.ApplyWindowIcon(this);
        if (flowsheet == null) return;

        BtnRefresh.Click += (_, _) => Render();
        BtnCopy.Click    += async (_, _) =>
        {
            var top = GetTopLevel(this);
            if (top?.Clipboard != null) await top.Clipboard.SetTextAsync(ReportBox.Text ?? "");
        };
        Render();
    }

    private void Render()
    {
        if (_flowsheet == null) return;

        var su = _flowsheet.FlowsheetOptions.SelectedUnitSystem;
        var sb = new StringBuilder();
        var nf = "G6";

        var materialIn  = new List<IMaterialStream>();
        var materialOut = new List<IMaterialStream>();
        var energyIn    = new List<IEnergyStream>();
        var energyOut   = new List<IEnergyStream>();

        foreach (var obj in _flowsheet.SimulationObjects.Values)
        {
            var go = obj.GraphicObject;
            if (go == null) continue;

            if (obj is IMaterialStream ms)
            {
                bool feedsCanvas = HasOutletAttached(go);
                bool acceptsFromCanvas = HasInletAttached(go);
                if (!acceptsFromCanvas && feedsCanvas) materialIn.Add(ms);
                else if (acceptsFromCanvas && !feedsCanvas) materialOut.Add(ms);
            }
            else if (obj is IEnergyStream es)
            {
                bool feedsCanvas = HasOutletAttached(go);
                bool acceptsFromCanvas = HasInletAttached(go);
                if (!acceptsFromCanvas && feedsCanvas) energyIn.Add(es);
                else if (acceptsFromCanvas && !feedsCanvas) energyOut.Add(es);
            }
        }

        sb.AppendLine("MASS BALANCE");
        sb.AppendLine(new string('-', 60));
        double massInSI  = materialIn.Sum(s => SafeGet(() => s.GetMassFlow()));
        double massOutSI = materialOut.Sum(s => SafeGet(() => s.GetMassFlow()));
        sb.AppendLine($"  Inlet streams  ({materialIn.Count}):");
        foreach (var s in materialIn)
            sb.AppendLine($"    {((ISimulationObject)s).GraphicObject.Tag,-30}  {ToUserUnit(s, su.massflow):G6} {su.massflow}");
        sb.AppendLine($"  Outlet streams ({materialOut.Count}):");
        foreach (var s in materialOut)
            sb.AppendLine($"    {((ISimulationObject)s).GraphicObject.Tag,-30}  {ToUserUnit(s, su.massflow):G6} {su.massflow}");
        sb.AppendLine();
        sb.AppendLine($"  Total inlet  : {massInSI:G6} kg/s   ({DWSIM.SharedClasses.SystemsOfUnits.Converter.ConvertFromSI(su.massflow, massInSI):G6} {su.massflow})");
        sb.AppendLine($"  Total outlet : {massOutSI:G6} kg/s   ({DWSIM.SharedClasses.SystemsOfUnits.Converter.ConvertFromSI(su.massflow, massOutSI):G6} {su.massflow})");
        double massDelta = massOutSI - massInSI;
        double massResid = massInSI != 0 ? Math.Abs(massDelta / massInSI) * 100.0 : 0.0;
        sb.AppendLine($"  Difference   : {massDelta:G6} kg/s   ({massResid:F4} % of inflow)");
        sb.AppendLine();

        sb.AppendLine("ENERGY BALANCE");
        sb.AppendLine(new string('-', 60));

        // For energy: material streams carry enthalpy (kJ/kg * massflow = kW). Energy streams
        // carry duty directly (kW).
        double matEnergyIn  = materialIn.Sum(s => StreamPowerKW(s));
        double matEnergyOut = materialOut.Sum(s => StreamPowerKW(s));
        double esEnergyIn   = energyIn.Sum(s => SafeGet(() => s.GetEnergyFlow()));
        double esEnergyOut  = energyOut.Sum(s => SafeGet(() => s.GetEnergyFlow()));

        sb.AppendLine($"  Material streams in  : {matEnergyIn:G6} kW");
        sb.AppendLine($"  Material streams out : {matEnergyOut:G6} kW");
        sb.AppendLine($"  Energy streams in    : {esEnergyIn:G6} kW   (boundary energy inflows)");
        sb.AppendLine($"  Energy streams out   : {esEnergyOut:G6} kW   (boundary energy outflows)");
        sb.AppendLine();

        double totalIn  = matEnergyIn + esEnergyIn;
        double totalOut = matEnergyOut + esEnergyOut;
        sb.AppendLine($"  Total power in  : {totalIn:G6} kW");
        sb.AppendLine($"  Total power out : {totalOut:G6} kW");
        double eDelta = totalOut - totalIn;
        double eResid = totalIn != 0 ? Math.Abs(eDelta / totalIn) * 100.0 : 0.0;
        sb.AppendLine($"  Difference      : {eDelta:G6} kW   ({eResid:F4} % of inflow)");
        sb.AppendLine();

        sb.AppendLine("(A non-zero residual usually means a recycle hasn't converged yet,");
        sb.AppendLine(" or an inlet/outlet stream isn't recognized as a system boundary.)");

        ReportBox.Text = sb.ToString();
    }

    private static bool HasInletAttached(IGraphicObject go)
    {
        foreach (var cp in go.InputConnectors) if (cp.IsAttached) return true;
        return false;
    }
    private static bool HasOutletAttached(IGraphicObject go)
    {
        foreach (var cp in go.OutputConnectors) if (cp.IsAttached) return true;
        return false;
    }

    private static double SafeGet(Func<double> getter)
    {
        try { return getter(); } catch { return 0.0; }
    }

    private static double ToUserUnit(IMaterialStream s, string unit)
    {
        try { return DWSIM.SharedClasses.SystemsOfUnits.Converter.ConvertFromSI(unit, s.GetMassFlow()); }
        catch { return 0.0; }
    }

    /// <summary>
    /// Returns the absolute power carried by a material stream in kW. DWSIM's
    /// IMaterialStream.GetEnergyFlow already does mass-flow * specific-enthalpy in SI.
    /// </summary>
    private static double StreamPowerKW(IMaterialStream s)
    {
        try { return s.GetEnergyFlow(); }
        catch { return 0.0; }
    }
}
