'    ISO 5167 Orifice Plate Calculation Routines 
'    Copyright 2010 Daniel Wagner O. de Medeiros
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


Imports DWSIM.Thermodynamics
Imports DWSIM.Thermodynamics.Streams
Imports DWSIM.SharedClasses
Imports DWSIM.UnitOperations.UnitOperations.Auxiliary
Imports DWSIM.Thermodynamics.BaseClasses
Imports DWSIM.Interfaces.Enums

Namespace UnitOperations

    ''' <summary>
    ''' Represents an orifice plate flow-measurement device modelled using the ISO 5167 standard.
    ''' Calculates the pressure drop across the orifice for a given mass flow rate and fluid properties,
    ''' supporting single-phase liquid or gas service and two-phase homogeneous or slip flow models.
    ''' </summary>
    <System.Serializable()> Public Partial Class OrificePlate

        Inherits UnitOperations.UnitOpBaseClass

        ''' <summary>Gets or sets the simulation object class category for this orifice plate (PressureChangers).</summary>
        Public Overrides Property ObjectClass As SimulationObjectClass = SimulationObjectClass.PressureChangers

        ''' <summary>Gets a value indicating whether this unit operation supports dynamic simulation mode.</summary>
        Public Overrides ReadOnly Property SupportsDynamicMode As Boolean = True

        ''' <summary>Gets a value indicating whether this unit operation exposes dedicated properties for dynamic mode.</summary>
        Public Overrides ReadOnly Property HasPropertiesForDynamicMode As Boolean = False

        <NonSerialized> <Xml.Serialization.XmlIgnore> Public f As Object

        ''' <summary>Defines the two-phase flow model used when both liquid and vapour phases are present.</summary>
        Public Enum CalcMethod
            ''' <summary>Treats the two-phase mixture as a homogeneous fluid with averaged properties.</summary>
            Homogeneous = 0
            ''' <summary>Accounts for slip between the liquid and vapour phases using phase-specific velocities.</summary>
            Slip = 1
        End Enum

        ''' <summary>Defines the pressure tap configuration of the orifice plate, which affects the discharge coefficient.</summary>
        Public Enum OrificeType
            ''' <summary>Pressure taps located immediately upstream and downstream of the orifice plate (corner taps).</summary>
            CornerTaps = 0
            ''' <summary>Pressure taps located 1 inch (25.4 mm) from each face of the orifice plate.</summary>
            FlangeTaps = 1
            ''' <summary>Pressure taps located at D and D/2 from the orifice plate (radius or D&frac12;D taps).</summary>
            RadiusTaps = 2
        End Enum

        Protected m_dt As Nullable(Of Double)
        Protected _orificeDP As Double = 0.0
        Protected _fluidDP As Double = 0.0
        Protected _beta As Double = 0.5
        Protected _orificediameter As Double = 100.0# / 1000.0
        Protected _internaldiameter As Double = 200.0# / 1000.0
        Protected _orificetype As OrificeType = OrificeType.FlangeTaps
        Protected _calcmethod As CalcMethod
        Protected _corrfactor As Double = 1.0

        ''' <summary>
        ''' Initializes a new instance of the <see cref="OrificePlate"/> class with a name and description.
        ''' </summary>
        ''' <param name="name">The display name of the orifice plate.</param>
        ''' <param name="description">A brief description of the orifice plate.</param>
        Public Sub New(ByVal name As String, ByVal description As String)

            MyBase.CreateNew()
            Me.ComponentName = name
            Me.ComponentDescription = description

        End Sub

        ''' <summary>Creates a deep copy of this orifice plate via XML serialization.</summary>
        ''' <returns>A new <see cref="OrificePlate"/> instance with the same state.</returns>
        Public Overrides Function CloneXML() As Object
            Dim obj As ICustomXMLSerialization = New OrificePlate()
            obj.LoadData(Me.SaveData)
            Return obj
        End Function

        ''' <summary>Creates a deep copy of this orifice plate via JSON serialization.</summary>
        ''' <returns>A new <see cref="OrificePlate"/> instance with the same state.</returns>
        Public Overrides Function CloneJSON() As Object
            Return Newtonsoft.Json.JsonConvert.DeserializeObject(Of OrificePlate)(Newtonsoft.Json.JsonConvert.SerializeObject(Me))
        End Function

        ''' <summary>Gets or sets the pressure tap configuration of this orifice plate.</summary>
        Public Property OrifType() As OrificeType
            Get
                Return _orificetype
            End Get
            Set(ByVal value As OrificeType)
                _orificetype = value
            End Set
        End Property

        ''' <summary>Gets or sets the total pressure drop (Pa) across the orifice plate including flow recovery losses.</summary>
        Public Property OverallPressureDrop() As Double
            Get
                Return _fluidDP
            End Get
            Set(ByVal value As Double)
                _fluidDP = value
            End Set
        End Property

        ''' <summary>Gets or sets the differential pressure (Pa) measured directly across the orifice throat.</summary>
        Public Property OrificePressureDrop() As Double
            Get
                Return _orificeDP
            End Get
            Set(ByVal value As Double)
                _orificeDP = value
            End Set
        End Property

        ''' <summary>Gets or sets a dimensionless correction factor applied to the calculated discharge coefficient.</summary>
        Public Property CorrectionFactor() As Double
            Get
                Return _corrfactor
            End Get
            Set(ByVal value As Double)
                _corrfactor = value
            End Set
        End Property

        ''' <summary>Gets or sets the two-phase flow calculation method used when both phases are present.</summary>
        Public Property CalculationMethod() As CalcMethod
            Get
                Return _calcmethod
            End Get
            Set(ByVal value As CalcMethod)
                _calcmethod = value
            End Set
        End Property

        ''' <summary>Gets or sets the diameter ratio β = d/D (orifice diameter / pipe internal diameter).</summary>
        Public Property Beta() As Double
            Get
                Return _beta
            End Get
            Set(ByVal value As Double)
                _beta = value
            End Set
        End Property

        ''' <summary>Gets or sets the orifice bore diameter (m).</summary>
        Public Property OrificeDiameter() As Double
            Get
                Return _orificediameter
            End Get
            Set(ByVal value As Double)
                _orificediameter = value
            End Set
        End Property

        ''' <summary>Gets or sets the internal pipe bore diameter (m) upstream of the orifice.</summary>
        Public Property InternalPipeDiameter() As Double
            Get
                Return _internaldiameter
            End Get
            Set(ByVal value As Double)
                _internaldiameter = value
            End Set
        End Property

        ''' <summary>Gets or sets the calculated temperature change (K) across the orifice plate.</summary>
        Public Property DeltaT() As Nullable(Of Double)
            Get
                Return m_dt
            End Get
            Set(ByVal value As Nullable(Of Double))
                m_dt = value
            End Set
        End Property

        ''' <summary>Initializes a new default instance of the <see cref="OrificePlate"/> class.</summary>
        Public Sub New()
            MyBase.New()
        End Sub

        ''' <summary>Executes a single dynamic model step by delegating to the steady-state <see cref="Calculate"/> routine.</summary>
        Public Overrides Sub RunDynamicModel()

            Calculate()

        End Sub

        ''' <summary>
        ''' Performs the ISO 5167 orifice plate calculation: determines the downstream pressure and temperature
        ''' given the inlet stream conditions and the configured orifice geometry.
        ''' </summary>
        ''' <param name="args">Optional calculation arguments (not used).</param>
        Public Overrides Sub Calculate(Optional ByVal args As Object = Nothing)

            Dim IObj As Inspector.InspectorItem = Inspector.Host.GetNewInspectorItem()

            Inspector.Host.CheckAndAdd(IObj, "", "Calculate", If(GraphicObject IsNot Nothing, GraphicObject.Tag, "Temporary Object") & " (" & GetDisplayName() & ")", GetDisplayName() & " Calculation Routine", True)

            IObj?.SetCurrent()

            If Not Me.GraphicObject.OutputConnectors(0).IsAttached Then
                Throw New Exception(FlowSheet.GetTranslatedString("Verifiqueasconexesdo"))
            ElseIf Not Me.GraphicObject.InputConnectors(0).IsAttached Then
                Throw New Exception(FlowSheet.GetTranslatedString("Verifiqueasconexesdo"))
            End If

            Dim Ti, Pi, Wi, T2, P2, H2, H1, xv, xl, wv, wl As Double
            Dim rhom, mum, rhov, rhol, muv, mul As Double

            Dim instr, outstr As MaterialStream

            instr = Me.GetInletMaterialStream(0)
            outstr = Me.GetOutletMaterialStream(0)

            Me.PropertyPackage.CurrentMaterialStream = instr
            Ti = instr.Phases(0).Properties.temperature.GetValueOrDefault
            Pi = instr.Phases(0).Properties.pressure.GetValueOrDefault
            Wi = instr.Phases(0).Properties.massflow.GetValueOrDefault
            H1 = instr.Phases(0).Properties.enthalpy.GetValueOrDefault
            rhom = instr.Phases(0).Properties.density.GetValueOrDefault
            rhov = instr.Phases(2).Properties.density.GetValueOrDefault
            rhol = instr.Phases(1).Properties.density.GetValueOrDefault
            muv = instr.Phases(2).Properties.viscosity.GetValueOrDefault
            mul = instr.Phases(1).Properties.viscosity.GetValueOrDefault
            xv = instr.Phases(2).Properties.molarfraction.GetValueOrDefault
            xl = instr.Phases(1).Properties.molarfraction.GetValueOrDefault
            wv = instr.Phases(2).Properties.massfraction.GetValueOrDefault
            wl = instr.Phases(1).Properties.massfraction.GetValueOrDefault
            If xv > 0 And xl > 0 Then
                mum = (xv / muv + xl / mul) ^ -1
            ElseIf xl = 0 Then
                mum = muv
            ElseIf xv = 0 Then
                mum = mul
            End If

            Dim beta, A1, A2, s2_s1, L1, L2 As Double

            beta = _beta
            A1 = 3.1416 * _internaldiameter ^ 2 / 4
            A2 = 3.1416 * _orificediameter ^ 2 / 4

            Select Case _orificetype

                Case OrificeType.CornerTaps

                    'placa de orificio corner taps

                    s2_s1 = 0
                    L1 = 0
                    L2 = 0

                Case OrificeType.FlangeTaps

                    'placa de orificio flange taps

                    s2_s1 = 0.0508
                    L1 = 1 / (_orificediameter / 0.0254)
                    L2 = 1 / (_orificediameter / 0.0254)

                Case OrificeType.RadiusTaps

                    'placa de orificio radius taps

                    s2_s1 = 1.5 * _orificediameter
                    L1 = 1

                    L2 = 0.47

            End Select

            Dim ReD, Cd, DP, c1, c2, c3, M2, A As Double

            ReD = Wi * _internaldiameter / (A1 * mum)

            M2 = 2 * L2 / (1 - beta)
            A = (19000 * beta / ReD) ^ 0.8

            c1 = 0.5961 + 0.0261 * beta ^ 2 - 0.216 * beta ^ 8 + 0.000521 * (10 ^ 6 * beta / ReD) ^ 0.7 + (0.0188 + 0.0063 * A) * beta ^ 3.5 * (10 ^ 6 / ReD) ^ 0.3
            c2 = (0.043 + 0.08 * Math.Exp(-10 * L1) - 0.123 * Math.Exp(-7 * L1)) * (1 - 0.11 * A) * beta ^ 4 / (1 - beta ^ 4) - 0.031 * (M2 - 0.8 * M2 ^ 1.1) * beta ^ 1.3
            If _internaldiameter * 1000 < 71.12 Then
                c3 = 0.011 * (0.75 - beta) * (2.8 - (_internaldiameter * 1000) / 25.4)
            Else
                c3 = 0
            End If
            Cd = c1 + c2 + c3

            'DP = (Wi / (_corrfactor * Cd * A2)) ^ 2 * (1 - beta ^ 4) / (2 * rhom)
            DP = rhom / 2 * (Wi / rhom / (_corrfactor * Cd / (1 - beta ^ 4) ^ 0.5 * A2)) ^ 2
            DP = DP + (rhom * 9.8 * (s2_s1))

            _orificeDP = DP
            If beta >= 1.0# Then
                _fluidDP = DP
            Else
                _fluidDP = DP * ((1 - beta ^ 4 * (1 - Cd ^ 2)) ^ 0.5 - Cd * beta ^ 2) / ((1 - beta ^ 4 * (1 - Cd ^ 2)) ^ 0.5 + Cd * beta ^ 2)
            End If

            P2 = Pi - _fluidDP
            H2 = H1

            If P2 <= 0 Then
                Throw New Exception("Error: Negative Pressure! Orifice Plate to small for inlet stream.")
            End If

            IObj?.SetCurrent()
            Dim tmp = Me.PropertyPackage.CalculateEquilibrium2(FlashCalculationType.PressureEnthalpy, P2, H2, Ti)
            T2 = tmp.CalculatedTemperature

            Me.DeltaT = T2 - Ti

            'Atribuir valores a corrente de materia conectada a jusante
            With outstr
                .AtEquilibrium = False
                .Phases(0).Properties.temperature = T2
                .Phases(0).Properties.pressure = P2
                .Phases(0).Properties.enthalpy = H2
                Dim comp As BaseClasses.Compound
                Dim i As Integer = 0
                For Each comp In .Phases(0).Compounds.Values
                    comp.MoleFraction = instr.Phases(0).Compounds(comp.Name).MoleFraction
                    comp.MassFraction = instr.Phases(0).Compounds(comp.Name).MassFraction
                    i += 1
                Next
                .Phases(0).Properties.massflow = instr.Phases(0).Properties.massflow.GetValueOrDefault
                .DefinedFlow = FlowSpec.Mass
                .SpecType = StreamSpec.Pressure_and_Enthalpy
            End With

            IObj?.Close()

        End Sub

        ''' <summary>Clears all calculated results.</summary>
        Public Overrides Sub DeCalculate()

            If Me.GraphicObject.OutputConnectors(0).IsAttached Then

                'Zerar valores da corrente de materia conectada a jusante
                With Me.GetOutletMaterialStream(0)
                    .Phases(0).Properties.temperature = Nothing
                    .Phases(0).Properties.pressure = Nothing
                    .Phases(0).Properties.molarfraction = 1
                    .Phases(0).Properties.massfraction = 1
                    .Phases(0).Properties.enthalpy = Nothing
                    Dim comp As BaseClasses.Compound
                    Dim i As Integer = 0
                    For Each comp In .Phases(0).Compounds.Values
                        comp.MoleFraction = 0
                        comp.MassFraction = 0
                        i += 1
                    Next
                    .Phases(0).Properties.massflow = Nothing
                    .Phases(0).Properties.molarflow = Nothing
                    .GraphicObject.Calculated = False
                End With

            End If

        End Sub

        ''' <summary>Returns the value of the specified property.</summary>
        Public Overrides Function GetPropertyValue(ByVal prop As String, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As Object

            Dim val0 As Object = MyBase.GetPropertyValue(prop, su)

            If Not val0 Is Nothing Then
                Return val0
            Else
                If su Is Nothing Then su = New SystemsOfUnits.SI
                Dim cv As New SystemsOfUnits.Converter
                Dim value As Double = 0
                Dim propidx As Integer = Convert.ToInt32(prop.Split("_")(2))

                Select Case propidx
                    Case 0
                        'PROP_OP_0	Orifice Type	1
                        value = Me.OrifType
                    Case 1
                        'PROP_OP_1	Orifice Diameter	1
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.distance, Me.OrificeDiameter)
                    Case 2
                        'PROP_OP_2	Internal Pipe Diameter	1
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.distance, Me.InternalPipeDiameter)
                    Case 3
                        'PROP_OP_3	Correction Factor	1
                        value = Me.CorrectionFactor
                    Case 4
                        'PROP_OP_4	Beta (d/D)	1
                        value = Me.Beta
                    Case 5
                        'PROP_OP_5	Overall Pressure Drop	0
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.deltaP, Me.OverallPressureDrop)
                    Case 6
                        'PROP_OP_6	Orifice Pressure Drop	0
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.deltaP, Me.OrificePressureDrop)
                    Case 7
                        'PROP_OP_7	Delta T	0
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.deltaT, Me.DeltaT.GetValueOrDefault)
                End Select
                Return value
            End If

        End Function

        ''' <summary>Returns an array of property identifiers for the specified property type.</summary>
        Public Overloads Overrides Function GetProperties(ByVal proptype As Interfaces.Enums.PropertyType) As String()
            Dim i As Integer = 0
            Dim proplist As New ArrayList
            Dim basecol = MyBase.GetProperties(proptype)
            If basecol.Length > 0 Then proplist.AddRange(basecol)
            Select Case proptype
                Case PropertyType.RO
                    For i = 4 To 7
                        proplist.Add("PROP_OP_" + CStr(i))
                    Next
                Case PropertyType.RW
                    For i = 0 To 7
                        proplist.Add("PROP_OP_" + CStr(i))
                    Next
                Case PropertyType.WR
                    For i = 0 To 3
                        proplist.Add("PROP_OP_" + CStr(i))
                    Next
                Case PropertyType.ALL
                    For i = 0 To 7
                        proplist.Add("PROP_OP_" + CStr(i))
                    Next
            End Select
            Return proplist.ToArray(GetType(System.String))
            proplist = Nothing
            'PROP_OP_0	Orifice Type	1
            'PROP_OP_1	Orifice Diameter	1
            'PROP_OP_2	Internal Pipe Diameter	1
            'PROP_OP_3	Correction Factor	1
            'PROP_OP_4	Beta (d/D)	1
            'PROP_OP_5	Overall Pressure Drop	0
            'PROP_OP_6	Orifice Pressure Drop	0
            'PROP_OP_7	Delta T	0

        End Function

        ''' <summary>Sets the value of the specified property.</summary>
        Public Overrides Function SetPropertyValue(ByVal prop As String, ByVal propval As Object, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As Boolean

            If MyBase.SetPropertyValue(prop, propval, su) Then Return True

            If su Is Nothing Then su = New SystemsOfUnits.SI
            Dim cv As New SystemsOfUnits.Converter
            Dim propidx As Integer = Convert.ToInt32(prop.Split("_")(2))

            Select Case propidx
                Case 0
                    'PROP_OP_0	Orifice Type	1
                    Me.OrifType = propval
                Case 1
                    'PROP_OP_1	Orifice Diameter	1
                    Me.OrificeDiameter = SystemsOfUnits.Converter.ConvertToSI(su.distance, propval)
                    Me.Beta = Me.OrificeDiameter / Me.InternalPipeDiameter
                Case 2
                    'PROP_OP_2	Internal Pipe Diameter	1
                    Me.InternalPipeDiameter = SystemsOfUnits.Converter.ConvertToSI(su.distance, propval)
                    Me.Beta = Me.OrificeDiameter / Me.InternalPipeDiameter
                Case 3
                    'PROP_OP_3	Correction Factor	1
                    Me.CorrectionFactor = propval
            End Select
            Return 1
        End Function

        ''' <summary>Returns the unit string for the specified property.</summary>
        Public Overrides Function GetPropertyUnit(ByVal prop As String, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As String
            Dim u0 As String = MyBase.GetPropertyUnit(prop, su)

            If u0 <> "NF" Then
                Return u0
            Else
                If su Is Nothing Then su = New SystemsOfUnits.SI
                Dim value As String = ""
                Dim propidx As Integer = Convert.ToInt32(prop.Split("_")(2))
                Select Case propidx
                    Case 0
                        'PROP_OP_0	Orifice Type	1
                        value = ""
                    Case 1
                        'PROP_OP_1	Orifice Diameter	1
                        value = su.distance
                    Case 2
                        'PROP_OP_2	Internal Pipe Diameter
                        value = su.distance
                    Case 3
                        'PROP_OP_3	Correction Factor	1
                        value = ""
                    Case 4
                        'PROP_OP_4	Beta (d/D)	1
                        value = ""
                    Case 5
                        'PROP_OP_5	Overall Pressure Drop	0
                        value = su.deltaP
                    Case 6
                        'PROP_OP_6	Orifice Pressure Drop	0
                        value = su.deltaP
                    Case 7
                        'PROP_OP_7	Delta T	0
                        value = su.deltaT
                End Select
                Return value
            End If
        End Function

        ''' <summary>Returns the icon bitmap as a byte array.</summary>
        Public Overrides Function GetIconBitmapBytes() As Byte()

            Return GetBytesFromResource("DWSIM.UnitOperations.uo_orifice_32.png")

        End Function

        ''' <summary>Returns the localised display description.</summary>
        Public Overrides Function GetDisplayDescription() As String
            Return ResMan.GetLocalString("ORIF_Desc")
        End Function

        ''' <summary>Returns the localised display name.</summary>
        Public Overrides Function GetDisplayName() As String
            Return ResMan.GetLocalString("ORIF_Name")
        End Function

        ''' <summary>Gets a value indicating whether this unit operation is compatible with mobile interfaces.</summary>
        Public Overrides ReadOnly Property MobileCompatible As Boolean
            Get
                Return False
            End Get
        End Property

        ''' <summary>Generates a plain-text report of the orifice plate results.</summary>
        Public Overrides Function GetReport(su As IUnitsOfMeasure, ci As Globalization.CultureInfo, numberformat As String) As String

            Dim str As New Text.StringBuilder

            Dim istr, ostr As MaterialStream
            istr = Me.GetInletMaterialStream(0)
            ostr = Me.GetOutletMaterialStream(0)

            istr.PropertyPackage.CurrentMaterialStream = istr

            str.AppendLine("Pump: " & Me.GraphicObject.Tag)
            str.AppendLine("Property Package: " & Me.PropertyPackage.ComponentName)
            str.AppendLine()
            str.AppendLine("Inlet conditions")
            str.AppendLine()
            str.AppendLine("    Temperature: " & SystemsOfUnits.Converter.ConvertFromSI(su.temperature, istr.Phases(0).Properties.temperature.GetValueOrDefault).ToString(numberformat, ci) & " " & su.temperature)
            str.AppendLine("    Pressure: " & SystemsOfUnits.Converter.ConvertFromSI(su.pressure, istr.Phases(0).Properties.pressure.GetValueOrDefault).ToString(numberformat, ci) & " " & su.pressure)
            str.AppendLine("    Mass flow: " & SystemsOfUnits.Converter.ConvertFromSI(su.massflow, istr.Phases(0).Properties.massflow.GetValueOrDefault).ToString(numberformat, ci) & " " & su.massflow)
            str.AppendLine("    Volumetric flow: " & SystemsOfUnits.Converter.ConvertFromSI(su.volumetricFlow, istr.Phases(0).Properties.volumetric_flow.GetValueOrDefault).ToString(numberformat, ci) & " " & su.volumetricFlow)
            str.AppendLine("    Vapor fraction: " & istr.Phases(2).Properties.molarfraction.GetValueOrDefault.ToString(numberformat, ci))
            str.AppendLine("    Compounds: " & istr.PropertyPackage.RET_VNAMES.ToArrayString)
            str.AppendLine("    Molar composition: " & istr.PropertyPackage.RET_VMOL(PropertyPackages.Phase.Mixture).ToArrayString(ci))
            str.AppendLine()
            str.AppendLine("Calculation parameters")
            str.AppendLine()
            str.AppendLine("    Pressure Tappings: " & OrifType.ToString)
            str.AppendLine("    Orifice Diameter: " & SystemsOfUnits.Converter.ConvertFromSI(su.diameter, Me.OrificeDiameter).ToString(numberformat, ci) & " " & su.diameter)
            str.AppendLine("    Internal Pipe Diameter: " & SystemsOfUnits.Converter.ConvertFromSI(su.diameter, Me.InternalPipeDiameter).ToString(numberformat, ci) & " " & su.diameter)
            str.AppendLine("    Correction Factor: " & CorrectionFactor.ToString(numberformat))
            str.AppendLine()
            str.AppendLine("Results")
            str.AppendLine()
            str.AppendLine("    Orifice Pressure Drop: " & SystemsOfUnits.Converter.ConvertFromSI(su.deltaP, Me.OrificePressureDrop).ToString(numberformat, ci) & " " & su.deltaP)
            str.AppendLine("    Overall Pressure Drop: " & SystemsOfUnits.Converter.ConvertFromSI(su.deltaP, Me.OverallPressureDrop).ToString(numberformat, ci) & " " & su.deltaP)
            str.AppendLine("    Temperature change: " & SystemsOfUnits.Converter.ConvertFromSI(su.deltaT, Me.DeltaT).ToString(numberformat, ci) & " " & su.deltaT)
            str.AppendLine("    Temperature change: " & SystemsOfUnits.Converter.ConvertFromSI(su.deltaT, Me.DeltaT).ToString(numberformat, ci) & " " & su.deltaT)
            str.AppendLine("    Orifice Beta (d/D): " & Beta.ToString(numberformat))

            Return str.ToString

        End Function



    End Class

End Namespace


