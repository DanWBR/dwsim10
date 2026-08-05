'    Stream Classes
'    Copyright 2008-2011 Daniel Wagner O. de Medeiros
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


Imports CapeOpen
Imports System.Linq
Imports DWSIM.Thermodynamics.PropertyPackages
Imports DWSIM.Thermodynamics
Imports System.Runtime.InteropServices
Imports System.Threading.Tasks
Imports System.Runtime.Serialization
Imports System.Reflection
Imports DWSIM.Interfaces
Imports DWSIM.Interfaces.Enums
Imports DWSIM.SharedClasses.UnitOperations
Imports DWSIM.SharedClasses

''' <summary>
''' Contains the energy stream class representing power or heat flow connections between unit operations in a flowsheet.
''' </summary>
Namespace Streams

    ''' <summary>
    ''' Represents an energy stream that carries a heat or power flow between unit operations in a flowsheet.
    ''' Implements CAPE-OPEN identification and collection interfaces.
    ''' </summary>
    <System.Serializable()> <ComVisible(True)> Public Partial Class EnergyStream

        Inherits BaseClass

        Implements ICapeIdentification, ICapeCollection, IEnergyStream

        'CAPE-OPEN Error Interfaces
        Implements ECapeUser, ECapeUnknown, ECapeRoot

        <NonSerialized> <Xml.Serialization.XmlIgnore> Public f As Object

        Private WithEvents m_work As CapeOpen.RealParameter
        Private WithEvents m_tLow As CapeOpen.RealParameter
        Private WithEvents m_tUp As CapeOpen.RealParameter

        Private initialized As Boolean = False

        ''' <summary>
        ''' Gets or sets the simulation object class for this energy stream.
        ''' </summary>
        Public Overrides Property ObjectClass As SimulationObjectClass = SimulationObjectClass.Streams

        ''' <summary>
        ''' Gets a value indicating whether this energy stream supports dynamic simulation mode.
        ''' </summary>
        Public Overrides ReadOnly Property SupportsDynamicMode As Boolean = True

        ''' <summary>
        ''' Gets a value indicating whether this energy stream exposes properties specific to dynamic mode.
        ''' </summary>
        Public Overrides ReadOnly Property HasPropertiesForDynamicMode As Boolean = False

#Region "   CAPE-OPEN ICapeIdentification"

        ''' <summary>
        ''' Gets or sets the CAPE-OPEN component description for this energy stream.
        ''' </summary>
        Public Overrides Property ComponentDescription() As String = "" Implements CapeOpen.ICapeIdentification.ComponentDescription

        ''' <summary>
        ''' Gets or sets the CAPE-OPEN component name for this energy stream.
        ''' </summary>
        Public Overrides Property ComponentName() As String = "" Implements CapeOpen.ICapeIdentification.ComponentName

#End Region

#Region "   DWSIM Specific"

        ''' <summary>
        ''' Initializes a new default instance of the <see cref="EnergyStream"/> class.
        ''' </summary>
        Public Sub New()
            MyBase.New()
        End Sub

        ''' <summary>
        ''' Initializes a new instance of the <see cref="EnergyStream"/> class with a name and description.
        ''' </summary>
        ''' <param name="name">The display name of the energy stream.</param>
        ''' <param name="description">A brief description of the energy stream.</param>
        Public Sub New(ByVal name As String, ByVal description As String)

            MyBase.CreateNew()
            Me.ComponentName = name
            Me.ComponentDescription = description
            Init()

        End Sub

        Sub Init()

            If Type.GetType("Mono.Runtime") Is Nothing Then CreateParamCol()
            initialized = True

        End Sub

        Sub CreateParamCol()

            m_work = New CapeOpen.RealParameter("work", Me.EnergyFlow.GetValueOrDefault, 0.0#, "J/s")
            m_tLow = New CapeOpen.RealParameter("temperatureLow", 0.0, 0.0#, "K")
            m_tUp = New CapeOpen.RealParameter("temperatureHigh", 2000.0, 2000.0#, "K")

        End Sub

        Private _eflow As Double?

        ''' <summary>
        ''' Power (energy) associated with this stream.
        ''' </summary>
        ''' <value></value>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Public Overrides Property EnergyFlow() As Double?
            Get
                Return _eflow
            End Get
            Set(value As Double?)
                _eflow = value
                SetDirtyStatus(True)
            End Set
        End Property

        ''' <summary>
        ''' Sets the energy flow of this stream directly using a value in kilowatts.
        ''' </summary>
        ''' <param name="energyflow_kW">The energy flow value in kilowatts (kW).</param>
        Public Sub SetValue(ByVal energyflow_kW As Double)
            EnergyFlow = energyflow_kW
        End Sub

        ''' <summary>
        ''' Copies the energy flow property from the specified source energy stream to this instance.
        ''' </summary>
        ''' <param name="ASource">The source <see cref="EnergyStream"/> whose properties are copied.</param>
        Public Sub Assign(ByVal ASource As EnergyStream)

            'Copy properties from the ASource stream.

            Me.EnergyFlow = ASource.EnergyFlow

        End Sub

        ''' <summary>
        ''' Marks the energy stream as calculated (clears its dirty state).
        ''' </summary>
        ''' <param name="args">Optional calculation arguments (not used).</param>
        Public Overrides Sub Calculate(Optional args As Object = Nothing)

            SetDirtyStatus(False)

        End Sub

        ''' <summary>
        ''' Returns the value of the specified property converted to the given unit system.
        ''' </summary>
        ''' <param name="prop">The property identifier string (e.g., "PROP_ES_0").</param>
        ''' <param name="su">The unit system to use for conversion; defaults to SI if not provided.</param>
        ''' <returns>The property value as an <see cref="Object"/> in the requested unit system.</returns>
        Public Overrides Function GetPropertyValue(ByVal prop As String, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As Object

            If su Is Nothing Then su = New SystemsOfUnits.SI

            Dim val0 As Object = MyBase.GetPropertyValue(prop, su)

            If val0 Is Nothing Then

                Dim epcol = DirectCast(ExtraProperties, IDictionary(Of String, Object))
                Dim epucol = DirectCast(ExtraPropertiesUnitTypes, IDictionary(Of String, Object))

                If epcol.ContainsKey(prop) Then

                    If epucol.ContainsKey(prop) Then
                        Dim utype = epucol(prop)
                        If su Is Nothing Then
                            Return Convert.ToDouble(epcol(prop)).ConvertFromSI(SharedClasses.SystemsOfUnits.Converter.SharedSI.GetCurrentUnits(utype))
                        Else
                            Return Convert.ToDouble(epcol(prop)).ConvertFromSI(su.GetCurrentUnits(utype))
                        End If
                    Else
                        Return epcol(prop)
                    End If

                Else

                    Dim cv As New SystemsOfUnits.Converter
                    Dim value As Double = 0
                    Dim propidx As Integer = -1

                    Try
                        propidx = Convert.ToInt32(prop.Split("_")(2))
                    Catch ex As Exception

                    End Try

                    Select Case propidx

                        Case 0
                            'PROP_ES_0	Power
                            value = SystemsOfUnits.Converter.ConvertFromSI(su.heatflow, Me.EnergyFlow.GetValueOrDefault)

                    End Select

                    Return value

                End If

            Else

                Return val0

            End If

        End Function

        ''' <summary>
        ''' Returns the list of property identifiers available for this energy stream filtered by the specified property type.
        ''' </summary>
        ''' <param name="proptype">The type of properties to retrieve (read-only, read-write, write-read, or all).</param>
        ''' <returns>An array of property identifier strings.</returns>
        Public Overloads Overrides Function GetProperties(ByVal proptype As Interfaces.Enums.PropertyType) As String()
            Dim i As Integer = 0
            Dim proplist As New ArrayList
            Select Case proptype
                Case PropertyType.RO
                    For i = 0 To 0
                        proplist.Add("PROP_ES_" + CStr(i))
                    Next
                Case PropertyType.RW
                    For i = 0 To 0
                        proplist.Add("PROP_ES_" + CStr(i))
                    Next
                Case PropertyType.WR
                    For i = 0 To 0
                        proplist.Add("PROP_ES_" + CStr(i))
                    Next
                Case PropertyType.ALL
                    For i = 0 To 0
                        proplist.Add("PROP_ES_" + CStr(i))
                    Next
            End Select
            Return proplist.ToArray(GetType(System.String))
            proplist = Nothing
        End Function

        ''' <summary>
        ''' Sets the value of the specified property after converting from the given unit system to SI.
        ''' </summary>
        ''' <param name="prop">The property identifier string (e.g., "PROP_ES_0").</param>
        ''' <param name="propval">The property value to set in the units of <paramref name="su"/>.</param>
        ''' <param name="su">The unit system of the supplied value; defaults to SI if not provided.</param>
        ''' <returns><c>True</c> if the property was set successfully.</returns>
        Public Overrides Function SetPropertyValue(ByVal prop As String, ByVal propval As Object, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As Boolean
            If su Is Nothing Then su = New SystemsOfUnits.SI
            Dim cv As New SystemsOfUnits.Converter
            Dim propidx As Integer = Convert.ToInt32(prop.Split("_")(2))

            Select Case propidx
                Case 0
                    'PROP_ES_0	Power
                    Me.EnergyFlow = SystemsOfUnits.Converter.ConvertToSI(su.heatflow, propval)
            End Select
            Return 1
        End Function

        ''' <summary>
        ''' Returns the unit string for the specified property in the given unit system.
        ''' </summary>
        ''' <param name="prop">The property identifier string (e.g., "PROP_ES_0").</param>
        ''' <param name="su">The unit system to use; defaults to SI if not provided.</param>
        ''' <returns>A string representing the unit of the property.</returns>
        Public Overrides Function GetPropertyUnit(ByVal prop As String, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As String
            If su Is Nothing Then su = New SystemsOfUnits.SI
            Dim value As String = ""
            Dim propidx As Integer = Convert.ToInt32(prop.Split("_")(2))

            Select Case propidx

                Case 0
                    'PROP_ES_0	Power
                    value = su.heatflow

            End Select

            Return value

        End Function

#End Region

#Region "   CAPE-OPEN"

        Private Sub m_work_OnParameterValueChanged(ByVal sender As Object, ByVal args As System.EventArgs) Handles m_work.ParameterValueChanged
            Me.EnergyFlow = m_work.SIValue / 1000
        End Sub

        ''' <summary>
        ''' Returns the number of parameters in the CAPE-OPEN parameter collection for this energy stream.
        ''' </summary>
        ''' <returns>The count of parameters exposed via the CAPE-OPEN <see cref="ICapeCollection"/> interface.</returns>
        Public Function Count() As Integer Implements CapeOpen.ICapeCollection.Count
            Return 1
        End Function

        ''' <summary>
        ''' Retrieves a CAPE-OPEN parameter from the collection by index or name.
        ''' </summary>
        ''' <param name="index">A 1-based integer index or the parameter name ("work", "temperatureLow", "temperatureHigh").</param>
        ''' <returns>The <see cref="CapeOpen.RealParameter"/> corresponding to the specified index or name.</returns>
        Public Function Item(ByVal index As Object) As Object Implements CapeOpen.ICapeCollection.Item
            If Not initialized Then Init()
            Select Case index.ToString()
                Case "1", "work"
                    Return m_work
                Case "2", "temperatureLow"
                    Return m_tLow
                Case "3", "temperatureHigh"
                    Return m_tUp
                Case Else
                    Return m_work
            End Select
        End Function

#End Region

        ''' <summary>
        ''' Returns the current energy flow value of the stream in watts (SI).
        ''' </summary>
        ''' <returns>The energy flow in watts as a <see cref="Double"/>.</returns>
        Public Overrides Function GetEnergyConsumption() As Double

            Return EnergyFlow.GetValueOrDefault()

        End Function

        ''' <summary>
        ''' Executes the dynamic simulation model step for this energy stream (no-op for energy streams).
        ''' </summary>
        Public Overrides Sub RunDynamicModel()

        End Sub

        ''' <summary>
        ''' Creates a deep copy of this energy stream by serializing and deserializing via XML.
        ''' </summary>
        ''' <returns>A new <see cref="EnergyStream"/> instance with the same data as this instance.</returns>
        Public Overrides Function CloneXML() As Object
            Dim obj As ICustomXMLSerialization = New EnergyStream()
            obj.LoadData(Me.SaveData)
            Return obj
        End Function

        ''' <summary>
        ''' Creates a deep copy of this energy stream by serializing and deserializing via JSON.
        ''' </summary>
        ''' <returns>A new <see cref="EnergyStream"/> instance with the same data as this instance.</returns>
        Public Overrides Function CloneJSON() As Object
            Return Newtonsoft.Json.JsonConvert.DeserializeObject(Of EnergyStream)(Newtonsoft.Json.JsonConvert.SerializeObject(Me))
        End Function

        ''' <summary>
        ''' Returns the raw bytes of the energy stream icon image resource.
        ''' </summary>
        ''' <returns>A byte array containing the PNG image data for the energy stream icon.</returns>
        Public Overrides Function GetIconBitmapBytes() As Byte()

            Return GetBytesFromResource("DWSIM.UnitOperations.energy_stream.png")

        End Function

        ''' <summary>
        ''' Returns the localized display description for the energy stream object type.
        ''' </summary>
        ''' <returns>A localized description string.</returns>
        Public Overrides Function GetDisplayDescription() As String
            Return ResMan.GetLocalString("ESTR_Desc")
        End Function

        ''' <summary>
        ''' Returns the localized display name for the energy stream object type.
        ''' </summary>
        ''' <returns>A localized name string.</returns>
        Public Overrides Function GetDisplayName() As String
            Return ResMan.GetLocalString("ESTR_Name")
        End Function

        ''' <summary>
        ''' Gets a value indicating whether this energy stream is compatible with the DWSIM mobile interface.
        ''' </summary>
        Public Overrides ReadOnly Property MobileCompatible As Boolean
            Get
                Return True
            End Get
        End Property

        ''' <summary>
        ''' Generates a plain-text report of the energy stream results using the specified unit system and formatting options.
        ''' </summary>
        ''' <param name="su">The unit system to use for property values in the report.</param>
        ''' <param name="ci">The culture info used for number formatting.</param>
        ''' <param name="numberformat">The numeric format string applied to values.</param>
        ''' <returns>A formatted plain-text string summarizing the energy stream results.</returns>
        Public Overrides Function GetReport(su As IUnitsOfMeasure, ci As Globalization.CultureInfo, numberformat As String) As String

            Dim str As New Text.StringBuilder

            str.AppendLine("Energy Stream : " & Me.GraphicObject.Tag)
            str.AppendLine()
            str.AppendLine("Heat Flow: " & SystemsOfUnits.Converter.ConvertFromSI(su.heatflow, Me.EnergyFlow.GetValueOrDefault).ToString(numberformat, ci) & " " & su.heatflow)

            Return str.ToString

        End Function

        ''' <summary>
        ''' Generates a structured report of the energy stream results as a list of labeled tuples for display.
        ''' </summary>
        ''' <returns>A list of <see cref="Tuple(Of ReportItemType, String())"/> entries describing the energy stream results.</returns>
        Public Overrides Function GetStructuredReport() As List(Of Tuple(Of ReportItemType, String()))

            Dim su As IUnitsOfMeasure = GetFlowsheet().FlowsheetOptions.SelectedUnitSystem
            Dim nf = GetFlowsheet().FlowsheetOptions.NumberFormat

            Dim list As New List(Of Tuple(Of ReportItemType, String()))

            list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.Label, New String() {"Results Report for Energy Stream '" & Me.GraphicObject.Tag + "'"}))
            list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.SingleColumn, New String() {"Calculated successfully on " & LastUpdated.ToString}))

            list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                            New String() {"Energy Flow",
                            EnergyFlow.GetValueOrDefault.ConvertFromSI(su.heatflow).ToString(nf),
                            su.heatflow}))

            Return list

        End Function

        ''' <summary>
        ''' Returns a human-readable description for the specified property identifier.
        ''' </summary>
        ''' <param name="p">The property identifier string.</param>
        ''' <returns>A description string for the property.</returns>
        Public Overrides Function GetPropertyDescription(p As String) As String
            Return "Amount of heat flow carried by this stream."
        End Function

#Region "    CAPE-OPEN Error Interfaces"

        ''' <summary>
        ''' Stores CAPE-OPEN error information and throws a <see cref="CapeComputationException"/> wrapping the original exception.
        ''' </summary>
        ''' <param name="ex">The original exception that triggered the CAPE-OPEN error.</param>
        ''' <param name="name">The name of the component raising the exception.</param>
        ''' <param name="description">A human-readable description of the error.</param>
        ''' <param name="interf">The name of the CAPE-OPEN interface where the error occurred.</param>
        ''' <param name="moreinfo">Additional information about the error condition.</param>
        ''' <param name="operation">The name of the operation that failed.</param>
        ''' <param name="scope">The scope in which the error occurred.</param>
        ''' <param name="code">The CAPE-OPEN error code.</param>
        Sub ThrowCAPEException(ByRef ex As Exception, ByVal name As String, ByVal description As String, ByVal interf As String, ByVal moreinfo As String, ByVal operation As String, ByVal scope As String, ByVal code As Integer)

            _code = code
            _description = description
            _interfacename = interf
            _moreinfo = moreinfo
            _operation = operation
            _scope = scope

            Throw New CapeComputationException(ex.Message.ToString, ex)

        End Sub

        ''' <summary>
        ''' Returns the current energy flow value in watts (SI) via the <see cref="IEnergyStream"/> interface.
        ''' </summary>
        ''' <returns>The energy flow in watts as a <see cref="Double"/>.</returns>
        Public Function GetEnergyFlow() As Double Implements IEnergyStream.GetEnergyFlow
            Return EnergyFlow.GetValueOrDefault()
        End Function

        ''' <summary>
        ''' Sets the energy flow value in watts (SI) via the <see cref="IEnergyStream"/> interface.
        ''' </summary>
        ''' <param name="value">The energy flow value in watts to assign to this stream.</param>
        Public Sub SetEnergyFlow(value As Double) Implements IEnergyStream.SetEnergyFlow
            EnergyFlow = value
        End Sub

        Private _description, _interfacename, _moreinfo, _operation, _scope As String, _code As Integer

        ''' <summary>
        ''' Gets the CAPE-OPEN root name of this component, used by the ECapeRoot interface.
        ''' </summary>
        Public ReadOnly Property Name2() As String Implements CapeOpen.ECapeRoot.Name
            Get
                Return Me.Name
            End Get
        End Property

        ''' <summary>
        ''' Gets the CAPE-OPEN error code from the most recent exception thrown by this object.
        ''' </summary>
        Public ReadOnly Property code() As Integer Implements CapeOpen.ECapeUser.code
            Get
                Return _code
            End Get
        End Property

        ''' <summary>
        ''' Gets the CAPE-OPEN error description from the most recent exception thrown by this object.
        ''' </summary>
        Public ReadOnly Property description() As String Implements CapeOpen.ECapeUser.description
            Get
                Return _description
            End Get
        End Property

        ''' <summary>
        ''' Gets the name of the CAPE-OPEN interface that raised the most recent exception.
        ''' </summary>
        Public ReadOnly Property interfaceName() As String Implements CapeOpen.ECapeUser.interfaceName
            Get
                Return _interfacename
            End Get
        End Property

        ''' <summary>
        ''' Gets additional information about the most recent CAPE-OPEN exception.
        ''' </summary>
        Public ReadOnly Property moreInfo() As String Implements CapeOpen.ECapeUser.moreInfo
            Get
                Return _moreinfo
            End Get
        End Property

        ''' <summary>
        ''' Gets the name of the operation that raised the most recent CAPE-OPEN exception.
        ''' </summary>
        Public ReadOnly Property operation() As String Implements CapeOpen.ECapeUser.operation
            Get
                Return _operation
            End Get
        End Property

        ''' <summary>
        ''' Gets the scope description associated with the most recent CAPE-OPEN exception.
        ''' </summary>
        Public ReadOnly Property scope() As String Implements CapeOpen.ECapeUser.scope
            Get
                Return _scope
            End Get
        End Property

#End Region


    End Class

End Namespace
