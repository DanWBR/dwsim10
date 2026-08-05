'    BioReactor Calculation Routines
'    Copyright 2026 Daniel Wagner O. de Medeiros
'
'    This file is part of DWSIM.
'
'    DWSIM is free software: you can redistribute it and/or modify
'    it under the terms of the GNU General Public License as published by
'    the Free Software Foundation, either version 3 of the License, or
'    (at your option) any later version.
'
'    DWSIM is distributed in the hope that it will be useful,
'    but WITHOUT ANY WARRANTY; without even the implied warranty of
'    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
'    GNU General Public License for more details.
'
'    You should have received a copy of the GNU General Public License
'    along with DWSIM.  If not, see <http://www.gnu.org/licenses/>.

Imports DWSIM.Thermodynamics.BaseClasses
Imports System.Math
Imports System.Linq
Imports DWSIM.Interfaces
Imports DWSIM.Interfaces.Enums
Imports DWSIM.Interfaces.Enums.GraphicObjects
Imports DWSIM.DrawingTools.Point
Imports DWSIM.Drawing.SkiaSharp.GraphicObjects
Imports SkiaSharp
Imports DWSIM.SharedClasses
Imports DWSIM.Thermodynamics.Streams
Imports DWSIM.Thermodynamics
Imports DWSIM.MathOps
Imports DWSIM.UnitOperations.Streams
Imports System.Collections.Generic
Imports DWSIM.UI.Shared.Avalonia

Namespace Reactors

    ''' <summary>Defines the growth kinetic model used by the BioReactor.</summary>
    Public Enum BioKineticModel
        ''' <summary>Monod: mu = mu_max * S / (Ks + S)</summary>
        Monod = 0
        ''' <summary>Contois: mu = mu_max * S / (Ks*X + S)</summary>
        Contois = 1
        ''' <summary>Moser: mu = mu_max * S^n / (Ks^n + S^n)</summary>
        Moser = 2
        ''' <summary>Haldane (substrate inhibition): mu = mu_max * S / (Ks + S + S^2/Ki)</summary>
        Haldane = 3
        ''' <summary>User-defined via Python script returning specific growth rate (1/s).</summary>
        UserScript = 4
        ''' <summary>Enzymatic hydrolysis of cellulose (and optionally hemicellulose) to glucose (and xylose).
        ''' Competitive product inhibition kinetics, no microbial growth. Substrate role = cellulose,
        ''' Product role = glucose, plus new roles Hemicellulose, Xylose and Enzyme.</summary>
        EnzymaticHydrolysis = 5
    End Enum

    ''' <summary>Defines the operation mode of the BioReactor.</summary>
    Public Enum BioReactorMode
        ''' <summary>Continuous operation (steady-state CSTR).</summary>
        Continuous = 0
        ''' <summary>Batch operation - inlet feed represents initial charge; residence time is batch duration.</summary>
        Batch = 1
        ''' <summary>Fed-batch - inlet continuously fed; internal volume ramps (integration time = BatchDuration).</summary>
        FedBatch = 2
    End Enum

    ''' <summary>Thermal operation mode of the BioReactor (how the metabolic heat is handled).</summary>
    Public Enum BioReactorThermalMode
        ''' <summary>Broth temperature held at the inlet temperature; cooling duty is computed so that Q_cool = -Q_metabolic.</summary>
        Isothermal = 0
        ''' <summary>No heat exchange with surroundings; outlet temperature rises (or falls) to absorb the metabolic heat.</summary>
        Adiabatic = 1
        ''' <summary>Outlet temperature is user-defined; the required net heat duty is back-computed from the enthalpy balance.</summary>
        DefinedOutletTemperature = 2
    End Enum

    ''' <summary>
    ''' Represents a generic aerobic/anaerobic BioReactor that simulates microbial growth using
    ''' Monod-family kinetics. Biomass is a real compound in the material stream (pseudo-compound
    ''' from the shipped biomass database or a user-created one via the Biomass Compound Creator),
    ''' flagged with IsBiomass=true in ExtraProperties. Stoichiometry for growth is auto-generated
    ''' from the elemental formula of the biomass compound.
    ''' </summary>
    <System.Serializable()> Public Partial Class Reactor_BioReactor

        Inherits Reactor

        Implements IExternalUnitOperation

        Public ReadOnly Property IsBio As Boolean = True

        Public Overrides Property ObjectClass As SimulationObjectClass = SimulationObjectClass.Reactors

        ''' <summary>Gets or sets the display name for this unit operation.</summary>
        Public Overrides Property ComponentName As String = GetDisplayName()

        ''' <summary>Gets or sets the display description for this unit operation.</summary>
        Public Overrides Property ComponentDescription As String = GetDisplayDescription()

        Protected m_vol As Double = 1.0

        ''' <summary>Gets or sets the reactor working volume (m3).</summary>
        Public Property Volume As Double = 1.0

        ''' <summary>Gets or sets the batch/fed-batch duration (s). Ignored in Continuous mode.</summary>
        Public Property BatchDuration As Double = 3600.0

        ''' <summary>Selected name of the biomass compound in the flowsheet.</summary>
        Public Property BiomassCompound As String = ""

        ''' <summary>Selected name of the limiting-substrate compound in the flowsheet.</summary>
        Public Property SubstrateCompound As String = ""

        ''' <summary>Selected name of the main product compound (optional).</summary>
        Public Property ProductCompound As String = ""

        ''' <summary>Selected name of the oxygen compound (required if Aerobic).</summary>
        Public Property OxygenCompound As String = "Oxygen"

        ''' <summary>Selected name of the CO2 compound.</summary>
        Public Property CO2Compound As String = "Carbon dioxide"

        ''' <summary>Selected name of the nitrogen-source compound (e.g. Ammonia).</summary>
        Public Property NitrogenSourceCompound As String = "Ammonia"

        ''' <summary>
        ''' Selected name of the sulfur-carrier compound (e.g. Sulfuric acid, Ammonium sulfate,
        ''' Hydrogen sulfide). Leave empty to skip the sulfur balance entirely, which is what the
        ''' reactor did before the balance existed. The carrier works in both directions: it is
        ''' consumed when the biomass needs more sulfur than the substrate carries, and produced
        ''' when an S-rich substrate releases more than the cells assimilate. It must be a
        ''' different compound from the nitrogen source, otherwise one coefficient would have to
        ''' satisfy both the N and the S balance.
        ''' </summary>
        Public Property SulfurSourceCompound As String = ""

        ''' <summary>Selected name of the water compound.</summary>
        Public Property WaterCompound As String = "Water"

        ''' <summary>Kinetic model used for the specific growth rate.</summary>
        Public Property KineticModel As BioKineticModel = BioKineticModel.Monod

        ''' <summary>Operation mode.</summary>
        Public Property OperatingMode As BioReactorMode = BioReactorMode.Continuous

        ''' <summary>Thermal handling mode (isothermal / adiabatic / defined outlet T).</summary>
        Public Property ThermalMode As BioReactorThermalMode = BioReactorThermalMode.Isothermal

        ''' <summary>
        ''' Specific heat of metabolism per mole of O2 consumed (J/mol_O2). The default 460 kJ/mol
        ''' is the Cooney-Wang-Mateles correlation widely used for aerobic fermentations. Set to
        ''' zero or adjust for specialised cases.
        ''' </summary>
        Public Property HeatPerMolO2_JmolO2 As Double = 460000.0

        ''' <summary>Whether the culture is aerobic (consumes O2, produces CO2).</summary>
        Public Property IsAerobic As Boolean = True

        ''' <summary>Maximum specific growth rate (1/h).</summary>
        Public Property MuMax_h As Double = 0.4

        ''' <summary>Saturation constant (g/L).</summary>
        Public Property Ks_gL As Double = 0.5

        ''' <summary>Substrate inhibition constant for Haldane (g/L).</summary>
        Public Property Ki_gL As Double = 100.0

        ''' <summary>Moser exponent.</summary>
        Public Property MoserN As Double = 2.0

        ''' <summary>Biomass yield on substrate (g biomass / g substrate).</summary>
        Public Property YieldXS As Double = 0.5

        ''' <summary>Product yield on substrate (g product / g substrate), growth-associated.</summary>
        Public Property YieldPS As Double = 0.0

        ''' <summary>Maintenance coefficient (g substrate / g biomass / h).</summary>
        Public Property Maintenance_gSg_cellh As Double = 0.04

        ''' <summary>First-order death/decay rate constant (1/h).</summary>
        Public Property DeathRate_h As Double = 0.01

        ''' <summary>Volumetric oxygen transfer coefficient (1/h). Used for OTR/OUR balance in aerobic mode.</summary>
        Public Property KLa_h As Double = 100.0

        ''' <summary>Saturation dissolved oxygen concentration (g/L) at operating T, P.</summary>
        Public Property CO2sat_gL As Double = 0.008

        ''' <summary>Name of the IronPython script (in flowsheet.Scripts) that evaluates the specific growth rate for UserScript mode. The script must set variable `mu` (1/s).</summary>
        Public Property UserScriptName As String = ""

        ' ---------------- ENZYMATIC HYDROLYSIS ROLES & PARAMETERS ----------------

        ''' <summary>(Enzymatic Hydrolysis) Hemicellulose / xylan compound name. Optional; leave empty for cellulose-only hydrolysis.</summary>
        Public Property HemicelluloseCompound As String = ""

        ''' <summary>(Enzymatic Hydrolysis) Xylose compound name (hemicellulose hydrolysis product). Optional.</summary>
        Public Property XyloseCompound As String = ""

        ''' <summary>(Enzymatic Hydrolysis) Cellulase enzyme compound name. Acts catalytically - not consumed by the reaction.
        ''' If the enzyme is not tracked as a stream compound, leave empty and use EH_EnzymeLoading_gL.</summary>
        Public Property EnzymeCompound As String = ""

        ''' <summary>(Enzymatic Hydrolysis) Cellulose hydrolysis rate constant k1, in L/(g_enzymeÂ·h).
        ''' Typical value 0.02â€“0.10 L/(gÂ·h) for commercial cellulase cocktails at 50 Â°C.</summary>
        Public Property EH_k1_Lgh As Double = 0.04

        ''' <summary>(Enzymatic Hydrolysis) Hemicellulose hydrolysis rate constant k2, in L/(g_enzymeÂ·h).
        ''' Typically 0.3â€“0.7 Ã— k1 for hemicellulases bundled with cellulase cocktails.</summary>
        Public Property EH_k2_Lgh As Double = 0.02

        ''' <summary>(Enzymatic Hydrolysis) Glucose competitive-inhibition constant (g/L). Smaller = stronger inhibition.</summary>
        Public Property EH_KG_glucose_gL As Double = 5.0

        ''' <summary>(Enzymatic Hydrolysis) Xylose competitive-inhibition constant (g/L).</summary>
        Public Property EH_KX_xylose_gL As Double = 10.0

        ''' <summary>(Enzymatic Hydrolysis) Enzyme loading (g/L) used when no EnzymeCompound is defined. Set 0 to force use of the stream enzyme concentration.</summary>
        Public Property EH_EnzymeLoading_gL As Double = 2.0

        ''' <summary>(Enzymatic Hydrolysis) Heat released per gram of sugar produced (J/g, negative = exothermic).
        ''' Net hydrolysis is mildly exothermic, â‰ˆ âˆ’11 J/g glucose; set to 0 to disable the thermal contribution.</summary>
        Public Property EH_HeatPerGProduct_Jg As Double = -11.0

        ' ---------------- RESULT PROPERTIES ----------------

        ''' <summary>Final biomass concentration at outlet (g/L).</summary>
        Public Property Result_X_gL As Double = 0.0

        ''' <summary>Final substrate concentration at outlet (g/L).</summary>
        Public Property Result_S_gL As Double = 0.0

        ''' <summary>Final product concentration at outlet (g/L).</summary>
        Public Property Result_P_gL As Double = 0.0

        ''' <summary>Average specific growth rate during integration (1/h).</summary>
        Public Property Result_Mu_h As Double = 0.0

        ''' <summary>Oxygen uptake rate (g O2 / L / h).</summary>
        Public Property Result_OUR_gLh As Double = 0.0

        ''' <summary>Carbon dioxide evolution rate (g CO2 / L / h).</summary>
        Public Property Result_CER_gLh As Double = 0.0

        ''' <summary>Respiratory quotient (CER / OUR in mol/mol basis).</summary>
        Public Property Result_RQ As Double = 0.0

        ''' <summary>Metabolic heat released by the culture (kW). Positive = heat produced.</summary>
        Public Property Result_Q_metabolic_kW As Double = 0.0

        ''' <summary>
        ''' Net heat duty delivered to the broth by the energy stream (kW). Positive = heating,
        ''' negative = cooling. In isothermal mode this is approximately -Q_metabolic.
        ''' </summary>
        Public Property Result_Q_duty_kW As Double = 0.0

        ''' <summary>Outlet broth temperature (K) computed from the selected thermal mode.</summary>
        Public Property Result_OutletTemperature_K As Double = 0.0

        <NonSerialized> <Xml.Serialization.XmlIgnore> Public f As Object

        ''' <summary>
        ''' Last dynamic integration trajectory (populated by Calculate). Not persisted - recomputed on each run.
        ''' </summary>
        <Xml.Serialization.XmlIgnore> <Newtonsoft.Json.JsonIgnore>
        Public Property LastTrajectory As BioReactorTrajectoryResult

        Public Overrides ReadOnly Property SupportsDynamicMode As Boolean = False

        Public Overrides ReadOnly Property EquipmentTypes As List(Of String)
            Get
                Return New List(Of String) From {"", "Stirred Tank Bioreactor", "Airlift", "Bubble Column", "Photobioreactor"}
            End Get
        End Property

        Public Overrides Sub CreateDimensionsList()
            Dimensions = New List(Of IDimension)
            Dimensions.Add(New Dimension With {.Name = DimensionName.Volume, .IsUserDefined = False})
        End Sub

        Public Overrides Sub UpdateDimensionsList()
            Dimensions(0).Value = Volume
        End Sub

        Public Sub New()
            MyBase.New()
        End Sub

        Public Sub New(ByVal name As String, ByVal description As String)
            MyBase.New()
            Me.ComponentName = name
            Me.ComponentDescription = description
        End Sub

        Public Overrides Function CloneXML() As Object
            Dim obj As ICustomXMLSerialization = New Reactor_BioReactor()
            obj.LoadData(Me.SaveData)
            Return obj
        End Function

        Public Overrides Function CloneJSON() As Object
            Return Newtonsoft.Json.JsonConvert.DeserializeObject(Of Reactor_BioReactor)(Newtonsoft.Json.JsonConvert.SerializeObject(Me))
        End Function

        ''' <summary>
        ''' Computes the specific growth rate mu (1/s) based on the selected kinetic model.
        ''' S, X are in g/L. mu_max, Ks, Ki are in h and g/L - converted internally to SI.
        ''' </summary>
        Private Function ComputeMu(S As Double, X As Double, Px As Double, T As Double, P As Double) As Double

            Dim muMaxSI As Double = MuMax_h / 3600.0 ' convert to 1/s

            If S <= 0.0 Then Return 0.0

            Select Case KineticModel

                Case BioKineticModel.Monod
                    Return muMaxSI * S / (Ks_gL + S)

                Case BioKineticModel.Contois
                    Dim denom = Ks_gL * Max(X, 0.000000000001) + S
                    If denom <= 0 Then Return 0.0
                    Return muMaxSI * S / denom

                Case BioKineticModel.Moser
                    Dim n = MoserN
                    Return muMaxSI * Pow(S, n) / (Pow(Ks_gL, n) + Pow(S, n))

                Case BioKineticModel.Haldane
                    Dim ki = Max(Ki_gL, 0.000000000001)
                    Return muMaxSI * S / (Ks_gL + S + S * S / ki)

                Case BioKineticModel.UserScript
                    If String.IsNullOrEmpty(UserScriptName) Then Return 0.0
                    Return EvalUserScriptMu(S, X, Px, T, P)

                Case Else
                    Return 0.0

            End Select

        End Function

        ''' <summary>Evaluates a user-defined Python script to compute mu (1/s).
        ''' The script receives S, X, Px, T, P, mu_max, Ks, Ki, reactor, Flowsheet - must set `mu`.</summary>
        Private Function EvalUserScriptMu(S As Double, X As Double, Px As Double, T As Double, P As Double) As Double

            Try
                Dim opts As New Dictionary(Of String, Object)()
                opts("Frames") = Microsoft.Scripting.Runtime.ScriptingRuntimeHelpers.True
                Dim eng = IronPython.Hosting.Python.CreateEngine(opts)
                eng.Runtime.LoadAssembly(GetType(System.String).Assembly)
                eng.Runtime.LoadAssembly(GetType(Thermodynamics.BaseClasses.ConstantProperties).Assembly)
                Dim scope = eng.CreateScope()
                scope.SetVariable("Flowsheet", FlowSheet)
                scope.SetVariable("reactor", Me)
                scope.SetVariable("S", S)
                scope.SetVariable("X", X)
                scope.SetVariable("Px", Px)
                scope.SetVariable("T", T)
                scope.SetVariable("P", P)
                scope.SetVariable("mu_max", MuMax_h / 3600.0)
                scope.SetVariable("Ks", Ks_gL)
                scope.SetVariable("Ki", Ki_gL)
                scope.SetVariable("mu", 0.0)
                Dim scr = FlowSheet.Scripts.Values.FirstOrDefault(Function(s2) s2.Title = UserScriptName)
                If scr Is Nothing Then Return 0.0
                Dim src = eng.CreateScriptSourceFromString(scr.ScriptText)
                src.Execute(scope)
                Return Convert.ToDouble(scope.GetVariable("mu"))
            Catch ex As Exception
                FlowSheet.ShowMessage("BioReactor user kinetic script error: " & ex.Message, IFlowsheet.MessageType.GeneralError)
                Return 0.0
            End Try

        End Function

        ''' <summary>
        ''' Elemental composition of one mol of a species, in atoms per mol. The biomass is
        ''' carried in the same structure but normalised to one C-mol.
        ''' </summary>
        Private Structure ElementalComposition
            Public C As Double
            Public H As Double
            Public O As Double
            Public N As Double
            Public S As Double
        End Structure

        ''' <summary>
        ''' Reads C, H, O, N and S atoms per mol from a compound's elemental formula. Missing
        ''' elements come back as zero, which is exactly what the balance needs.
        ''' </summary>
        Private Function ReadComposition(cp As ConstantProperties) As ElementalComposition

            Dim e As New ElementalComposition
            If cp Is Nothing Then Return e

            Dim elems As SortedList = cp.Elements
            If elems Is Nothing OrElse elems.Count = 0 Then Return e

            If elems.Contains("C") Then e.C = Convert.ToDouble(elems("C"))
            If elems.Contains("H") Then e.H = Convert.ToDouble(elems("H"))
            If elems.Contains("O") Then e.O = Convert.ToDouble(elems("O"))
            If elems.Contains("N") Then e.N = Convert.ToDouble(elems("N"))
            If elems.Contains("S") Then e.S = Convert.ToDouble(elems("S"))

            Return e

        End Function

        ''' <summary>
        ''' Rescales a composition to one C-mol. A carbon-free species comes back untouched,
        ''' since there is nothing to normalise against and its yield coefficient is zero anyway.
        ''' </summary>
        Private Function NormaliseToCmol(e As ElementalComposition) As ElementalComposition
            If e.C <= 0.0 Then Return e
            Return New ElementalComposition With {
                .C = 1.0,
                .H = e.H / e.C,
                .O = e.O / e.C,
                .N = e.N / e.C,
                .S = e.S / e.C}
        End Function

        ''' <summary>
        ''' Reads the elemental formula of the biomass compound (CaHbOcNdSe) per 1 C-mol.
        ''' </summary>
        Private Sub ReadBiomassFormula(biomassComp As Compound,
                                       ByRef comp As ElementalComposition,
                                       ByRef MW_Cmol As Double)

            ' Roels' average biomass, used whenever the compound carries no usable formula.
            comp = New ElementalComposition With {.C = 1.0, .H = 1.8, .O = 0.5, .N = 0.2, .S = 0.0}
            MW_Cmol = 24.6

            Dim cp = biomassComp.ConstantProperties
            If cp Is Nothing Then Return

            Dim abs_ = ReadComposition(cp)
            If abs_.C <= 0 Then Return

            comp.C = 1.0
            comp.H = abs_.H / abs_.C
            comp.O = abs_.O / abs_.C
            comp.N = abs_.N / abs_.C
            comp.S = abs_.S / abs_.C
            ' MW per C-mol = MW_total / C_abs
            MW_Cmol = cp.Molar_Weight / abs_.C

        End Sub

        ''' <summary>
        ''' Solves the elemental balance for the growth reaction
        '''   Substrate + aa*O2 + bb*N_source + ee*S_carrier
        '''       -> Y_XC*Biomass + Y_PC*Product + cc*CO2 + dd*H2O
        ''' returning molar stoichiometric coefficients (per C-mol of substrate consumed).
        ''' Biomass composition is per C-mol; every other species is per mol.
        ''' Y_XC = C-mol biomass / C-mol substrate; Y_PC = C-mol product / C-mol substrate.
        '''
        ''' The N source and the S carrier are taken with their real formulas rather than as
        ''' bare NH3 and S, because the species that actually supply them - ammonium sulfate,
        ''' sulfuric acid, hydrogen sulfide - drag C, H, O and N into the other four balances.
        ''' ee comes out negative when the substrate carries more sulfur than the cells
        ''' assimilate, meaning the carrier is produced rather than consumed.
        ''' </summary>
        Private Sub SolveElementalBalance(subs As ElementalComposition,
                                          prod As ElementalComposition,
                                          biom As ElementalComposition,
                                          nsrc As ElementalComposition,
                                          ssrc As ElementalComposition,
                                          Y_XC As Double, Y_PC As Double,
                                          ByRef aa_O2 As Double, ByRef bb_Nsrc As Double,
                                          ByRef ee_Ssrc As Double,
                                          ByRef cc_CO2 As Double, ByRef dd_H2O As Double,
                                          Optional forceAnaerobic As Boolean = False)

            ' Per 1 C-mol substrate. Net demand for N and S: what the biomass and the product
            ' take up, less what the substrate already brings.
            Dim RN = Y_XC * biom.N + Y_PC * prod.N - subs.N
            Dim RS = Y_XC * biom.S + Y_PC * prod.S - subs.S

            ' N and S are coupled whenever either carrier holds both elements (ammonium
            ' sulfate is the obvious case), so solve the 2x2 system rather than one at a time.
            '   nsrc.N*bb + ssrc.N*ee = RN
            '   nsrc.S*bb + ssrc.S*ee = RS
            bb_Nsrc = 0.0
            ee_Ssrc = 0.0
            Dim det = nsrc.N * ssrc.S - nsrc.S * ssrc.N
            If ssrc.S > 0.0 AndAlso nsrc.N > 0.0 AndAlso Abs(det) > 0.000000000001 Then
                bb_Nsrc = (RN * ssrc.S - RS * ssrc.N) / det
                ee_Ssrc = (nsrc.N * RS - nsrc.S * RN) / det
            ElseIf ssrc.S > 0.0 AndAlso nsrc.N <= 0.0 Then
                ee_Ssrc = RS / ssrc.S
            ElseIf nsrc.N > 0.0 Then
                ' No sulfur carrier, or a degenerate pair: the N source alone closes N and the
                ' sulfur balance stays open, exactly as it was before sulfur was modelled.
                bb_Nsrc = RN / nsrc.N
                If ssrc.S > 0.0 Then ee_Ssrc = (RS - bb_Nsrc * nsrc.S) / ssrc.S
            End If

            ' A negative bb means the substrate is N-rich enough to need no feed at all; the
            ' surplus leaves with the biomass rather than as free ammonia. Re-solve the sulfur
            ' coefficient afterwards, since it was found jointly with the bb we just discarded.
            If bb_Nsrc < 0.0 Then
                bb_Nsrc = 0.0
                If ssrc.S > 0.0 Then ee_Ssrc = RS / ssrc.S
            End If

            ' C:  subs.C + bb*nsrc.C + ee*ssrc.C = Y_XC + Y_PC*prod.C + cc
            cc_CO2 = subs.C + bb_Nsrc * nsrc.C + ee_Ssrc * ssrc.C - Y_XC - Y_PC * prod.C

            If forceAnaerobic Then
                ' Anaerobic / fermentative mode: no O2 available. Close the O balance via H2O,
                ' which may be negative (water consumed, Buswell-like) when the substrate is
                ' more oxidised than needed. H balance is no longer enforced - the residual
                ' is implicitly absorbed by un-modelled H2 / VFA / alcohol production.
                aa_O2 = 0.0
                ' O:  subs.O + bb*nsrc.O + ee*ssrc.O = Y_XC*biom.O + Y_PC*prod.O + 2*cc + dd
                dd_H2O = subs.O + bb_Nsrc * nsrc.O + ee_Ssrc * ssrc.O -
                         Y_XC * biom.O - Y_PC * prod.O - 2.0 * cc_CO2
            Else
                ' Aerobic mode: CO2 closes C, the N source closes N, the S carrier closes S,
                ' H2O closes H and O2 closes O.
                ' H:  subs.H + bb*nsrc.H + ee*ssrc.H = Y_XC*biom.H + Y_PC*prod.H + 2*dd
                dd_H2O = (subs.H + bb_Nsrc * nsrc.H + ee_Ssrc * ssrc.H -
                          Y_XC * biom.H - Y_PC * prod.H) / 2.0

                ' O:  subs.O + 2*aa + bb*nsrc.O + ee*ssrc.O = Y_XC*biom.O + Y_PC*prod.O + 2*cc + dd
                aa_O2 = (Y_XC * biom.O + Y_PC * prod.O + 2.0 * cc_CO2 + dd_H2O -
                         subs.O - bb_Nsrc * nsrc.O - ee_Ssrc * ssrc.O) / 2.0
                If aa_O2 < 0.0 Then aa_O2 = 0.0 ' anaerobic / fermentative
            End If

        End Sub

        Public Overrides Sub Calculate(Optional ByVal args As Object = Nothing)

            ' Enzymatic Hydrolysis uses a separate calculation path: no biomass, no aerobic
            ' elemental balance. Substrate = cellulose, Product = glucose, plus the dedicated
            ' Hemicellulose / Xylose / Enzyme roles.
            If KineticModel = BioKineticModel.EnzymaticHydrolysis Then
                CalculateEnzymaticHydrolysis()
                Return
            End If

            If String.IsNullOrEmpty(BiomassCompound) Then
                Throw New Exception("BioReactor: Biomass compound not selected.")
            End If
            If String.IsNullOrEmpty(SubstrateCompound) Then
                Throw New Exception("BioReactor: Substrate compound not selected.")
            End If
            If Not Me.GraphicObject.InputConnectors(0).IsAttached Then
                Throw New Exception("BioReactor: Inlet stream not connected.")
            End If
            If Not Me.GraphicObject.OutputConnectors(0).IsAttached Then
                Throw New Exception("BioReactor: Outlet stream not connected.")
            End If
            If Volume <= 0.0 Then
                Throw New Exception("BioReactor: Working volume must be positive.")
            End If

            Dim ims As MaterialStream =
                DirectCast(FlowSheet.SimulationObjects(Me.GraphicObject.InputConnectors(0).AttachedConnector.AttachedFrom.Name), MaterialStream).Clone
            ims.SetFlowsheet(Me.FlowSheet)
            ims.SetPropertyPackage(PropertyPackage)
            PropertyPackage.CurrentMaterialStream = ims
            ims.DefinedFlow = FlowSpec.Mass

            Dim T As Double = ims.Phases(0).Properties.temperature.GetValueOrDefault
            Dim P0 As Double = ims.Phases(0).Properties.pressure.GetValueOrDefault
            Dim P As Double = P0 - DeltaP.GetValueOrDefault
            ims.Phases(0).Properties.pressure = P

            Select Case ReactorOperationMode
                Case OperationMode.OutletTemperature
                    T = OutletTemperature
                    ims.Phases(0).Properties.temperature = T
                    ims.SpecType = StreamSpec.Temperature_and_Pressure
                    ims.Calculate(True, True)
            End Select

            Dim compounds = ims.Phases(0).Compounds

            If Not compounds.ContainsKey(BiomassCompound) Then _
                Throw New Exception("BioReactor: Biomass compound '" & BiomassCompound & "' not present in stream.")
            If Not compounds.ContainsKey(SubstrateCompound) Then _
                Throw New Exception("BioReactor: Substrate compound '" & SubstrateCompound & "' not present in stream.")

            Dim biomass = compounds(BiomassCompound)
            Dim substrate = compounds(SubstrateCompound)
            Dim product As Compound = Nothing
            If Not String.IsNullOrEmpty(ProductCompound) AndAlso compounds.ContainsKey(ProductCompound) Then product = compounds(ProductCompound)
            Dim o2 As Compound = Nothing
            If IsAerobic AndAlso Not String.IsNullOrEmpty(OxygenCompound) AndAlso compounds.ContainsKey(OxygenCompound) Then o2 = compounds(OxygenCompound)
            Dim co2 As Compound = Nothing
            If Not String.IsNullOrEmpty(CO2Compound) AndAlso compounds.ContainsKey(CO2Compound) Then co2 = compounds(CO2Compound)
            Dim nh3 As Compound = Nothing
            If Not String.IsNullOrEmpty(NitrogenSourceCompound) AndAlso compounds.ContainsKey(NitrogenSourceCompound) Then nh3 = compounds(NitrogenSourceCompound)
            Dim h2o As Compound = Nothing
            If Not String.IsNullOrEmpty(WaterCompound) AndAlso compounds.ContainsKey(WaterCompound) Then h2o = compounds(WaterCompound)
            Dim ssrcComp As Compound = Nothing
            If Not String.IsNullOrEmpty(SulfurSourceCompound) AndAlso compounds.ContainsKey(SulfurSourceCompound) Then ssrcComp = compounds(SulfurSourceCompound)

            ' One compound cannot close both the N and the S balance: a single coefficient would
            ' have to satisfy two equations. Drop the sulfur role and say so.
            If ssrcComp IsNot Nothing AndAlso nh3 IsNot Nothing AndAlso ssrcComp.Name = nh3.Name Then
                FlowSheet.ShowMessage(String.Format(
                    "BioReactor '{0}': '{1}' is selected as both the nitrogen source and the sulfur carrier. " &
                    "The sulfur balance is being skipped - pick a separate sulfur carrier to close it.",
                    Me.GraphicObject.Tag, ssrcComp.Name), IFlowsheet.MessageType.Warning)
                ssrcComp = Nothing
            End If

            ' Read biomass elemental formula (per C-mol)
            Dim biomComp As ElementalComposition, MW_Xcmol As Double
            ReadBiomassFormula(biomass, biomComp, MW_Xcmol)

            ' Substrate formula (per mol)
            Dim subsComp = ReadComposition(substrate.ConstantProperties)
            If subsComp.C <= 0 Then ' fallback: glucose
                subsComp = New ElementalComposition With {.C = 6.0, .H = 12.0, .O = 6.0}
            End If

            ' Product formula (per mol)
            Dim prodComp As New ElementalComposition
            If product IsNot Nothing Then prodComp = ReadComposition(product.ConstantProperties)

            ' Nitrogen source formula (per mol). Falls back to bare NH3 when the compound is not
            ' in the stream, which is the assumption the balance was hard-wired to before.
            Dim nsrcComp As New ElementalComposition
            If nh3 IsNot Nothing Then nsrcComp = ReadComposition(nh3.ConstantProperties)
            If nsrcComp.N <= 0.0 Then
                nsrcComp = New ElementalComposition With {.H = 3.0, .N = 1.0}
            End If

            ' Sulfur carrier formula (per mol). Empty unless a carrier compound carrying S was
            ' selected, and an empty carrier leaves the sulfur balance out of the system.
            Dim ssrcE As New ElementalComposition
            If ssrcComp IsNot Nothing Then ssrcE = ReadComposition(ssrcComp.ConstantProperties)
            If ssrcE.S <= 0.0 Then
                If ssrcComp IsNot Nothing Then
                    FlowSheet.ShowMessage(String.Format(
                        "BioReactor '{0}': sulfur carrier '{1}' declares no sulfur in its elemental formula. " &
                        "The sulfur balance is being skipped.", Me.GraphicObject.Tag, ssrcComp.Name),
                        IFlowsheet.MessageType.Warning)
                    ssrcComp = Nothing
                End If
                ssrcE = New ElementalComposition
            End If

            ' Convert mass-based yield coefficients to C-mol basis
            Dim MW_S = substrate.ConstantProperties.Molar_Weight   ' g/mol
            Dim MW_X_per_Cmol = MW_Xcmol                           ' g / C-mol biomass
            Dim Y_XC_Cmol = YieldXS * (MW_S / subsComp.C) / MW_X_per_Cmol  ' (C-mol X per C-mol S)
            Dim MW_P As Double = 1.0
            If product IsNot Nothing Then MW_P = product.ConstantProperties.Molar_Weight
            Dim Y_PC_Cmol = 0.0
            If product IsNot Nothing AndAlso prodComp.C > 0 Then
                Y_PC_Cmol = YieldPS * (MW_S / subsComp.C) / (MW_P / prodComp.C)
            End If

            ' Molar masses used to turn stoichiometric coefficients back into mass flows. Take
            ' them from the compounds themselves wherever they are in the stream: rounding O2 to
            ' 32 and H2O to 18 while the elemental accounting uses the database values leaks a
            ' few parts in ten thousand out of the balance.
            Dim MW_Nsrc As Double = 17.0 ' bare NH3, matching the nitrogen-source fallback above
            If nh3 IsNot Nothing AndAlso nh3.ConstantProperties.Molar_Weight > 0.0 Then _
                MW_Nsrc = nh3.ConstantProperties.Molar_Weight
            Dim MW_Ssrc As Double = 0.0
            If ssrcComp IsNot Nothing Then MW_Ssrc = ssrcComp.ConstantProperties.Molar_Weight
            Dim MW_O2 As Double = 32.0
            If o2 IsNot Nothing AndAlso o2.ConstantProperties.Molar_Weight > 0.0 Then _
                MW_O2 = o2.ConstantProperties.Molar_Weight
            Dim MW_CO2 As Double = 44.0
            If co2 IsNot Nothing AndAlso co2.ConstantProperties.Molar_Weight > 0.0 Then _
                MW_CO2 = co2.ConstantProperties.Molar_Weight
            Dim MW_H2O As Double = 18.0
            If h2o IsNot Nothing AndAlso h2o.ConstantProperties.Molar_Weight > 0.0 Then _
                MW_H2O = h2o.ConstantProperties.Molar_Weight

            ' Solve elemental balance for growth reaction (per C-mol substrate).
            ' Use the anaerobic branch when IsAerobic=False so the O balance is closed via H2O
            ' (possibly consumed) rather than O2 - avoids the mass-balance inconsistency that
            ' resulted from solving aerobic stoich and then clamping dm_O2 to zero downstream.
            '
            ' Substrate and product go in normalised to one C-mol, matching the basis Y_XC and
            ' Y_PC are already on and the basis the coefficients are consumed on further down
            ' (everything is multiplied by dS_Cmol_per_L). Feeding the raw per-mol formulas here
            ' mixed the two bases and inflated every coefficient by roughly the substrate's
            ' carbon number.
            Dim aa As Double, bb As Double, cc As Double, dd As Double, ee As Double
            SolveElementalBalance(NormaliseToCmol(subsComp), NormaliseToCmol(prodComp),
                                  biomComp, nsrcComp, ssrcE,
                                  Y_XC_Cmol, Y_PC_Cmol, aa, bb, ee, cc, dd,
                                  forceAnaerobic:=Not IsAerobic)

            ' -------------------------------------------------------
            ' State variables: S, X, P (g/L).
            ' Liquid volumetric flow:
            Dim Q_liquid As Double = ims.Phases(1).Properties.volumetric_flow.GetValueOrDefault
            If Q_liquid <= 0.0 Then Q_liquid = ims.Phases(0).Properties.volumetric_flow.GetValueOrDefault
            If Q_liquid <= 0.0 Then Q_liquid = 0.000000000001

            ' Inlet concentrations (g/L) - use overall phase mass to broth volume
            Dim rho_L = ims.Phases(1).Properties.density.GetValueOrDefault
            If rho_L <= 0.0 Then rho_L = 1000.0

            Dim massflow_S_in = substrate.MassFlow.GetValueOrDefault      ' kg/s
            Dim massflow_X_in = biomass.MassFlow.GetValueOrDefault
            Dim massflow_P_in As Double = 0.0
            If product IsNot Nothing Then massflow_P_in = product.MassFlow.GetValueOrDefault

            Dim S0 = massflow_S_in / Q_liquid ' kg/s / (m3/s) = kg/m3 = g/L
            Dim X0 = massflow_X_in / Q_liquid
            Dim P0c = massflow_P_in / Q_liquid

            ' Integration time and "effective" volumetric flow used to convert the integrated
            ' concentration changes (g/L) back into mass-flow deltas (kg/s).
            '
            '  Continuous : Q_eff = Q_liquid (the inlet volumetric flow); tau = V/Q (HRT)
            '  Batch      : Q_eff = V/BatchDuration (cycle-averaged equivalent volumetric flow,
            '               as if the reactor processed one V-volume charge every BatchDuration
            '               seconds and discharged it instantly); tau = BatchDuration
            '  Fed-Batch  : same as Batch in this simplified model (final-volume / cycle-time)
            Dim tau As Double
            Dim Q_eff As Double
            Select Case OperatingMode
                Case BioReactorMode.Continuous
                    tau = Volume / Q_liquid
                    Q_eff = Q_liquid
                Case BioReactorMode.Batch, BioReactorMode.FedBatch
                    tau = BatchDuration
                    Q_eff = Volume / Max(BatchDuration, 1.0E-9)
                Case Else
                    tau = Volume / Q_liquid
                    Q_eff = Q_liquid
            End Select

            ' Consistency check: in batch / fed-batch, the upstream stream's volumetric flow
            ' should match V/BatchDuration so the simulation closes mass. Warn the user when
            ' they don't agree (the inlet mass flows will be re-scaled to Q_eff below).
            If OperatingMode <> BioReactorMode.Continuous AndAlso Q_liquid > 0.0 AndAlso Q_eff > 0.0 Then
                If Abs(Q_liquid - Q_eff) / Q_eff > 0.05 Then
                    FlowSheet.ShowMessage(String.Format(
                        "BioReactor '{0}': inlet volumetric flow ({1:F4} m3/s) differs from V/BatchDuration ({2:F4} m3/s) by >5%. Using V/BatchDuration as the cycle-averaged equivalent flow; inlet stream mass flows will be scaled.",
                        Me.GraphicObject.Tag, Q_liquid, Q_eff),
                        IFlowsheet.MessageType.Warning)
                End If
            End If

            ' Scale factor that maps the inlet stream's mass flows (kg/s defined at Q_liquid)
            ' onto the cycle-equivalent flow Q_eff used by the batch/fed-batch model.
            Dim flowScale As Double = If(Q_liquid > 0.0, Q_eff / Q_liquid, 1.0)

            ' Forward integration (adaptive RK4) of dX/dt, dS/dt, dP/dt
            Dim S = S0, X = X0, Px = P0c
            Dim kd_SI = DeathRate_h / 3600.0
            Dim ms_SI = Maintenance_gSg_cellh / 3600.0
            Dim muAvg As Double = 0.0
            Dim muAccum As Double = 0.0
            Dim tAccum As Double = 0.0
            Dim tt As Double = 0.0
            Dim dt As Double = Max(tau / 500.0, 1.0)
            Dim integrated_rX_total As Double = 0.0
            Dim integrated_rS_total As Double = 0.0
            Dim integrated_rP_total As Double = 0.0
            Dim integrated_O2_total As Double = 0.0
            Dim integrated_CO2_total As Double = 0.0
            Dim integrated_NH3_total As Double = 0.0
            Dim integrated_H2O_total As Double = 0.0
            Dim integrated_Ssrc_total As Double = 0.0
            Dim integrated_S_Cmol_total As Double = 0.0

            ' --- Trajectory capture ---
            Dim traj As New BioReactorTrajectoryResult() With {.Mode = "Growth"}
            LastTrajectory = traj
            Dim nStepsExpected = Max(1, CInt(Math.Ceiling(tau / Max(dt, 0.000000000001))))
            Dim sampleInterval As Integer = Max(1, nStepsExpected \ 500)
            Dim maxSamples As Integer = 2000
            Dim stepCount As Integer = 0

            ' Initial sample
            traj.Times.Add(0.0)
            traj.X.Add(X) : traj.S.Add(S) : traj.P.Add(Px)
            Dim mu0 = ComputeMu(S, X, Px, T, P)
            traj.Mu.Add(mu0 * 3600.0)
            Dim qS0 = (mu0 / Max(YieldXS, 0.000000000001) + ms_SI) * 3600.0
            traj.qS.Add(qS0)
            Dim qP0 = If(YieldPS > 0.0, YieldPS * mu0 / Max(YieldXS, 0.000000000001) * 3600.0, 0.0)
            traj.qP.Add(qP0)
            traj.OUR.Add(0.0) : traj.CER.Add(0.0) : traj.RQ.Add(0.0)

            While tt < tau
                Dim h = Min(dt, tau - tt)

                Dim mu = ComputeMu(S, X, Px, T, P)
                Dim rX = (mu - kd_SI) * X           ' g/L/s
                Dim qS = mu / Max(YieldXS, 0.000000000001) + ms_SI ' g S / g X / s
                Dim rS = -qS * X
                Dim qP = 0.0
                If YieldPS > 0.0 Then qP = YieldPS * mu / Max(YieldXS, 0.000000000001)
                Dim rP = qP * X

                ' Forward Euler step (simple, robust)
                Dim dX = rX * h
                Dim dS = rS * h
                Dim dP = rP * h

                ' Take the realised deltas, not the ones the rate laws asked for: a state that
                ' hits its floor must stop contributing to the totals, or the reactor keeps
                ' charging itself for substrate the broth no longer holds.
                Dim Xn = Max(X + dX, 0.0)
                Dim Sn = Max(S + dS, 0.0)
                Dim Pn = Max(Px + dP, 0.0)
                dX = Xn - X
                dS = Sn - S
                dP = Pn - Px
                X = Xn : S = Sn : Px = Pn

                ' Track cumulative metabolic fluxes (g/L total over the integration).
                ' delta(S) in C-mol/L:
                Dim dS_Cmol_per_L = (-dS) / MW_S * subsComp.C ' g/L -> mol/L -> C-mol/L
                integrated_rS_total += -dS ' g/L consumed (positive)
                integrated_rX_total += dX
                integrated_rP_total += dP
                integrated_S_Cmol_total += dS_Cmol_per_L

                muAccum += mu * h
                tAccum += h
                tt += h
                stepCount += 1

                ' Sample capture (respecting cap)
                If traj.Times.Count < maxSamples AndAlso (stepCount Mod sampleInterval = 0) Then
                    Dim ourInst = aa * ((qS * X) / Max(MW_S, 0.000000000001)) * subsComp.C * MW_O2 * 3600.0 ' g O2/L/h (approx via consumed rate)
                    Dim cerInst = cc * ((qS * X) / Max(MW_S, 0.000000000001)) * subsComp.C * MW_CO2 * 3600.0 ' g CO2/L/h
                    Dim rqInst As Double = 0.0
                    If ourInst > 0.000000001 Then rqInst = (cerInst / MW_CO2) / (ourInst / MW_O2)
                    traj.Times.Add(tt)
                    traj.X.Add(X) : traj.S.Add(S) : traj.P.Add(Px)
                    traj.Mu.Add(mu * 3600.0)
                    traj.qS.Add(qS * 3600.0)
                    traj.qP.Add(qP * 3600.0)
                    traj.OUR.Add(If(IsAerobic, ourInst, 0.0))
                    traj.CER.Add(cerInst)
                    traj.RQ.Add(rqInst)
                    ' Geometric fallback if sample count would exceed cap
                    If traj.Times.Count >= maxSamples \ 2 Then
                        sampleInterval = Max(sampleInterval, sampleInterval * 2)
                    End If
                End If
            End While

            ' Final sample
            If traj.Times.Count = 0 OrElse traj.Times(traj.Times.Count - 1) < tt - 0.000000001 Then
                traj.Times.Add(tt)
                traj.X.Add(X) : traj.S.Add(S) : traj.P.Add(Px)
                Dim muF = ComputeMu(S, X, Px, T, P)
                traj.Mu.Add(muF * 3600.0)
                traj.qS.Add((muF / Max(YieldXS, 0.000000000001) + ms_SI) * 3600.0)
                traj.qP.Add(If(YieldPS > 0.0, YieldPS * muF / Max(YieldXS, 0.000000000001) * 3600.0, 0.0))
                traj.OUR.Add(0.0) : traj.CER.Add(0.0) : traj.RQ.Add(0.0)
            End If

            If tAccum > 0 Then muAvg = muAccum / tAccum

            ' -------------------------------------------------------
            ' Re-solve the stoichiometry on the yields the integration actually realised.
            ' The nominal YieldXS / YieldPS above ignore endogenous decay and maintenance, so
            ' the biomass and product the loop produced per C-mol of substrate consumed are a
            ' few percent off them - and the elemental balance has to be written around what
            ' the streams will really carry, or carbon and nitrogen go missing at the outlet.
            If integrated_S_Cmol_total > 0.0 Then
                Y_XC_Cmol = (integrated_rX_total / MW_X_per_Cmol) / integrated_S_Cmol_total
                If product IsNot Nothing AndAlso prodComp.C > 0 AndAlso MW_P > 0.0 Then
                    Y_PC_Cmol = (integrated_rP_total / (MW_P / prodComp.C)) / integrated_S_Cmol_total
                Else
                    Y_PC_Cmol = 0.0
                End If
                SolveElementalBalance(NormaliseToCmol(subsComp), NormaliseToCmol(prodComp),
                                      biomComp, nsrcComp, ssrcE,
                                      Y_XC_Cmol, Y_PC_Cmol, aa, bb, ee, cc, dd,
                                      forceAnaerobic:=Not IsAerobic)
            End If

            ' The coefficients are constant over the integration, so the cumulative fluxes are
            ' just the total C-mol of substrate consumed times each coefficient.
            integrated_O2_total = aa * integrated_S_Cmol_total * MW_O2       ' g O2 / L
            integrated_CO2_total = cc * integrated_S_Cmol_total * MW_CO2     ' g CO2 / L
            integrated_NH3_total = bb * integrated_S_Cmol_total * MW_Nsrc    ' g N source / L
            integrated_H2O_total = dd * integrated_S_Cmol_total * MW_H2O     ' g H2O / L
            integrated_Ssrc_total = ee * integrated_S_Cmol_total * MW_Ssrc   ' g S carrier / L (<0 = released)

            ' Results (convert from SI to display units)
            Result_X_gL = X
            Result_S_gL = S
            Result_P_gL = Px
            Result_Mu_h = muAvg * 3600.0
            If tau > 0 Then
                Result_OUR_gLh = integrated_O2_total / (tau / 3600.0)
                Result_CER_gLh = integrated_CO2_total / (tau / 3600.0)
            End If
            If Result_OUR_gLh > 0.000000001 Then
                Result_RQ = (Result_CER_gLh / MW_CO2) / (Result_OUR_gLh / MW_O2)
            Else
                Result_RQ = 0.0
            End If

            ' -------------------------------------------------------
            ' Convert concentration changes (g/L) back to mass flows (kg/s) using Q_eff
            ' (= Q_liquid in continuous; = V/BatchDuration in batch / fed-batch).
            Dim dm_S = -integrated_rS_total * Q_eff ' g/L * m3/s = kg/s
            Dim dm_X = integrated_rX_total * Q_eff
            Dim dm_P = integrated_rP_total * Q_eff
            Dim dm_O2 = -integrated_O2_total * Q_eff
            Dim dm_CO2 = integrated_CO2_total * Q_eff
            Dim dm_NH3 = -integrated_NH3_total * Q_eff
            Dim dm_H2O = integrated_H2O_total * Q_eff
            ' Positive ee = carrier assimilated, so the stream loses it; negative ee = sulfur
            ' released by an S-rich substrate, so the stream gains it.
            Dim dm_Ssrc = -integrated_Ssrc_total * Q_eff

            If Not IsAerobic Then
                dm_O2 = 0.0
            End If

            ' Update compound mass flows in the internal material stream. In batch / fed-batch,
            ' scale the inlet mass flows by Q_eff/Q_liquid so the "cycle-averaged" inlet matches
            ' V/BatchDuration; this keeps the mass balance closed end-to-end.
            Dim newMass As New Dictionary(Of String, Double)
            Dim totalNewMass As Double = 0.0
            For Each kvp In compounds
                Dim currentMF = kvp.Value.MassFlow.GetValueOrDefault * flowScale
                newMass(kvp.Key) = currentMF
            Next

            newMass(substrate.Name) = Max(newMass(substrate.Name) + dm_S, 0.0)
            newMass(biomass.Name) = Max(newMass(biomass.Name) + dm_X, 0.0)
            If product IsNot Nothing Then newMass(product.Name) = Max(newMass(product.Name) + dm_P, 0.0)
            If o2 IsNot Nothing Then newMass(o2.Name) = Max(newMass(o2.Name) + dm_O2, 0.0)
            If co2 IsNot Nothing Then newMass(co2.Name) = Max(newMass(co2.Name) + dm_CO2, 0.0)
            If nh3 IsNot Nothing Then newMass(nh3.Name) = Max(newMass(nh3.Name) + dm_NH3, 0.0)
            If h2o IsNot Nothing Then newMass(h2o.Name) = Max(newMass(h2o.Name) + dm_H2O, 0.0)
            If ssrcComp IsNot Nothing Then newMass(ssrcComp.Name) = Max(newMass(ssrcComp.Name) + dm_Ssrc, 0.0)

            For Each kvp In newMass
                totalNewMass += kvp.Value
            Next
            If totalNewMass <= 0 Then totalNewMass = ims.Phases(0).Properties.massflow.GetValueOrDefault

            ' Set new mass fractions & total mass flow
            For Each comp In compounds.Values
                comp.MassFraction = newMass(comp.Name) / totalNewMass
            Next
            ' Convert mass -> mole fractions
            Dim invMWsum As Double = 0.0
            For Each comp In compounds.Values
                invMWsum += comp.MassFraction.GetValueOrDefault / comp.ConstantProperties.Molar_Weight
            Next
            For Each comp In compounds.Values
                comp.MoleFraction = (comp.MassFraction.GetValueOrDefault / comp.ConstantProperties.Molar_Weight) / invMWsum
            Next
            ims.Phases(0).Properties.massflow = totalNewMass
            ims.DefinedFlow = FlowSpec.Mass

            ' -------------------------------------------------------
            ' ENERGY BALANCE - metabolic heat + thermal mode
            ' -------------------------------------------------------
            '
            ' Metabolic heat is estimated from the Cooney-Wang-Mateles correlation:
            '     Q_met [W] = HeatPerMolO2 [J/mol O2] * n_dot_O2_consumed [mol/s]
            ' Only applies in aerobic mode; for anaerobic we use zero (fermentations
            ' release only a few % of combustion enthalpy and most leaves as ethanol/
            ' lactate etc. carried in the outlet).
            '
            Dim n_dot_O2_mol_s As Double = 0.0
            If IsAerobic Then n_dot_O2_mol_s = Abs(dm_O2) / (MW_O2 / 1000.0) ' kg/s / (kg/mol) = mol/s
            Dim Q_met_W = HeatPerMolO2_JmolO2 * n_dot_O2_mol_s
            Result_Q_metabolic_kW = Q_met_W / 1000.0

            ' Mass heat capacity of the liquid phase (J/kg/K) at inlet T for the enthalpy balance.
            Dim cp_L_mass As Double = 0.0
            Try
                cp_L_mass = ims.Phases(1).Properties.heatCapacityCp.GetValueOrDefault * 1000.0 ' kJ/kg/K -> J/kg/K
            Catch
            End Try
            If cp_L_mass <= 0.0 Then
                Try
                    cp_L_mass = ims.Phases(0).Properties.heatCapacityCp.GetValueOrDefault * 1000.0
                Catch
                End Try
            End If
            If cp_L_mass <= 0.0 Then cp_L_mass = 4180.0 ' water fallback

            ' Mass flow basis used in the enthalpy balance. For batch / fed-batch we treat the
            ' broth inventory as (rho_L * Volume) and compute the steady-state temperature rise
            ' assumption over the batch duration.
            Dim m_dot_kgs As Double = ims.Phases(0).Properties.massflow.GetValueOrDefault
            Dim m_holdup_kg As Double = rho_L * Volume

            Dim T_in_K = T
            Dim T_out_K = T_in_K
            Dim Q_duty_W = 0.0 ' heat added to broth by external stream

            Select Case ThermalMode

                Case BioReactorThermalMode.Isothermal
                    ' Hold outlet T at inlet T -> cooling duty removes the metabolic heat
                    T_out_K = T_in_K
                    Q_duty_W = -Q_met_W

                Case BioReactorThermalMode.Adiabatic
                    ' No external duty; metabolic heat raises the broth temperature.
                    Q_duty_W = 0.0
                    Select Case OperatingMode
                        Case BioReactorMode.Continuous
                            If m_dot_kgs > 0.0 Then T_out_K = T_in_K + Q_met_W / (m_dot_kgs * cp_L_mass)
                        Case BioReactorMode.Batch, BioReactorMode.FedBatch
                            If m_holdup_kg > 0.0 Then T_out_K = T_in_K + (Q_met_W * tau) / (m_holdup_kg * cp_L_mass)
                    End Select

                Case BioReactorThermalMode.DefinedOutletTemperature
                    ' User-prescribed outlet T; back-compute the required duty.
                    If OutletTemperature > 0.0 Then T_out_K = OutletTemperature Else T_out_K = T_in_K
                    Select Case OperatingMode
                        Case BioReactorMode.Continuous
                            Q_duty_W = m_dot_kgs * cp_L_mass * (T_out_K - T_in_K) - Q_met_W
                        Case BioReactorMode.Batch, BioReactorMode.FedBatch
                            If tau > 0.0 Then _
                                Q_duty_W = (m_holdup_kg * cp_L_mass * (T_out_K - T_in_K)) / tau - Q_met_W
                    End Select

            End Select

            ims.Phases(0).Properties.temperature = T_out_K
            T = T_out_K
            Result_OutletTemperature_K = T_out_K
            Result_Q_duty_kW = Q_duty_W / 1000.0

            ' Reflash the stream at the new temperature
            ims.SpecType = StreamSpec.Temperature_and_Pressure
            PropertyPackage.CurrentMaterialStream = ims
            ims.Calculate(True, True)

            ' -------------------------------------------------------
            ' Split outlet into Broth (liquid) and Offgas (CO2 + residual O2).
            ' Volatile species (CO2 produced metabolically and any O2 not consumed) go to
            ' the off-gas port; substrate, biomass, product, water and NH3 stay in the broth.
            Dim brothMass As New Dictionary(Of String, Double)
            Dim offgasMass As New Dictionary(Of String, Double)
            For Each kvp In compounds
                brothMass(kvp.Key) = newMass(kvp.Key)
                offgasMass(kvp.Key) = 0.0
            Next
            If co2 IsNot Nothing Then
                offgasMass(co2.Name) = brothMass(co2.Name)
                brothMass(co2.Name) = 0.0
            End If
            If IsAerobic AndAlso o2 IsNot Nothing Then
                offgasMass(o2.Name) = brothMass(o2.Name)
                brothMass(o2.Name) = 0.0
            End If

            Dim totalBrothMass As Double = 0.0
            Dim totalOffgasMass As Double = 0.0
            For Each v In brothMass.Values : totalBrothMass += v : Next
            For Each v In offgasMass.Values : totalOffgasMass += v : Next

            ' Copy results to actual outlet stream(s)
            Dim ms_out As MaterialStream = Nothing
            Dim cp = Me.GraphicObject.OutputConnectors(0)
            If cp.IsAttached Then
                ms_out = FlowSheet.SimulationObjects(cp.AttachedConnector.AttachedTo.Name)
                WriteSplitStream(ms_out, brothMass, totalBrothMass,
                                 ims.Phases(0).Properties.temperature.GetValueOrDefault,
                                 ims.Phases(0).Properties.pressure.GetValueOrDefault)
            End If

            ' Off-gas outlet (port 1) - only written when connected
            If Me.GraphicObject.OutputConnectors.Count > 1 Then
                Dim cpGas = Me.GraphicObject.OutputConnectors(1)
                If cpGas.IsAttached Then
                    Dim msGas As MaterialStream = FlowSheet.SimulationObjects(cpGas.AttachedConnector.AttachedTo.Name)
                    WriteSplitStream(msGas, offgasMass, totalOffgasMass,
                                     ims.Phases(0).Properties.temperature.GetValueOrDefault,
                                     ims.Phases(0).Properties.pressure.GetValueOrDefault)
                End If
            End If

            ' Energy stream - publish the duty computed by the thermal mode.
            ' DWSIM EnergyFlow convention: kW added to the unit (positive = heating).
            DeltaQ = Result_Q_duty_kW
            If GetInletEnergyStream(1) IsNot Nothing Then
                With GetInletEnergyStream(1)
                    .EnergyFlow = Result_Q_duty_kW
                    .GraphicObject.Calculated = True
                End With
            End If

            OutletTemperature = T

        End Sub

        ''' <summary>
        ''' Enzymatic hydrolysis of cellulose (and optionally hemicellulose) to glucose (and xylose),
        ''' with competitive product inhibition. Stoichiometry:
        '''   (C6H10O5)n + n H2O -> n C6H12O6          (1.111 g glucose per g cellulose)
        '''   (C5H8O4)n  + n H2O -> n C5H10O5          (1.136 g xylose per g xylan)
        ''' Rate law (g/L/h):
        '''   r_cell = k1 Â· E Â· C_cell / (1 + G/K_G + X/K_X)
        '''   r_hemi = k2 Â· E Â· C_hemi / (1 + G/K_G + X/K_X)
        ''' Reuses the compound role map: Substrate -> cellulose, Product -> glucose,
        ''' Hemicellulose / Xylose / Enzyme via dedicated role properties.
        ''' </summary>
        Private Sub CalculateEnzymaticHydrolysis()

            If String.IsNullOrEmpty(SubstrateCompound) Then
                Throw New Exception("BioReactor (EH): Substrate (cellulose) compound not selected.")
            End If
            If String.IsNullOrEmpty(ProductCompound) Then
                Throw New Exception("BioReactor (EH): Product (glucose) compound not selected.")
            End If
            If Not Me.GraphicObject.InputConnectors(0).IsAttached Then
                Throw New Exception("BioReactor (EH): Inlet stream not connected.")
            End If
            If Not Me.GraphicObject.OutputConnectors(0).IsAttached Then
                Throw New Exception("BioReactor (EH): Outlet stream not connected.")
            End If
            If Volume <= 0.0 Then
                Throw New Exception("BioReactor (EH): Working volume must be positive.")
            End If

            Dim ims As MaterialStream =
                DirectCast(FlowSheet.SimulationObjects(Me.GraphicObject.InputConnectors(0).AttachedConnector.AttachedFrom.Name), MaterialStream).Clone
            ims.SetFlowsheet(Me.FlowSheet)
            ims.SetPropertyPackage(PropertyPackage)
            PropertyPackage.CurrentMaterialStream = ims
            ims.DefinedFlow = FlowSpec.Mass

            Dim T As Double = ims.Phases(0).Properties.temperature.GetValueOrDefault
            Dim P0 As Double = ims.Phases(0).Properties.pressure.GetValueOrDefault
            Dim P As Double = P0 - DeltaP.GetValueOrDefault
            ims.Phases(0).Properties.pressure = P

            Select Case ReactorOperationMode
                Case OperationMode.OutletTemperature
                    T = OutletTemperature
                    ims.Phases(0).Properties.temperature = T
                    ims.SpecType = StreamSpec.Temperature_and_Pressure
                    ims.Calculate(True, True)
            End Select

            Dim compounds = ims.Phases(0).Compounds

            If Not compounds.ContainsKey(SubstrateCompound) Then _
                Throw New Exception("BioReactor (EH): Cellulose compound '" & SubstrateCompound & "' not present in stream.")
            If Not compounds.ContainsKey(ProductCompound) Then _
                Throw New Exception("BioReactor (EH): Glucose compound '" & ProductCompound & "' not present in stream.")

            Dim cell = compounds(SubstrateCompound)
            Dim glu = compounds(ProductCompound)
            Dim hemi As Compound = Nothing
            Dim xyl As Compound = Nothing
            Dim enz As Compound = Nothing
            Dim h2o As Compound = Nothing
            If Not String.IsNullOrEmpty(HemicelluloseCompound) AndAlso compounds.ContainsKey(HemicelluloseCompound) Then hemi = compounds(HemicelluloseCompound)
            If Not String.IsNullOrEmpty(XyloseCompound) AndAlso compounds.ContainsKey(XyloseCompound) Then xyl = compounds(XyloseCompound)
            If Not String.IsNullOrEmpty(EnzymeCompound) AndAlso compounds.ContainsKey(EnzymeCompound) Then enz = compounds(EnzymeCompound)
            If Not String.IsNullOrEmpty(WaterCompound) AndAlso compounds.ContainsKey(WaterCompound) Then h2o = compounds(WaterCompound)

            ' Liquid volumetric flow
            Dim Q_liquid As Double = ims.Phases(1).Properties.volumetric_flow.GetValueOrDefault
            If Q_liquid <= 0.0 Then Q_liquid = ims.Phases(0).Properties.volumetric_flow.GetValueOrDefault
            If Q_liquid <= 0.0 Then Q_liquid = 0.000000000001

            Dim rho_L = ims.Phases(1).Properties.density.GetValueOrDefault
            If rho_L <= 0.0 Then rho_L = 1000.0

            ' Inlet concentrations (g/L)
            Dim Cc = cell.MassFlow.GetValueOrDefault / Q_liquid
            Dim Cg = glu.MassFlow.GetValueOrDefault / Q_liquid
            Dim Ch As Double = 0.0
            Dim Cx As Double = 0.0
            If hemi IsNot Nothing Then Ch = hemi.MassFlow.GetValueOrDefault / Q_liquid
            If xyl IsNot Nothing Then Cx = xyl.MassFlow.GetValueOrDefault / Q_liquid

            ' Enzyme concentration: override from EH_EnzymeLoading_gL if set, else read from stream compound
            Dim E_gL As Double = EH_EnzymeLoading_gL
            If E_gL <= 0.0 AndAlso enz IsNot Nothing Then
                E_gL = enz.MassFlow.GetValueOrDefault / Q_liquid
            End If
            If E_gL <= 0.0 Then E_gL = 0.0 ' allow zero - rates collapse to zero

            ' Integration time
            Dim tau As Double
            Select Case OperatingMode
                Case BioReactorMode.Continuous
                    tau = Volume / Q_liquid
                Case BioReactorMode.Batch, BioReactorMode.FedBatch
                    tau = BatchDuration
                Case Else
                    tau = Volume / Q_liquid
            End Select

            ' Forward Euler integration
            Dim k1_SI = EH_k1_Lgh / 3600.0
            Dim k2_SI = EH_k2_Lgh / 3600.0
            Dim tt As Double = 0.0
            Dim dt As Double = Max(tau / 500.0, 1.0)
            Dim sumRCell As Double = 0.0
            Dim sumRHemi As Double = 0.0

            ' --- Trajectory capture ---
            Dim traj As New BioReactorTrajectoryResult() With {.Mode = "EnzymaticHydrolysis"}
            LastTrajectory = traj
            Dim nStepsExpectedEH = Max(1, CInt(Math.Ceiling(tau / Max(dt, 0.000000000001))))
            Dim sampleIntervalEH As Integer = Max(1, nStepsExpectedEH \ 500)
            Dim maxSamplesEH As Integer = 2000
            Dim stepCountEH As Integer = 0
            traj.Times.Add(0.0)
            traj.Cellulose.Add(Cc) : traj.Hemicellulose.Add(Ch)
            traj.Glucose.Add(Cg) : traj.Xylose.Add(Cx)

            While tt < tau
                Dim h = Min(dt, tau - tt)
                Dim inhib = 1.0 + Cg / Max(EH_KG_glucose_gL, 0.000000000001) +
                            Cx / Max(EH_KX_xylose_gL, 0.000000000001)
                Dim rC = k1_SI * E_gL * Cc / inhib ' g/L/s
                Dim rH = k2_SI * E_gL * Ch / inhib
                Dim dCc = -rC * h
                Dim dCh = -rH * h
                Cc = Max(Cc + dCc, 0.0)
                Ch = Max(Ch + dCh, 0.0)
                Cg += -dCc * 1.111  ' 180.16 / 162.14
                Cx += -dCh * 1.1364 ' 150.13 / 132.12
                sumRCell += -dCc
                sumRHemi += -dCh
                tt += h
                stepCountEH += 1

                If traj.Times.Count < maxSamplesEH AndAlso (stepCountEH Mod sampleIntervalEH = 0) Then
                    traj.Times.Add(tt)
                    traj.Cellulose.Add(Cc) : traj.Hemicellulose.Add(Ch)
                    traj.Glucose.Add(Cg) : traj.Xylose.Add(Cx)
                    If traj.Times.Count >= maxSamplesEH \ 2 Then
                        sampleIntervalEH = Max(sampleIntervalEH, sampleIntervalEH * 2)
                    End If
                End If
            End While

            If traj.Times.Count = 0 OrElse traj.Times(traj.Times.Count - 1) < tt - 0.000000001 Then
                traj.Times.Add(tt)
                traj.Cellulose.Add(Cc) : traj.Hemicellulose.Add(Ch)
                traj.Glucose.Add(Cg) : traj.Xylose.Add(Cx)
            End If

            ' Mass flow changes (kg/s)
            Dim dm_cell = -sumRCell * Q_liquid
            Dim dm_hemi = -sumRHemi * Q_liquid
            Dim dm_glu = sumRCell * 1.111 * Q_liquid
            Dim dm_xyl = sumRHemi * 1.1364 * Q_liquid
            ' Water consumed: 0.111 g/g cell + 0.136 g/g hemi
            Dim dm_h2o = -(sumRCell * 0.111 + sumRHemi * 0.1364) * Q_liquid

            Dim newMass As New Dictionary(Of String, Double)
            Dim totalNewMass As Double = 0.0
            For Each kvp In compounds
                newMass(kvp.Key) = kvp.Value.MassFlow.GetValueOrDefault
            Next
            newMass(cell.Name) = Max(newMass(cell.Name) + dm_cell, 0.0)
            newMass(glu.Name) = Max(newMass(glu.Name) + dm_glu, 0.0)
            If hemi IsNot Nothing Then newMass(hemi.Name) = Max(newMass(hemi.Name) + dm_hemi, 0.0)
            If xyl IsNot Nothing Then newMass(xyl.Name) = Max(newMass(xyl.Name) + dm_xyl, 0.0)
            If h2o IsNot Nothing Then newMass(h2o.Name) = Max(newMass(h2o.Name) + dm_h2o, 0.0)

            For Each kvp In newMass
                totalNewMass += kvp.Value
            Next
            If totalNewMass <= 0 Then totalNewMass = ims.Phases(0).Properties.massflow.GetValueOrDefault

            For Each comp In compounds.Values
                comp.MassFraction = newMass(comp.Name) / totalNewMass
            Next
            Dim invMWsum As Double = 0.0
            For Each comp In compounds.Values
                invMWsum += comp.MassFraction.GetValueOrDefault / comp.ConstantProperties.Molar_Weight
            Next
            For Each comp In compounds.Values
                comp.MoleFraction = (comp.MassFraction.GetValueOrDefault / comp.ConstantProperties.Molar_Weight) / invMWsum
            Next
            ims.Phases(0).Properties.massflow = totalNewMass
            ims.DefinedFlow = FlowSpec.Mass

            ' Results (reuse the same result fields; no biomass, no gas exchange)
            Result_X_gL = 0.0
            Result_S_gL = Cc
            Result_P_gL = Cg
            Result_Mu_h = 0.0
            Result_OUR_gLh = 0.0
            Result_CER_gLh = 0.0
            Result_RQ = 0.0

            ' Thermal balance - EH is mildly exothermic; parameterized per g of sugar produced
            Dim sugar_kg_s As Double = Max(dm_glu, 0.0) + Max(dm_xyl, 0.0) ' kg/s sugar produced
            Dim Q_met_W = Abs(EH_HeatPerGProduct_Jg) * sugar_kg_s * 1000.0 ' J/g Â· g/s = W ; sign convention: positive = exothermic
            If EH_HeatPerGProduct_Jg > 0.0 Then Q_met_W = -Q_met_W ' endothermic override
            Result_Q_metabolic_kW = Q_met_W / 1000.0

            Dim cp_L_mass As Double = 0.0
            Try
                cp_L_mass = ims.Phases(1).Properties.heatCapacityCp.GetValueOrDefault * 1000.0
            Catch
            End Try
            If cp_L_mass <= 0.0 Then
                Try
                    cp_L_mass = ims.Phases(0).Properties.heatCapacityCp.GetValueOrDefault * 1000.0
                Catch
                End Try
            End If
            If cp_L_mass <= 0.0 Then cp_L_mass = 4180.0

            Dim m_dot_kgs As Double = ims.Phases(0).Properties.massflow.GetValueOrDefault
            Dim m_holdup_kg As Double = rho_L * Volume

            Dim T_in_K = T
            Dim T_out_K = T_in_K
            Dim Q_duty_W = 0.0

            Select Case ThermalMode
                Case BioReactorThermalMode.Isothermal
                    T_out_K = T_in_K
                    Q_duty_W = -Q_met_W
                Case BioReactorThermalMode.Adiabatic
                    Q_duty_W = 0.0
                    Select Case OperatingMode
                        Case BioReactorMode.Continuous
                            If m_dot_kgs > 0.0 Then T_out_K = T_in_K + Q_met_W / (m_dot_kgs * cp_L_mass)
                        Case BioReactorMode.Batch, BioReactorMode.FedBatch
                            If m_holdup_kg > 0.0 Then T_out_K = T_in_K + (Q_met_W * tau) / (m_holdup_kg * cp_L_mass)
                    End Select
                Case BioReactorThermalMode.DefinedOutletTemperature
                    If OutletTemperature > 0.0 Then T_out_K = OutletTemperature Else T_out_K = T_in_K
                    Select Case OperatingMode
                        Case BioReactorMode.Continuous
                            Q_duty_W = m_dot_kgs * cp_L_mass * (T_out_K - T_in_K) - Q_met_W
                        Case BioReactorMode.Batch, BioReactorMode.FedBatch
                            If tau > 0.0 Then _
                                Q_duty_W = (m_holdup_kg * cp_L_mass * (T_out_K - T_in_K)) / tau - Q_met_W
                    End Select
            End Select

            ims.Phases(0).Properties.temperature = T_out_K
            T = T_out_K
            Result_OutletTemperature_K = T_out_K
            Result_Q_duty_kW = Q_duty_W / 1000.0

            ims.SpecType = StreamSpec.Temperature_and_Pressure
            PropertyPackage.CurrentMaterialStream = ims
            ims.Calculate(True, True)

            ' Copy to outlet stream
            Dim ms_out As MaterialStream = Nothing
            Dim cpt = Me.GraphicObject.OutputConnectors(0)
            If cpt.IsAttached Then
                ms_out = FlowSheet.SimulationObjects(cpt.AttachedConnector.AttachedTo.Name)
                With ms_out
                    .ClearAllProps()
                    .Phases(0).Properties.temperature = ims.Phases(0).Properties.temperature
                    .Phases(0).Properties.pressure = ims.Phases(0).Properties.pressure
                    For Each c In .Phases(0).Compounds.Values
                        If ims.Phases(0).Compounds.ContainsKey(c.Name) Then
                            c.MassFraction = ims.Phases(0).Compounds(c.Name).MassFraction
                            c.MoleFraction = ims.Phases(0).Compounds(c.Name).MoleFraction
                        End If
                    Next
                    .Phases(0).Properties.massflow = totalNewMass
                    .DefinedFlow = FlowSpec.Mass
                    .SpecType = StreamSpec.Temperature_and_Pressure
                End With
            End If

            DeltaQ = Result_Q_duty_kW
            If GetInletEnergyStream(1) IsNot Nothing Then
                With GetInletEnergyStream(1)
                    .EnergyFlow = Result_Q_duty_kW
                    .GraphicObject.Calculated = True
                End With
            End If

            OutletTemperature = T

        End Sub

        Public Overrides Sub DeCalculate()

            Dim cp = Me.GraphicObject.OutputConnectors(0)
            If cp.IsAttached Then
                Dim ms As MaterialStream = FlowSheet.SimulationObjects(cp.AttachedConnector.AttachedTo.Name)
                With ms
                    .Phases(0).Properties.temperature = Nothing
                    .Phases(0).Properties.pressure = Nothing
                    .Phases(0).Properties.enthalpy = Nothing
                    For Each comp In .Phases(0).Compounds.Values
                        comp.MoleFraction = 0
                        comp.MassFraction = 0
                    Next
                    .Phases(0).Properties.massflow = Nothing
                    .GraphicObject.Calculated = False
                End With
            End If

        End Sub

        Public Overrides Function GetIconBitmapBytes() As Byte()
            Return UnitOperations.BioOpsDrawHelper.RenderIconToPngBytes(64, 64, AddressOf DrawIcon)
        End Function

        Public Overrides Function GetDisplayDescription() As String
            Return "Microbial bioreactor with Monod-family kinetics"
        End Function

        Public Overrides Function GetDisplayName() As String
            Return "BioReactor"
        End Function

        Public Overrides ReadOnly Property MobileCompatible As Boolean
            Get
                Return False
            End Get
        End Property

        Public Overrides Function GetReport(su As IUnitsOfMeasure, ci As Globalization.CultureInfo, numberformat As String) As String

            Dim str As New Text.StringBuilder
            str.AppendLine("BioReactor:  " & Me.GraphicObject.Tag)
            str.AppendLine("Property Package: " & Me.PropertyPackage.ComponentName)
            str.AppendLine()
            str.AppendLine("Configuration")
            str.AppendLine("    Mode: " & OperatingMode.ToString)
            str.AppendLine("    Kinetic Model: " & KineticModel.ToString)
            str.AppendLine("    Aerobic: " & IsAerobic.ToString)
            str.AppendLine("    Biomass: " & BiomassCompound)
            str.AppendLine("    Substrate: " & SubstrateCompound)
            If ProductCompound <> "" Then str.AppendLine("    Product: " & ProductCompound)
            str.AppendLine("    Volume: " & Volume.ToString(numberformat, ci) & " m3")
            str.AppendLine()
            str.AppendLine("Kinetic Parameters")
            str.AppendLine("    mu_max: " & MuMax_h.ToString(numberformat, ci) & " 1/h")
            str.AppendLine("    Ks:     " & Ks_gL.ToString(numberformat, ci) & " g/L")
            str.AppendLine("    Y_XS:   " & YieldXS.ToString(numberformat, ci))
            str.AppendLine("    Y_PS:   " & YieldPS.ToString(numberformat, ci))
            str.AppendLine("    ms:     " & Maintenance_gSg_cellh.ToString(numberformat, ci) & " g/g/h")
            str.AppendLine("    kd:     " & DeathRate_h.ToString(numberformat, ci) & " 1/h")
            str.AppendLine()
            If KineticModel = BioKineticModel.EnzymaticHydrolysis Then
                str.AppendLine("Enzymatic Hydrolysis Parameters")
                str.AppendLine("    Cellulose (Substrate):    " & SubstrateCompound)
                str.AppendLine("    Glucose (Product):        " & ProductCompound)
                If HemicelluloseCompound <> "" Then str.AppendLine("    Hemicellulose:            " & HemicelluloseCompound)
                If XyloseCompound <> "" Then str.AppendLine("    Xylose:                   " & XyloseCompound)
                If EnzymeCompound <> "" Then str.AppendLine("    Enzyme:                   " & EnzymeCompound)
                str.AppendLine("    k1 (cellulose):           " & EH_k1_Lgh.ToString(numberformat, ci) & " L/(gÂ·h)")
                str.AppendLine("    k2 (hemicellulose):       " & EH_k2_Lgh.ToString(numberformat, ci) & " L/(gÂ·h)")
                str.AppendLine("    K_G (glucose inhibition): " & EH_KG_glucose_gL.ToString(numberformat, ci) & " g/L")
                str.AppendLine("    K_X (xylose inhibition):  " & EH_KX_xylose_gL.ToString(numberformat, ci) & " g/L")
                str.AppendLine("    Enzyme loading (default): " & EH_EnzymeLoading_gL.ToString(numberformat, ci) & " g/L")
                str.AppendLine()
                str.AppendLine("Results")
                str.AppendLine("    Residual Cellulose [S]:   " & Result_S_gL.ToString(numberformat, ci) & " g/L")
                str.AppendLine("    Glucose [P]:              " & Result_P_gL.ToString(numberformat, ci) & " g/L")
            Else
                str.AppendLine("Results")
                str.AppendLine("    Biomass [X]:  " & Result_X_gL.ToString(numberformat, ci) & " g/L")
                str.AppendLine("    Substrate [S]: " & Result_S_gL.ToString(numberformat, ci) & " g/L")
                str.AppendLine("    Product [P]:  " & Result_P_gL.ToString(numberformat, ci) & " g/L")
                str.AppendLine("    Avg mu:       " & Result_Mu_h.ToString(numberformat, ci) & " 1/h")
                str.AppendLine("    OUR:          " & Result_OUR_gLh.ToString(numberformat, ci) & " g O2/L/h")
                str.AppendLine("    CER:          " & Result_CER_gLh.ToString(numberformat, ci) & " g CO2/L/h")
                str.AppendLine("    RQ:           " & Result_RQ.ToString(numberformat, ci))
            End If
            str.AppendLine()
            str.AppendLine("Thermal Balance")
            str.AppendLine("    Mode:              " & ThermalMode.ToString)
            str.AppendLine("    Metabolic heat:    " & Result_Q_metabolic_kW.ToString(numberformat, ci) & " kW")
            str.AppendLine("    Net heat duty:     " & Result_Q_duty_kW.ToString(numberformat, ci) & " kW  (+ heating / âˆ’ cooling)")
            str.AppendLine("    Outlet temperature:" & Result_OutletTemperature_K.ToString(numberformat, ci) & " K")
            Return str.ToString()

        End Function

        Private Shared ReadOnly _inputProps As String() = {
            "Working Volume",
            "Batch Duration",
            "Operating Mode",
            "Thermal Mode",
            "Heat per mol O2",
            "Kinetic Model",
            "Aerobic",
            "Biomass Compound",
            "Substrate Compound",
            "Product Compound",
            "Oxygen Compound",
            "CO2 Compound",
            "Nitrogen Source Compound",
            "Sulfur Source Compound",
            "Water Compound",
            "Max Specific Growth Rate",
            "Saturation Constant",
            "Inhibition Constant",
            "Moser Exponent",
            "Biomass Yield on Substrate",
            "Product Yield on Substrate",
            "Maintenance Coefficient",
            "Death Rate Constant",
            "Volumetric Oxygen Transfer Coefficient",
            "Dissolved Oxygen Saturation",
            "User Kinetics Script Name",
            "Hemicellulose Compound",
            "Xylose Compound",
            "Enzyme Compound",
            "EH Cellulose Rate Constant",
            "EH Hemicellulose Rate Constant",
            "EH Glucose Inhibition Constant",
            "EH Xylose Inhibition Constant",
            "EH Enzyme Loading",
            "EH Heat Per Gram Product"
        }

        Private Shared ReadOnly _outputProps As String() = {
            "Outlet Biomass Concentration",
            "Outlet Substrate Concentration",
            "Outlet Product Concentration",
            "Average Specific Growth Rate",
            "Oxygen Uptake Rate",
            "Carbon Dioxide Evolution Rate",
            "Respiratory Quotient",
            "Metabolic Heat Duty",
            "Net Heat Duty",
            "Outlet Temperature"
        }

        Public Overrides Function GetProperties(proptype As PropertyType) As String()
            Dim baseprops = MyBase.GetProperties(proptype)
            Select Case proptype
                Case PropertyType.WR
                    Return _inputProps
                Case PropertyType.RO
                    Return _outputProps
                Case Else
                    Return _inputProps.Concat(_outputProps).Concat(baseprops).ToArray()
            End Select
        End Function

        Public Overrides Function GetPropertyValue(prop As String, Optional su As IUnitsOfMeasure = Nothing) As Object
            Select Case prop
                Case "Working Volume" : Return Volume
                Case "Batch Duration" : Return BatchDuration
                Case "Operating Mode" : Return OperatingMode.ToString()
                Case "Thermal Mode" : Return ThermalMode.ToString()
                Case "Heat per mol O2" : Return HeatPerMolO2_JmolO2
                Case "Kinetic Model" : Return KineticModel.ToString()
                Case "Aerobic" : Return IsAerobic
                Case "Biomass Compound" : Return BiomassCompound
                Case "Substrate Compound" : Return SubstrateCompound
                Case "Product Compound" : Return ProductCompound
                Case "Oxygen Compound" : Return OxygenCompound
                Case "CO2 Compound" : Return CO2Compound
                Case "Nitrogen Source Compound" : Return NitrogenSourceCompound
                Case "Sulfur Source Compound" : Return SulfurSourceCompound
                Case "Water Compound" : Return WaterCompound
                Case "Max Specific Growth Rate" : Return MuMax_h
                Case "Saturation Constant" : Return Ks_gL
                Case "Inhibition Constant" : Return Ki_gL
                Case "Moser Exponent" : Return MoserN
                Case "Biomass Yield on Substrate" : Return YieldXS
                Case "Product Yield on Substrate" : Return YieldPS
                Case "Maintenance Coefficient" : Return Maintenance_gSg_cellh
                Case "Death Rate Constant" : Return DeathRate_h
                Case "Volumetric Oxygen Transfer Coefficient" : Return KLa_h
                Case "Dissolved Oxygen Saturation" : Return CO2sat_gL
                Case "User Kinetics Script Name" : Return UserScriptName
                Case "Hemicellulose Compound" : Return HemicelluloseCompound
                Case "Xylose Compound" : Return XyloseCompound
                Case "Enzyme Compound" : Return EnzymeCompound
                Case "EH Cellulose Rate Constant" : Return EH_k1_Lgh
                Case "EH Hemicellulose Rate Constant" : Return EH_k2_Lgh
                Case "EH Glucose Inhibition Constant" : Return EH_KG_glucose_gL
                Case "EH Xylose Inhibition Constant" : Return EH_KX_xylose_gL
                Case "EH Enzyme Loading" : Return EH_EnzymeLoading_gL
                Case "EH Heat Per Gram Product" : Return EH_HeatPerGProduct_Jg
                Case "Outlet Biomass Concentration" : Return Result_X_gL
                Case "Outlet Substrate Concentration" : Return Result_S_gL
                Case "Outlet Product Concentration" : Return Result_P_gL
                Case "Average Specific Growth Rate" : Return Result_Mu_h
                Case "Oxygen Uptake Rate" : Return Result_OUR_gLh
                Case "Carbon Dioxide Evolution Rate" : Return Result_CER_gLh
                Case "Respiratory Quotient" : Return Result_RQ
                Case "Metabolic Heat Duty" : Return Result_Q_metabolic_kW
                Case "Net Heat Duty" : Return Result_Q_duty_kW
                Case "Outlet Temperature" : Return Result_OutletTemperature_K
                Case Else : Return MyBase.GetPropertyValue(prop, su)
            End Select
        End Function

        Public Overrides Function GetPropertyUnit(prop As String, Optional su As IUnitsOfMeasure = Nothing) As String
            Select Case prop
                Case "Working Volume" : Return "m3"
                Case "Batch Duration" : Return "s"
                Case "Max Specific Growth Rate",
                     "Death Rate Constant",
                     "Volumetric Oxygen Transfer Coefficient",
                     "Average Specific Growth Rate" : Return "1/h"
                Case "Saturation Constant",
                     "Inhibition Constant",
                     "Dissolved Oxygen Saturation",
                     "Outlet Biomass Concentration",
                     "Outlet Substrate Concentration",
                     "Outlet Product Concentration" : Return "g/L"
                Case "Biomass Yield on Substrate",
                     "Product Yield on Substrate" : Return "g/g"
                Case "Maintenance Coefficient" : Return "g/g/h"
                Case "Oxygen Uptake Rate",
                     "Carbon Dioxide Evolution Rate" : Return "g/L/h"
                Case "Metabolic Heat Duty",
                     "Net Heat Duty" : Return "kW"
                Case "Outlet Temperature" : Return "K"
                Case "Heat per mol O2" : Return "J/mol"
                Case "EH Cellulose Rate Constant",
                     "EH Hemicellulose Rate Constant" : Return "L/(g.h)"
                Case "EH Glucose Inhibition Constant",
                     "EH Xylose Inhibition Constant",
                     "EH Enzyme Loading" : Return "g/L"
                Case "EH Heat Per Gram Product" : Return "J/g"
                Case Else : Return ""
            End Select
        End Function

        Public Overrides Function SetPropertyValue(prop As String, propval As Object, Optional su As IUnitsOfMeasure = Nothing) As Boolean
            Dim d As Double = 0.0
            If TypeOf propval Is Double Then
                d = CDbl(propval)
            ElseIf TypeOf propval Is String Then
                Double.TryParse(CStr(propval), Globalization.NumberStyles.Any, Globalization.CultureInfo.CurrentCulture, d)
            End If
            Select Case prop
                Case "Working Volume" : Volume = d : Return True
                Case "Batch Duration" : BatchDuration = d : Return True
                Case "Operating Mode"
                    Dim om As BioReactorMode
                    If [Enum].TryParse(Of BioReactorMode)(propval?.ToString(), om) Then OperatingMode = om
                    Return True
                Case "Thermal Mode"
                    Dim tm As BioReactorThermalMode
                    If [Enum].TryParse(Of BioReactorThermalMode)(propval?.ToString(), tm) Then ThermalMode = tm
                    Return True
                Case "Heat per mol O2" : HeatPerMolO2_JmolO2 = d : Return True
                Case "Kinetic Model"
                    Dim km As BioKineticModel
                    If [Enum].TryParse(Of BioKineticModel)(propval?.ToString(), km) Then KineticModel = km
                    Return True
                Case "Aerobic" : IsAerobic = Convert.ToBoolean(propval) : Return True
                Case "Biomass Compound" : BiomassCompound = propval?.ToString() : Return True
                Case "Substrate Compound" : SubstrateCompound = propval?.ToString() : Return True
                Case "Product Compound" : ProductCompound = propval?.ToString() : Return True
                Case "Oxygen Compound" : OxygenCompound = propval?.ToString() : Return True
                Case "CO2 Compound" : CO2Compound = propval?.ToString() : Return True
                Case "Nitrogen Source Compound" : NitrogenSourceCompound = propval?.ToString() : Return True
                Case "Sulfur Source Compound" : SulfurSourceCompound = propval?.ToString() : Return True
                Case "Water Compound" : WaterCompound = propval?.ToString() : Return True
                Case "Max Specific Growth Rate" : MuMax_h = d : Return True
                Case "Saturation Constant" : Ks_gL = d : Return True
                Case "Inhibition Constant" : Ki_gL = d : Return True
                Case "Moser Exponent" : MoserN = d : Return True
                Case "Biomass Yield on Substrate" : YieldXS = d : Return True
                Case "Product Yield on Substrate" : YieldPS = d : Return True
                Case "Maintenance Coefficient" : Maintenance_gSg_cellh = d : Return True
                Case "Death Rate Constant" : DeathRate_h = d : Return True
                Case "Volumetric Oxygen Transfer Coefficient" : KLa_h = d : Return True
                Case "Dissolved Oxygen Saturation" : CO2sat_gL = d : Return True
                Case "User Kinetics Script Name" : UserScriptName = propval?.ToString() : Return True
                Case "Hemicellulose Compound" : HemicelluloseCompound = propval?.ToString() : Return True
                Case "Xylose Compound" : XyloseCompound = propval?.ToString() : Return True
                Case "Enzyme Compound" : EnzymeCompound = propval?.ToString() : Return True
                Case "EH Cellulose Rate Constant" : EH_k1_Lgh = d : Return True
                Case "EH Hemicellulose Rate Constant" : EH_k2_Lgh = d : Return True
                Case "EH Glucose Inhibition Constant" : EH_KG_glucose_gL = d : Return True
                Case "EH Xylose Inhibition Constant" : EH_KX_xylose_gL = d : Return True
                Case "EH Enzyme Loading" : EH_EnzymeLoading_gL = d : Return True
                Case "EH Heat Per Gram Product" : EH_HeatPerGProduct_Jg = d : Return True
                Case Else : Return MyBase.SetPropertyValue(prop, propval, su)
            End Select
        End Function

        ' ======================================================================
        ' IExternalUnitOperation implementation
        ' ======================================================================

        Private ReadOnly Property IEUO_Name As String Implements IExternalUnitOperation.Name
            Get
                Return GetDisplayName()
            End Get
        End Property

        Private ReadOnly Property IEUO_Description As String Implements IExternalUnitOperation.Description
            Get
                Return GetDisplayDescription()
            End Get
        End Property

        Public ReadOnly Property Prefix As String Implements IExternalUnitOperation.Prefix
            Get
                Return "BIO-"
            End Get
        End Property

        Public Function ReturnInstance(typename As String) As Object Implements IExternalUnitOperation.ReturnInstance
            Return New Reactor_BioReactor()
        End Function

        Public Sub PopulateEditorPanel(ctner As Object) Implements IExternalUnitOperation.PopulateEditorPanel

            If TypeOf ctner Is AvaloniaEditorPanel Then PopulateEditorPanelAvalonia(DirectCast(ctner, AvaloniaEditorPanel)) : Return
        End Sub

        Private Sub PopulateEditorPanelAvalonia(container As AvaloniaEditorPanel)

            Dim su = FlowSheet.FlowsheetOptions.SelectedUnitSystem
            Dim nf = FlowSheet.FlowsheetOptions.NumberFormat
            Dim compIds = FlowSheet.SelectedCompounds.Values.Select(Function(c) c.Name).ToList()

            container.CreateAndAddLabelRow("Operation")

            container.CreateAndAddDropDownRow("Kinetic Model",
                                              New List(Of String)({"Monod", "Contois", "Moser", "Haldane", "User Script", "Enzymatic Hydrolysis"}),
                                              CInt(KineticModel),
                                              Sub(dd, e)
                                                  KineticModel = CType(dd.SelectedIndex, BioKineticModel)
                                                  FlowSheet.RequestCalculation()
                                              End Sub)

            container.CreateAndAddDropDownRow("Operating Mode",
                                              New List(Of String)({"Continuous", "Batch", "Fed-Batch"}),
                                              CInt(OperatingMode),
                                              Sub(dd, e)
                                                  OperatingMode = CType(dd.SelectedIndex, BioReactorMode)
                                                  FlowSheet.RequestCalculation()
                                              End Sub)

            container.CreateAndAddDropDownRow("Thermal Mode",
                                              New List(Of String)({"Isothermal", "Adiabatic", "Defined Outlet Temperature"}),
                                              CInt(ThermalMode),
                                              Sub(dd, e)
                                                  ThermalMode = CType(dd.SelectedIndex, BioReactorThermalMode)
                                                  FlowSheet.RequestCalculation()
                                              End Sub)

            container.CreateAndAddCheckBoxRow("Aerobic", IsAerobic,
                                              Sub(cb, e)
                                                  IsAerobic = cb.IsChecked.GetValueOrDefault()
                                                  FlowSheet.RequestCalculation()
                                              End Sub)

            container.CreateAndAddTextBoxRow(nf, String.Format("Reactor Volume ({0})", su.volume),
                                             Volume.ConvertFromSI(su.volume),
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     Volume = tb.Text.ParseExpressionToDouble().ConvertToSI(su.volume)
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Batch / Residence Duration (s)", BatchDuration,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     BatchDuration = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddLabelRow("Kinetic Parameters")

            container.CreateAndAddTextBoxRow(nf, "mu_max (1/h)", MuMax_h,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     MuMax_h = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Ks (g/L)", Ks_gL,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     Ks_gL = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Ki (g/L, Haldane)", Ki_gL,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     Ki_gL = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Moser n", MoserN,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     MoserN = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Yield Y_X/S (g cells / g substrate)", YieldXS,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     YieldXS = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Yield Y_P/S (g product / g substrate)", YieldPS,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     YieldPS = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Maintenance m_s (g/gÂ·h)", Maintenance_gSg_cellh,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     Maintenance_gSg_cellh = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Death Rate (1/h)", DeathRate_h,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     DeathRate_h = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddLabelRow("Mass Transfer")

            container.CreateAndAddTextBoxRow(nf, "kLa (1/h)", KLa_h,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     KLa_h = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "CO2 Saturation (g/L)", CO2sat_gL,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     CO2sat_gL = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Heat per mol O2 (J/mol)", HeatPerMolO2_JmolO2,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     HeatPerMolO2_JmolO2 = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddLabelRow("Enzymatic Hydrolysis (EnzymaticHydrolysis mode only)")

            container.CreateAndAddTextBoxRow(nf, "k1 Cellulose-Glucose (L/gÂ·h)", EH_k1_Lgh,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     EH_k1_Lgh = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "k2 Hemicellulose-Xylose (L/gÂ·h)", EH_k2_Lgh,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     EH_k2_Lgh = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "KG Glucose Inhibition (g/L)", EH_KG_glucose_gL,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     EH_KG_glucose_gL = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "KX Xylose Inhibition (g/L)", EH_KX_xylose_gL,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     EH_KX_xylose_gL = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Enzyme Loading (g/L)", EH_EnzymeLoading_gL,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     EH_EnzymeLoading_gL = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddLabelRow("Compound Mapping")

            Dim addCompoundDropdownA =
                Sub(label As String, currentValue As String, setter As Action(Of String))
                    Dim idx = compIds.IndexOf(currentValue)
                    container.CreateAndAddDropDownRow(label,
                                                      New List(Of String)(New String() {"(none)"}.Concat(compIds)),
                                                      If(idx < 0, 0, idx + 1),
                                                      Sub(dd, e)
                                                          setter(If(dd.SelectedIndex > 0, compIds(dd.SelectedIndex - 1), ""))
                                                          FlowSheet.RequestCalculation()
                                                      End Sub)
                End Sub

            addCompoundDropdownA("Biomass", BiomassCompound, Sub(v) BiomassCompound = v)
            addCompoundDropdownA("Substrate", SubstrateCompound, Sub(v) SubstrateCompound = v)
            addCompoundDropdownA("Product", ProductCompound, Sub(v) ProductCompound = v)
            addCompoundDropdownA("Oxygen", OxygenCompound, Sub(v) OxygenCompound = v)
            addCompoundDropdownA("CO2", CO2Compound, Sub(v) CO2Compound = v)
            addCompoundDropdownA("Nitrogen Source", NitrogenSourceCompound, Sub(v) NitrogenSourceCompound = v)
            addCompoundDropdownA("Sulfur Source", SulfurSourceCompound, Sub(v) SulfurSourceCompound = v)
            addCompoundDropdownA("Water", WaterCompound, Sub(v) WaterCompound = v)
            addCompoundDropdownA("Hemicellulose (EH)", HemicelluloseCompound, Sub(v) HemicelluloseCompound = v)
            addCompoundDropdownA("Xylose (EH)", XyloseCompound, Sub(v) XyloseCompound = v)
            addCompoundDropdownA("Enzyme (EH)", EnzymeCompound, Sub(v) EnzymeCompound = v)

        End Sub

        Public Sub CreateConnectors() Implements IExternalUnitOperation.CreateConnectors

            If GraphicObject Is Nothing Then Return

            Dim w = GraphicObject.Width
            Dim h = GraphicObject.Height
            Dim gx = GraphicObject.X
            Dim gy = GraphicObject.Y

            If GraphicObject.InputConnectors.Count = 2 AndAlso GraphicObject.OutputConnectors.Count = 2 Then

                ' Position-only update
                GraphicObject.InputConnectors(0).Position = New Point(gx, gy + 0.5 * h)
                GraphicObject.InputConnectors(0).ConnectorName = "Inlet (Feed)"
                GraphicObject.InputConnectors(1).Position = New Point(gx + 0.25 * w, gy + h)
                GraphicObject.InputConnectors(1).ConnectorName = "Sparger Gas Inlet (Optional)"
                GraphicObject.InputConnectors(1).Direction = ConDir.Up

                GraphicObject.OutputConnectors(0).Position = New Point(gx + w, gy + 0.7 * h)
                GraphicObject.OutputConnectors(0).ConnectorName = "Broth Outlet"
                GraphicObject.OutputConnectors(1).Position = New Point(gx + 0.5 * w, gy)
                GraphicObject.OutputConnectors(1).ConnectorName = "Offgas Outlet"
                GraphicObject.OutputConnectors(1).Direction = ConDir.Up

            Else

                GraphicObject.InputConnectors.Clear()
                GraphicObject.OutputConnectors.Clear()

                GraphicObject.InputConnectors.Add(New ConnectionPoint With {
                    .Position = New Point(gx, gy + 0.5 * h),
                    .Type = ConType.ConIn,
                    .Direction = ConDir.Right,
                    .ConnectorName = "Inlet (Feed)"
                })
                GraphicObject.InputConnectors.Add(New ConnectionPoint With {
                    .Position = New Point(gx + 0.25 * w, gy + h),
                    .Type = ConType.ConIn,
                    .Direction = ConDir.Up,
                    .ConnectorName = "Sparger Gas Inlet (Optional)"
                })
                GraphicObject.OutputConnectors.Add(New ConnectionPoint With {
                    .Position = New Point(gx + w, gy + 0.7 * h),
                    .Type = ConType.ConOut,
                    .Direction = ConDir.Right,
                    .ConnectorName = "Broth Outlet"
                })
                GraphicObject.OutputConnectors.Add(New ConnectionPoint With {
                    .Position = New Point(gx + 0.5 * w, gy),
                    .Type = ConType.ConOut,
                    .Direction = ConDir.Up,
                    .ConnectorName = "Offgas Outlet"
                })
            End If

            GraphicObject.EnergyConnector.Position = New Point(gx + 0.75 * w, gy + h)
            GraphicObject.EnergyConnector.Direction = ConDir.Up
            GraphicObject.EnergyConnector.Active = True
            GraphicObject.EnergyConnector.ConnectorName = "Heat Duty"

        End Sub

        <NonSerialized> <Xml.Serialization.XmlIgnore> Private _photoImage As SKImage

        Public Sub Draw(g As Object) Implements IExternalUnitOperation.Draw

            If GraphicObject Is Nothing Then Return

            Dim canvas As SKCanvas = DirectCast(g, SKCanvas)

            If GraphicObject.DrawMode = 2 Then
                If UnitOperations.BioOpsDrawHelper.TryDrawPhotorealistic(canvas,
                    GraphicObject.X, GraphicObject.Y, GraphicObject.Width, GraphicObject.Height,
                    "bioreactor_photo", _photoImage) Then Return
                ' fallback to icon
            End If

            DrawIcon(canvas, CSng(GraphicObject.X), CSng(GraphicObject.Y),
                     CSng(GraphicObject.Width), CSng(GraphicObject.Height),
                     GraphicObject.DrawMode = 1)

        End Sub

        Private Shared Sub DrawIcon(canvas As SKCanvas, gx As Single, gy As Single, w As Single, h As Single, Optional mono As Boolean = False)
            ' Stirred-tank bioreactor: stainless vessel on skid, top-mounted motor + agitator shaft w/ Rushton turbines,
            ' sparger ring near bottom, offgas nozzle, feed nozzle.
            Dim skid As New SKRect(gx + 0.08F * w, gy + 0.9F * h, gx + 0.92F * w, gy + h)
            UnitOperations.BioOpsDrawHelper.DrawSkid(canvas, skid, mono)
            Dim vessel As New SKRect(gx + 0.2F * w, gy + 0.22F * h, gx + 0.8F * w, gy + 0.92F * h)
            UnitOperations.BioOpsDrawHelper.DrawVerticalTank(canvas, vessel, mono)
            Dim cx = (vessel.Left + vessel.Right) * 0.5F
            ' top mounting flange on vessel head
            UnitOperations.BioOpsDrawHelper.DrawFlange(canvas, cx, gy + 0.22F * h, 0.24F * w, mono)
            ' lantern / coupling bay between motor and vessel (two small posts)
            Using lantern As New SKPaint With {.Color = UnitOperations.BioOpsDrawHelper.ClrMetalDark(mono), .IsAntialias = True}
                canvas.DrawRect(New SKRect(cx - 0.07F * w, gy + 0.14F * h, cx - 0.05F * w, gy + 0.21F * h), lantern)
                canvas.DrawRect(New SKRect(cx + 0.05F * w, gy + 0.14F * h, cx + 0.07F * w, gy + 0.21F * h), lantern)
            End Using
            Using stroke As New SKPaint With {.Color = UnitOperations.BioOpsDrawHelper.ClrStroke(mono), .Style = SKPaintStyle.Stroke, .StrokeWidth = 0.9F, .IsAntialias = True}
                canvas.DrawRect(New SKRect(cx - 0.07F * w, gy + 0.14F * h, cx - 0.05F * w, gy + 0.21F * h), stroke)
                canvas.DrawRect(New SKRect(cx + 0.05F * w, gy + 0.14F * h, cx + 0.07F * w, gy + 0.21F * h), stroke)
            End Using
            ' top gearbox (wider) + motor (narrower) stacked
            Dim gearbox As New SKRect(cx - 0.12F * w, gy + 0.08F * h, cx + 0.12F * w, gy + 0.15F * h)
            Using gb As New SKPaint With {.Color = UnitOperations.BioOpsDrawHelper.ClrMetalMid(mono), .IsAntialias = True}
                canvas.DrawRoundRect(gearbox, 1.5F, 1.5F, gb)
            End Using
            Using stroke As New SKPaint With {.Color = UnitOperations.BioOpsDrawHelper.ClrStroke(mono), .Style = SKPaintStyle.Stroke, .StrokeWidth = 1.1F, .IsAntialias = True}
                canvas.DrawRoundRect(gearbox, 1.5F, 1.5F, stroke)
            End Using
            Dim motor As New SKRect(cx - 0.08F * w, gy + 0.01F * h, cx + 0.08F * w, gy + 0.08F * h)
            UnitOperations.BioOpsDrawHelper.DrawMotor(canvas, motor, mono)
            ' shaft + two Rushton impellers (start below mounting flange)
            UnitOperations.BioOpsDrawHelper.DrawAgitator(canvas, cx, gy + 0.25F * h, gy + 0.82F * h, 0.32F * w, mono)
            ' sparger ring at bottom (full ring + tiny downward tabs as nozzles)
            Using a As New SKPaint With {.Color = If(mono, New SKColor(60, 60, 60), New SKColor(55, 110, 75)), .Style = SKPaintStyle.Stroke, .StrokeWidth = 1.4F, .IsAntialias = True}
                Dim spY = gy + 0.84F * h
                Dim ring As New SKRect(cx - 0.22F * w, spY - 0.015F * h, cx + 0.22F * w, spY + 0.015F * h)
                canvas.DrawOval(ring, a)
                Dim nH = 5
                For i = 0 To nH - 1
                    Dim hx = cx - 0.18F * w + i * 0.09F * w
                    canvas.DrawLine(hx, spY + 0.01F * h, hx, spY + 0.035F * h, a)
                Next
            End Using
            ' offgas nozzle (top) with flange
            UnitOperations.BioOpsDrawHelper.DrawPipe(canvas, New SKPoint(cx + 0.22F * w, gy + 0.2F * h), New SKPoint(cx + 0.22F * w, gy + 0.3F * h), 0.032F * w, mono)
            UnitOperations.BioOpsDrawHelper.DrawFlange(canvas, cx + 0.22F * w, gy + 0.3F * h, 0.08F * w, mono)
            ' feed nozzle on side
            UnitOperations.BioOpsDrawHelper.DrawPipe(canvas, New SKPoint(gx + 0.02F * w, gy + 0.4F * h), New SKPoint(vessel.Left, gy + 0.4F * h), 0.035F * h, mono)
            UnitOperations.BioOpsDrawHelper.DrawFlange(canvas, vessel.Left, gy + 0.4F * h, 0.08F * w, mono)
        End Sub

        Private Shared Sub DrawIconLegacy(canvas As SKCanvas, gx As Single, gy As Single, w As Single, h As Single)

            ' Reserve 20% top for agitator motor, 75% for vessel body, 5% for bottom dish
            Dim motorH = 0.15F * h
            Dim bodyTop = gy + motorH
            Dim bodyBot = gy + 0.95F * h
            Dim bodyH = bodyBot - bodyTop
            Dim bodyLeft = gx + 0.15F * w
            Dim bodyRight = gx + 0.85F * w
            Dim bodyW = bodyRight - bodyLeft
            Dim cx = gx + 0.5F * w

            Dim broth As New SKPaint With {
                .Color = New SKColor(200, 230, 180, 200),
                .Style = SKPaintStyle.Fill,
                .IsAntialias = True
            }
            Dim body As New SKPaint With {
                .Color = New SKColor(245, 250, 240),
                .Style = SKPaintStyle.Fill,
                .IsAntialias = True
            }
            Dim stroke As New SKPaint With {
                .Color = New SKColor(40, 90, 60),
                .Style = SKPaintStyle.Stroke,
                .StrokeWidth = 1.8F,
                .IsAntialias = True
            }
            Dim accent As New SKPaint With {
                .Color = New SKColor(60, 120, 80),
                .Style = SKPaintStyle.Stroke,
                .StrokeWidth = 1.4F,
                .IsAntialias = True
            }
            Dim motorPaint As New SKPaint With {
                .Color = New SKColor(70, 70, 80),
                .Style = SKPaintStyle.Fill,
                .IsAntialias = True
            }
            Dim bubble As New SKPaint With {
                .Color = New SKColor(255, 255, 255, 220),
                .Style = SKPaintStyle.Fill,
                .IsAntialias = True
            }

            ' Vessel body (tank)
            Dim vessel As New SKRect(bodyLeft, bodyTop, bodyRight, bodyBot)
            canvas.DrawRect(vessel, body)

            ' Broth level (70% fill)
            Dim liquidTop = bodyTop + 0.25F * bodyH
            Dim liquidRect As New SKRect(bodyLeft, liquidTop, bodyRight, bodyBot)
            canvas.DrawRect(liquidRect, broth)

            ' Top dished head
            Dim topDish As New SKRect(bodyLeft, bodyTop - 0.1F * bodyH, bodyRight, bodyTop + 0.1F * bodyH)
            canvas.DrawOval(topDish, body)
            canvas.DrawArc(topDish, 180, 180, False, stroke)

            ' Bottom dished head
            Dim botDish As New SKRect(bodyLeft, bodyBot - 0.1F * bodyH, bodyRight, bodyBot + 0.1F * bodyH)
            canvas.DrawOval(botDish, broth)
            canvas.DrawArc(botDish, 0, 180, False, stroke)

            ' Vessel outline (sides)
            canvas.DrawLine(bodyLeft, bodyTop, bodyLeft, bodyBot, stroke)
            canvas.DrawLine(bodyRight, bodyTop, bodyRight, bodyBot, stroke)

            ' Agitator motor on top
            Dim motorRect As New SKRect(cx - 0.08F * w, gy, cx + 0.08F * w, gy + motorH)
            canvas.DrawRect(motorRect, motorPaint)
            ' Motor fins
            canvas.DrawLine(cx - 0.09F * w, gy + motorH * 0.35F, cx + 0.09F * w, gy + motorH * 0.35F, stroke)
            canvas.DrawLine(cx - 0.09F * w, gy + motorH * 0.65F, cx + 0.09F * w, gy + motorH * 0.65F, stroke)

            ' Impeller shaft
            Dim shaftTop = gy + motorH
            Dim shaftBot = bodyBot - 0.1F * bodyH
            canvas.DrawLine(cx, shaftTop, cx, shaftBot, stroke)

            ' Two Rushton turbines (disk + 6 flat blades seen as short horizontal lines)
            Dim imp1Y = liquidTop + 0.35F * (shaftBot - liquidTop)
            Dim imp2Y = liquidTop + 0.75F * (shaftBot - liquidTop)
            Dim impW = 0.28F * w
            ' Turbine disk + blades (simplified)
            canvas.DrawLine(cx - impW, imp1Y, cx + impW, imp1Y, accent)
            canvas.DrawLine(cx - 0.08F * w, imp1Y - 0.02F * h, cx - 0.08F * w, imp1Y + 0.02F * h, accent)
            canvas.DrawLine(cx + 0.08F * w, imp1Y - 0.02F * h, cx + 0.08F * w, imp1Y + 0.02F * h, accent)
            canvas.DrawLine(cx - impW, imp2Y, cx + impW, imp2Y, accent)
            canvas.DrawLine(cx - 0.08F * w, imp2Y - 0.02F * h, cx - 0.08F * w, imp2Y + 0.02F * h, accent)
            canvas.DrawLine(cx + 0.08F * w, imp2Y - 0.02F * h, cx + 0.08F * w, imp2Y + 0.02F * h, accent)

            ' Baffles (two short vertical lines near walls)
            canvas.DrawLine(bodyLeft + 0.05F * bodyW, liquidTop + 0.1F * (shaftBot - liquidTop),
                            bodyLeft + 0.05F * bodyW, shaftBot, accent)
            canvas.DrawLine(bodyRight - 0.05F * bodyW, liquidTop + 0.1F * (shaftBot - liquidTop),
                            bodyRight - 0.05F * bodyW, shaftBot, accent)

            ' Sparger ring near bottom
            Dim spargerY = shaftBot + 0.03F * h
            canvas.DrawLine(cx - 0.2F * w, spargerY, cx + 0.2F * w, spargerY, accent)
            canvas.DrawLine(cx - 0.2F * w, spargerY, cx - 0.2F * w, spargerY + 0.03F * h, accent)
            canvas.DrawLine(cx + 0.2F * w, spargerY, cx + 0.2F * w, spargerY + 0.03F * h, accent)

            ' Bubbles rising
            canvas.DrawCircle(cx - 0.14F * w, spargerY - 0.05F * h, 0.018F * w, bubble)
            canvas.DrawCircle(cx - 0.05F * w, spargerY - 0.1F * h, 0.022F * w, bubble)
            canvas.DrawCircle(cx + 0.08F * w, spargerY - 0.07F * h, 0.02F * w, bubble)
            canvas.DrawCircle(cx + 0.16F * w, spargerY - 0.12F * h, 0.018F * w, bubble)
            canvas.DrawCircle(cx, spargerY - 0.16F * h, 0.016F * w, bubble)

            ' Offgas nozzle on top
            canvas.DrawLine(cx + 0.15F * w, bodyTop - 0.05F * bodyH, cx + 0.15F * w, gy, stroke)

            broth.Dispose()
            body.Dispose()
            stroke.Dispose()
            accent.Dispose()
            motorPaint.Dispose()
            bubble.Dispose()

        End Sub

        Public Overrides Function LoadData(data As System.Collections.Generic.List(Of System.Xml.Linq.XElement)) As Boolean

            XMLSerializer.XMLSerializer.Deserialize(Me, data)

        End Function

        ''' <summary>Serializes the reactor state, including reaction extents and component IDs, to XML.</summary>
        Public Overrides Function SaveData() As System.Collections.Generic.List(Of System.Xml.Linq.XElement)

            Return XMLSerializer.XMLSerializer.Serialize(Me)

        End Function

        ''' <summary>Writes a mass-fraction-normalised outlet stream from a {compound name -> mass flow}
        ''' dictionary.  Total mass flow is set to <paramref name="total"/>; mole fractions are derived
        ''' from mass fractions and the compound molecular weights.</summary>
        Private Shared Sub WriteSplitStream(ms As MaterialStream, m As Dictionary(Of String, Double),
                                             total As Double, T As Double, P As Double)
            With ms
                .ClearAllProps()
                .Phases(0).Properties.temperature = T
                .Phases(0).Properties.pressure = P
                If total > 0 Then
                    For Each c In .Phases(0).Compounds.Values
                        c.MassFraction = If(m.ContainsKey(c.Name), m(c.Name), 0.0) / total
                    Next
                    Dim invMW As Double = 0.0
                    For Each c In .Phases(0).Compounds.Values
                        invMW += c.MassFraction.GetValueOrDefault / c.ConstantProperties.Molar_Weight
                    Next
                    If invMW > 0 Then
                        For Each c In .Phases(0).Compounds.Values
                            c.MoleFraction = (c.MassFraction.GetValueOrDefault / c.ConstantProperties.Molar_Weight) / invMW
                        Next
                    End If
                End If
                .Phases(0).Properties.massflow = total
                .DefinedFlow = FlowSpec.Mass
                .SpecType = StreamSpec.Temperature_and_Pressure
            End With
        End Sub

    End Class

End Namespace
