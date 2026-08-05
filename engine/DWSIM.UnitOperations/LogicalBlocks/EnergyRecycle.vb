'    Energy Recycle Calculation Routines 
'    Copyright 2009 Daniel Wagner O. de Medeiros
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
Imports DWSIM.UnitOperations.SpecialOps.Helpers.Recycle

''' <summary>
''' Contains special logical block operations including recycle convergence, design specifications,
''' adjust blocks, and information carriers used to close recycle loops and enforce process constraints.
''' </summary>
Namespace SpecialOps

    ''' <summary>
    ''' Represents an energy recycle convergence block that iterates energy stream values until
    ''' the recycle loop converges within the specified tolerance.
    ''' </summary>
    <System.Serializable()> Public Partial Class EnergyRecycle

        Inherits UnitOperations.SpecialOpBaseClass

        ''' <summary>
        ''' Asked whether the recycle should keep iterating past its maximum. The host assigns
        ''' it; without one the recycle keeps going, which is what answering yes did.
        ''' </summary>
        Public Shared Property ContinuePastMaximumIterations As Func(Of String, String, Boolean)

        <NonSerialized> <Xml.Serialization.XmlIgnore> Public f As Object

        Protected m_ConvPar As ConvergenceParametersE
        Protected m_ConvHist As ConvergenceHistoryE
        Protected m_AccelMethod As AccelMethod = AccelMethod.Wegstein
        Protected m_WegPars As WegsteinParameters

        Protected m_MaxIterations As Integer = 100
        Protected m_IterationCount As Integer = 0
        Protected m_InternalCounterE As Integer = 0
        Protected m_IterationsTaken As Integer = 0

        ''' <summary>Gets a value indicating whether this energy recycle block supports dynamic simulation mode.</summary>
        Public Overrides ReadOnly Property SupportsDynamicMode As Boolean = True

        ''' <summary>Gets a value indicating whether this energy recycle block exposes properties for dynamic mode.</summary>
        Public Overrides ReadOnly Property HasPropertiesForDynamicMode As Boolean = False

        ''' <summary>
        ''' Creates a deep copy of this energy recycle block by serializing and deserializing via XML.
        ''' </summary>
        ''' <returns>A new <see cref="EnergyRecycle"/> instance with the same data.</returns>
        Public Overrides Function CloneXML() As Object
            Dim obj As ICustomXMLSerialization = New EnergyRecycle()
            obj.LoadData(Me.SaveData)
            Return obj
        End Function

        ''' <summary>
        ''' Creates a deep copy of this energy recycle block by serializing and deserializing via JSON.
        ''' </summary>
        ''' <returns>A new <see cref="EnergyRecycle"/> instance with the same data.</returns>
        Public Overrides Function CloneJSON() As Object
            Return Newtonsoft.Json.JsonConvert.DeserializeObject(Of EnergyRecycle)(Newtonsoft.Json.JsonConvert.SerializeObject(Me))
        End Function

        ''' <summary>
        ''' Restores the energy recycle state from a list of XML elements.
        ''' </summary>
        ''' <param name="data">The list of <see cref="XElement"/> objects containing the serialized state.</param>
        ''' <returns><c>True</c> if the data was loaded successfully.</returns>
        Public Overrides Function LoadData(data As System.Collections.Generic.List(Of System.Xml.Linq.XElement)) As Boolean

            Dim ci As Globalization.CultureInfo = Globalization.CultureInfo.InvariantCulture

            MyBase.LoadData(data)
            Dim xel As XElement

            xel = (From xel2 As XElement In data Select xel2 Where xel2.Name = "ConvHist").SingleOrDefault

            If Not xel Is Nothing Then
                m_ConvHist.Energy = Double.Parse(xel.@Energy, ci)
                m_ConvHist.Energy0 = Double.Parse(xel.@Energy0, ci)
                m_ConvHist.EnergyE = Double.Parse(xel.@EnergyE, ci)
                m_ConvHist.EnergyE0 = Double.Parse(xel.@EnergyE0, ci)
            End If

            xel = (From xel2 As XElement In data Select xel2 Where xel2.Name = "WegPars").SingleOrDefault

            If Not xel Is Nothing Then
                m_WegPars.AccelDelay = Double.Parse(xel.@AccelDelay, ci)
                m_WegPars.AccelFreq = Double.Parse(xel.@AccelFreq, ci)
                m_WegPars.Qmax = Double.Parse(xel.@Qmax, ci)
                m_WegPars.Qmin = Double.Parse(xel.@Qmin, ci)
            End If
            Return True
        End Function

        ''' <summary>
        ''' Serializes the energy recycle state to a list of XML elements for persistence.
        ''' </summary>
        ''' <returns>A list of <see cref="XElement"/> objects representing the current state.</returns>
        Public Overrides Function SaveData() As System.Collections.Generic.List(Of System.Xml.Linq.XElement)

            Dim elements As System.Collections.Generic.List(Of System.Xml.Linq.XElement) = MyBase.SaveData()
            Dim ci As Globalization.CultureInfo = Globalization.CultureInfo.InvariantCulture

            With elements
                .Add(New XElement("ConvHist", New XAttribute("Energy", m_ConvHist.Energy),
                                  New XAttribute("EnergyE", m_ConvHist.EnergyE),
                                  New XAttribute("Energy0", m_ConvHist.Energy0),
                                 New XAttribute("EnergyE0", m_ConvHist.EnergyE0)))
                .Add(New XElement("WegPars", New XAttribute("AccelDelay", m_WegPars.AccelDelay),
                                  New XAttribute("AccelFreq", m_WegPars.AccelFreq),
                                  New XAttribute("Qmax", m_WegPars.Qmax),
                                  New XAttribute("Qmin", m_WegPars.Qmin)))
            End With

            Return elements

        End Function

        ''' <summary>Gets or sets the total number of iterations taken during the last convergence run.</summary>
        Public Property IterationsTaken() As Integer
            Get
                Return m_IterationsTaken
            End Get
            Set(ByVal value As Integer)
                m_IterationsTaken = value
            End Set
        End Property

        ''' <summary>Gets or sets the current iteration counter during an active convergence run.</summary>
        Public Property IterationCount() As Integer
            Get
                Return m_IterationCount
            End Get
            Set(ByVal value As Integer)
                m_IterationCount = value
            End Set
        End Property

        ''' <summary>Gets or sets the Wegstein acceleration parameters used during convergence.</summary>
        Public Property WegsteinParameters() As WegsteinParameters
            Get
                Return m_WegPars
            End Get
            Set(ByVal value As WegsteinParameters)
                m_WegPars = value
            End Set
        End Property

        ''' <summary>Gets or sets the convergence acceleration method (e.g. None or Wegstein).</summary>
        Public Property AccelerationMethod() As AccelMethod
            Get
                Return m_AccelMethod
            End Get
            Set(ByVal value As AccelMethod)
                m_AccelMethod = value
            End Set
        End Property

        ''' <summary>Gets or sets the convergence tolerance parameters for the energy recycle loop.</summary>
        Public Property ConvergenceParameters() As ConvergenceParametersE
            Get
                Return m_ConvPar
            End Get
            Set(ByVal value As ConvergenceParametersE)
                m_ConvPar = value
            End Set
        End Property

        ''' <summary>Gets or sets the convergence history (current and previous energy values) for the recycle loop.</summary>
        Public Property ConvergenceHistory() As ConvergenceHistoryE
            Get
                Return m_ConvHist
            End Get
            Set(ByVal value As ConvergenceHistoryE)
                m_ConvHist = value
            End Set
        End Property

        ''' <summary>Gets or sets the maximum number of iterations allowed before convergence is considered failed.</summary>
        Public Property MaximumIterations() As Integer
            Get
                Return Me.m_MaxIterations
            End Get
            Set(ByVal value As Integer)
                Me.m_MaxIterations = value
            End Set
        End Property

        ''' <summary>Initializes a new default instance of the <see cref="EnergyRecycle"/> class.</summary>
        Public Sub New()

            MyBase.CreateNew()

            m_ConvPar = New ConvergenceParametersE
            m_ConvHist = New ConvergenceHistoryE
            m_WegPars = New WegsteinParameters

        End Sub

        ''' <summary>
        ''' Initializes a new instance of the <see cref="EnergyRecycle"/> class with a name and description.
        ''' </summary>
        ''' <param name="name">The display name of the energy recycle block.</param>
        ''' <param name="description">A brief description of the energy recycle block.</param>
        Public Sub New(ByVal name As String, ByVal description As String)

            MyBase.CreateNew()

            m_ConvPar = New ConvergenceParametersE
            m_ConvHist = New ConvergenceHistoryE
            m_WegPars = New WegsteinParameters

            Me.ComponentName = name
            Me.ComponentDescription = description



        End Sub

        ''' <summary>Executes the dynamic simulation model step for this energy recycle block (no-op).</summary>
        Public Overrides Sub RunDynamicModel()

        End Sub

        ''' <summary>
        ''' Performs one iteration of the energy recycle convergence, applying optional Wegstein acceleration,
        ''' and updates the connected outlet energy stream with the new energy flow value.
        ''' </summary>
        ''' <param name="args">Optional calculation arguments (not used).</param>
        Public Overrides Sub Calculate(Optional ByVal args As Object = Nothing)

            If Not Me.GraphicObject.OutputConnectors(0).IsAttached Then
                Throw New Exception(FlowSheet.GetTranslatedString("NohcorrentedeEnergyFlow2"))
            ElseIf Not Me.GraphicObject.InputConnectors(0).IsAttached Then
                Throw New Exception(FlowSheet.GetTranslatedString("NohcorrentedeEnergyFlow2"))
            End If

            Dim Enew As Double

            Dim ees As Streams.EnergyStream = FlowSheet.SimulationObjects(Me.GraphicObject.InputConnectors(0).AttachedConnector.AttachedFrom.Name)
            With ees

                Me.ConvergenceHistory.EnergyE = .EnergyFlow.GetValueOrDefault - Me.ConvergenceHistory.Energy

                Me.ConvergenceHistory.EnergyE0 = Me.ConvergenceHistory.Energy - Me.ConvergenceHistory.Energy0

                Me.ConvergenceHistory.Energy0 = Me.ConvergenceHistory.Energy

                Me.ConvergenceHistory.Energy = .EnergyFlow.GetValueOrDefault

            End With

            If Me.IterationCount <= 3 Then

SS:             Enew = Me.ConvergenceHistory.Energy

            Else

                Select Case Me.AccelerationMethod

                    Case AccelMethod.None

                        GoTo SS

                    Case AccelMethod.Wegstein

                        If Me.WegsteinParameters.AccelDelay <= Me.IterationCount + 3 Then

                            Dim sE, qE As Double
                            sE = (Me.ConvergenceHistory.EnergyE - Me.ConvergenceHistory.EnergyE0) / (Me.ConvergenceHistory.Energy - Me.ConvergenceHistory.Energy0)
                            qE = sE / (sE - 1)
                            If Me.WegsteinParameters.AccelFreq <= Me.m_InternalCounterE And Double.IsNaN(sE) = False And qE > Me.WegsteinParameters.Qmin And qE < Me.WegsteinParameters.Qmax Then
                                Enew = Me.ConvergenceHistory.EnergyE * (1 - qE) + Me.ConvergenceHistory.Energy * qE
                                Me.m_InternalCounterE = 0
                            Else
                                Enew = Me.ConvergenceHistory.Energy
                                Me.m_InternalCounterE += 1
                            End If

                        Else

                            GoTo SS

                        End If

                End Select

            End If

            'energy stream - update energy flow value (kW)

            Dim es As Streams.EnergyStream = FlowSheet.SimulationObjects(Me.GraphicObject.OutputConnectors(0).AttachedConnector.AttachedTo.Name)

            With es
                .EnergyFlow = Enew
                .GraphicObject.Calculated = True
            End With

            If Me.IterationCount >= Me.MaximumIterations Then
                Dim keepgoing As Boolean = True
                If ContinuePastMaximumIterations IsNot Nothing Then
                    keepgoing = ContinuePastMaximumIterations.Invoke(
                        Me.GraphicObject.Tag & " - " & FlowSheet.GetTranslatedString("Nmeromximodeiteraesa3"),
                        FlowSheet.GetTranslatedString("Onmeromximodeiteraes"))
                End If
                If Not keepgoing Then
                    GoTo final
                Else
                    Me.IterationCount = 0
                End If
            End If

            Me.IterationCount += 1

            If Math.Abs(Me.ConvergenceHistory.EnergyE) > Me.ConvergenceParameters.Energy Then

            Else
final:          Me.IterationsTaken = Me.IterationCount.ToString
                Me.IterationCount = 0
            End If

        End Sub

        ''' <summary>Resets the iteration counter, effectively unsetting the convergence state.</summary>
        Public Overloads Sub DeCalculate()

            Me.IterationCount = 0

        End Sub

        ''' <summary>
        ''' Returns the maximum value from an array-like object.
        ''' </summary>
        ''' <param name="Vv">An array whose maximum element is to be found.</param>
        ''' <returns>The maximum value found in <paramref name="Vv"/>.</returns>
        Function MAX(ByVal Vv As Object)

            Dim n = UBound(Vv)
            Dim mx As Double

            If n >= 1 Then
                Dim i As Integer = 1
                mx = Vv(i - 1)
                i = 0
                Do
                    If Vv(i) > mx Then
                        mx = Vv(i)
                    End If
                    i += 1
                Loop Until i = n + 1
                Return mx
            Else
                Return Vv(0)
            End If

        End Function

        ''' <summary>
        ''' Returns the value of the specified property converted to the given unit system.
        ''' </summary>
        ''' <param name="prop">The property identifier string (e.g., "PROP_ER_0").</param>
        ''' <param name="su">The unit system to use for conversion; defaults to SI if not provided.</param>
        ''' <returns>The property value as an <see cref="Object"/>.</returns>
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
                        'PROP_ER_0	Maximum Iterations
                        value = Me.MaximumIterations
                    Case 1
                        'PROP_ER_1	Power Tolerance
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.heatflow, Me.ConvergenceParameters.Energy)
                    Case 2
                        'PROP_ER_2	Power Error
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.heatflow, Me.ConvergenceHistory.EnergyE)
                End Select

                Return value
            End If
        End Function

        ''' <summary>
        ''' Returns the list of property identifiers available for this energy recycle block filtered by property type.
        ''' </summary>
        ''' <param name="proptype">The type of properties to retrieve.</param>
        ''' <returns>An array of property identifier strings.</returns>
        Public Overloads Overrides Function GetProperties(ByVal proptype As Interfaces.Enums.PropertyType) As String()
            Dim i As Integer = 0
            Dim proplist As New ArrayList
            Dim basecol = MyBase.GetProperties(proptype)
            If basecol.Length > 0 Then proplist.AddRange(basecol)
            Select Case proptype
                Case PropertyType.RO
                    For i = 2 To 2
                        proplist.Add("PROP_ER_" + CStr(i))
                    Next
                Case PropertyType.RW
                    For i = 0 To 2
                        proplist.Add("PROP_ER_" + CStr(i))
                    Next
                Case PropertyType.WR
                    For i = 0 To 1
                        proplist.Add("PROP_ER_" + CStr(i))
                    Next
                Case PropertyType.ALL
                    For i = 0 To 2
                        proplist.Add("PROP_ER_" + CStr(i))
                    Next
            End Select
            Return proplist.ToArray(GetType(System.String))
            proplist = Nothing
        End Function

        ''' <summary>
        ''' Sets the value of the specified property after converting from the given unit system to SI.
        ''' </summary>
        ''' <param name="prop">The property identifier string.</param>
        ''' <param name="propval">The new value in the units of <paramref name="su"/>.</param>
        ''' <param name="su">The unit system of the supplied value; defaults to SI if not provided.</param>
        ''' <returns><c>True</c> if the property was set successfully.</returns>
        Public Overrides Function SetPropertyValue(ByVal prop As String, ByVal propval As Object, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As Boolean

            If MyBase.SetPropertyValue(prop, propval, su) Then Return True

            If su Is Nothing Then su = New SystemsOfUnits.SI
            Dim cv As New SystemsOfUnits.Converter
            Dim propidx As Integer = Convert.ToInt32(prop.Split("_")(2))

            Select Case propidx

                Case 0
                    'PROP_RY_0	Maximum Iterations
                    Me.MaximumIterations = propval
                Case 1
                    'PROP_ER_1	Power Tolerance
                    Me.ConvergenceParameters.Energy = SystemsOfUnits.Converter.ConvertToSI(su.heatflow, propval)

            End Select
            Return 1
        End Function

        ''' <summary>
        ''' Returns the unit string for the specified property in the given unit system.
        ''' </summary>
        ''' <param name="prop">The property identifier string.</param>
        ''' <param name="su">The unit system to use; defaults to SI if not provided.</param>
        ''' <returns>A string representing the unit of the property.</returns>
        Public Overrides Function GetPropertyUnit(ByVal prop As String, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As String
            Dim u0 As String = MyBase.GetPropertyUnit(prop, su)

            If u0 <> "NF" Then
                Return u0
            Else
                If su Is Nothing Then su = New SystemsOfUnits.SI
                Dim cv As New SystemsOfUnits.Converter
                Dim value As String = ""
                Dim propidx As Integer = Convert.ToInt32(prop.Split("_")(2))

                Select Case propidx

                    Case 0
                        'PROP_ER_0	Maximum Iterations
                        value = ""
                    Case 1
                        'PROP_ER_1	Power Tolerance
                        value = su.heatflow
                    Case 2
                        'PROP_ER_2	Power Error
                        value = su.heatflow
                End Select

                Return value
            End If
        End Function

        ''' <summary>Returns the raw bytes of the energy recycle icon image resource.</summary>
        ''' <returns>A byte array containing the PNG image data for the icon.</returns>
        Public Overrides Function GetIconBitmapBytes() As Byte()

            Return GetBytesFromResource("DWSIM.UnitOperations.erecycle.png")

        End Function

        ''' <summary>Returns the localized display description for the energy recycle block type.</summary>
        ''' <returns>A localized description string.</returns>
        Public Overrides Function GetDisplayDescription() As String
            Return ResMan.GetLocalString("ERECY_Desc")
        End Function

        ''' <summary>Returns the localized display name for the energy recycle block type.</summary>
        ''' <returns>A localized name string.</returns>
        Public Overrides Function GetDisplayName() As String
            Return ResMan.GetLocalString("ERECY_Name")
        End Function

        ''' <summary>Gets a value indicating whether this energy recycle block is compatible with the DWSIM mobile interface.</summary>
        Public Overrides ReadOnly Property MobileCompatible As Boolean
            Get
                Return False
            End Get
        End Property
    End Class

End Namespace

''' <summary>
''' Contains convergence parameter and iteration state classes for mass and energy recycle loop calculations.
''' </summary>
Namespace SpecialOps.Helpers.Recycle

    ''' <summary>Holds the convergence tolerance parameters for an energy recycle loop.</summary>
    <System.Serializable()> Public Class ConvergenceParametersE

        ''' <summary>Gets or sets the energy tolerance in watts (W) used as the convergence criterion.</summary>
        Public Energy As Double = 0.1

        ''' <summary>Initializes a new default instance of <see cref="ConvergenceParametersE"/>.</summary>
        Sub New()

        End Sub

    End Class

    ''' <summary>Stores current and previous energy flow values for tracking the convergence history of an energy recycle loop.</summary>
    <System.Serializable()> Public Class ConvergenceHistoryE

        ''' <summary>Gets or sets the current energy flow value in watts (W).</summary>
        Public Energy As Double = 0
        ''' <summary>Gets or sets the previous energy flow value in watts (W).</summary>
        Public Energy0 As Double = 0
        ''' <summary>Gets or sets the current convergence error in watts (W).</summary>
        Public EnergyE As Double = 0
        ''' <summary>Gets or sets the previous convergence error in watts (W).</summary>
        Public EnergyE0 As Double = 0

        ''' <summary>Initializes a new default instance of <see cref="ConvergenceHistoryE"/>.</summary>
        Sub New()

        End Sub

    End Class

End Namespace


