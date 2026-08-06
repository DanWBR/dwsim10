using System;
using System.Collections.Generic;
using Avalonia.Controls;
using DWSIM.Interfaces.Enums;
using DWSIM.UI.Shared.Avalonia;
using Reactor = DWSIM.UnitOperations.Reactors.Reactor;
using Reactors = DWSIM.UnitOperations.Reactors;

namespace DWSIM.UI.Desktop.Editors
{

    /// <summary>
    /// The heat exchange parameters the CSTR and the PFR share, in the order the Windows editors
    /// build them. The internal coefficient is estimated from an impeller in the stirred tank and
    /// from the tube flow in the plug flow reactor, which is the only difference between the two.
    /// </summary>
    public static class ReactorHeatExchange
    {

        private static readonly List<string> CoolantDirections = new List<string>
        {
            "Constant Temperature", "Co-Current", "Counter-Current"
        };

        private static readonly List<string> AreaModes = new List<string>
        {
            "Auto from Geometry", "User Specified"
        };

        private static readonly List<string> WallMaterials = new List<string>
        {
            "Steel", "Carbon Steel", "Cast Iron", "Stainless Steel", "PVC", "Commercial Copper"
        };

        private static readonly List<string> Impellers = new List<string>
        {
            "Flat Blade Turbine", "Rushton Turbine", "Pitched Blade Turbine",
            "Marine Propeller", "Anchor", "Helical Ribbon"
        };

        public static Control Build(Reactor reactor, bool stirred)
        {
            var panel = new AvaloniaEditorPanel();
            var nf = reactor.GetFlowsheet().FlowsheetOptions.NumberFormat;

            UnitOpEditorRows.ValueRow overallU = null, wallThickness = null, internalHTC = null,
                                      externalHTC = null, area = null, coolantT = null, coolantFlow = null,
                                      coolantCp = null, impellerDiameter = null, jacketDiameter = null,
                                      coolantDensity = null, coolantViscosity = null, coolantK = null;
            TextBox impellerSpeed = null;
            ComboBox wallMaterial = null, impellerType = null;
            CheckBox calcInternal = null, calcExternal = null;

            void Apply()
            {
                var wall = reactor.UseWallProperties;
                var utility = reactor.UseUtilityStream;
                var constantT = reactor.HeatExchangeCoolantFlowDirection ==
                                Reactors.HeatExchangeCoolantMode.ConstantTemperature;

                if (overallU != null) overallU.IsEnabled = !wall;
                if (wallMaterial != null) wallMaterial.IsEnabled = wall;
                if (wallThickness != null) wallThickness.IsEnabled = wall;

                if (area != null)
                    area.IsEnabled = reactor.HeatExchangeAreaCalculationMode ==
                                     Reactors.HeatExchangeAreaMode.UserSpecified;

                if (coolantT != null) coolantT.IsEnabled = !utility;
                if (coolantFlow != null) coolantFlow.IsEnabled = !utility && !constantT;
                if (coolantCp != null) coolantCp.IsEnabled = !utility && !constantT;

                if (calcInternal != null) calcInternal.IsEnabled = wall;
                if (calcExternal != null) calcExternal.IsEnabled = wall;

                if (internalHTC != null) internalHTC.IsEnabled = wall && !reactor.CalculateInternalHTC;
                if (externalHTC != null) externalHTC.IsEnabled = wall && !reactor.CalculateExternalHTC;

                var impeller = wall && reactor.CalculateInternalHTC;
                if (impellerDiameter != null) impellerDiameter.IsEnabled = impeller;
                if (impellerSpeed != null) impellerSpeed.IsEnabled = impeller;
                if (impellerType != null) impellerType.IsEnabled = impeller;

                if (jacketDiameter != null) jacketDiameter.IsEnabled = wall && reactor.CalculateExternalHTC;

                var coolantProps = reactor.CalculateExternalHTC && !utility;
                if (coolantDensity != null) coolantDensity.IsEnabled = coolantProps;
                if (coolantViscosity != null) coolantViscosity.IsEnabled = coolantProps;
                if (coolantK != null) coolantK.IsEnabled = coolantProps;
            }

            panel.CreateAndAddDropDownRow("Coolant Flow Direction", CoolantDirections,
                (int)reactor.HeatExchangeCoolantFlowDirection, (dd, e) =>
                {
                    reactor.HeatExchangeCoolantFlowDirection = (Reactors.HeatExchangeCoolantMode)dd.SelectedIndex;
                    Apply();
                });

            panel.CreateAndAddDropDownRow("Heat Transfer Area Mode", AreaModes,
                (int)reactor.HeatExchangeAreaCalculationMode, (dd, e) =>
                {
                    reactor.HeatExchangeAreaCalculationMode = (Reactors.HeatExchangeAreaMode)dd.SelectedIndex;
                    Apply();
                });

            panel.CreateAndAddCheckBoxRow("Calculate U from Wall Properties", reactor.UseWallProperties,
                (cb, e) => { reactor.UseWallProperties = cb.IsChecked.GetValueOrDefault(); Apply(); });

            overallU = panel.CreateAndAddValueUnitRow(reactor, "Overall HTC",
                UnitOfMeasure.heat_transf_coeff, reactor.OverallHeatTransferCoefficient,
                v => reactor.OverallHeatTransferCoefficient = v);

            wallMaterial = panel.CreateAndAddDropDownRow("Wall Material", WallMaterials,
                Math.Max(0, WallMaterials.IndexOf(reactor.WallMaterial ?? "")), (dd, e) =>
                {
                    if (dd.SelectedIndex < 0) return;
                    reactor.WallMaterial = WallMaterials[dd.SelectedIndex];
                });

            wallThickness = panel.CreateAndAddValueUnitRow(reactor, "Wall Thickness",
                UnitOfMeasure.thickness, reactor.WallThickness, v => reactor.WallThickness = v);

            internalHTC = panel.CreateAndAddValueUnitRow(reactor, "Internal HTC",
                UnitOfMeasure.heat_transf_coeff, reactor.InternalHTC, v => reactor.InternalHTC = v);

            externalHTC = panel.CreateAndAddValueUnitRow(reactor, "External HTC",
                UnitOfMeasure.heat_transf_coeff, reactor.ExternalHTC, v => reactor.ExternalHTC = v);

            area = panel.CreateAndAddValueUnitRow(reactor, "Heat Transfer Area",
                UnitOfMeasure.area, reactor.HeatExchangeArea, v => reactor.HeatExchangeArea = v);

            coolantT = panel.CreateAndAddValueUnitRow(reactor, "Coolant Inlet Temp.",
                UnitOfMeasure.temperature, reactor.CoolantInletTemperature,
                v => reactor.CoolantInletTemperature = v);

            coolantFlow = panel.CreateAndAddValueUnitRow(reactor, "Coolant Mass Flow Rate",
                UnitOfMeasure.massflow, reactor.CoolantMassFlowRate, v => reactor.CoolantMassFlowRate = v);

            // the engine keeps the coolant heat capacity in J/[kg.K] while the editor works in kJ
            coolantCp = panel.CreateAndAddValueUnitRow(reactor, "Coolant Specific Heat",
                UnitOfMeasure.heatCapacityCp, reactor.CoolantSpecificHeat / 1000.0,
                v => reactor.CoolantSpecificHeat = v * 1000.0);

            panel.CreateAndAddCheckBoxRow("Use Utility Material Stream", reactor.UseUtilityStream,
                (cb, e) => { reactor.UseUtilityStream = cb.IsChecked.GetValueOrDefault(); Apply(); });

            calcInternal = panel.CreateAndAddCheckBoxRow(stirred
                    ? "Auto-Calculate Internal HTC (Stirred Vessel)"
                    : "Auto-Calculate Internal HTC (Dittus-Boelter)",
                reactor.CalculateInternalHTC,
                (cb, e) => { reactor.CalculateInternalHTC = cb.IsChecked.GetValueOrDefault(); Apply(); });

            var cstr = reactor as DWSIM.UnitOperations.Reactors.Reactor_CSTR;

            if (stirred && cstr != null)
            {
                impellerDiameter = panel.CreateAndAddValueUnitRow(reactor, "Impeller Diameter",
                    UnitOfMeasure.diameter, cstr.ImpellerDiameter, v => cstr.ImpellerDiameter = v);

                impellerSpeed = panel.CreateAndAddTextBoxRow(nf, "Impeller Speed (RPM)",
                    cstr.ImpellerSpeed,
                    (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) cstr.ImpellerSpeed = v; });

                impellerType = panel.CreateAndAddDropDownRow("Impeller Type", Impellers,
                    (int)cstr.Impeller, (dd, e) =>
                    {
                        if (dd.SelectedIndex < 0) return;
                        cstr.Impeller = (Reactors.ImpellerType)dd.SelectedIndex;
                    });
            }

            calcExternal = panel.CreateAndAddCheckBoxRow("Auto-Calculate External HTC (Annular Jacket)",
                reactor.CalculateExternalHTC,
                (cb, e) => { reactor.CalculateExternalHTC = cb.IsChecked.GetValueOrDefault(); Apply(); });

            jacketDiameter = panel.CreateAndAddValueUnitRow(reactor, "Jacket Diameter",
                UnitOfMeasure.diameter, reactor.JacketDiameter, v => reactor.JacketDiameter = v);

            coolantDensity = panel.CreateAndAddValueUnitRow(reactor, "Coolant Density",
                UnitOfMeasure.density, reactor.CoolantDensity, v => reactor.CoolantDensity = v);

            coolantViscosity = panel.CreateAndAddValueUnitRow(reactor, "Coolant Viscosity",
                UnitOfMeasure.viscosity, reactor.CoolantViscosity, v => reactor.CoolantViscosity = v);

            coolantK = panel.CreateAndAddValueUnitRow(reactor, "Coolant Thermal Conductivity",
                UnitOfMeasure.thermalConductivity, reactor.CoolantThermalConductivity,
                v => reactor.CoolantThermalConductivity = v);

            panel.CreateAndAddResultRow(reactor, "Calculated Internal HTC",
                UnitOfMeasure.heat_transf_coeff,
                reactor.CalculatedInternalHTC > 0.0 ? reactor.CalculatedInternalHTC : (double?)null);

            panel.CreateAndAddResultRow(reactor, "Calculated External HTC",
                UnitOfMeasure.heat_transf_coeff,
                reactor.CalculatedExternalHTC > 0.0 ? reactor.CalculatedExternalHTC : (double?)null);

            Apply();

            return panel;
        }

    }

}
