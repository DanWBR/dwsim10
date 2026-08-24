'    Black Oil Compound Builder
'    Copyright 2026 Daniel Wagner O. de Medeiros
'
'    This file is part of DWSIM.
'
'    DWSIM is free software: you can redistribute it and/or modify
'    it under the terms of the GNU General Public License as published by
'    the Free Software Foundation, either version 3 of the License, or
'    (at your option) any later version.

Imports System.Math
Imports DWSIM.Thermodynamics.PropertyPackages.Auxiliary
Imports props1 = DWSIM.Thermodynamics.PetroleumCharacterization.Methods.PropertyMethods

Namespace Utilities.BlackOil

    ''' <summary>
    ''' Builds a BLACK-OIL pseudo-compound (a ConstantProperties JSON dictionary with IsBlackOil = True and
    ''' the BO_* metadata) from black-oil parameters: oil API/SG, gas SG, GOR, BSW (water cut) and optional
    ''' oil viscosity points. The dictionary is written to the addcomps folder and loaded by DWSIM on start.
    ''' Shared by the WinForms and Avalonia black-oil compound creators so both produce identical compounds.
    ''' The Black Oil property package reads SGO/SGG/GOR/BSW and computes every property from its own
    ''' correlations - the critical constants below are only for a clean load / cross-package display.
    ''' </summary>
    Public Module BlackOilCompoundBuilder

        ''' <summary>Oil specific gravity (60/60 F) from API gravity.</summary>
        Public Function SGOFromAPI(api As Double) As Double
            Return 141.5 / (api + 131.5)
        End Function

        ''' <summary>API gravity from oil specific gravity.</summary>
        Public Function APIFromSGO(sgo As Double) As Double
            Return 141.5 / sgo - 131.5
        End Function

        ''' <summary>Dead-oil molar weight (g/mol) from the oil specific gravity (Black-Oil correlation).</summary>
        Public Function MolarWeight(sgo As Double) As Double
            Return New BlackOilProperties().LiquidMolecularWeight(sgo, 0.0)
        End Function

        ''' <summary>Normal boiling point (K), blended with water by BSW.</summary>
        Public Function NormalBoilingPoint(sgo As Double, bsw As Double) As Double
            Return New BlackOilProperties().LiquidNormalBoilingPoint(sgo, bsw)
        End Function

        ''' <summary>Bubble-point pressure (Pa) at temperature T (K) - the black-oil vapour pressure.</summary>
        Public Function BubblePoint(sgo As Double, bsw As Double, T As Double) As Double
            Return New BlackOilProperties().VaporPressure(T, sgo, bsw)
        End Function

        ''' <summary>
        ''' Builds the compound and materialises it as a ConstantProperties through the exact addcomps JSON
        ''' round-trip (Dictionary -> JSON -> ConstantProperties), so callers get a ready black-oil compound.
        ''' </summary>
        Public Function BuildConstantProperties(name As String, sgo As Double, sgg As Double, gor As Double, bsw As Double,
                                                oilVisc1 As Double, oilViscTemp1 As Double, oilVisc2 As Double, oilViscTemp2 As Double,
                                                comments As String,
                                                Optional rsMult As Double = 1.0, Optional boMult As Double = 1.0,
                                                Optional pbMult As Double = 1.0, Optional oilViscMult As Double = 1.0) As BaseClasses.ConstantProperties
            Dim dict = BuildCompound(name, sgo, sgg, gor, bsw, oilVisc1, oilViscTemp1, oilVisc2, oilViscTemp2, comments, rsMult, boMult, pbMult, oilViscMult)
            Dim json = Newtonsoft.Json.JsonConvert.SerializeObject(dict)
            Return Newtonsoft.Json.JsonConvert.DeserializeObject(Of BaseClasses.ConstantProperties)(json)
        End Function

        ''' <summary>
        ''' Builds the compound dictionary. sgo/sgg = specific gravities, gor = m3/m3 STD, bsw = water cut %.
        ''' Viscosity points are optional (0 = use Beggs-Robinson); temperatures in K, viscosities in m2/s.
        ''' </summary>
        Public Function BuildCompound(name As String, sgo As Double, sgg As Double, gor As Double, bsw As Double,
                                      oilVisc1 As Double, oilViscTemp1 As Double, oilVisc2 As Double, oilViscTemp2 As Double,
                                      comments As String,
                                      Optional rsMult As Double = 1.0, Optional boMult As Double = 1.0,
                                      Optional pbMult As Double = 1.0, Optional oilViscMult As Double = 1.0) As Dictionary(Of String, Object)

            If String.IsNullOrWhiteSpace(name) Then name = "BlackOil_Custom"
            If sgo <= 0.0 Then Throw New ArgumentException("Oil specific gravity must be positive.")
            If sgg <= 0.0 Then sgg = 0.7
            If bsw < 0.0 OrElse bsw > 100.0 Then Throw New ArgumentException("BSW (water cut) must be between 0 and 100 %.")

            Dim bop As New BlackOilProperties()
            ' overall (gas + oil + water) molar weight, matching BlackOilPropertyPackage.CalcBOFluid so a
            ' pre-set MW equals the value the PP would otherwise derive at flash time (consistent results).
            Dim gasTerm As Double = gor * sgg
            Dim liqTerm As Double = sgo * 1000.0 * (100.0 - bsw) / 100.0 + 1000.0 * bsw / 100.0
            Dim mw As Double = (gasTerm * bop.VaporMolecularWeight(sgg) + liqTerm * bop.LiquidMolecularWeight(sgo, bsw)) /
                               (gasTerm + liqTerm)
            Dim nbp As Double = bop.LiquidNormalBoilingPoint(sgo, bsw)
            Dim deadMw As Double = bop.LiquidMolecularWeight(sgo, 0.0)
            Dim deadNbp As Double = 1080 - Exp(6.97996 - 0.01964 * deadMw ^ (2.0 / 3.0))
            Dim tc As Double = props1.Tc_LeeKesler(deadNbp, sgo)
            Dim pc As Double = props1.Pc_LeeKesler(deadNbp, sgo)
            Dim w As Double = props1.AcentricFactor_LeeKesler(tc, pc, deadNbp)
            Dim api As Double = APIFromSGO(sgo)

            Return New Dictionary(Of String, Object) From {
                {"Acentric_Factor", w},
                {"CAS_Number", "BO-" & name.ToUpper},
                {"Charge", 0},
                {"ChemicalStructure", ""},
                {"Comments", comments &
                    "  [Black Oil Compound Creator. API=" & api.ToString("N1") &
                    ", GOR=" & gor.ToString("N0") & " m3/m3, BSW=" & bsw.ToString("N1") & " %]"},
                {"Critical_Compressibility", 0.0},
                {"Critical_Pressure", pc},
                {"Critical_Temperature", tc},
                {"Critical_Volume", 0.0},
                {"CurrentDB", "User"},
                {"Dipole_Moment", 0.0},
                {"EnthalpyOfFusionAtTf", 0.0},
                {"Formula", "BlackOil"},
                {"HVap_A", 0.0}, {"HVap_B", 0.0}, {"HVap_C", 0.0}, {"HVap_D", 0.0}, {"HVap_E", 0.0},
                {"HVap_TMAX", 0.0}, {"HVap_TMIN", 0.0},
                {"HydrationNumber", 0.0},
                {"ID", 20200 + DateTime.Now.Second},
                {"Ideal_Gas_Heat_Capacity_Const_A", 1500.0},
                {"Ideal_Gas_Heat_Capacity_Const_B", 0.0},
                {"Ideal_Gas_Heat_Capacity_Const_C", 0.0},
                {"Ideal_Gas_Heat_Capacity_Const_D", 0.0},
                {"Ideal_Gas_Heat_Capacity_Const_E", 0.0},
                {"IdealgasCpEquation", "4"},
                {"IG_Enthalpy_of_Formation_25C", 0.0},
                {"IG_Entropy_of_Formation_25C", 0.0},
                {"IG_Gibbs_Energy_of_Formation_25C", 0.0},
                {"InChI", ""},
                {"IsBlackOil", True},
                {"BO_SGO", sgo}, {"BO_SGG", sgg}, {"BO_GOR", gor}, {"BO_BSW", bsw},
                {"BO_OilVisc1", oilVisc1}, {"BO_OilViscTemp1", oilViscTemp1},
                {"BO_OilVisc2", oilVisc2}, {"BO_OilViscTemp2", oilViscTemp2},
                {"BO_PNA_P", 0.0}, {"BO_PNA_N", 0.0}, {"BO_PNA_A", 0.0},
                {"BO_RsMult", rsMult}, {"BO_BoMult", boMult}, {"BO_PbMult", pbMult}, {"BO_OilViscMult", oilViscMult},
                {"IsCOOLPROPSupported", False}, {"IsFPROPSSupported", False},
                {"IsHydratedSalt", False}, {"IsHYPO", 0}, {"IsIon", False}, {"IsModified", False},
                {"IsPF", 0}, {"IsSalt", False},
                {"Liquid_Density_Const_A", 1000.0 * sgo},
                {"Liquid_Density_Const_B", 0.0}, {"Liquid_Density_Const_C", 0.0},
                {"Liquid_Density_Const_D", 0.0}, {"Liquid_Density_Const_E", 0.0},
                {"Liquid_Density_Tmax", 1000.0}, {"Liquid_Density_Tmin", 200.0},
                {"Liquid_Heat_Capacity_Const_A", 2000.0},
                {"Liquid_Heat_Capacity_Const_B", 0.0}, {"Liquid_Heat_Capacity_Const_C", 0.0},
                {"Liquid_Heat_Capacity_Const_D", 0.0}, {"Liquid_Heat_Capacity_Const_E", 0.0},
                {"Liquid_Heat_Capacity_Tmax", 1000.0}, {"Liquid_Heat_Capacity_Tmin", 200.0},
                {"Liquid_Thermal_Conductivity_Const_A", 0.13},
                {"Liquid_Thermal_Conductivity_Const_B", 0.0},
                {"Liquid_Thermal_Conductivity_Const_C", 0.0},
                {"Liquid_Thermal_Conductivity_Const_D", 0.0},
                {"Liquid_Thermal_Conductivity_Const_E", 0.0},
                {"Liquid_Thermal_Conductivity_Tmax", 1000.0},
                {"Liquid_Thermal_Conductivity_Tmin", 200.0},
                {"Liquid_Viscosity_Const_A", 0.001},
                {"Liquid_Viscosity_Const_B", 0.0}, {"Liquid_Viscosity_Const_C", 0.0},
                {"Liquid_Viscosity_Const_D", 0.0}, {"Liquid_Viscosity_Const_E", 0.0},
                {"LiquidDensityEquation", "2"},
                {"LiquidHeatCapacityEquation", "2"},
                {"LiquidThermalConductivityEquation", "2"},
                {"LiquidViscosityEquation", "2"},
                {"Molar_Weight", mw},
                {"Name", name},
                {"Normal_Boiling_Point", nbp},
                {"OriginalDB", "User"},
                {"SMILES", ""},
                {"Solid_Density_Const_A", 0.0},
                {"Solid_Density_Const_B", 0.0}, {"Solid_Density_Const_C", 0.0},
                {"Solid_Density_Const_D", 0.0}, {"Solid_Density_Const_E", 0.0},
                {"Solid_Density_Tmax", 0.0}, {"Solid_Density_Tmin", 0.0},
                {"SolidDensityAtTs", 0.0}, {"SolidTs", 0.0},
                {"StandardStateMolarVolume", 0.0}, {"StoichSum", 0},
                {"Surface_Tension_Const_A", 0.02},
                {"Surface_Tension_Const_B", 0.0}, {"Surface_Tension_Const_C", 0.0},
                {"Surface_Tension_Const_D", 0.0}, {"Surface_Tension_Const_E", 0.0},
                {"Surface_Tension_Tmax", 0.0}, {"Surface_Tension_Tmin", 0.0},
                {"TemperatureOfFusion", 0.0},
                {"UNIQUAC_Q", 0.0}, {"UNIQUAC_R", 0.0},
                {"Vapor_Pressure_Constant_A", -1000.0},
                {"Vapor_Pressure_Constant_B", 0.0}, {"Vapor_Pressure_Constant_C", 0.0},
                {"Vapor_Pressure_Constant_D", 0.0}, {"Vapor_Pressure_Constant_E", 0.0},
                {"Vapor_Pressure_TMAX", 0.0}, {"Vapor_Pressure_TMIN", 0.0},
                {"Vapor_Thermal_Conductivity_Const_A", 0.0},
                {"Vapor_Thermal_Conductivity_Const_B", 0.0},
                {"Vapor_Thermal_Conductivity_Const_C", 0.0},
                {"Vapor_Thermal_Conductivity_Const_D", 0.0},
                {"Vapor_Thermal_Conductivity_Const_E", 0.0},
                {"Vapor_Thermal_Conductivity_Tmax", 0.0},
                {"Vapor_Thermal_Conductivity_Tmin", 0.0},
                {"Vapor_Viscosity_Const_A", 0.0},
                {"Vapor_Viscosity_Const_B", 0.0}, {"Vapor_Viscosity_Const_C", 0.0},
                {"Vapor_Viscosity_Const_D", 0.0}, {"Vapor_Viscosity_Const_E", 0.0},
                {"Vapor_Viscosity_Tmax", 0.0}, {"Vapor_Viscosity_Tmin", 0.0},
                {"VaporPressureEquation", "3"},
                {"Z_Rackett", 0.0},
                {"Elements", New Dictionary(Of String, Double)()},
                {"MODFACGroups", New Dictionary(Of String, Object)()},
                {"NISTMODFACGroups", New Dictionary(Of String, Object)()},
                {"UNIFACGroups", New Dictionary(Of String, Object)()},
                {"FullerDiffusionVolume", 0.0},
                {"LennardJonesDiameter", 0.0}, {"LennardJonesEnergy", 0.0},
                {"Parachor", 0.0},
                {"Tag", "Black Oil"},
                {"ExtraProperties", New Dictionary(Of String, Object) From {
                    {"IsBlackOilCreated", True},
                    {"API", api},
                    {"ReferenceSource", "User-created"}
                }}
            }

        End Function

    End Module

End Namespace
