using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums;
using DWSIM.UI.Shared.Avalonia;
using Reactors = DWSIM.UnitOperations.Reactors;
using UOps = DWSIM.UnitOperations.UnitOperations;
using cv = DWSIM.SharedClasses.SystemsOfUnits.Converter;

namespace DWSIM.UI.Desktop.Editors
{

    /// <summary>
    /// The rows the bio and refinery editors share: the section headers their Windows grids use,
    /// the compound role pickers, and the enumeration pickers built straight from the enum.
    /// </summary>
    internal static class BioRows
    {

        /// <summary>A section header, standing in for the grey separator row of the Windows grids.</summary>
        internal static void Section(AvaloniaEditorPanel panel, string title)
        {
            panel.CreateAndAddLabelRow(title);
        }

        /// <summary>
        /// A picker over the flowsheet compounds. <paramref name="optional"/> puts an empty entry
        /// first, which is how the Windows editors let a role go unassigned.
        /// </summary>
        internal static void Compound(AvaloniaEditorPanel panel, ISimulationObject obj, string label,
                                      Func<string> get, Action<string> set, bool optional = true)
        {
            var compounds = new List<string>();
            if (optional) compounds.Add("");
            compounds.AddRange(obj.GetFlowsheet().SelectedCompounds.Keys);

            panel.CreateAndAddDropDownRow(label, compounds,
                Math.Max(0, compounds.IndexOf(get() ?? "")), (dd, e) =>
                {
                    if (dd.SelectedIndex < 0 || dd.SelectedIndex >= compounds.Count) return;
                    set(compounds[dd.SelectedIndex]);
                });
        }

        /// <summary>A picker over an enumeration, listed and stored by name as the Windows grids do.</summary>
        internal static void Choice<T>(AvaloniaEditorPanel panel, string label,
                                       Func<T> get, Action<T> set) where T : struct
        {
            var names = Enum.GetNames(typeof(T)).ToList();

            panel.CreateAndAddDropDownRow(label, names,
                Math.Max(0, names.IndexOf(get().ToString())), (dd, e) =>
                {
                    if (dd.SelectedIndex < 0) return;
                    set((T)Enum.Parse(typeof(T), names[dd.SelectedIndex]));
                });
        }

        /// <summary>A plain number, in the unit the object stores it in.</summary>
        internal static void Number(AvaloniaEditorPanel panel, ISimulationObject obj, string label,
                                    double value, Action<double> set)
        {
            var nf = obj.GetFlowsheet().FlowsheetOptions.NumberFormat;

            panel.CreateAndAddTextBoxRow(nf, label, value,
                (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) set(v); });
        }

        /// <summary>A read-only result in the unit the object stores it in.</summary>
        internal static void Result(AvaloniaEditorPanel panel, ISimulationObject obj, string label,
                                    double value, string unit)
        {
            var nf = obj.GetFlowsheet().FlowsheetOptions.NumberFormat;

            panel.CreateAndAddTwoLabelsRow(label,
                value.ToString(nf, CultureInfo.CurrentCulture) + (string.IsNullOrEmpty(unit) ? "" : " " + unit));
        }

    }

    /// <summary>
    /// A per-compound coefficient: the value the unit uses for each compound, with where it came
    /// from. Editing a row pins the value, which is what the Windows grids call "user-set".
    /// </summary>
    internal sealed class CompoundCoefficientRow : INotifyPropertyChanged
    {
        private readonly Dictionary<string, double> _values;
        private readonly string _compound;
        private readonly string _nf;
        private double _seed;
        private string _note;

        internal CompoundCoefficientRow(Dictionary<string, double> values, string compound,
                                        double seed, string note, string nf)
        {
            _values = values;
            _compound = compound;
            _seed = seed;
            _note = note;
            _nf = nf;
        }

        public string Compound { get { return _compound; } }

        public string Value
        {
            get
            {
                var value = _values.ContainsKey(_compound) ? _values[_compound] : _seed;
                return value.ToString(_nf, CultureInfo.CurrentCulture);
            }
            set
            {
                if (!UnitOpEditorRows.TryParse(value, out var v)) return;
                if (v < 0.0) v = 0.0;
                if (v > 1.0) v = 1.0;

                _values[_compound] = v;
                _note = "user-set";

                Raise("Value");
                Raise("Note");
            }
        }

        public string Note { get { return _note; } }

        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise(string name)
        {
            if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(name));
        }
    }

    internal static class CompoundCoefficientGrid
    {

        /// <summary>
        /// Builds the grid from a seeding rule applied to each compound, which is how the Windows
        /// editors suggest a value from the molecular weight before the user pins one.
        /// </summary>
        internal static Control Build(ISimulationObject obj, Dictionary<string, double> values,
                                      string valueHeader,
                                      Func<ICompoundConstantProperties, (double Value, string Note)> seed)
        {
            var nf = obj.GetFlowsheet().FlowsheetOptions.NumberFormat;
            var rows = new ObservableCollection<CompoundCoefficientRow>();

            foreach (ICompoundConstantProperties compound in obj.GetFlowsheet().SelectedCompounds.Values)
            {
                var suggested = seed(compound);

                var note = values.ContainsKey(compound.Name) ? "user-set" : suggested.Note;
                rows.Add(new CompoundCoefficientRow(values, compound.Name, suggested.Value, note, nf));
            }

            var grid = new DataGrid
            {
                ItemsSource = rows,
                AutoGenerateColumns = false,
                CanUserSortColumns = false,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                Height = 260
            };

            grid.Columns.Add(GridColumns.Text("Compound", "Compound", 1.4, readOnly: true));
            grid.Columns.Add(GridColumns.Text(valueHeader, "Value", 1.0));
            grid.Columns.Add(GridColumns.Text("Source", "Note", 1.8, readOnly: true));

            return grid;
        }

    }

    /// <summary>Anaerobic digester editor, as the Windows EditingForm_AnaerobicDigester lays it out.</summary>
    public static class AnaerobicDigesterEditor
    {

        public static Control Build(Reactors.Reactor_AnaerobicDigester digester)
        {
            return UnitOpEditor.Build(digester,
                input: panel =>
                {
                    BioRows.Section(panel, "General");

                    panel.CreateAndAddValueUnitRow(digester, "Working Volume", UnitOfMeasure.volume,
                        digester.Volume, v => digester.Volume = v);

                    BioRows.Number(panel, digester, "Hydraulic Retention Time (h)",
                        digester.HRT_s / 3600.0, v => digester.HRT_s = v * 3600.0);

                    BioRows.Section(panel, "Compound Roles");

                    BioRows.Compound(panel, digester, "Organic Substrate Compound",
                        () => digester.SubstrateCompound, v => digester.SubstrateCompound = v, optional: false);
                    BioRows.Compound(panel, digester, "Methane Compound",
                        () => digester.MethaneCompound, v => digester.MethaneCompound = v, optional: false);
                    BioRows.Compound(panel, digester, "CO2 Compound",
                        () => digester.CO2Compound, v => digester.CO2Compound = v, optional: false);
                    BioRows.Compound(panel, digester, "Water Compound",
                        () => digester.WaterCompound, v => digester.WaterCompound = v);
                    BioRows.Compound(panel, digester, "NH3 Compound",
                        () => digester.NH3Compound, v => digester.NH3Compound = v);
                    BioRows.Compound(panel, digester, "Biomass (Sludge) Compound",
                        () => digester.BiomassCompound, v => digester.BiomassCompound = v);
                    BioRows.Compound(panel, digester, "H2S Compound",
                        () => digester.H2SCompound, v => digester.H2SCompound = v);

                    BioRows.Section(panel, "Sulfur Balance");

                    BioRows.Number(panel, digester, "Influent Sulfate Sulfur (mg S/L)",
                        digester.InfluentSulfateS_mgL, v => digester.InfluentSulfateS_mgL = v);
                    BioRows.Number(panel, digester, "Substrate Organic Sulfur (g S/kg)",
                        digester.SubstrateOrganicS_gPerKg, v => digester.SubstrateOrganicS_gPerKg = v);
                    BioRows.Number(panel, digester, "Assumed pH for Sulfide Speciation",
                        digester.AssumedPH_ForSulfide, v => digester.AssumedPH_ForSulfide = v);

                    BioRows.Section(panel, "Digester Parameters");

                    BioRows.Number(panel, digester, "COD Removal Efficiency (0-1)",
                        digester.CODRemovalEfficiency, v => digester.CODRemovalEfficiency = v);
                    BioRows.Number(panel, digester, "Biomass Yield on COD (g VSS/g COD)",
                        digester.BiomassYield_gVSSpergCOD, v => digester.BiomassYield_gVSSpergCOD = v);
                    BioRows.Number(panel, digester, "Methane Mole Fraction (override, 0 = Buswell)",
                        digester.MethaneFractionOverride, v => digester.MethaneFractionOverride = v);

                    BioRows.Section(panel, "Thermal Balance");

                    BioRows.Choice(panel, "Thermal Mode",
                        () => digester.ThermalMode, v => digester.ThermalMode = v);

                    panel.CreateAndAddValueUnitRow(digester, "Outlet Temperature Setpoint",
                        UnitOfMeasure.temperature, digester.OutletTemperature,
                        v => digester.OutletTemperature = v);

                    BioRows.Number(panel, digester, "Heat per g COD removed (J/g)",
                        digester.HeatPerGCODremoved_Jg, v => digester.HeatPerGCODremoved_Jg = v);

                    BioRows.Section(panel, "Model Fidelity");

                    BioRows.Choice(panel, "Digester Model", () => digester.Model, v => digester.Model = v);
                },
                results: panel => BuildResults(digester, panel),
                extras: new[] { ("ADM1-Lite Kinetics", BuildKinetics(digester)) });
        }

        /// <summary>The kinetics only feed the ADM1-Lite model, so they get a group of their own.</summary>
        private static Control BuildKinetics(Reactors.Reactor_AnaerobicDigester digester)
        {
            var panel = new AvaloniaEditorPanel();

            panel.CreateAndAddDescriptionRow("Only used when the model is ADM1Lite.");

            BioRows.Number(panel, digester, "k_hyd (hydrolysis, 1/d)",
                digester.ADM1_k_hyd_d, v => digester.ADM1_k_hyd_d = v);
            BioRows.Number(panel, digester, "km_su (acidogen max rate, 1/d)",
                digester.ADM1_km_su_d, v => digester.ADM1_km_su_d = v);
            BioRows.Number(panel, digester, "Ks_su (g COD/L)",
                digester.ADM1_Ks_su, v => digester.ADM1_Ks_su = v);
            BioRows.Number(panel, digester, "Y_su (g VSS/g COD)",
                digester.ADM1_Y_su, v => digester.ADM1_Y_su = v);
            BioRows.Number(panel, digester, "km_vfa (acetogen max rate, 1/d)",
                digester.ADM1_km_vfa_d, v => digester.ADM1_km_vfa_d = v);
            BioRows.Number(panel, digester, "Ks_vfa (g COD/L)",
                digester.ADM1_Ks_vfa, v => digester.ADM1_Ks_vfa = v);
            BioRows.Number(panel, digester, "Y_ace (g VSS/g COD)",
                digester.ADM1_Y_ace, v => digester.ADM1_Y_ace = v);
            BioRows.Number(panel, digester, "KI_h2 (H2 inhib. on acetogens, g COD/L)",
                digester.ADM1_KI_h2, v => digester.ADM1_KI_h2 = v);
            BioRows.Number(panel, digester, "km_ac (acetoclastic MG, 1/d)",
                digester.ADM1_km_ac_d, v => digester.ADM1_km_ac_d = v);
            BioRows.Number(panel, digester, "Ks_ac (g COD/L)",
                digester.ADM1_Ks_ac, v => digester.ADM1_Ks_ac = v);
            BioRows.Number(panel, digester, "Y_am (g VSS/g COD)",
                digester.ADM1_Y_am, v => digester.ADM1_Y_am = v);
            BioRows.Number(panel, digester, "km_h2 (hydrogenotrophic MG, 1/d)",
                digester.ADM1_km_h2_d, v => digester.ADM1_km_h2_d = v);
            BioRows.Number(panel, digester, "Ks_h2 (g COD/L)",
                digester.ADM1_Ks_h2, v => digester.ADM1_Ks_h2 = v);
            BioRows.Number(panel, digester, "Y_hm (g VSS/g COD)",
                digester.ADM1_Y_hm, v => digester.ADM1_Y_hm = v);
            BioRows.Number(panel, digester, "k_dec (population decay, 1/d)",
                digester.ADM1_k_dec_d, v => digester.ADM1_k_dec_d = v);

            return panel;
        }

        private static void BuildResults(Reactors.Reactor_AnaerobicDigester digester,
                                         AvaloniaEditorPanel panel)
        {
            BioRows.Section(panel, "COD Balance");
            panel.CreateAndAddResultRow(digester, "Feed COD", UnitOfMeasure.massflow, digester.Result_CODin_kgs);
            panel.CreateAndAddResultRow(digester, "COD Removed", UnitOfMeasure.massflow, digester.Result_CODremoved_kgs);
            panel.CreateAndAddResultRow(digester, "Substrate Consumed", UnitOfMeasure.massflow, digester.Result_SubstrateConsumed_kgs);

            BioRows.Section(panel, "Biogas");
            BioRows.Result(panel, digester, "Biogas Molar Flow", digester.Result_BiogasFlow_mols, "mol/s");
            panel.CreateAndAddResultRow(digester, "Methane Mass Flow", UnitOfMeasure.massflow, digester.Result_CH4_kgs);
            panel.CreateAndAddResultRow(digester, "CO2 Mass Flow", UnitOfMeasure.massflow, digester.Result_CO2_kgs);
            BioRows.Result(panel, digester, "Methane Mole Fraction", digester.Result_CH4MoleFraction * 100.0, "%");
            BioRows.Result(panel, digester, "Specific CH4 Yield", digester.Result_SpecificCH4Yield_Nm3kgCOD, "Nm3/kg COD");

            BioRows.Section(panel, "Sludge");
            panel.CreateAndAddResultRow(digester, "Biomass (Sludge) Production", UnitOfMeasure.massflow, digester.Result_Sludge_kgs);

            BioRows.Section(panel, "Sulfur");
            BioRows.Result(panel, digester, "H2S in Biogas", digester.Result_H2S_ppmv, "ppmv");
            panel.CreateAndAddResultRow(digester, "H2S Mass Flow", UnitOfMeasure.massflow, digester.Result_H2S_kgs);
            BioRows.Result(panel, digester, "Dissolved Sulfide in Effluent", digester.Result_DissolvedSulfide_kgSm3, "kg S/m3");

            BioRows.Section(panel, "Thermal Balance");
            panel.CreateAndAddResultRow(digester, "Metabolic Heat Release", UnitOfMeasure.heatflow, digester.Result_Q_metabolic_kW);
            panel.CreateAndAddResultRow(digester, "Net Heat Duty", UnitOfMeasure.heatflow, digester.Result_Q_duty_kW);
            panel.CreateAndAddResultRow(digester, "Outlet Temperature", UnitOfMeasure.temperature, digester.Result_OutletTemperature_K);

            if (digester.Model != Reactors.DigesterModel.BlackBox)
            {
                BioRows.Section(panel, "ADM1 Final State");
                BioRows.Result(panel, digester, "S_s (soluble substrate)", digester.ADM1_Result_S_s, "g COD/L");
                BioRows.Result(panel, digester, "S_VFA", digester.ADM1_Result_S_VFA, "g COD/L");
                BioRows.Result(panel, digester, "S_Ac (acetate)", digester.ADM1_Result_S_Ac, "g COD/L");
                BioRows.Result(panel, digester, "S_H2", digester.ADM1_Result_S_H2, "g COD/L");
                BioRows.Result(panel, digester, "X_hyd (acidogens)", digester.ADM1_Result_X_hyd, "g VSS/L");
                BioRows.Result(panel, digester, "X_ace (acetogens)", digester.ADM1_Result_X_ace, "g VSS/L");
                BioRows.Result(panel, digester, "X_am (acetoclastic)", digester.ADM1_Result_X_am, "g VSS/L");
                BioRows.Result(panel, digester, "X_hm (hydrogenotrophic)", digester.ADM1_Result_X_hm, "g VSS/L");
                BioRows.Result(panel, digester, "pH (crude estimate)", digester.ADM1_Result_pH, "");
            }
        }

    }

    /// <summary>Biogas upgrader editor: the removal efficiencies and which compound plays each role.</summary>
    public static class BiogasUpgraderEditor
    {

        public static Control Build(UOps.UnitOp_BiogasUpgrader upgrader)
        {
            return UnitOpEditor.Build(upgrader,
                input: panel =>
                {
                    BioRows.Section(panel, "General");

                    // picking a technology writes the efficiencies that go with it
                    BioRows.Choice(panel, "Technology", () => upgrader.Technology, v =>
                    {
                        upgrader.Technology = v;
                        upgrader.ApplyTechnologyDefaults();
                        upgrader.GetFlowsheet().UpdateOpenEditForms();
                    });

                    BioRows.Section(panel, "Removal Efficiencies (0-1)");

                    BioRows.Number(panel, upgrader, "H2S Removal",
                        upgrader.H2SRemovalEfficiency, v => upgrader.H2SRemovalEfficiency = v);
                    BioRows.Number(panel, upgrader, "CO2 Removal",
                        upgrader.CO2RemovalEfficiency, v => upgrader.CO2RemovalEfficiency = v);
                    BioRows.Number(panel, upgrader, "CH4 Loss (to offgas)",
                        upgrader.CH4LossFraction, v => upgrader.CH4LossFraction = v);
                    BioRows.Number(panel, upgrader, "H2O Removal",
                        upgrader.H2ORemovalEfficiency, v => upgrader.H2ORemovalEfficiency = v);
                    BioRows.Number(panel, upgrader, "Target CH4 Purity (reporting)",
                        upgrader.TargetCH4Purity, v => upgrader.TargetCH4Purity = v);

                    BioRows.Section(panel, "Compound Roles");

                    BioRows.Compound(panel, upgrader, "Methane Compound",
                        () => upgrader.MethaneCompound, v => upgrader.MethaneCompound = v, optional: false);
                    BioRows.Compound(panel, upgrader, "CO2 Compound",
                        () => upgrader.CO2Compound, v => upgrader.CO2Compound = v, optional: false);
                    BioRows.Compound(panel, upgrader, "H2S Compound",
                        () => upgrader.H2SCompound, v => upgrader.H2SCompound = v);
                    BioRows.Compound(panel, upgrader, "Water Compound",
                        () => upgrader.WaterCompound, v => upgrader.WaterCompound = v);
                    BioRows.Compound(panel, upgrader, "N2 Compound",
                        () => upgrader.N2Compound, v => upgrader.N2Compound = v);
                },
                results: panel =>
                {
                    BioRows.Section(panel, "Flows");
                    panel.CreateAndAddResultRow(upgrader, "Biogas Feed", UnitOfMeasure.massflow, upgrader.Result_FeedMass_kgs);
                    panel.CreateAndAddResultRow(upgrader, "Upgraded Gas (RNG)", UnitOfMeasure.massflow, upgrader.Result_UpgradedMass_kgs);
                    panel.CreateAndAddResultRow(upgrader, "Off-gas", UnitOfMeasure.massflow, upgrader.Result_OffgasMass_kgs);

                    BioRows.Section(panel, "Quality");
                    BioRows.Result(panel, upgrader, "Upgraded CH4 mass fraction", upgrader.Result_UpgradedCH4Fraction * 100.0, "%");
                    BioRows.Result(panel, upgrader, "CH4 recovery", upgrader.Result_CH4RecoveryFraction * 100.0, "%");
                });
        }

    }

    /// <summary>Cell lysis editor: the disruption technology and the release each compound sees.</summary>
    public static class CellLysisEditor
    {

        public static Control Build(UOps.UnitOp_CellLysis lysis)
        {
            return UnitOpEditor.Build(lysis,
                input: panel =>
                {
                    BioRows.Section(panel, "General");

                    BioRows.Choice(panel, "Technology", () => lysis.Technology, v => lysis.Technology = v);
                    BioRows.Number(panel, lysis, "Number of Passes", lysis.Passes, v => lysis.Passes = (int)v);
                    BioRows.Number(panel, lysis, "Operating Pressure (MPa)",
                        lysis.Pressure_MPa, v => lysis.Pressure_MPa = v);

                    BioRows.Section(panel, "Hetherington Correlation");

                    BioRows.Number(panel, lysis, "Rate constant k", lysis.HetheringtonK, v => lysis.HetheringtonK = v);
                    BioRows.Number(panel, lysis, "Pressure exponent alpha",
                        lysis.HetheringtonAlpha, v => lysis.HetheringtonAlpha = v);
                    BioRows.Number(panel, lysis, "Default Release Fraction (cap)",
                        lysis.DefaultReleaseFraction, v => lysis.DefaultReleaseFraction = v);

                    BioRows.Section(panel, "Ultrasound / Sonication");

                    BioRows.Number(panel, lysis, "Acoustic Power Density (W/mL)",
                        lysis.Ultrasound_PowerDensity_WmL, v => lysis.Ultrasound_PowerDensity_WmL = v);
                    BioRows.Number(panel, lysis, "Sonication Time (s)",
                        lysis.Ultrasound_Time_s, v => lysis.Ultrasound_Time_s = v);
                    BioRows.Number(panel, lysis, "Rate constant k_u",
                        lysis.Ultrasound_k, v => lysis.Ultrasound_k = v);
                    BioRows.Number(panel, lysis, "Power exponent beta",
                        lysis.Ultrasound_Beta, v => lysis.Ultrasound_Beta = v);

                    BioRows.Section(panel, "Compound Role");

                    BioRows.Compound(panel, lysis, "Biomass (routes to debris)",
                        () => lysis.BiomassCompound, v => lysis.BiomassCompound = v);
                },
                results: panel =>
                {
                    BioRows.Section(panel, "Flows");
                    panel.CreateAndAddResultRow(lysis, "Feed", UnitOfMeasure.massflow, lysis.Result_FeedMass_kgs);
                    panel.CreateAndAddResultRow(lysis, "Lysate", UnitOfMeasure.massflow, lysis.Result_LysateMass_kgs);
                    panel.CreateAndAddResultRow(lysis, "Debris", UnitOfMeasure.massflow, lysis.Result_DebrisMass_kgs);

                    BioRows.Section(panel, "Release");
                    BioRows.Result(panel, lysis, "Overall macromolecule release",
                        lysis.Result_OverallRelease * 100.0, "%");
                },
                extras: new[] { ("Release Fractions", BuildRelease(lysis)) });
        }

        private static Control BuildRelease(UOps.UnitOp_CellLysis lysis)
        {
            if (lysis.ReleaseFraction == null) lysis.ReleaseFraction = new Dictionary<string, double>();

            // the correlation gives the cap, and the biomass itself never releases: it is the debris
            var release = lysis.Technology == UOps.LysisTechnology.Ultrasound
                ? lysis.UltrasoundRelease()
                : lysis.HetheringtonRelease();

            return CompoundCoefficientGrid.Build(lysis, lysis.ReleaseFraction, "Release Fraction",
                compound =>
                {
                    if (compound.Name == lysis.BiomassCompound) return (0.0, "biomass (to debris)");
                    if (compound.Molar_Weight > 5000)
                        return (release * lysis.DefaultReleaseFraction, "intracellular macromolecule");
                    return (1.0, "small solute (diffuses out)");
                });
        }

    }

    /// <summary>Centrifuge editor: the bowl and the recovery each compound sees to the heavy phase.</summary>
    public static class CentrifugeEditor
    {

        public static Control Build(UOps.UnitOp_Centrifuge centrifuge)
        {
            return UnitOpEditor.Build(centrifuge,
                input: panel =>
                {
                    BioRows.Section(panel, "General");

                    BioRows.Choice(panel, "Technology", () => centrifuge.Technology, v => centrifuge.Technology = v);
                    BioRows.Number(panel, centrifuge, "Bowl Speed (rpm)",
                        centrifuge.BowlSpeed_rpm, v => centrifuge.BowlSpeed_rpm = v);
                    BioRows.Number(panel, centrifuge, "Sigma Factor (m2)",
                        centrifuge.SigmaFactor_m2, v => centrifuge.SigmaFactor_m2 = v);
                    BioRows.Number(panel, centrifuge, "Default Recovery to Heavy",
                        centrifuge.DefaultRecoveryToHeavy, v => centrifuge.DefaultRecoveryToHeavy = v);
                },
                results: panel =>
                {
                    BioRows.Section(panel, "Flows");
                    panel.CreateAndAddResultRow(centrifuge, "Feed", UnitOfMeasure.massflow, centrifuge.Result_FeedMass_kgs);
                    panel.CreateAndAddResultRow(centrifuge, "Heavy (Concentrate)", UnitOfMeasure.massflow, centrifuge.Result_HeavyMass_kgs);
                    panel.CreateAndAddResultRow(centrifuge, "Light (Clarified)", UnitOfMeasure.massflow, centrifuge.Result_LightMass_kgs);

                    BioRows.Section(panel, "Performance");
                    BioRows.Result(panel, centrifuge, "Solids Recovery (MW > 10 kDa)",
                        centrifuge.Result_SolidsRecovery * 100.0, "%");
                },
                extras: new[] { ("Recovery", BuildRecovery(centrifuge)) });
        }

        private static Control BuildRecovery(UOps.UnitOp_Centrifuge centrifuge)
        {
            if (centrifuge.RecoveryToHeavy == null)
                centrifuge.RecoveryToHeavy = new Dictionary<string, double>();

            return CompoundCoefficientGrid.Build(centrifuge, centrifuge.RecoveryToHeavy,
                "Recovery to Heavy",
                compound =>
                {
                    if (compound.Molar_Weight > 10000) return (0.95, "macromolecule default");
                    if (compound.Molar_Weight > 1000) return (0.6, "colloid default");
                    return (centrifuge.DefaultRecoveryToHeavy, "solute default");
                });
        }

    }

    /// <summary>Chromatography editor: the column, its chemistry and the recovery per compound.</summary>
    public static class ChromatographyEditor
    {

        public static Control Build(UOps.UnitOp_Chromatography column)
        {
            return UnitOpEditor.Build(column,
                input: panel =>
                {
                    BioRows.Section(panel, "General");

                    BioRows.Choice(panel, "Mode", () => column.Mode, v =>
                    {
                        column.Mode = v;
                        column.GetFlowsheet().UpdateOpenEditForms();
                    });

                    BioRows.Choice(panel, "Chemistry", () => column.Chemistry, v => column.Chemistry = v);

                    BioRows.Number(panel, column, "Column Volume (L)",
                        column.ColumnVolume_L, v => column.ColumnVolume_L = v);
                    BioRows.Number(panel, column, "Dynamic Binding Capacity (g/L)",
                        column.DynamicBindingCapacity_gL, v => column.DynamicBindingCapacity_gL = v);
                    BioRows.Number(panel, column, "Default Recovery to Product",
                        column.DefaultRecoveryToProduct, v => column.DefaultRecoveryToProduct = v);

                    // the breakthrough model only runs in the dynamic mode
                    if (column.Mode != UOps.ChromatographyMode.BindElute_Dynamic) return;

                    BioRows.Section(panel, "Thomas Breakthrough (Dynamic)");

                    BioRows.Number(panel, column, "Thomas Rate Constant k_Th (L/(g.s))",
                        column.ThomasRateConstant_Lgs, v => column.ThomasRateConstant_Lgs = v);
                    BioRows.Number(panel, column, "Loading Time (s, 0 = auto to 99%)",
                        column.LoadingTime_s, v => column.LoadingTime_s = v);
                    BioRows.Number(panel, column, "Resin Density (g/L)",
                        column.ResinDensity_gL, v => column.ResinDensity_gL = v);
                },
                results: panel =>
                {
                    BioRows.Section(panel, "Flows");
                    panel.CreateAndAddResultRow(column, "Feed", UnitOfMeasure.massflow, column.Result_FeedMass_kgs);
                    panel.CreateAndAddResultRow(column, "Product", UnitOfMeasure.massflow, column.Result_ProductMass_kgs);
                    panel.CreateAndAddResultRow(column, "Waste", UnitOfMeasure.massflow, column.Result_WasteMass_kgs);

                    BioRows.Section(panel, "Performance");
                    BioRows.Result(panel, column, "Target Recovery (MW > 5 kDa)",
                        column.Result_TargetRecovery * 100.0, "%");
                    BioRows.Result(panel, column, "Load Ratio (load/DBC)", column.Result_LoadRatio, "");
                    panel.CreateAndAddTwoLabelsRow("Saturated?",
                        column.Result_Saturated ? "YES - column exceeded" : "no");
                },
                extras: new[] { ("Recovery", BuildRecovery(column)) });
        }

        private static Control BuildRecovery(UOps.UnitOp_Chromatography column)
        {
            if (column.RecoveryToProduct == null)
                column.RecoveryToProduct = new Dictionary<string, double>();

            var bindElute = column.Mode == UOps.ChromatographyMode.BindElute;

            return CompoundCoefficientGrid.Build(column, column.RecoveryToProduct,
                "Recovery to Product",
                compound =>
                {
                    if (compound.Molar_Weight > 5000)
                        return bindElute
                            ? (0.90, "macromolecule (binds, elutes to product)")
                            : (0.10, "macromolecule (binds, stays on column)");

                    return bindElute
                        ? (column.DefaultRecoveryToProduct, "small solute (flows through)")
                        : (0.95, "small solute (passes through)");
                });
        }

    }

    /// <summary>Crossflow ultrafiltration editor: the operating specs, the membrane and the sieving.</summary>
    public static class CrossflowUFEditor
    {

        public static Control Build(UOps.UnitOp_CrossflowUF unit)
        {
            return UnitOpEditor.Build(unit,
                input: panel =>
                {
                    BioRows.Section(panel, "General");

                    BioRows.Choice(panel, "Operating Mode",
                        () => unit.OperatingMode, v => unit.OperatingMode = v);

                    BioRows.Section(panel, "Operating Specs");

                    BioRows.Number(panel, unit, "VCF (Concentration mode)", unit.VCF, v => unit.VCF = v);
                    BioRows.Number(panel, unit, "Diavolumes (DF mode)", unit.Diavolumes, v => unit.Diavolumes = v);
                    BioRows.Number(panel, unit, "Default Sieving Coefficient",
                        unit.DefaultSievingCoefficient, v => unit.DefaultSievingCoefficient = v);

                    BioRows.Section(panel, "Membrane");

                    BioRows.Number(panel, unit, "Permeate Flux J0 (kg/m2/s)",
                        unit.MembraneFlux_kgm2s, v => unit.MembraneFlux_kgm2s = v);

                    panel.CreateAndAddValueUnitRow(unit, "Transmembrane Pressure", UnitOfMeasure.pressure,
                        unit.TMP_Pa, v => unit.TMP_Pa = v);

                    BioRows.Number(panel, unit, "Membrane Area (m2, dynamic)",
                        unit.MembraneArea_m2, v => unit.MembraneArea_m2 = v);
                    BioRows.Number(panel, unit, "Fouling Half-Life (s, 0 = off)",
                        unit.FoulingHalfLife_s, v => unit.FoulingHalfLife_s = v);
                },
                results: panel =>
                {
                    BioRows.Section(panel, "Flows");
                    panel.CreateAndAddResultRow(unit, "Feed Mass Flow", UnitOfMeasure.massflow, unit.Result_FeedMass_kgs);
                    panel.CreateAndAddResultRow(unit, "DF Buffer Mass Flow", UnitOfMeasure.massflow, unit.Result_BufferMass_kgs);
                    panel.CreateAndAddResultRow(unit, "Retentate Mass Flow", UnitOfMeasure.massflow, unit.Result_Retentate_kgs);
                    panel.CreateAndAddResultRow(unit, "Permeate Mass Flow", UnitOfMeasure.massflow, unit.Result_Permeate_kgs);

                    BioRows.Section(panel, "Performance");
                    BioRows.Result(panel, unit, "Effective VCF", unit.Result_EffectiveVCF, "");
                    BioRows.Result(panel, unit, "Required Membrane Area", unit.Result_MembraneArea_m2, "m2");
                },
                extras: new[] { ("Sieving Coefficients", BuildSieving(unit)) });
        }

        private static Control BuildSieving(UOps.UnitOp_CrossflowUF unit)
        {
            if (unit.SievingCoefficients == null)
                unit.SievingCoefficients = new Dictionary<string, double>();

            // sigma follows the molecular weight: small compounds pass, macromolecules are retained
            return CompoundCoefficientGrid.Build(unit, unit.SievingCoefficients, "Sieving Coefficient",
                compound =>
                {
                    if (compound.Molar_Weight <= 0) return (unit.DefaultSievingCoefficient, "default (no MW)");
                    if (compound.Molar_Weight < 200) return (1.0, "small-MW default");
                    if (compound.Molar_Weight < 1000) return (0.5, "mid-MW default");
                    return (0.0, "macromolecule default");
                });
        }

    }

    /// <summary>Crystallizer editor: the solubility curve and what the chosen mode reads from it.</summary>
    public static class CrystallizerEditor
    {

        public static Control Build(UOps.UnitOp_Crystallizer crystallizer)
        {
            return UnitOpEditor.Build(crystallizer,
                input: panel =>
                {
                    BioRows.Section(panel, "General");

                    BioRows.Choice(panel, "Mode", () => crystallizer.Mode, v => crystallizer.Mode = v);

                    BioRows.Compound(panel, crystallizer, "Solute Compound",
                        () => crystallizer.SoluteCompound, v => crystallizer.SoluteCompound = v, optional: false);
                    BioRows.Compound(panel, crystallizer, "Solvent Compound",
                        () => crystallizer.SolventCompound, v => crystallizer.SolventCompound = v, optional: false);

                    panel.CreateAndAddValueUnitRow(crystallizer, "Operating Temperature",
                        UnitOfMeasure.temperature, crystallizer.OperatingT_K,
                        v => crystallizer.OperatingT_K = v);

                    BioRows.Section(panel, "Solubility  C_sat(T) = A + B*(T-298) + C*(T-298)^2  [g/g solvent]");

                    BioRows.Number(panel, crystallizer, "A (solubility at 298 K, g/g)",
                        crystallizer.Sol_A, v => crystallizer.Sol_A = v);
                    BioRows.Number(panel, crystallizer, "B (linear T coefficient, g/g/K)",
                        crystallizer.Sol_B, v => crystallizer.Sol_B = v);
                    BioRows.Number(panel, crystallizer, "C (quadratic T coefficient, g/g/K2)",
                        crystallizer.Sol_C, v => crystallizer.Sol_C = v);

                    BioRows.Section(panel, "Mode-Specific");

                    BioRows.Number(panel, crystallizer, "Evaporation Fraction (Evaporative)",
                        crystallizer.EvaporationFraction, v => crystallizer.EvaporationFraction = v);
                    BioRows.Number(panel, crystallizer, "Solubility Reduction by Antisolvent",
                        crystallizer.SolubilityReductionByAntisolvent,
                        v => crystallizer.SolubilityReductionByAntisolvent = v);
                    BioRows.Number(panel, crystallizer, "Mean Crystal Size (um, reported)",
                        crystallizer.MeanCrystalSize_um, v => crystallizer.MeanCrystalSize_um = v);
                },
                results: panel =>
                {
                    BioRows.Section(panel, "Solubility");
                    BioRows.Result(panel, crystallizer, "C_sat at operating T",
                        crystallizer.Result_Csat_gg, "g/g solvent");

                    BioRows.Section(panel, "Yield");
                    panel.CreateAndAddResultRow(crystallizer, "Solute in feed", UnitOfMeasure.massflow, crystallizer.Result_SoluteInFeed_kgs);
                    panel.CreateAndAddResultRow(crystallizer, "Crystallized", UnitOfMeasure.massflow, crystallizer.Result_Cryst_kgs);
                    panel.CreateAndAddResultRow(crystallizer, "Mother liquor", UnitOfMeasure.massflow, crystallizer.Result_MotherLiquor_kgs);
                    BioRows.Result(panel, crystallizer, "Crystallization Yield", crystallizer.Result_Yield * 100.0, "%");
                });
        }

    }

    /// <summary>
    /// Pretreatment reactor editor: the severity of the treatment, which compound plays each role
    /// in the biomass, and the conversions the reactor applies.
    /// </summary>
    public static class PretreatmentEditor
    {

        public static Control Build(Reactors.Reactor_Pretreatment reactor)
        {
            return UnitOpEditor.Build(reactor,
                input: panel =>
                {
                    BioRows.Section(panel, "General");

                    BioRows.Choice(panel, "Technology", () => reactor.Technology, v =>
                    {
                        reactor.Technology = v;
                        reactor.ApplyTechnologyDefaults();
                        reactor.GetFlowsheet().UpdateOpenEditForms();
                    });

                    BioRows.Number(panel, reactor, "Severity log R0",
                        reactor.SeverityLogR0, v => reactor.SeverityLogR0 = v);

                    panel.CreateAndAddValueUnitRow(reactor, "Residence Time", UnitOfMeasure.time,
                        reactor.ResidenceTime_s, v => reactor.ResidenceTime_s = v);

                    BioRows.Number(panel, reactor, "Solids Loading (w/w)",
                        reactor.SolidsLoading_wfrac, v => reactor.SolidsLoading_wfrac = v);

                    panel.CreateAndAddValueUnitRow(reactor, "Outlet Temperature (optional)",
                        UnitOfMeasure.temperature, reactor.OutletTemperature,
                        v => reactor.OutletTemperature = v);

                    BioRows.Section(panel, "Compound Roles");

                    BioRows.Compound(panel, reactor, "Cellulose",
                        () => reactor.CelluloseCompound, v => reactor.CelluloseCompound = v);
                    BioRows.Compound(panel, reactor, "Hemicellulose",
                        () => reactor.HemicelluloseCompound, v => reactor.HemicelluloseCompound = v);
                    BioRows.Compound(panel, reactor, "Lignin",
                        () => reactor.LigninCompound, v => reactor.LigninCompound = v);
                    BioRows.Compound(panel, reactor, "Soluble Lignin (optional)",
                        () => reactor.SolubleLigninCompound, v => reactor.SolubleLigninCompound = v);
                    BioRows.Compound(panel, reactor, "Glucose",
                        () => reactor.GlucoseCompound, v => reactor.GlucoseCompound = v);
                    BioRows.Compound(panel, reactor, "Xylose",
                        () => reactor.XyloseCompound, v => reactor.XyloseCompound = v);
                    BioRows.Compound(panel, reactor, "HMF",
                        () => reactor.HMFCompound, v => reactor.HMFCompound = v);
                    BioRows.Compound(panel, reactor, "Furfural",
                        () => reactor.FurfuralCompound, v => reactor.FurfuralCompound = v);
                    BioRows.Compound(panel, reactor, "Acetic Acid",
                        () => reactor.AceticAcidCompound, v => reactor.AceticAcidCompound = v);
                    BioRows.Compound(panel, reactor, "Water",
                        () => reactor.WaterCompound, v => reactor.WaterCompound = v);

                    BioRows.Section(panel, "Conversion Fractions (0-1)");

                    BioRows.Number(panel, reactor, "Cellulose to Glucose",
                        reactor.CelluloseConversion, v => reactor.CelluloseConversion = v);
                    BioRows.Number(panel, reactor, "Glucose to HMF",
                        reactor.GlucoseToHMF, v => reactor.GlucoseToHMF = v);
                    BioRows.Number(panel, reactor, "Hemicellulose to Xylose",
                        reactor.HemicelluloseConversion, v => reactor.HemicelluloseConversion = v);
                    BioRows.Number(panel, reactor, "Xylose to Furfural",
                        reactor.XyloseToFurfural, v => reactor.XyloseToFurfural = v);
                    BioRows.Number(panel, reactor, "Lignin Solubilization",
                        reactor.LigninSolubilization, v => reactor.LigninSolubilization = v);
                    BioRows.Number(panel, reactor, "Acetic Acid Yield on Hemi (g/g)",
                        reactor.AceticAcidYieldOnHemi, v => reactor.AceticAcidYieldOnHemi = v);
                },
                results: panel =>
                {
                    BioRows.Section(panel, "Consumed");
                    panel.CreateAndAddResultRow(reactor, "Cellulose", UnitOfMeasure.massflow, reactor.Result_CelluloseConsumed_kgs);
                    panel.CreateAndAddResultRow(reactor, "Hemicellulose", UnitOfMeasure.massflow, reactor.Result_HemicelluloseConsumed_kgs);
                    panel.CreateAndAddResultRow(reactor, "Lignin Solubilized", UnitOfMeasure.massflow, reactor.Result_LigninSolubilized_kgs);

                    BioRows.Section(panel, "Sugars Produced");
                    panel.CreateAndAddResultRow(reactor, "Glucose", UnitOfMeasure.massflow, reactor.Result_GlucoseProduced_kgs);
                    panel.CreateAndAddResultRow(reactor, "Xylose", UnitOfMeasure.massflow, reactor.Result_XyloseProduced_kgs);

                    BioRows.Section(panel, "Inhibitors Produced");
                    panel.CreateAndAddResultRow(reactor, "HMF", UnitOfMeasure.massflow, reactor.Result_HMFProduced_kgs);
                    panel.CreateAndAddResultRow(reactor, "Furfural", UnitOfMeasure.massflow, reactor.Result_FurfuralProduced_kgs);
                    panel.CreateAndAddResultRow(reactor, "Acetic Acid", UnitOfMeasure.massflow, reactor.Result_AceticAcidProduced_kgs);
                });
        }

    }

    /// <summary>
    /// Bioreactor editor: the growth kinetics, the yields and the oxygen transfer, with the
    /// enzymatic hydrolysis parameters the alternative kinetic model reads.
    /// </summary>
    public static class BioReactorEditor
    {

        public static Control Build(Reactors.Reactor_BioReactor reactor)
        {
            return UnitOpEditor.Build(reactor,
                input: panel => BuildParameters(reactor, panel),
                results: panel => BuildResults(reactor, panel),
                extras: new[] { ("Enzymatic Hydrolysis", BuildHydrolysis(reactor)) });
        }

        private static void BuildParameters(Reactors.Reactor_BioReactor reactor, AvaloniaEditorPanel panel)
        {
            BioRows.Section(panel, "General");

            BioRows.Choice(panel, "Operating Mode",
                () => reactor.OperatingMode, v => reactor.OperatingMode = v);
            BioRows.Choice(panel, "Kinetic Model",
                () => reactor.KineticModel, v =>
                {
                    reactor.KineticModel = v;
                    reactor.GetFlowsheet().UpdateOpenEditForms();
                });

            panel.CreateAndAddCheckBoxRow("Aerobic", reactor.IsAerobic,
                (cb, e) => reactor.IsAerobic = cb.IsChecked.GetValueOrDefault());

            panel.CreateAndAddValueUnitRow(reactor, "Working Volume", UnitOfMeasure.volume,
                reactor.Volume, v => reactor.Volume = v);

            panel.CreateAndAddValueUnitRow(reactor, "Batch Duration", UnitOfMeasure.time,
                reactor.BatchDuration, v => reactor.BatchDuration = v);

            BioRows.Section(panel, "Compound Roles");

            BioRows.Compound(panel, reactor, "Biomass Compound",
                () => reactor.BiomassCompound, v => reactor.BiomassCompound = v, optional: false);
            BioRows.Compound(panel, reactor, "Substrate Compound",
                () => reactor.SubstrateCompound, v => reactor.SubstrateCompound = v, optional: false);
            BioRows.Compound(panel, reactor, "Product Compound",
                () => reactor.ProductCompound, v => reactor.ProductCompound = v);
            BioRows.Compound(panel, reactor, "Oxygen Compound",
                () => reactor.OxygenCompound, v => reactor.OxygenCompound = v);
            BioRows.Compound(panel, reactor, "CO2 Compound",
                () => reactor.CO2Compound, v => reactor.CO2Compound = v);
            BioRows.Compound(panel, reactor, "N-Source Compound",
                () => reactor.NitrogenSourceCompound, v => reactor.NitrogenSourceCompound = v);
            BioRows.Compound(panel, reactor, "Water Compound",
                () => reactor.WaterCompound, v => reactor.WaterCompound = v);

            BioRows.Section(panel, "Kinetics");

            BioRows.Number(panel, reactor, "Max Specific Growth Rate mu_max (1/h)",
                reactor.MuMax_h, v => reactor.MuMax_h = v);
            BioRows.Number(panel, reactor, "Saturation Constant Ks (g/L)",
                reactor.Ks_gL, v => reactor.Ks_gL = v);
            BioRows.Number(panel, reactor, "Inhibition Constant Ki, Haldane (g/L)",
                reactor.Ki_gL, v => reactor.Ki_gL = v);
            BioRows.Number(panel, reactor, "Moser Exponent n",
                reactor.MoserN, v => reactor.MoserN = v);

            BioRows.Section(panel, "Stoichiometry / Yields");

            BioRows.Number(panel, reactor, "Biomass Yield Yxs (g/g)",
                reactor.YieldXS, v => reactor.YieldXS = v);
            BioRows.Number(panel, reactor, "Product Yield Yps (g/g)",
                reactor.YieldPS, v => reactor.YieldPS = v);
            BioRows.Number(panel, reactor, "Maintenance Coefficient ms (g/g/h)",
                reactor.Maintenance_gSg_cellh, v => reactor.Maintenance_gSg_cellh = v);
            BioRows.Number(panel, reactor, "Death Rate kd (1/h)",
                reactor.DeathRate_h, v => reactor.DeathRate_h = v);

            BioRows.Section(panel, "Oxygen Transfer");

            BioRows.Number(panel, reactor, "Volumetric Oxygen Transfer Coeff. kLa (1/h)",
                reactor.KLa_h, v => reactor.KLa_h = v);

            BioRows.Section(panel, "Thermal Balance");

            BioRows.Choice(panel, "Thermal Mode",
                () => reactor.ThermalMode, v => reactor.ThermalMode = v);

            BioRows.Number(panel, reactor, "Heat per mol O2, Cooney (J/mol O2)",
                reactor.HeatPerMolO2_JmolO2, v => reactor.HeatPerMolO2_JmolO2 = v);

            panel.CreateAndAddValueUnitRow(reactor, "Outlet Temperature Setpoint",
                UnitOfMeasure.temperature, reactor.OutletTemperature,
                v => reactor.OutletTemperature = v);
        }

        private static Control BuildHydrolysis(Reactors.Reactor_BioReactor reactor)
        {
            var panel = new AvaloniaEditorPanel();

            panel.CreateAndAddDescriptionRow(
                "Only used when the kinetic model is EnzymaticHydrolysis.");

            BioRows.Compound(panel, reactor, "Hemicellulose Compound",
                () => reactor.HemicelluloseCompound, v => reactor.HemicelluloseCompound = v);
            BioRows.Compound(panel, reactor, "Xylose Compound",
                () => reactor.XyloseCompound, v => reactor.XyloseCompound = v);
            BioRows.Compound(panel, reactor, "Enzyme (Cellulase) Compound",
                () => reactor.EnzymeCompound, v => reactor.EnzymeCompound = v);

            BioRows.Number(panel, reactor, "Cellulose Rate Constant k1 (L/(g.h))",
                reactor.EH_k1_Lgh, v => reactor.EH_k1_Lgh = v);
            BioRows.Number(panel, reactor, "Hemicellulose Rate Constant k2 (L/(g.h))",
                reactor.EH_k2_Lgh, v => reactor.EH_k2_Lgh = v);
            BioRows.Number(panel, reactor, "Glucose Inhibition Constant K_G (g/L)",
                reactor.EH_KG_glucose_gL, v => reactor.EH_KG_glucose_gL = v);
            BioRows.Number(panel, reactor, "Xylose Inhibition Constant K_X (g/L)",
                reactor.EH_KX_xylose_gL, v => reactor.EH_KX_xylose_gL = v);
            BioRows.Number(panel, reactor, "Enzyme Loading (override, g/L)",
                reactor.EH_EnzymeLoading_gL, v => reactor.EH_EnzymeLoading_gL = v);
            BioRows.Number(panel, reactor, "Heat per g Sugar Produced (J/g)",
                reactor.EH_HeatPerGProduct_Jg, v => reactor.EH_HeatPerGProduct_Jg = v);

            return panel;
        }

        private static void BuildResults(Reactors.Reactor_BioReactor reactor, AvaloniaEditorPanel panel)
        {
            if (reactor.KineticModel == Reactors.BioKineticModel.EnzymaticHydrolysis)
            {
                BioRows.Section(panel, "Streams (Hydrolysate Outlet)");
                BioRows.Result(panel, reactor, "Residual Cellulose [S]", reactor.Result_S_gL, "g/L");
                BioRows.Result(panel, reactor, "Glucose [P]", reactor.Result_P_gL, "g/L");
            }
            else
            {
                BioRows.Section(panel, "Streams (Outlet Broth)");
                BioRows.Result(panel, reactor, "Biomass Concentration [X]", reactor.Result_X_gL, "g/L");
                BioRows.Result(panel, reactor, "Substrate Concentration [S]", reactor.Result_S_gL, "g/L");
                BioRows.Result(panel, reactor, "Product Concentration [P]", reactor.Result_P_gL, "g/L");

                BioRows.Section(panel, "Kinetics");
                BioRows.Result(panel, reactor, "Average Specific Growth Rate (mu)", reactor.Result_Mu_h, "1/h");

                BioRows.Section(panel, "Gas Exchange");
                BioRows.Result(panel, reactor, "Oxygen Uptake Rate (OUR)", reactor.Result_OUR_gLh, "g O2/L/h");
                BioRows.Result(panel, reactor, "Carbon Dioxide Evolution Rate (CER)", reactor.Result_CER_gLh, "g CO2/L/h");
                BioRows.Result(panel, reactor, "Respiratory Quotient (RQ)", reactor.Result_RQ, "");
            }

            BioRows.Section(panel, "Thermal Balance");
            panel.CreateAndAddResultRow(reactor, "Metabolic Heat Release", UnitOfMeasure.heatflow, reactor.Result_Q_metabolic_kW);
            panel.CreateAndAddResultRow(reactor, "Net Heat Duty", UnitOfMeasure.heatflow, reactor.Result_Q_duty_kW);
            panel.CreateAndAddResultRow(reactor, "Outlet Temperature", UnitOfMeasure.temperature, reactor.Result_OutletTemperature_K);
        }

    }

    /// <summary>
    /// Circulating fluidized bed fast pyrolysis reactor editor: the riser, the bed material, the
    /// biomass composition and the char combustor that closes the heat loop.
    /// </summary>
    public static class CFBPyrolysisEditor
    {

        public static Control Build(Reactors.Reactor_CFBFastPyrolysis reactor)
        {
            return UnitOpEditor.Build(reactor,
                input: panel => BuildParameters(reactor, panel),
                results: panel => BuildResults(reactor, panel),
                extras: new[] { ("Internal Char Combustor", BuildCombustor(reactor)) });
        }

        private static void BuildParameters(Reactors.Reactor_CFBFastPyrolysis reactor,
                                            AvaloniaEditorPanel panel)
        {
            BioRows.Section(panel, "Operating Mode");

            BioRows.Choice(panel, "Sand / Heat Supply Mode",
                () => reactor.SandMode, v =>
                {
                    reactor.SandMode = v;
                    reactor.GetFlowsheet().UpdateOpenEditForms();
                });

            BioRows.Section(panel, "Geometry");

            panel.CreateAndAddValueUnitRow(reactor, "Riser Height", UnitOfMeasure.distance,
                reactor.RiserHeight_m, v => reactor.RiserHeight_m = v);
            panel.CreateAndAddValueUnitRow(reactor, "Riser Diameter", UnitOfMeasure.diameter,
                reactor.RiserDiameter_m, v => reactor.RiserDiameter_m = v);

            BioRows.Number(panel, reactor, "Axial Discretization Cells",
                reactor.NumAxialCells, v => reactor.NumAxialCells = (int)v);
            BioRows.Number(panel, reactor, "Solids Holdup (0-1)",
                reactor.SolidsHoldup, v => reactor.SolidsHoldup = v);

            BioRows.Section(panel, "Bed Material");

            panel.CreateAndAddValueUnitRow(reactor, "Density", UnitOfMeasure.density,
                reactor.BedMaterialDensity_kgm3, v => reactor.BedMaterialDensity_kgm3 = v);

            // the object keeps the heat capacity in J/kg/K, while the unit system works in kJ/kg/K
            panel.CreateAndAddValueUnitRow(reactor, "Heat Capacity", UnitOfMeasure.heatCapacityCp,
                reactor.BedMaterialCp_JkgK / 1000.0, v => reactor.BedMaterialCp_JkgK = v * 1000.0);

            BioRows.Section(panel, "Operating Conditions");

            panel.CreateAndAddValueUnitRow(reactor, "Carrier Gas Velocity", UnitOfMeasure.velocity,
                reactor.CarrierGasVelocity_ms, v => reactor.CarrierGasVelocity_ms = v);
            panel.CreateAndAddValueUnitRow(reactor, "Sand Inlet Temperature", UnitOfMeasure.temperature,
                reactor.SandInletTemperature_K, v => reactor.SandInletTemperature_K = v);

            BioRows.Number(panel, reactor, "Sand / Biomass Ratio (kg/kg)",
                reactor.SandToBiomassRatio, v => reactor.SandToBiomassRatio = v);
            BioRows.Number(panel, reactor, "Heat Loss Fraction",
                reactor.HeatLossFraction, v => reactor.HeatLossFraction = v);

            BioRows.Section(panel, "Biomass Composition (dry basis)");

            BioRows.Number(panel, reactor, "Cellulose Mass Fraction",
                reactor.CelluloseMassFrac, v => reactor.CelluloseMassFrac = v);
            BioRows.Number(panel, reactor, "Hemicellulose Mass Fraction",
                reactor.HemicelluloseMassFrac, v => reactor.HemicelluloseMassFrac = v);
            BioRows.Number(panel, reactor, "Lignin Mass Fraction",
                reactor.LigninMassFrac, v => reactor.LigninMassFrac = v);

            panel.CreateAndAddValueUnitRow(reactor, "Heat of Pyrolysis", UnitOfMeasure.enthalpy,
                reactor.HeatOfPyrolysis_Jkg / 1000.0, v => reactor.HeatOfPyrolysis_Jkg = v * 1000.0);

            BioRows.Section(panel, "Compound Roles");

            BioRows.Compound(panel, reactor, "Biomass Compound",
                () => reactor.BiomassCompound, v => reactor.BiomassCompound = v);
            BioRows.Compound(panel, reactor, "Char Compound",
                () => reactor.CharCompound, v => reactor.CharCompound = v);
            BioRows.Compound(panel, reactor, "Bio-oil Compound",
                () => reactor.BioOilCompound, v => reactor.BioOilCompound = v);
            BioRows.Compound(panel, reactor, "Gas Lump Compound",
                () => reactor.GasLumpCompound, v => reactor.GasLumpCompound = v);
            BioRows.Compound(panel, reactor, "Water Compound",
                () => reactor.WaterCompound, v => reactor.WaterCompound = v);
            BioRows.Compound(panel, reactor, "Oxygen Compound",
                () => reactor.OxygenCompound, v => reactor.OxygenCompound = v);
            BioRows.Compound(panel, reactor, "CO2 Compound",
                () => reactor.CO2Compound, v => reactor.CO2Compound = v);
            BioRows.Compound(panel, reactor, "Nitrogen Compound",
                () => reactor.NitrogenCompound, v => reactor.NitrogenCompound = v);
        }

        private static Control BuildCombustor(Reactors.Reactor_CFBFastPyrolysis reactor)
        {
            var panel = new AvaloniaEditorPanel();

            panel.CreateAndAddDescriptionRow(
                "Only used when the sand mode is InternalCharCombustor.");

            panel.CreateAndAddValueUnitRow(reactor, "Char LHV", UnitOfMeasure.enthalpy,
                reactor.CharLHV_Jkg / 1000.0, v => reactor.CharLHV_Jkg = v * 1000.0);

            BioRows.Number(panel, reactor, "Excess Air Fraction",
                reactor.CharCombustorExcessAir, v => reactor.CharCombustorExcessAir = v);
            BioRows.Number(panel, reactor, "Combustor Heat Loss Fraction",
                reactor.CharCombustorHeatLoss, v => reactor.CharCombustorHeatLoss = v);

            return panel;
        }

        private static void BuildResults(Reactors.Reactor_CFBFastPyrolysis reactor,
                                         AvaloniaEditorPanel panel)
        {
            BioRows.Section(panel, "Outlet Conditions");
            panel.CreateAndAddResultRow(reactor, "Outlet Temperature", UnitOfMeasure.temperature, reactor.Result_OutletTemperature_K);
            BioRows.Result(panel, reactor, "Vapor Residence Time", reactor.Result_VaporResidenceTime_s, "s");

            BioRows.Section(panel, "Product Yields (mass fraction of dry biomass)");
            BioRows.Result(panel, reactor, "Bio-oil", reactor.Result_OilYield_wfrac * 100.0, "%");
            BioRows.Result(panel, reactor, "Gas", reactor.Result_GasYield_wfrac * 100.0, "%");
            BioRows.Result(panel, reactor, "Char", reactor.Result_CharYield_wfrac * 100.0, "%");
            BioRows.Result(panel, reactor, "Unreacted Solid", reactor.Result_UnreactedSolid_wfrac * 100.0, "%");

            BioRows.Section(panel, "Sand / Heat Loop");
            panel.CreateAndAddResultRow(reactor, "Sand Circulation", UnitOfMeasure.massflow, reactor.Result_SandCirculation_kgps);
            panel.CreateAndAddResultRow(reactor, "Sand Outlet Temperature", UnitOfMeasure.temperature, reactor.Result_SandOutletTemperature_K);
            panel.CreateAndAddResultRow(reactor, "Net Pyrolysis Duty", UnitOfMeasure.heatflow, reactor.Result_PyrolysisDuty_kW);

            if (reactor.SandMode != Reactors.CFBSandMode.InternalCharCombustor) return;

            BioRows.Section(panel, "Char Combustor");
            panel.CreateAndAddResultRow(reactor, "Combustor Duty", UnitOfMeasure.heatflow, reactor.Result_CombustorDuty_kW);
            panel.CreateAndAddResultRow(reactor, "Combustor Air Flow", UnitOfMeasure.massflow, reactor.Result_CombustorAirFlow_kgps);
            panel.CreateAndAddResultRow(reactor, "Combustor Flue Temperature", UnitOfMeasure.temperature, reactor.Result_CombustorFlueT_K);
        }

    }

    /// <summary>
    /// Reaktoro Gibbs reactor editor: which database it reads, the phases it lets exist and the
    /// pressure drop across it.
    /// </summary>
    public static class ReaktoroGibbsEditor
    {

        public static Control Build(Reactors.Reactor_ReaktoroGibbs reactor)
        {
            return UnitOpEditor.Build(reactor,
                input: panel =>
                {
                    var databases = new List<string>
                    {
                        "supcrt98.xml", "supcrt98-organics.xml", "supcrt07.xml", "supcrt07-organics.xml"
                    };

                    panel.CreateAndAddDropDownRow("Database", databases,
                        Math.Max(0, databases.IndexOf(reactor.DatabaseName ?? "")), (dd, e) =>
                        {
                            if (dd.SelectedIndex < 0) return;
                            reactor.DatabaseName = databases[dd.SelectedIndex];
                        });

                    panel.CreateAndAddCheckBoxRow("Use External Database", reactor.UseExternalDatabase,
                        (cb, e) => reactor.UseExternalDatabase = cb.IsChecked.GetValueOrDefault());

                    // the path is picked from a dialog, never typed, as the Windows editor has it
                    var external = panel.CreateAndAddStringEditorRow("External Database File",
                        reactor.ExternalDatabaseFileName, null);
                    external.IsReadOnly = true;

                    panel.CreateAndAddButtonRow("Browse...", null, (btn, e) =>
                    {
                        var picked = FileRows.Pick("YAML files", new[] { "*.yaml" });
                        if (picked == null) return;

                        reactor.ExternalDatabaseFileName = picked;
                        external.Text = picked;
                    });

                    BioRows.Section(panel, "Phases");

                    panel.CreateAndAddCheckBoxRow("Aqueous Phase", reactor.AqueousPhase,
                        (cb, e) => reactor.AqueousPhase = cb.IsChecked.GetValueOrDefault());
                    panel.CreateAndAddCheckBoxRow("Gaseous Phase", reactor.GaseousPhase,
                        (cb, e) => reactor.GaseousPhase = cb.IsChecked.GetValueOrDefault());
                    panel.CreateAndAddCheckBoxRow("Liquid Phase", reactor.LiquidPhase,
                        (cb, e) => reactor.LiquidPhase = cb.IsChecked.GetValueOrDefault());
                    panel.CreateAndAddCheckBoxRow("Mineral Phase", reactor.MineralPhase,
                        (cb, e) => reactor.MineralPhase = cb.IsChecked.GetValueOrDefault());

                    BioRows.Section(panel, "Conditions");

                    panel.CreateAndAddValueUnitRow(reactor, "Pressure Drop", UnitOfMeasure.deltaP,
                        reactor.DeltaP.GetValueOrDefault(), v => reactor.DeltaP = v);
                });
        }

    }

    /// <summary>
    /// PEM fuel cell editor: the model parameters the OPEM library reads, the results it produces
    /// and the reports it writes.
    /// </summary>
    public static class FuelCellEditor
    {

        private sealed class ParameterRow : INotifyPropertyChanged
        {
            private readonly DWSIM.UnitOperations.UnitOperations.Auxiliary.PEMFuelCellModelParameter _parameter;
            private readonly string _nf;
            private readonly bool _readOnly;

            public ParameterRow(DWSIM.UnitOperations.UnitOperations.Auxiliary.PEMFuelCellModelParameter parameter,
                                string nf, bool readOnly)
            {
                _parameter = parameter;
                _nf = nf;
                _readOnly = readOnly;
            }

            public string Name { get { return _parameter.Name; } }
            public string Description { get { return _parameter.Description; } }
            public string Units { get { return _parameter.Units; } }

            public string Value
            {
                get { return _parameter.Value.ToString(_nf, CultureInfo.CurrentCulture); }
                set
                {
                    if (_readOnly) return;
                    if (!UnitOpEditorRows.TryParse(value, out var v)) return;
                    _parameter.Value = v;
                    Raise("Value");
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;
            private void Raise(string name)
            {
                if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }

        public static Control Build(UOps.PEMFuelCellUnitOpBase cell)
        {
            return UnitOpEditor.Build(cell,
                input: panel =>
                {
                    panel.CreateAndAddDescriptionRow("Model parameters read by the OPEM library.");
                },
                propertyPackage: false,
                extras: new[]
                {
                    ("Input Parameters", Grid(cell, cell.InputParameters, readOnly: false)),
                    ("Output Parameters", Grid(cell, cell.OutputParameters, readOnly: true)),
                    ("Reports", BuildReports(cell))
                });
        }

        private static Control Grid(UOps.PEMFuelCellUnitOpBase cell,
            Dictionary<string, DWSIM.UnitOperations.UnitOperations.Auxiliary.PEMFuelCellModelParameter> parameters,
            bool readOnly)
        {
            var nf = cell.GetFlowsheet().FlowsheetOptions.NumberFormat;
            var rows = new ObservableCollection<ParameterRow>();

            if (parameters != null)
                foreach (var parameter in parameters.Values)
                    rows.Add(new ParameterRow(parameter, nf, readOnly));

            var grid = new DataGrid
            {
                ItemsSource = rows,
                AutoGenerateColumns = false,
                CanUserSortColumns = false,
                IsReadOnly = readOnly,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                Height = 240
            };

            grid.Columns.Add(GridColumns.Text("Name", "Name", 1.0, readOnly: true));
            grid.Columns.Add(GridColumns.Text("Description", "Description", 2.2, readOnly: true));
            grid.Columns.Add(GridColumns.Text("Value", "Value", 1.0, readOnly));
            grid.Columns.Add(GridColumns.Text("Units", "Units", 0.8, readOnly: true));

            return grid;
        }

        private static Control BuildReports(UOps.PEMFuelCellUnitOpBase cell)
        {
            var panel = new AvaloniaEditorPanel();

            var html = panel.CreateAndAddButtonRow("View HTML Report", null,
                (btn, e) => ShowReport(cell, "HTML Report", cell.HTMLreport));

            var text = panel.CreateAndAddButtonRow("View OPEM Report", null,
                (btn, e) => ShowReport(cell, "OPEM Report", cell.OPEMreport));

            var csv = panel.CreateAndAddButtonRow("View CSV Report", null,
                (btn, e) => ShowReport(cell, "CSV Report", cell.CSVreport));

            // the reports only exist once the cell has been solved
            html.IsEnabled = cell.Calculated;
            text.IsEnabled = cell.Calculated;
            csv.IsEnabled = cell.Calculated;

            return panel;
        }

        private static void ShowReport(UOps.PEMFuelCellUnitOpBase cell, string title, string text)
        {
            var panel = AvaloniaCommon.GetDefaultContainer();
            var window = AvaloniaCommon.GetDefaultEditorForm(
                cell.GraphicObject.Tag + ": " + title, 800, 600, panel);

            panel.CreateAndAddMultilineMonoSpaceTextBoxRow(text ?? "(empty)", 520, true, null);
            window.Show();
        }

    }

}
