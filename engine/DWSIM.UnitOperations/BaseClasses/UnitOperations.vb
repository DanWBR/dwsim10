'    Flowsheet Object Base Classes
'    Copyright 2008-2020 Daniel Wagner O. de Medeiros
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

Imports System.IO
Imports CapeOpen
Imports System.Runtime.InteropServices
Imports DWSIM.Interfaces.Enums
Imports Microsoft.Scripting.Hosting
Imports DWSIM.Thermodynamics
Imports Org.XmlUnit
Imports Org.XmlUnit.Builder
Imports System.Buffers

''' <summary>
''' Contains the base classes and core unit operation implementations for DWSIM process simulation,
''' including equipment such as heat exchangers, columns, reactors, pumps, compressors, and valves.
''' </summary>
Namespace UnitOperations

    ''' <summary>
    ''' Abstract base class for all DWSIM unit operation simulation objects.
    ''' Provides CAPE-OPEN identification, property package management, persistence,
    ''' debug reporting, and dynamic simulation support.
    ''' </summary>
    <System.Serializable()> <ComVisible(True)> Public MustInherit Class UnitOpBaseClass

        Inherits SharedClasses.UnitOperations.BaseClass

        Implements ICapeIdentification, IUnitOperation

        <NonSerialized> <Xml.Serialization.XmlIgnore> Public _pp As Interfaces.IPropertyPackage

        Public _ppid As String = ""

        Private _ppwasset As Boolean = False

        Protected _capeopenmode As Boolean = False

        Public AccumulationStream As Thermodynamics.Streams.MaterialStream

        ''' <summary>
        ''' Gets or sets the unique identifier of an external solver to be used for this unit operation.
        ''' </summary>
        Public Property ExternalSolverID As String = ""

        ''' <summary>
        ''' Gets or sets serialized configuration data for the external solver associated with this unit operation.
        ''' </summary>
        Public Property ExternalSolverConfigData As String = ""

        ''' <summary>
        ''' Gets or sets the dictionary of particle size distribution data, keyed by compound or stream name.
        ''' </summary>
        Public Property ParticleSizeDistributions As New Dictionary(Of String, String)

        ''' <summary>
        ''' Gets a value indicating whether this unit operation supports particle size distribution calculations.
        ''' </summary>
        Public Overridable ReadOnly Property SupportsParticleSizeDistributions As Boolean = False

        ''' <summary>
        ''' Gets a value indicating whether this unit operation supports restoring its state after a solver error.
        ''' </summary>
        Public Overridable ReadOnly Property SupportsRestoreStateAfterError As Boolean = True

        Private _AttachedExtensions As List(Of IUnitOperationExtension)

        Public Property AttachedExtensions As List(Of IUnitOperationExtension) Implements IUnitOperation.AttachedExtensions
            Get
                If _AttachedExtensions Is Nothing Then _AttachedExtensions = New List(Of IUnitOperationExtension)
                Return _AttachedExtensions
            End Get
            Set(value As List(Of IUnitOperationExtension))
                _AttachedExtensions = value
            End Set
        End Property

        ''' <summary>
        ''' Initializes a new instance of <see cref="UnitOpBaseClass"/> and sets up the dimensions list.
        ''' </summary>
        Public Sub New()

            MyBase.CreateNew()
            CreateDimensionsList()

        End Sub

        ''' <summary>
        ''' Initializes the list of equipment dimensions for this unit operation.
        ''' Override to populate the <see cref="Dimensions"/> collection with dimension entries.
        ''' </summary>
        Public Overridable Sub CreateDimensionsList()

            Dimensions = New List(Of IDimension)

        End Sub

        ''' <summary>
        ''' Updates the equipment dimensions list after a successful calculation.
        ''' Override to refresh computed dimension values.
        ''' </summary>
        Public Overridable Sub UpdateDimensionsList()


        End Sub

        ''' <summary>
        ''' Solves the unit operation, optionally saving and restoring state on error
        ''' when the flowsheet option <c>RestoreUnitOperationStateAfterError</c> is enabled.
        ''' </summary>

        Public Overrides Sub Solve()
            If FlowSheet IsNot Nothing AndAlso
                FlowSheet.FlowsheetOptions.RestoreUnitOperationStateAfterError And
                SupportsRestoreStateAfterError Then
                Dim cstate = SaveData()
                Try
                    MyBase.Solve()
                    UpdateDimensionsList()
                    RunExtensions()
                Catch ex As Exception
                    LoadData(cstate)
                    Throw ex
                End Try
            Else
                MyBase.Solve()
                UpdateDimensionsList()
                RunExtensions()
            End If
        End Sub

        Private Sub RunExtensions()

            AttachedExtensions.ForEach(
                Sub(ext)
                    Try
                        ext.Run(Me)
                    Catch ex As Exception
                        FlowSheet?.ShowMessage(String.Format("{0}: failed to run extension '{1}': {2}", GraphicObject?.Tag, ext.Name, ex.Message), IFlowsheet.MessageType.Warning)
                    End Try
                End Sub)

        End Sub

        ''' <summary>
        ''' Sets the active calculation mode for this unit operation by numeric identifier.
        ''' </summary>
        ''' <param name="modeID">The integer identifier of the calculation mode to activate.</param>
        ''' <returns>A status message indicating whether the mode change was applied.</returns>
        Public Overridable Function SetCalculationMode(modeID As Integer)

            Return "Calculation Mode change through this function not supported by this Unit Operation"

        End Function

        ''' <summary>
        ''' Returns the list of available calculation mode names for this unit operation.
        ''' </summary>
        ''' <returns>An array of strings describing each supported calculation mode.</returns>
        Public Overridable Function GetCalculationModes() As String()

            Return New String() {"Getting Calculation Modes through this function not supported by this Unit Operation"}

        End Function

        ''' <summary>
        ''' Checks whether the unit operation's input data has changed since the last successful solve,
        ''' and updates the dirty flag accordingly.
        ''' </summary>
        Public Overrides Sub CheckDirtyStatus()

            If LastSolutionInputSnapshot <> "" Then

                Dim inputdirty As Boolean = False

                For Each ic In GraphicObject.InputConnectors
                    If ic.IsAttached Then
                        Dim obj = FlowSheet.SimulationObjects(ic.AttachedConnector.AttachedFrom.Name)
                        If obj.IsDirty Then
                            inputdirty = True
                            Exit For
                        End If
                    End If
                Next

                If Not inputdirty Then

                    Dim xdoc = New XDocument()
                    xdoc.Add(New XElement("Data"))
                    xdoc.Element("Data").Add(SaveData())
                    xdoc.Element("Data").Element("Calculated").Remove()
                    xdoc.Element("Data").Element("LastUpdated").Remove()
                    Dim currentdata = xdoc.ToString()

                    Dim myDiff = DiffBuilder.Compare(Org.XmlUnit.Builder.Input.FromString(currentdata))
                    myDiff.WithTest(Org.XmlUnit.Builder.Input.FromString(LastSolutionInputSnapshot))
                    Dim result = myDiff.Build()

                    If result.HasDifferences() Then
                        SetDirtyStatus(True)
                    Else
                        SetDirtyStatus(False)
                    End If

                    xdoc = Nothing

                Else

                    SetDirtyStatus(True)

                End If

            Else

                SetDirtyStatus(True)

            End If

        End Sub

        ''' <summary>
        ''' Restores the unit operation state from a list of XML elements previously produced by <see cref="SaveData"/>.
        ''' </summary>
        ''' <param name="data">The list of <see cref="XElement"/> objects containing serialized state.</param>
        ''' <returns><c>True</c> if the data was loaded successfully.</returns>
        Public Overrides Function LoadData(data As System.Collections.Generic.List(Of System.Xml.Linq.XElement)) As Boolean

            MyBase.LoadData(data)

            Dim dimel = (From xel As XElement In data Select xel Where xel.Name = "Dimensions").FirstOrDefault
            If Not dimel Is Nothing Then
                Dimensions = New List(Of IDimension)
                For Each xel In dimel.Elements
                    Dim as1 As New SharedClasses.Dimension()
                    as1.LoadData(xel.Elements.ToList)
                    Dimensions.Add(as1)
                Next
            End If

            Dim ael = (From xel As XElement In data Select xel Where xel.Name = "AccumulationStream").FirstOrDefault

            If Not ael Is Nothing Then
                AccumulationStream = New Thermodynamics.Streams.MaterialStream()
                AccumulationStream.LoadData(ael.Elements.ToList)
                For Each phase In AccumulationStream.Phases.Values
                    For Each comp In phase.Compounds.Values
                        comp.ConstantProperties = FlowSheet.SelectedCompounds(comp.Name)
                    Next
                Next
            End If

            Dim aeel = (From xel As XElement In data Select xel Where xel.Name = "AttachedExtensions").FirstOrDefault

            If Not aeel Is Nothing Then
                AttachedExtensions.Clear()
                For Each xel In aeel.Elements
                    Try
                        Dim ext As IUnitOperationExtension = GetFlowsheet()?.AvailableUnitOperationExtensions(xel.Attribute("Name").Value).NewInstance()
                        AttachedExtensions.Add(ext)
                    Catch ex As Exception
                    End Try
                Next
            End If

            Try
                Me._ppid = (From xel As XElement In data Select xel Where xel.Name = "PropertyPackage").SingleOrDefault.Value
            Catch
            End Try

            Return True

        End Function

        ''' <summary>
        ''' Serializes the unit operation state to a list of XML elements for persistence.
        ''' </summary>
        ''' <returns>A list of <see cref="XElement"/> objects representing the current state.</returns>
        Public Overrides Function SaveData() As System.Collections.Generic.List(Of System.Xml.Linq.XElement)

            Dim elements As List(Of XElement) = MyBase.SaveData()

            Dim ppid As String = ""
            If _ppid <> "" Then
                ppid = _ppid
            ElseIf Not _pp Is Nothing Then
                ppid = _pp.Name
            Else
                ppid = ""
            End If

            elements.Add(New XElement("PropertyPackage", ppid))

            If AccumulationStream IsNot Nothing Then
                elements.Add(New XElement("AccumulationStream", AccumulationStream.SaveData()))
            End If

            If Dimensions IsNot Nothing Then
                Dim dimel As New XElement("Dimensions")
                elements.Add(dimel)
                For Each dimension In Dimensions
                    dimel.Add(New XElement("Dimension", DirectCast(dimension, SharedClasses.Dimension).SaveData()))
                Next
            End If

            Dim aeel As New XElement("AttachedExtensions")
            elements.Add(aeel)
            For Each item In AttachedExtensions
                aeel.Add(New XElement("Extension", New XAttribute("Name", item.Name)))
            Next

            Return elements

        End Function

        ''' <summary>
        ''' Returns the dynamic residence time of this unit operation, computed as volume divided
        ''' by the total volumetric inlet flow rate.
        ''' </summary>
        ''' <returns>The residence time in seconds, or <see cref="Double.NaN"/> if it cannot be determined.</returns>
        Public Overrides Function GetDynamicResidenceTime() As Double
            If GetDynamicProperty("Volume") IsNot Nothing Then
                Try
                    Dim q As Double = 0.0
                    For Each inlet In GraphicObject.InputConnectors
                        If inlet.IsAttached And inlet.Type = GraphicObjects.ConType.ConIn Then
                            q += Convert.ToDouble(inlet.AttachedConnector.AttachedFrom.Owner.GetPropertyValue("PROP_MS_4"))
                        End If
                    Next
                    Dim v = Convert.ToDouble(GetDynamicProperty("Volume"))
                    Return v / q
                Catch ex As Exception
                    Return Double.NaN
                End Try
            Else
                Return Double.NaN
            End If
        End Function

        ''' <summary>
        ''' Returns the dynamic volume of this unit operation from its dynamic property store.
        ''' </summary>
        ''' <returns>The volume in cubic metres, or <see cref="Double.NaN"/> if the property is not set.</returns>
        Public Overrides Function GetDynamicVolume() As Double
            If GetDynamicProperty("Volume") IsNot Nothing Then
                Return Convert.ToDouble(GetDynamicProperty("Volume"))
            Else
                Return Double.NaN
            End If
        End Function

        ''' <summary>
        ''' Returns the current mass held in the accumulation stream of this unit operation.
        ''' </summary>
        ''' <returns>The accumulated mass flow in kg/s, or <see cref="Double.NaN"/> if there is no accumulation stream.</returns>
        Public Overrides Function GetDynamicContents() As Double
            If AccumulationStream IsNot Nothing Then
                Return AccumulationStream.GetMassFlow()
            Else
                Return Double.NaN
            End If
        End Function

        Public Overrides Function SaveDynamicState() As Object
            If AccumulationStream IsNot Nothing Then
                'a content stream loaded from a file has no flowsheet or property package
                'until the dynamic model runs; CloneXML needs both
                If AccumulationStream.FlowSheet Is Nothing Then AccumulationStream.SetFlowsheet(FlowSheet)
                If AccumulationStream.PropertyPackage Is Nothing Then AccumulationStream.PropertyPackage = PropertyPackage
                Return AccumulationStream.CloneXML()
            End If
            Return Nothing
        End Function

        Public Overrides Sub RestoreDynamicState(state As Object)
            If state IsNot Nothing Then
                AccumulationStream = DirectCast(state, Thermodynamics.Streams.MaterialStream)
            Else
                AccumulationStream = Nothing
            End If
        End Sub

        ''' <summary>
        ''' Returns the value of a named property, converting from SI to the current unit system.
        ''' Falls back to extra properties if the property is not found in the base implementation.
        ''' </summary>
        ''' <param name="prop">The property identifier string.</param>
        ''' <param name="su">Optional units-of-measure system used for conversion; uses the shared SI system when <c>Nothing</c>.</param>
        ''' <returns>The converted property value, or <c>Nothing</c> if the property is not recognised.</returns>
        Public Overrides Function GetPropertyValue(prop As String, Optional su As IUnitsOfMeasure = Nothing) As Object

            Dim value = MyBase.GetPropertyValue(prop, su)

            If value Is Nothing Then

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
                    Return Nothing
                End If

            Else

                Return value

            End If

        End Function

#Region "   DWSIM Specific"

        ''' <summary>
        ''' Gets or sets the list of physical dimensions (e.g. vessel diameter, length) associated with this unit operation.
        ''' </summary>
        Public Property Dimensions As List(Of IDimension) = New List(Of IDimension) Implements IUnitOperation.Dimensions

        ''' <summary>
        ''' Gets or sets the name of the equipment type currently selected for this unit operation.
        ''' </summary>
        Public Property SelectedEquipmentType As String = "" Implements IUnitOperation.SelectedEquipmentType

        ''' <summary>
        ''' Gets the list of available equipment type names supported by this unit operation.
        ''' </summary>
        Public Overridable ReadOnly Property EquipmentTypes As List(Of String) = New List(Of String)() Implements IUnitOperation.EquipmentTypes

        ''' <summary>
        ''' Returns the total energy consumed by this unit operation by summing the energy flows
        ''' from all connected energy streams and the energy connector.
        ''' </summary>
        ''' <returns>The total energy consumption in watts (W).</returns>
        Public Overrides Function GetEnergyConsumption() As Double

            If GraphicObject Is Nothing Then Return 0.0

            Dim ec As Double = 0
            For Each ic In GraphicObject.InputConnectors
                If ic.Type = GraphicObjects.ConType.ConEn And ic.IsAttached Then
                    Dim es = DirectCast(FlowSheet.SimulationObjects(ic.AttachedConnector.AttachedFrom.Name), IEnergyStream)
                    ec += es.GetEnergyFlow()
                End If
            Next
            If GraphicObject.EnergyConnector.Active Then
                If GraphicObject.EnergyConnector.IsAttached Then
                    If GraphicObject.EnergyConnector.AttachedConnector.AttachedFrom IsNot Nothing Then
                        Try
                            Dim es = DirectCast(FlowSheet.SimulationObjects(GraphicObject.EnergyConnector.AttachedConnector.AttachedFrom.Name), IEnergyStream)
                            ec += es.GetEnergyFlow()
                        Catch ex As Exception
                        End Try
                    End If
                    If GraphicObject.EnergyConnector.AttachedConnector.AttachedTo IsNot Nothing Then
                        Try
                            Dim es = DirectCast(FlowSheet.SimulationObjects(GraphicObject.EnergyConnector.AttachedConnector.AttachedTo.Name), IEnergyStream)
                            ec += es.GetEnergyFlow()
                        Catch ex As Exception
                        End Try
                    End If
                End If
            End If

            Return ec

        End Function

        ''' <summary>
        ''' Runs the unit operation in debug mode and returns a formatted text report
        ''' containing intermediate calculation steps and any error information.
        ''' </summary>
        ''' <returns>A multi-line string with the debug output from the last calculation attempt.</returns>
        Public Overrides Function GetDebugReport() As String

            Me.DebugMode = True
            Me.DebugText = ""

            Try

                Calculate(Nothing)

                Me.DebugText += vbCrLf & vbCrLf & "Calculated OK."

            Catch ex As Exception

                Dim st As New StackTrace(ex, True)
                Dim frame As StackFrame = st.GetFrame(0)
                Dim fileName As String = Path.GetFileName(frame.GetFileName)
                Dim methodName As String = frame.GetMethod().Name
                Dim line As Integer = frame.GetFileLineNumber()

                AppendDebugLine(String.Format("ERROR: exception raised on file {0}, method {1}, line {2}:", fileName, methodName, line))
                AppendDebugLine(ex.Message.ToString)

            Finally

                Me.DebugMode = False

            End Try

            Return DebugText

        End Function

        ''' <summary>
        ''' Assigns an externally supplied property package instance to this unit operation,
        ''' bypassing the flowsheet-level property package lookup.
        ''' </summary>
        ''' <param name="PP">The <see cref="IPropertyPackage"/> instance to assign.</param>
        Public Overrides Sub SetPropertyPackageInstance(PP As IPropertyPackage)

            _ppwasset = True
            _pp = PP

        End Sub

        ''' <summary>
        ''' Removes the externally set property package instance from this unit operation,
        ''' reverting to the flowsheet-level property package lookup.
        ''' </summary>
        ''' <returns><c>True</c> if an instance was present and has been cleared; otherwise <c>False</c>.</returns>
        Public Overrides Function ClearPropertyPackageInstance() As Boolean

            Dim hadvalue As Boolean = _pp IsNot Nothing

            _pp = Nothing
            _ppwasset = False

            Return hadvalue

        End Function

        ''' <summary>
        ''' Gets or sets the property package associated with this object.
        ''' </summary>
        ''' <value></value>
        ''' <returns></returns>
        ''' <remarks></remarks>
        <Xml.Serialization.XmlIgnore()> Public Overrides Property PropertyPackage() As Interfaces.IPropertyPackage
            Get
                If _ppid Is Nothing Then _ppid = ""
                If _pp IsNot Nothing And _ppwasset Then
                    Return _pp
                ElseIf _pp IsNot Nothing AndAlso FlowSheet.PropertyPackages.ContainsKey(_pp.UniqueID) Then
                    Return FlowSheet.PropertyPackages(_pp.UniqueID)
                Else
                    If FlowSheet.PropertyPackages.ContainsKey(_ppid) Then
                        Return FlowSheet.PropertyPackages(_ppid)
                    Else
                        Dim firstpp = FlowSheet.PropertyPackages.Values.FirstOrDefault()
                        If firstpp Is Nothing Then
                            Return Nothing
                        Else
                            _ppid = firstpp.UniqueID
                            Return firstpp
                        End If
                    End If
                End If
            End Get
            Set(ByVal value As Interfaces.IPropertyPackage)
                If value IsNot Nothing Then
                    If FlowSheet Is Nothing Then
                        _ppwasset = True
                        _pp = value
                    Else
                        If FlowSheet.PropertyPackages.ContainsKey(value.UniqueID) Then
                            _ppid = value.UniqueID
                        Else
                            _ppwasset = True
                            _pp = value
                        End If
                    End If
                    SetDirtyStatus(True)
                Else
                    _pp = Nothing
                End If
            End Set
        End Property

        ''' <summary>
        ''' Decalculates the object.
        ''' </summary>
        ''' <remarks></remarks>
        Public Overridable Overloads Sub DeCalculate()
            MyBase.DeCalculate()
        End Sub

        ''' <summary>
        ''' Decalculates the object.
        ''' </summary>
        ''' <remarks></remarks>
        Public Sub Unsolve()

            DeCalculate()

            Calculated = False

        End Sub

        ''' <summary>
        ''' Returns the names of the key output properties reported by this unit operation.
        ''' </summary>
        ''' <returns>A list of property name strings, or an empty list if none are defined.</returns>
        Public Overridable Function GetKeyPropertyNames() As List(Of String) Implements IUnitOperation.GetKeyPropertyNames

            Return New List(Of String)

        End Function

        ''' <summary>
        ''' Returns the current numeric value of a named key property in SI units.
        ''' </summary>
        ''' <param name="prop_name">The name of the key property to retrieve.</param>
        ''' <returns>The property value as a <see cref="Double"/>, or 0.0 if not supported.</returns>
        Public Overridable Function GetKeyPropertyValue(prop_name As String) As Double Implements IUnitOperation.GetKeyPropertyValue

            Return 0.0

        End Function

        ''' <summary>
        ''' Returns the display unit string for a named key property.
        ''' </summary>
        ''' <param name="prop_name">The name of the key property whose units are requested.</param>
        ''' <returns>The unit string (e.g. "kW", "kg/h"), or an empty string if not supported.</returns>
        Public Overridable Function GetKeyPropertyUnits(prop_name As String) As String Implements IUnitOperation.GetKeyPropertyUnits

            Return ""

        End Function

        ''' <summary>
        ''' Sets the value of a named key property, converting from the supplied units to SI internally.
        ''' </summary>
        ''' <param name="prop_name">The name of the key property to set.</param>
        ''' <param name="prop_value">The new value expressed in <paramref name="prop_units"/>.</param>
        ''' <param name="prop_units">The unit string corresponding to <paramref name="prop_value"/>.</param>
        ''' <returns>An <see cref="Exception"/> if the operation failed, or <c>Nothing</c> on success.</returns>
        Public Overridable Function SetKeyPropertyValue(prop_name As String, prop_value As Double, prop_units As String) As Exception Implements IUnitOperation.SetKeyPropertyValue

            Return Nothing

        End Function

#End Region

#Region "   CAPE-OPEN ICapeIdentification"

        ''' <summary>
        ''' Gets or sets the CAPE-OPEN component description for this unit operation.
        ''' </summary>
        Public Overrides Property ComponentDescription() As String = "" Implements CapeOpen.ICapeIdentification.ComponentDescription

        ''' <summary>
        ''' Gets or sets the CAPE-OPEN component name for this unit operation.
        ''' </summary>
        Public Overrides Property ComponentName() As String = "" Implements CapeOpen.ICapeIdentification.ComponentName

#End Region

    End Class

    ''' <summary>
    ''' Abstract base class for special (logical) unit operations such as recycle loops,
    ''' spreadsheets, and other non-physical simulation objects.
    ''' </summary>
    <System.Serializable()> Public MustInherit Class SpecialOpBaseClass

        Inherits SharedClasses.UnitOperations.BaseClass

        ''' <summary>
        ''' Gets or sets the simulation object class, which is always <see cref="SimulationObjectClass.Logical"/>
        ''' for special operations.
        ''' </summary>
        Public Overrides Property ObjectClass As SimulationObjectClass = SimulationObjectClass.Logical

        ''' <summary>
        ''' Initializes a new instance of <see cref="SpecialOpBaseClass"/>.
        ''' </summary>
        Public Sub New()

            MyBase.CreateNew()

        End Sub

    End Class

End Namespace

''' <summary>
''' Provides helper data structures used by special operations such as design specifications and sensitivity analysis.
''' </summary>
Namespace SpecialOps.Helpers

    ''' <summary>
    ''' Stores metadata about a single object-property pair used by special operations
    ''' such as the Design Specification or Sensitivity Analysis tools.
    ''' </summary>
    <System.Serializable()> Public Class SpecialOpObjectInfo

        Implements ISpecialOpObjectInfo

        ''' <summary>
        ''' Initializes a new default instance of <see cref="SpecialOpObjectInfo"/>.
        ''' </summary>
        Sub New()

        End Sub

        ''' <summary>
        ''' Gets or sets the unique identifier of the referenced simulation object.
        ''' </summary>
        Public Property ID As String = "" Implements ISpecialOpObjectInfo.ID

        ''' <summary>
        ''' Gets or sets the display name of the referenced simulation object.
        ''' </summary>
        Public Property Name As String = "" Implements ISpecialOpObjectInfo.Name

        ''' <summary>
        ''' Gets or sets the name of the property on the referenced simulation object.
        ''' </summary>
        Public Property PropertyName As String = "" Implements ISpecialOpObjectInfo.PropertyName

        ''' <summary>
        ''' Gets or sets the type name of the referenced simulation object (e.g. "MaterialStream", "Compressor").
        ''' </summary>
        Public Property ObjectType As String = "" Implements ISpecialOpObjectInfo.Type

        ''' <summary>
        ''' Gets or sets the unit-of-measure category for the referenced property (e.g. pressure, temperature).
        ''' </summary>
        Public Property UnitsType As UnitOfMeasure = UnitOfMeasure.none Implements ISpecialOpObjectInfo.UnitsType

        ''' <summary>
        ''' Gets or sets the display unit string for the referenced property (e.g. "bar", "°C").
        ''' </summary>
        Public Property Units As String = "" Implements ISpecialOpObjectInfo.Units

    End Class

End Namespace

