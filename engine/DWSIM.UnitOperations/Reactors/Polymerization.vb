'    Free-Radical Polymerization Reactor (CSTR) - unit operation
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

Imports DWSIM.Thermodynamics
Imports DWSIM.Thermodynamics.BaseClasses
Imports DWSIM.Interfaces.Enums
Imports DWSIM.Thermodynamics.Streams
Imports DWSIM.Thermodynamics.Polymers
Imports DWSIM.SharedClasses

Namespace Reactors

    ''' <summary>
    ''' Free-radical polymerization reactor operated as a homogeneous, isothermal, well-mixed vessel. It reads
    ''' the monomer and initiator (and optional solvent / chain-transfer agent) from its feed, solves the
    ''' steady-state method-of-moments model at the reactor residence time, and writes the unreacted feed plus
    ''' the polymer to its product stream, reporting conversion and the number- and weight-average molar masses.
    ''' The polymer product compound's molar mass is set to the computed Mn so the mass balance closes.
    ''' </summary>
    <System.Serializable()> Public Partial Class Reactor_Polymerization

        Inherits Reactor

        <NonSerialized> <Xml.Serialization.XmlIgnore> Public f As Object

        ' --- configuration ---

        ''' <summary>Name of the monomer compound in the feed.</summary>
        Public Property MonomerID As String = ""

        ''' <summary>Name of the initiator compound in the feed.</summary>
        Public Property InitiatorID As String = ""

        ''' <summary>Name of the solvent or chain-transfer-agent compound (empty for a bulk polymerization).</summary>
        Public Property SolventID As String = ""

        ''' <summary>Name of the polymer product compound (a PC-SAFT pseudo-compound already in the flowsheet).</summary>
        Public Property PolymerID As String = ""

        ''' <summary>Reactor vessel volume (m3).</summary>
        Public Property Volume As Double = 1.0

        ''' <summary>Isothermal operating temperature (K); when zero the feed temperature is used.</summary>
        Public Property IsothermalTemperature As Double = 0.0

        ' --- Arrhenius kinetics (k = A*exp(-E/RT); A in 1/s or L/mol/s, E in J/mol), styrene/AIBN defaults ---
        Public Property Kd_A As Double = 1.58E+15
        Public Property Kd_E As Double = 128000.0
        Public Property Efficiency As Double = 0.6
        Public Property Kp_A As Double = 4.266E+7
        Public Property Kp_E As Double = 32510.0
        Public Property Ktc_A As Double = 1.255E+9
        Public Property Ktc_E As Double = 8000.0
        Public Property Ktd_A As Double = 0.0
        Public Property Ktd_E As Double = 0.0
        Public Property KtrM_A As Double = 4.266E+7 * 6.0E-5
        Public Property KtrM_E As Double = 32510.0
        Public Property KtrS_A As Double = 0.0
        Public Property KtrS_E As Double = 0.0
        Public Property MonomerMolarMass As Double = 104.15

        ' --- results (read-only outputs) ---
        Public Property Conversion As Double = 0.0
        Public Property Mn As Double = 0.0
        Public Property Mw As Double = 0.0
        Public Property PDI As Double = 0.0
        Public Property RateOfPolymerization As Double = 0.0
        Public Property ResidenceTime As Double = 0.0

        Public Sub New()
            MyBase.New()
        End Sub

        Public Sub New(ByVal name As String, ByVal description As String)
            MyBase.New()
            Me.ComponentName = name
            Me.ComponentDescription = description
        End Sub

        Public Overrides Function CloneXML() As Object
            Dim obj As ICustomXMLSerialization = New Reactor_Polymerization()
            obj.LoadData(Me.SaveData)
            Return obj
        End Function

        Public Overrides Function CloneJSON() As Object
            Return Newtonsoft.Json.JsonConvert.DeserializeObject(Of Reactor_Polymerization)(Newtonsoft.Json.JsonConvert.SerializeObject(Me))
        End Function

        Private Function BuildKinetics() As FreeRadicalKinetics
            Return New FreeRadicalKinetics With {
                .Ad = Kd_A, .Ed = Kd_E, .Efficiency = Efficiency,
                .Ap = Kp_A, .Ep = Kp_E,
                .Atc = Ktc_A, .Etc = Ktc_E, .Atd = Ktd_A, .Etd = Ktd_E,
                .AtrM = KtrM_A, .EtrM = KtrM_E, .AtrS = KtrS_A, .EtrS = KtrS_E,
                .MonomerMW = MonomerMolarMass}
        End Function

        ''' <summary>Loads the AIBN-initiated bulk styrene benchmark kinetics into this reactor.</summary>
        Public Sub LoadStyrenePreset()
            Dim k = FreeRadicalKinetics.StyreneAIBN()
            Kd_A = k.Ad : Kd_E = k.Ed : Efficiency = k.Efficiency
            Kp_A = k.Ap : Kp_E = k.Ep
            Ktc_A = k.Atc : Ktc_E = k.Etc : Ktd_A = k.Atd : Ktd_E = k.Etd
            KtrM_A = k.AtrM : KtrM_E = k.EtrM : KtrS_A = k.AtrS : KtrS_E = k.EtrS
            MonomerMolarMass = k.MonomerMW
        End Sub

        Public Overrides Sub Calculate(Optional ByVal args As Object = Nothing)

            If Not Me.GraphicObject.InputConnectors(0).IsAttached Then Throw New Exception("No feed material stream connected.")
            If Not Me.GraphicObject.OutputConnectors(0).IsAttached Then Throw New Exception("No product material stream connected.")
            If String.IsNullOrEmpty(MonomerID) Then Throw New Exception("No monomer compound selected.")
            If String.IsNullOrEmpty(InitiatorID) Then Throw New Exception("No initiator compound selected.")
            If String.IsNullOrEmpty(PolymerID) Then Throw New Exception("No polymer product compound selected.")
            If Volume <= 0.0 Then Throw New Exception("Reactor volume must be greater than zero.")

            Dim ims As MaterialStream = GetInletMaterialStream(0)
            Dim comps = ims.Phases(0).Compounds
            For Each id In {MonomerID, InitiatorID, PolymerID}
                If Not comps.ContainsKey(id) Then Throw New Exception("Compound '" & id & "' is not present in the feed.")
            Next
            Dim hasSolvent = Not String.IsNullOrEmpty(SolventID) AndAlso comps.ContainsKey(SolventID)

            Dim Q As Double = ims.Phases(0).Properties.volumetric_flow.GetValueOrDefault()
            If Q <= 0.0 Then Q = ims.Phases(1).Properties.volumetric_flow.GetValueOrDefault()
            If Q <= 0.0 Then Throw New Exception("Feed volumetric flow is zero; cannot define a residence time.")

            Dim Tr As Double = IsothermalTemperature
            If Tr <= 0.0 Then Tr = ims.Phases(0).Properties.temperature.GetValueOrDefault()
            Dim Pout As Double = ims.Phases(0).Properties.pressure.GetValueOrDefault() - Me.DeltaP.GetValueOrDefault()

            Dim monFlow = comps(MonomerID).MolarFlow.GetValueOrDefault()     ' mol/s
            Dim iniFlow = comps(InitiatorID).MolarFlow.GetValueOrDefault()
            Dim solFlow = If(hasSolvent, comps(SolventID).MolarFlow.GetValueOrDefault(), 0.0)

            ' Concentrations in mol/L (volumetric flow is m3/s).
            Dim Cmon = monFlow / (Q * 1000.0)
            Dim Cini = iniFlow / (Q * 1000.0)
            Dim Csol = solFlow / (Q * 1000.0)
            Dim theta = Volume / Q

            Dim kin = BuildKinetics()
            Dim r = FreeRadicalCSTR.Solve(kin, Tr, theta, Cmon, Cini, Csol)
            If Not r.Converged Then Throw New Exception("The polymerization solver did not converge at these conditions.")

            Conversion = r.Conversion
            Mn = r.Mn : Mw = r.Mw : PDI = r.PDI
            RateOfPolymerization = r.Rp
            ResidenceTime = theta

            ' The polymer product compound carries the computed number-average molar mass, so the reacted
            ' monomer mass is conserved when it leaves as polymer chains.
            comps(PolymerID).ConstantProperties.Molar_Weight = If(r.Mn > 0.0, r.Mn, comps(PolymerID).ConstantProperties.Molar_Weight)

            Dim monConverted = monFlow * r.Conversion
            Dim DP = If(kin.MonomerMW > 0.0, r.Mn / kin.MonomerMW, 0.0)
            Dim chainFlow = If(DP > 0.0, monConverted / DP, 0.0)
            Dim iniRatio = If(Cini > 0.0, r.InitiatorConc / Cini, 1.0)

            ' Outlet molar flows: monomer depleted, initiator partly consumed, polymer produced, rest inert.
            Dim outFlow As New Dictionary(Of String, Double)
            Dim total As Double = 0.0
            For Each c In comps.Values
                Dim fl = c.MolarFlow.GetValueOrDefault()
                If c.Name = MonomerID Then
                    fl = monFlow * (1.0 - r.Conversion)
                ElseIf c.Name = InitiatorID Then
                    fl = iniFlow * iniRatio
                ElseIf c.Name = PolymerID Then
                    fl = c.MolarFlow.GetValueOrDefault() + chainFlow
                End If
                fl = Math.Max(fl, 0.0)
                outFlow(c.Name) = fl
                total += fl
            Next

            Dim W = ims.Phases(0).Properties.massflow.GetValueOrDefault()

            Dim cp = Me.GraphicObject.OutputConnectors(0)
            Dim oms As MaterialStream = FlowSheet.SimulationObjects(cp.AttachedConnector.AttachedTo.Name)
            With oms
                .SpecType = StreamSpec.Temperature_and_Pressure
                .Phases(0).Properties.temperature = Tr
                .Phases(0).Properties.pressure = Pout
                For Each c In .Phases(0).Compounds.Values
                    c.MoleFraction = If(total > 0.0, outFlow(c.Name) / total, 0.0)
                Next
                .Phases(0).Properties.massflow = W
                .DefinedFlow = FlowSpec.Mass
            End With

        End Sub

        Public Overrides Sub DeCalculate()
            Dim cp = Me.GraphicObject.OutputConnectors(0)
            If cp.IsAttached Then
                Dim oms As MaterialStream = FlowSheet.SimulationObjects(cp.AttachedConnector.AttachedTo.Name)
                oms.Clear()
            End If
        End Sub

        Public Overrides Function GetProperties(ByVal proptype As Interfaces.Enums.PropertyType) As String()
            Dim proplist As New List(Of String)
            Select Case proptype
                Case PropertyType.RW, PropertyType.WR
                    proplist.AddRange({"Volume", "Isothermal Temperature"})
                Case Else
                    proplist.AddRange({"Volume", "Isothermal Temperature", "Residence Time", "Conversion",
                                       "Number-Average Molar Mass (Mn)", "Weight-Average Molar Mass (Mw)",
                                       "Polydispersity Index", "Rate of Polymerization"})
            End Select
            Return proplist.ToArray()
        End Function

        Public Overrides Function GetPropertyValue(ByVal prop As String, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As Object
            If su Is Nothing Then su = New SystemsOfUnits.SI
            Select Case prop
                Case "Volume" : Return SystemsOfUnits.Converter.ConvertFromSI(su.volume, Volume)
                Case "Isothermal Temperature" : Return SystemsOfUnits.Converter.ConvertFromSI(su.temperature, IsothermalTemperature)
                Case "Residence Time" : Return SystemsOfUnits.Converter.ConvertFromSI(su.time, ResidenceTime)
                Case "Conversion" : Return Conversion * 100.0
                Case "Number-Average Molar Mass (Mn)" : Return Mn
                Case "Weight-Average Molar Mass (Mw)" : Return Mw
                Case "Polydispersity Index" : Return PDI
                Case "Rate of Polymerization" : Return RateOfPolymerization
                Case Else : Return Nothing
            End Select
        End Function

        Public Overrides Function SetPropertyValue(ByVal prop As String, ByVal propval As Object, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As Boolean
            If su Is Nothing Then su = New SystemsOfUnits.SI
            Select Case prop
                Case "Volume" : Volume = SystemsOfUnits.Converter.ConvertToSI(su.volume, propval) : Return True
                Case "Isothermal Temperature" : IsothermalTemperature = SystemsOfUnits.Converter.ConvertToSI(su.temperature, propval) : Return True
            End Select
            Return False
        End Function

        Public Overrides Function GetPropertyUnit(ByVal prop As String, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As String
            If su Is Nothing Then su = New SystemsOfUnits.SI
            Select Case prop
                Case "Volume" : Return su.volume
                Case "Isothermal Temperature" : Return su.temperature
                Case "Residence Time" : Return su.time
                Case "Conversion" : Return "%"
                Case "Number-Average Molar Mass (Mn)", "Weight-Average Molar Mass (Mw)" : Return "g/mol"
                Case "Rate of Polymerization" : Return "mol/[L.s]"
                Case Else : Return ""
            End Select
        End Function

        Public Overrides Function GetIconBitmapBytes() As Byte()
            Return GetBytesFromResource("DWSIM.UnitOperations.cstr.png")
        End Function

        Public Overrides Function GetDisplayDescription() As String
            Return "Free-radical polymerization reactor (isothermal CSTR, method of moments)"
        End Function

        Public Overrides Function GetDisplayName() As String
            Return "Polymerization Reactor"
        End Function

        Public Overrides ReadOnly Property MobileCompatible As Boolean
            Get
                Return False
            End Get
        End Property

        Public Overrides Function GetReport(su As IUnitsOfMeasure, ci As Globalization.CultureInfo, numberformat As String) As String
            Dim str As New Text.StringBuilder
            str.AppendLine("Polymerization Reactor: " & Me.GraphicObject.Tag)
            str.AppendLine()
            str.AppendLine("Monomer: " & MonomerID & "   Initiator: " & InitiatorID &
                           If(String.IsNullOrEmpty(SolventID), "", "   Solvent/CTA: " & SolventID))
            str.AppendLine("Polymer product: " & PolymerID)
            str.AppendLine()
            str.AppendLine("Residence time: " & SystemsOfUnits.Converter.ConvertFromSI(su.time, ResidenceTime).ToString(numberformat, ci) & " " & su.time)
            str.AppendLine("Conversion: " & (Conversion * 100.0).ToString(numberformat, ci) & " %")
            str.AppendLine("Number-average molar mass (Mn): " & Mn.ToString(numberformat, ci) & " g/mol")
            str.AppendLine("Weight-average molar mass (Mw): " & Mw.ToString(numberformat, ci) & " g/mol")
            str.AppendLine("Polydispersity index (Mw/Mn): " & PDI.ToString(numberformat, ci))
            Return str.ToString()
        End Function

    End Class

End Namespace
