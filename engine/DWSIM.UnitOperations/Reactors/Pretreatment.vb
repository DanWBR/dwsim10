'    Biomass Pretreatment Reactor - Calculation Routines
'    Copyright 2026 Daniel Wagner O. de Medeiros
'
'    This file is part of DWSIM.

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
Imports DWSIM.UnitOperations.Streams
Imports System.Collections.Generic
Imports DWSIM.UI.Shared.Avalonia

Namespace Reactors

    ''' <summary>Pretreatment technology selector.</summary>
    Public Enum PretreatmentType
        DiluteAcid = 0
        SteamExplosion = 1
        Alkaline = 2
        Organosolv = 3
    End Enum

    ''' <summary>Selects where the pretreatment reactor takes its conversions from.</summary>
    Public Enum PretreatmentConversionMode
        ''' <summary>The conversion fractions entered by the user (or loaded by <see cref="Reactor_Pretreatment.ApplyTechnologyDefaults"/>) are applied as they are.</summary>
        UserFractions = 0
        ''' <summary>The cellulose and hemicellulose conversions and the sugar degradation follow from the severity factor
        ''' through first-order kinetics in R0, with rate constants calibrated per technology.</summary>
        Severity = 1
    End Enum

    ''' <summary>
    ''' Biomass pretreatment reactor. Converts a lignocellulosic slurry (cellulose + hemicellulose + lignin)
    ''' into a pretreated slurry containing sugars (glucose, xylose) and inhibitors (furfural, HMF,
    ''' acetic acid). With <see cref="ConversionMode"/> = UserFractions (default) it applies the user-specified
    ''' conversion fractions, whose defaults are keyed to the selected PretreatmentType. With
    ''' ConversionMode = Severity the conversions follow from the severity factor
    ''' R0 = t·exp((T - 100)/14.75), t in min and T in °C (Overend and Chornet, 1987), or, for dilute acid,
    ''' from the combined severity CSF = log R0 - pH (Chum et al., 1990): the polysaccharides hydrolyse
    ''' first order in R0 (Abatzoglou et al., 1992) and the sugars degrade by the consecutive model of
    ''' Saeman (1945), with rate constants calibrated so that log R0 = 3.5 reproduces the technology defaults.
    ''' Reactions:
    '''   (C6H10O5)n + H2O -> C6H12O6                  (cellulose to glucose)
    '''   C6H12O6       -> C6H6O3 + 3 H2O             (glucose to HMF)
    '''   (C5H8O4)n  + H2O -> C5H10O5                  (xylan to xylose)
    '''   C5H10O5       -> C5H4O2 + 3 H2O             (xylose to furfural)
    ''' Acetic acid is released from acetyl groups in the hemicellulose, proportionally to the
    ''' hemicellulose mass consumed.
    ''' </summary>
    <System.Serializable()> Public Partial Class Reactor_Pretreatment

        Inherits Reactor

        Implements IExternalUnitOperation
        ''' <summary>Gets a value indicating that this reactor belongs to the Bio group of the object palette. Always <c>True</c>.</summary>
        Public ReadOnly Property IsBio As Boolean = True

        Public Overrides Property ObjectClass As SimulationObjectClass
            Get
                Return SimulationObjectClass.Reactors
            End Get
            Set(value As SimulationObjectClass)
                MyBase.ObjectClass = value
            End Set
        End Property

        ''' <summary>Gets or sets the display name for this unit operation.</summary>
        Public Overrides Property ComponentName As String = GetDisplayName()

        ''' <summary>Gets or sets the display description for this unit operation.</summary>
        Public Overrides Property ComponentDescription As String = GetDisplayDescription()

        ' -------- CONFIG --------

        ''' <summary>Gets or sets the pretreatment technology: DiluteAcid (0), SteamExplosion (1), Alkaline (2) or Organosolv (3).
        ''' It selects the conversion fractions set by <see cref="ApplyTechnologyDefaults"/>; in Severity mode it also selects
        ''' the rate constants, calibrated on those defaults, and DiluteAcid switches the severity to the combined severity
        ''' log R0 - pH. Default DiluteAcid.</summary>
        Public Property Technology As PretreatmentType = PretreatmentType.DiluteAcid
        ''' <summary>Gets or sets where the conversions come from: UserFractions (0, default) applies the conversion
        ''' fractions as entered; Severity (1) computes the cellulose and hemicellulose conversions and the sugar
        ''' degradation from the severity factor. Lignin solubilization and the acetic acid yield are read from their
        ''' properties in both modes.</summary>
        Public Property ConversionMode As PretreatmentConversionMode = PretreatmentConversionMode.UserFractions
        ''' <summary>Gets or sets the severity factor log10(R0) (dimensionless), with R0 in min. Default 3.5.
        ''' Read only when <see cref="ResidenceTime_s"/> is zero; with a positive residence time the reactor computes
        ''' log R0 from the residence time and the temperature, reported in <see cref="Result_LogR0"/>.</summary>
        Public Property SeverityLogR0 As Double = 3.5
        ''' <summary>Gets or sets the reactor residence time, in s. Default 600. When positive, the reactor computes the
        ''' severity log R0 from it and from the reaction temperature; set it to zero to use <see cref="SeverityLogR0"/> instead.</summary>
        Public Property ResidenceTime_s As Double = 600.0
        ''' <summary>Gets or sets the expected solids loading of the slurry, as a mass fraction (0-1). Default 0.20.
        ''' The reactor computes the solids loading from the feed (<see cref="Result_SolidsLoading_wfrac"/>) and does not
        ''' add or remove water; in Severity mode it warns when the feed differs from this value by more than 0.005.</summary>
        Public Property SolidsLoading_wfrac As Double = 0.20
        ''' <summary>Gets or sets the second dissociation constant pKa2 of the acid in <see cref="AcidCompound"/>, at 25 °C.
        ''' The first proton is taken as fully dissociated. Default 1.99 (sulfuric acid); use a large value (e.g. 99)
        ''' for a monoprotic strong acid such as HCl.</summary>
        Public Property AcidSecondPKa As Double = 1.99

        ' -------- COMPOUND ROLES --------

        ''' <summary>Gets or sets the name of the compound that represents cellulose in the feed. The cellulose to glucose reaction consumes it.</summary>
        Public Property CelluloseCompound As String = ""
        ''' <summary>Gets or sets the name of the compound that represents hemicellulose (xylan with acetyl groups) in the feed. The hemicellulose to xylose and acetic acid reactions consume it.</summary>
        Public Property HemicelluloseCompound As String = ""
        ''' <summary>Gets or sets the name of the compound that represents insoluble lignin in the feed. The solubilized fraction moves to <see cref="SolubleLigninCompound"/>.</summary>
        Public Property LigninCompound As String = ""
        ''' <summary>Gets or sets the name of the glucose product compound. Cellulose hydrolysis runs only when this compound is assigned and present.</summary>
        Public Property GlucoseCompound As String = ""
        ''' <summary>Gets or sets the name of the xylose product compound. Hemicellulose hydrolysis runs only when this compound is assigned and present.</summary>
        Public Property XyloseCompound As String = ""
        ''' <summary>Gets or sets the name of the furfural compound formed by xylose degradation. The degradation runs only when this compound is assigned and present.</summary>
        Public Property FurfuralCompound As String = ""
        ''' <summary>Gets or sets the name of the 5-hydroxymethylfurfural (HMF) compound formed by glucose degradation. The degradation runs only when this compound is assigned and present.</summary>
        Public Property HMFCompound As String = ""
        ''' <summary>Gets or sets the name of the acetic acid compound released from the hemicellulose acetyl groups. The release runs only when this compound is assigned and present.</summary>
        Public Property AceticAcidCompound As String = ""
        ''' <summary>Gets or sets the name of the water compound, consumed by the hydrolysis reactions and released by the degradation reactions. Default "Water".</summary>
        Public Property WaterCompound As String = "Water"
        ''' <summary>Gets or sets the name of the soluble lignin compound that receives the solubilized lignin. Lignin solubilization runs only when this compound is assigned and present.</summary>
        Public Property SolubleLigninCompound As String = ""
        ''' <summary>Gets or sets the name of the mineral acid compound in the feed (e.g. sulfuric acid). Its concentration in
        ''' the liquid gives the pH of the combined severity. Optional; without it the dilute acid technology assumes pH 1.0.
        ''' The acid passes through unchanged.</summary>
        Public Property AcidCompound As String = ""

        ' -------- CONVERSION FRACTIONS --------

        ''' <summary>Fraction of cellulose converted to glucose (0-1). Typical dilute acid: 0.05-0.15 (main conversion is in EH downstream).
        ''' Read in UserFractions mode; in Severity mode the applied value is <see cref="Result_CelluloseConversion"/>.</summary>
        Public Property CelluloseConversion As Double = 0.10

        ''' <summary>Mass of HMF formed per mass of glucose generated (0-0.70); the glucose consumed is this value / 0.70,
        ''' the HMF to glucose mass ratio. Typical dilute acid: 0.02-0.05.
        ''' Read in UserFractions mode; in Severity mode the applied value is <see cref="Result_GlucoseToHMF"/>.</summary>
        Public Property GlucoseToHMF As Double = 0.03

        ''' <summary>Fraction of hemicellulose converted to xylose (0-1). Typical dilute acid: 0.80-0.95.
        ''' Read in UserFractions mode; in Severity mode the applied value is <see cref="Result_HemicelluloseConversion"/>.</summary>
        Public Property HemicelluloseConversion As Double = 0.90

        ''' <summary>Mass of furfural formed per mass of xylose generated (0-0.64); the xylose consumed is this value / 0.64,
        ''' the furfural to xylose mass ratio. Typical dilute acid: 0.05-0.10.
        ''' Read in UserFractions mode; in Severity mode the applied value is <see cref="Result_XyloseToFurfural"/>.</summary>
        Public Property XyloseToFurfural As Double = 0.07

        ''' <summary>Fraction of lignin solubilized (0-1). Strong for alkaline/organosolv; low for dilute acid.</summary>
        Public Property LigninSolubilization As Double = 0.15

        ''' <summary>g acetic acid released per g hemicellulose consumed (mass fraction). Default 0.12.</summary>
        Public Property AceticAcidYieldOnHemi As Double = 0.12

        ' -------- RESULTS --------

        ''' <summary>Gets or sets the net glucose mass flow produced (after the part degraded to HMF), in kg/s. Calculated result.</summary>
        Public Property Result_GlucoseProduced_kgs As Double = 0.0
        ''' <summary>Gets or sets the net xylose mass flow produced (after the part degraded to furfural), in kg/s. Calculated result.</summary>
        Public Property Result_XyloseProduced_kgs As Double = 0.0
        ''' <summary>Gets or sets the furfural mass flow produced, in kg/s. Calculated result.</summary>
        Public Property Result_FurfuralProduced_kgs As Double = 0.0
        ''' <summary>Gets or sets the HMF mass flow produced, in kg/s. Calculated result.</summary>
        Public Property Result_HMFProduced_kgs As Double = 0.0
        ''' <summary>Gets or sets the acetic acid mass flow released from the hemicellulose, in kg/s. Calculated result.</summary>
        Public Property Result_AceticAcidProduced_kgs As Double = 0.0
        ''' <summary>Gets or sets the lignin mass flow moved to the soluble lignin compound, in kg/s. Calculated result.</summary>
        Public Property Result_LigninSolubilized_kgs As Double = 0.0
        ''' <summary>Gets or sets the cellulose mass flow consumed by hydrolysis, in kg/s. Calculated result.</summary>
        Public Property Result_CelluloseConsumed_kgs As Double = 0.0
        ''' <summary>Gets or sets the hemicellulose mass flow consumed, in kg/s. Calculated result.</summary>
        Public Property Result_HemicelluloseConsumed_kgs As Double = 0.0

        ''' <summary>Gets or sets the severity factor log10(R0) used in the calculation, with R0 in min: computed from the residence
        ''' time and the reaction temperature when the residence time is positive, else equal to <see cref="SeverityLogR0"/>. Calculated result.</summary>
        Public Property Result_LogR0 As Double = 0.0
        ''' <summary>Gets or sets the temperature the severity is evaluated at, in K: the outlet temperature when the reactor runs in
        ''' OutletTemperature mode, else the feed temperature. Calculated result.</summary>
        Public Property Result_SeverityTemperature_K As Double = 0.0
        ''' <summary>Gets or sets the solids loading of the feed, in w/w: the mass of the cellulose, hemicellulose and lignin
        ''' compounds over the total mass. Calculated result.</summary>
        Public Property Result_SolidsLoading_wfrac As Double = 0.0
        ''' <summary>Gets or sets the acid concentration in the liquid (feed mass minus the solids), in mol per kg of liquid.
        ''' Zero when no acid compound is assigned. Calculated result.</summary>
        Public Property Result_AcidConcentration_molkg As Double = 0.0
        ''' <summary>Gets or sets the pH of the liquid at 25 °C used by the combined severity: from the acid concentration when an acid
        ''' compound is assigned; 1.0 (the calibration pH) for dilute acid without one; 7 otherwise. Calculated result.</summary>
        Public Property Result_pH As Double = 7.0
        ''' <summary>Gets or sets the combined severity factor CSF = log R0 - pH (Chum et al., 1990). Severity mode uses it in place of
        ''' log R0 for the DiluteAcid technology only. Calculated result.</summary>
        Public Property Result_CombinedSeverity As Double = 0.0
        ''' <summary>Gets or sets the cellulose to glucose conversion applied in the last calculation (0-1). Calculated result.</summary>
        Public Property Result_CelluloseConversion As Double = 0.0
        ''' <summary>Gets or sets the HMF formed per mass of glucose generated applied in the last calculation (g/g). Calculated result.</summary>
        Public Property Result_GlucoseToHMF As Double = 0.0
        ''' <summary>Gets or sets the hemicellulose conversion applied in the last calculation (0-1). Calculated result.</summary>
        Public Property Result_HemicelluloseConversion As Double = 0.0
        ''' <summary>Gets or sets the furfural formed per mass of xylose generated applied in the last calculation (g/g). Calculated result.</summary>
        Public Property Result_XyloseToFurfural As Double = 0.0

        ' -------- SEVERITY MODEL CONSTANTS --------

        ''' <summary>Severity log R0 at which the rate constants reproduce the technology default fractions.</summary>
        Private Const CalibrationLogR0 As Double = 3.5
        ''' <summary>pH at which the dilute acid rate constants are calibrated (about 1 wt% sulfuric acid in the liquid), so that
        ''' the dilute acid defaults are reproduced at log R0 = 3.5 and pH 1.0, i.e. CSF = 2.5. Also the pH assumed when no acid compound is assigned.</summary>
        Private Const CalibrationPH As Double = 1.0
        ''' <summary>Mass of HMF per mass of glucose (126.11 / 180.16), as used by the mass balance.</summary>
        Private Const HMFPerGlucose As Double = 0.7
        ''' <summary>Mass of furfural per mass of xylose (96.08 / 150.13), as used by the mass balance.</summary>
        Private Const FurfuralPerXylose As Double = 0.64

        ' Rate constants of the last Severity calculation, as k·R_ref, and R_ref itself (for the report only)
        Private _kCell1, _kCell2, _kHemi1, _kHemi2 As Double
        Private _kBasis As Double = 0.0

        ''' <summary>The classic (WinForms) editor window open for this reactor, if any. Not saved with the flowsheet.</summary>
        <NonSerialized> <Xml.Serialization.XmlIgnore> Public f As Object

        ''' <summary>Gets a value indicating whether this reactor supports dynamic simulation mode. Always <c>False</c>; it is calculated as a steady-state model.</summary>
        Public Overrides ReadOnly Property SupportsDynamicMode As Boolean = False

        ''' <summary>Gets a value indicating whether this reactor is compatible with mobile interfaces. Always <c>False</c>.</summary>
        Public Overrides ReadOnly Property MobileCompatible As Boolean
            Get
                Return False
            End Get
        End Property

        ''' <summary>Initializes a new default instance of the <see cref="Reactor_Pretreatment"/> class.</summary>
        Public Sub New()
            MyBase.New()
        End Sub

        ''' <summary>Initializes a new instance of the <see cref="Reactor_Pretreatment"/> class with a name and description.</summary>
        ''' <param name="name">The name of this reactor.</param>
        ''' <param name="description">A brief description of this reactor.</param>
        Public Sub New(ByVal name As String, ByVal description As String)
            MyBase.New()
            Me.ComponentName = name
            Me.ComponentDescription = description
        End Sub

        ''' <summary>Creates a deep copy of this object by round-tripping through XML serialization.</summary>
        ''' <returns>A new <see cref="Reactor_Pretreatment"/> instance with the same property values.</returns>
        Public Overrides Function CloneXML() As Object
            Dim obj As ICustomXMLSerialization = New Reactor_Pretreatment()
            obj.LoadData(Me.SaveData)
            Return obj
        End Function

        ''' <summary>Creates a deep copy of this object by round-tripping through JSON serialization.</summary>
        ''' <returns>A new <see cref="Reactor_Pretreatment"/> instance with the same property values.</returns>
        Public Overrides Function CloneJSON() As Object
            Return Newtonsoft.Json.JsonConvert.DeserializeObject(Of Reactor_Pretreatment)(Newtonsoft.Json.JsonConvert.SerializeObject(Me))
        End Function

        ''' <summary>Preset default conversions for a given pretreatment technology.</summary>
        Public Sub ApplyTechnologyDefaults()
            Dim d = TechnologyDefaults(Technology)
            CelluloseConversion = d(0) : GlucoseToHMF = d(1)
            HemicelluloseConversion = d(2) : XyloseToFurfural = d(3)
            LigninSolubilization = d(4) : AceticAcidYieldOnHemi = d(5)
        End Sub

        ''' <summary>Returns the default fractions of a technology: cellulose conversion, glucose to HMF, hemicellulose conversion,
        ''' xylose to furfural, lignin solubilization and acetic acid yield on hemicellulose, in this order. The Severity mode
        ''' calibrates its rate constants on the first four at log R0 = 3.5.</summary>
        ''' <param name="tech">The pretreatment technology.</param>
        Public Shared Function TechnologyDefaults(tech As PretreatmentType) As Double()
            Select Case tech
                Case PretreatmentType.SteamExplosion
                    Return {0.05, 0.02, 0.8, 0.05, 0.05, 0.1}
                Case PretreatmentType.Alkaline
                    Return {0.02, 0.0, 0.55, 0.0, 0.7, 0.15}
                Case PretreatmentType.Organosolv
                    Return {0.05, 0.01, 0.75, 0.03, 0.85, 0.05}
                Case Else ' DiluteAcid
                    Return {0.08, 0.03, 0.9, 0.07, 0.1, 0.12}
            End Select
        End Function

        ''' <summary>Severity factor log10(R0) of Overend and Chornet (1987), R0 = t·exp((T - 100)/14.75), t in min and T in °C.</summary>
        ''' <param name="time_s">Residence time, in s.</param>
        ''' <param name="T_K">Reaction temperature, in K.</param>
        Public Shared Function SeverityLog10R0(time_s As Double, T_K As Double) As Double
            Return Log10(time_s / 60.0) + (T_K - 373.15) / 14.75 / Log(10.0)
        End Function

        ''' <summary>pH at 25 °C of a strong acid whose first proton dissociates fully and whose second one follows pKa2.</summary>
        ''' <param name="c_molkg">Acid concentration, in mol per kg of water (taken as mol/L).</param>
        ''' <param name="pKa2">Second dissociation constant.</param>
        Public Shared Function AcidPH(c_molkg As Double, pKa2 As Double) As Double
            If c_molkg <= 0.0 Then Return 7.0
            Dim Ka2 = Pow(10.0, -pKa2)
            Dim b = c_molkg + Ka2
            ' x² + (c + Ka2)·x - Ka2·c = 0, written to avoid cancellation
            Dim x = 2.0 * Ka2 * c_molkg / (b + Sqrt(b * b + 4.0 * Ka2 * c_molkg))
            Return -Log10(Max(c_molkg + x, 0.0000001))
        End Function

        ''' <summary>Consecutive first-order model of Saeman (1945), polymer → sugar → furan, in reduced form: <paramref name="a"/> = k1·R
        ''' and <paramref name="b"/> = k2·R. Returns the polymer converted and the fraction of the sugar formed that degraded.</summary>
        Private Shared Sub Saeman(a As Double, b As Double, ByRef converted As Double, ByRef degraded As Double)
            converted = 1.0 - Exp(-a)
            If converted <= 0.0 Then converted = 0.0 : degraded = 0.0 : Return
            Dim y As Double
            If Abs(b - a) <= 0.000000001 * a Then
                y = a * Exp(-a)
            Else
                y = a / (b - a) * (Exp(-a) - Exp(-b))
            End If
            degraded = Max(0.0, Min(1.0, (converted - y) / converted))
        End Sub

        ''' <summary>Finds b = k2·R_ref such that the Saeman model degrades the fraction <paramref name="target"/> of the sugar formed
        ''' when a = k1·R_ref. Bisection in log b; the degraded fraction rises monotonically with b.</summary>
        Private Shared Function CalibrateDegradation(a As Double, target As Double) As Double
            If target <= 0.0 OrElse a <= 0.0 Then Return 0.0
            target = Min(target, 0.999999)
            Dim lo = Log(a * 0.0000000001), hi = Log(a * 100000000.0)
            Dim x, d As Double
            For i = 1 To 200
                Dim mid = 0.5 * (lo + hi)
                Saeman(a, Exp(mid), x, d)
                If d < target Then lo = mid Else hi = mid
            Next
            Return Exp(0.5 * (lo + hi))
        End Function

        ''' <summary>Applies the severity model: hydrolysis X = 1 - exp(-k1·R) and degradation of the sugar formed by the Saeman
        ''' model, with k1 and k2 calibrated so that the technology defaults are reproduced at the reference severity.</summary>
        ''' <param name="severityRatio">R / R_ref, the severity relative to the calibration point (1 reproduces the defaults).</param>
        ''' <param name="defaultConversion">Default polymer conversion (0-1).</param>
        ''' <param name="defaultFuranYield">Default furan mass formed per mass of sugar generated.</param>
        ''' <param name="furanPerSugar">Furan to sugar mass ratio of the mass balance.</param>
        ''' <param name="conversion">Returns the polymer conversion.</param>
        ''' <param name="furanYield">Returns the furan mass formed per mass of sugar generated.</param>
        ''' <param name="k1">Returns k1·R_ref (dimensionless).</param>
        ''' <param name="k2">Returns k2·R_ref (dimensionless).</param>
        Private Shared Sub SeverityConversion(severityRatio As Double,
                                              defaultConversion As Double, defaultFuranYield As Double, furanPerSugar As Double,
                                              ByRef conversion As Double, ByRef furanYield As Double,
                                              ByRef k1 As Double, ByRef k2 As Double)
            k1 = -Log(1.0 - Max(0.0, Min(0.999999999999, defaultConversion)))
            k2 = CalibrateDegradation(k1, defaultFuranYield / furanPerSugar)
            Dim degraded As Double
            Saeman(k1 * severityRatio, k2 * severityRatio, conversion, degraded)
            furanYield = furanPerSugar * degraded
        End Sub

        Public Overrides Sub Calculate(Optional ByVal args As Object = Nothing)

            If Not Me.GraphicObject.InputConnectors(0).IsAttached Then _
                Throw New Exception("Pretreatment: Biomass slurry inlet not connected.")
            If Not Me.GraphicObject.OutputConnectors(0).IsAttached Then _
                Throw New Exception("Pretreatment: Pretreated slurry outlet not connected.")

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

            Dim compounds = ims.Phases(0).Compounds
            Dim newMass As New Dictionary(Of String, Double)
            For Each kvp In compounds
                newMass(kvp.Key) = kvp.Value.MassFlow.GetValueOrDefault
            Next

            Dim m_cell_in As Double = 0.0, m_hemi_in As Double = 0.0, m_lignin_in As Double = 0.0
            If Not String.IsNullOrEmpty(CelluloseCompound) AndAlso compounds.ContainsKey(CelluloseCompound) Then _
                m_cell_in = compounds(CelluloseCompound).MassFlow.GetValueOrDefault
            If Not String.IsNullOrEmpty(HemicelluloseCompound) AndAlso compounds.ContainsKey(HemicelluloseCompound) Then _
                m_hemi_in = compounds(HemicelluloseCompound).MassFlow.GetValueOrDefault
            If Not String.IsNullOrEmpty(LigninCompound) AndAlso compounds.ContainsKey(LigninCompound) Then _
                m_lignin_in = compounds(LigninCompound).MassFlow.GetValueOrDefault

            ' Cellulose to glucose (with subsequent glucose to HMF)
            ' A reaction runs only when its product has a compound to go to; otherwise the reactant
            ' stays as it is, so no mass leaves the balance.
            Dim assigned = Function(name As String) Not String.IsNullOrEmpty(name) AndAlso newMass.ContainsKey(name)

            ' Severity indicators, computed in both modes. The severity is evaluated at the outlet temperature when the
            ' thermal mode fixes it, else at the feed temperature. A positive residence time gives log R0; a zero one
            ' leaves the user's SeverityLogR0 in charge.
            Dim Tsev As Double = T
            If ReactorOperationMode = OperationMode.OutletTemperature AndAlso OutletTemperature > 0 Then Tsev = OutletTemperature
            Dim logR0 As Double = If(ResidenceTime_s > 0.0, SeverityLog10R0(ResidenceTime_s, Tsev), SeverityLogR0)

            ' Solids loading from the feed: the polysaccharides and the lignin are the insoluble solids; the rest is liquid.
            Dim m_total_in As Double = newMass.Values.Sum()
            Dim m_solids_in As Double = m_cell_in + m_hemi_in + m_lignin_in
            Dim m_liquid_in As Double = Max(m_total_in - m_solids_in, 0.0)
            Dim solidsLoading As Double = If(m_total_in > 0.0, m_solids_in / m_total_in, 0.0)

            ' Acid concentration in the liquid and its pH at 25 °C (mol/kg taken as mol/L in a dilute aqueous liquid).
            Dim c_acid As Double = 0.0
            Dim acidAssigned As Boolean = assigned(AcidCompound)
            If acidAssigned AndAlso m_liquid_in > 0.0 Then
                Dim mw_acid = compounds(AcidCompound).ConstantProperties.Molar_Weight
                If mw_acid > 0.0 Then c_acid = newMass(AcidCompound) * 1000.0 / mw_acid / m_liquid_in
            End If
            Dim isAcidTechnology As Boolean = (Technology = PretreatmentType.DiluteAcid)
            Dim pH As Double
            If acidAssigned Then
                pH = AcidPH(c_acid, AcidSecondPKa)
            ElseIf isAcidTechnology Then
                pH = CalibrationPH
            Else
                pH = 7.0
            End If
            Dim csf As Double = logR0 - pH

            Result_LogR0 = logR0
            Result_SeverityTemperature_K = Tsev
            Result_SolidsLoading_wfrac = solidsLoading
            Result_AcidConcentration_molkg = c_acid
            Result_pH = pH
            Result_CombinedSeverity = csf

            ' Conversions applied by the mass balance
            Dim xCell, fHMF, xHemi, fFur As Double
            If ConversionMode = PretreatmentConversionMode.Severity Then
                ' Dilute acid works on the combined severity, calibrated at CSF = 3.5 - 1.0; the others on log R0, calibrated at 3.5.
                Dim sevIndex = If(isAcidTechnology, csf, logR0)
                Dim sevRef = If(isAcidTechnology, CalibrationLogR0 - CalibrationPH, CalibrationLogR0)
                Dim ratio = Pow(10.0, sevIndex - sevRef)
                Dim def = TechnologyDefaults(Technology)
                SeverityConversion(ratio, def(0), def(1), HMFPerGlucose, xCell, fHMF, _kCell1, _kCell2)
                SeverityConversion(ratio, def(2), def(3), FurfuralPerXylose, xHemi, fFur, _kHemi1, _kHemi2)
                _kBasis = Pow(10.0, sevRef)

                If SolidsLoading_wfrac > 0.0 AndAlso Abs(solidsLoading - SolidsLoading_wfrac) > 0.005 Then
                    FlowSheet?.ShowMessage(String.Format(
                        "{0}: the feed carries {1:0.000} w/w of solids and the Solids Loading input is {2:0.000} w/w. " &
                        "The reactor uses the feed as it is; change the water in the feed to reach the target loading.",
                        Me.GraphicObject.Tag, solidsLoading, SolidsLoading_wfrac), IFlowsheet.MessageType.Warning)
                End If
                If isAcidTechnology AndAlso Not acidAssigned Then
                    FlowSheet?.ShowMessage(String.Format(
                        "{0}: no acid compound is assigned, so the combined severity takes pH {1:0.0}. Assign the acid in the feed " &
                        "to have the pH computed from its concentration.", Me.GraphicObject.Tag, CalibrationPH),
                        IFlowsheet.MessageType.Warning)
                End If
            Else
                xCell = Max(0.0, Min(1.0, CelluloseConversion))
                fHMF = Max(0.0, Min(1.0, GlucoseToHMF))
                xHemi = Max(0.0, Min(1.0, HemicelluloseConversion))
                fFur = Max(0.0, Min(1.0, XyloseToFurfural))
            End If
            Result_CelluloseConversion = xCell
            Result_GlucoseToHMF = fHMF
            Result_HemicelluloseConversion = xHemi
            Result_XyloseToFurfural = fFur

            ' Cellulose to glucose (with subsequent glucose to HMF)
            Dim dm_cell = If(assigned(GlucoseCompound), m_cell_in * xCell, 0.0)
            ' 1 g cellulose (162.14) + H2O (18.02) to 1.111 g glucose (180.16)
            Dim dm_glu_gross = dm_cell * 1.111
            Dim dm_h2o_cell = dm_cell * 0.111 ' water consumed by cellulose hydrolysis
            Dim dm_hmf = If(assigned(HMFCompound), dm_glu_gross * fHMF, 0.0)
            ' glucose (180.16) to HMF (126.11) + 3 H2O (54.05); 1 g glu to 0.70 g HMF + 0.30 g H2O
            Dim dm_glu_net = dm_glu_gross - dm_hmf / 0.70
            Dim dm_h2o_hmf_release = dm_hmf * 0.30 / 0.70 ' water released by glucose to HMF

            ' Hemicellulose to xylose (with subsequent xylose to furfural); its acetyl groups give acetic acid:
            ' R-O-COCH3 + H2O to R-OH + CH3COOH, so 60.05 g of acid take 42.04 g from the chain and 18.02 g of water.
            Dim dm_hemi = If(assigned(XyloseCompound), m_hemi_in * xHemi, 0.0)
            Dim dm_acetic = If(assigned(AceticAcidCompound), dm_hemi * Max(0.0, AceticAcidYieldOnHemi), 0.0)
            Dim dm_acetyl = dm_acetic * 42.04 / 60.05
            Dim dm_h2o_acetic = dm_acetic * 18.02 / 60.05
            Dim dm_xylan = Max(dm_hemi - dm_acetyl, 0.0)
            ' 1 g xylan (132.12) + H2O (18.02) to 1.136 g xylose (150.13)
            Dim dm_xyl_gross = dm_xylan * 1.1364
            Dim dm_h2o_hemi = dm_xylan * 0.1364
            Dim dm_fur = If(assigned(FurfuralCompound), dm_xyl_gross * fFur, 0.0)
            ' xylose (150.13) to furfural (96.08) + 3 H2O (54.05); 1 g xyl to 0.64 g fur + 0.36 g H2O
            Dim dm_xyl_net = dm_xyl_gross - dm_fur / 0.64
            Dim dm_h2o_fur_release = dm_fur * 0.36 / 0.64

            ' Lignin solubilization: without a soluble-lignin compound the lignin stays where it is.
            Dim dm_lignin_sol = If(assigned(SolubleLigninCompound), m_lignin_in * Max(0.0, Min(1.0, LigninSolubilization)), 0.0)

            ' Apply mass balances
            If Not String.IsNullOrEmpty(CelluloseCompound) AndAlso newMass.ContainsKey(CelluloseCompound) Then _
                newMass(CelluloseCompound) = Max(newMass(CelluloseCompound) - dm_cell, 0.0)
            If Not String.IsNullOrEmpty(HemicelluloseCompound) AndAlso newMass.ContainsKey(HemicelluloseCompound) Then _
                newMass(HemicelluloseCompound) = Max(newMass(HemicelluloseCompound) - dm_hemi, 0.0)
            If Not String.IsNullOrEmpty(LigninCompound) AndAlso newMass.ContainsKey(LigninCompound) Then _
                newMass(LigninCompound) = Max(newMass(LigninCompound) - dm_lignin_sol, 0.0)
            If Not String.IsNullOrEmpty(SolubleLigninCompound) AndAlso newMass.ContainsKey(SolubleLigninCompound) Then _
                newMass(SolubleLigninCompound) += dm_lignin_sol

            If Not String.IsNullOrEmpty(GlucoseCompound) AndAlso newMass.ContainsKey(GlucoseCompound) Then _
                newMass(GlucoseCompound) += dm_glu_net
            If Not String.IsNullOrEmpty(XyloseCompound) AndAlso newMass.ContainsKey(XyloseCompound) Then _
                newMass(XyloseCompound) += dm_xyl_net
            If Not String.IsNullOrEmpty(HMFCompound) AndAlso newMass.ContainsKey(HMFCompound) Then _
                newMass(HMFCompound) += dm_hmf
            If Not String.IsNullOrEmpty(FurfuralCompound) AndAlso newMass.ContainsKey(FurfuralCompound) Then _
                newMass(FurfuralCompound) += dm_fur
            If Not String.IsNullOrEmpty(AceticAcidCompound) AndAlso newMass.ContainsKey(AceticAcidCompound) Then _
                newMass(AceticAcidCompound) += dm_acetic

            Dim dm_h2o_net = -(dm_h2o_cell + dm_h2o_hemi + dm_h2o_acetic) + dm_h2o_hmf_release + dm_h2o_fur_release
            If Not String.IsNullOrEmpty(WaterCompound) AndAlso newMass.ContainsKey(WaterCompound) Then _
                newMass(WaterCompound) = Max(newMass(WaterCompound) + dm_h2o_net, 0.0)

            Result_CelluloseConsumed_kgs = dm_cell
            Result_HemicelluloseConsumed_kgs = dm_hemi
            Result_GlucoseProduced_kgs = dm_glu_net
            Result_XyloseProduced_kgs = dm_xyl_net
            Result_HMFProduced_kgs = dm_hmf
            Result_FurfuralProduced_kgs = dm_fur
            Result_AceticAcidProduced_kgs = dm_acetic
            Result_LigninSolubilized_kgs = dm_lignin_sol

            Dim totalNewMass As Double = 0.0
            For Each v In newMass.Values : totalNewMass += v : Next
            If totalNewMass <= 0 Then totalNewMass = ims.Phases(0).Properties.massflow.GetValueOrDefault

            For Each comp In compounds.Values
                comp.MassFraction = newMass(comp.Name) / totalNewMass
            Next
            Dim invMWsum As Double = 0.0
            For Each comp In compounds.Values
                invMWsum += comp.MassFraction.GetValueOrDefault / comp.ConstantProperties.Molar_Weight
            Next
            If invMWsum > 0 Then
                For Each comp In compounds.Values
                    comp.MoleFraction = (comp.MassFraction.GetValueOrDefault / comp.ConstantProperties.Molar_Weight) / invMWsum
                Next
            End If
            ims.Phases(0).Properties.massflow = totalNewMass
            ims.DefinedFlow = FlowSpec.Mass

            ' Outlet temperature: if user set OutletTemperature via ReactorOperationMode, honour; else keep T
            Select Case ReactorOperationMode
                Case OperationMode.OutletTemperature
                    If OutletTemperature > 0 Then ims.Phases(0).Properties.temperature = OutletTemperature
            End Select

            ims.SpecType = StreamSpec.Temperature_and_Pressure
            PropertyPackage.CurrentMaterialStream = ims
            ims.Calculate(True, True)

            ' Push to outlet
            Dim cp = Me.GraphicObject.OutputConnectors(0)
            If cp.IsAttached Then
                Dim ms_out As MaterialStream = FlowSheet.SimulationObjects(cp.AttachedConnector.AttachedTo.Name)
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

        End Sub

        Public Overrides Sub DeCalculate()
            Dim cp = Me.GraphicObject.OutputConnectors(0)
            If cp.IsAttached Then
                Dim ms As MaterialStream = FlowSheet.SimulationObjects(cp.AttachedConnector.AttachedTo.Name)
                With ms
                    .Phases(0).Properties.temperature = Nothing
                    .Phases(0).Properties.pressure = Nothing
                    .Phases(0).Properties.enthalpy = Nothing
                    For Each c In .Phases(0).Compounds.Values
                        c.MoleFraction = 0
                        c.MassFraction = 0
                    Next
                    .Phases(0).Properties.massflow = Nothing
                    .GraphicObject.Calculated = False
                End With
            End If
        End Sub

        ''' <summary>Returns the raw bytes of the icon image for this reactor.</summary>
        ''' <returns>A byte array containing the PNG image data for the icon.</returns>
        Public Overrides Function GetIconBitmapBytes() As Byte()
            Return UnitOperations.BioOpsDrawHelper.RenderIconToPngBytes(64, 64, AddressOf DrawIcon)
        End Function

        ''' <summary>Returns the localized description string for this reactor type.</summary>
        ''' <returns>A translated description string identifying this reactor type.</returns>
        Public Overrides Function GetDisplayDescription() As String
            Return "Biomass pretreatment reactor (dilute-acid / steam-explosion / alkaline / organosolv)"
        End Function

        ''' <summary>Returns the localized display name for this reactor type.</summary>
        ''' <returns>A translated name string for this reactor type.</returns>
        Public Overrides Function GetDisplayName() As String
            Return "Pretreatment Reactor"
        End Function

        ''' <summary>Generates a plain-text results report for this reactor.</summary>
        ''' <param name="su">The unit system used for formatting output values.</param>
        ''' <param name="ci">The culture info used for number formatting.</param>
        ''' <param name="numberformat">A .NET numeric format string (e.g. "G6") applied to output values.</param>
        ''' <returns>A formatted multi-line string report.</returns>
        Public Overrides Function GetReport(su As IUnitsOfMeasure, ci As Globalization.CultureInfo, numberformat As String) As String
            Dim s As New Text.StringBuilder
            s.AppendLine("Pretreatment: " & Me.GraphicObject.Tag)
            s.AppendLine("Technology:        " & Technology.ToString())
            s.AppendLine("Conversion mode:   " & ConversionMode.ToString())
            s.AppendLine()
            s.AppendLine("Severity:")
            s.AppendLine("  Temperature:             " & Result_SeverityTemperature_K.ToString(numberformat, ci) & " K")
            s.AppendLine("  log R0:                  " & Result_LogR0.ToString(numberformat, ci) &
                         If(ResidenceTime_s > 0.0, " (from residence time and temperature)", " (input; residence time is zero)"))
            s.AppendLine("  Feed solids loading:     " & Result_SolidsLoading_wfrac.ToString(numberformat, ci) & " w/w")
            s.AppendLine("  Acid in liquid:          " & Result_AcidConcentration_molkg.ToString(numberformat, ci) & " mol/kg")
            s.AppendLine("  pH (25 C):               " & Result_pH.ToString(numberformat, ci))
            s.AppendLine("  Combined severity (CSF): " & Result_CombinedSeverity.ToString(numberformat, ci))
            If ConversionMode = PretreatmentConversionMode.Severity AndAlso _kBasis > 0.0 Then
                Dim basis = If(Technology = PretreatmentType.DiluteAcid, "10^CSF", "R0")
                s.AppendLine("  k cellulose to glucose:  " & (_kCell1 / _kBasis).ToString(numberformat, ci) & " per unit " & basis)
                s.AppendLine("  k glucose to HMF:        " & (_kCell2 / _kBasis).ToString(numberformat, ci) & " per unit " & basis)
                s.AppendLine("  k hemicellulose to xyl.: " & (_kHemi1 / _kBasis).ToString(numberformat, ci) & " per unit " & basis)
                s.AppendLine("  k xylose to furfural:    " & (_kHemi2 / _kBasis).ToString(numberformat, ci) & " per unit " & basis)
            End If
            s.AppendLine()
            s.AppendLine("Applied conversions:")
            s.AppendLine("  Cellulose to glucose:    " & (Result_CelluloseConversion * 100).ToString(numberformat, ci) & " %")
            s.AppendLine("  HMF per glucose formed:  " & (Result_GlucoseToHMF * 100).ToString(numberformat, ci) & " %")
            s.AppendLine("  Hemicellulose to xylose: " & (Result_HemicelluloseConversion * 100).ToString(numberformat, ci) & " %")
            s.AppendLine("  Furfural per xylose:     " & (Result_XyloseToFurfural * 100).ToString(numberformat, ci) & " %")
            s.AppendLine("  Lignin solubilization:   " & (LigninSolubilization * 100).ToString(numberformat, ci) & " %")
            s.AppendLine()
            s.AppendLine("Results (kg/s):")
            s.AppendLine("  Cellulose consumed:     " & Result_CelluloseConsumed_kgs.ToString(numberformat, ci))
            s.AppendLine("  Hemicellulose consumed: " & Result_HemicelluloseConsumed_kgs.ToString(numberformat, ci))
            s.AppendLine("  Glucose produced:       " & Result_GlucoseProduced_kgs.ToString(numberformat, ci))
            s.AppendLine("  Xylose produced:        " & Result_XyloseProduced_kgs.ToString(numberformat, ci))
            s.AppendLine("  HMF produced:           " & Result_HMFProduced_kgs.ToString(numberformat, ci))
            s.AppendLine("  Furfural produced:      " & Result_FurfuralProduced_kgs.ToString(numberformat, ci))
            s.AppendLine("  Acetic acid produced:   " & Result_AceticAcidProduced_kgs.ToString(numberformat, ci))
            s.AppendLine("  Lignin solubilized:     " & Result_LigninSolubilized_kgs.ToString(numberformat, ci))
            Return s.ToString()
        End Function

        Private Shared ReadOnly _inputProps As String() = {
            "Technology", "Conversion Mode", "Severity Log R0", "Residence Time", "Solids Loading",
            "Cellulose Compound", "Hemicellulose Compound", "Lignin Compound",
            "Glucose Compound", "Xylose Compound", "Furfural Compound", "HMF Compound",
            "Acetic Acid Compound", "Water Compound", "Soluble Lignin Compound",
            "Acid Compound", "Acid Second pKa",
            "Cellulose Conversion", "Glucose to HMF", "Hemicellulose Conversion",
            "Xylose to Furfural", "Lignin Solubilization", "Acetic Acid Yield on Hemi"
        }

        ' The four fractions the Severity mode computes instead of reading
        Private Shared ReadOnly _userFractionProps As String() = {
            "Cellulose Conversion", "Glucose to HMF", "Hemicellulose Conversion", "Xylose to Furfural"
        }

        Private Shared ReadOnly _outputProps As String() = {
            "Cellulose Consumed", "Hemicellulose Consumed", "Glucose Produced",
            "Xylose Produced", "HMF Produced", "Furfural Produced",
            "Acetic Acid Produced", "Lignin Solubilized",
            "Computed Log R0", "Severity Temperature", "Feed Solids Loading", "Acid Concentration",
            "Liquid pH", "Combined Severity Factor",
            "Applied Cellulose Conversion", "Applied Glucose to HMF",
            "Applied Hemicellulose Conversion", "Applied Xylose to Furfural"
        }

        ''' <summary>Returns the property IDs of this reactor. The writable list (WR) follows <see cref="ConversionMode"/>:
        ''' the Severity mode leaves out the four conversion fractions it computes.</summary>
        ''' <param name="proptype">The kind of property to list.</param>
        Public Overrides Function GetProperties(proptype As PropertyType) As String()
            Dim baseprops = MyBase.GetProperties(proptype)
            Select Case proptype
                Case PropertyType.WR
                    If ConversionMode = PretreatmentConversionMode.Severity Then
                        Return _inputProps.Except(_userFractionProps).ToArray()
                    End If
                    Return _inputProps
                Case PropertyType.RO : Return _outputProps
                Case Else : Return _inputProps.Concat(_outputProps).Concat(baseprops).ToArray()
            End Select
        End Function

        Public Overrides Function GetPropertyValue(prop As String, Optional su As IUnitsOfMeasure = Nothing) As Object
            Select Case prop
                Case "Technology" : Return Technology.ToString()
                Case "Conversion Mode" : Return ConversionMode.ToString()
                Case "Severity Log R0" : Return SeverityLogR0
                Case "Residence Time" : Return ResidenceTime_s
                Case "Solids Loading" : Return SolidsLoading_wfrac
                Case "Cellulose Compound" : Return CelluloseCompound
                Case "Hemicellulose Compound" : Return HemicelluloseCompound
                Case "Lignin Compound" : Return LigninCompound
                Case "Glucose Compound" : Return GlucoseCompound
                Case "Xylose Compound" : Return XyloseCompound
                Case "Furfural Compound" : Return FurfuralCompound
                Case "HMF Compound" : Return HMFCompound
                Case "Acetic Acid Compound" : Return AceticAcidCompound
                Case "Water Compound" : Return WaterCompound
                Case "Soluble Lignin Compound" : Return SolubleLigninCompound
                Case "Acid Compound" : Return AcidCompound
                Case "Acid Second pKa" : Return AcidSecondPKa
                Case "Cellulose Conversion" : Return CelluloseConversion
                Case "Glucose to HMF" : Return GlucoseToHMF
                Case "Hemicellulose Conversion" : Return HemicelluloseConversion
                Case "Xylose to Furfural" : Return XyloseToFurfural
                Case "Lignin Solubilization" : Return LigninSolubilization
                Case "Acetic Acid Yield on Hemi" : Return AceticAcidYieldOnHemi
                Case "Cellulose Consumed" : Return Result_CelluloseConsumed_kgs
                Case "Hemicellulose Consumed" : Return Result_HemicelluloseConsumed_kgs
                Case "Glucose Produced" : Return Result_GlucoseProduced_kgs
                Case "Xylose Produced" : Return Result_XyloseProduced_kgs
                Case "HMF Produced" : Return Result_HMFProduced_kgs
                Case "Furfural Produced" : Return Result_FurfuralProduced_kgs
                Case "Acetic Acid Produced" : Return Result_AceticAcidProduced_kgs
                Case "Lignin Solubilized" : Return Result_LigninSolubilized_kgs
                Case "Computed Log R0" : Return Result_LogR0
                Case "Severity Temperature" : Return Result_SeverityTemperature_K
                Case "Feed Solids Loading" : Return Result_SolidsLoading_wfrac
                Case "Acid Concentration" : Return Result_AcidConcentration_molkg
                Case "Liquid pH" : Return Result_pH
                Case "Combined Severity Factor" : Return Result_CombinedSeverity
                Case "Applied Cellulose Conversion" : Return Result_CelluloseConversion
                Case "Applied Glucose to HMF" : Return Result_GlucoseToHMF
                Case "Applied Hemicellulose Conversion" : Return Result_HemicelluloseConversion
                Case "Applied Xylose to Furfural" : Return Result_XyloseToFurfural
                Case Else : Return MyBase.GetPropertyValue(prop, su)
            End Select
        End Function

        Public Overrides Function GetPropertyUnit(prop As String, Optional su As IUnitsOfMeasure = Nothing) As String
            Select Case prop
                Case "Residence Time" : Return "s"
                Case "Severity Temperature" : Return "K"
                Case "Acid Concentration" : Return "mol/kg"
                Case "Cellulose Consumed", "Hemicellulose Consumed", "Glucose Produced",
                     "Xylose Produced", "HMF Produced", "Furfural Produced",
                     "Acetic Acid Produced", "Lignin Solubilized" : Return "kg/s"
                Case Else : Return "-"
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
                Case "Technology"
                    Dim t As PretreatmentType
                    If [Enum].TryParse(Of PretreatmentType)(propval?.ToString(), t) Then Technology = t
                    Return True
                Case "Conversion Mode"
                    Dim m As PretreatmentConversionMode
                    If [Enum].TryParse(Of PretreatmentConversionMode)(propval?.ToString(), m) Then ConversionMode = m
                    Return True
                Case "Severity Log R0" : SeverityLogR0 = d : Return True
                Case "Residence Time" : ResidenceTime_s = d : Return True
                Case "Solids Loading" : SolidsLoading_wfrac = d : Return True
                Case "Cellulose Compound" : CelluloseCompound = propval?.ToString() : Return True
                Case "Hemicellulose Compound" : HemicelluloseCompound = propval?.ToString() : Return True
                Case "Lignin Compound" : LigninCompound = propval?.ToString() : Return True
                Case "Glucose Compound" : GlucoseCompound = propval?.ToString() : Return True
                Case "Xylose Compound" : XyloseCompound = propval?.ToString() : Return True
                Case "Furfural Compound" : FurfuralCompound = propval?.ToString() : Return True
                Case "HMF Compound" : HMFCompound = propval?.ToString() : Return True
                Case "Acetic Acid Compound" : AceticAcidCompound = propval?.ToString() : Return True
                Case "Water Compound" : WaterCompound = propval?.ToString() : Return True
                Case "Soluble Lignin Compound" : SolubleLigninCompound = propval?.ToString() : Return True
                Case "Acid Compound" : AcidCompound = propval?.ToString() : Return True
                Case "Acid Second pKa" : AcidSecondPKa = d : Return True
                Case "Cellulose Conversion" : CelluloseConversion = d : Return True
                Case "Glucose to HMF" : GlucoseToHMF = d : Return True
                Case "Hemicellulose Conversion" : HemicelluloseConversion = d : Return True
                Case "Xylose to Furfural" : XyloseToFurfural = d : Return True
                Case "Lignin Solubilization" : LigninSolubilization = d : Return True
                Case "Acetic Acid Yield on Hemi" : AceticAcidYieldOnHemi = d : Return True
                Case Else : Return MyBase.SetPropertyValue(prop, propval, su)
            End Select
        End Function

        ' IExternalUnitOperation
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
                Return "PRE-"
            End Get
        End Property
        Public Function ReturnInstance(typename As String) As Object Implements IExternalUnitOperation.ReturnInstance
            Return New Reactor_Pretreatment()
        End Function
        Public Sub PopulateEditorPanel(ctner As Object) Implements IExternalUnitOperation.PopulateEditorPanel

            If TypeOf ctner Is AvaloniaEditorPanel Then PopulateEditorPanelAvalonia(DirectCast(ctner, AvaloniaEditorPanel)) : Return
        End Sub

        Private Sub PopulateEditorPanelAvalonia(container As AvaloniaEditorPanel)

            Dim nf = FlowSheet.FlowsheetOptions.NumberFormat
            Dim compIds = FlowSheet.SelectedCompounds.Values.Select(Function(c) c.Name).ToList()

            container.CreateAndAddLabelRow("Pretreatment Technology")

            container.CreateAndAddDropDownRow("Technology",
                                              New List(Of String)({"Dilute Acid", "Steam Explosion", "Alkaline", "Organosolv"}),
                                              CInt(Technology),
                                              Sub(dd, e)
                                                  Technology = CType(dd.SelectedIndex, PretreatmentType)
                                                  FlowSheet.RequestCalculation()
                                              End Sub)

            container.CreateAndAddDropDownRow("Conversion Mode",
                                              New List(Of String)({"User Fractions", "Severity"}),
                                              CInt(ConversionMode),
                                              Sub(dd, e)
                                                  ConversionMode = CType(dd.SelectedIndex, PretreatmentConversionMode)
                                                  FlowSheet.RequestCalculation()
                                              End Sub)

            container.CreateAndAddLabelRow("Operating Conditions")

            container.CreateAndAddTextBoxRow(nf, "Severity log(R0) (used when residence time is 0)", SeverityLogR0,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     SeverityLogR0 = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Residence Time (s)", ResidenceTime_s,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     ResidenceTime_s = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Solids Loading (w. frac.)", SolidsLoading_wfrac,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     SolidsLoading_wfrac = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Acid Second pKa", AcidSecondPKa,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     AcidSecondPKa = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddLabelRow("Reaction Conversions (0-1, User Fractions mode)")

            container.CreateAndAddTextBoxRow(nf, "Cellulose to Glucose", CelluloseConversion,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     CelluloseConversion = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Glucose to HMF (side)", GlucoseToHMF,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     GlucoseToHMF = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Hemicellulose to Xylose", HemicelluloseConversion,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     HemicelluloseConversion = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Xylose to Furfural (side)", XyloseToFurfural,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     XyloseToFurfural = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Lignin Solubilization", LigninSolubilization,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     LigninSolubilization = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Acetic Acid Yield on Hemicellulose", AceticAcidYieldOnHemi,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     AceticAcidYieldOnHemi = tb.Text.ParseExpressionToDouble()
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

            addCompoundDropdownA("Cellulose", CelluloseCompound, Sub(v) CelluloseCompound = v)
            addCompoundDropdownA("Hemicellulose", HemicelluloseCompound, Sub(v) HemicelluloseCompound = v)
            addCompoundDropdownA("Lignin (insoluble)", LigninCompound, Sub(v) LigninCompound = v)
            addCompoundDropdownA("Lignin (soluble)", SolubleLigninCompound, Sub(v) SolubleLigninCompound = v)
            addCompoundDropdownA("Glucose", GlucoseCompound, Sub(v) GlucoseCompound = v)
            addCompoundDropdownA("Xylose", XyloseCompound, Sub(v) XyloseCompound = v)
            addCompoundDropdownA("Furfural", FurfuralCompound, Sub(v) FurfuralCompound = v)
            addCompoundDropdownA("HMF", HMFCompound, Sub(v) HMFCompound = v)
            addCompoundDropdownA("Acetic Acid", AceticAcidCompound, Sub(v) AceticAcidCompound = v)
            addCompoundDropdownA("Water", WaterCompound, Sub(v) WaterCompound = v)
            addCompoundDropdownA("Acid", AcidCompound, Sub(v) AcidCompound = v)

        End Sub

        Public Sub CreateConnectors() Implements IExternalUnitOperation.CreateConnectors
            If GraphicObject Is Nothing Then Return
            Dim w = GraphicObject.Width, h = GraphicObject.Height
            Dim gx = GraphicObject.X, gy = GraphicObject.Y
            If GraphicObject.InputConnectors.Count = 1 AndAlso GraphicObject.OutputConnectors.Count = 1 Then
                GraphicObject.InputConnectors(0).Position = New Point(gx, gy + 0.5 * h)
                GraphicObject.InputConnectors(0).ConnectorName = "Biomass Slurry"
                GraphicObject.OutputConnectors(0).Position = New Point(gx + w, gy + 0.5 * h)
                GraphicObject.OutputConnectors(0).ConnectorName = "Pretreated Slurry"
            Else
                GraphicObject.InputConnectors.Clear()
                GraphicObject.OutputConnectors.Clear()
                GraphicObject.InputConnectors.Add(New ConnectionPoint With {
                    .Position = New Point(gx, gy + 0.5 * h), .Type = ConType.ConIn,
                    .Direction = ConDir.Right, .ConnectorName = "Biomass Slurry"})
                GraphicObject.OutputConnectors.Add(New ConnectionPoint With {
                    .Position = New Point(gx + w, gy + 0.5 * h), .Type = ConType.ConOut,
                    .Direction = ConDir.Right, .ConnectorName = "Pretreated Slurry"})
            End If
            GraphicObject.EnergyConnector.Position = New Point(gx + 0.5 * w, gy + h)
            GraphicObject.EnergyConnector.Direction = ConDir.Up
            GraphicObject.EnergyConnector.Active = False
        End Sub

        <NonSerialized> <Xml.Serialization.XmlIgnore> Private _photoImage As SKImage

        Public Sub Draw(g As Object) Implements IExternalUnitOperation.Draw
            If GraphicObject Is Nothing Then Return
            Dim canvas As SKCanvas = DirectCast(g, SKCanvas)
            If GraphicObject.DrawMode = 2 Then
                If UnitOperations.BioOpsDrawHelper.TryDrawPhotorealistic(canvas,
                    GraphicObject.X, GraphicObject.Y, GraphicObject.Width, GraphicObject.Height,
                    "pretreatment_photo", _photoImage) Then Return
            End If
            DrawIcon(canvas, CSng(GraphicObject.X), CSng(GraphicObject.Y),
                     CSng(GraphicObject.Width), CSng(GraphicObject.Height),
                     GraphicObject.DrawMode = 1)
        End Sub

        Private Shared Sub DrawIcon(canvas As SKCanvas, gx As Single, gy As Single, w As Single, h As Single, Optional mono As Boolean = False)
            ' Horizontal jacketed pretreatment reactor on saddles with steam inlets + discharge nozzle.
            Dim saddle1 As New SKRect(gx + 0.18F * w, gy + 0.78F * h, gx + 0.32F * w, gy + h)
            Dim saddle2 As New SKRect(gx + 0.68F * w, gy + 0.78F * h, gx + 0.82F * w, gy + h)
            UnitOperations.BioOpsDrawHelper.DrawSkid(canvas, saddle1, mono)
            UnitOperations.BioOpsDrawHelper.DrawSkid(canvas, saddle2, mono)
            Dim vessel As New SKRect(gx + 0.1F * w, gy + 0.3F * h, gx + 0.9F * w, gy + 0.8F * h)
            UnitOperations.BioOpsDrawHelper.DrawHorizontalTank(canvas, vessel, mono)
            ' jacket outline (double line)
            Using s As New SKPaint With {.Color = If(mono, New SKColor(80, 80, 80), New SKColor(90, 110, 135)), .Style = SKPaintStyle.Stroke, .StrokeWidth = 0.9F, .IsAntialias = True}
                canvas.DrawRect(New SKRect(vessel.Left + 3, vessel.Top + 3, vessel.Right - 3, vessel.Bottom - 3), s)
            End Using
            ' two steam inlet nozzles on top (shorter, with flange at base on vessel)
            UnitOperations.BioOpsDrawHelper.DrawPipe(canvas, New SKPoint(gx + 0.3F * w, gy + 0.15F * h), New SKPoint(gx + 0.3F * w, gy + 0.3F * h), 0.035F * w, mono)
            UnitOperations.BioOpsDrawHelper.DrawFlange(canvas, gx + 0.3F * w, gy + 0.3F * h, 0.1F * w, mono)
            UnitOperations.BioOpsDrawHelper.DrawPipe(canvas, New SKPoint(gx + 0.7F * w, gy + 0.15F * h), New SKPoint(gx + 0.7F * w, gy + 0.3F * h), 0.035F * w, mono)
            UnitOperations.BioOpsDrawHelper.DrawFlange(canvas, gx + 0.7F * w, gy + 0.3F * h, 0.1F * w, mono)
            ' right-side coupling bay + motor (screw drive)
            Dim coupling As New SKRect(gx + 0.86F * w, gy + 0.45F * h, gx + 0.92F * w, gy + 0.63F * h)
            Using cpl As New SKPaint With {.Color = UnitOperations.BioOpsDrawHelper.ClrMetalMid(mono), .IsAntialias = True}
                canvas.DrawRect(coupling, cpl)
            End Using
            Using stroke As New SKPaint With {.Color = UnitOperations.BioOpsDrawHelper.ClrStroke(mono), .Style = SKPaintStyle.Stroke, .StrokeWidth = 1.0F, .IsAntialias = True}
                canvas.DrawRect(coupling, stroke)
            End Using
            UnitOperations.BioOpsDrawHelper.DrawFlange(canvas, gx + 0.86F * w, gy + 0.54F * h, 0.14F * w, mono)
            Dim motor As New SKRect(gx + 0.92F * w, gy + 0.42F * h, gx + w, gy + 0.66F * h)
            UnitOperations.BioOpsDrawHelper.DrawMotor(canvas, motor, mono)
        End Sub

    End Class

End Namespace
