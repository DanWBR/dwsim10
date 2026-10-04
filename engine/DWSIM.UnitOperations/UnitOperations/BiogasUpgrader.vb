'    Biogas Upgrader - Calculation Routines
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

Namespace UnitOperations

    Public Enum BiogasUpgraderTech
        WaterScrubbing = 0
        Amine = 1
        PSA = 2
        MembraneSeparation = 3
    End Enum

    ''' <summary>How the biogas upgrader uses <see cref="UnitOp_BiogasUpgrader.TargetCH4Purity"/>.</summary>
    Public Enum BiogasUpgraderPurityMode
        ''' <summary>The CO2 removal efficiency is an input; the target purity is stored for reference only.</summary>
        Report = 0
        ''' <summary>The CO2 removal is solved so that the upgraded gas reaches the target methane mole fraction.</summary>
        Target = 1
    End Enum

    ''' <summary>
    ''' Biogas upgrader. Two-stage algebraic model:
    '''   Stage 1 - H2S polishing (ZnO bed / caustic wash), default 99 % removal. Only active when
    '''             <see cref="H2SCompound"/> names a compound present in the feed; it is unassigned
    '''             by default, since a desulfurized feed is the common case.
    '''   Stage 2 - CO2 bulk removal (water scrubbing / amine / PSA / membrane), user-selectable
    '''             efficiency and CH4 loss per technology. With <see cref="PurityMode"/> set to Target,
    '''             the CO2 removal is solved for the methane mole fraction in <see cref="TargetCH4Purity"/>.
    ''' Splits the biogas feed into an Upgraded Gas (RNG-spec) outlet and an Off-gas outlet.
    ''' References: Ryckebosch, Drouillon and Vervaeren, Biomass and Bioenergy 35 (2011) 1633-1645;
    ''' Sun et al., Renewable and Sustainable Energy Reviews 51 (2015) 521-532; Angelidaki et al.,
    ''' Biotechnology Advances 36 (2018) 452-466; ISO 6976 (calorific value, density, Wobbe index);
    ''' EN 16723-1 (biomethane for injection in the natural gas network).
    ''' </summary>
    <System.Serializable()> Public Partial Class UnitOp_BiogasUpgrader

        Inherits UnitOperations.UnitOpBaseClass

        Implements IExternalUnitOperation

        ''' <summary>Gets a value indicating that this unit operation belongs to the Bio group of the object palette. Always <c>True</c>.</summary>
        Public ReadOnly Property IsBio As Boolean = True

        ''' <summary>Gets or sets the simulation object class category (Separators). The getter always returns <c>Separators</c>.</summary>
        Public Overrides Property ObjectClass As SimulationObjectClass
            Get
                Return SimulationObjectClass.Separators
            End Get
            Set(value As SimulationObjectClass)
                MyBase.ObjectClass = value
            End Set
        End Property

        ''' <summary>Gets or sets the CO2 removal technology: WaterScrubbing (0), Amine (1), PSA (2) or MembraneSeparation (3).
        ''' <see cref="ApplyTechnologyDefaults"/> uses it to set <see cref="CO2RemovalEfficiency"/>, <see cref="CH4LossFraction"/>
        ''' and <see cref="N2RemovalFraction"/>; the split itself reads only those values. Default Amine.</summary>
        Public Property Technology As BiogasUpgraderTech = BiogasUpgraderTech.Amine
        ''' <summary>Gets or sets the fraction (0-1) of the H2S compound sent to the off-gas outlet. Applied only when <see cref="H2SCompound"/> is assigned. Default 0.99.</summary>
        Public Property H2SRemovalEfficiency As Double = 0.99
        ''' <summary>Gets or sets the fraction (0-1) of the CO2 compound sent to the off-gas outlet. Default 0.95.
        ''' Ignored when <see cref="PurityMode"/> is Target; the removal solved then is in <see cref="Result_CO2RemovalApplied"/>.</summary>
        Public Property CO2RemovalEfficiency As Double = 0.95
        ''' <summary>Gets or sets the fraction (0-1) of the methane compound lost to the off-gas outlet. Default 0.01.</summary>
        Public Property CH4LossFraction As Double = 0.01 ' CH4 that ends up in off-gas
        ''' <summary>Gets or sets the fraction (0-1) of the water compound sent to the off-gas outlet. Default 0.98.</summary>
        Public Property H2ORemovalEfficiency As Double = 0.98
        ''' <summary>Gets or sets the target methane mole fraction of the upgraded gas (0-1). Used only when <see cref="PurityMode"/>
        ''' is Target; in Report mode it is stored and shown for reference. Default 0.96.</summary>
        Public Property TargetCH4Purity As Double = 0.96
        ''' <summary>Gets or sets how <see cref="TargetCH4Purity"/> is used. Report (default) keeps <see cref="CO2RemovalEfficiency"/>
        ''' as the input. Target solves the CO2 removal for the target methane mole fraction, given the other split fractions:
        ''' f_CO2 = 1 - [n_CH4 (1 - f_CH4) (1/y* - 1) - sum over i other than CH4 and CO2 of n_i (1 - f_i)] / n_CO2.
        ''' When the result falls outside 0-1 it is clamped and a warning names the highest purity the other fractions allow.</summary>
        Public Property PurityMode As BiogasUpgraderPurityMode = BiogasUpgraderPurityMode.Report
        ''' <summary>Gets or sets the fraction (0-1) of the nitrogen compound sent to the off-gas outlet. Applied only when
        ''' <see cref="N2Compound"/> is assigned. Default 0 (nitrogen behaves as an inert that stays in the upgraded gas).
        ''' <see cref="ApplyTechnologyDefaults"/> sets 0 for water scrubbing and amine, 0.2 for PSA and 0.05 for membranes.</summary>
        Public Property N2RemovalFraction As Double = 0.0

        ''' <summary>Gets or sets the name of the methane compound. Default "Methane".</summary>
        Public Property MethaneCompound As String = "Methane"
        ''' <summary>Gets or sets the name of the carbon dioxide compound. Default "Carbon dioxide".</summary>
        Public Property CO2Compound As String = "Carbon dioxide"
        ''' <summary>Gets or sets the name of the hydrogen sulfide compound. Empty by default; while it is empty, any H2S in the feed passes to the upgraded gas and a warning is shown.</summary>
        Public Property H2SCompound As String = ""
        ''' <summary>Gets or sets the name of the water compound. Default "Water".</summary>
        Public Property WaterCompound As String = "Water"
        ''' <summary>Gets or sets the name of the nitrogen compound. Empty by default. When assigned, <see cref="N2RemovalFraction"/>
        ''' of it goes to the off-gas and the rest to the upgraded gas; unassigned, nitrogen, like every unassigned compound, goes entirely to the upgraded gas.</summary>
        Public Property N2Compound As String = ""

        ''' <summary>Gets or sets the feed mass flow, in kg/s. Calculated result.</summary>
        Public Property Result_FeedMass_kgs As Double = 0.0
        ''' <summary>Gets or sets the upgraded gas mass flow, in kg/s. Calculated result.</summary>
        Public Property Result_UpgradedMass_kgs As Double = 0.0
        ''' <summary>Gets or sets the off-gas mass flow, in kg/s. Calculated result.</summary>
        Public Property Result_OffgasMass_kgs As Double = 0.0
        ''' <summary>Gets or sets the methane mass fraction in the upgraded gas (0-1). Calculated result. Purity specifications
        ''' are molar; see <see cref="Result_UpgradedCH4MoleFraction"/>.</summary>
        Public Property Result_UpgradedCH4Fraction As Double = 0.0
        ''' <summary>Gets or sets the methane mole fraction in the upgraded gas (0-1), the basis of <see cref="TargetCH4Purity"/>
        ''' and of biomethane specifications such as EN 16723-1. Calculated result.</summary>
        Public Property Result_UpgradedCH4MoleFraction As Double = 0.0
        ''' <summary>Gets or sets the fraction (0-1) of the feed methane recovered in the upgraded gas, on a mass basis. Calculated result.</summary>
        Public Property Result_CH4RecoveryFraction As Double = 0.0
        ''' <summary>Gets or sets the CO2 removal fraction (0-1) used in the last calculation: <see cref="CO2RemovalEfficiency"/>
        ''' in Report mode, the solved value in Target mode. Calculated result.</summary>
        Public Property Result_CO2RemovalApplied As Double = 0.0
        ''' <summary>Gets or sets the highest methane mole fraction (0-1) the upgraded gas can reach with every other split
        ''' fraction as set and all the CO2 removed. Calculated result.</summary>
        Public Property Result_MaxCH4MoleFraction As Double = 0.0
        ''' <summary>Gets or sets the ideal-gas gross (higher) calorific value of the upgraded gas per unit volume, in MJ/m3,
        ''' following ISO 6976: combustion at 25 °C, metering at 0 °C and 101.325 kPa. Calculated result.</summary>
        Public Property Result_HHV_MJm3 As Double = 0.0
        ''' <summary>Gets or sets the ideal-gas relative density of the upgraded gas to dry air (ISO 6976, M_air = 28.9626 g/mol). Calculated result.</summary>
        Public Property Result_RelativeDensity As Double = 0.0
        ''' <summary>Gets or sets the gross (superior) Wobbe index of the upgraded gas, in MJ/m3: W = HHV_v / sqrt(d), with
        ''' <see cref="Result_HHV_MJm3"/> and <see cref="Result_RelativeDensity"/> on the ISO 6976 ideal-gas basis
        ''' (combustion 25 °C, metering 0 °C, 101.325 kPa). Calculated result.</summary>
        Public Property Result_WobbeIndex As Double = 0.0

        ''' <summary>The classic (WinForms) editor window open for this unit operation, if any. Not saved with the flowsheet.</summary>
        <NonSerialized> <Xml.Serialization.XmlIgnore> Public f As Object

        ''' <summary>Gets a value indicating whether this unit operation supports dynamic simulation mode. Always <c>False</c>; it is calculated as a steady-state model.</summary>
        Public Overrides ReadOnly Property SupportsDynamicMode As Boolean = False
        ''' <summary>Gets a value indicating whether this unit operation is compatible with mobile interfaces. Always <c>False</c>.</summary>
        Public Overrides ReadOnly Property MobileCompatible As Boolean
            Get
                Return False
            End Get
        End Property

        ''' <summary>Initializes a new default instance of the <see cref="UnitOp_BiogasUpgrader"/> class.</summary>
        Public Sub New()
            MyBase.New()
        End Sub

        ''' <summary>Initializes a new instance of the <see cref="UnitOp_BiogasUpgrader"/> class with a name and description.</summary>
        ''' <param name="name">The name of this unit operation.</param>
        ''' <param name="description">A brief description of this unit operation.</param>
        Public Sub New(ByVal name As String, ByVal description As String)
            MyBase.New()
            Me.ComponentName = name
            Me.ComponentDescription = description
        End Sub

        ''' <summary>Creates a deep copy of this object by round-tripping through XML serialization.</summary>
        ''' <returns>A new <see cref="UnitOp_BiogasUpgrader"/> instance with the same property values.</returns>
        Public Overrides Function CloneXML() As Object
            Dim obj As ICustomXMLSerialization = New UnitOp_BiogasUpgrader()
            obj.LoadData(Me.SaveData)
            Return obj
        End Function

        ''' <summary>Apply default removal efficiencies for the selected technology.</summary>
        ''' <remarks>
        ''' Nitrogen: water scrubbing and amine absorption leave N2 (and O2) in the biomethane; carbon molecular sieve PSA
        ''' removes part of it, and polymeric membranes very little, since N2 and CH4 permeate at similar rates
        ''' (Ryckebosch et al. 2011; Sun et al. 2015; Angelidaki et al. 2018). The PSA (0.2) and membrane (0.05)
        ''' values are indicative points inside that qualitative "partial removal"; set <see cref="N2RemovalFraction"/>
        ''' from vendor data when it matters.
        ''' </remarks>
        Public Sub ApplyTechnologyDefaults()
            Select Case Technology
                Case BiogasUpgraderTech.WaterScrubbing
                    CO2RemovalEfficiency = 0.92 : CH4LossFraction = 0.02 : N2RemovalFraction = 0.0
                Case BiogasUpgraderTech.Amine
                    CO2RemovalEfficiency = 0.99 : CH4LossFraction = 0.001 : N2RemovalFraction = 0.0
                Case BiogasUpgraderTech.PSA
                    CO2RemovalEfficiency = 0.95 : CH4LossFraction = 0.03 : N2RemovalFraction = 0.2
                Case BiogasUpgraderTech.MembraneSeparation
                    CO2RemovalEfficiency = 0.90 : CH4LossFraction = 0.02 : N2RemovalFraction = 0.05
            End Select
        End Sub

        ' ISO 6976 ideal molar gross calorific values at 25 °C, kJ/mol (ISO 6976:1995, Table 3), keyed by CAS number.
        ' Compounds not listed (CO2, N2, O2, water, argon, ...) contribute nothing.
        Private Shared ReadOnly _grossCalorificValue25C As New Dictionary(Of String, Double) From {
            {"74-82-8", 890.63},    ' methane
            {"74-84-0", 1560.69},   ' ethane
            {"74-98-6", 2219.17},   ' propane
            {"106-97-8", 2877.4},   ' n-butane
            {"75-28-5", 2868.2},    ' isobutane
            {"1333-74-0", 285.83},  ' hydrogen
            {"630-08-0", 282.98},   ' carbon monoxide
            {"7783-06-4", 562.01},  ' hydrogen sulfide
            {"7664-41-7", 382.81}}  ' ammonia
        Private Shared ReadOnly _nonCombustibles As String() = {
            "124-38-9", "7727-37-9", "7782-44-7", "7732-18-5", "7440-37-1", "7440-59-7"}
        ' ISO 6976:1995 molar mass of dry air (g/mol) and ideal molar volume at 0 °C, 101.325 kPa (m3/kmol)
        Private Const AirMolarMass As Double = 28.9626
        Private Const IdealMolarVolume0C As Double = 22.41397

        Public Overrides Sub Calculate(Optional ByVal args As Object = Nothing)

            If Not Me.GraphicObject.InputConnectors(0).IsAttached Then _
                Throw New Exception("BiogasUpgrader: Biogas feed not connected.")
            If Me.GraphicObject.OutputConnectors.Count < 2 OrElse
               Not Me.GraphicObject.OutputConnectors(0).IsAttached OrElse
               Not Me.GraphicObject.OutputConnectors(1).IsAttached Then
                Throw New Exception("BiogasUpgrader: Both Upgraded and Offgas outlets must be connected.")
            End If

            Dim feed As MaterialStream =
                DirectCast(FlowSheet.SimulationObjects(Me.GraphicObject.InputConnectors(0).AttachedConnector.AttachedFrom.Name), MaterialStream)

            Dim T = feed.Phases(0).Properties.temperature.GetValueOrDefault
            Dim P = feed.Phases(0).Properties.pressure.GetValueOrDefault
            Dim m_total = feed.Phases(0).Properties.massflow.GetValueOrDefault

            Dim feedComp As New Dictionary(Of String, Double)
            For Each c In feed.Phases(0).Compounds.Values
                feedComp(c.Name) = c.MassFraction.GetValueOrDefault * m_total
            Next

            ' H2S is only routed to the offgas when the H2S compound role is assigned. Leaving it
            ' unassigned while the feed actually carries H2S sends all of it to the upgraded gas and
            ' silently ignores H2SRemovalEfficiency, so say so rather than fail quietly.
            If String.IsNullOrEmpty(H2SCompound) AndAlso FlowSheet IsNot Nothing Then
                Dim h2s = feed.Phases(0).Compounds.Values.FirstOrDefault(
                    Function(c) (c.ConstantProperties.Formula = "H2S" OrElse
                                 c.ConstantProperties.CAS_Number = "7783-06-4") AndAlso
                                feedComp(c.Name) > 0.0)
                If h2s IsNot Nothing Then
                    FlowSheet.ShowMessage(GraphicObject.Tag & ": feed contains " & h2s.Name &
                        " but no H2S compound is assigned, so the H2S removal efficiency is ignored" &
                        " and the H2S passes through to the upgraded gas. Set the H2S Compound role" &
                        " to remove it.", IFlowsheet.MessageType.Warning)
                End If
            End If

            Dim mw As New Dictionary(Of String, Double)
            Dim cas As New Dictionary(Of String, String)
            For Each c In feed.Phases(0).Compounds.Values
                mw(c.Name) = c.ConstantProperties.Molar_Weight
                cas(c.Name) = If(c.ConstantProperties.CAS_Number, "")
            Next

            ' Per-compound split rule: user specifies a "fraction-to-offgas" for the key species,
            ' everything else defaults to upgraded stream (clean).
            Dim fOff As New Dictionary(Of String, Double)
            For Each cname In feedComp.Keys
                fOff(cname) = FractionToOffgas(cname)
            Next

            ' molar flows (kmol/s) reaching the upgraded gas, CO2 apart
            Dim nCO2 As Double = 0.0, nCH4Upg As Double = 0.0, nOtherUpg As Double = 0.0
            For Each kv In feedComp
                Dim n = kv.Value / mw(kv.Key)
                If kv.Key = CO2Compound Then
                    nCO2 = n
                ElseIf kv.Key = MethaneCompound Then
                    nCH4Upg = n * (1.0 - fOff(kv.Key))
                Else
                    nOtherUpg += n * (1.0 - fOff(kv.Key))
                End If
            Next
            Result_MaxCH4MoleFraction = If(nCH4Upg + nOtherUpg > 0.0, nCH4Upg / (nCH4Upg + nOtherUpg), 0.0)

            If PurityMode = BiogasUpgraderPurityMode.Target AndAlso fOff.ContainsKey(CO2Compound) Then
                fOff(CO2Compound) = SolveCO2Removal(nCH4Upg, nCO2, nOtherUpg, fOff(CO2Compound))
            End If
            Result_CO2RemovalApplied = If(fOff.ContainsKey(CO2Compound), fOff(CO2Compound), 0.0)

            Dim upg As New Dictionary(Of String, Double)
            Dim off As New Dictionary(Of String, Double)
            For Each kv In feedComp
                Dim toOff = fOff(kv.Key)
                off(kv.Key) = kv.Value * toOff
                upg(kv.Key) = kv.Value * (1.0 - toOff)
            Next

            Dim m_upg As Double = 0.0, m_off As Double = 0.0
            For Each v In upg.Values : m_upg += v : Next
            For Each v In off.Values : m_off += v : Next

            Result_FeedMass_kgs = m_total
            Result_UpgradedMass_kgs = m_upg
            Result_OffgasMass_kgs = m_off

            ' CH4 mass fraction and mole fraction in upgraded gas (if CH4 is present)
            Dim ch4_mass_upg As Double = 0.0
            If upg.ContainsKey(MethaneCompound) Then ch4_mass_upg = upg(MethaneCompound)
            If m_upg > 0 Then Result_UpgradedCH4Fraction = ch4_mass_upg / m_upg Else Result_UpgradedCH4Fraction = 0.0

            Dim ch4_feed As Double = 0.0
            If feedComp.ContainsKey(MethaneCompound) Then ch4_feed = feedComp(MethaneCompound)
            If ch4_feed > 0 Then Result_CH4RecoveryFraction = ch4_mass_upg / ch4_feed Else Result_CH4RecoveryFraction = 0.0

            ' upgraded gas mole fractions, then heating value, relative density and Wobbe index (ISO 6976, ideal gas)
            Dim n_upg As Double = 0.0
            For Each kv In upg : n_upg += kv.Value / mw(kv.Key) : Next
            Dim hm As Double = 0.0, molw As Double = 0.0, ch4mol As Double = 0.0
            Dim missing As New List(Of String)
            If n_upg > 0.0 Then
                For Each kv In upg
                    Dim y = kv.Value / mw(kv.Key) / n_upg
                    molw += y * mw(kv.Key)
                    If kv.Key = MethaneCompound Then ch4mol = y
                    Dim gcv As Double
                    If _grossCalorificValue25C.TryGetValue(cas(kv.Key), gcv) Then
                        hm += y * gcv
                    ElseIf kv.Key = MethaneCompound Then
                        hm += y * _grossCalorificValue25C("74-82-8")
                    ElseIf y > 0.000001 AndAlso Not _nonCombustibles.Contains(cas(kv.Key)) Then
                        missing.Add(kv.Key)
                    End If
                Next
            End If
            Result_UpgradedCH4MoleFraction = ch4mol
            Result_HHV_MJm3 = hm / IdealMolarVolume0C
            Result_RelativeDensity = molw / AirMolarMass
            Result_WobbeIndex = If(Result_RelativeDensity > 0.0, Result_HHV_MJm3 / Sqrt(Result_RelativeDensity), 0.0)
            If missing.Count > 0 AndAlso FlowSheet IsNot Nothing Then
                FlowSheet.ShowMessage(GraphicObject.Tag & ": the heating value and Wobbe index leave out " &
                    String.Join(", ", missing) & ", which have no ISO 6976 calorific value in this model.",
                    IFlowsheet.MessageType.Warning)
            End If

            WriteStream(FlowSheet.SimulationObjects(Me.GraphicObject.OutputConnectors(0).AttachedConnector.AttachedTo.Name),
                        upg, m_upg, T, P)
            WriteStream(FlowSheet.SimulationObjects(Me.GraphicObject.OutputConnectors(1).AttachedConnector.AttachedTo.Name),
                        off, m_off, T, P)

        End Sub

        ''' <summary>The fraction (0-1) of a feed compound sent to the off-gas, from the compound roles and the removal inputs.</summary>
        ''' <param name="name">The compound name.</param>
        Private Function FractionToOffgas(name As String) As Double
            If name = CO2Compound Then
                Return Max(0.0, Min(1.0, CO2RemovalEfficiency))
            ElseIf name = H2SCompound AndAlso Not String.IsNullOrEmpty(H2SCompound) Then
                Return Max(0.0, Min(1.0, H2SRemovalEfficiency))
            ElseIf name = MethaneCompound Then
                Return Max(0.0, Min(1.0, CH4LossFraction))
            ElseIf name = WaterCompound AndAlso Not String.IsNullOrEmpty(WaterCompound) Then
                Return Max(0.0, Min(1.0, H2ORemovalEfficiency))
            ElseIf name = N2Compound AndAlso Not String.IsNullOrEmpty(N2Compound) Then
                Return Max(0.0, Min(1.0, N2RemovalFraction))
            Else
                ' Inerts / trace gases (O2, etc.) carry through to the upgraded gas
                Return 0.0
            End If
        End Function

        ''' <summary>
        ''' The CO2 removal that gives the upgraded gas the target methane mole fraction y*:
        ''' f_CO2 = 1 - [n_CH4 (1 - f_CH4) (1/y* - 1) - sum n_i (1 - f_i)] / n_CO2, the sum running over every compound
        ''' other than CH4 and CO2. Clamped to 0-1 with a warning when the target cannot be met.
        ''' </summary>
        ''' <param name="nCH4Upg">Methane molar flow reaching the upgraded gas, n_CH4 (1 - f_CH4).</param>
        ''' <param name="nCO2">CO2 molar flow in the feed.</param>
        ''' <param name="nOtherUpg">Molar flow of the other compounds reaching the upgraded gas.</param>
        ''' <param name="fallback">The CO2 removal kept when there is nothing to solve (no CO2 or no methane).</param>
        Private Function SolveCO2Removal(nCH4Upg As Double, nCO2 As Double, nOtherUpg As Double, fallback As Double) As Double

            Dim target = TargetCH4Purity
            If Not (target > 0.0 AndAlso target <= 1.0) Then
                Throw New Exception("BiogasUpgrader: the target CH4 purity must be a mole fraction above 0 and at most 1.")
            End If

            If nCH4Upg <= 0.0 OrElse nCO2 <= 0.0 Then
                If FlowSheet IsNot Nothing Then
                    FlowSheet.ShowMessage(GraphicObject.Tag & ": the target CH4 purity needs methane and CO2 in the feed (" &
                        MethaneCompound & ", " & CO2Compound & "); the CO2 removal efficiency is used as entered.",
                        IFlowsheet.MessageType.Warning)
                End If
                Return fallback
            End If

            Dim f = 1.0 - (nCH4Upg * (1.0 / target - 1.0) - nOtherUpg) / nCO2
            Dim ymax = nCH4Upg / (nCH4Upg + nOtherUpg)

            If f > 1.0 Then
                If f > 1.0 + 0.000000001 AndAlso FlowSheet IsNot Nothing Then
                    FlowSheet.ShowMessage(GraphicObject.Tag & ": a CH4 purity of " & target.ToString("0.####") &
                        " cannot be reached: with every CO2 molecule removed and the other fractions as set, the upgraded gas" &
                        " holds at most " & ymax.ToString("0.####") & " CH4 (mole fraction). CO2 removal set to 1." &
                        " Remove more of the other compounds (water, N2, H2S) or lower the target.",
                        IFlowsheet.MessageType.Warning)
                End If
                Return 1.0
            ElseIf f < 0.0 Then
                If FlowSheet IsNot Nothing Then
                    Dim y0 = nCH4Upg / (nCH4Upg + nOtherUpg + nCO2)
                    FlowSheet.ShowMessage(GraphicObject.Tag & ": the target CH4 purity of " & target.ToString("0.####") &
                        " is below the purity with no CO2 removal (" & y0.ToString("0.####") & ", mole fraction). CO2 removal" &
                        " set to 0; the highest purity reachable is " & ymax.ToString("0.####") & ".",
                        IFlowsheet.MessageType.Warning)
                End If
                Return 0.0
            End If

            Return f

        End Function

        Private Shared Sub WriteStream(ms As MaterialStream, m As Dictionary(Of String, Double), total As Double, T As Double, P As Double)
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
                'a single-compound outlet would otherwise be re-flashed at PH with the H cleared above
                .OverrideSingleCompoundFlashBehavior = True
            End With
        End Sub

        Public Overrides Sub DeCalculate()
            For i = 0 To Math.Min(1, Me.GraphicObject.OutputConnectors.Count - 1)
                Dim cp = Me.GraphicObject.OutputConnectors(i)
                If cp.IsAttached Then
                    Dim ms As MaterialStream = FlowSheet.SimulationObjects(cp.AttachedConnector.AttachedTo.Name)
                    With ms
                        .Phases(0).Properties.temperature = Nothing
                        .Phases(0).Properties.pressure = Nothing
                        For Each c In .Phases(0).Compounds.Values
                            c.MoleFraction = 0 : c.MassFraction = 0
                        Next
                        .Phases(0).Properties.massflow = Nothing
                        .GraphicObject.Calculated = False
                    End With
                End If
            Next
        End Sub

        ''' <summary>Returns the raw bytes of the icon image for this unit operation.</summary>
        ''' <returns>A byte array containing the PNG image data for the icon.</returns>
        Public Overrides Function GetIconBitmapBytes() As Byte()
            Return BioOpsDrawHelper.RenderIconToPngBytes(64, 64, AddressOf DrawIcon)
        End Function
        ''' <summary>Returns the localized description string for this unit operation type.</summary>
        ''' <returns>A translated description string identifying this unit operation type.</returns>
        Public Overrides Function GetDisplayDescription() As String
            Return "Biogas Upgrader (H2S + CO2 removal -> RNG)"
        End Function
        ''' <summary>Returns the localized display name for this unit operation type.</summary>
        ''' <returns>A translated name string for this unit operation type.</returns>
        Public Overrides Function GetDisplayName() As String
            Return "Biogas Upgrader"
        End Function

        ''' <summary>Generates a plain-text results report for this unit operation.</summary>
        ''' <param name="su">The unit system used for formatting output values.</param>
        ''' <param name="ci">The culture info used for number formatting.</param>
        ''' <param name="numberformat">A .NET numeric format string (e.g. "G6") applied to output values.</param>
        ''' <returns>A formatted multi-line string report.</returns>
        Public Overrides Function GetReport(su As IUnitsOfMeasure, ci As Globalization.CultureInfo, numberformat As String) As String
            Dim s As New Text.StringBuilder
            s.AppendLine("BiogasUpgrader: " & Me.GraphicObject.Tag)
            s.AppendLine("Technology:   " & Technology.ToString())
            s.AppendLine("Purity mode:  " & PurityMode.ToString())
            If PurityMode = BiogasUpgraderPurityMode.Target Then
                s.AppendLine("Target CH4:   " & (TargetCH4Purity * 100).ToString(numberformat, ci) & " % (mole)")
            Else
                s.AppendLine("CO2 removal:  " & (CO2RemovalEfficiency * 100).ToString(numberformat, ci) & " %")
            End If
            s.AppendLine("CH4 loss:     " & (CH4LossFraction * 100).ToString(numberformat, ci) & " %")
            s.AppendLine("H2S removal:  " & (H2SRemovalEfficiency * 100).ToString(numberformat, ci) & " %")
            s.AppendLine("N2 removal:   " & (N2RemovalFraction * 100).ToString(numberformat, ci) & " %")
            s.AppendLine()
            s.AppendLine("Feed:          " & Result_FeedMass_kgs.ToString(numberformat, ci) & " kg/s")
            s.AppendLine("Upgraded:      " & Result_UpgradedMass_kgs.ToString(numberformat, ci) & " kg/s")
            s.AppendLine("Offgas:        " & Result_OffgasMass_kgs.ToString(numberformat, ci) & " kg/s")
            s.AppendLine("Upgraded CH4:  " & (Result_UpgradedCH4MoleFraction * 100).ToString(numberformat, ci) & " % (mole)")
            s.AppendLine("Upgraded CH4:  " & (Result_UpgradedCH4Fraction * 100).ToString(numberformat, ci) & " % (mass)")
            s.AppendLine("Max. CH4:      " & (Result_MaxCH4MoleFraction * 100).ToString(numberformat, ci) & " % (mole, all CO2 removed)")
            s.AppendLine("CO2 removal:   " & (Result_CO2RemovalApplied * 100).ToString(numberformat, ci) & " % (applied)")
            s.AppendLine("CH4 recovery:  " & (Result_CH4RecoveryFraction * 100).ToString(numberformat, ci) & " %")
            s.AppendLine("HHV:           " & Result_HHV_MJm3.ToString(numberformat, ci) & " MJ/m3 (ISO 6976 ideal gas, 25 C / 0 C)")
            s.AppendLine("Rel. density:  " & Result_RelativeDensity.ToString(numberformat, ci))
            s.AppendLine("Wobbe index:   " & Result_WobbeIndex.ToString(numberformat, ci) & " MJ/m3 (gross, 25 C / 0 C)")
            Return s.ToString()
        End Function

        Private Shared ReadOnly _inputProps As String() = {
            "Technology", "H2S Removal", "CO2 Removal", "CH4 Loss", "H2O Removal", "Target CH4 Purity",
            "Methane Compound", "CO2 Compound", "H2S Compound", "Water Compound", "N2 Compound",
            "Purity Mode", "N2 Removal"}
        Private Shared ReadOnly _outputProps As String() = {
            "Feed Mass", "Upgraded Mass", "Offgas Mass", "Upgraded CH4 Fraction", "CH4 Recovery",
            "Upgraded CH4 Mole Fraction", "CO2 Removal Applied", "Maximum CH4 Mole Fraction",
            "Higher Heating Value", "Relative Density", "Wobbe Index"}

        Public Overrides Function GetProperties(proptype As PropertyType) As String()
            Dim baseprops = MyBase.GetProperties(proptype)
            Select Case proptype
                Case PropertyType.WR
                    ' in Target mode the CO2 removal is solved, not entered
                    If PurityMode = BiogasUpgraderPurityMode.Target Then Return _inputProps.Where(Function(p) p <> "CO2 Removal").ToArray()
                    Return _inputProps
                Case PropertyType.RO : Return _outputProps
                Case Else : Return _inputProps.Concat(_outputProps).Concat(baseprops).ToArray()
            End Select
        End Function

        Public Overrides Function GetPropertyValue(prop As String, Optional su As IUnitsOfMeasure = Nothing) As Object
            Select Case prop
                Case "Technology" : Return Technology.ToString()
                Case "H2S Removal" : Return H2SRemovalEfficiency
                Case "CO2 Removal" : Return CO2RemovalEfficiency
                Case "CH4 Loss" : Return CH4LossFraction
                Case "H2O Removal" : Return H2ORemovalEfficiency
                Case "Target CH4 Purity" : Return TargetCH4Purity
                Case "Methane Compound" : Return MethaneCompound
                Case "CO2 Compound" : Return CO2Compound
                Case "H2S Compound" : Return H2SCompound
                Case "Water Compound" : Return WaterCompound
                Case "N2 Compound" : Return N2Compound
                Case "Purity Mode" : Return PurityMode.ToString()
                Case "N2 Removal" : Return N2RemovalFraction
                Case "Feed Mass" : Return Result_FeedMass_kgs
                Case "Upgraded Mass" : Return Result_UpgradedMass_kgs
                Case "Offgas Mass" : Return Result_OffgasMass_kgs
                Case "Upgraded CH4 Fraction" : Return Result_UpgradedCH4Fraction
                Case "CH4 Recovery" : Return Result_CH4RecoveryFraction
                Case "Upgraded CH4 Mole Fraction" : Return Result_UpgradedCH4MoleFraction
                Case "CO2 Removal Applied" : Return Result_CO2RemovalApplied
                Case "Maximum CH4 Mole Fraction" : Return Result_MaxCH4MoleFraction
                Case "Higher Heating Value" : Return Result_HHV_MJm3
                Case "Relative Density" : Return Result_RelativeDensity
                Case "Wobbe Index" : Return Result_WobbeIndex
                Case Else : Return MyBase.GetPropertyValue(prop, su)
            End Select
        End Function

        Public Overrides Function GetPropertyUnit(prop As String, Optional su As IUnitsOfMeasure = Nothing) As String
            Select Case prop
                Case "Feed Mass", "Upgraded Mass", "Offgas Mass" : Return "kg/s"
                Case "Higher Heating Value", "Wobbe Index" : Return "MJ/m3"
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
                    Dim t As BiogasUpgraderTech
                    If [Enum].TryParse(Of BiogasUpgraderTech)(propval?.ToString(), t) Then Technology = t
                    Return True
                Case "H2S Removal" : H2SRemovalEfficiency = d : Return True
                Case "CO2 Removal" : CO2RemovalEfficiency = d : Return True
                Case "CH4 Loss" : CH4LossFraction = d : Return True
                Case "H2O Removal" : H2ORemovalEfficiency = d : Return True
                Case "Target CH4 Purity" : TargetCH4Purity = d : Return True
                Case "Methane Compound" : MethaneCompound = propval?.ToString() : Return True
                Case "CO2 Compound" : CO2Compound = propval?.ToString() : Return True
                Case "H2S Compound" : H2SCompound = propval?.ToString() : Return True
                Case "Water Compound" : WaterCompound = propval?.ToString() : Return True
                Case "N2 Compound" : N2Compound = propval?.ToString() : Return True
                Case "Purity Mode"
                    Dim m As BiogasUpgraderPurityMode
                    If [Enum].TryParse(Of BiogasUpgraderPurityMode)(propval?.ToString(), m) Then PurityMode = m
                    Return True
                Case "N2 Removal" : N2RemovalFraction = d : Return True
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
                Return "BGU-"
            End Get
        End Property
        Public Function ReturnInstance(typename As String) As Object Implements IExternalUnitOperation.ReturnInstance
            Return New UnitOp_BiogasUpgrader()
        End Function

        Public Sub PopulateEditorPanel(ctner As Object) Implements IExternalUnitOperation.PopulateEditorPanel

            If TypeOf ctner Is AvaloniaEditorPanel Then
                PopulateEditorPanelAvalonia(DirectCast(ctner, AvaloniaEditorPanel))
                Return
            End If
        End Sub

        Private Sub PopulateEditorPanelAvalonia(container As AvaloniaEditorPanel)

            Dim nf = FlowSheet.FlowsheetOptions.NumberFormat
            Dim compIds = FlowSheet.SelectedCompounds.Values.Select(Function(c) c.Name).ToList()

            container.CreateAndAddLabelRow("Upgrader Technology")

            container.CreateAndAddDropDownRow("Technology",
                                              New List(Of String)({"Water Scrubbing", "Amine", "PSA", "Membrane Separation"}),
                                              Technology,
                                              Sub(dd, e)
                                                  Technology = CType(dd.SelectedIndex, BiogasUpgraderTech)
                                                  FlowSheet.RequestCalculation()
                                              End Sub)

            container.CreateAndAddLabelRow("Removal Efficiencies (0-1)")

            container.CreateAndAddTextBoxRow(nf, "H2S Removal Efficiency", H2SRemovalEfficiency,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     H2SRemovalEfficiency = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "CO2 Removal Efficiency", CO2RemovalEfficiency,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     CO2RemovalEfficiency = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "H2O Removal Efficiency", H2ORemovalEfficiency,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     H2ORemovalEfficiency = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "CH4 Loss to Off-gas", CH4LossFraction,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     CH4LossFraction = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "N2 Removal (when N2 is assigned)", N2RemovalFraction,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     N2RemovalFraction = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddLabelRow("Methane Purity")

            container.CreateAndAddDropDownRow("Purity Mode",
                                              New List(Of String)({"Report (CO2 removal is an input)", "Target (solve CO2 removal)"}),
                                              PurityMode,
                                              Sub(dd, e)
                                                  PurityMode = CType(dd.SelectedIndex, BiogasUpgraderPurityMode)
                                                  FlowSheet.RequestCalculation()
                                              End Sub)

            container.CreateAndAddTextBoxRow(nf, "Target CH4 Purity (mole fraction)", TargetCH4Purity,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     TargetCH4Purity = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddLabelRow("Compound Mapping")

            Dim addCompoundDropdown =
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

            addCompoundDropdown("Methane (CH4)", MethaneCompound, Sub(v) MethaneCompound = v)
            addCompoundDropdown("Carbon Dioxide (CO2)", CO2Compound, Sub(v) CO2Compound = v)
            addCompoundDropdown("Hydrogen Sulfide (H2S)", H2SCompound, Sub(v) H2SCompound = v)
            addCompoundDropdown("Water (H2O)", WaterCompound, Sub(v) WaterCompound = v)
            addCompoundDropdown("Nitrogen (N2)", N2Compound, Sub(v) N2Compound = v)

        End Sub

        Public Sub CreateConnectors() Implements IExternalUnitOperation.CreateConnectors
            If GraphicObject Is Nothing Then Return
            Dim w = GraphicObject.Width, h = GraphicObject.Height
            Dim gx = GraphicObject.X, gy = GraphicObject.Y
            If GraphicObject.InputConnectors.Count = 1 AndAlso GraphicObject.OutputConnectors.Count = 2 Then
                GraphicObject.InputConnectors(0).Position = New Point(gx, gy + 0.5 * h)
                GraphicObject.InputConnectors(0).ConnectorName = "Biogas"
                GraphicObject.OutputConnectors(0).Position = New Point(gx + w, gy + 0.3 * h)
                GraphicObject.OutputConnectors(0).ConnectorName = "Upgraded Gas (RNG)"
                GraphicObject.OutputConnectors(1).Position = New Point(gx + w, gy + 0.7 * h)
                GraphicObject.OutputConnectors(1).ConnectorName = "Off-gas"
            Else
                GraphicObject.InputConnectors.Clear() : GraphicObject.OutputConnectors.Clear()
                GraphicObject.InputConnectors.Add(New ConnectionPoint With {
                    .Position = New Point(gx, gy + 0.5 * h), .Type = ConType.ConIn,
                    .Direction = ConDir.Right, .ConnectorName = "Biogas"})
                GraphicObject.OutputConnectors.Add(New ConnectionPoint With {
                    .Position = New Point(gx + w, gy + 0.3 * h), .Type = ConType.ConOut,
                    .Direction = ConDir.Right, .ConnectorName = "Upgraded Gas (RNG)"})
                GraphicObject.OutputConnectors.Add(New ConnectionPoint With {
                    .Position = New Point(gx + w, gy + 0.7 * h), .Type = ConType.ConOut,
                    .Direction = ConDir.Right, .ConnectorName = "Off-gas"})
            End If
            GraphicObject.EnergyConnector.Active = False
        End Sub

        <NonSerialized> <Xml.Serialization.XmlIgnore> Private _photoImage As SKImage

        Public Sub Draw(g As Object) Implements IExternalUnitOperation.Draw
            If GraphicObject Is Nothing Then Return
            Dim canvas As SKCanvas = DirectCast(g, SKCanvas)
            If GraphicObject.DrawMode = 2 Then
                If BioOpsDrawHelper.TryDrawPhotorealistic(canvas,
                    GraphicObject.X, GraphicObject.Y, GraphicObject.Width, GraphicObject.Height,
                    "biogas_upgrader_photo", _photoImage) Then Return
            End If
            DrawIcon(canvas, CSng(GraphicObject.X), CSng(GraphicObject.Y),
                     CSng(GraphicObject.Width), CSng(GraphicObject.Height),
                     GraphicObject.DrawMode = 1)
        End Sub

        Private Shared Sub DrawIcon(canvas As SKCanvas, gx As Single, gy As Single, w As Single, h As Single, Optional mono As Boolean = False)
            ' Biogas upgrading skid: H2S polisher + CO2 absorber columns on shared skid with crossover piping.
            Dim skid As New SKRect(gx + 0.05F * w, gy + 0.85F * h, gx + 0.95F * w, gy + h)
            BioOpsDrawHelper.DrawSkid(canvas, skid, mono)
            Dim col1 As New SKRect(gx + 0.13F * w, gy + 0.2F * h, gx + 0.4F * w, gy + 0.87F * h)
            Dim col2 As New SKRect(gx + 0.55F * w, gy + 0.2F * h, gx + 0.82F * w, gy + 0.87F * h)
            BioOpsDrawHelper.DrawVerticalTank(canvas, col1, mono)
            BioOpsDrawHelper.DrawVerticalTank(canvas, col2, mono)
            ' small vent stubs on top of each column
            Dim cx1 = (col1.Left + col1.Right) * 0.5F
            Dim cx2 = (col2.Left + col2.Right) * 0.5F
            BioOpsDrawHelper.DrawPipe(canvas, New SKPoint(cx1, gy + 0.12F * h), New SKPoint(cx1, col1.Top), 0.025F * w, mono)
            BioOpsDrawHelper.DrawPipe(canvas, New SKPoint(cx2, gy + 0.12F * h), New SKPoint(cx2, col2.Top), 0.025F * w, mono)
            ' crossover pipe between upper sides
            BioOpsDrawHelper.DrawPipe(canvas, New SKPoint(col1.Right, gy + 0.28F * h), New SKPoint(col2.Left, gy + 0.28F * h), 0.04F * h, mono)
            ' flanges at top
            BioOpsDrawHelper.DrawFlange(canvas, cx1, col1.Top, col1.Width * 0.8F, mono)
            BioOpsDrawHelper.DrawFlange(canvas, cx2, col2.Top, col2.Width * 0.8F, mono)
            ' inlet and outlet pipes with flanges
            BioOpsDrawHelper.DrawPipe(canvas, New SKPoint(gx + 0.02F * w, gy + 0.4F * h), New SKPoint(col1.Left, gy + 0.4F * h), 0.035F * h, mono)
            BioOpsDrawHelper.DrawFlange(canvas, col1.Left, gy + 0.4F * h, 0.08F * w, mono)
            BioOpsDrawHelper.DrawPipe(canvas, New SKPoint(col2.Right, gy + 0.7F * h), New SKPoint(gx + 0.98F * w, gy + 0.7F * h), 0.035F * h, mono)
            BioOpsDrawHelper.DrawFlange(canvas, col2.Right, gy + 0.7F * h, 0.08F * w, mono)
            ' labels
            Using txt As New SKPaint With {.Color = If(mono, New SKColor(30, 30, 30), New SKColor(40, 70, 95)), .IsAntialias = True,
                                           .TextSize = 0.12F * h, .TextAlign = SKTextAlign.Center, .Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold)}
                canvas.DrawText("H" & ChrW(&H2082) & "S", (col1.Left + col1.Right) * 0.5F, gy + 0.55F * h, txt)
                canvas.DrawText("CO" & ChrW(&H2082), (col2.Left + col2.Right) * 0.5F, gy + 0.55F * h, txt)
            End Using
        End Sub

    End Class

End Namespace
