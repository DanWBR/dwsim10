using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums;
using DWSIM.UI.Shared.Avalonia;
using DWSIM.Thermodynamics.Polymers;
using OperationMode = DWSIM.UnitOperations.Reactors.OperationMode;
using Reactor = DWSIM.UnitOperations.Reactors.Reactor;
using Reactor_Conversion = DWSIM.UnitOperations.Reactors.Reactor_Conversion;
using Reactor_CSTR = DWSIM.UnitOperations.Reactors.Reactor_CSTR;
using Reactor_Equilibrium = DWSIM.UnitOperations.Reactors.Reactor_Equilibrium;
using Reactor_Gibbs = DWSIM.UnitOperations.Reactors.Reactor_Gibbs;
using Reactor_PFR = DWSIM.UnitOperations.Reactors.Reactor_PFR;
using Reactor_Polymerization = DWSIM.UnitOperations.Reactors.Reactor_Polymerization;
using IC = System.Globalization.CultureInfo;
using Thickness = Avalonia.Thickness;

namespace DWSIM.UI.Desktop.Editors
{

    /// <summary>
    /// Reactor editors, laid out as the Windows forms do. The conversion, equilibrium and Gibbs
    /// reactors share EditingForm_ReactorConvEqGibbs; the CSTR and the PFR have one each.
    /// </summary>
    public static class ReactorEditors
    {

        private static readonly List<string> Modes = new List<string>
        {
            "Isothermic", "Adiabatic", "Define Outlet Temperature"
        };

        private static readonly OperationMode[] ModeOrder =
        {
            OperationMode.Isothermic,
            OperationMode.Adiabatic,
            OperationMode.OutletTemperature
        };

        // ---------------------------------------------------------------------
        // Conversion, equilibrium and Gibbs
        // ---------------------------------------------------------------------

        public static Control Build(Reactor_Conversion reactor)
        {
            return BuildConvEqGibbs(reactor, tabs => { });
        }

        public static Control Build(Reactor_Equilibrium reactor)
        {
            return BuildConvEqGibbs(reactor, tabs =>
            {
                var panel = new AvaloniaEditorPanel();
                var nf = reactor.GetFlowsheet().FlowsheetOptions.NumberFormat;

                panel.CreateAndAddCheckBoxRow("Initialize from a Previous Solution",
                    reactor.UsePreviousSolution,
                    (cb, e) => reactor.UsePreviousSolution = cb.IsChecked.GetValueOrDefault());

                panel.CreateAndAddTextBoxRow(nf, "Maximum Internal Loop Iterations",
                    reactor.InternalLoopMaximumIterations,
                    (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) reactor.InternalLoopMaximumIterations = (int)v; });

                panel.CreateAndAddTextBoxRow(nf, "Maximum External Loop iterations",
                    reactor.ExternalLoopMaximumIterations,
                    (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) reactor.ExternalLoopMaximumIterations = (int)v; });

                panel.CreateAndAddTextBoxRow(nf, "Internal Loop Tolerance", reactor.InternalLoopTolerance,
                    (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) reactor.InternalLoopTolerance = v; });

                panel.CreateAndAddTextBoxRow(nf, "External Loop Tolerance", reactor.ExternalLoopTolerance,
                    (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) reactor.ExternalLoopTolerance = v; });

                tabs.Items.Add(new TabItem { Header = "Convergence", Content = panel });
            });
        }

        public static Control Build(Reactor_Gibbs reactor)
        {
            return BuildConvEqGibbs(reactor, tabs =>
            {
                tabs.Items.Add(new TabItem { Header = "Compounds", Content = BuildGibbsCompounds(reactor) });

                var elements = new AvaloniaEditorPanel();
                elements.CreateAndAddButtonRow("Edit Element Matrix", null,
                    (btn, e) => GeneralEditorsAvalonia.ShowGibbsElementMatrix(reactor));
                elements.CreateAndAddDescriptionRow(
                    "One row per element, one coefficient per reactive compound. The engine seeds " +
                    "the matrix from the compound formulas.");
                tabs.Items.Add(new TabItem { Header = "Elements", Content = elements });

                tabs.Items.Add(new TabItem { Header = "Convergence", Content = BuildGibbsConvergence(reactor) });
            });
        }

        private static Control BuildConvEqGibbs(Reactor reactor, Action<TabControl> extraTabs)
        {
            var gibbs = reactor as Reactor_Gibbs;

            return UnitOpEditor.Build(reactor,
                input: panel =>
                {
                    var tabs = new TabControl { Margin = new Thickness(0, 4, 0, 0) };

                    var parameters = new AvaloniaEditorPanel();
                    AddReactionSetRow(reactor, parameters, enabled: gibbs == null);
                    AddOperationModeRows(reactor, parameters);

                    if (gibbs != null)
                    {
                        parameters.CreateAndAddDropDownRow("Reactive Phase Behavior",
                            new List<string> { "Calculate Equilibria", "Vapor Only", "Liquid Only", "Solid Only" },
                            (int)gibbs.ReactivePhaseBehavior,
                            (dd, e) => gibbs.ReactivePhaseBehavior = (Reactor_Gibbs.ReactivePhaseType)dd.SelectedIndex);
                    }

                    tabs.Items.Add(new TabItem { Header = "Parameters", Content = parameters });
                    extraTabs(tabs);

                    panel.Children.Add(tabs);
                },
                results: panel => BuildResults(reactor, panel),
                propertyPackage: true);
        }

        private static Control BuildGibbsCompounds(Reactor_Gibbs reactor)
        {
            var panel = new AvaloniaEditorPanel();

            panel.CreateAndAddDescriptionRow(
                "Compounds taken into account by the Gibbs energy minimization. With none checked, " +
                "every compound is treated as reactive.");

            if (reactor.ComponentIDs == null) reactor.ComponentIDs = new List<string>();

            foreach (var compound in reactor.GetFlowsheet().SelectedCompounds.Values)
            {
                var name = compound.Name;
                panel.CreateAndAddCheckBoxRow(name, reactor.ComponentIDs.Contains(name), (cb, e) =>
                {
                    if (cb.IsChecked.GetValueOrDefault())
                    {
                        if (!reactor.ComponentIDs.Contains(name)) reactor.ComponentIDs.Add(name);
                    }
                    else
                    {
                        reactor.ComponentIDs.Remove(name);
                    }

                    try { reactor.CreateElementMatrix(); } catch (Exception) { }
                });
            }

            return panel;
        }

        private static Control BuildGibbsConvergence(Reactor_Gibbs reactor)
        {
            var panel = new AvaloniaEditorPanel();
            var nf = reactor.GetFlowsheet().FlowsheetOptions.NumberFormat;

            panel.CreateAndAddCheckBoxRow("Initialize from a Previous Solution",
                reactor.InitializeFromPreviousSolution,
                (cb, e) => reactor.InitializeFromPreviousSolution = cb.IsChecked.GetValueOrDefault());

            panel.CreateAndAddTextBoxRow(nf, "Maximum Iterations", reactor.MaximumInternalIterations,
                (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) reactor.MaximumInternalIterations = (int)v; });

            panel.CreateAndAddTextBoxRow(nf, "Error Tolerance", reactor.InternalTolerance,
                (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) reactor.InternalTolerance = v; });

            panel.CreateAndAddCheckBoxRow("Use IPOPT Solver", reactor.UseIPOPTSolver,
                (cb, e) => reactor.UseIPOPTSolver = cb.IsChecked.GetValueOrDefault());

            panel.CreateAndAddCheckBoxRow("Use Alternate Calculation Method", reactor.AlternateSolvingMethod,
                (cb, e) => reactor.AlternateSolvingMethod = cb.IsChecked.GetValueOrDefault());

            return panel;
        }

        // ---------------------------------------------------------------------
        // CSTR
        // ---------------------------------------------------------------------

        public static Control Build(Reactor_CSTR reactor)
        {
            return UnitOpEditor.Build(reactor,
                input: panel =>
                {
                    var nf = reactor.GetFlowsheet().FlowsheetOptions.NumberFormat;
                    var tabs = new TabControl { Margin = new Thickness(0, 4, 0, 0) };

                    var general = new AvaloniaEditorPanel();
                    AddReactionSetRow(reactor, general, enabled: true);
                    var outletT = AddOperationModeRows(reactor, general, heatExchange: true);

                    general.CreateAndAddValueUnitRow(reactor, "Reactor Volume", UnitOfMeasure.volume,
                        reactor.Volume, v => reactor.Volume = v);
                    general.CreateAndAddValueUnitRow(reactor, "Reactor Headspace", UnitOfMeasure.volume,
                        reactor.Headspace, v => reactor.Headspace = v);
                    general.CreateAndAddValueUnitRow(reactor, "Reactor Pressure Drop", UnitOfMeasure.deltaP,
                        reactor.DeltaP.GetValueOrDefault(), v => reactor.DeltaP = v);
                    general.CreateAndAddValueUnitRow(reactor, "Reactor Diameter", UnitOfMeasure.diameter,
                        reactor.Diameter, v => reactor.Diameter = v);
                    general.CreateAndAddValueUnitRow(reactor, "Catalyst Amount", UnitOfMeasure.mass,
                        reactor.CatalystAmount, v => reactor.CatalystAmount = v);

                    var solver = new AvaloniaEditorPanel();
                    solver.CreateAndAddTextBoxRow(nf, "Relative Convergence Tolerance", reactor.Tolerance,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) reactor.Tolerance = v; });
                    solver.CreateAndAddTextBoxRow(nf, "Initial Number of Time Steps",
                        reactor.InitialNumberOfTimeSteps,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) reactor.InitialNumberOfTimeSteps = (int)v; });

                    tabs.Items.Add(new TabItem { Header = "General", Content = general });
                    tabs.Items.Add(new TabItem { Header = "Heat Exchange", Content = ReactorHeatExchange.Build(reactor, stirred: true) });
                    tabs.Items.Add(new TabItem { Header = "Solver Settings", Content = solver });

                    panel.Children.Add(tabs);
                },
                results: panel =>
                {
                    BuildResults(reactor, panel);
                    panel.CreateAndAddResultRow(reactor, "Liquid residence time", UnitOfMeasure.time,
                        reactor.ResidenceTimeL);
                    panel.CreateAndAddResultRow(reactor, "Vapor residence time", UnitOfMeasure.time,
                        reactor.ResidenceTimeV);
                    AddHeatExchangeResults(reactor, panel);
                });
        }

        // ---------------------------------------------------------------------
        // Polymerization (free-radical CSTR / PFR, homo- and copolymer)
        // ---------------------------------------------------------------------

        public static Control Build(Reactor_Polymerization reactor)
        {
            void Notify(string message)
            {
                var fs = reactor.GetFlowsheet();
                if (fs != null) fs.ShowMessage(message, IFlowsheet.MessageType.Information);
            }

            return UnitOpEditor.Build(reactor,
                input: panel =>
                {
                    var nf = reactor.GetFlowsheet().FlowsheetOptions.NumberFormat;
                    var tabs = new TabControl { Margin = new Thickness(0, 4, 0, 0) };

                    var comps = reactor.GetFlowsheet().SelectedCompounds.Keys.ToList();
                    var withNone = new List<string> { "" };
                    withNone.AddRange(comps);

                    // -- General: feed roles and operation --------------------
                    var general = new AvaloniaEditorPanel();

                    general.CreateAndAddLabelRow("Feed Roles");
                    general.CreateAndAddDropDownRow("Monomer", comps, Math.Max(0, comps.IndexOf(reactor.MonomerID)),
                        (dd, e) => { if (dd.SelectedIndex >= 0) reactor.MonomerID = comps[dd.SelectedIndex]; });
                    general.CreateAndAddDropDownRow("Second monomer (copolymer; none = homopolymer)", withNone,
                        Math.Max(0, withNone.IndexOf(reactor.MonomerBID)),
                        (dd, e) => { reactor.MonomerBID = dd.SelectedIndex > 0 ? withNone[dd.SelectedIndex] : ""; });
                    general.CreateAndAddDropDownRow("Initiator", comps, Math.Max(0, comps.IndexOf(reactor.InitiatorID)),
                        (dd, e) => { if (dd.SelectedIndex >= 0) reactor.InitiatorID = comps[dd.SelectedIndex]; });
                    general.CreateAndAddDropDownRow("Solvent / chain-transfer agent (optional)", withNone,
                        Math.Max(0, withNone.IndexOf(reactor.SolventID)),
                        (dd, e) => { reactor.SolventID = dd.SelectedIndex > 0 ? withNone[dd.SelectedIndex] : ""; });
                    general.CreateAndAddDropDownRow("Polymer product", comps, Math.Max(0, comps.IndexOf(reactor.PolymerID)),
                        (dd, e) => { if (dd.SelectedIndex >= 0) reactor.PolymerID = comps[dd.SelectedIndex]; });

                    general.CreateAndAddLabelRow("Operation");
                    general.CreateAndAddDropDownRow("Flow model",
                        new List<string> { "Well-mixed (CSTR)", "Plug flow / batch (PFR)" }, reactor.PlugFlow ? 1 : 0,
                        (dd, e) => reactor.PlugFlow = dd.SelectedIndex == 1);
                    general.CreateAndAddDropDownRow("Operation mode",
                        new List<string> { "Isothermic", "Adiabatic", "Outlet Temperature" },
                        Math.Max(0, Math.Min((int)reactor.ReactorOperationMode, 2)), (dd, e) =>
                        {
                            reactor.ReactorOperationMode = dd.SelectedIndex == 1 ? OperationMode.Adiabatic
                                : dd.SelectedIndex == 2 ? OperationMode.OutletTemperature : OperationMode.Isothermic;
                        });
                    double polyTset = reactor.ReactorOperationMode == OperationMode.OutletTemperature
                        ? reactor.OutletTemperature : reactor.IsothermalTemperature;
                    general.CreateAndAddValueUnitRow(reactor, "Temperature (isothermal / outlet)",
                        UnitOfMeasure.temperature, polyTset, v =>
                        {
                            if (reactor.ReactorOperationMode == OperationMode.OutletTemperature) reactor.OutletTemperature = v;
                            else reactor.IsothermalTemperature = v;
                        });
                    general.CreateAndAddValueUnitRow(reactor, "Reactor Volume", UnitOfMeasure.volume,
                        reactor.Volume, v => reactor.Volume = v);
                    general.CreateAndAddTextBoxRow(nf, "Heat of Polymerization (J/mol)", reactor.HeatOfPolymerization,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) reactor.HeatOfPolymerization = v; });

                    // -- Kinetics: Arrhenius rate constants -------------------
                    var kinetics = new AvaloniaEditorPanel();
                    kinetics.CreateAndAddDescriptionRow("Rate constants follow k = A*exp(-E/RT).");
                    kinetics.CreateAndAddButtonRow("Load Styrene / AIBN preset", null,
                        (btn, e) => { reactor.LoadStyrenePreset(); Notify("Styrene/AIBN kinetics loaded. Re-open the editor to see the values."); });
                    kinetics.CreateAndAddButtonRow("Load Styrene / MMA copolymer preset", null,
                        (btn, e) => { reactor.LoadStyreneMMAPreset(); Notify("Styrene/MMA kinetics loaded. Re-open the editor to see the values."); });
                    kinetics.CreateAndAddTextBoxRow(nf, "Initiator efficiency f", reactor.Efficiency,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) reactor.Efficiency = v; });
                    kinetics.CreateAndAddTextBoxRow("E3", "Decomposition kd: A (1/s)", reactor.Kd_A,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) reactor.Kd_A = v; });
                    kinetics.CreateAndAddTextBoxRow(nf, "Decomposition kd: E (J/mol)", reactor.Kd_E,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) reactor.Kd_E = v; });
                    kinetics.CreateAndAddTextBoxRow("E3", "Propagation kp: A (L/mol/s)", reactor.Kp_A,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) reactor.Kp_A = v; });
                    kinetics.CreateAndAddTextBoxRow(nf, "Propagation kp: E (J/mol)", reactor.Kp_E,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) reactor.Kp_E = v; });
                    kinetics.CreateAndAddTextBoxRow("E3", "Termination (comb.) ktc: A", reactor.Ktc_A,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) reactor.Ktc_A = v; });
                    kinetics.CreateAndAddTextBoxRow(nf, "Termination (comb.) ktc: E", reactor.Ktc_E,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) reactor.Ktc_E = v; });
                    kinetics.CreateAndAddTextBoxRow("E3", "Termination (disp.) ktd: A", reactor.Ktd_A,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) reactor.Ktd_A = v; });
                    kinetics.CreateAndAddTextBoxRow(nf, "Termination (disp.) ktd: E", reactor.Ktd_E,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) reactor.Ktd_E = v; });
                    kinetics.CreateAndAddTextBoxRow("E3", "Transfer to monomer ktrM: A", reactor.KtrM_A,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) reactor.KtrM_A = v; });
                    kinetics.CreateAndAddTextBoxRow(nf, "Transfer to monomer ktrM: E", reactor.KtrM_E,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) reactor.KtrM_E = v; });
                    kinetics.CreateAndAddTextBoxRow("E3", "Transfer to solvent ktrS: A", reactor.KtrS_A,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) reactor.KtrS_A = v; });
                    kinetics.CreateAndAddTextBoxRow(nf, "Transfer to solvent ktrS: E", reactor.KtrS_E,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) reactor.KtrS_E = v; });
                    kinetics.CreateAndAddTextBoxRow(nf, "Monomer molar mass (g/mol)", reactor.MonomerMolarMass,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) reactor.MonomerMolarMass = v; });

                    // -- Copolymer: terminal-model parameters ----------------
                    var copolymer = new AvaloniaEditorPanel();
                    copolymer.CreateAndAddDescriptionRow(
                        "Terminal model. These parameters are used only when a second monomer is set.");
                    copolymer.CreateAndAddTextBoxRow(nf, "Reactivity ratio r1 (monomer A)", reactor.ReactivityRatioA,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) reactor.ReactivityRatioA = v; });
                    copolymer.CreateAndAddTextBoxRow(nf, "Reactivity ratio r2 (monomer B)", reactor.ReactivityRatioB,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) reactor.ReactivityRatioB = v; });
                    copolymer.CreateAndAddTextBoxRow("E3", "Monomer-B propagation kp: A", reactor.KpB_A,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) reactor.KpB_A = v; });
                    copolymer.CreateAndAddTextBoxRow(nf, "Monomer-B propagation kp: E", reactor.KpB_E,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) reactor.KpB_E = v; });
                    copolymer.CreateAndAddTextBoxRow("E3", "Monomer-B transfer-to-monomer ktrM: A", reactor.KtrMB_A,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) reactor.KtrMB_A = v; });
                    copolymer.CreateAndAddTextBoxRow(nf, "Monomer-B transfer-to-monomer ktrM: E", reactor.KtrMB_E,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) reactor.KtrMB_E = v; });
                    copolymer.CreateAndAddTextBoxRow(nf, "Monomer-B molar mass (g/mol)", reactor.MonomerBMolarMass,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) reactor.MonomerBMolarMass = v; });

                    // -- Gel / glass effect ----------------------------------
                    var gel = new AvaloniaEditorPanel();
                    gel.CreateAndAddDescriptionRow("g = exp(-(c1*X + c2*X^2 + c3*X^3)), applied to termination and propagation.");
                    gel.CreateAndAddDropDownRow("Model", new List<string> { "None", "Exponential" },
                        reactor.GelModel == GelModelType.Exponential ? 1 : 0,
                        (dd, e) => reactor.GelModel = dd.SelectedIndex == 1 ? GelModelType.Exponential : GelModelType.None);
                    gel.CreateAndAddTextBoxRow(nf, "Termination g_t: c1", reactor.GelGtC1,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) reactor.GelGtC1 = v; });
                    gel.CreateAndAddTextBoxRow(nf, "Termination g_t: c2", reactor.GelGtC2,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) reactor.GelGtC2 = v; });
                    gel.CreateAndAddTextBoxRow(nf, "Termination g_t: c3", reactor.GelGtC3,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) reactor.GelGtC3 = v; });
                    gel.CreateAndAddTextBoxRow(nf, "Propagation g_p: c1", reactor.GelGpC1,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) reactor.GelGpC1 = v; });
                    gel.CreateAndAddTextBoxRow(nf, "Propagation g_p: c2", reactor.GelGpC2,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) reactor.GelGpC2 = v; });
                    gel.CreateAndAddTextBoxRow(nf, "Propagation g_p: c3", reactor.GelGpC3,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) reactor.GelGpC3 = v; });

                    // -- Molecular weight distribution -----------------------
                    var mwd = new AvaloniaEditorPanel();
                    mwd.CreateAndAddDropDownRow("Emit distribution", new List<string> { "No", "Yes" },
                        reactor.EmitDistribution ? 1 : 0, (dd, e) => reactor.EmitDistribution = dd.SelectedIndex == 1);
                    mwd.CreateAndAddNumericEditorRow("Number of cuts", reactor.NumberOfCuts, 2, 30, 0,
                        (nud, e) => reactor.NumberOfCuts = (int)(nud.Value ?? 7));
                    mwd.CreateAndAddDropDownRow("Distribution type", new List<string> { "Schulz-Zimm", "Log-Normal" },
                        reactor.DistributionType == PolymerDistribution.LogNormal ? 1 : 0,
                        (dd, e) => reactor.DistributionType = dd.SelectedIndex == 1 ? PolymerDistribution.LogNormal : PolymerDistribution.SchulzZimm);
                    mwd.CreateAndAddButtonRow("Generate distribution cuts", null, (btn, e) =>
                    {
                        try { reactor.GenerateDistributionCompounds(); reactor.EmitDistribution = true; Notify("Distribution cuts generated and added to the flowsheet."); }
                        catch (Exception ex) { Notify("Generate cuts failed: " + ex.Message); }
                    });

                    tabs.Items.Add(new TabItem { Header = "General", Content = general });
                    tabs.Items.Add(new TabItem { Header = "Kinetics", Content = kinetics });
                    tabs.Items.Add(new TabItem { Header = "Copolymer", Content = copolymer });
                    tabs.Items.Add(new TabItem { Header = "Gel Effect", Content = gel });
                    tabs.Items.Add(new TabItem { Header = "Distribution", Content = mwd });

                    panel.Children.Add(tabs);
                },
                results: panel =>
                {
                    panel.CreateAndAddTwoLabelsRow("Conversion", (reactor.Conversion * 100.0).ToString("N2", IC.InvariantCulture) + " %");
                    panel.CreateAndAddTwoLabelsRow("Number-average molar mass Mn", reactor.Mn.ToString("N0", IC.InvariantCulture) + " g/mol");
                    panel.CreateAndAddTwoLabelsRow("Weight-average molar mass Mw", reactor.Mw.ToString("N0", IC.InvariantCulture) + " g/mol");
                    panel.CreateAndAddTwoLabelsRow("Polydispersity (Mw/Mn)", reactor.PDI.ToString("N3", IC.InvariantCulture));
                    if (reactor.IsCopolymer())
                        panel.CreateAndAddTwoLabelsRow("Copolymer composition F1 (monomer A)",
                            reactor.CopolymerCompositionA.ToString("N4", IC.InvariantCulture));
                });
        }

        // ---------------------------------------------------------------------
        // PFR
        // ---------------------------------------------------------------------

        private static readonly List<string> InternalSolvers = new List<string>
        {
            "Implicit Runge-Kutta", "Explicit Runge-Kutta", "Adams-Moulton", "Gear's BDF", "OSLO RK45"
        };

        public static Control Build(Reactor_PFR reactor)
        {
            return UnitOpEditor.Build(reactor,
                input: panel =>
                {
                    var nf = reactor.GetFlowsheet().FlowsheetOptions.NumberFormat;
                    var tabs = new TabControl { Margin = new Thickness(0, 4, 0, 0) };

                    var general = new AvaloniaEditorPanel();
                    AddReactionSetRow(reactor, general, enabled: true);
                    AddOperationModeRows(reactor, general, nonAdiabatic: true, heatExchange: true);

                    general.CreateAndAddDropDownRow("Internal Solver", InternalSolvers,
                        Math.Max(0, reactor.InternalSolver), (dd, e) =>
                        {
                            if (dd.SelectedIndex < 0) return;
                            reactor.InternalSolver = dd.SelectedIndex;
                        });

                    var dimensions = new AvaloniaEditorPanel();
                    dimensions.CreateAndAddValueUnitRow(reactor, "Reactive Volume", UnitOfMeasure.volume,
                        reactor.Volume, v => reactor.Volume = v);

                    UnitOpEditorRows.ValueRow length = null, diameter = null;

                    void ApplySizing()
                    {
                        var byLength = reactor.ReactorSizingType == Reactor_PFR.SizingType.Length;
                        if (length != null) length.IsEnabled = byLength;
                        if (diameter != null) diameter.IsEnabled = !byLength;
                    }

                    dimensions.CreateAndAddDropDownRow("Sizing Information",
                        new List<string> { "Length", "Diameter" },
                        reactor.ReactorSizingType == Reactor_PFR.SizingType.Length ? 0 : 1, (dd, e) =>
                        {
                            reactor.ReactorSizingType = dd.SelectedIndex == 0
                                ? Reactor_PFR.SizingType.Length
                                : Reactor_PFR.SizingType.Diameter;
                            ApplySizing();
                        });

                    length = dimensions.CreateAndAddValueUnitRow(reactor, "Tube Length",
                        UnitOfMeasure.distance, reactor.Length, v => reactor.Length = v);
                    diameter = dimensions.CreateAndAddValueUnitRow(reactor, "Tube Diameter",
                        UnitOfMeasure.diameter, reactor.Diameter, v => reactor.Diameter = v);

                    dimensions.CreateAndAddTextBoxRow(nf, "Number of Tubes", reactor.NumberOfTubes,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) reactor.NumberOfTubes = (int)v; });

                    ApplySizing();

                    var catalyst = new AvaloniaEditorPanel();
                    catalyst.CreateAndAddDescriptionRow(
                        "Catalyst Loading information are required when dealing with Heterogeneous " +
                        "Catalytic Reactions.");
                    catalyst.CreateAndAddValueUnitRow(reactor, "Catalyst Loading", UnitOfMeasure.density,
                        reactor.CatalystLoading, v => reactor.CatalystLoading = v);
                    catalyst.CreateAndAddValueUnitRow(reactor, "Catalyst Particle Diameter",
                        UnitOfMeasure.diameter, reactor.CatalystParticleDiameter,
                        v => reactor.CatalystParticleDiameter = v);
                    catalyst.CreateAndAddTextBoxRow(nf, "Catalyst Void Fraction", reactor.CatalystVoidFraction,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) reactor.CatalystVoidFraction = v; });
                    catalyst.CreateAndAddTextBoxRow(nf, "Catalyst Particle Sphericity",
                        reactor.CatalystParticleSphericity,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) reactor.CatalystParticleSphericity = v; });

                    var advanced = new AvaloniaEditorPanel();
                    advanced.CreateAndAddTextBoxRow(nf, "ODE Solver Volume Step (0.00 to 1.00)", reactor.dV,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) reactor.dV = v; });
                    advanced.CreateAndAddCheckBoxRow("Constant Linear Pressure Drop",
                        reactor.UseUserDefinedPressureDrop,
                        (cb, e) => reactor.UseUserDefinedPressureDrop = cb.IsChecked.GetValueOrDefault());
                    advanced.CreateAndAddValueUnitRow(reactor, "Pressure Drop", UnitOfMeasure.deltaP,
                        reactor.UserDefinedPressureDrop, v => reactor.UserDefinedPressureDrop = v);
                    advanced.CreateAndAddDropDownRow("Slurry Viscosity",
                        new List<string> { "Disabled", "Yoshida et al" },
                        reactor.SlurryViscosityMode, (dd, e) =>
                        {
                            if (dd.SelectedIndex < 0) return;
                            reactor.SlurryViscosityMode = dd.SelectedIndex;
                        });

                    tabs.Items.Add(new TabItem { Header = "General", Content = general });
                    tabs.Items.Add(new TabItem { Header = "Dimensions", Content = dimensions });
                    tabs.Items.Add(new TabItem { Header = "Catalyst Info", Content = catalyst });
                    tabs.Items.Add(new TabItem { Header = "Advanced", Content = advanced });
                    tabs.Items.Add(new TabItem { Header = "Heat Exchange", Content = ReactorHeatExchange.Build(reactor, stirred: false) });

                    panel.Children.Add(tabs);
                },
                results: panel =>
                {
                    BuildResults(reactor, panel);
                    panel.CreateAndAddResultRow(reactor, "Residence Time", UnitOfMeasure.time,
                        reactor.ResidenceTime);
                    AddHeatExchangeResults(reactor, panel);
                    panel.CreateAndAddResultRow(reactor, "Pressure Drop", UnitOfMeasure.deltaP,
                        reactor.DeltaP.GetValueOrDefault());
                });
        }

        // ---------------------------------------------------------------------
        // Shared rows
        // ---------------------------------------------------------------------

        private static void AddReactionSetRow(Reactor reactor, AvaloniaEditorPanel panel, bool enabled)
        {
            var flowsheet = reactor.GetFlowsheet();
            var sets = flowsheet.ReactionSets.Values.ToList();
            if (sets.Count == 0) return;

            var names = sets.Select(x => x.Name).ToList();
            var selected = sets.FindIndex(x => x.ID == reactor.ReactionSetID);

            var picker = panel.CreateAndAddDropDownRow("Reaction Set", names, Math.Max(0, selected),
                (dd, e) =>
                {
                    if (dd.SelectedIndex < 0 || dd.SelectedIndex >= sets.Count) return;
                    reactor.ReactionSetID = sets[dd.SelectedIndex].ID;
                    panel.OnAfterEdit?.Invoke();
                });

            // the Gibbs reactor takes every compound into account, not a reaction set
            picker.IsEnabled = enabled;
        }

        /// <summary>
        /// The calculation mode and the outlet temperature it governs. The extra modes the CSTR
        /// and the PFR carry are appended in the order their Windows forms add them.
        /// </summary>
        private static UnitOpEditorRows.ValueRow AddOperationModeRows(Reactor reactor,
            AvaloniaEditorPanel panel, bool nonAdiabatic = false, bool heatExchange = false)
        {
            var modes = new List<string>(Modes);
            var order = new List<OperationMode>(ModeOrder);

            if (nonAdiabatic)
            {
                modes.Add("Non-Adiabatic Non-Isothermal");
                order.Add(OperationMode.NonIsothermalNonAdiabatic);
            }

            if (heatExchange)
            {
                modes.Add("Heat Exchange");
                order.Add(OperationMode.HeatExchange);
            }

            UnitOpEditorRows.ValueRow outletT = null;

            void Apply()
            {
                if (outletT != null)
                    outletT.IsEnabled = reactor.ReactorOperationMode == OperationMode.OutletTemperature;
            }

            panel.CreateAndAddDropDownRow("Calculation Mode", modes,
                Math.Max(0, order.IndexOf(reactor.ReactorOperationMode)), (dd, e) =>
                {
                    if (dd.SelectedIndex < 0 || dd.SelectedIndex >= order.Count) return;
                    reactor.ReactorOperationMode = order[dd.SelectedIndex];
                    Apply();
                    panel.OnAfterEdit?.Invoke();
                });

            outletT = panel.CreateAndAddValueUnitRow(reactor, "Outlet Temperature",
                UnitOfMeasure.temperature, reactor.OutletTemperature, v => reactor.OutletTemperature = v);

            panel.CreateAndAddValueUnitRow(reactor, "Pressure Drop", UnitOfMeasure.deltaP,
                reactor.DeltaP.GetValueOrDefault(), v => reactor.DeltaP = v);

            Apply();
            return outletT;
        }

        // ---------------------------------------------------------------------
        // Shared results
        // ---------------------------------------------------------------------

        private static void BuildResults(Reactor reactor, AvaloniaEditorPanel panel)
        {
            var nf = reactor.GetFlowsheet().FlowsheetOptions.NumberFormat;

            panel.CreateAndAddResultRow(reactor, "Temperature Difference", UnitOfMeasure.deltaT,
                reactor.DeltaT.GetValueOrDefault());
            panel.CreateAndAddResultRow(reactor, "Heat Load", UnitOfMeasure.heatflow,
                reactor.DeltaQ.GetValueOrDefault());

            if (reactor is Reactor_Gibbs gibbs)
            {
                panel.CreateAndAddResultRow(reactor, "Initial Gibbs Free Energy", UnitOfMeasure.heatflow,
                    gibbs.InitialGibbsEnergy);
                panel.CreateAndAddResultRow(reactor, "Final Gibbs Free Energy", UnitOfMeasure.heatflow,
                    gibbs.FinalGibbsEnergy);
                panel.CreateAndAddTwoLabelsRow("Mass Balance (Elements)", gibbs.ElementBalance.ToString("E"));
            }

            if (reactor is Reactor_Equilibrium equilibrium)
            {
                panel.CreateAndAddResultRow(reactor, "Initial Gibbs Free Energy", UnitOfMeasure.heatflow,
                    equilibrium.InitialGibbsEnergy);
                panel.CreateAndAddResultRow(reactor, "Final Gibbs Free Energy", UnitOfMeasure.heatflow,
                    equilibrium.FinalGibbsEnergy);
            }

            // per-compound conversions, as the Windows Conversions grid lists them
            var conversions = reactor.ComponentConversions;
            if (conversions == null) return;

            var listed = false;

            foreach (var item in conversions)
            {
                if (item.Value <= 0.0 || double.IsInfinity(item.Value) || double.IsNaN(item.Value)) continue;

                if (!listed)
                {
                    panel.CreateAndAddLabelRow("Conversions");
                    listed = true;
                }

                panel.CreateAndAddTwoLabelsRow(item.Key, (item.Value * 100).ToString(nf) + " %");
            }
        }

        private static void AddHeatExchangeResults(Reactor reactor, AvaloniaEditorPanel panel)
        {
            if (reactor.ReactorOperationMode != OperationMode.HeatExchange) return;

            panel.CreateAndAddResultRow(reactor, "Coolant Outlet Temperature", UnitOfMeasure.temperature,
                reactor.CoolantOutletTemperature);
            panel.CreateAndAddResultRow(reactor, "Heat Transfer Area", UnitOfMeasure.area,
                reactor.CalculatedHeatExchangeArea);

            if (reactor.UseWallProperties)
                panel.CreateAndAddResultRow(reactor, "Calculated Overall HTC",
                    UnitOfMeasure.heat_transf_coeff, reactor.CalculatedOverallHTC);
        }

    }

}
