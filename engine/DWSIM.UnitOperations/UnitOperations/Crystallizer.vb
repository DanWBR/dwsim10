'    Crystallizer (cooling / evaporative / antisolvent)
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

    Public Enum CrystallizerMode
        Cooling = 0
        Evaporative = 1
        Antisolvent = 2
    End Enum

    ''' <summary>
    ''' Crystallizer (cooling / evaporative / antisolvent). Splits the inlet between a Crystals outlet
    ''' and a Mother-Liquor outlet. Solubility of the target solute in the solvent is described by a
    ''' modified Apelblat/Van't-Hoff form:
    '''   C_sat(T)  [g solute / g solvent]  =  A + B*(T - 298) + C*(T - 298)^2
    ''' At steady state, mass crystallized = max(0, m_solute_in - C_sat * m_solvent_in).
    ''' In Evaporative mode the solvent mass is reduced by EvaporationFraction before the check. The
    ''' evaporated solvent leaves through the optional Vapor outlet (port 2) as vapor at the operating
    ''' temperature, at the feed pressure or, when that is above the solvent vapor pressure at the
    ''' operating temperature, at that vapor pressure (saturated vapor: the vessel runs under the
    ''' vacuum at which the solvent boils at the operating temperature). With no Vapor stream connected
    ''' the evaporated solvent stays in the Mother Liquor outlet, which closes the mass balance, and a
    ''' warning is shown; the crystallized mass is the same in both cases.
    ''' In Antisolvent mode, an additional stream adds solvent that "dilutes" the solubility by a
    ''' user-set factor (SolubilityReductionByAntisolvent, 0 to 1).
    ''' The heat duty is the enthalpy balance of the streams, sum of the outlets minus sum of the
    ''' inlets, with the outlet enthalpies from flashes at the outlet conditions; in Evaporative mode it
    ''' includes the latent heat of the evaporated solvent. It is reported in Result_Duty_kW (positive =
    ''' heat added).
    ''' </summary>
    <System.Serializable()> Public Partial Class UnitOp_Crystallizer

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

        ''' <summary>Gets or sets the display name for this unit operation.</summary>
        Public Overrides Property ComponentName As String = GetDisplayName()

        ''' <summary>Gets or sets the display description for this unit operation.</summary>
        Public Overrides Property ComponentDescription As String = GetDisplayDescription()

        ''' <summary>Gets or sets the crystallization mode: Cooling (0) solves at <see cref="OperatingT_K"/>; Evaporative (1) solves at <see cref="OperatingT_K"/> after removing <see cref="EvaporationFraction"/> of the solvent; Antisolvent (2) keeps the feed temperature and reduces the solubility by <see cref="SolubilityReductionByAntisolvent"/>. Default Cooling.</summary>
        Public Property Mode As CrystallizerMode = CrystallizerMode.Cooling
        ''' <summary>Gets or sets the name of the solute compound that crystallizes. Required.</summary>
        Public Property SoluteCompound As String = ""
        ''' <summary>Gets or sets the name of the solvent compound used as the basis of the solubility. Required. Default "Water".</summary>
        Public Property SolventCompound As String = "Water"
        ''' <summary>Gets or sets the operating temperature in Cooling and Evaporative modes, in K. Both outlets leave at this temperature. Default 278.15.</summary>
        Public Property OperatingT_K As Double = 278.15 ' 5 C for cooling
        ''' <summary>Gets or sets the constant term A of the solubility correlation C_sat = A + B(T - 298.15) + C(T - 298.15)^2, in g solute per g solvent. Default 0.35.</summary>
        Public Property Sol_A As Double = 0.35
        ''' <summary>Gets or sets the linear coefficient B of the solubility correlation, in g solute per g solvent per K. Default 0.005.</summary>
        Public Property Sol_B As Double = 0.005
        ''' <summary>Gets or sets the quadratic coefficient C of the solubility correlation, in g solute per g solvent per K2. Default 0.</summary>
        Public Property Sol_C As Double = 0.0
        ''' <summary>Gets or sets the fraction (0-1) of the solvent evaporated in Evaporative mode. The evaporated solvent leaves through the Vapor outlet, or stays in the Mother Liquor outlet when no Vapor stream is connected. Default 0.30.</summary>
        Public Property EvaporationFraction As Double = 0.30
        ''' <summary>Gets or sets the fractional reduction (0-1) of the solute solubility in Antisolvent mode; the saturation concentration is multiplied by (1 - value). Default 0.7.</summary>
        Public Property SolubilityReductionByAntisolvent As Double = 0.7
        ''' <summary>Gets or sets the mean crystal size, in micrometres. Reported only; the calculation does not use it. Default 200.</summary>
        Public Property MeanCrystalSize_um As Double = 200.0 ' reported only

        ''' <summary>Gets or sets the solute mass flow in the combined feed, in kg/s. Calculated result.</summary>
        Public Property Result_SoluteInFeed_kgs As Double = 0.0
        ''' <summary>Gets or sets the crystals outlet mass flow, in kg/s. Calculated result.</summary>
        Public Property Result_Cryst_kgs As Double = 0.0
        ''' <summary>Gets or sets the mother liquor outlet mass flow, in kg/s. Calculated result.</summary>
        Public Property Result_MotherLiquor_kgs As Double = 0.0
        ''' <summary>Gets or sets the crystallization yield, the crystallized mass over the solute mass in the feed (0-1). Calculated result.</summary>
        Public Property Result_Yield As Double = 0.0
        ''' <summary>Gets or sets the saturation concentration used in the last calculation, in g solute per g solvent. Calculated result.</summary>
        Public Property Result_Csat_gg As Double = 0.0
        ''' <summary>Gets or sets the Vapor outlet mass flow (evaporated solvent), in kg/s. Zero outside Evaporative mode and when no Vapor stream is connected. Calculated result.</summary>
        Public Property Result_Vapor_kgs As Double = 0.0
        ''' <summary>Gets or sets the heat duty from the enthalpy balance (outlets minus inlets), in kW; positive = heat added. Calculated result.</summary>
        Public Property Result_Duty_kW As Double = 0.0

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

        ''' <summary>Initializes a new default instance of the <see cref="UnitOp_Crystallizer"/> class.</summary>
        Public Sub New()
            MyBase.New()
        End Sub

        ''' <summary>Initializes a new instance of the <see cref="UnitOp_Crystallizer"/> class with a name and description.</summary>
        ''' <param name="name">The name of this unit operation.</param>
        ''' <param name="description">A brief description of this unit operation.</param>
        Public Sub New(ByVal name As String, ByVal description As String)
            MyBase.New()
            Me.ComponentName = name
            Me.ComponentDescription = description
        End Sub

        ''' <summary>Creates a deep copy of this object by round-tripping through XML serialization.</summary>
        ''' <returns>A new <see cref="UnitOp_Crystallizer"/> instance with the same property values.</returns>
        Public Overrides Function CloneXML() As Object
            Dim obj As ICustomXMLSerialization = New UnitOp_Crystallizer()
            obj.LoadData(Me.SaveData)
            Return obj
        End Function

        ''' <summary>Creates a deep copy of this object by round-tripping through JSON serialization.</summary>
        ''' <returns>A new <see cref="UnitOp_Crystallizer"/> instance with the same property values.</returns>
        Public Overrides Function CloneJSON() As Object
            Return Newtonsoft.Json.JsonConvert.DeserializeObject(Of UnitOp_Crystallizer)(Newtonsoft.Json.JsonConvert.SerializeObject(Me))
        End Function

        ''' <summary>Evaluates the solubility correlation A + B(T - 298.15) + C(T - 298.15)^2, clipped at zero.</summary>
        ''' <param name="T_K">The temperature, in K.</param>
        ''' <returns>The saturation concentration, in g solute per g solvent.</returns>
        Public Function SolubilityAt(T_K As Double) As Double
            Dim x = T_K - 298.15
            Return Max(0.0, Sol_A + Sol_B * x + Sol_C * x * x)
        End Function

        Public Overrides Sub Calculate(Optional ByVal args As Object = Nothing)

            If String.IsNullOrEmpty(SoluteCompound) Then _
                Throw New Exception("Crystallizer: Solute compound not selected.")
            If String.IsNullOrEmpty(SolventCompound) Then _
                Throw New Exception("Crystallizer: Solvent compound not selected.")
            If Not Me.GraphicObject.InputConnectors(0).IsAttached Then _
                Throw New Exception("Crystallizer: Feed not connected.")
            If Me.GraphicObject.OutputConnectors.Count < 2 OrElse
               Not Me.GraphicObject.OutputConnectors(0).IsAttached OrElse
               Not Me.GraphicObject.OutputConnectors(1).IsAttached Then
                Throw New Exception("Crystallizer: Both Crystals and Mother Liquor outlets must be connected.")
            End If

            Dim feed As MaterialStream =
                DirectCast(FlowSheet.SimulationObjects(Me.GraphicObject.InputConnectors(0).AttachedConnector.AttachedFrom.Name), MaterialStream)
            Dim antisolvent As MaterialStream = Nothing
            If Me.GraphicObject.InputConnectors.Count > 1 AndAlso Me.GraphicObject.InputConnectors(1).IsAttached Then
                antisolvent = DirectCast(FlowSheet.SimulationObjects(Me.GraphicObject.InputConnectors(1).AttachedConnector.AttachedFrom.Name), MaterialStream)
            End If

            Dim T_in = feed.Phases(0).Properties.temperature.GetValueOrDefault
            Dim P = feed.Phases(0).Properties.pressure.GetValueOrDefault
            Dim m_total = feed.Phases(0).Properties.massflow.GetValueOrDefault

            Dim feedComp As New Dictionary(Of String, Double)
            For Each c In feed.Phases(0).Compounds.Values
                feedComp(c.Name) = c.MassFraction.GetValueOrDefault * m_total
            Next
            If antisolvent IsNot Nothing Then
                Dim m_as = antisolvent.Phases(0).Properties.massflow.GetValueOrDefault
                For Each c In antisolvent.Phases(0).Compounds.Values
                    Dim mf = c.MassFraction.GetValueOrDefault * m_as
                    If feedComp.ContainsKey(c.Name) Then
                        feedComp(c.Name) += mf
                    Else
                        feedComp(c.Name) = mf
                    End If
                Next
                m_total += m_as
            End If

            Dim m_solute As Double = 0.0, m_solvent As Double = 0.0
            If feedComp.ContainsKey(SoluteCompound) Then m_solute = feedComp(SoluteCompound)
            If feedComp.ContainsKey(SolventCompound) Then m_solvent = feedComp(SolventCompound)

            Dim T_op As Double = T_in
            Dim solvent_effective As Double = m_solvent

            Select Case Mode
                Case CrystallizerMode.Cooling
                    T_op = OperatingT_K
                Case CrystallizerMode.Evaporative
                    T_op = OperatingT_K ' may be elevated to boiling
                    solvent_effective = m_solvent * Max(0.0, 1.0 - Max(0.0, Min(1.0, EvaporationFraction)))
                Case CrystallizerMode.Antisolvent
                    ' Keep inlet T; antisolvent already added. Effective solubility dropped by user factor.
                    T_op = T_in
            End Select

            Dim Csat = SolubilityAt(T_op)
            If Mode = CrystallizerMode.Antisolvent Then
                Csat *= Max(0.0, Min(1.0, 1.0 - SolubilityReductionByAntisolvent))
            End If
            Result_Csat_gg = Csat

            Dim max_dissolved = Csat * solvent_effective
            Dim m_cryst = Max(0.0, m_solute - max_dissolved)
            Dim m_solute_in_liquor = m_solute - m_cryst

            ' Evaporated solvent (Evaporative mode only): to the Vapor outlet when one is connected,
            ' otherwise back into the mother liquor so that the mass balance closes.
            Dim m_evap As Double = m_solvent - solvent_effective
            Dim vaporConnected As Boolean = Me.GraphicObject.OutputConnectors.Count > 2 AndAlso
                                            Me.GraphicObject.OutputConnectors(2).IsAttached
            Dim m_vap As Double = 0.0
            If m_evap > 0.0 Then
                If vaporConnected Then
                    m_vap = m_evap
                Else
                    FlowSheet.ShowMessage(Me.GraphicObject.Tag & ": no Vapor outlet connected, the evaporated solvent stays in the Mother Liquor outlet.",
                                          IFlowsheet.MessageType.Warning)
                End If
            End If

            ' Build outlet streams. Crystals outlet: pure solute (crystallized). Mother liquor:
            ' remaining solute, the solvent left after evaporation (plus the evaporated solvent when
            ' there is no Vapor outlet) and all other compounds. Vapor: the evaporated solvent.
            Dim cryst As New Dictionary(Of String, Double)
            Dim liquor As New Dictionary(Of String, Double)
            Dim vapor As New Dictionary(Of String, Double)
            For Each kv In feedComp
                vapor(kv.Key) = 0.0
                If kv.Key = SoluteCompound Then
                    cryst(kv.Key) = m_cryst
                    liquor(kv.Key) = m_solute_in_liquor
                ElseIf kv.Key = SolventCompound Then
                    cryst(kv.Key) = 0.0
                    liquor(kv.Key) = solvent_effective + (m_evap - m_vap)
                    vapor(kv.Key) = m_vap
                Else
                    cryst(kv.Key) = 0.0
                    liquor(kv.Key) = kv.Value
                End If
            Next

            Dim m_c As Double = 0.0, m_l As Double = 0.0
            For Each v In cryst.Values : m_c += v : Next
            For Each v In liquor.Values : m_l += v : Next

            ' The vapor leaves at the operating temperature. When the feed pressure is above the
            ' solvent vapor pressure there, the solvent cannot boil at that pressure: the vapor then
            ' leaves saturated at the solvent vapor pressure (the vessel vacuum).
            Dim P_vap As Double = P
            Dim vaporSaturated As Boolean = False
            If m_vap > 0.0 Then
                PropertyPackage.CurrentMaterialStream = feed
                Dim Psat = PropertyPackage.AUX_PVAPi(SolventCompound, T_op)
                If Psat > 0.0 AndAlso Psat < P Then
                    P_vap = Psat
                    vaporSaturated = True
                End If
            End If

            Result_SoluteInFeed_kgs = m_solute
            Result_Cryst_kgs = m_c
            Result_MotherLiquor_kgs = m_l
            Result_Vapor_kgs = m_vap
            If m_solute > 0 Then Result_Yield = m_c / m_solute Else Result_Yield = 0.0

            ' Heat duty from the enthalpy balance of the streams (kW, positive = heat added).
            Try
                Dim H_in As Double = feed.Phases(0).Properties.enthalpy.GetValueOrDefault *
                                     feed.Phases(0).Properties.massflow.GetValueOrDefault
                If antisolvent IsNot Nothing Then
                    H_in += antisolvent.Phases(0).Properties.enthalpy.GetValueOrDefault *
                            antisolvent.Phases(0).Properties.massflow.GetValueOrDefault
                End If
                Dim H_out As Double = OutletEnthalpyFlow(feed, cryst, m_c, T_op, P, False) +
                                      OutletEnthalpyFlow(feed, liquor, m_l, T_op, P, False) +
                                      OutletEnthalpyFlow(feed, vapor, m_vap, T_op, P_vap, vaporSaturated)
                Result_Duty_kW = H_out - H_in
            Catch ex As Exception
                Result_Duty_kW = Double.NaN
                FlowSheet.ShowMessage(Me.GraphicObject.Tag & ": heat duty not calculated (" & ex.Message & ").",
                                      IFlowsheet.MessageType.Warning)
            End Try

            WriteStream(FlowSheet.SimulationObjects(Me.GraphicObject.OutputConnectors(0).AttachedConnector.AttachedTo.Name),
                        cryst, m_c, T_op, P)
            WriteStream(FlowSheet.SimulationObjects(Me.GraphicObject.OutputConnectors(1).AttachedConnector.AttachedTo.Name),
                        liquor, m_l, T_op, P)
            If vaporConnected Then
                Dim msv As MaterialStream = FlowSheet.SimulationObjects(Me.GraphicObject.OutputConnectors(2).AttachedConnector.AttachedTo.Name)
                WriteStream(msv, vapor, m_vap, T_op, P_vap)
                If vaporSaturated Then
                    msv.SpecType = StreamSpec.Pressure_and_VaporFraction
                    msv.Phases(2).Properties.molarfraction = 1.0
                End If
            End If

        End Sub

        ''' <summary>Enthalpy flow of an outlet, in kW, from a flash of a copy of the feed carrying the outlet composition.</summary>
        Private Function OutletEnthalpyFlow(template As MaterialStream, m As Dictionary(Of String, Double), total As Double,
                                            T As Double, P As Double, saturatedVapor As Boolean) As Double
            If total <= 0.0 Then Return 0.0
            Dim tms As MaterialStream = template.Clone
            tms.SetFlowsheet(Me.FlowSheet)
            tms.SetPropertyPackage(PropertyPackage)
            WriteStream(tms, m, total, T, P)
            If saturatedVapor Then
                tms.SpecType = StreamSpec.Pressure_and_VaporFraction
                tms.Phases(2).Properties.molarfraction = 1.0
            End If
            PropertyPackage.CurrentMaterialStream = tms
            tms.Calculate(True, True)
            Return tms.Phases(0).Properties.enthalpy.GetValueOrDefault * total
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
            For i = 0 To Me.GraphicObject.OutputConnectors.Count - 1
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
            Return "Crystallizer (cooling / evaporative / antisolvent)"
        End Function
        ''' <summary>Returns the localized display name for this unit operation type.</summary>
        ''' <returns>A translated name string for this unit operation type.</returns>
        Public Overrides Function GetDisplayName() As String
            Return "Crystallizer"
        End Function

        ''' <summary>Generates a plain-text results report for this unit operation.</summary>
        ''' <param name="su">The unit system used for formatting output values.</param>
        ''' <param name="ci">The culture info used for number formatting.</param>
        ''' <param name="numberformat">A .NET numeric format string (e.g. "G6") applied to output values.</param>
        ''' <returns>A formatted multi-line string report.</returns>
        Public Overrides Function GetReport(su As IUnitsOfMeasure, ci As Globalization.CultureInfo, numberformat As String) As String
            Dim s As New Text.StringBuilder
            s.AppendLine("Crystallizer: " & Me.GraphicObject.Tag)
            s.AppendLine("Mode:        " & Mode.ToString())
            s.AppendLine("Solute:      " & SoluteCompound)
            s.AppendLine("Solvent:     " & SolventCompound)
            s.AppendLine("Operating T: " & OperatingT_K.ToString(numberformat, ci) & " K")
            s.AppendLine("Csat:        " & Result_Csat_gg.ToString(numberformat, ci) & " g/g solvent")
            s.AppendLine()
            s.AppendLine("Solute in feed:      " & Result_SoluteInFeed_kgs.ToString(numberformat, ci) & " kg/s")
            s.AppendLine("Crystallized:        " & Result_Cryst_kgs.ToString(numberformat, ci) & " kg/s")
            s.AppendLine("Mother liquor:       " & Result_MotherLiquor_kgs.ToString(numberformat, ci) & " kg/s")
            s.AppendLine("Vapor:               " & Result_Vapor_kgs.ToString(numberformat, ci) & " kg/s")
            s.AppendLine("Crystallization yield: " & (Result_Yield * 100).ToString(numberformat, ci) & " %")
            s.AppendLine("Heat duty:           " & Result_Duty_kW.ToString(numberformat, ci) & " kW")
            Return s.ToString()
        End Function

        Private Shared ReadOnly _inputProps As String() = {
            "Mode", "Solute Compound", "Solvent Compound", "Operating T",
            "Solubility A", "Solubility B", "Solubility C",
            "Evaporation Fraction", "Solubility Reduction By Antisolvent", "Mean Crystal Size"}
        Private Shared ReadOnly _outputProps As String() = {
            "Solute In Feed", "Crystallized Mass", "Mother Liquor Mass", "Crystallization Yield", "Saturation Concentration",
            "Vapor Mass", "Heat Duty"}

        Public Overrides Function GetProperties(proptype As PropertyType) As String()
            Dim baseprops = MyBase.GetProperties(proptype)
            Select Case proptype
                Case PropertyType.WR
                    'only the inputs the mode reads: the evaporation fraction in Evaporative mode, the
                    'solubility reduction in Antisolvent mode, which runs at the inlet temperature.
                    Dim unused As New List(Of String)
                    If Mode <> CrystallizerMode.Evaporative Then unused.Add("Evaporation Fraction")
                    If Mode <> CrystallizerMode.Antisolvent Then
                        unused.Add("Solubility Reduction By Antisolvent")
                    Else
                        unused.Add("Operating T")
                    End If
                    Return _inputProps.Where(Function(p) Not unused.Contains(p)).ToArray()
                Case PropertyType.RO : Return _outputProps
                Case Else : Return _inputProps.Concat(_outputProps).Concat(baseprops).ToArray()
            End Select
        End Function

        Public Overrides Function GetPropertyValue(prop As String, Optional su As IUnitsOfMeasure = Nothing) As Object
            Select Case prop
                Case "Mode" : Return Mode.ToString()
                Case "Solute Compound" : Return SoluteCompound
                Case "Solvent Compound" : Return SolventCompound
                Case "Operating T" : Return OperatingT_K
                Case "Solubility A" : Return Sol_A
                Case "Solubility B" : Return Sol_B
                Case "Solubility C" : Return Sol_C
                Case "Evaporation Fraction" : Return EvaporationFraction
                Case "Solubility Reduction By Antisolvent" : Return SolubilityReductionByAntisolvent
                Case "Mean Crystal Size" : Return MeanCrystalSize_um
                Case "Solute In Feed" : Return Result_SoluteInFeed_kgs
                Case "Crystallized Mass" : Return Result_Cryst_kgs
                Case "Mother Liquor Mass" : Return Result_MotherLiquor_kgs
                Case "Crystallization Yield" : Return Result_Yield
                Case "Saturation Concentration" : Return Result_Csat_gg
                Case "Vapor Mass" : Return Result_Vapor_kgs
                Case "Heat Duty" : Return Result_Duty_kW
                Case Else : Return MyBase.GetPropertyValue(prop, su)
            End Select
        End Function

        Public Overrides Function GetPropertyUnit(prop As String, Optional su As IUnitsOfMeasure = Nothing) As String
            Select Case prop
                Case "Operating T" : Return "K"
                Case "Mean Crystal Size" : Return "um"
                Case "Solute In Feed", "Crystallized Mass", "Mother Liquor Mass", "Vapor Mass" : Return "kg/s"
                Case "Heat Duty" : Return "kW"
                Case "Saturation Concentration" : Return "g/g"
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
                Case "Mode"
                    Dim m As CrystallizerMode
                    If [Enum].TryParse(Of CrystallizerMode)(propval?.ToString(), m) Then Me.Mode = m
                    Return True
                Case "Solute Compound" : SoluteCompound = propval?.ToString() : Return True
                Case "Solvent Compound" : SolventCompound = propval?.ToString() : Return True
                Case "Operating T" : OperatingT_K = d : Return True
                Case "Solubility A" : Sol_A = d : Return True
                Case "Solubility B" : Sol_B = d : Return True
                Case "Solubility C" : Sol_C = d : Return True
                Case "Evaporation Fraction" : EvaporationFraction = d : Return True
                Case "Solubility Reduction By Antisolvent" : SolubilityReductionByAntisolvent = d : Return True
                Case "Mean Crystal Size" : MeanCrystalSize_um = d : Return True
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
                Return "CRY-"
            End Get
        End Property
        Public Function ReturnInstance(typename As String) As Object Implements IExternalUnitOperation.ReturnInstance
            Return New UnitOp_Crystallizer()
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

            container.CreateAndAddLabelRow("Crystallizer Mode")

            container.CreateAndAddDropDownRow("Mode",
                                              New List(Of String)({"Cooling", "Evaporative", "Antisolvent"}),
                                              Mode,
                                              Sub(dd, e)
                                                  Mode = CType(dd.SelectedIndex, CrystallizerMode)
                                                  FlowSheet.RequestCalculation()
                                              End Sub)

            Dim solIdx = Math.Max(0, compIds.IndexOf(SoluteCompound))
            container.CreateAndAddDropDownRow("Solute Compound",
                                              New List(Of String)(New String() {"(none)"}.Concat(compIds)),
                                              If(String.IsNullOrEmpty(SoluteCompound), 0, solIdx + 1),
                                              Sub(dd, e)
                                                  SoluteCompound = If(dd.SelectedIndex > 0, compIds(dd.SelectedIndex - 1), "")
                                                  FlowSheet.RequestCalculation()
                                              End Sub)

            Dim solvIdx = Math.Max(0, compIds.IndexOf(SolventCompound))
            container.CreateAndAddDropDownRow("Solvent Compound",
                                              New List(Of String)(New String() {"(none)"}.Concat(compIds)),
                                              If(String.IsNullOrEmpty(SolventCompound), 0, solvIdx + 1),
                                              Sub(dd, e)
                                                  SolventCompound = If(dd.SelectedIndex > 0, compIds(dd.SelectedIndex - 1), "")
                                                  FlowSheet.RequestCalculation()
                                              End Sub)

            container.CreateAndAddLabelRow("Operating Conditions")

            container.CreateAndAddTextBoxRow(nf, "Operating Temperature (K)", OperatingT_K,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     OperatingT_K = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Evaporation Fraction (0-1)", EvaporationFraction,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     EvaporationFraction = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Solubility Reduction by Antisolvent (0-1)", SolubilityReductionByAntisolvent,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     SolubilityReductionByAntisolvent = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddLabelRow("Solubility: Csat(T) = A + B*(T-298) + C*(T-298)^2 [g solute / g solvent]")

            container.CreateAndAddTextBoxRow(nf, "Coefficient A", Sol_A,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     Sol_A = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Coefficient B", Sol_B,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     Sol_B = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Coefficient C", Sol_C,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     Sol_C = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Mean Crystal Size (um, reported only)", MeanCrystalSize_um,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     MeanCrystalSize_um = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

        End Sub

        Public Sub CreateConnectors() Implements IExternalUnitOperation.CreateConnectors
            If GraphicObject Is Nothing Then Return
            Dim w = GraphicObject.Width, h = GraphicObject.Height
            Dim gx = GraphicObject.X, gy = GraphicObject.Y
            If GraphicObject.InputConnectors.Count = 2 AndAlso
               (GraphicObject.OutputConnectors.Count = 2 OrElse GraphicObject.OutputConnectors.Count = 3) Then
                GraphicObject.InputConnectors(0).Position = New Point(gx, gy + 0.4 * h)
                GraphicObject.InputConnectors(0).ConnectorName = "Feed"
                GraphicObject.InputConnectors(1).Position = New Point(gx + 0.3 * w, gy)
                GraphicObject.InputConnectors(1).ConnectorName = "Antisolvent (Optional)"
                GraphicObject.InputConnectors(1).Direction = ConDir.Down
                GraphicObject.OutputConnectors(0).Position = New Point(gx + 0.7 * w, gy + h)
                GraphicObject.OutputConnectors(0).ConnectorName = "Crystals"
                GraphicObject.OutputConnectors(0).Direction = ConDir.Up
                GraphicObject.OutputConnectors(1).Position = New Point(gx + w, gy + 0.4 * h)
                GraphicObject.OutputConnectors(1).ConnectorName = "Mother Liquor"
                'flowsheets saved before the Vapor outlet have two outlets; it goes at the end, so the
                'saved connections keep their indices.
                If GraphicObject.OutputConnectors.Count = 2 Then
                    GraphicObject.OutputConnectors.Add(New ConnectionPoint With {.Type = ConType.ConOut})
                End If
                GraphicObject.OutputConnectors(2).Position = New Point(gx + 0.7 * w, gy)
                GraphicObject.OutputConnectors(2).ConnectorName = "Vapor (Optional)"
                GraphicObject.OutputConnectors(2).Direction = ConDir.Up
            Else
                GraphicObject.InputConnectors.Clear() : GraphicObject.OutputConnectors.Clear()
                GraphicObject.InputConnectors.Add(New ConnectionPoint With {
                    .Position = New Point(gx, gy + 0.4 * h), .Type = ConType.ConIn,
                    .Direction = ConDir.Right, .ConnectorName = "Feed"})
                GraphicObject.InputConnectors.Add(New ConnectionPoint With {
                    .Position = New Point(gx + 0.3 * w, gy), .Type = ConType.ConIn,
                    .Direction = ConDir.Down, .ConnectorName = "Antisolvent (Optional)"})
                GraphicObject.OutputConnectors.Add(New ConnectionPoint With {
                    .Position = New Point(gx + 0.7 * w, gy + h), .Type = ConType.ConOut,
                    .Direction = ConDir.Up, .ConnectorName = "Crystals"})
                GraphicObject.OutputConnectors.Add(New ConnectionPoint With {
                    .Position = New Point(gx + w, gy + 0.4 * h), .Type = ConType.ConOut,
                    .Direction = ConDir.Right, .ConnectorName = "Mother Liquor"})
                GraphicObject.OutputConnectors.Add(New ConnectionPoint With {
                    .Position = New Point(gx + 0.7 * w, gy), .Type = ConType.ConOut,
                    .Direction = ConDir.Up, .ConnectorName = "Vapor (Optional)"})
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
                    "crystallizer_photo", _photoImage) Then Return
            End If
            DrawIcon(canvas, CSng(GraphicObject.X), CSng(GraphicObject.Y),
                     CSng(GraphicObject.Width), CSng(GraphicObject.Height),
                     GraphicObject.DrawMode = 1)
        End Sub

        Private Shared Sub DrawIcon(canvas As SKCanvas, gx As Single, gy As Single, w As Single, h As Single, Optional mono As Boolean = False)
            ' Draft-tube crystallizer: vertical tank w/ cone bottom, top agitator motor, sparkle crystals overlay.
            Dim vessel As New SKRect(gx + 0.2F * w, gy + 0.18F * h, gx + 0.8F * w, gy + 0.95F * h)
            BioOpsDrawHelper.DrawConeBottomTank(canvas, vessel, mono)
            Dim cx = (vessel.Left + vessel.Right) * 0.5F
            ' top motor and shaft
            Dim motor As New SKRect(cx - 0.08F * w, gy + 0.02F * h, cx + 0.08F * w, gy + 0.15F * h)
            BioOpsDrawHelper.DrawMotor(canvas, motor, mono)
            BioOpsDrawHelper.DrawAgitator(canvas, cx, gy + 0.18F * h, gy + 0.62F * h, 0.22F * w, mono)
            ' draft tube hint (inner cylinder)
            Using dt As New SKPaint With {.Color = If(mono, New SKColor(120, 120, 120), New SKColor(130, 150, 175)), .Style = SKPaintStyle.Stroke, .StrokeWidth = 1.0F, .IsAntialias = True}
                canvas.DrawRect(New SKRect(cx - 0.12F * w, gy + 0.28F * h, cx + 0.12F * w, gy + 0.7F * h), dt)
            End Using
            ' crystal sparkles inside
            Using stroke As New SKPaint With {.Color = If(mono, New SKColor(60, 60, 60), New SKColor(60, 90, 130)), .Style = SKPaintStyle.Stroke, .StrokeWidth = 1.0F, .IsAntialias = True}
                Dim pts = New Single(,) {{0.3F, 0.75F}, {0.52F, 0.82F}, {0.7F, 0.72F}, {0.42F, 0.6F}, {0.62F, 0.5F}}
                For i = 0 To pts.GetLength(0) - 1
                    Dim rx = gx + pts(i, 0) * w
                    Dim ry = gy + pts(i, 1) * h
                    Dim sz = 0.025F * w
                    canvas.DrawLine(rx - sz, ry, rx + sz, ry, stroke)
                    canvas.DrawLine(rx, ry - sz, rx, ry + sz, stroke)
                Next
            End Using
        End Sub

    End Class

End Namespace
