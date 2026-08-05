'    Crossflow Ultrafiltration / Diafiltration (UF/DF) - Calculation Routines
'    Copyright 2026 Daniel Wagner O. de Medeiros
'
'    This file is part of DWSIM.
'
'    DWSIM is free software: you can redistribute it and/or modify
'    it under the terms of the GNU General Public License as published by
'    the Free Software Foundation, either version 3 of the License, or
'    (at your option) any later version.

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

    ''' <summary>Operating mode of the Crossflow UF/DF block.</summary>
    Public Enum CrossflowUFMode
        ''' <summary>Batch concentration: pulls permeate until retentate volume = feed volume / VCF.</summary>
        Concentration = 0
        ''' <summary>Constant-volume diafiltration: N diavolumes of buffer exchanged at constant retentate volume.</summary>
        DiafiltrationConstantVolume = 1
        ''' <summary>Dynamic batch concentration with Hermia pore-blocking flux decline J(t) = J0 / (1 + t/Ï„).</summary>
        ConcentrationDynamic = 2
        ''' <summary>Dynamic constant-volume diafiltration with Hermia pore-blocking flux decline.</summary>
        DiafiltrationDynamic = 3
    End Enum

    ''' <summary>
    ''' Crossflow ultrafiltration / diafiltration unit. Splits an inlet (plus an optional diafiltration-
    ''' buffer inlet) into a concentrated Retentate outlet and a Permeate outlet using per-compound
    ''' sieving coefficients Ïƒáµ¢ âˆˆ [0, 1] (0 = fully retained, 1 = freely permeable).
    '''
    ''' Two operating modes are supported:
    '''   Concentration                - retentate volume = feed volume / VCF;  m_ret_i / m_feed_i = VCF^(âˆ’Ïƒáµ¢)
    '''   DiafiltrationConstantVolume  - retentate volume is held constant; m_ret_i / m_feed_i = exp(âˆ’NÂ·(1âˆ’Ïƒáµ¢))
    ''' </summary>
    <System.Serializable()> Public Partial Class UnitOp_CrossflowUF

        Inherits UnitOperations.UnitOpBaseClass

        Implements IExternalUnitOperation
        Public ReadOnly Property IsBio As Boolean = True

        Public Overrides Property ObjectClass As SimulationObjectClass
            Get
                Return SimulationObjectClass.Separators
            End Get
            Set(value As SimulationObjectClass)
                MyBase.ObjectClass = value
            End Set
        End Property

        ''' <summary>Gets or sets the display name for this unit operation.</summary>
        Public Overrides Property ComponentName As String = GetDisplayName()

        ''' <summary>Gets or sets the display description for this unit operation.</summary>
        Public Overrides Property ComponentDescription As String = GetDisplayDescription()

        ' ----------- INPUT PROPERTIES -----------

        ''' <summary>Selected operating mode.</summary>
        Public Property OperatingMode As CrossflowUFMode = CrossflowUFMode.Concentration

        ''' <summary>Volume concentration factor (feed volume / retentate volume). Used in Concentration mode.</summary>
        Public Property VCF As Double = 5.0

        ''' <summary>Number of diavolumes of buffer exchanged. Used in DiafiltrationConstantVolume mode.</summary>
        Public Property Diavolumes As Double = 5.0

        ''' <summary>
        ''' Per-compound sieving coefficients Ïƒáµ¢ âˆˆ [0, 1]. Compounds absent from this dictionary
        ''' use DefaultSievingCoefficient (default 1.0 = freely permeable).
        ''' </summary>
        Public Property SievingCoefficients As Dictionary(Of String, Double)

        ''' <summary>Default sieving coefficient for compounds not listed in SievingCoefficients.</summary>
        Public Property DefaultSievingCoefficient As Double = 1.0

        ''' <summary>Permeate flux through the membrane (kg/mÂ²/s). If > 0 the required membrane area is reported.</summary>
        Public Property MembraneFlux_kgm2s As Double = 0.02 ' ~72 LMH of water

        ''' <summary>Transmembrane pressure (Pa). Reported only; not used in the flux calculation.</summary>
        Public Property TMP_Pa As Double = 100000.0

        ''' <summary>Fouling half-life (s). Hermia cake-filtration decay J(t) = J0 / (1 + t/Ï„). Set â‰¤ 0 to disable (no decline).</summary>
        Public Property FoulingHalfLife_s As Double = 0.0

        ''' <summary>Membrane area (mÂ²) used only in dynamic modes. If â‰¤ 0 it is auto-sized from J0 and retentate flow.</summary>
        Public Property MembraneArea_m2 As Double = 10.0

        ''' <summary>Last dynamic-mode trajectory (populated by Calculate in dynamic modes). Not persisted.</summary>
        <Xml.Serialization.XmlIgnore> <Newtonsoft.Json.JsonIgnore>
        Public Property LastTrajectory As CrossflowUFTrajectoryResult

        ' ----------- RESULT PROPERTIES -----------

        ''' <summary>Feed mass flow (kg/s).</summary>
        Public Property Result_FeedMass_kgs As Double = 0.0

        ''' <summary>Diafiltration-buffer mass flow (kg/s).</summary>
        Public Property Result_BufferMass_kgs As Double = 0.0

        ''' <summary>Retentate mass flow (kg/s).</summary>
        Public Property Result_Retentate_kgs As Double = 0.0

        ''' <summary>Permeate mass flow (kg/s).</summary>
        Public Property Result_Permeate_kgs As Double = 0.0

        ''' <summary>Required membrane area (mÂ²) at the specified flux. 0 if flux â‰¤ 0.</summary>
        Public Property Result_MembraneArea_m2 As Double = 0.0

        ''' <summary>Effective volume concentration factor actually realised.</summary>
        Public Property Result_EffectiveVCF As Double = 0.0

        <NonSerialized> <Xml.Serialization.XmlIgnore> Public f As Object

        Public Overrides ReadOnly Property SupportsDynamicMode As Boolean = False

        Public Overrides ReadOnly Property MobileCompatible As Boolean
            Get
                Return False
            End Get
        End Property

        Public Sub New()
            MyBase.New()
            SievingCoefficients = New Dictionary(Of String, Double)()
        End Sub

        Public Sub New(ByVal name As String, ByVal description As String)
            MyBase.New()
            Me.ComponentName = name
            Me.ComponentDescription = description
            SievingCoefficients = New Dictionary(Of String, Double)()
        End Sub

        Public Overrides Function CloneXML() As Object
            Dim obj As ICustomXMLSerialization = New UnitOp_CrossflowUF()
            obj.LoadData(Me.SaveData)
            Return obj
        End Function

        Public Overrides Function CloneJSON() As Object
            Return Newtonsoft.Json.JsonConvert.DeserializeObject(Of UnitOp_CrossflowUF)(Newtonsoft.Json.JsonConvert.SerializeObject(Me))
        End Function

        ''' <summary>Returns the sieving coefficient for a compound - uses the dict or the default.</summary>
        Public Function SigmaFor(compName As String) As Double
            If SievingCoefficients IsNot Nothing AndAlso SievingCoefficients.ContainsKey(compName) Then
                Return Max(0.0, Min(1.0, SievingCoefficients(compName)))
            End If
            Return Max(0.0, Min(1.0, DefaultSievingCoefficient))
        End Function

        Public Overrides Sub Calculate(Optional ByVal args As Object = Nothing)

            If Not Me.GraphicObject.InputConnectors(0).IsAttached Then
                Throw New Exception("CrossflowUF: Feed stream not connected.")
            End If
            If Me.GraphicObject.OutputConnectors.Count < 2 OrElse
               Not Me.GraphicObject.OutputConnectors(0).IsAttached OrElse
               Not Me.GraphicObject.OutputConnectors(1).IsAttached Then
                Throw New Exception("CrossflowUF: Both Retentate and Permeate outlets must be connected.")
            End If

            Dim feed As MaterialStream =
                DirectCast(FlowSheet.SimulationObjects(Me.GraphicObject.InputConnectors(0).AttachedConnector.AttachedFrom.Name), MaterialStream)
            Dim buffer As MaterialStream = Nothing
            If Me.GraphicObject.InputConnectors.Count > 1 AndAlso Me.GraphicObject.InputConnectors(1).IsAttached Then
                buffer = DirectCast(FlowSheet.SimulationObjects(Me.GraphicObject.InputConnectors(1).AttachedConnector.AttachedFrom.Name), MaterialStream)
            End If

            Dim T_feed As Double = feed.Phases(0).Properties.temperature.GetValueOrDefault
            Dim P_feed As Double = feed.Phases(0).Properties.pressure.GetValueOrDefault
            Dim P_out As Double = P_feed ' Crossflow UF is modeled as isobaric at the feed side; TMP is reported separately

            Dim feedCompMass As New Dictionary(Of String, Double)
            Dim bufCompMass As New Dictionary(Of String, Double)
            Dim m_feed_total As Double = 0.0
            Dim m_buf_total As Double = 0.0

            For Each c In feed.Phases(0).Compounds.Values
                Dim mf = c.MassFraction.GetValueOrDefault * feed.Phases(0).Properties.massflow.GetValueOrDefault
                feedCompMass(c.Name) = mf
                bufCompMass(c.Name) = 0.0
                m_feed_total += mf
            Next
            If buffer IsNot Nothing Then
                For Each c In buffer.Phases(0).Compounds.Values
                    Dim mf = c.MassFraction.GetValueOrDefault * buffer.Phases(0).Properties.massflow.GetValueOrDefault
                    If bufCompMass.ContainsKey(c.Name) Then
                        bufCompMass(c.Name) = mf
                    Else
                        bufCompMass(c.Name) = mf
                        feedCompMass(c.Name) = 0.0
                    End If
                    m_buf_total += mf
                Next
            End If

            Result_FeedMass_kgs = m_feed_total
            Result_BufferMass_kgs = m_buf_total

            Dim retentateMass As New Dictionary(Of String, Double)
            Dim permeateMass As New Dictionary(Of String, Double)
            For Each k In feedCompMass.Keys
                retentateMass(k) = 0.0
                permeateMass(k) = 0.0
            Next
            For Each k In bufCompMass.Keys
                If Not retentateMass.ContainsKey(k) Then retentateMass(k) = 0.0
                If Not permeateMass.ContainsKey(k) Then permeateMass(k) = 0.0
            Next

            Select Case OperatingMode

                Case CrossflowUFMode.Concentration
                    ' Ignore buffer in concentration mode.
                    ' Per compound:  m_ret_i / m_feed_i = VCF^(âˆ’Ïƒáµ¢)
                    Dim vcfEff = Max(VCF, 1.0)
                    For Each k In feedCompMass.Keys
                        Dim Ïƒ = SigmaFor(k)
                        Dim retFrac = Pow(vcfEff, -Ïƒ)
                        retentateMass(k) = feedCompMass(k) * retFrac
                        permeateMass(k) = feedCompMass(k) - retentateMass(k)
                        If permeateMass(k) < 0 Then permeateMass(k) = 0.0
                    Next
                    Result_EffectiveVCF = vcfEff

                Case CrossflowUFMode.DiafiltrationConstantVolume
                    ' Constant-volume DF. For each compound, buffer contribution joins the retentate
                    ' initially, then the CV-DF sieving law removes it:  m_ret / m_in = exp(âˆ’NÂ·(1âˆ’Ïƒ))
                    Dim N = Max(Diavolumes, 0.0)
                    For Each k In retentateMass.Keys
                        Dim Ïƒ = SigmaFor(k)
                        Dim m_in = 0.0
                        If feedCompMass.ContainsKey(k) Then m_in += feedCompMass(k)
                        If bufCompMass.ContainsKey(k) Then m_in += bufCompMass(k)
                        Dim retFrac = Exp(-N * (1.0 - Ïƒ))
                        retentateMass(k) = m_in * retFrac
                        permeateMass(k) = m_in - retentateMass(k)
                    Next
                    Result_EffectiveVCF = 1.0 ' CV-DF keeps V constant

                Case CrossflowUFMode.ConcentrationDynamic
                    CalculateDynamicConcentration(feed, feedCompMass, retentateMass, permeateMass)

                Case CrossflowUFMode.DiafiltrationDynamic
                    CalculateDynamicDiafiltration(feed, buffer, feedCompMass, bufCompMass, retentateMass, permeateMass)

            End Select

            Dim m_ret_total As Double = 0.0
            Dim m_perm_total As Double = 0.0
            For Each v In retentateMass.Values : m_ret_total += v : Next
            For Each v In permeateMass.Values : m_perm_total += v : Next
            Result_Retentate_kgs = m_ret_total
            Result_Permeate_kgs = m_perm_total

            ' Required membrane area: A = m_permeate / flux
            If MembraneFlux_kgm2s > 0.0 Then
                Result_MembraneArea_m2 = m_perm_total / MembraneFlux_kgm2s
            Else
                Result_MembraneArea_m2 = 0.0
            End If

            ' ----------- Push to outlets -----------
            Dim retConn = Me.GraphicObject.OutputConnectors(0)
            Dim permConn = Me.GraphicObject.OutputConnectors(1)

            If retConn.IsAttached Then
                Dim ms As MaterialStream = FlowSheet.SimulationObjects(retConn.AttachedConnector.AttachedTo.Name)
                WriteStream(ms, retentateMass, m_ret_total, T_feed, P_out)
            End If
            If permConn.IsAttached Then
                Dim ms As MaterialStream = FlowSheet.SimulationObjects(permConn.AttachedConnector.AttachedTo.Name)
                WriteStream(ms, permeateMass, m_perm_total, T_feed, P_out)
            End If

        End Sub

        ''' <summary>
        ''' Dynamic batch concentration with Hermia cake-filtration flux decline.
        ''' J(t) = J0 / (1 + t/Ï„). Feed volume V0 is concentrated to V0/VCF. Per-compound retentate
        ''' mass evolves as dm_i/dt = -(1-Ïƒ_i) Â· J(t) Â· A Â· c_i(t) (for sieving fraction through the membrane).
        ''' We integrate with simple explicit Euler; 500 samples default (cap 2000).
        ''' Populates LastTrajectory and writes retentate / permeate mass dictionaries.
        ''' </summary>
        Private Sub CalculateDynamicConcentration(feed As MaterialStream,
                                                  feedCompMass As Dictionary(Of String, Double),
                                                  ByRef retentateMass As Dictionary(Of String, Double),
                                                  ByRef permeateMass As Dictionary(Of String, Double))

            Dim rho_L = feed.Phases(0).Properties.density.GetValueOrDefault
            If rho_L <= 0.0 Then rho_L = 1000.0
            Dim m_feed = 0.0
            For Each mv In feedCompMass.Values : m_feed += mv : Next
            Dim V0_m3 = m_feed / Max(rho_L, 0.000000000001) ' volumetric feed rate (m3/s) treated as a batch charge over 1s? We use a nominal 1 s batch basis: total inventory
            ' Treat feedCompMass as the batch inventory (kg). Convert to concentrations (g/L):
            '   V0 = m_feed / rho  (m3) ;  c_i0 = feedCompMass_i / V0 * 1000.0 (g/L treating kg/m3 == g/L)
            Dim V0 = V0_m3
            If V0 <= 0.0 Then V0 = 1.0

            Dim VCFtarget = Max(VCF, 1.0)
            Dim Vfinal = V0 / VCFtarget
            Dim J0 = Max(MembraneFlux_kgm2s, 0.000000000001)   ' kg/m2/s (water-equiv)
            Dim A = If(MembraneArea_m2 > 0.0, MembraneArea_m2, 10.0)
            Dim tau = If(FoulingHalfLife_s > 0.0, FoulingHalfLife_s, Double.PositiveInfinity)

            ' Per-compound state (mass kg, in the retentate)
            Dim m_i As New Dictionary(Of String, Double)
            For Each k In feedCompMass.Keys
                m_i(k) = feedCompMass(k)
            Next

            ' Integrate dV/dt = -J(t)Â·A/rho  (kg/m2/s * m2 / (kg/m3) = m3/s)
            Dim V As Double = V0
            Dim t As Double = 0.0
            Dim dt As Double = 1.0   ' initial step, seconds
            Dim steps As Integer = 0
            Dim maxSteps As Integer = 200000
            Dim traj As New CrossflowUFTrajectoryResult() With {.Mode = "ConcentrationDynamic"}
            LastTrajectory = traj
            For Each k In feedCompMass.Keys
                traj.Concentrations(k) = New List(Of Double)()
            Next
            Dim sampleInterval As Integer = 1
            Dim maxSamples As Integer = 2000
            Dim recordSample = Sub()
                                   traj.Times.Add(t)
                                   Dim Jt = J0 / (1.0 + If(Double.IsPositiveInfinity(tau), 0.0, t / tau))
                                   traj.J.Add(Jt)
                                   traj.V_ret.Add(V)
                                   traj.VCF_instant.Add(If(V > 0.0, V0 / V, VCFtarget))
                                   traj.Diavolumes.Add(0.0)
                                   For Each k In m_i.Keys
                                       Dim c_gL = If(V > 0.0, (m_i(k) / V), 0.0)   ' kg/m3 == g/L
                                       traj.Concentrations(k).Add(c_gL)
                                   Next
                               End Sub
            recordSample()

            While V > Vfinal AndAlso steps < maxSteps
                Dim Jt = J0 / (1.0 + If(Double.IsPositiveInfinity(tau), 0.0, t / tau))
                Dim dVdt = -Jt * A / rho_L    ' m3/s
                If dVdt >= 0.0 Then Exit While
                ' choose a step that removes at most 1% of remaining volume
                Dim h = Min(dt, (V - Vfinal) * 0.5 / Max(Math.Abs(dVdt), 0.000000000001))
                If h <= 0.0 Then Exit While
                ' Update per-compound mass (permeate flux draws Ïƒ_i fraction)
                For Each k In m_i.Keys.ToList()
                    Dim Ïƒ = SigmaFor(k)
                    Dim c_gL = If(V > 0.0, (m_i(k) / V), 0.0) ' kg/m3
                    Dim dm = -Ïƒ * Jt * A * c_gL / Max(rho_L, 0.000000000001) ' simplified: ÏƒÂ·JÂ·AÂ·c(mass/vol ratio)
                    ' proper: dm/dt = -ÏƒÂ·JÂ·AÂ·c with c in kg/m3 and J in kg/m2/s, but flux J is typically water-based.
                    ' Here we interpret JÂ·A/rho = dV/dt and Ïƒ governs solute passage:  dm = -ÏƒÂ·cÂ·|dV|
                    dm = -Ïƒ * c_gL * Math.Abs(dVdt) * h
                    m_i(k) = Max(m_i(k) + dm, 0.0)
                Next
                V += dVdt * h
                t += h
                steps += 1
                If (steps Mod sampleInterval = 0) AndAlso traj.Times.Count < maxSamples Then
                    recordSample()
                    If traj.Times.Count >= maxSamples \ 2 Then sampleInterval = Max(sampleInterval, sampleInterval * 2)
                End If
                dt = Math.Min(dt * 1.1, 3600.0)
            End While

            ' Final sample
            recordSample()

            ' Fill dictionaries
            For Each k In feedCompMass.Keys
                retentateMass(k) = m_i(k)
                permeateMass(k) = Max(feedCompMass(k) - m_i(k), 0.0)
            Next
            Result_EffectiveVCF = If(V > 0.0, V0 / V, VCFtarget)

        End Sub

        ''' <summary>
        ''' Dynamic constant-volume diafiltration with Hermia flux decline. V is held constant;
        ''' fresh buffer replaces permeate volume. Compound mass decays as dm/dt = -(1-Ïƒ)Â·(JÂ·A)Â·cÂ·(1/rho).
        ''' </summary>
        Private Sub CalculateDynamicDiafiltration(feed As MaterialStream, buffer As MaterialStream,
                                                  feedCompMass As Dictionary(Of String, Double),
                                                  bufCompMass As Dictionary(Of String, Double),
                                                  ByRef retentateMass As Dictionary(Of String, Double),
                                                  ByRef permeateMass As Dictionary(Of String, Double))

            Dim rho_L = feed.Phases(0).Properties.density.GetValueOrDefault
            If rho_L <= 0.0 Then rho_L = 1000.0
            Dim m_feed = 0.0
            For Each mv In feedCompMass.Values : m_feed += mv : Next
            Dim V = m_feed / Max(rho_L, 0.000000000001)   ' held constant
            If V <= 0.0 Then V = 1.0

            Dim J0 = Max(MembraneFlux_kgm2s, 0.000000000001)
            Dim A = If(MembraneArea_m2 > 0.0, MembraneArea_m2, 10.0)
            Dim tau = If(FoulingHalfLife_s > 0.0, FoulingHalfLife_s, Double.PositiveInfinity)
            Dim Ntarget = Max(Diavolumes, 0.0)

            Dim m_i As New Dictionary(Of String, Double)
            For Each k In feedCompMass.Keys
                m_i(k) = feedCompMass(k) + If(bufCompMass.ContainsKey(k), bufCompMass(k), 0.0)
            Next

            Dim traj As New CrossflowUFTrajectoryResult() With {.Mode = "DiafiltrationDynamic"}
            LastTrajectory = traj
            For Each k In m_i.Keys
                traj.Concentrations(k) = New List(Of Double)()
            Next

            Dim t As Double = 0.0
            Dim dv As Double = 0.0   ' diavolumes swept = cumulative permeate volume / V
            Dim dt As Double = 1.0
            Dim steps As Integer = 0
            Dim maxSteps As Integer = 200000
            Dim sampleInterval As Integer = 1
            Dim maxSamples As Integer = 2000

            Dim recordSample = Sub()
                                   traj.Times.Add(t)
                                   Dim Jt = J0 / (1.0 + If(Double.IsPositiveInfinity(tau), 0.0, t / tau))
                                   traj.J.Add(Jt)
                                   traj.V_ret.Add(V)
                                   traj.VCF_instant.Add(1.0)
                                   traj.Diavolumes.Add(dv)
                                   For Each k In m_i.Keys
                                       traj.Concentrations(k).Add(m_i(k) / V)
                                   Next
                               End Sub
            recordSample()

            While dv < Ntarget AndAlso steps < maxSteps
                Dim Jt = J0 / (1.0 + If(Double.IsPositiveInfinity(tau), 0.0, t / tau))
                Dim dVol_dt = Jt * A / rho_L  ' m3/s permeate
                If dVol_dt <= 0.0 Then Exit While
                Dim h = Math.Min(dt, (Ntarget - dv) * V / dVol_dt)
                If h <= 0.0 Then Exit While
                For Each k In m_i.Keys.ToList()
                    Dim Ïƒ = SigmaFor(k)
                    Dim c = m_i(k) / V
                    Dim dm = -Ïƒ * c * dVol_dt * h
                    m_i(k) = Max(m_i(k) + dm, 0.0)
                Next
                t += h
                dv += dVol_dt * h / V
                steps += 1
                If (steps Mod sampleInterval = 0) AndAlso traj.Times.Count < maxSamples Then
                    recordSample()
                    If traj.Times.Count >= maxSamples \ 2 Then sampleInterval = Max(sampleInterval, sampleInterval * 2)
                End If
                dt = Math.Min(dt * 1.1, 3600.0)
            End While

            recordSample()

            Dim totalInputMass As Double = 0.0
            For Each k In m_i.Keys
                Dim m_in = If(feedCompMass.ContainsKey(k), feedCompMass(k), 0.0) + If(bufCompMass.ContainsKey(k), bufCompMass(k), 0.0)
                retentateMass(k) = m_i(k)
                permeateMass(k) = Max(m_in - m_i(k), 0.0)
                totalInputMass += m_in
            Next
            Result_EffectiveVCF = 1.0

        End Sub

        Private Shared Sub WriteStream(ms As MaterialStream, compMass As Dictionary(Of String, Double),
                                       total As Double, T_K As Double, P_Pa As Double)
            With ms
                .ClearAllProps()
                .Phases(0).Properties.temperature = T_K
                .Phases(0).Properties.pressure = P_Pa
                If total > 0 Then
                    For Each comp In .Phases(0).Compounds.Values
                        Dim mv = 0.0
                        If compMass.ContainsKey(comp.Name) Then mv = compMass(comp.Name)
                        comp.MassFraction = mv / total
                    Next
                    Dim invMWsum As Double = 0.0
                    For Each comp In .Phases(0).Compounds.Values
                        invMWsum += comp.MassFraction.GetValueOrDefault / comp.ConstantProperties.Molar_Weight
                    Next
                    If invMWsum > 0 Then
                        For Each comp In .Phases(0).Compounds.Values
                            comp.MoleFraction = (comp.MassFraction.GetValueOrDefault / comp.ConstantProperties.Molar_Weight) / invMWsum
                        Next
                    End If
                End If
                .Phases(0).Properties.massflow = total
                .DefinedFlow = FlowSpec.Mass
                .SpecType = StreamSpec.Temperature_and_Pressure
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
                        .Phases(0).Properties.enthalpy = Nothing
                        For Each c In .Phases(0).Compounds.Values
                            c.MoleFraction = 0
                            c.MassFraction = 0
                        Next
                        .Phases(0).Properties.massflow = Nothing
                        .GraphicObject.Calculated = False
                    End With
                End If
            Next
        End Sub

        Public Overrides Function GetIconBitmapBytes() As Byte()
            Return BioOpsDrawHelper.RenderIconToPngBytes(64, 64, AddressOf DrawIcon)
        End Function

        Public Overrides Function GetDisplayDescription() As String
            Return "Crossflow UF/DF (sieving-coefficient membrane separator)"
        End Function

        Public Overrides Function GetDisplayName() As String
            Return "Crossflow UF/DF"
        End Function

        Public Overrides Function GetReport(su As IUnitsOfMeasure, ci As Globalization.CultureInfo, numberformat As String) As String

            Dim str As New Text.StringBuilder
            str.AppendLine("CrossflowUF:  " & Me.GraphicObject.Tag)
            str.AppendLine("Property Package: " & Me.PropertyPackage.ComponentName)
            str.AppendLine()
            str.AppendLine("Configuration")
            str.AppendLine("    Mode:                 " & OperatingMode.ToString())
            If OperatingMode = CrossflowUFMode.Concentration Then
                str.AppendLine("    VCF (target):         " & VCF.ToString(numberformat, ci))
            Else
                str.AppendLine("    Diavolumes:           " & Diavolumes.ToString(numberformat, ci))
            End If
            str.AppendLine("    Default Ïƒ:            " & DefaultSievingCoefficient.ToString(numberformat, ci))
            str.AppendLine("    Membrane Flux:        " & MembraneFlux_kgm2s.ToString(numberformat, ci) & " kg/mÂ²/s")
            str.AppendLine()
            str.AppendLine("Results")
            str.AppendLine("    Feed Mass Flow:       " & Result_FeedMass_kgs.ToString(numberformat, ci) & " kg/s")
            str.AppendLine("    DF Buffer Flow:       " & Result_BufferMass_kgs.ToString(numberformat, ci) & " kg/s")
            str.AppendLine("    Retentate Flow:       " & Result_Retentate_kgs.ToString(numberformat, ci) & " kg/s")
            str.AppendLine("    Permeate Flow:        " & Result_Permeate_kgs.ToString(numberformat, ci) & " kg/s")
            str.AppendLine("    Effective VCF:        " & Result_EffectiveVCF.ToString(numberformat, ci))
            str.AppendLine("    Membrane Area:        " & Result_MembraneArea_m2.ToString(numberformat, ci) & " mÂ²")
            Return str.ToString()

        End Function

        Private Shared ReadOnly _inputProps As String() = {
            "Operating Mode",
            "VCF",
            "Diavolumes",
            "Default Sieving Coefficient",
            "Membrane Flux",
            "Transmembrane Pressure"
        }

        Private Shared ReadOnly _outputProps As String() = {
            "Feed Mass Flow",
            "DF Buffer Mass Flow",
            "Retentate Mass Flow",
            "Permeate Mass Flow",
            "Effective VCF",
            "Membrane Area"
        }

        Public Overrides Function GetProperties(proptype As PropertyType) As String()
            Dim baseprops = MyBase.GetProperties(proptype)
            Select Case proptype
                Case PropertyType.WR : Return _inputProps
                Case PropertyType.RO : Return _outputProps
                Case Else : Return _inputProps.Concat(_outputProps).Concat(baseprops).ToArray()
            End Select
        End Function

        Public Overrides Function GetPropertyValue(prop As String, Optional su As IUnitsOfMeasure = Nothing) As Object
            Select Case prop
                Case "Operating Mode" : Return OperatingMode.ToString()
                Case "VCF" : Return VCF
                Case "Diavolumes" : Return Diavolumes
                Case "Default Sieving Coefficient" : Return DefaultSievingCoefficient
                Case "Membrane Flux" : Return MembraneFlux_kgm2s
                Case "Transmembrane Pressure" : Return TMP_Pa
                Case "Feed Mass Flow" : Return Result_FeedMass_kgs
                Case "DF Buffer Mass Flow" : Return Result_BufferMass_kgs
                Case "Retentate Mass Flow" : Return Result_Retentate_kgs
                Case "Permeate Mass Flow" : Return Result_Permeate_kgs
                Case "Effective VCF" : Return Result_EffectiveVCF
                Case "Membrane Area" : Return Result_MembraneArea_m2
                Case Else : Return MyBase.GetPropertyValue(prop, su)
            End Select
        End Function

        Public Overrides Function GetPropertyUnit(prop As String, Optional su As IUnitsOfMeasure = Nothing) As String
            Select Case prop
                Case "VCF", "Diavolumes", "Default Sieving Coefficient", "Effective VCF" : Return "-"
                Case "Membrane Flux" : Return "kg/m2/s"
                Case "Transmembrane Pressure" : Return "Pa"
                Case "Feed Mass Flow",
                     "DF Buffer Mass Flow",
                     "Retentate Mass Flow",
                     "Permeate Mass Flow" : Return "kg/s"
                Case "Membrane Area" : Return "m2"
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
                Case "Operating Mode"
                    Dim m As CrossflowUFMode
                    If [Enum].TryParse(Of CrossflowUFMode)(propval?.ToString(), m) Then OperatingMode = m
                    Return True
                Case "VCF" : VCF = d : Return True
                Case "Diavolumes" : Diavolumes = d : Return True
                Case "Default Sieving Coefficient" : DefaultSievingCoefficient = d : Return True
                Case "Membrane Flux" : MembraneFlux_kgm2s = d : Return True
                Case "Transmembrane Pressure" : TMP_Pa = d : Return True
                Case Else : Return MyBase.SetPropertyValue(prop, propval, su)
            End Select
        End Function

        ' ======================================================================
        ' IExternalUnitOperation
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
                Return "UF-"
            End Get
        End Property

        Public Function ReturnInstance(typename As String) As Object Implements IExternalUnitOperation.ReturnInstance
            Return New UnitOp_CrossflowUF()
        End Function

        Public Sub PopulateEditorPanel(ctner As Object) Implements IExternalUnitOperation.PopulateEditorPanel

            If TypeOf ctner Is AvaloniaEditorPanel Then PopulateEditorPanelAvalonia(DirectCast(ctner, AvaloniaEditorPanel)) : Return
        End Sub

        Private Sub PopulateEditorPanelAvalonia(container As AvaloniaEditorPanel)

            Dim nf = FlowSheet.FlowsheetOptions.NumberFormat

            container.CreateAndAddLabelRow("Operating Mode")

            container.CreateAndAddDropDownRow("Mode",
                                              New List(Of String)({"Concentration (batch)", "Diafiltration (const. volume)", "Concentration (dynamic, Hermia)", "Diafiltration (dynamic, Hermia)"}),
                                              CInt(OperatingMode),
                                              Sub(dd, e)
                                                  OperatingMode = CType(dd.SelectedIndex, CrossflowUFMode)
                                                  FlowSheet.RequestCalculation()
                                              End Sub)

            container.CreateAndAddLabelRow("Process Setpoints")

            container.CreateAndAddTextBoxRow(nf, "Volumetric Concentration Factor (VCF)", VCF,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     VCF = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Number of Diavolumes (N)", Diavolumes,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     Diavolumes = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Default Sieving Coefficient (0-1)", DefaultSievingCoefficient,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     DefaultSievingCoefficient = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddLabelRow("Membrane")

            container.CreateAndAddTextBoxRow(nf, "Membrane Area (m2)", MembraneArea_m2,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     MembraneArea_m2 = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Membrane Flux J0 (kg/m2Â·s)", MembraneFlux_kgm2s,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     MembraneFlux_kgm2s = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Transmembrane Pressure TMP (Pa)", TMP_Pa,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     TMP_Pa = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Fouling Half-Life t (s, 0 = no fouling)", FoulingHalfLife_s,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     FoulingHalfLife_s = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

        End Sub

        Public Sub CreateConnectors() Implements IExternalUnitOperation.CreateConnectors

            If GraphicObject Is Nothing Then Return

            Dim w = GraphicObject.Width
            Dim h = GraphicObject.Height
            Dim gx = GraphicObject.X
            Dim gy = GraphicObject.Y

            If GraphicObject.InputConnectors.Count = 2 AndAlso GraphicObject.OutputConnectors.Count = 2 Then

                GraphicObject.InputConnectors(0).Position = New Point(gx, gy + 0.4 * h)
                GraphicObject.InputConnectors(0).ConnectorName = "Feed"
                GraphicObject.InputConnectors(1).Position = New Point(gx + 0.25 * w, gy)
                GraphicObject.InputConnectors(1).ConnectorName = "DF Buffer (Optional)"
                GraphicObject.InputConnectors(1).Direction = ConDir.Down

                GraphicObject.OutputConnectors(0).Position = New Point(gx + w, gy + 0.4 * h)
                GraphicObject.OutputConnectors(0).ConnectorName = "Retentate"
                GraphicObject.OutputConnectors(1).Position = New Point(gx + 0.75 * w, gy + h)
                GraphicObject.OutputConnectors(1).ConnectorName = "Permeate"
                GraphicObject.OutputConnectors(1).Direction = ConDir.Up

            Else

                GraphicObject.InputConnectors.Clear()
                GraphicObject.OutputConnectors.Clear()

                GraphicObject.InputConnectors.Add(New ConnectionPoint With {
                    .Position = New Point(gx, gy + 0.4 * h),
                    .Type = ConType.ConIn,
                    .Direction = ConDir.Right,
                    .ConnectorName = "Feed"
                })
                GraphicObject.InputConnectors.Add(New ConnectionPoint With {
                    .Position = New Point(gx + 0.25 * w, gy),
                    .Type = ConType.ConIn,
                    .Direction = ConDir.Down,
                    .ConnectorName = "DF Buffer (Optional)"
                })
                GraphicObject.OutputConnectors.Add(New ConnectionPoint With {
                    .Position = New Point(gx + w, gy + 0.4 * h),
                    .Type = ConType.ConOut,
                    .Direction = ConDir.Right,
                    .ConnectorName = "Retentate"
                })
                GraphicObject.OutputConnectors.Add(New ConnectionPoint With {
                    .Position = New Point(gx + 0.75 * w, gy + h),
                    .Type = ConType.ConOut,
                    .Direction = ConDir.Up,
                    .ConnectorName = "Permeate"
                })
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
                    "crossflow_uf_photo", _photoImage) Then Return
            End If

            DrawIcon(canvas, CSng(GraphicObject.X), CSng(GraphicObject.Y),
                     CSng(GraphicObject.Width), CSng(GraphicObject.Height),
                     GraphicObject.DrawMode = 1)

        End Sub

        Private Shared Sub DrawIcon(canvas As SKCanvas, gx As Single, gy As Single, w As Single, h As Single, Optional mono As Boolean = False)
            ' Crossflow UF skid: three parallel horizontal membrane housings on a frame, common feed manifold left + retentate right.
            Dim skid As New SKRect(gx + 0.05F * w, gy + 0.88F * h, gx + 0.95F * w, gy + h)
            BioOpsDrawHelper.DrawSkid(canvas, skid, mono)
            ' three stacked membrane tubes
            Dim tubes = 3
            Dim tubeLeft = gx + 0.22F * w
            Dim tubeRight = gx + 0.82F * w
            Dim startY = gy + 0.15F * h
            Dim span = (0.88F - 0.15F) * h
            Dim tubeH = span / (tubes * 1.5F)
            For i = 0 To tubes - 1
                Dim ty = startY + i * (span / tubes)
                Dim rect As New SKRect(tubeLeft, ty, tubeRight, ty + tubeH)
                BioOpsDrawHelper.DrawHorizontalTank(canvas, rect, mono)
                ' flanges on both ends
                BioOpsDrawHelper.DrawFlange(canvas, tubeLeft, (rect.Top + rect.Bottom) * 0.5F, rect.Height * 1.3F, mono)
                BioOpsDrawHelper.DrawFlange(canvas, tubeRight, (rect.Top + rect.Bottom) * 0.5F, rect.Height * 1.3F, mono)
            Next
            ' common feed manifold (left vertical pipe)
            BioOpsDrawHelper.DrawPipe(canvas, New SKPoint(gx + 0.12F * w, startY), New SKPoint(gx + 0.12F * w, startY + span * 0.85F), 0.04F * w, mono)
            BioOpsDrawHelper.DrawPipe(canvas, New SKPoint(gx + 0.02F * w, startY + span * 0.4F), New SKPoint(gx + 0.12F * w, startY + span * 0.4F), 0.04F * h, mono)
            ' connector stubs to each tube
            Using s As New SKPaint With {.Color = If(mono, New SKColor(30, 30, 30), New SKColor(50, 65, 85)), .Style = SKPaintStyle.Stroke, .StrokeWidth = 1.0F, .IsAntialias = True}
                For i = 0 To tubes - 1
                    Dim ty = startY + i * (span / tubes) + tubeH * 0.5F
                    canvas.DrawLine(gx + 0.12F * w, ty, tubeLeft, ty, s)
                Next
            End Using
            ' retentate manifold right
            BioOpsDrawHelper.DrawPipe(canvas, New SKPoint(gx + 0.88F * w, startY), New SKPoint(gx + 0.88F * w, startY + span * 0.85F), 0.04F * w, mono)
            BioOpsDrawHelper.DrawPipe(canvas, New SKPoint(gx + 0.88F * w, startY + span * 0.4F), New SKPoint(gx + 0.98F * w, startY + span * 0.4F), 0.04F * h, mono)
            ' control panel
            Using p As New SKPaint With {.Color = If(mono, New SKColor(60, 60, 60), New SKColor(70, 85, 105)), .IsAntialias = True}
                canvas.DrawRect(New SKRect(gx + 0.4F * w, gy + 0.02F * h, gx + 0.6F * w, gy + 0.12F * h), p)
            End Using
            Using s As New SKPaint With {.Color = If(mono, New SKColor(30, 30, 30), New SKColor(50, 65, 85)), .Style = SKPaintStyle.Stroke, .StrokeWidth = 1.1F, .IsAntialias = True}
                canvas.DrawRect(New SKRect(gx + 0.4F * w, gy + 0.02F * h, gx + 0.6F * w, gy + 0.12F * h), s)
            End Using
            BioOpsDrawHelper.DrawGauge(canvas, gx + 0.45F * w, gy + 0.07F * h, 0.02F * w, mono)
            BioOpsDrawHelper.DrawGauge(canvas, gx + 0.55F * w, gy + 0.07F * h, 0.02F * w, mono)
        End Sub

        Private Shared Sub DrawIconLegacy(canvas As SKCanvas, gx As Single, gy As Single, w As Single, h As Single)

            ' Horizontal membrane cassette: outer shell + central membrane band, feed left, retentate right, permeate down
            Dim shell As New SKPaint With {
                .Color = New SKColor(230, 235, 245),
                .Style = SKPaintStyle.Fill,
                .IsAntialias = True
            }
            Dim stroke As New SKPaint With {
                .Color = New SKColor(40, 60, 100),
                .Style = SKPaintStyle.Stroke,
                .StrokeWidth = 1.8F,
                .IsAntialias = True
            }
            Dim band As New SKPaint With {
                .Color = New SKColor(120, 170, 210, 200),
                .Style = SKPaintStyle.Fill,
                .IsAntialias = True
            }
            Dim permeatePaint As New SKPaint With {
                .Color = New SKColor(170, 210, 240, 200),
                .Style = SKPaintStyle.Fill,
                .IsAntialias = True
            }
            Dim accent As New SKPaint With {
                .Color = New SKColor(60, 90, 140),
                .Style = SKPaintStyle.Stroke,
                .StrokeWidth = 1.2F,
                .IsAntialias = True
            }

            ' Outer cassette rectangle
            Dim outer As New SKRect(gx + 0.08F * w, gy + 0.2F * h, gx + 0.92F * w, gy + 0.8F * h)
            canvas.DrawRect(outer, shell)
            canvas.DrawRect(outer, stroke)

            ' Feed channel (top band)
            Dim feedBand As New SKRect(outer.Left, outer.Top + 0.05F * h, outer.Right, outer.Top + 0.2F * h)
            canvas.DrawRect(feedBand, band)

            ' Membrane line
            canvas.DrawLine(outer.Left, outer.Top + 0.2F * h, outer.Right, outer.Top + 0.2F * h, accent)

            ' Permeate chamber (bottom band)
            Dim permBand As New SKRect(outer.Left, outer.Top + 0.2F * h, outer.Right, outer.Bottom)
            canvas.DrawRect(permBand, permeatePaint)

            ' Membrane hatching
            Dim nh = 8
            For i = 1 To nh - 1
                Dim xLine = outer.Left + i * (outer.Right - outer.Left) / nh
                canvas.DrawLine(xLine, outer.Top + 0.2F * h - 0.02F * h,
                                xLine, outer.Top + 0.2F * h + 0.02F * h, accent)
            Next

            ' Small permeate droplets
            Dim droplet As New SKPaint With {
                .Color = New SKColor(200, 230, 250),
                .Style = SKPaintStyle.Fill,
                .IsAntialias = True
            }
            canvas.DrawCircle(gx + 0.35F * w, gy + 0.65F * h, 0.02F * w, droplet)
            canvas.DrawCircle(gx + 0.55F * w, gy + 0.55F * h, 0.015F * w, droplet)
            canvas.DrawCircle(gx + 0.7F * w, gy + 0.6F * h, 0.018F * w, droplet)

            ' Feed, retentate, permeate short stubs
            canvas.DrawLine(gx, gy + 0.4F * h, outer.Left, gy + 0.4F * h, stroke)
            canvas.DrawLine(outer.Right, gy + 0.4F * h, gx + w, gy + 0.4F * h, stroke)
            canvas.DrawLine(gx + 0.75F * w, outer.Bottom, gx + 0.75F * w, gy + h, stroke)
            ' DF buffer stub
            canvas.DrawLine(gx + 0.25F * w, gy, gx + 0.25F * w, outer.Top, stroke)

            shell.Dispose()
            stroke.Dispose()
            band.Dispose()
            permeatePaint.Dispose()
            accent.Dispose()
            droplet.Dispose()

        End Sub

    End Class

End Namespace
