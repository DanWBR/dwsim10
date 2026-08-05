Imports System.Globalization
Imports System.Text.RegularExpressions

Namespace Utilities.Biomass

    ''' <summary>
    ''' Elemental composition of one mol of biomass, restricted to C, H, O, N and S.
    ''' </summary>
    Public Class BiomassComposition
        Public Property C As Double
        Public Property H As Double
        Public Property O As Double
        Public Property N As Double
        Public Property S As Double

        ''' <summary>True when the formula carries carbon, without which nothing can be derived.</summary>
        Public ReadOnly Property IsValid As Boolean
            Get
                Return C > 0.0
            End Get
        End Property
    End Class

    ''' <summary>
    ''' Kinetic defaults stored in the compound's ExtraProperties block. The Bioreactor
    ''' editor reads them to pre-fill its own fields.
    ''' </summary>
    Public Class BiomassKineticDefaults
        Public Property MuMax_h As Double = 0.3
        Public Property Ks_gL As Double = 0.5
        Public Property YXS As Double = 0.5
        Public Property YPS As Double = 0.0
        Public Property Maintenance As Double = 0.02
        Public Property DeathRate_h As Double = 0.005
    End Class

    ''' <summary>
    ''' Builds the pseudo-compound JSON that represents a biomass entity from an elemental
    ''' formula. UI-agnostic on purpose: the WinForms and Avalonia creators are both thin
    ''' shells over this.
    ''' </summary>
    Public Module BiomassCompoundBuilder

        Public ReadOnly BiomassTypes As String() =
            {"Generic", "Bacterial", "Yeast", "Mammalian", "Algal", "Mixed Culture (ADM1)", "Custom"}

        ''' <summary>
        ''' Reads an elemental formula such as "C100H180O50N20S0.5". Subscripts may be integers
        ''' or decimals; an element without a subscript counts as one.
        ''' </summary>
        Public Function ParseFormula(formula As String) As BiomassComposition

            Dim result As New BiomassComposition

            If String.IsNullOrWhiteSpace(formula) Then Return result

            Dim rx As New Regex("([CHONS])(\d*\.?\d*)")
            For Each m As Match In rx.Matches(formula)
                Dim el = m.Groups(1).Value
                Dim countStr = m.Groups(2).Value
                Dim count As Double = 1.0
                If countStr.Length > 0 Then count = Double.Parse(countStr, CultureInfo.InvariantCulture)
                Select Case el
                    Case "C" : result.C = count
                    Case "H" : result.H = count
                    Case "O" : result.O = count
                    Case "N" : result.N = count
                    Case "S" : result.S = count
                End Select
            Next

            Return result

        End Function

        ''' <summary>Molar weight of one mol of biomass, from standard atomic weights.</summary>
        Public Function MolarWeight(c As BiomassComposition) As Double
            Return c.C * 12.011 + c.H * 1.008 + c.O * 15.999 + c.N * 14.007 + c.S * 32.06
        End Function

        ''' <summary>
        ''' Degree of reduction per C-mol, referenced to CO2 / H2O / NH3 / SO4²⁻:
        ''' γ = 4 + H/C - 2·O/C - 3·N/C + 6·S/C.
        ''' </summary>
        Public Function DegreeOfReduction(c As BiomassComposition) As Double
            If c.C <= 0.0 Then Return Double.NaN
            Return 4.0 + c.H / c.C - 2.0 * c.O / c.C - 3.0 * c.N / c.C + 6.0 * c.S / c.C
        End Function

        ''' <summary>
        ''' Elemental formula normalised to one C-mol. The sulfur term is only appended when
        ''' the formula actually contains sulfur.
        ''' </summary>
        Public Function FormulaPerCmol(c As BiomassComposition) As String
            If c.C <= 0.0 Then Return ""
            Dim s = "CH" & (c.H / c.C).ToString("N3") &
                    "O" & (c.O / c.C).ToString("N3") &
                    "N" & (c.N / c.C).ToString("N3")
            If c.S > 0 Then s &= "S" & (c.S / c.C).ToString("N4")
            Return s
        End Function

        ''' <summary>
        ''' Heijnen / Roels approximation for the standard enthalpy of formation, in kJ/kg.
        ''' Combustion products are CO2, H2O and H2SO4, matching the SO4²⁻ reference used by
        ''' the degree of reduction; each S takes 2 H out of the water term.
        ''' </summary>
        Public Function EnthalpyOfFormation_kJkg(c As BiomassComposition) As Double
            Dim mw = MolarWeight(c)
            If mw <= 0.0 Then Return 0.0
            Dim gamma = DegreeOfReduction(c)
            Dim dH_comb = 115.0 * gamma * c.C
            Dim dHf = c.C * (-393.5) + ((c.H - 2.0 * c.S) / 2.0) * (-285.8) + c.S * (-814.0) + dH_comb
            Return dHf / mw * 1000.0
        End Function

        ''' <summary>
        ''' Assembles the compound dictionary, ready to be serialized as the JSON that DWSIM
        ''' reads from the addcomps folder on startup.
        ''' </summary>
        Public Function BuildCompound(name As String,
                                      formula As String,
                                      biomassType As String,
                                      cmolsPerMol As Double,
                                      comments As String,
                                      kinetics As BiomassKineticDefaults) As Dictionary(Of String, Object)

            Dim c = ParseFormula(formula)
            If Not c.IsValid Then Throw New ArgumentException("Invalid formula - no carbon found.")

            Dim mw = MolarWeight(c)
            Dim gamma = DegreeOfReduction(c)
            Dim dHf_kJkg = EnthalpyOfFormation_kJkg(c)

            If cmolsPerMol <= 0.0 Then cmolsPerMol = c.C
            If String.IsNullOrWhiteSpace(name) Then name = "Biomass_Custom"
            If kinetics Is Nothing Then kinetics = New BiomassKineticDefaults

            Dim elements As New Dictionary(Of String, Double) From {
                {"C", c.C}, {"H", c.H}, {"O", c.O}, {"N", c.N}}
            If c.S > 0 Then elements.Add("S", c.S)

            Return New Dictionary(Of String, Object) From {
                {"Acentric_Factor", 0.5},
                {"CAS_Number", "BIO-" & name.ToUpper},
                {"Charge", 0},
                {"ChemicalStructure", ""},
                {"Comments", comments &
                    "  [Generated by Biomass Compound Creator. γ=" & gamma.ToString("N2") & "]"},
                {"Critical_Compressibility", 0.0},
                {"Critical_Pressure", 320000.0},
                {"Critical_Temperature", 8000.0},
                {"Critical_Volume", 0.0},
                {"CurrentDB", "User"},
                {"Dipole_Moment", 0.0},
                {"EnthalpyOfFusionAtTf", 0.0},
                {"Formula", formula.Trim()},
                {"HVap_A", 0.0}, {"HVap_B", 0.0}, {"HVap_C", 0.0}, {"HVap_D", 0.0}, {"HVap_E", 0.0},
                {"HVap_TMAX", 0.0}, {"HVap_TMIN", 0.0},
                {"HydrationNumber", 0.0},
                {"ID", 20100 + DateTime.Now.Second},
                {"Ideal_Gas_Heat_Capacity_Const_A", 1500.0},
                {"Ideal_Gas_Heat_Capacity_Const_B", 0.0},
                {"Ideal_Gas_Heat_Capacity_Const_C", 0.0},
                {"Ideal_Gas_Heat_Capacity_Const_D", 0.0},
                {"Ideal_Gas_Heat_Capacity_Const_E", 0.0},
                {"IdealgasCpEquation", "4"},
                {"IG_Enthalpy_of_Formation_25C", dHf_kJkg},
                {"IG_Entropy_of_Formation_25C", 0.0},
                {"IG_Gibbs_Energy_of_Formation_25C", dHf_kJkg * 0.67},
                {"InChI", ""},
                {"IsBlackOil", False}, {"IsCOOLPROPSupported", False}, {"IsFPROPSSupported", False},
                {"IsHydratedSalt", False}, {"IsHYPO", 0}, {"IsIon", False}, {"IsModified", False},
                {"IsPF", 0}, {"IsSalt", False},
                {"Liquid_Density_Const_A", 1100.0},
                {"Liquid_Density_Const_B", 0.0}, {"Liquid_Density_Const_C", 0.0},
                {"Liquid_Density_Const_D", 0.0}, {"Liquid_Density_Const_E", 0.0},
                {"Liquid_Density_Tmax", 1000.0}, {"Liquid_Density_Tmin", 200.0},
                {"Liquid_Heat_Capacity_Const_A", 1300.0},
                {"Liquid_Heat_Capacity_Const_B", 0.0}, {"Liquid_Heat_Capacity_Const_C", 0.0},
                {"Liquid_Heat_Capacity_Const_D", 0.0}, {"Liquid_Heat_Capacity_Const_E", 0.0},
                {"Liquid_Heat_Capacity_Tmax", 1000.0}, {"Liquid_Heat_Capacity_Tmin", 200.0},
                {"Liquid_Thermal_Conductivity_Const_A", 0.6},
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
                {"Normal_Boiling_Point", 6800.0},
                {"OriginalDB", "User"},
                {"SMILES", ""},
                {"Solid_Density_Const_A", 0.0},
                {"Solid_Density_Const_B", 0.0}, {"Solid_Density_Const_C", 0.0},
                {"Solid_Density_Const_D", 0.0}, {"Solid_Density_Const_E", 0.0},
                {"Solid_Density_Tmax", 0.0}, {"Solid_Density_Tmin", 0.0},
                {"SolidDensityAtTs", 1100.0}, {"SolidTs", 553.0},
                {"StandardStateMolarVolume", 0.0}, {"StoichSum", 0},
                {"Surface_Tension_Const_A", 0.0},
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
                {"Elements", elements},
                {"MODFACGroups", New Dictionary(Of String, Object)()},
                {"NISTMODFACGroups", New Dictionary(Of String, Object)()},
                {"UNIFACGroups", New Dictionary(Of String, Object)()},
                {"FullerDiffusionVolume", 0.0},
                {"LennardJonesDiameter", 0.0}, {"LennardJonesEnergy", 0.0},
                {"Parachor", 0.0},
                {"Tag", "Biomass"},
                {"ExtraProperties", New Dictionary(Of String, Object) From {
                    {"IsBiomass", True},
                    {"BiomassType", If(biomassType, "Generic")},
                    {"DegreeOfReduction", gamma},
                    {"ElementalFormulaPerCmol", FormulaPerCmol(c)},
                    {"CmolsPerMol", cmolsPerMol},
                    {"ReferenceSource", "User-created"},
                    {"DefaultMuMax_h", kinetics.MuMax_h},
                    {"DefaultKs_gL", kinetics.Ks_gL},
                    {"DefaultYXS", kinetics.YXS},
                    {"DefaultYPS", kinetics.YPS},
                    {"DefaultMaintenance_gSg_cellh", kinetics.Maintenance},
                    {"DefaultDeathRate_h", kinetics.DeathRate_h}
                }}
            }

        End Function

    End Module

End Namespace
