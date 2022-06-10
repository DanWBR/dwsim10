Imports System.Xml.Serialization
Imports System.Runtime.Serialization.Formatters.Binary
Imports System.Runtime.Serialization
Imports System.IO
Imports System.Reflection
Imports System.Globalization
Imports System.Dynamic
Imports System.Text.RegularExpressions

Namespace BaseClasses

    <System.Serializable()> Public Class Compound

        Implements ICustomXMLSerialization, CapeOpen.ICapeIdentification, ICompound

        Public Sub New(ByVal name As String, ByVal description As String)

            Me.Name = name
            Me.ComponentName = name
            Me.ComponentDescription = description

        End Sub

        Public Overrides Function ToString() As String

            Return Name + String.Format(": xm = {0}, xw = {1}, M = {2} mol/s, W = {3} kg/s", MoleFraction.GetValueOrDefault, MassFraction.GetValueOrDefault, MolarFlow.GetValueOrDefault, MassFlow.GetValueOrDefault)

        End Function

        Public Function LoadData(data As System.Collections.Generic.List(Of System.Xml.Linq.XElement)) As Boolean Implements ICustomXMLSerialization.LoadData

            XMLSerializer.Deserialize(Me, data)

            ExtraProperties = New ExpandoObject

            Dim xel_d = (From xel2 As XElement In data Select xel2 Where xel2.Name = "DynamicProperties")

            If Not xel_d Is Nothing Then
                Dim dataDyn As List(Of XElement) = xel_d.Elements.ToList
                For Each xel As XElement In dataDyn
                    Try
                        Dim propname = xel.Element("Name").Value
                        Dim proptype = xel.Element("PropertyType").Value
                        Dim ptype As Type = Type.GetType(proptype)
                        Dim propval = Newtonsoft.Json.JsonConvert.DeserializeObject(xel.Element("Data").Value, ptype)
                        DirectCast(ExtraProperties, IDictionary(Of String, Object))(propname) = propval
                    Catch ex As Exception
                    End Try
                Next
            End If

            Return True

        End Function

        Public Function SaveData() As System.Collections.Generic.List(Of System.Xml.Linq.XElement) Implements ICustomXMLSerialization.SaveData

            Dim elements As System.Collections.Generic.List(Of System.Xml.Linq.XElement) = XMLSerializer.Serialize(Me)

            elements.Add(New XElement("DynamicProperties"))
            Dim extraprops = DirectCast(ExtraProperties, IDictionary(Of String, Object))
            For Each item In extraprops
                Try
                    elements.Item(elements.Count - 1).Add(New XElement("Property", {New XElement("Name", item.Key),
                                                                           New XElement("PropertyType", item.Value.GetType.ToString),
                                                                           New XElement("Data", Newtonsoft.Json.JsonConvert.SerializeObject(item.Value))}))
                Catch ex As Exception
                End Try
            Next

            Return elements

        End Function

        Public Property ComponentDescription As String = "" Implements CapeOpen.ICapeIdentification.ComponentDescription

        Public Property ComponentName As String = "" Implements CapeOpen.ICapeIdentification.ComponentName

        Public Property ActivityCoeff As Double? = 0.0# Implements ICompound.ActivityCoeff

        Public Property PetroleumFraction As Boolean Implements ICompound.PetroleumFraction

        Public Property MassFraction As Double? = 0.0# Implements ICompound.MassFraction

        Public Property MoleFraction As Double? = 0.0# Implements ICompound.MoleFraction

        Public Property Molarity As Double? = 0.0# Implements ICompound.Molarity

        Public Property Molality As Double? = 0.0# Implements ICompound.Molality

        Public Property FugacityCoeff As Double? = 0.0# Implements ICompound.FugacityCoeff

        Public Property Kvalue As Double = 0.0# Implements ICompound.Kvalue

        Public Property lnKvalue As Double = 0.0# Implements ICompound.lnKvalue

        Public Property MassFlow As Double? = 0.0# Implements ICompound.MassFlow

        Public Property MolarFlow As Double? = 0.0# Implements ICompound.MolarFlow

        Public Property Name As String = "" Implements ICompound.Name

        Public Property PartialPressure As Double? = 0.0# Implements ICompound.PartialPressure

        Public Property PartialVolume As Double? = 0.0# Implements ICompound.PartialVolume

        Public Property VolumetricFlow As Double? = 0.0# Implements ICompound.VolumetricFlow

        Public Property VolumetricFraction As Double? = 0.0# Implements ICompound.VolumetricFraction

        <XmlIgnore> Public Property ConstantProperties As ICompoundConstantProperties = New ConstantProperties Implements ICompound.ConstantProperties

        Public Property DiffusionCoefficient As Double? Implements ICompound.DiffusionCoefficient

        Public Property ExtraProperties As New ExpandoObject Implements ICompound.ExtraProperties

        Public Property EnthalpyF_Dmol As Double? = 0.0 Implements ICompound.EnthalpyF_Dmol

        Public Property EntropyF_Dmol As Double? = 0.0 Implements ICompound.EntropyF_Dmol

    End Class

    <System.Serializable()> Public Class Phase

        Implements ICustomXMLSerialization

        Implements IPhase

        Public Property Properties As New PhaseProperties

        Public Sub New(ByVal name As String, ByVal description As String)

            Me.Name = name
            Me.ComponentName = name
            Me.ComponentDescription = description

        End Sub

        Public Overrides Function ToString() As String
            If Name <> "" Then
                Return Name
            Else
                Return MyBase.ToString()
            End If
        End Function

        Public Function LoadData(data As System.Collections.Generic.List(Of System.Xml.Linq.XElement)) As Boolean Implements ICustomXMLSerialization.LoadData

            XMLSerializer.Deserialize(Me, data)

            Dim datac As List(Of XElement) = (From xel As XElement In data Select xel Where xel.Name = "Compounds").Elements.ToList

            For Each xel As XElement In datac
                Dim s As New Compound("", "")
                s.LoadData(xel.Elements.ToList)
                Me.Compounds.Add(s.Name, s)
            Next

            If (From xel As XElement In data Select xel Where xel.Name = "SPMProperties").Count > 0 Then
                ' DWSIM 3
                XMLSerializer.Deserialize(Me.Properties, (From xel As XElement In data Select xel Where xel.Name = "SPMProperties").Elements.ToList)
            Else
                ' DWSIM 4
                XMLSerializer.Deserialize(Me.Properties, (From xel As XElement In data Select xel Where xel.Name = "Properties").Elements.ToList)
            End If

        End Function

        Public Function SaveData() As System.Collections.Generic.List(Of System.Xml.Linq.XElement) Implements ICustomXMLSerialization.SaveData

            Dim elements As New List(Of System.Xml.Linq.XElement)
            Dim ci As CultureInfo = CultureInfo.InvariantCulture

            With elements

                .Add(New XElement("Compounds"))

                For Each kvp As KeyValuePair(Of String, ICompound) In Me.Compounds
                    elements(elements.Count - 1).Add(New XElement("Compound", DirectCast(kvp.Value, ICustomXMLSerialization).SaveData().ToArray()))
                Next

                Dim props As PropertyInfo() = Me.GetType.GetProperties()
                For Each fi As PropertyInfo In props
                    If TypeOf Me.GetType.GetProperty(fi.Name).GetValue(Me, Nothing) Is Double Then
                        .Add(New XElement(fi.Name, Double.Parse(Me.GetType.GetProperty(fi.Name).GetValue(Me, Nothing).ToString()).ToString(ci)))
                    Else
                        .Add(New XElement(fi.Name, Me.GetType.GetProperty(fi.Name).GetValue(Me, Nothing).ToString()))
                    End If
                Next

                .Add(New XElement("Properties"))
                elements(elements.Count - 1).Add(XMLSerializer.Serialize(Me.Properties))

            End With

            Return elements

        End Function

        Public Property ComponentDescription As String = "" Implements IPhase.ComponentDescription

        Public Property ComponentName As String = "" Implements IPhase.ComponentName

        Public Property Compounds As Dictionary(Of String, ICompound) = New Dictionary(Of String, ICompound) Implements IPhase.Compounds

        Public Property Name As String = "" Implements IPhase.Name

        Public ReadOnly Property Properties1 As IPhaseProperties Implements IPhase.Properties
            Get
                Return Properties
            End Get
        End Property

    End Class

#Region "Subclasses"

    <System.Serializable()> Public Class PhaseProperties

        Implements IPhaseProperties

        Public Sub New()

        End Sub

        Public Property activity As Double? Implements IPhaseProperties.activity

        Public Property activityCoefficient As Double? Implements IPhaseProperties.activityCoefficient

        Public Property bubblePressure As Double? Implements IPhaseProperties.bubblePressure

        Public Property bubbleTemperature As Double? Implements IPhaseProperties.bubbleTemperature

        Public Property compressibility As Double? Implements IPhaseProperties.compressibility

        Public Property compressibilityFactor As Double? Implements IPhaseProperties.compressibilityFactor

        Public Property density As Double? Implements IPhaseProperties.density

        Public Property dewPressure As Double? Implements IPhaseProperties.dewPressure

        Public Property dewTemperature As Double? Implements IPhaseProperties.dewTemperature

        Public Property enthalpy As Double? Implements IPhaseProperties.enthalpy

        Public Property enthalpyF As Double? Implements IPhaseProperties.enthalpyF

        Public Property entropy As Double? Implements IPhaseProperties.entropy

        Public Property entropyF As Double? Implements IPhaseProperties.entropyF

        Public Property excessEnthalpy As Double? Implements IPhaseProperties.excessEnthalpy

        Public Property excessEntropy As Double? Implements IPhaseProperties.excessEntropy

        Public Property freezingPoint As Double? Implements IPhaseProperties.freezingPoint

        Public Property freezingPointDepression As Double? Implements IPhaseProperties.freezingPointDepression

        Public Property fugacity As Double? Implements IPhaseProperties.fugacity

        Public Property fugacityCoefficient As Double? Implements IPhaseProperties.fugacityCoefficient

        Public Property heatCapacityCp As Double? Implements IPhaseProperties.heatCapacityCp

        Public Property heatCapacityCv As Double? Implements IPhaseProperties.heatCapacityCv

        Public Property ionicStrength As Double? Implements IPhaseProperties.ionicStrength

        Public Property jouleThomsonCoefficient As Double? Implements IPhaseProperties.jouleThomsonCoefficient

        Public Property kinematic_viscosity As Double? Implements IPhaseProperties.kinematic_viscosity

        Public Property kvalue As Double? Implements IPhaseProperties.kvalue

        Public Property logFugacityCoefficient As Double? Implements IPhaseProperties.logFugacityCoefficient

        Public Property logKvalue As Double? Implements IPhaseProperties.logKvalue

        Public Property mean_ionic_acitivty_coefficient As Double? Implements IPhaseProperties.mean_ionic_acitivty_coefficient

        Public Property massflow As Double? Implements IPhaseProperties.massflow

        Public Property massfraction As Double? Implements IPhaseProperties.massfraction

        Public Property molar_enthalpy As Double? Implements IPhaseProperties.molar_enthalpy

        Public Property molar_enthalpyF As Double? Implements IPhaseProperties.molar_enthalpyF

        Public Property molar_entropy As Double? Implements IPhaseProperties.molar_entropy

        Public Property molar_entropyF As Double? Implements IPhaseProperties.molar_entropyF

        Public Property molarflow As Double? Implements IPhaseProperties.molarflow

        Public Property molarfraction As Double? Implements IPhaseProperties.molarfraction

        Public Property molecularWeight As Double? Implements IPhaseProperties.molecularWeight

        Public Property osmoticCoefficient As Double? Implements IPhaseProperties.osmoticCoefficient

        Public Property pH As Double? Implements IPhaseProperties.pH

        Public Property pressure As Double? Implements IPhaseProperties.pressure

        Public Property speedOfSound As Double? Implements IPhaseProperties.speedOfSound

        Public Property surfaceTension As Double? Implements IPhaseProperties.surfaceTension

        Public Property temperature As Double? Implements IPhaseProperties.temperature

        Public Property thermalConductivity As Double? Implements IPhaseProperties.thermalConductivity

        Public Property viscosity As Double? Implements IPhaseProperties.viscosity

        Public Property volumetric_flow As Double? Implements IPhaseProperties.volumetric_flow

        Public Property bulk_modulus As Double? Implements IPhaseProperties.bulk_modulus

        Public Property gibbs_free_energy As Double? Implements IPhaseProperties.gibbs_free_energy

        Public Property helmholtz_energy As Double? Implements IPhaseProperties.helmholtz_energy

        Public Property internal_energy As Double? Implements IPhaseProperties.internal_energy

        Public Property isothermal_compressibility As Double? Implements IPhaseProperties.isothermal_compressibility

        Public Property molar_gibbs_free_energy As Double? Implements IPhaseProperties.molar_gibbs_free_energy

        Public Property molar_helmholtz_energy As Double? Implements IPhaseProperties.molar_helmholtz_energy

        Public Property molar_internal_energy As Double? Implements IPhaseProperties.molar_internal_energy

        Public Property idealGasHeatCapacityCp As Double? Implements IPhaseProperties.idealGasHeatCapacityCp

        Public Property idealGasHeatCapacityRatio As Double? Implements IPhaseProperties.idealGasHeatCapacityRatio

    End Class

    <System.Serializable()> Public Class InteractionParameter

        Implements ICloneable, ICustomXMLSerialization
        Public Comp1 As String = ""
        Public Comp2 As String = ""
        Public Model As String = ""
        Public DataType As String = ""
        Public Description As String = ""
        Public RegressionFile As String = ""
        Public Parameters As Dictionary(Of String, Object)

        Public Sub New()
            Parameters = New Dictionary(Of String, Object)
        End Sub

        Public Function Clone() As Object Implements System.ICloneable.Clone
            Return ObjectCopy(Me)
        End Function

        Function ObjectCopy(ByVal obj As InteractionParameter) As InteractionParameter

            Dim objMemStream As New MemoryStream(50000)
            Dim objBinaryFormatter As New BinaryFormatter(Nothing, New StreamingContext(StreamingContextStates.Clone))

            objBinaryFormatter.Serialize(objMemStream, obj)

            objMemStream.Seek(0, SeekOrigin.Begin)

            ObjectCopy = objBinaryFormatter.Deserialize(objMemStream)

            objMemStream.Close()

        End Function

        Public Function LoadData(data As System.Collections.Generic.List(Of System.Xml.Linq.XElement)) As Boolean Implements ICustomXMLSerialization.LoadData

            XMLSerializer.Deserialize(Me, data, True)
            Return True

        End Function

        Public Function SaveData() As System.Collections.Generic.List(Of System.Xml.Linq.XElement) Implements ICustomXMLSerialization.SaveData

            Dim elements As List(Of System.Xml.Linq.XElement) = XMLSerializer.Serialize(Me, True)
            Dim ci As CultureInfo = CultureInfo.InvariantCulture

            Return elements

        End Function

    End Class

    <System.Serializable()> Public Class ConstantProperties

        Implements ICloneable, ICustomXMLSerialization, ICompoundConstantProperties

        Public Sub New()

        End Sub

        Public Overrides Function ToString() As String

            Return Name + String.Format(": {0}, {1}, NBP = {2} K, Tc = {3} K, Pc = {4} Pa, AF = {5}", Formula, CAS_Number, NBP, Critical_Temperature, Critical_Pressure, Acentric_Factor)

        End Function

        Public Sub UpdateElements()

            Dim el As New Dictionary(Of String, Double)

            Dim _molecule = Formula

            Dim useParenthesis As Boolean = Regex.IsMatch(_molecule, "[A-Z][a-z]?\d*\((([A-Z][a-z]?\d*){1,2})\)\d*")
            Dim findMatches = Regex.Matches(_molecule, "\(?[A-Z][a-z]?\d*\)?")

            ' Get all elements

            If useParenthesis Then
                Dim strval = If(Regex.IsMatch(_molecule, "\)\d+"), Regex.Match(_molecule, "\)\d+").Value.Remove(0, 1), "1")
                Dim endNumber As Double = Double.Parse(strval, NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture)
                ' Finds the number after the ')'
                For Each i As Match In findMatches
                    Dim element As String = Regex.Match(i.Value, "[A-Z][a-z]?").Value
                    ' Gets the element
                    Dim amountOfElement As Double = 0
                    If Regex.IsMatch(i.Value, "[\(\)]") Then
                        If Not Double.TryParse(Regex.Replace(i.Value, "(\(|\)|[A-Z]|[a-z])", ""), amountOfElement) Then
                            ' If the element has either '(' or ')' and doesn't specify an amount, then set it equal to the endnumber
                            amountOfElement = endNumber
                        Else
                            ' If the element has either '(' or ')' and specifies an amount, then multiply it by the end number
                            amountOfElement = amountOfElement * endNumber
                        End If
                    Else
                        amountOfElement = Double.Parse(If(String.IsNullOrWhiteSpace(i.Value.Replace(element, "")), "1", i.Value.Replace(element, "")))
                    End If
                    If el.ContainsKey(element) Then
                        el(element) += amountOfElement
                    Else
                        el.Add(element, amountOfElement)
                    End If
                Next
            Else
                Dim elementRegex = "([A-Z][a-z]*)([0-9]*)"
                Dim validateRegex = "^(" + elementRegex + ")+$"
                For Each match In Regex.Matches(_molecule, elementRegex)
                    Dim name = match.Groups(1).Value
                    Dim count = If(match.Groups(2).Value <> "", Integer.Parse(match.Groups(2).Value), 1)
                    If el.ContainsKey(name) Then
                        el(name) += count
                    Else
                        el.Add(name, count)
                    End If
                Next
            End If

            Elements = New SortedList()

            For Each item In el
                Elements.Add(item.Key, item.Value)
            Next

        End Sub

        Public Function Clone() As Object Implements System.ICloneable.Clone

            Dim comp = ObjectCopy(Me)

            comp.ExtraProperties = New ExpandoObject()

            Return comp

        End Function

        Function ObjectCopy(ByVal obj As ConstantProperties) As ConstantProperties

            Dim objMemStream As New MemoryStream(50000)
            Dim objBinaryFormatter As New BinaryFormatter(Nothing, New StreamingContext(StreamingContextStates.Clone))

            objBinaryFormatter.Serialize(objMemStream, obj)

            objMemStream.Seek(0, SeekOrigin.Begin)

            ObjectCopy = objBinaryFormatter.Deserialize(objMemStream)

            objMemStream.Close()

        End Function

        Public Function LoadData(data As System.Collections.Generic.List(Of System.Xml.Linq.XElement)) As Boolean Implements ICustomXMLSerialization.LoadData

            XMLSerializer.Deserialize(Me, data)

            ExtraProperties = New ExpandoObject

            Dim xel_d = (From xel2 As XElement In data Select xel2 Where xel2.Name = "DynamicProperties")

            If Not xel_d Is Nothing Then
                Dim dataDyn As List(Of XElement) = xel_d.Elements.ToList
                For Each xel As XElement In dataDyn
                    Try
                        Dim propname = xel.Element("Name").Value
                        Dim proptype = xel.Element("PropertyType").Value
                        Dim ptype As Type = Type.GetType(proptype)
                        Dim propval = Newtonsoft.Json.JsonConvert.DeserializeObject(xel.Element("Data").Value, ptype)
                        DirectCast(ExtraProperties, IDictionary(Of String, Object))(propname) = propval
                    Catch ex As Exception
                    End Try
                Next
            End If

            Dim unif As New PropertyPackages.Auxiliary.Unifac
            Dim modf As New PropertyPackages.Auxiliary.Modfac

            For Each xel2 As XElement In (From xel As XElement In data Select xel Where xel.Name = "UNIFACGroups").Elements
                If xel2.@Name Is Nothing Then
                    Me.UNIFACGroups.Add(xel2.@GroupID.ToString, xel2.@Value)
                Else
                    Dim id As Integer = unif.Group2ID(xel2.@Name)
                    Me.UNIFACGroups.Add(id.ToString, xel2.@Value)
                End If
            Next

            For Each xel2 As XElement In (From xel As XElement In data Select xel Where xel.Name = "MODFACGroups").Elements
                If xel2.@Name Is Nothing Then
                    Me.MODFACGroups.Add(xel2.@GroupID.ToString, xel2.@Value)
                Else
                    Dim id As Integer = modf.Group2ID(xel2.@Name)
                    Me.MODFACGroups.Add(id.ToString, xel2.@Value)
                End If
            Next

            For Each xel2 As XElement In (From xel As XElement In data Select xel Where xel.Name = "NISTMODFACGroups").Elements
                Me.NISTMODFACGroups.Add(xel2.@GroupID.ToString, xel2.@Value)
            Next

            For Each xel2 As XElement In (From xel As XElement In data Select xel Where xel.Name = "Elements").Elements
                If Not Me.Elements.ContainsKey(xel2.@Name) Then
                    Me.Elements.Add(xel2.@Name, xel2.@Value)
                End If
            Next

            unif = Nothing
            modf = Nothing

            Return True

        End Function

        Public Function SaveData() As System.Collections.Generic.List(Of System.Xml.Linq.XElement) Implements ICustomXMLSerialization.SaveData

            Dim xelements As List(Of System.Xml.Linq.XElement) = XMLSerializer.Serialize(Me)
            Dim ci As CultureInfo = CultureInfo.InvariantCulture

            With xelements

                .Add(New XElement("DynamicProperties"))
                Dim extraprops = DirectCast(ExtraProperties, IDictionary(Of String, Object))
                For Each item In extraprops
                    Try
                        .Item(.Count - 1).Add(New XElement("Property", {New XElement("Name", item.Key),
                                                                               New XElement("PropertyType", item.Value.GetType.ToString),
                                                                               New XElement("Data", Newtonsoft.Json.JsonConvert.SerializeObject(item.Value))}))
                    Catch ex As Exception
                    End Try
                Next

                .Add(New XElement("UNIFACGroups"))

                If Not UNIFACGroups Is Nothing Then

                    For Each key As String In UNIFACGroups.Keys
                        .Item(xelements.Count - 1).Add(New XElement("Item", New XAttribute("GroupID", key), New XAttribute("Value", UNIFACGroups(key.ToString))))
                    Next

                End If

                .Add(New XElement("MODFACGroups"))

                If Not MODFACGroups Is Nothing Then

                    For Each key As String In MODFACGroups.Keys
                        .Item(xelements.Count - 1).Add(New XElement("Item", New XAttribute("GroupID", key), New XAttribute("Value", MODFACGroups(key.ToString))))
                    Next

                End If

                .Add(New XElement("NISTMODFACGroups"))

                If Not MODFACGroups Is Nothing Then

                    For Each key As String In NISTMODFACGroups.Keys
                        .Item(xelements.Count - 1).Add(New XElement("Item", New XAttribute("GroupID", key), New XAttribute("Value", NISTMODFACGroups(key.ToString))))
                    Next

                End If

                .Add(New XElement("Elements"))

                If Not Me.Elements Is Nothing Then

                    For Each key As String In Me.Elements.Keys
                        .Item(xelements.Count - 1).Add(New XElement("Item", New XAttribute("Name", key), New XAttribute("Value", Me.Elements(key))))
                    Next

                End If

            End With

            Return xelements

        End Function

        Public Function ExportToJSON() As String Implements ICompoundConstantProperties.ExportToJSON

            Return Newtonsoft.Json.JsonConvert.SerializeObject(Me, Newtonsoft.Json.Formatting.Indented)

        End Function

        Public Sub ImportFromJSON(data As String) Implements ICompoundConstantProperties.ImportFromJSON

            Dim obj = Newtonsoft.Json.JsonConvert.DeserializeObject(Of ConstantProperties)(data)

            Me.LoadData(obj.SaveData())

        End Sub

        Public Function GetVaporPressure(T As Double, Optional ByRef message As String = "") As Double Implements ICompoundConstantProperties.GetVaporPressure

            If IsPF = 1 Then
                message = "Estimated using Lee-Kesler correlation."
                Return PropertyPackages.Auxiliary.PROPS.Pvp_leekesler(T, Critical_Temperature, Critical_Pressure, Acentric_Factor)
            Else
                If OriginalDB = "DWSIM" Or
                    OriginalDB = "" Then
                    Dim A, B, C, D, E, result As Double
                    A = Vapor_Pressure_Constant_A
                    B = Vapor_Pressure_Constant_B
                    C = Vapor_Pressure_Constant_C
                    D = Vapor_Pressure_Constant_D
                    E = Vapor_Pressure_Constant_E
                    message = "Calculated using Experimental/Regressed data."
                    result = Math.Exp(A + B / T + C * Math.Log(T) + D * T ^ E)
                    Return result
                ElseIf OriginalDB = "CheResources" Then
                    Dim A, B, C, result As Double
                    A = Vapor_Pressure_Constant_A
                    B = Vapor_Pressure_Constant_B
                    C = Vapor_Pressure_Constant_C
                    '[LN(P)=A-B/(T+C), P(mmHG) T(K)]
                    message = "Calculated using Experimental/Regressed data."
                    result = Math.Exp(A - B / (T + C)) * 133.322368 'mmHg to Pascal
                    Return result
                ElseIf OriginalDB = "ChemSep" Or
                OriginalDB = "CoolProp" Or
                OriginalDB = "User" Or
                OriginalDB = "ChEDL Thermo" Or
                OriginalDB = "KDB" Then
                    Dim A, B, C, D, E, result As Double
                    Dim eqno As String = VaporPressureEquation
                    Dim mw As Double = Molar_Weight
                    A = Vapor_Pressure_Constant_A
                    B = Vapor_Pressure_Constant_B
                    C = Vapor_Pressure_Constant_C
                    D = Vapor_Pressure_Constant_D
                    E = Vapor_Pressure_Constant_E
                    '<vp_c name="Vapour pressure"  units="Pa" >
                    If eqno = "0" Then
                        message = "Estimated using Lee-Kesler correlation."
                        Return PropertyPackages.Auxiliary.PROPS.Pvp_leekesler(T, Critical_Temperature, Critical_Pressure, Acentric_Factor)
                    Else
                        If Integer.TryParse(eqno, New Integer) Then
                            message = "Calculated using Experimental/Regressed data."
                            result = PropertyPackages.PropertyPackage.CalcCSTDepProp(eqno, A, B, C, D, E, T, 0) 'Pa
                        Else
                            If eqno = "" Then
                                message = "Estimated using Lee-Kesler correlation."
                                Return PropertyPackages.Auxiliary.PROPS.Pvp_leekesler(T, Critical_Temperature, Critical_Pressure, Acentric_Factor)
                            Else
                                message = "Calculated using Experimental/Regressed data."
                                result = PropertyPackages.PropertyPackage.ParseEquation(eqno, A, B, C, D, E, T) 'Pa
                            End If
                        End If
                    End If
                    Return result
                ElseIf OriginalDB = "Biodiesel" Then
                    Dim A, B, C, D, E, result As Double
                    Dim eqno As String = VaporPressureEquation
                    A = Vapor_Pressure_Constant_A
                    B = Vapor_Pressure_Constant_B
                    C = Vapor_Pressure_Constant_C
                    D = Vapor_Pressure_Constant_D
                    E = Vapor_Pressure_Constant_E
                    result = PropertyPackages.PropertyPackage.CalcCSTDepProp(eqno, A, B, C, D, E, T, 0) 'kPa
                    message = "Calculated using Experimental/Regressed data."
                    Return result * 1000
                Else
                    message = "Estimated using Lee-Kesler correlation."
                    Return PropertyPackages.Auxiliary.PROPS.Pvp_leekesler(T, Critical_Temperature, Critical_Pressure, Acentric_Factor)
                End If
            End If

        End Function

        Public Function GetIdealGasHeatCapacity(T As Double, Optional ByRef message As String = "") As Double Implements ICompoundConstantProperties.GetIdealGasHeatCapacity

            Dim db As String = OriginalDB

            If IsPF = 1 Then

                message = "Estimated using Lee-Kesler correlation."
                Return PropertyPackages.Auxiliary.PROPS.Cpig_lk(PF_Watson_K, Acentric_Factor, T) '* .Molar_Weight

            Else

                If db = "DWSIM" Or db = "" Then
                    Dim A, B, C, D, E, result As Double
                    A = Ideal_Gas_Heat_Capacity_Const_A
                    B = Ideal_Gas_Heat_Capacity_Const_B
                    C = Ideal_Gas_Heat_Capacity_Const_C
                    D = Ideal_Gas_Heat_Capacity_Const_D
                    E = Ideal_Gas_Heat_Capacity_Const_E
                    'Cp = A + B*T + C*T^2 + D*T^3 + E*T^4 where Cp in kJ/kg-mol , T in K 
                    message = "Calculated using Experimental/Regressed data."
                    result = A + B * T + C * T ^ 2 + D * T ^ 3 + E * T ^ 4
                    Return result / Molar_Weight 'kJ/kg.K
                ElseIf db = "CheResources" Then
                    Dim A, B, C, D, E, result As Double
                    A = Ideal_Gas_Heat_Capacity_Const_A
                    B = Ideal_Gas_Heat_Capacity_Const_B
                    C = Ideal_Gas_Heat_Capacity_Const_C
                    D = Ideal_Gas_Heat_Capacity_Const_D
                    E = Ideal_Gas_Heat_Capacity_Const_E
                    'CAL/MOL.K [CP=A+(B*T)+(C*T^2)+(D*T^3)], T in K
                    message = "Calculated using Experimental/Regressed data."
                    result = A + B * T + C * T ^ 2 + D * T ^ 3
                    Return result / Molar_Weight * 4.1868 'kJ/kg.K
                ElseIf db = "ChemSep" Or db = "ChEDL Thermo" Or db = "User" Then
                    Dim A, B, C, D, E, result As Double
                    Dim eqno As String = IdealgasCpEquation
                    Dim mw As Double = Molar_Weight
                    A = Ideal_Gas_Heat_Capacity_Const_A
                    B = Ideal_Gas_Heat_Capacity_Const_B
                    C = Ideal_Gas_Heat_Capacity_Const_C
                    D = Ideal_Gas_Heat_Capacity_Const_D
                    E = Ideal_Gas_Heat_Capacity_Const_E
                    message = "Calculated using Experimental/Regressed data."
                    If Integer.TryParse(eqno, New Integer) Then
                        result = PropertyPackages.PropertyPackage.CalcCSTDepProp(eqno, A, B, C, D, E, T, 0) / 1000 / mw 'kJ/kg.K
                    Else
                        result = PropertyPackages.PropertyPackage.ParseEquation(eqno, A, B, C, D, E, T) / mw
                    End If
                    If result = 0.0 Then
                        message = "Couldn't calculate Ideal Gas Cp."
                    End If
                    Return result
                ElseIf db = "ChEDL Thermo" Then
                    Dim A, B, C, D, E, result As Double
                    Dim eqno As String = IdealgasCpEquation
                    A = Ideal_Gas_Heat_Capacity_Const_A
                    B = Ideal_Gas_Heat_Capacity_Const_B
                    C = Ideal_Gas_Heat_Capacity_Const_C
                    D = Ideal_Gas_Heat_Capacity_Const_D
                    E = Ideal_Gas_Heat_Capacity_Const_E
                    message = "Calculated using Experimental/Regressed data."
                    result = PropertyPackages.PropertyPackage.CalcCSTDepProp(eqno, A, B, C, D, E, T, 0) 'kJ/kg.K
                    Return result
                ElseIf db = "CoolProp" Then
                    Dim A, B, C, D, E, result As Double
                    Dim eqno As String = IdealgasCpEquation
                    Dim mw As Double = Molar_Weight
                    A = Ideal_Gas_Heat_Capacity_Const_A
                    B = Ideal_Gas_Heat_Capacity_Const_B
                    C = Ideal_Gas_Heat_Capacity_Const_C
                    D = Ideal_Gas_Heat_Capacity_Const_D
                    E = Ideal_Gas_Heat_Capacity_Const_E
                    message = "Calculated using Experimental/Regressed data."
                    result = PropertyPackages.PropertyPackage.CalcCSTDepProp(eqno, A, B, C, D, E, T, 0) 'kJ/kg.K
                    Return result
                ElseIf db = "Biodiesel" Then
                    Dim A, B, C, D, E, result As Double
                    Dim eqno As String = IdealgasCpEquation
                    A = Ideal_Gas_Heat_Capacity_Const_A
                    B = Ideal_Gas_Heat_Capacity_Const_B
                    C = Ideal_Gas_Heat_Capacity_Const_C
                    D = Ideal_Gas_Heat_Capacity_Const_D
                    E = Ideal_Gas_Heat_Capacity_Const_E
                    message = "Calculated using Experimental/Regressed data."
                    result = PropertyPackages.PropertyPackage.CalcCSTDepProp(eqno, A, B, C, D, E, T, 0) 'kJ/kg.K
                    Return result
                ElseIf db = "KDB" Then
                    Dim A, B, C, D, E As Double
                    Dim eqno As String = IdealgasCpEquation
                    A = Ideal_Gas_Heat_Capacity_Const_A
                    B = Ideal_Gas_Heat_Capacity_Const_B
                    C = Ideal_Gas_Heat_Capacity_Const_C
                    D = Ideal_Gas_Heat_Capacity_Const_D
                    E = Ideal_Gas_Heat_Capacity_Const_E
                    Dim mw As Double = Molar_Weight
                    message = "Calculated using Experimental/Regressed data."
                    Return PropertyPackages.PropertyPackage.ParseEquation(eqno, A, B, C, D, E, T) / mw
                Else
                    message = "Couldn't calculate Ideal Gas Cp."
                    Return 0.0
                End If

            End If


        End Function

        Public Function GetEnthalpyOfVaporization(T As Double, Optional ByRef message As String = "") As Double Implements ICompoundConstantProperties.GetEnthalpyOfVaporization

            Dim A, B, C, D, E, Tr, result As Double
            A = HVap_A
            B = HVap_B
            C = HVap_C
            D = HVap_D
            E = HVap_E

            Tr = T / Critical_Temperature

            If Tr >= 1 Then Return 0.0#

            If OriginalDB = "DWSIM" Or OriginalDB = "" Then
                message = "Calculated using Experimental/Regressed data."
                If IsHYPO = 1 Or
                IsPF = 1 Then
                    Dim tr1 As Double
                    tr1 = Normal_Boiling_Point / Critical_Temperature
                    result = HVap_A * ((1 - Tr) / (1 - tr1)) ^ 0.375
                    Return result 'kJ/kg
                Else
                    result = A * (1 - Tr) ^ (B + C * Tr + D * Tr ^ 2)
                    Return result / Molar_Weight / 1000 'kJ/kg
                End If
            ElseIf OriginalDB = "CheResources" Or OriginalDB = "CoolProp" Or OriginalDB = "User" Or OriginalDB = "KDB" Then
                Dim tr1 As Double
                If OriginalDB = "KDB" Then
                    tr1 = HVap_B / Critical_Temperature
                Else
                    tr1 = Normal_Boiling_Point / Critical_Temperature
                End If
                If HVap_A = 0.0# Then
                    message = "Estimated using Vetere correlation."
                    HVap_A = New Utilities.Hypos.Methods.HYP().DHvb_Vetere(Critical_Temperature, Critical_Pressure, Normal_Boiling_Point)
                    HVap_A /= Molar_Weight
                End If
                result = HVap_A * ((1 - Tr) / (1 - tr1)) ^ 0.375
                Return result 'kJ/kg
            ElseIf OriginalDB = "ChemSep" Then
                Dim eqno As String = VaporizationEnthalpyEquation
                message = "Calculated using Experimental/Regressed data."
                result = PropertyPackages.PropertyPackage.CalcCSTDepProp(eqno, A, B, C, D, E, T, T / Tr) / Molar_Weight / 1000 'kJ/kg
                Return result
            ElseIf OriginalDB = "ChEDL Thermo" Then
                Dim eqno As String = VaporizationEnthalpyEquation
                message = "Calculated using Experimental/Regressed data."
                result = PropertyPackages.PropertyPackage.CalcCSTDepProp(eqno, A, B, C, D, E, T, T / Tr) 'kJ/kg
                Return result
            Else

            End If

        End Function

        Public Function GetVaporViscosity(T As Double, Optional ByRef message As String = "") As Double Implements ICompoundConstantProperties.GetVaporViscosity

            Dim val As Double

            If VaporViscosityEquation <> "" And VaporViscosityEquation <> "0" And Not IsIon And Not IsSalt Then
                message = "Calculated using Experimental/Regressed data."
                If Integer.TryParse(VaporViscosityEquation, New Integer) Then
                    val = PropertyPackages.PropertyPackage.CalcCSTDepProp(VaporViscosityEquation, Vapor_Viscosity_Const_A, Vapor_Viscosity_Const_B, Vapor_Viscosity_Const_C, Vapor_Viscosity_Const_D, Vapor_Viscosity_Const_E, T, Critical_Temperature)
                Else
                    val = PropertyPackages.PropertyPackage.ParseEquation(VaporViscosityEquation, Vapor_Viscosity_Const_A, Vapor_Viscosity_Const_B, Vapor_Viscosity_Const_C, Vapor_Viscosity_Const_D, Vapor_Viscosity_Const_E, T)
                End If
            ElseIf IsIon Or IsSalt Then
                val = 0.0#
            Else
                If Critical_Temperature > 0.0# Then
                    message = "Estimated using Lucas correlation."
                    val = PropertyPackages.Auxiliary.PROPS.viscg_lucas(T, Critical_Temperature, Critical_Pressure, Acentric_Factor, Molar_Weight)
                Else
                    val = 0.0#
                End If
            End If

            Return val

        End Function

        Public Function GetVaporThermalConductivity(T As Double, Optional ByRef message As String = "") As Double Implements ICompoundConstantProperties.GetVaporThermalConductivity

            Dim val As Double

            If VaporThermalConductivityEquation <> "" And VaporThermalConductivityEquation <> "0" And Not IsIon And Not IsSalt Then
                message = "Calculated using Experimental/Regressed data."
                val = PropertyPackages.PropertyPackage.CalcCSTDepProp(VaporThermalConductivityEquation, Vapor_Thermal_Conductivity_Const_A, Vapor_Thermal_Conductivity_Const_B, Vapor_Thermal_Conductivity_Const_C, Vapor_Thermal_Conductivity_Const_D, Vapor_Thermal_Conductivity_Const_E, T, Critical_Temperature)
            ElseIf IsIon Or IsSalt Then
                val = 0.0#
            Else
                message = "Estimated using Ely-Hanley correlation."
                val = PropertyPackages.Auxiliary.PROPS.condtg_elyhanley(T, Critical_Temperature, Critical_Volume / 1000, Critical_Compressibility, Acentric_Factor, Molar_Weight, GetIdealGasHeatCapacity(T) * Molar_Weight - 8.314)
            End If

            Return val

        End Function

        Public Function GetLiquidViscosity(T As Double, Optional ByRef message As String = "") As Double Implements ICompoundConstantProperties.GetLiquidViscosity

            If IsPF = 1 Then
                Dim dens = GetLiquidDensity(T)
                Dim visc = PropertyPackages.Auxiliary.PROPS.oilvisc_twu(T, PF_Tv1, PF_Tv2, PF_v1, PF_v2)
                If Double.IsNaN(visc) Then
                    Dim Tc, Pc, w, Mw As Double
                    Tc = Critical_Temperature
                    Pc = Critical_Pressure
                    w = Acentric_Factor
                    Mw = Molar_Weight
                    visc = PropertyPackages.Auxiliary.PROPS.viscl_letsti(T, Tc, Pc, w, Mw)
                End If
                message = "Estimated using Twu correlation."
                Return visc * dens
            Else
                If OriginalDB = "DWSIM" Or
                    OriginalDB = "" Then
                    Dim A, B, C, D, E, result As Double
                    A = Liquid_Viscosity_Const_A
                    B = Liquid_Viscosity_Const_B
                    C = Liquid_Viscosity_Const_C
                    D = Liquid_Viscosity_Const_D
                    E = Liquid_Viscosity_Const_E
                    message = "Calculated using Experimental/Regressed data."
                    result = Math.Exp(A + B / T + C * Math.Log(T) + D * T ^ E)
                    Return result
                ElseIf OriginalDB = "CheResources" Then
                    Dim B, C, result As Double
                    B = Liquid_Viscosity_Const_B
                    C = Liquid_Viscosity_Const_C
                    '[LOG(V)=B*(1/T-1/C), T(K) V(CP)]
                    message = "Calculated using Experimental/Regressed data."
                    result = Math.Exp(B * (1 / T - 1 / C)) * 0.001
                    Return result
                ElseIf OriginalDB = "ChemSep" Or
                    OriginalDB = "CoolProp" Or
                    OriginalDB = "User" Or
                    OriginalDB = "ChEDL Thermo" Or
                    OriginalDB = "KDB" Then
                    Dim A, B, C, D, E, result As Double
                    Dim eqno As String = LiquidViscosityEquation
                    Dim mw As Double = Molar_Weight
                    A = Liquid_Viscosity_Const_A
                    B = Liquid_Viscosity_Const_B
                    C = Liquid_Viscosity_Const_C
                    D = Liquid_Viscosity_Const_D
                    E = Liquid_Viscosity_Const_E
                    '<lvsc name="Liquid viscosity"  units="Pa.s" >
                    If eqno = "0" Or eqno = "" Then
                        Dim Tc, Pc, w As Double
                        Tc = Critical_Temperature
                        Pc = Critical_Pressure
                        w = Acentric_Factor
                        mw = Molar_Weight
                        message = "Estimated using Lestou-Stiel correlation."
                        result = PropertyPackages.Auxiliary.PROPS.viscl_letsti(T, Tc, Pc, w, mw)
                    Else
                        message = "Calculated using Experimental/Regressed data."
                        If Integer.TryParse(eqno, New Integer) Then
                            result = PropertyPackages.PropertyPackage.CalcCSTDepProp(eqno, A, B, C, D, E, T, 0) 'Pa.s
                        Else
                            result = PropertyPackages.PropertyPackage.ParseEquation(eqno, A, B, C, D, E, T) 'Pa.s
                        End If
                    End If
                    Return result
                ElseIf OriginalDB = "Biodiesel" Then
                    Dim result As Double
                    Dim Tc, Pc, w, Mw As Double
                    Tc = Critical_Temperature
                    Pc = Critical_Pressure
                    w = Acentric_Factor
                    Mw = Molar_Weight
                    message = "Estimated using Lestou-Stiel correlation."
                    result = PropertyPackages.Auxiliary.PROPS.viscl_letsti(T, Tc, Pc, w, Mw)
                    Return result
                End If
            End If

        End Function

        Public Function GetLiquidThermalConductivity(T As Double, Optional ByRef message As String = "") As Double Implements ICompoundConstantProperties.GetLiquidThermalConductivity

            Dim val As Double

            If LiquidThermalConductivityEquation <> "" And LiquidThermalConductivityEquation <> "0" And Not IsIon And Not IsSalt Then
                message = "Calculated using Experimental/Regressed data."
                val = PropertyPackages.PropertyPackage.CalcCSTDepProp(LiquidThermalConductivityEquation, Liquid_Thermal_Conductivity_Const_A, Liquid_Thermal_Conductivity_Const_B, Liquid_Thermal_Conductivity_Const_C, Liquid_Thermal_Conductivity_Const_D, Liquid_Thermal_Conductivity_Const_E, T, Critical_Temperature)
            ElseIf IsIon Or IsSalt Then
                val = 0.0#
            Else
                message = "Estimated using Latini correlation."
                val = PropertyPackages.Auxiliary.PROPS.condl_latini(T, Normal_Boiling_Point, Critical_Temperature, Molar_Weight, "")
            End If

            Return val

        End Function

        Public Function GetLiquidHeatCapacity(T As Double, Optional ByRef message As String = "") As Double Implements ICompoundConstantProperties.GetLiquidHeatCapacity

            Dim val As Double
            If T >= Critical_Temperature Then
                'surrogate for supercritical gases solved in liquid
                message = "Calculated using Experimental/Regressed data."
                val = PropertyPackages.PropertyPackage.CalcCSTDepProp(IdealgasCpEquation, Ideal_Gas_Heat_Capacity_Const_A, Ideal_Gas_Heat_Capacity_Const_B, Ideal_Gas_Heat_Capacity_Const_C, Ideal_Gas_Heat_Capacity_Const_D, Ideal_Gas_Heat_Capacity_Const_E, T, Critical_Temperature)
                If OriginalDB <> "CoolProp" Then val = val / 1000 / Molar_Weight 'kJ/kg.K
            Else
                If LiquidHeatCapacityEquation <> "" And LiquidHeatCapacityEquation <> "0" And Not IsIon And Not IsSalt Then
                    message = "Calculated using Experimental/Regressed data."
                    If Integer.TryParse(LiquidHeatCapacityEquation, New Integer) Then
                        val = PropertyPackages.PropertyPackage.CalcCSTDepProp(LiquidHeatCapacityEquation, Liquid_Heat_Capacity_Const_A, Liquid_Heat_Capacity_Const_B, Liquid_Heat_Capacity_Const_C, Liquid_Heat_Capacity_Const_D, Liquid_Heat_Capacity_Const_E, T, Critical_Temperature)
                    Else
                        val = PropertyPackages.PropertyPackage.ParseEquation(LiquidHeatCapacityEquation, Liquid_Heat_Capacity_Const_A, Liquid_Heat_Capacity_Const_B, Liquid_Heat_Capacity_Const_C, Liquid_Heat_Capacity_Const_D, Liquid_Heat_Capacity_Const_E, T) / Molar_Weight
                    End If
                    If OriginalDB <> "CoolProp" And OriginalDB <> "ChEDL Thermo" Then val = val / 1000 / Molar_Weight 'kJ/kg.K
                Else
                    'estimate using Rownlinson/Bondi correlation
                    message = "Estimated using Rowlinson/Bondi correlation."
                    val = PropertyPackages.Auxiliary.PROPS.Cpl_rb(GetIdealGasHeatCapacity(T), T, Critical_Temperature, Acentric_Factor, Molar_Weight) 'kJ/kg.K
                End If
            End If

            Return val

        End Function

        Public Function GetLiquidDensity(T As Double, Optional ByRef message As String = "") As Double Implements ICompoundConstantProperties.GetLiquidDensity

            Dim val As Double

            If LiquidDensityEquation <> "" And LiquidDensityEquation <> "0" And Not IsIon And Not IsSalt Then
                message = "Calculated using Experimental/Regressed data."
                If Integer.TryParse(LiquidDensityEquation, New Integer) Then
                    val = PropertyPackages.PropertyPackage.CalcCSTDepProp(LiquidDensityEquation, Liquid_Density_Const_A, Liquid_Density_Const_B, Liquid_Density_Const_C, Liquid_Density_Const_D, Liquid_Density_Const_E, T, Critical_Temperature)
                Else
                    val = PropertyPackages.PropertyPackage.ParseEquation(LiquidDensityEquation, Liquid_Density_Const_A, Liquid_Density_Const_B, Liquid_Density_Const_C, Liquid_Density_Const_D, Liquid_Density_Const_E, T)
                End If
                If OriginalDB <> "CoolProp" And OriginalDB <> "User" And OriginalDB <> "ChEDL Thermo" Then val = Molar_Weight * val
            Else
                message = "Estimated using Rackett correlation."
                val = PropertyPackages.Auxiliary.PROPS.liq_dens_rackett(T, Critical_Temperature, Critical_Pressure, Acentric_Factor, Molar_Weight, Z_Rackett)
            End If

            Return val 'kg/m3

        End Function

        Public Function GetLiquidSurfaceTension(T As Double, Optional ByRef message As String = "") As Double Implements ICompoundConstantProperties.GetLiquidSurfaceTension

            Dim val As Double

            If SurfaceTensionEquation <> "" And SurfaceTensionEquation <> "0" And Not IsIon And Not IsSalt Then
                message = "Calculated using Experimental/Regressed data."
                If Integer.TryParse(SurfaceTensionEquation, New Integer) Then
                    val = PropertyPackages.PropertyPackage.CalcCSTDepProp(SurfaceTensionEquation, Surface_Tension_Const_A, Surface_Tension_Const_B, Surface_Tension_Const_C, Surface_Tension_Const_D, Surface_Tension_Const_E, T, Critical_Temperature)
                Else
                    val = PropertyPackages.PropertyPackage.ParseEquation(SurfaceTensionEquation, Surface_Tension_Const_A, Surface_Tension_Const_B, Surface_Tension_Const_C, Surface_Tension_Const_D, Surface_Tension_Const_E, T)
                End If
            Else
                message = "Estimated using Rackett correlation."
                val = PropertyPackages.Auxiliary.PROPS.sigma_bb(T, Normal_Boiling_Point, Critical_Temperature, Critical_Pressure)
            End If

            Return val 'N/m

        End Function

        Public Function GetSolidDensity(T As Double, Optional ByRef message As String = "") As Double Implements ICompoundConstantProperties.GetSolidDensity

            Dim val As Double

            If OriginalDB = "ChemSep" Or (OriginalDB = "User" And SolidDensityEquation <> "") Then
                Dim A, B, C, D, E, result As Double
                Dim eqno As String = SolidDensityEquation
                Dim mw As Double = Molar_Weight
                A = Solid_Density_Const_A
                B = Solid_Density_Const_B
                C = Solid_Density_Const_C
                D = Solid_Density_Const_D
                E = Solid_Density_Const_E
                message = "Calculated using Experimental/Regressed data."
                If eqno <> "" Then result = PropertyPackages.PropertyPackage.CalcCSTDepProp(eqno, A, B, C, D, E, T, 0) 'kmol/m3
                val = 1 / (result * mw)
            ElseIf OriginalDB = "ChEDL Thermo" Then
                Dim A, B, C, D, E, result As Double
                Dim eqno As String = SolidDensityEquation
                A = Solid_Density_Const_A
                B = Solid_Density_Const_B
                C = Solid_Density_Const_C
                D = Solid_Density_Const_D
                E = Solid_Density_Const_E
                message = "Calculated using Experimental/Regressed data."
                If eqno <> "" Then result = result = PropertyPackages.PropertyPackage.CalcCSTDepProp(eqno, A, B, C, D, E, T, 0) 'kg/m3
                val = 1 / (result)
            Else
                message = "Using stored value at Ts."
                If SolidDensityAtTs <> 0.0# Then
                    val = 1 / SolidDensityAtTs
                Else
                    val = 1.0E+20
                End If
            End If

            Return 1 / val

        End Function

        Public Function GetSolidHeatCapacity(T As Double, Optional ByRef message As String = "") As Double Implements ICompoundConstantProperties.GetSolidHeatCapacity

            Dim val As Double

            If OriginalDB = "ChemSep" Or (OriginalDB = "User" And SolidHeatCapacityEquation <> "") Then
                Dim A, B, C, D, E, result As Double
                Dim eqno As String = SolidHeatCapacityEquation
                Dim mw As Double = Molar_Weight
                A = Solid_Heat_Capacity_Const_A
                B = Solid_Heat_Capacity_Const_B
                C = Solid_Heat_Capacity_Const_C
                D = Solid_Heat_Capacity_Const_D
                E = Solid_Heat_Capacity_Const_E
                message = "Calculated using Experimental/Regressed data."
                result = PropertyPackages.PropertyPackage.CalcCSTDepProp(eqno, A, B, C, D, E, T, 0) 'J/kmol/K
                val = result / 1000 / mw 'kJ/kg.K
            ElseIf OriginalDB = "ChEDL Thermo" Then
                Dim A, B, C, D, E As Double
                Dim eqno As String = SolidHeatCapacityEquation
                A = Solid_Heat_Capacity_Const_A
                B = Solid_Heat_Capacity_Const_B
                C = Solid_Heat_Capacity_Const_C
                D = Solid_Heat_Capacity_Const_D
                E = Solid_Heat_Capacity_Const_E
                message = "Calculated using Experimental/Regressed data."
                val = PropertyPackages.PropertyPackage.CalcCSTDepProp(eqno, A, B, C, D, E, T, 0) 'kJ/kg/K
            Else
                message = "Using default value when no data is available."
                val = 3 ' replacement if no params available
            End If

            Return val

        End Function

        Public Property Acentric_Factor As Double = 0.0# Implements ICompoundConstantProperties.Acentric_Factor

        Public Property BO_BSW As Double = 0.0# Implements ICompoundConstantProperties.BO_BSW

        Public Property BO_GOR As Double = 0.0# Implements ICompoundConstantProperties.BO_GOR

        Public Property BO_OilVisc1 As Double = 0.0# Implements ICompoundConstantProperties.BO_OilVisc1

        Public Property BO_OilVisc2 As Double = 0.0# Implements ICompoundConstantProperties.BO_OilVisc2

        Public Property BO_OilViscTemp1 As Double = 0.0# Implements ICompoundConstantProperties.BO_OilViscTemp1

        Public Property BO_OilViscTemp2 As Double = 0.0# Implements ICompoundConstantProperties.BO_OilViscTemp2

        Public Property BO_PNA_A As Double = 0.0# Implements ICompoundConstantProperties.BO_PNA_A

        Public Property BO_PNA_N As Double = 0.0# Implements ICompoundConstantProperties.BO_PNA_N

        Public Property BO_PNA_P As Double = 0.0# Implements ICompoundConstantProperties.BO_PNA_P

        Public Property BO_SGG As Double = 0.0# Implements ICompoundConstantProperties.BO_SGG

        Public Property BO_SGO As Double = 0.0# Implements ICompoundConstantProperties.BO_SGO

        Public Property CAS_Number As String = "" Implements ICompoundConstantProperties.CAS_Number

        Public Property Chao_Seader_Acentricity As Double = 0.0# Implements ICompoundConstantProperties.Chao_Seader_Acentricity

        Public Property Chao_Seader_Liquid_Molar_Volume As Double = 0.0# Implements ICompoundConstantProperties.Chao_Seader_Liquid_Molar_Volume

        Public Property Chao_Seader_Solubility_Parameter As Double = 0.0# Implements ICompoundConstantProperties.Chao_Seader_Solubility_Parameter

        Public Property Charge As Integer Implements ICompoundConstantProperties.Charge

        Public Property ChemicalStructure As String = "" Implements ICompoundConstantProperties.ChemicalStructure

        Public Property Comments As String = "" Implements ICompoundConstantProperties.Comments

        Public Property CompCreatorStudyFile As String = "" Implements ICompoundConstantProperties.CompCreatorStudyFile

        Public Property COSMODBName As Object Implements ICompoundConstantProperties.COSMODBName

        Public Property Critical_Compressibility As Double = 0.0# Implements ICompoundConstantProperties.Critical_Compressibility

        Public Property Critical_Pressure As Double = 0.0# Implements ICompoundConstantProperties.Critical_Pressure

        Public Property Critical_Temperature As Double = 0.0# Implements ICompoundConstantProperties.Critical_Temperature

        Public Property Critical_Volume As Double = 0.0# Implements ICompoundConstantProperties.Critical_Volume

        Public Property CurrentDB As String = "DWSIM" Implements ICompoundConstantProperties.CurrentDB

        Public Property Dipole_Moment As Double = 0.0# Implements ICompoundConstantProperties.Dipole_Moment

        Public Property Electrolyte_Cp0 As Double = 0.0# Implements ICompoundConstantProperties.Electrolyte_Cp0

        Public Property Electrolyte_DelGF As Double = 0.0# Implements ICompoundConstantProperties.Electrolyte_DelGF

        Public Property Electrolyte_DelHF As Double = 0.0# Implements ICompoundConstantProperties.Electrolyte_DelHF

        Public Property EnthalpyOfFusionAtTf As Double = 0.0# Implements ICompoundConstantProperties.EnthalpyOfFusionAtTf

        Private _formula As String = ""

        Public Property Formula As String Implements ICompoundConstantProperties.Formula
            Get
                Return _formula
            End Get
            Set(value As String)
                _formula = value
                Try
                    UpdateElements()
                Catch ex As Exception
                End Try
            End Set
        End Property

        Public Property HVap_A As Double = 0.0# Implements ICompoundConstantProperties.HVap_A

        Public Property HVap_B As Double = 0.0# Implements ICompoundConstantProperties.HVap_B

        Public Property HVap_C As Double = 0.0# Implements ICompoundConstantProperties.HVap_C

        Public Property HVap_D As Double = 0.0# Implements ICompoundConstantProperties.HVap_D

        Public Property HVap_E As Double = 0.0# Implements ICompoundConstantProperties.HVap_E

        Public Property HVap_TMAX As Double = 0.0# Implements ICompoundConstantProperties.HVap_TMAX

        Public Property HVap_TMIN As Double = 0.0# Implements ICompoundConstantProperties.HVap_TMIN

        Public Property HydrationNumber As Double = 0.0# Implements ICompoundConstantProperties.HydrationNumber

        Public Property ID As Integer Implements ICompoundConstantProperties.ID

        Public Property Ideal_Gas_Heat_Capacity_Const_A As Double = 0.0# Implements ICompoundConstantProperties.Ideal_Gas_Heat_Capacity_Const_A

        Public Property Ideal_Gas_Heat_Capacity_Const_B As Double = 0.0# Implements ICompoundConstantProperties.Ideal_Gas_Heat_Capacity_Const_B

        Public Property Ideal_Gas_Heat_Capacity_Const_C As Double = 0.0# Implements ICompoundConstantProperties.Ideal_Gas_Heat_Capacity_Const_C

        Public Property Ideal_Gas_Heat_Capacity_Const_D As Double = 0.0# Implements ICompoundConstantProperties.Ideal_Gas_Heat_Capacity_Const_D

        Public Property Ideal_Gas_Heat_Capacity_Const_E As Double = 0.0# Implements ICompoundConstantProperties.Ideal_Gas_Heat_Capacity_Const_E

        Public Property IdealgasCpEquation As String = "" Implements ICompoundConstantProperties.IdealgasCpEquation

        Public Property IG_Enthalpy_of_Formation_25C As Double = 0.0# Implements ICompoundConstantProperties.IG_Enthalpy_of_Formation_25C

        Public Property IG_Entropy_of_Formation_25C As Double = 0.0# Implements ICompoundConstantProperties.IG_Entropy_of_Formation_25C

        Public Property IG_Gibbs_Energy_of_Formation_25C As Double = 0.0# Implements ICompoundConstantProperties.IG_Gibbs_Energy_of_Formation_25C

        Public Property InChI As String = "" Implements ICompoundConstantProperties.InChI

        Public Property Ion_CpAq_a As Double = 0.0# Implements ICompoundConstantProperties.Ion_CpAq_a

        Public Property Ion_CpAq_b As Double = 0.0# Implements ICompoundConstantProperties.Ion_CpAq_b

        Public Property Ion_CpAq_c As Double = 0.0# Implements ICompoundConstantProperties.Ion_CpAq_c

        Public Property IsBlackOil As Boolean Implements ICompoundConstantProperties.IsBlackOil

        Public Property IsCOOLPROPSupported As Boolean Implements ICompoundConstantProperties.IsCOOLPROPSupported

        Public Property IsFPROPSSupported As Boolean Implements ICompoundConstantProperties.IsFPROPSSupported

        Public Property IsHydratedSalt As Boolean Implements ICompoundConstantProperties.IsHydratedSalt

        Public Property IsHYPO As Integer Implements ICompoundConstantProperties.IsHYPO

        Public Property IsIon As Boolean Implements ICompoundConstantProperties.IsIon

        Public Property IsModified As Boolean Implements ICompoundConstantProperties.IsModified

        Public Property IsPF As Integer Implements ICompoundConstantProperties.IsPF

        Public Property IsSalt As Boolean Implements ICompoundConstantProperties.IsSalt

        Public Property IsSolid As Boolean = False Implements ICompoundConstantProperties.IsSolid

        Public Property Liquid_Density_Const_A As Double = 0.0# Implements ICompoundConstantProperties.Liquid_Density_Const_A

        Public Property Liquid_Density_Const_B As Double = 0.0# Implements ICompoundConstantProperties.Liquid_Density_Const_B

        Public Property Liquid_Density_Const_C As Double = 0.0# Implements ICompoundConstantProperties.Liquid_Density_Const_C

        Public Property Liquid_Density_Const_D As Double = 0.0# Implements ICompoundConstantProperties.Liquid_Density_Const_D

        Public Property Liquid_Density_Const_E As Double = 0.0# Implements ICompoundConstantProperties.Liquid_Density_Const_E

        Public Property Liquid_Density_Tmax As Double = 0.0# Implements ICompoundConstantProperties.Liquid_Density_Tmax

        Public Property Liquid_Density_Tmin As Double = 0.0# Implements ICompoundConstantProperties.Liquid_Density_Tmin

        Public Property Liquid_Heat_Capacity_Const_A As Double = 0.0# Implements ICompoundConstantProperties.Liquid_Heat_Capacity_Const_A

        Public Property Liquid_Heat_Capacity_Const_B As Double = 0.0# Implements ICompoundConstantProperties.Liquid_Heat_Capacity_Const_B

        Public Property Liquid_Heat_Capacity_Const_C As Double = 0.0# Implements ICompoundConstantProperties.Liquid_Heat_Capacity_Const_C

        Public Property Liquid_Heat_Capacity_Const_D As Double = 0.0# Implements ICompoundConstantProperties.Liquid_Heat_Capacity_Const_D

        Public Property Liquid_Heat_Capacity_Const_E As Double = 0.0# Implements ICompoundConstantProperties.Liquid_Heat_Capacity_Const_E

        Public Property Liquid_Heat_Capacity_Tmax As Double = 0.0# Implements ICompoundConstantProperties.Liquid_Heat_Capacity_Tmax

        Public Property Liquid_Heat_Capacity_Tmin As Double = 0.0# Implements ICompoundConstantProperties.Liquid_Heat_Capacity_Tmin

        Public Property Liquid_Thermal_Conductivity_Const_A As Double = 0.0# Implements ICompoundConstantProperties.Liquid_Thermal_Conductivity_Const_A

        Public Property Liquid_Thermal_Conductivity_Const_B As Double = 0.0# Implements ICompoundConstantProperties.Liquid_Thermal_Conductivity_Const_B

        Public Property Liquid_Thermal_Conductivity_Const_C As Double = 0.0# Implements ICompoundConstantProperties.Liquid_Thermal_Conductivity_Const_C

        Public Property Liquid_Thermal_Conductivity_Const_D As Double = 0.0# Implements ICompoundConstantProperties.Liquid_Thermal_Conductivity_Const_D

        Public Property Liquid_Thermal_Conductivity_Const_E As Double = 0.0# Implements ICompoundConstantProperties.Liquid_Thermal_Conductivity_Const_E

        Public Property Liquid_Thermal_Conductivity_Tmax As Double = 0.0# Implements ICompoundConstantProperties.Liquid_Thermal_Conductivity_Tmax

        Public Property Liquid_Thermal_Conductivity_Tmin As Double = 0.0# Implements ICompoundConstantProperties.Liquid_Thermal_Conductivity_Tmin

        Public Property Liquid_Viscosity_Const_A As Double = 0.0# Implements ICompoundConstantProperties.Liquid_Viscosity_Const_A

        Public Property Liquid_Viscosity_Const_B As Double = 0.0# Implements ICompoundConstantProperties.Liquid_Viscosity_Const_B

        Public Property Liquid_Viscosity_Const_C As Double = 0.0# Implements ICompoundConstantProperties.Liquid_Viscosity_Const_C

        Public Property Liquid_Viscosity_Const_D As Double = 0.0# Implements ICompoundConstantProperties.Liquid_Viscosity_Const_D

        Public Property Liquid_Viscosity_Const_E As Double = 0.0# Implements ICompoundConstantProperties.Liquid_Viscosity_Const_E

        Public Property LiquidDensityEquation As String = "" Implements ICompoundConstantProperties.LiquidDensityEquation

        Public Property LiquidHeatCapacityEquation As String = "" Implements ICompoundConstantProperties.LiquidHeatCapacityEquation

        Public Property LiquidThermalConductivityEquation As String = "" Implements ICompoundConstantProperties.LiquidThermalConductivityEquation

        Public Property LiquidViscosityEquation As String = "" Implements ICompoundConstantProperties.LiquidViscosityEquation

        Public Property Molar_Weight As Double = 0.0# Implements ICompoundConstantProperties.Molar_Weight

        Public Property MolarVolume_k1i As Double = 0.0# Implements ICompoundConstantProperties.MolarVolume_k1i

        Public Property MolarVolume_k2i As Double = 0.0# Implements ICompoundConstantProperties.MolarVolume_k2i

        Public Property MolarVolume_k3i As Double = 0.0# Implements ICompoundConstantProperties.MolarVolume_k3i

        Public Property MolarVolume_v2i As Double = 0.0# Implements ICompoundConstantProperties.MolarVolume_v2i

        Public Property MolarVolume_v3i As Double = 0.0# Implements ICompoundConstantProperties.MolarVolume_v3i

        Public Property Name As String = "" Implements ICompoundConstantProperties.Name

        Public Property NBP As Double? Implements ICompoundConstantProperties.NBP

        Public Property NegativeIon As String = "" Implements ICompoundConstantProperties.NegativeIon

        Public Property NegativeIonStoichCoeff As Integer Implements ICompoundConstantProperties.NegativeIonStoichCoeff

        Public Property Normal_Boiling_Point As Double = 0.0# Implements ICompoundConstantProperties.Normal_Boiling_Point

        Public Property OriginalDB As String = "DWSIM" Implements ICompoundConstantProperties.OriginalDB

        Public Property PC_SAFT_epsilon_k As Double = 0.0# Implements ICompoundConstantProperties.PC_SAFT_epsilon_k

        Public Property PC_SAFT_m As Double = 0.0# Implements ICompoundConstantProperties.PC_SAFT_m

        Public Property PC_SAFT_sigma As Double = 0.0# Implements ICompoundConstantProperties.PC_SAFT_sigma

        Public Property PF_MM As Double? Implements ICompoundConstantProperties.PF_MM

        Public Property PF_SG As Double? Implements ICompoundConstantProperties.PF_SG

        Public Property PF_Tv1 As Double? Implements ICompoundConstantProperties.PF_Tv1

        Public Property PF_Tv2 As Double? Implements ICompoundConstantProperties.PF_Tv2

        Public Property PF_v1 As Double? Implements ICompoundConstantProperties.PF_v1

        Public Property PF_v2 As Double? Implements ICompoundConstantProperties.PF_v2

        Public Property PF_vA As Double? Implements ICompoundConstantProperties.PF_vA

        Public Property PF_vB As Double? Implements ICompoundConstantProperties.PF_vB

        Public Property PF_Watson_K As Double? Implements ICompoundConstantProperties.PF_Watson_K

        Public Property PositiveIon As String = "" Implements ICompoundConstantProperties.PositiveIon

        Public Property PositiveIonStoichCoeff As Integer Implements ICompoundConstantProperties.PositiveIonStoichCoeff

        Public Property PR_Volume_Translation_Coefficient As Double = 0.0# Implements ICompoundConstantProperties.PR_Volume_Translation_Coefficient

        Public Property SMILES As String = "" Implements ICompoundConstantProperties.SMILES

        Public Property Solid_Density_Const_A As Double = 0.0# Implements ICompoundConstantProperties.Solid_Density_Const_A

        Public Property Solid_Density_Const_B As Double = 0.0# Implements ICompoundConstantProperties.Solid_Density_Const_B

        Public Property Solid_Density_Const_C As Double = 0.0# Implements ICompoundConstantProperties.Solid_Density_Const_C

        Public Property Solid_Density_Const_D As Double = 0.0# Implements ICompoundConstantProperties.Solid_Density_Const_D

        Public Property Solid_Density_Const_E As Double = 0.0# Implements ICompoundConstantProperties.Solid_Density_Const_E

        Public Property Solid_Density_Tmax As Double = 0.0# Implements ICompoundConstantProperties.Solid_Density_Tmax

        Public Property Solid_Density_Tmin As Double = 0.0# Implements ICompoundConstantProperties.Solid_Density_Tmin

        Public Property Solid_Heat_Capacity_Const_A As Double = 0.0# Implements ICompoundConstantProperties.Solid_Heat_Capacity_Const_A

        Public Property Solid_Heat_Capacity_Const_B As Double = 0.0# Implements ICompoundConstantProperties.Solid_Heat_Capacity_Const_B

        Public Property Solid_Heat_Capacity_Const_C As Double = 0.0# Implements ICompoundConstantProperties.Solid_Heat_Capacity_Const_C

        Public Property Solid_Heat_Capacity_Const_D As Double = 0.0# Implements ICompoundConstantProperties.Solid_Heat_Capacity_Const_D

        Public Property Solid_Heat_Capacity_Const_E As Double = 0.0# Implements ICompoundConstantProperties.Solid_Heat_Capacity_Const_E

        Public Property Solid_Heat_Capacity_Tmax As Double = 0.0# Implements ICompoundConstantProperties.Solid_Heat_Capacity_Tmax

        Public Property Solid_Heat_Capacity_Tmin As Double = 0.0# Implements ICompoundConstantProperties.Solid_Heat_Capacity_Tmin

        Public Property SolidDensityAtTs As Double = 0.0# Implements ICompoundConstantProperties.SolidDensityAtTs

        Public Property SolidDensityEquation As String = "" Implements ICompoundConstantProperties.SolidDensityEquation

        Public Property SolidHeatCapacityEquation As String = "" Implements ICompoundConstantProperties.SolidHeatCapacityEquation

        Public Property SolidTs As Double = 0.0# Implements ICompoundConstantProperties.SolidTs

        Public Property SRK_Volume_Translation_Coefficient As Double = 0.0# Implements ICompoundConstantProperties.SRK_Volume_Translation_Coefficient

        Public Property StandardStateMolarVolume As Double = 0.0# Implements ICompoundConstantProperties.StandardStateMolarVolume

        Public Property StoichSum As Integer Implements ICompoundConstantProperties.StoichSum

        Public Property Surface_Tension_Const_A As Double = 0.0# Implements ICompoundConstantProperties.Surface_Tension_Const_A

        Public Property Surface_Tension_Const_B As Double = 0.0# Implements ICompoundConstantProperties.Surface_Tension_Const_B

        Public Property Surface_Tension_Const_C As Double = 0.0# Implements ICompoundConstantProperties.Surface_Tension_Const_C

        Public Property Surface_Tension_Const_D As Double = 0.0# Implements ICompoundConstantProperties.Surface_Tension_Const_D

        Public Property Surface_Tension_Const_E As Double = 0.0# Implements ICompoundConstantProperties.Surface_Tension_Const_E

        Public Property Surface_Tension_Tmax As Double = 0.0# Implements ICompoundConstantProperties.Surface_Tension_Tmax

        Public Property Surface_Tension_Tmin As Double = 0.0# Implements ICompoundConstantProperties.Surface_Tension_Tmin

        Public Property SurfaceTensionEquation As String = "" Implements ICompoundConstantProperties.SurfaceTensionEquation

        Public Property TemperatureOfFusion As Double = 0.0# Implements ICompoundConstantProperties.TemperatureOfFusion

        Public Property UNIQUAC_Q As Double = 0.0# Implements ICompoundConstantProperties.UNIQUAC_Q

        Public Property UNIQUAC_R As Double = 0.0# Implements ICompoundConstantProperties.UNIQUAC_R

        Public Property Vapor_Pressure_Constant_A As Double = 0.0# Implements ICompoundConstantProperties.Vapor_Pressure_Constant_A

        Public Property Vapor_Pressure_Constant_B As Double = 0.0# Implements ICompoundConstantProperties.Vapor_Pressure_Constant_B

        Public Property Vapor_Pressure_Constant_C As Double = 0.0# Implements ICompoundConstantProperties.Vapor_Pressure_Constant_C

        Public Property Vapor_Pressure_Constant_D As Double = 0.0# Implements ICompoundConstantProperties.Vapor_Pressure_Constant_D

        Public Property Vapor_Pressure_Constant_E As Double = 0.0# Implements ICompoundConstantProperties.Vapor_Pressure_Constant_E

        Public Property Vapor_Pressure_TMAX As Double = 0.0# Implements ICompoundConstantProperties.Vapor_Pressure_TMAX

        Public Property Vapor_Pressure_TMIN As Double = 0.0# Implements ICompoundConstantProperties.Vapor_Pressure_TMIN

        Public Property Vapor_Thermal_Conductivity_Const_A As Double = 0.0# Implements ICompoundConstantProperties.Vapor_Thermal_Conductivity_Const_A

        Public Property Vapor_Thermal_Conductivity_Const_B As Double = 0.0# Implements ICompoundConstantProperties.Vapor_Thermal_Conductivity_Const_B

        Public Property Vapor_Thermal_Conductivity_Const_C As Double = 0.0# Implements ICompoundConstantProperties.Vapor_Thermal_Conductivity_Const_C

        Public Property Vapor_Thermal_Conductivity_Const_D As Double = 0.0# Implements ICompoundConstantProperties.Vapor_Thermal_Conductivity_Const_D

        Public Property Vapor_Thermal_Conductivity_Const_E As Double = 0.0# Implements ICompoundConstantProperties.Vapor_Thermal_Conductivity_Const_E

        Public Property Vapor_Thermal_Conductivity_Tmax As Double = 0.0# Implements ICompoundConstantProperties.Vapor_Thermal_Conductivity_Tmax

        Public Property Vapor_Thermal_Conductivity_Tmin As Double = 0.0# Implements ICompoundConstantProperties.Vapor_Thermal_Conductivity_Tmin

        Public Property Vapor_Viscosity_Const_A As Double = 0.0# Implements ICompoundConstantProperties.Vapor_Viscosity_Const_A

        Public Property Vapor_Viscosity_Const_B As Double = 0.0# Implements ICompoundConstantProperties.Vapor_Viscosity_Const_B

        Public Property Vapor_Viscosity_Const_C As Double = 0.0# Implements ICompoundConstantProperties.Vapor_Viscosity_Const_C

        Public Property Vapor_Viscosity_Const_D As Double = 0.0# Implements ICompoundConstantProperties.Vapor_Viscosity_Const_D

        Public Property Vapor_Viscosity_Const_E As Double = 0.0# Implements ICompoundConstantProperties.Vapor_Viscosity_Const_E

        Public Property Vapor_Viscosity_Tmax As Double = 0.0# Implements ICompoundConstantProperties.Vapor_Viscosity_Tmax

        Public Property Vapor_Viscosity_Tmin As Double = 0.0# Implements ICompoundConstantProperties.Vapor_Viscosity_Tmin

        Public Property VaporizationEnthalpyEquation As String = "" Implements ICompoundConstantProperties.VaporizationEnthalpyEquation

        Public Property VaporPressureEquation As String = "" Implements ICompoundConstantProperties.VaporPressureEquation

        Public Property VaporThermalConductivityEquation As String = "" Implements ICompoundConstantProperties.VaporThermalConductivityEquation

        Public Property VaporViscosityEquation As String = "" Implements ICompoundConstantProperties.VaporViscosityEquation

        Public Property Z_Rackett As Double = 0.0# Implements ICompoundConstantProperties.Z_Rackett

        Public Property Elements As SortedList = New SortedList() Implements ICompoundConstantProperties.Elements

        Public Property MODFACGroups As SortedList = New SortedList() Implements ICompoundConstantProperties.MODFACGroups

        Public Property NISTMODFACGroups As SortedList = New SortedList() Implements ICompoundConstantProperties.NISTMODFACGroups

        Public Property UNIFACGroups As SortedList = New SortedList() Implements ICompoundConstantProperties.UNIFACGroups

        Public Property FullerDiffusionVolume As Double = 0.0# Implements ICompoundConstantProperties.FullerDiffusionVolume

        Public Property LennardJonesDiameter As Double = 0.0# Implements ICompoundConstantProperties.LennardJonesDiameter

        Public Property LennardJonesEnergy As Double = 0.0# Implements ICompoundConstantProperties.LennardJonesEnergy

        Public Property Parachor As Double = 0.0# Implements ICompoundConstantProperties.Parachor

        Public Property Tag As String = "" Implements ICompoundConstantProperties.Tag

        <NonSerialized> Private _ep As New ExpandoObject

        Public Property ExtraProperties As ExpandoObject Implements ICompoundConstantProperties.ExtraProperties
            Get
                Return _ep
            End Get
            Set(value As ExpandoObject)
                _ep = value
            End Set
        End Property

        Public Property COSTALD_SRK_Acentric_Factor As Double = 0.0 Implements ICompoundConstantProperties.COSTALD_SRK_Acentric_Factor

        Public Property COSTALD_Characteristic_Volume As Double = 0.0 Implements ICompoundConstantProperties.COSTALD_Characteristic_Volume

        Public Sub ExportToXLSX(filepath As String) Implements ICompoundConstantProperties.ExportToXLSX

            Using xcl As New OfficeOpenXml.ExcelPackage()

                Dim mybook As OfficeOpenXml.ExcelWorkbook = xcl.Workbook

                Dim mysheet As OfficeOpenXml.ExcelWorksheet = mybook.Worksheets.Add("Constant Properties")

                With mysheet

                    .Cells(1, 1).Value = "BASIC PROPERTIES"
                    .Cells(2, 1).Value = "Name"
                    .Cells(3, 1).Value = "Database"
                    .Cells(4, 1).Value = "Type"
                    .Cells(5, 1).Value = "CAS ID"
                    .Cells(6, 1).Value = "Molecular Weight"
                    .Cells(7, 1).Value = "Critical Temperature"
                    .Cells(8, 1).Value = "Critical Pressure"
                    .Cells(9, 1).Value = "Critical Volume"
                    .Cells(10, 1).Value = "Critical Compressibility"
                    .Cells(11, 1).Value = "Acentric Factor"
                    .Cells(12, 1).Value = "Gibbs Energy of Formation (Ideal Gas at 298.15 K)"
                    .Cells(13, 1).Value = "Enthalpy of Formation (Ideal Gas at 298.15 K)"
                    .Cells(14, 1).Value = "Normal Boiling Point"
                    .Cells(15, 1).Value = "Temperature of Fusion"
                    .Cells(16, 1).Value = "Enthalpy of Fusion @ Tf"
                    .Cells(17, 1).Value = "Reference Temperature for Solid Density"
                    .Cells(18, 1).Value = "Solid Density @ Tref"

                    .Cells(20, 1).Value = "MODEL-SPECIFIC PROPERTIES"
                    .Cells(21, 1).Value = "Chao Seader Acentric Factor"
                    .Cells(22, 1).Value = "Chao Seader Solubility Parameter"
                    .Cells(23, 1).Value = "Chao Seader Liquid Molar Volume"
                    .Cells(24, 1).Value = "Rackett Compressibility"
                    .Cells(25, 1).Value = "PR Volume Translation Coefficient"
                    .Cells(26, 1).Value = "SRK Volume Translation Coefficient"
                    .Cells(27, 1).Value = "UNIQUAC R"
                    .Cells(28, 1).Value = "UNIQUAC Q"

                    .Cells(30, 1).Value = "ELECTROLYTE-RELATED PROPERTIES"
                    .Cells(31, 1).Value = "Charge"
                    .Cells(32, 1).Value = "Hydration Number"
                    .Cells(33, 1).Value = "Positive Ion"
                    .Cells(34, 1).Value = "Negative Ion"
                    .Cells(35, 1).Value = "Electrolyte Gibbs Energy of Formation"
                    .Cells(36, 1).Value = "Electrolyte Enthalpy of Formation"
                    .Cells(37, 1).Value = "Standard State Heat Capacity"
                    .Cells(38, 1).Value = "Standard State Molar Volume"

                    .Cells(40, 1).Value = "BLACK-OIL-RELATED PROPERTIES"
                    .Cells(41, 1).Value = "Specific Gravity (Gas)"
                    .Cells(42, 1).Value = "Specific Gravity (Oil)"
                    .Cells(43, 1).Value = "Gas-Oil Ratio"
                    .Cells(44, 1).Value = "Basic Sediments and Water"
                    .Cells(45, 1).Value = "Temperature for Viscosity Data Point 1"
                    .Cells(46, 1).Value = "Viscosity @ T1"
                    .Cells(47, 1).Value = "Temperature for Viscosity Data Point 2"
                    .Cells(48, 1).Value = "Viscosity @ T2"
                    .Cells(49, 1).Value = "PNA - Paraffins"
                    .Cells(50, 1).Value = "PNA - Napthenes"
                    .Cells(51, 1).Value = "PNA - Aromatics"

                    .Cells(53, 1).Value = "PSEUDOCOMPOUND (PETROLEUM FRACTION)-RELATED PROPERTIES"
                    .Cells(54, 1).Value = "Specific Gravity"
                    .Cells(55, 1).Value = "Watson K"
                    .Cells(56, 1).Value = "Temperature for Viscosity Data Point 1"
                    .Cells(57, 1).Value = "Viscosity @ T1"
                    .Cells(58, 1).Value = "Temperature for Viscosity Data Point 2"
                    .Cells(59, 1).Value = "Viscosity @ T2"

                    .Cells(2, 2).Value = Name
                    .Cells(3, 2).Value = OriginalDB

                    If IsBlackOil Then
                        .Cells(4, 2).Value = "Black-Oil"
                    ElseIf IsPF Then
                        .Cells(4, 2).Value = "Petroleum Fraction (Pseudocompound)"
                    ElseIf IsIon Then
                        .Cells(4, 2).Value = "Ion"
                    ElseIf IsSalt Then
                        .Cells(4, 2).Value = "Salt"
                    ElseIf IsHydratedSalt Then
                        .Cells(4, 2).Value = "Hydrated Salt"
                    ElseIf IsHydratedSalt Then
                        .Cells(4, 2).Value = "Default"
                    End If

                    .Cells(5, 2).Value = CAS_Number
                    .Cells(6, 2).Value = Molar_Weight
                    .Cells(7, 2).Value = Critical_Temperature
                    .Cells(8, 2).Value = Critical_Pressure
                    .Cells(9, 2).Value = Critical_Volume
                    .Cells(10, 2).Value = Critical_Compressibility
                    .Cells(11, 2).Value = Acentric_Factor
                    .Cells(12, 2).Value = IG_Gibbs_Energy_of_Formation_25C
                    .Cells(13, 2).Value = IG_Enthalpy_of_Formation_25C
                    .Cells(14, 2).Value = Normal_Boiling_Point
                    .Cells(15, 2).Value = TemperatureOfFusion
                    .Cells(16, 2).Value = EnthalpyOfFusionAtTf
                    .Cells(17, 2).Value = SolidTs
                    .Cells(18, 2).Value = SolidDensityAtTs

                    .Cells(21, 2).Value = Chao_Seader_Acentricity
                    .Cells(22, 2).Value = Chao_Seader_Solubility_Parameter
                    .Cells(23, 2).Value = Chao_Seader_Liquid_Molar_Volume
                    .Cells(24, 2).Value = Z_Rackett
                    .Cells(25, 2).Value = PR_Volume_Translation_Coefficient
                    .Cells(26, 2).Value = SRK_Volume_Translation_Coefficient
                    .Cells(27, 2).Value = UNIQUAC_R
                    .Cells(28, 2).Value = UNIQUAC_Q

                    .Cells(31, 2).Value = Charge
                    .Cells(32, 2).Value = HydrationNumber
                    .Cells(33, 2).Value = PositiveIon
                    .Cells(34, 2).Value = NegativeIon
                    .Cells(35, 2).Value = Electrolyte_DelGF
                    .Cells(36, 2).Value = Electrolyte_DelHF
                    .Cells(37, 2).Value = Electrolyte_Cp0
                    .Cells(38, 2).Value = StandardStateMolarVolume

                    .Cells(41, 2).Value = BO_SGG
                    .Cells(42, 2).Value = BO_SGO
                    .Cells(43, 2).Value = BO_GOR
                    .Cells(44, 2).Value = BO_BSW
                    .Cells(45, 2).Value = BO_OilViscTemp1
                    .Cells(46, 2).Value = BO_OilVisc1
                    .Cells(47, 2).Value = BO_OilViscTemp2
                    .Cells(48, 2).Value = BO_OilVisc2
                    .Cells(49, 2).Value = BO_PNA_P
                    .Cells(50, 2).Value = BO_PNA_N
                    .Cells(51, 2).Value = BO_PNA_A

                    .Cells(54, 2).Value = PF_SG
                    .Cells(55, 2).Value = PF_Watson_K
                    .Cells(56, 2).Value = PF_Tv1
                    .Cells(57, 2).Value = PF_v1
                    .Cells(58, 2).Value = PF_Tv2
                    .Cells(59, 2).Value = PF_v2

                    .Cells(7, 3).Value = "K"
                    .Cells(8, 3).Value = "Pa"
                    .Cells(9, 3).Value = "m3/kmol"
                    .Cells(12, 3).Value = "kJ/kg"
                    .Cells(13, 3).Value = "kJ/kg"
                    .Cells(14, 3).Value = "K"
                    .Cells(15, 3).Value = "K"
                    .Cells(16, 3).Value = "kJ/mol"
                    .Cells(17, 3).Value = "K"
                    .Cells(18, 3).Value = "kg/m3"

                    .Cells(35, 3).Value = "kJ/mol"
                    .Cells(36, 3).Value = "kJ/mol"
                    .Cells(37, 3).Value = "kJ/[mol.K]"
                    .Cells(38, 3).Value = "cm3/mol"

                    .Cells(43, 3).Value = "m3/m3"
                    .Cells(44, 3).Value = "%"
                    .Cells(45, 3).Value = "K"
                    .Cells(46, 3).Value = "Pa.s"
                    .Cells(47, 3).Value = "K"
                    .Cells(48, 3).Value = "Pa.s"
                    .Cells(49, 3).Value = "%"
                    .Cells(50, 3).Value = "%"
                    .Cells(51, 3).Value = "%"

                    .Cells(56, 3).Value = "K"
                    .Cells(57, 3).Value = "Pa.s"
                    .Cells(58, 3).Value = "K"
                    .Cells(59, 3).Value = "Pa.s"

                End With


                mysheet = mybook.Worksheets.Add("T-Dep Properties")

                With mysheet

                    'igcp

                    .Cells(1, 1).Value = "IDEAL GAS HEAT CAPACITY"
                    .Cells(2, 1).Value = "Equation ID"
                    .Cells(3, 1).Value = "Equation String"
                    .Cells(4, 1).Value = "Temperature Units"
                    .Cells(5, 1).Value = "Heat Capacity Units"
                    .Cells(6, 1).Value = "A"
                    .Cells(7, 1).Value = "B"
                    .Cells(8, 1).Value = "C"
                    .Cells(9, 1).Value = "D"
                    .Cells(10, 1).Value = "E"
                    .Cells(11, 1).Value = "Tmin"
                    .Cells(12, 1).Value = "Tmax"

                    If Integer.TryParse(IdealgasCpEquation, New Integer) Then
                        .Cells(2, 2).Value = IdealgasCpEquation
                        .Cells(3, 2).Value = PropertyPackages.PropertyPackage.GetEquationString(IdealgasCpEquation)
                    ElseIf IdealgasCpEquation = "" Then
                        .Cells(2, 2).Value = "Estimated"
                        .Cells(3, 2).Value = ""
                    Else
                        .Cells(2, 2).Value = "User-Defined"
                        .Cells(3, 2).Value = IdealgasCpEquation
                    End If
                    .Cells(4, 2).Value = "K"
                    Select Case OriginalDB
                        Case "DWSIM"
                            .Cells(5, 2).Value = "kJ/[kmol.K]"
                        Case "ChemSep", "ChEDL Thermo", "User"
                            .Cells(5, 2).Value = "J/[kmol.K]"
                    End Select
                    .Cells(6, 2).Value = Ideal_Gas_Heat_Capacity_Const_A
                    .Cells(7, 2).Value = Ideal_Gas_Heat_Capacity_Const_B
                    .Cells(8, 2).Value = Ideal_Gas_Heat_Capacity_Const_C
                    .Cells(9, 2).Value = Ideal_Gas_Heat_Capacity_Const_D
                    .Cells(10, 2).Value = Ideal_Gas_Heat_Capacity_Const_E
                    .Cells(11, 2).Value = "N/A"
                    .Cells(12, 2).Value = "N/A"

                    .Cells(14, 1).Value = "TABULATED DATA"
                    .Cells(15, 1).Value = "T (K)"
                    .Cells(15, 2).Value = "IG Cp (kJ/[kg.K])"

                    Dim Tmin, Tmax, Tit As Double
                    If TemperatureOfFusion > 0 Then Tmin = TemperatureOfFusion Else Tmin = 0.3 * Normal_Boiling_Point
                    Tmax = 2 * Critical_Temperature

                    Tit = Tmin
                    Dim i As Integer = 1
                    While Tit <= Tmax
                        .Cells(15 + i, 1).Value = Tit
                        .Cells(15 + i, 2).Value = GetIdealGasHeatCapacity(Tit)
                        Tit += (Tmax - Tmin) / 50.0
                        i += 1
                    End While

                    'vapor pressure

                    .Cells(1, 4).Value = "VAPOR PRESSURE"
                    .Cells(2, 4).Value = "Equation ID"
                    .Cells(3, 4).Value = "Equation String"
                    .Cells(4, 4).Value = "Temperature Units"
                    .Cells(5, 4).Value = "Vapor Pressure Units"
                    .Cells(6, 4).Value = "A"
                    .Cells(7, 4).Value = "B"
                    .Cells(8, 4).Value = "C"
                    .Cells(9, 4).Value = "D"
                    .Cells(10, 4).Value = "E"
                    .Cells(11, 4).Value = "Tmin"
                    .Cells(12, 4).Value = "Tmax"

                    If Integer.TryParse(VaporPressureEquation, New Integer) Then
                        .Cells(2, 5).Value = VaporPressureEquation
                        .Cells(3, 5).Value = PropertyPackages.PropertyPackage.GetEquationString(VaporPressureEquation)
                    ElseIf VaporPressureEquation = "" Then
                        .Cells(2, 5).Value = "Estimated"
                        .Cells(3, 5).Value = "Lee-Kesler Vapor Pressure Correlation"
                    Else
                        .Cells(2, 5).Value = "User-Defined"
                        .Cells(3, 5).Value = VaporPressureEquation
                    End If
                    .Cells(4, 5).Value = "K"
                    .Cells(5, 5).Value = "Pa"
                    .Cells(6, 5).Value = Vapor_Pressure_Constant_A
                    .Cells(7, 5).Value = Vapor_Pressure_Constant_B
                    .Cells(8, 5).Value = Vapor_Pressure_Constant_C
                    .Cells(9, 5).Value = Vapor_Pressure_Constant_D
                    .Cells(10, 5).Value = Vapor_Pressure_Constant_E
                    .Cells(11, 5).Value = Vapor_Pressure_TMIN
                    .Cells(12, 5).Value = Vapor_Pressure_TMAX

                    .Cells(14, 4).Value = "TABULATED DATA"
                    .Cells(15, 4).Value = "T (K)"
                    .Cells(15, 5).Value = "Vapor Pressure (Pa)"

                    If TemperatureOfFusion > 0 Then Tmin = TemperatureOfFusion Else Tmin = 0.3 * Normal_Boiling_Point
                    Tmax = Critical_Temperature

                    Tit = Tmin
                    i = 1
                    While Tit <= Tmax
                        .Cells(15 + i, 4).Value = Tit
                        .Cells(15 + i, 5).Value = GetVaporPressure(Tit)
                        Tit += (Tmax - Tmin) / 50.0
                        i += 1
                    End While

                    'liquid density

                    .Cells(1, 7).Value = "LIQUID DENSITY"
                    .Cells(2, 7).Value = "Equation ID"
                    .Cells(3, 7).Value = "Equation String"
                    .Cells(4, 7).Value = "Temperature Units"
                    .Cells(5, 7).Value = "Liquid Density Units"
                    .Cells(6, 7).Value = "A"
                    .Cells(7, 7).Value = "B"
                    .Cells(8, 7).Value = "C"
                    .Cells(9, 7).Value = "D"
                    .Cells(10, 7).Value = "E"
                    .Cells(11, 7).Value = "Tmin"
                    .Cells(12, 7).Value = "Tmax"

                    If Integer.TryParse(LiquidDensityEquation, New Integer) Then
                        .Cells(2, 8).Value = LiquidDensityEquation
                        .Cells(3, 8).Value = PropertyPackages.PropertyPackage.GetEquationString(LiquidDensityEquation)
                    ElseIf LiquidDensityEquation = "" Then
                        .Cells(2, 8).Value = "Estimated"
                        .Cells(3, 8).Value = "Rackett Correlation"
                    Else
                        .Cells(2, 8).Value = "User-Defined"
                        .Cells(3, 8).Value = LiquidDensityEquation
                    End If
                    .Cells(4, 8).Value = "K"
                    .Cells(5, 8).Value = "kg/m3"
                    .Cells(6, 8).Value = Liquid_Density_Const_A
                    .Cells(7, 8).Value = Liquid_Density_Const_B
                    .Cells(8, 8).Value = Liquid_Density_Const_C
                    .Cells(9, 8).Value = Liquid_Density_Const_D
                    .Cells(10, 8).Value = Liquid_Density_Const_E
                    .Cells(11, 8).Value = Liquid_Density_Tmin
                    .Cells(12, 8).Value = Liquid_Density_Tmax

                    .Cells(14, 7).Value = "TABULATED DATA"
                    .Cells(15, 7).Value = "T (K)"
                    .Cells(15, 8).Value = "Liquid Density (kg/m3)"

                    If TemperatureOfFusion > 0 Then Tmin = TemperatureOfFusion Else Tmin = 0.3 * Normal_Boiling_Point
                    Tmax = Critical_Temperature

                    Tit = Tmin
                    i = 1
                    While Tit <= Tmax
                        .Cells(15 + i, 7).Value = Tit
                        .Cells(15 + i, 8).Value = GetLiquidDensity(Tit)
                        Tit += (Tmax - Tmin) / 50.0
                        i += 1
                    End While

                    'liquid viscosity

                    .Cells(1, 10).Value = "LIQUID VISCOSITY"
                    .Cells(2, 10).Value = "Equation ID"
                    .Cells(3, 10).Value = "Equation String"
                    .Cells(4, 10).Value = "Temperature Units"
                    .Cells(5, 10).Value = "Liquid Viscosity Units"
                    .Cells(6, 10).Value = "A"
                    .Cells(7, 10).Value = "B"
                    .Cells(8, 10).Value = "C"
                    .Cells(9, 10).Value = "D"
                    .Cells(10, 10).Value = "E"
                    .Cells(11, 10).Value = "Tmin"
                    .Cells(12, 10).Value = "Tmax"

                    If Integer.TryParse(LiquidViscosityEquation, New Integer) Then
                        .Cells(2, 11).Value = LiquidViscosityEquation
                        .Cells(3, 11).Value = PropertyPackages.PropertyPackage.GetEquationString(LiquidViscosityEquation)
                    ElseIf LiquidViscosityEquation = "" Then
                        .Cells(2, 11).Value = "Estimated"
                        .Cells(3, 11).Value = "Letsou-Stiel Correlation"
                    Else
                        .Cells(2, 11).Value = "User-Defined"
                        .Cells(3, 11).Value = LiquidViscosityEquation
                    End If
                    .Cells(4, 11).Value = "K"
                    .Cells(5, 11).Value = "Pa.s"
                    .Cells(6, 11).Value = Liquid_Viscosity_Const_A
                    .Cells(7, 11).Value = Liquid_Viscosity_Const_B
                    .Cells(8, 11).Value = Liquid_Viscosity_Const_C
                    .Cells(9, 11).Value = Liquid_Viscosity_Const_D
                    .Cells(10, 11).Value = Liquid_Viscosity_Const_E
                    .Cells(11, 11).Value = "N/A"
                    .Cells(12, 11).Value = "N/A"

                    .Cells(14, 10).Value = "TABULATED DATA"
                    .Cells(15, 10).Value = "T (K)"
                    .Cells(15, 11).Value = "Liquid Viscosity (Pa.s)"

                    If TemperatureOfFusion > 0 Then Tmin = TemperatureOfFusion Else Tmin = 0.3 * Normal_Boiling_Point
                    Tmax = Critical_Temperature

                    Tit = Tmin
                    i = 1
                    While Tit <= Tmax
                        .Cells(15 + i, 10).Value = Tit
                        .Cells(15 + i, 11).Value = GetLiquidViscosity(Tit)
                        Tit += (Tmax - Tmin) / 50.0
                        i += 1
                    End While

                    'liquid heat capacity

                    .Cells(1, 13).Value = "LIQUID HEAT CAPACITY"
                    .Cells(2, 13).Value = "Equation ID"
                    .Cells(3, 13).Value = "Equation String"
                    .Cells(4, 13).Value = "Temperature Units"
                    .Cells(5, 13).Value = "Heat Capacity Units"
                    .Cells(6, 13).Value = "A"
                    .Cells(7, 13).Value = "B"
                    .Cells(8, 13).Value = "C"
                    .Cells(9, 13).Value = "D"
                    .Cells(10, 13).Value = "E"
                    .Cells(11, 13).Value = "Tmin"
                    .Cells(12, 13).Value = "Tmax"

                    If Integer.TryParse(LiquidHeatCapacityEquation, New Integer) Then
                        .Cells(2, 14).Value = LiquidHeatCapacityEquation
                        .Cells(3, 14).Value = PropertyPackages.PropertyPackage.GetEquationString(LiquidHeatCapacityEquation)
                    ElseIf LiquidHeatCapacityEquation = "" Then
                        .Cells(2, 14).Value = "Unavailable"
                        .Cells(3, 14).Value = ""
                    Else
                        .Cells(2, 14).Value = "User-Defined"
                        .Cells(3, 14).Value = LiquidHeatCapacityEquation
                    End If
                    .Cells(4, 14).Value = "K"
                    Select Case OriginalDB
                        Case "DWSIM"
                            .Cells(5, 14).Value = "kJ/[kmol.K]"
                        Case "ChemSep", "ChEDL Thermo", "User"
                            .Cells(5, 14).Value = "J/[kmol.K]"
                    End Select
                    .Cells(6, 14).Value = Liquid_Heat_Capacity_Const_A
                    .Cells(7, 14).Value = Liquid_Heat_Capacity_Const_B
                    .Cells(8, 14).Value = Liquid_Heat_Capacity_Const_C
                    .Cells(9, 14).Value = Liquid_Heat_Capacity_Const_D
                    .Cells(10, 14).Value = Liquid_Heat_Capacity_Const_E
                    .Cells(11, 14).Value = "N/A"
                    .Cells(12, 14).Value = "N/A"

                    .Cells(14, 13).Value = "TABULATED DATA"
                    .Cells(15, 13).Value = "T (K)"
                    .Cells(15, 14).Value = "Liquid Cp (kJ/[kg.K])"

                    If TemperatureOfFusion > 0 Then Tmin = TemperatureOfFusion Else Tmin = 0.3 * Normal_Boiling_Point
                    Tmax = 2 * Critical_Temperature

                    Tit = Tmin
                    i = 1
                    While Tit <= Tmax
                        .Cells(15 + i, 13).Value = Tit
                        .Cells(15 + i, 14).Value = GetLiquidHeatCapacity(Tit)
                        Tit += (Tmax - Tmin) / 50.0
                        i += 1
                    End While

                    'liquid thermal conductivity

                    .Cells(1, 16).Value = "LIQUID THERMAL CONDUCTIVITY"
                    .Cells(2, 16).Value = "Equation ID"
                    .Cells(3, 16).Value = "Equation String"
                    .Cells(4, 16).Value = "Temperature Units"
                    .Cells(5, 16).Value = "Thermal Conductivity Units"
                    .Cells(6, 16).Value = "A"
                    .Cells(7, 16).Value = "B"
                    .Cells(8, 16).Value = "C"
                    .Cells(9, 16).Value = "D"
                    .Cells(10, 16).Value = "E"
                    .Cells(11, 16).Value = "Tmin"
                    .Cells(12, 16).Value = "Tmax"

                    If Integer.TryParse(LiquidThermalConductivityEquation, New Integer) Then
                        .Cells(2, 17).Value = LiquidThermalConductivityEquation
                        .Cells(3, 17).Value = PropertyPackages.PropertyPackage.GetEquationString(LiquidThermalConductivityEquation)
                    ElseIf LiquidThermalConductivityEquation = "" Then
                        .Cells(2, 17).Value = "Estimated"
                        .Cells(3, 17).Value = "Latini Correlation"
                    Else
                        .Cells(2, 17).Value = "User-Defined"
                        .Cells(3, 17).Value = LiquidThermalConductivityEquation
                    End If
                    .Cells(4, 17).Value = "K"
                    .Cells(5, 17).Value = "W/[m.K]"
                    .Cells(6, 17).Value = Liquid_Thermal_Conductivity_Const_A
                    .Cells(7, 17).Value = Liquid_Thermal_Conductivity_Const_B
                    .Cells(8, 17).Value = Liquid_Thermal_Conductivity_Const_C
                    .Cells(9, 17).Value = Liquid_Thermal_Conductivity_Const_D
                    .Cells(10, 17).Value = Liquid_Thermal_Conductivity_Const_E
                    .Cells(11, 17).Value = Liquid_Thermal_Conductivity_Tmin
                    .Cells(12, 17).Value = Liquid_Thermal_Conductivity_Tmax

                    .Cells(14, 16).Value = "TABULATED DATA"
                    .Cells(15, 16).Value = "T (K)"
                    .Cells(15, 17).Value = "Liquid Thermal Conductivity (W/[m.K])"

                    If TemperatureOfFusion > 0 Then Tmin = TemperatureOfFusion Else Tmin = 0.3 * Normal_Boiling_Point
                    Tmax = 2 * Critical_Temperature

                    Tit = Tmin
                    i = 1
                    While Tit <= Tmax
                        .Cells(15 + i, 16).Value = Tit
                        .Cells(15 + i, 17).Value = GetLiquidThermalConductivity(Tit)
                        Tit += (Tmax - Tmin) / 50.0
                        i += 1
                    End While

                    'solid density

                    .Cells(1, 19).Value = "SOLID DENSITY"
                    .Cells(2, 19).Value = "Equation ID"
                    .Cells(3, 19).Value = "Equation String"
                    .Cells(4, 19).Value = "Temperature Units"
                    .Cells(5, 19).Value = "Solid Density Units"
                    .Cells(6, 19).Value = "A"
                    .Cells(7, 19).Value = "B"
                    .Cells(8, 19).Value = "C"
                    .Cells(9, 19).Value = "D"
                    .Cells(10, 19).Value = "E"
                    .Cells(11, 19).Value = "Tmin"
                    .Cells(12, 19).Value = "Tmax"

                    If Integer.TryParse(SolidDensityEquation, New Integer) Then
                        .Cells(2, 20).Value = SolidDensityEquation
                        .Cells(3, 20).Value = PropertyPackages.PropertyPackage.GetEquationString(SolidDensityEquation)
                    ElseIf SolidDensityEquation = "" Then
                        .Cells(2, 20).Value = "Estimated"
                        .Cells(3, 20).Value = ""
                    Else
                        .Cells(2, 20).Value = "User-Defined"
                        .Cells(3, 20).Value = SolidDensityEquation
                    End If
                    .Cells(4, 20).Value = "K"
                    .Cells(5, 20).Value = "kg/m3"
                    .Cells(6, 20).Value = Solid_Density_Const_A
                    .Cells(7, 20).Value = Solid_Density_Const_B
                    .Cells(8, 20).Value = Solid_Density_Const_C
                    .Cells(9, 20).Value = Solid_Density_Const_D
                    .Cells(10, 20).Value = Solid_Density_Const_E
                    .Cells(11, 20).Value = Solid_Density_Tmin
                    .Cells(12, 20).Value = Solid_Density_Tmax

                    .Cells(14, 19).Value = "TABULATED DATA"
                    .Cells(15, 19).Value = "T (K)"
                    .Cells(15, 20).Value = "Solid Density (kg/m3)"

                    If TemperatureOfFusion > 0 Then Tmax = TemperatureOfFusion Else Tmax = 0.3 * Normal_Boiling_Point
                    Tmin = Tmax * 0.2

                    Tit = Tmin
                    i = 1
                    While Tit <= Tmax
                        .Cells(15 + i, 19).Value = Tit
                        .Cells(15 + i, 20).Value = GetSolidDensity(Tit)
                        Tit += (Tmax - Tmin) / 50.0
                        i += 1
                    End While

                    'solid heat capacity

                    .Cells(1, 22).Value = "SOLID HEAT CAPACITY"
                    .Cells(2, 22).Value = "Equation ID"
                    .Cells(3, 22).Value = "Equation String"
                    .Cells(4, 22).Value = "Temperature Units"
                    .Cells(5, 22).Value = "Heat Capacity Units"
                    .Cells(6, 22).Value = "A"
                    .Cells(7, 22).Value = "B"
                    .Cells(8, 22).Value = "C"
                    .Cells(9, 22).Value = "D"
                    .Cells(10, 22).Value = "E"
                    .Cells(11, 22).Value = "Tmin"
                    .Cells(12, 22).Value = "Tmax"

                    If Integer.TryParse(SolidHeatCapacityEquation, New Integer) Then
                        .Cells(2, 23).Value = SolidHeatCapacityEquation
                        .Cells(3, 23).Value = PropertyPackages.PropertyPackage.GetEquationString(SolidHeatCapacityEquation)
                    ElseIf SolidHeatCapacityEquation = "" Then
                        .Cells(2, 23).Value = "Estimated"
                        .Cells(3, 23).Value = ""
                    Else
                        .Cells(2, 23).Value = "User-Defined"
                        .Cells(3, 23).Value = SolidHeatCapacityEquation
                    End If
                    .Cells(4, 23).Value = "K"
                    Select Case OriginalDB
                        Case "ChemSep", "User"
                            .Cells(5, 23).Value = "J/[kmol.K]"
                        Case "ChEDL Thermo"
                            .Cells(5, 23).Value = "kJ/[kg.K]"
                    End Select
                    .Cells(6, 23).Value = Solid_Heat_Capacity_Const_A
                    .Cells(7, 23).Value = Solid_Heat_Capacity_Const_B
                    .Cells(8, 23).Value = Solid_Heat_Capacity_Const_C
                    .Cells(9, 23).Value = Solid_Heat_Capacity_Const_D
                    .Cells(10, 23).Value = Solid_Heat_Capacity_Const_E
                    .Cells(11, 23).Value = Solid_Heat_Capacity_Tmin
                    .Cells(12, 23).Value = Solid_Heat_Capacity_Tmax

                    .Cells(14, 22).Value = "TABULATED DATA"
                    .Cells(15, 22).Value = "T (K)"
                    .Cells(15, 23).Value = "Solid Cp (kJ/[kg.K])"

                    If TemperatureOfFusion > 0 Then Tmax = TemperatureOfFusion Else Tmax = 0.3 * Normal_Boiling_Point
                    Tmin = Tmax * 0.2

                    Tit = Tmin
                    i = 1
                    While Tit <= Tmax
                        .Cells(15 + i, 22).Value = Tit
                        .Cells(15 + i, 23).Value = GetSolidHeatCapacity(Tit)
                        Tit += (Tmax - Tmin) / 50.0
                        i += 1
                    End While

                End With

                xcl.SaveAs(New FileInfo(filepath))

            End Using

        End Sub

    End Class

    <System.Serializable()> Public Class ConstantPropertiesCollection
        Public Collection() As ConstantProperties
    End Class

#End Region

End Namespace
