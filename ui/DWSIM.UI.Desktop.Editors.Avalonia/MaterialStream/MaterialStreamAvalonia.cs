using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Media;
using DWSIM.ExtensionMethods;
using DWSIM.Interfaces.Enums;
using DWSIM.Interfaces.Enums.GraphicObjects;
using DWSIM.Thermodynamics.PropertyPackages;
using DWSIM.Thermodynamics.Streams;
using DWSIM.UI.Shared.Avalonia;
using cv = DWSIM.SharedClasses.SystemsOfUnits.Converter;
using StringResources = DWSIM.UI.Shared.Avalonia.StringArrays;

namespace DWSIM.UI.Desktop.Editors
{
    internal static class MaterialStreamEditorAvalonia
    {
        private static readonly NumberStyles NS = NumberStyles.Any;
        private static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        private static bool TryVal(string text, out double v) => double.TryParse(text, NS, IC, out v);

        private static readonly SolidColorBrush UserDefinedBrush =
            new SolidColorBrush(Avalonia.Media.Color.FromRgb(173, 216, 230)); // LightBlue

        internal static void Populate(MaterialStream ms, AvaloniaEditorPanel panel)
        {
            Populate(ms, panel, includeComposition: true);
        }

        /// <summary>
        /// Whether the stream is written by the object feeding it. A recycle does not count: its
        /// outlet is the tear the user seeds. In dynamic mode the conditions are live again.
        /// </summary>
        internal static bool IsDrivenUpstream(MaterialStream ms)
        {
            if (ms.GetFlowsheet().DynamicMode) return false;

            var connectors = ms.GraphicObject.InputConnectors;
            if (connectors.Count == 0 || !connectors[0].IsAttached) return false;

            return connectors[0].AttachedConnector.AttachedFrom.ObjectType != ObjectType.OT_Recycle;
        }

        /// <summary>
        /// The tabbed editor puts the composition in a grid of its own, so it asks for the
        /// conditions only.
        /// </summary>
        internal static void Populate(MaterialStream ms, AvaloniaEditorPanel panel, bool includeComposition,
                                      bool includeHeader = true)
        {
            var su = ms.GetFlowsheet().FlowsheetOptions.SelectedUnitSystem;
            var nf = ms.GetFlowsheet().FlowsheetOptions.NumberFormat;
            var nff = ms.GetFlowsheet().FlowsheetOptions.FractionNumberFormat;

            if (includeHeader)
            {
                panel.CreateAndAddLabelRow("Material Stream Property Editor");
                panel.CreateAndAddDescriptionRow("Except for compound amounts, property values are updated/stored as they are changed/edited.");

                panel.CreateAndAddLabelRow("Material Stream Details");
                panel.CreateAndAddTwoLabelsRow("Status", ms.GraphicObject.Active ? "Active" : "Inactive");
                panel.CreateAndAddStringEditorRow("Name", ms.GraphicObject.Tag, (tb, e) =>
                {
                    ms.GraphicObject.Tag = tb.Text;
                    ms.GetFlowsheet().UpdateInterface();
                });

                panel.CreateAndAddDropDownRow("Compound Amount Basis",
                    new List<string> { "Molar Fractions", "Mass Fractions", "Volumetric Fractions", "Molar Flows", "Mass Flows", "Volumetric Flows", "Default" },
                    (int)ms.FloatingTableAmountBasis,
                    (dd, e) => ms.FloatingTableAmountBasis = (CompositionBasis)dd.SelectedIndex);

                var proppacks = ms.GetFlowsheet().PropertyPackages.Values.Select(x => x.Tag).ToList();
                if (proppacks.Count > 0)
                {
                    panel.CreateAndAddLabelRow("Property Package");
                    var selPP = ms.PropertyPackage?.Tag ?? "";
                    panel.CreateAndAddDropDownRow("Property Package", proppacks, proppacks.IndexOf(selPP), (dd, e) =>
                    {
                        var tag = dd.SelectedItem?.ToString();
                        ms.PropertyPackage = (DWSIM.Thermodynamics.PropertyPackages.PropertyPackage)
                            ms.GetFlowsheet().PropertyPackages.Values.FirstOrDefault(x => x.Tag == tag);
                    });
                }
            }

            panel.CreateAndAddLabelRow("State Specification");

            var specModes = new[] {
                StreamSpec.Temperature_and_Pressure,
                StreamSpec.Temperature_and_VaporFraction,
                StreamSpec.Pressure_and_VaporFraction,
                StreamSpec.Pressure_and_Enthalpy,
                StreamSpec.Pressure_and_Entropy
            };
            int specPos = Math.Max(0, Array.IndexOf(specModes, ms.SpecType));

            TextBox tbT = null, tbP = null, tbH = null, tbS = null, tbVF = null;

            // only the two variables the flash specification names are read; the others come out
            // of the calculation, so the Windows editor greys them out
            void ApplySpec()
            {
                var spec = ms.SpecType;

                if (tbT != null)
                    tbT.IsEnabled = spec == StreamSpec.Temperature_and_Pressure
                                 || spec == StreamSpec.Temperature_and_VaporFraction;
                if (tbP != null)
                    tbP.IsEnabled = spec != StreamSpec.Temperature_and_VaporFraction;
                if (tbH != null)
                    tbH.IsEnabled = spec == StreamSpec.Pressure_and_Enthalpy;
                if (tbS != null)
                    tbS.IsEnabled = spec == StreamSpec.Pressure_and_Entropy;
                if (tbVF != null)
                    tbVF.IsEnabled = spec == StreamSpec.Temperature_and_VaporFraction
                                  || spec == StreamSpec.Pressure_and_VaporFraction;
            }

            panel.CreateAndAddDropDownRow("Specified Variables", StringResources.flash_spec().ToList(), specPos,
                (dd, e) =>
                {
                    if (dd.SelectedIndex < 0 || dd.SelectedIndex >= specModes.Length) return;
                    ms.SpecType = specModes[dd.SelectedIndex];
                    ApplySpec();
                });

            tbT = panel.CreateAndAddTextBoxRow(nf, "Temperature (" + su.temperature + ")",
                cv.ConvertFromSI(su.temperature, ms.Phases[0].Properties.temperature.GetValueOrDefault()),
                (tb, e) => { if (TryVal(tb.Text, out var v)) ms.Phases[0].Properties.temperature = cv.ConvertToSI(su.temperature, v); });

            tbP = panel.CreateAndAddTextBoxRow(nf, "Pressure (" + su.pressure + ")",
                cv.ConvertFromSI(su.pressure, ms.Phases[0].Properties.pressure.GetValueOrDefault()),
                (tb, e) => { if (TryVal(tb.Text, out var v)) ms.Phases[0].Properties.pressure = cv.ConvertToSI(su.pressure, v); });

            tbH = panel.CreateAndAddTextBoxRow(nf, "Specific Enthalpy (" + su.enthalpy + ")",
                cv.ConvertFromSI(su.enthalpy, ms.Phases[0].Properties.enthalpy.GetValueOrDefault()),
                (tb, e) => { if (TryVal(tb.Text, out var v)) ms.Phases[0].Properties.enthalpy = cv.ConvertToSI(su.enthalpy, v); });

            tbS = panel.CreateAndAddTextBoxRow(nf, "Specific Entropy (" + su.entropy + ")",
                cv.ConvertFromSI(su.entropy, ms.Phases[0].Properties.entropy.GetValueOrDefault()),
                (tb, e) => { if (TryVal(tb.Text, out var v)) ms.Phases[0].Properties.entropy = cv.ConvertToSI(su.entropy, v); });

            tbVF = panel.CreateAndAddTextBoxRow(nf, "Vapor Mole Frac (spec)", ms.Phases[2].Properties.molarfraction.GetValueOrDefault(),
                (tb, e) => { if (TryVal(tb.Text, out var v)) ms.Phases[2].Properties.molarfraction = v; });

            ApplySpec();

            var forcePhaseModes = new[] { ForcedPhase.GlobalDef, ForcedPhase.Vapor, ForcedPhase.Liquid, ForcedPhase.Solid };
            int fpPos = Math.Max(0, Array.IndexOf(forcePhaseModes, ms.ForcePhase));
            panel.CreateAndAddDropDownRow("Force Phase",
                new List<string> { "Global Definition", "Vapor", "Liquid", "Solid" }, fpPos,
                (dd, e) => { if (dd.SelectedIndex >= 0 && dd.SelectedIndex < forcePhaseModes.Length) ms.ForcePhase = forcePhaseModes[dd.SelectedIndex]; });

            panel.CreateAndAddCheckBoxRow("Enable Phase Envelope Lookup", ms.GeneratePhaseEnvelopeLookup,
                (cb, e) => ms.GeneratePhaseEnvelopeLookup = cb.IsChecked.GetValueOrDefault());

            panel.CreateAndAddDropDownRow("Lookup Table Type",
                new List<string> { "Critical Point + Widom Lines", "Full" }, (int)ms.PhaseEnvelopeLookupMode,
                (dd, e) => ms.PhaseEnvelopeLookupMode = (PhaseEnvelopeLookupMode)dd.SelectedIndex);

            panel.CreateAndAddDescriptionRow("Phase envelope lookup generates tables for phase identification near the critical point; may be slow.");

            panel.CreateAndAddLabelRow("Flow Specification");

            var txtW = panel.CreateAndAddTextBoxRow(nf, "Mass Flow (" + su.massflow + ")",
                cv.ConvertFromSI(su.massflow, ms.Phases[0].Properties.massflow.GetValueOrDefault()),
                (tb, e) =>
                {
                    if (TryVal(tb.Text, out var v))
                    {
                        ms.Phases[0].Properties.volumetric_flow = null;
                        ms.Phases[0].Properties.molarflow = null;
                        ms.Phases[0].Properties.massflow = cv.ConvertToSI(su.massflow, v);
                        ms.DefinedFlow = FlowSpec.Mass;
                    }
                });

            var txtQ = panel.CreateAndAddTextBoxRow(nf, "Molar Flow (" + su.molarflow + ")",
                cv.ConvertFromSI(su.molarflow, ms.Phases[0].Properties.molarflow.GetValueOrDefault()),
                (tb, e) =>
                {
                    if (TryVal(tb.Text, out var v))
                    {
                        ms.Phases[0].Properties.massflow = null;
                        ms.Phases[0].Properties.volumetric_flow = null;
                        ms.Phases[0].Properties.molarflow = cv.ConvertToSI(su.molarflow, v);
                        ms.DefinedFlow = FlowSpec.Mole;
                    }
                });

            var txtV = panel.CreateAndAddTextBoxRow(nf, "Volumetric Flow (" + su.volumetricFlow + ")",
                cv.ConvertFromSI(su.volumetricFlow, ms.Phases[0].Properties.volumetric_flow.GetValueOrDefault()),
                (tb, e) =>
                {
                    if (TryVal(tb.Text, out var v))
                    {
                        ms.Phases[0].Properties.massflow = null;
                        ms.Phases[0].Properties.molarflow = null;
                        ms.Phases[0].Properties.volumetric_flow = cv.ConvertToSI(su.volumetricFlow, v);
                        ms.DefinedFlow = FlowSpec.Volumetric;
                    }
                });

            switch (ms.DefinedFlow)
            {
                case FlowSpec.Mass: txtW.Background = UserDefinedBrush; break;
                case FlowSpec.Mole: txtQ.Background = UserDefinedBrush; break;
                case FlowSpec.Volumetric: txtV.Background = UserDefinedBrush; break;
            }

            // a stream fed by a unit operation carries whatever that operation wrote into it, so
            // nothing here is editable. A recycle is the exception: its stream is still a tear
            // the user seeds, and in dynamic mode everything is live again
            if (IsDrivenUpstream(ms))
            {
                foreach (var box in new[] { tbT, tbP, tbH, tbS, tbVF, txtW, txtQ, txtV })
                    if (box != null) box.IsEnabled = false;

                panel.CreateAndAddDescriptionRow(
                    "This stream is calculated by the object connected upstream.");
            }

            // dynamics live with the stream conditions, as they do in the WinForms editor
            if (ms.SupportsDynamicMode)
            {
                panel.CreateAndAddLabelRow("Dynamics");

                var dynSpec = panel.CreateAndAddDropDownRow("Dynamic P/F Spec",
                    new List<string> { "Pressure", "Flow" },
                    ms.DynamicsSpec == DWSIM.Interfaces.Enums.Dynamics.DynamicsSpecType.Flow ? 1 : 0,
                    (dd, e) =>
                    {
                        ms.DynamicsSpec = dd.SelectedIndex == 1
                            ? DWSIM.Interfaces.Enums.Dynamics.DynamicsSpecType.Flow
                            : DWSIM.Interfaces.Enums.Dynamics.DynamicsSpecType.Pressure;
                        ms.GetFlowsheet().UpdateInterface();
                    });

                dynSpec.IsEnabled = ms.GetFlowsheet().DynamicMode;
                if (!ms.GetFlowsheet().DynamicMode)
                    panel.CreateAndAddDescriptionRow("The pressure/flow specification only applies in dynamic mode.");

                AvaloniaTabBuilders.PopulateDynamics(ms, panel);
            }

            if (!includeComposition) return;

            if (IsDrivenUpstream(ms))
            {
                panel.CreateAndAddLabelRow("Mixture Composition");
                panel.CreateAndAddDescriptionRow(
                    "The composition is calculated by the object connected upstream.");
                return;
            }

            panel.CreateAndAddLabelRow("Mixture Composition");
            panel.CreateAndAddDescriptionRow("Composition changes will only be committed after clicking on the 'Accept' button.");

            var basisDD = panel.CreateAndAddDropDownRow("Amount Basis",
                new List<string> {
                    "Mole Fractions",
                    "Mass Fractions",
                    "Molar Flows (" + su.molarflow + ")",
                    "Mass Flows (" + su.massflow + ")"
                }, 0, null);

            var tblist = new List<(TextBox tb, string name)>();
            foreach (var comp0 in ms.GetFlowsheet().SelectedCompounds.Values)
            {
                var comp = ms.Phases[0].Compounds[comp0.Name];
                var tbox = panel.CreateAndAddTextBoxRow(nff, comp.Name,
                    comp.MoleFraction.GetValueOrDefault(), null);
                tblist.Add((tbox, comp.Name));
            }

            basisDD.SelectionChanged += (s, e) =>
            {
                var W = ms.Phases[0].Properties.massflow.GetValueOrDefault();
                var Q = ms.Phases[0].Properties.molarflow.GetValueOrDefault();
                switch (basisDD.SelectedIndex)
                {
                    case 0:
                        foreach (var (tb, n) in tblist)
                            tb.Text = ms.Phases[0].Compounds[n].MoleFraction.GetValueOrDefault().ToString(nff, IC);
                        break;
                    case 1:
                        foreach (var (tb, n) in tblist)
                            tb.Text = ms.Phases[0].Compounds[n].MassFraction.GetValueOrDefault().ToString(nff, IC);
                        break;
                    case 2:
                        foreach (var (tb, n) in tblist)
                            tb.Text = cv.ConvertFromSI(su.molarflow,
                                ms.Phases[0].Compounds[n].MoleFraction.GetValueOrDefault() * Q).ToString(nff, IC);
                        break;
                    case 3:
                        foreach (var (tb, n) in tblist)
                            tb.Text = cv.ConvertFromSI(su.massflow,
                                ms.Phases[0].Compounds[n].MassFraction.GetValueOrDefault() * W).ToString(nff, IC);
                        break;
                }
            };

            panel.CreateAndAddButtonRow("Accept/Update", null, (btn, e) =>
            {
                AcceptComposition(ms, basisDD.SelectedIndex, tblist, su, nf, nff, txtQ, txtW);
            });

            panel.CreateAndAddButtonRow("Normalize", null, (btn, e) =>
            {
                double total = tblist.Sum(t => TryVal(t.tb.Text, out var v) ? v : 0.0);
                if (total > 0)
                    foreach (var (tb, _) in tblist)
                        if (TryVal(tb.Text, out var v)) tb.Text = (v / total).ToString(nff, IC);
            });

            panel.CreateAndAddButtonRow("Equalize", null, (btn, e) =>
            {
                foreach (var (tb, _) in tblist)
                    tb.Text = (1.0 / tblist.Count).ToString(nff, IC);
            });

            panel.CreateAndAddButtonRow("Clear", null, (btn, e) =>
            {
                foreach (var (tb, _) in tblist)
                    tb.Text = 0.0.ToString(nff, IC);
            });

            // Disable editing when stream is driven by an upstream unit operation
            if (ms.GraphicObject.InputConnectors[0].IsAttached &&
                ms.GraphicObject.InputConnectors[0].AttachedConnector.AttachedFrom.ObjectType != ObjectType.OT_Recycle)
            {
                if (!ms.GetFlowsheet().DynamicMode)
                    panel.IsEnabled = false;
            }
        }

        private static void AcceptComposition(MaterialStream ms, int basisIndex,
            List<(TextBox tb, string name)> tblist,
            object su, string nf, string nff,
            TextBox txtQ, TextBox txtW)
        {
            var suUnit = (DWSIM.Interfaces.IUnitsOfMeasure)su;

            double total = 0, mtotal = 0, mmtotal = 0;

            switch (basisIndex)
            {
                case 0: // Mole fractions — normalize then commit
                    total = tblist.Sum(t => TryVal(t.tb.Text, out var v) ? v : 0.0);
                    if (total <= 0) return;
                    foreach (var (tb, n) in tblist)
                        if (TryVal(tb.Text, out var v))
                        {
                            ms.Phases[0].Compounds[n].MoleFraction = v / total;
                            tb.Text = (v / total).ToString(nff, IC);
                        }
                    foreach (var comp in ms.Phases[0].Compounds.Values)
                        mtotal += comp.MoleFraction.GetValueOrDefault() * comp.ConstantProperties.Molar_Weight;
                    foreach (var comp in ms.Phases[0].Compounds.Values)
                        comp.MassFraction = comp.MoleFraction.GetValueOrDefault() * comp.ConstantProperties.Molar_Weight / mtotal;
                    break;

                case 1: // Mass fractions — normalize then commit
                    total = tblist.Sum(t => TryVal(t.tb.Text, out var v) ? v : 0.0);
                    if (total <= 0) return;
                    foreach (var (tb, n) in tblist)
                        if (TryVal(tb.Text, out var v))
                        {
                            ms.Phases[0].Compounds[n].MassFraction = v / total;
                            tb.Text = (v / total).ToString(nff, IC);
                        }
                    foreach (var comp in ms.Phases[0].Compounds.Values)
                        mmtotal += comp.MassFraction.GetValueOrDefault() / comp.ConstantProperties.Molar_Weight;
                    foreach (var comp in ms.Phases[0].Compounds.Values)
                        comp.MoleFraction = comp.MassFraction.GetValueOrDefault() / comp.ConstantProperties.Molar_Weight / mmtotal;
                    break;

                case 2: // Molar flows
                    total = tblist.Sum(t => TryVal(t.tb.Text, out var v) ? v : 0.0);
                    if (total <= 0) return;
                    double Q = cv.ConvertToSI(suUnit.molarflow, total);
                    foreach (var (tb, n) in tblist)
                        if (TryVal(tb.Text, out var v)) ms.Phases[0].Compounds[n].MoleFraction = v / total;
                    foreach (var comp in ms.Phases[0].Compounds.Values)
                        mtotal += comp.MoleFraction.GetValueOrDefault() * comp.ConstantProperties.Molar_Weight;
                    double W = 0;
                    foreach (var comp in ms.Phases[0].Compounds.Values)
                    {
                        comp.MassFraction = comp.MoleFraction.GetValueOrDefault() * comp.ConstantProperties.Molar_Weight / mtotal;
                        W += comp.MoleFraction.GetValueOrDefault() * comp.ConstantProperties.Molar_Weight / 1000 * Q;
                    }
                    ms.Phases[0].Properties.molarflow = Q;
                    ms.Phases[0].Properties.massflow = W;
                    txtQ.Text = cv.ConvertFromSI(suUnit.molarflow, Q).ToString(nf, IC);
                    break;

                case 3: // Mass flows
                    total = tblist.Sum(t => TryVal(t.tb.Text, out var v) ? v : 0.0);
                    if (total <= 0) return;
                    W = cv.ConvertToSI(suUnit.massflow, total);
                    foreach (var (tb, n) in tblist)
                        if (TryVal(tb.Text, out var v)) ms.Phases[0].Compounds[n].MassFraction = v / total;
                    foreach (var comp in ms.Phases[0].Compounds.Values)
                        mmtotal += comp.MassFraction.GetValueOrDefault() / comp.ConstantProperties.Molar_Weight;
                    Q = 0;
                    foreach (var comp in ms.Phases[0].Compounds.Values)
                    {
                        comp.MoleFraction = comp.MassFraction.GetValueOrDefault() / comp.ConstantProperties.Molar_Weight / mmtotal;
                        Q += comp.MassFraction.GetValueOrDefault() * W / comp.ConstantProperties.Molar_Weight * 1000;
                    }
                    ms.Phases[0].Properties.molarflow = Q;
                    ms.Phases[0].Properties.massflow = W;
                    txtW.Text = cv.ConvertFromSI(suUnit.massflow, W).ToString(nf, IC);
                    break;
            }
        }
    }
}
