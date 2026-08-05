'    Copyright Daniel Wagner O. de Medeiros
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

Imports Flee.PublicTypes
Imports DWSIM.Thermodynamics
Imports DWSIM.Thermodynamics.Streams
Imports DWSIM.SharedClasses
Imports DWSIM.UnitOperations.UnitOperations.Auxiliary
Imports DWSIM.Thermodynamics.BaseClasses
Imports DWSIM.Interfaces.Enums
Imports System.Linq

Namespace SpecialOps

    ''' <summary>
    ''' Represents an information carrier block that copies a property value from a source
    ''' simulation object to up to three target simulation objects without performing
    ''' a thermodynamic calculation.
    ''' </summary>
    <System.Serializable()> Public Partial Class InformationCarrier

        Inherits UnitOperations.SpecialOpBaseClass

        Implements IInformationCarrier

        <NonSerialized> <Xml.Serialization.XmlIgnore> Public f As Object

        ''' <summary>Gets a value indicating whether this block supports dynamic simulation mode.</summary>
        Public Overrides ReadOnly Property SupportsDynamicMode As Boolean = True

        ''' <summary>Gets a value indicating whether this block exposes properties for dynamic mode.</summary>
        Public Overrides ReadOnly Property HasPropertiesForDynamicMode As Boolean = False

        ''' <summary>Gets or sets the calculation mode that determines when the block is evaluated.</summary>
        Public Property CalculationMode As SpecCalcMode2 = SpecCalcMode2.GlobalSetting Implements IInformationCarrier.CalculationMode

        ''' <summary>
        ''' Creates a deep copy of this information carrier by serializing and deserializing via XML.
        ''' </summary>
        ''' <returns>A new <see cref="InformationCarrier"/> instance with the same data.</returns>
        Public Overrides Function CloneXML() As Object
            Dim obj As ICustomXMLSerialization = New InformationCarrier()
            obj.LoadData(Me.SaveData)
            Return obj
        End Function

        ''' <summary>
        ''' Creates a deep copy of this information carrier by serializing and deserializing via JSON.
        ''' </summary>
        ''' <returns>A new <see cref="InformationCarrier"/> instance with the same data.</returns>
        Public Overrides Function CloneJSON() As Object
            Return Newtonsoft.Json.JsonConvert.DeserializeObject(Of InformationCarrier)(Newtonsoft.Json.JsonConvert.SerializeObject(Me))
        End Function

        ''' <summary>Gets or sets whether the target object should be recalculated after the information is transferred.</summary>
        Public Property CalculateTargetObject() As Boolean

        ''' <summary>Gets or sets the metadata for the source simulation object whose property value is read.</summary>
        Public Property SourceObjectData As New SpecialOps.Helpers.SpecialOpObjectInfo

        ''' <summary>Gets or sets the metadata for the second target simulation object.</summary>
        Public Property TargetObjectData2 As New SpecialOps.Helpers.SpecialOpObjectInfo

        ''' <summary>Gets or sets the metadata for the third target simulation object.</summary>
        Public Property TargetObjectData3 As New SpecialOps.Helpers.SpecialOpObjectInfo

        ''' <summary>Gets or sets the metadata for the primary target simulation object.</summary>
        Public Property TargetObjectData As New SpecialOps.Helpers.SpecialOpObjectInfo

        ''' <summary>Gets or sets the source simulation object instance (not serialized).</summary>
        <Xml.Serialization.XmlIgnore()> Public Property SourceObject As SharedClasses.UnitOperations.BaseClass

        ''' <summary>Gets or sets the primary target simulation object instance (not serialized).</summary>
        <Xml.Serialization.XmlIgnore()> Public Property TargetObject As SharedClasses.UnitOperations.BaseClass

        ''' <summary>Gets or sets the second target simulation object instance (not serialized).</summary>
        <Xml.Serialization.XmlIgnore()> Public Property TargetObject2 As SharedClasses.UnitOperations.BaseClass

        ''' <summary>Gets or sets the third target simulation object instance (not serialized).</summary>
        <Xml.Serialization.XmlIgnore()> Public Property TargetObject3 As SharedClasses.UnitOperations.BaseClass

        ''' <summary>
        ''' Restores the information carrier state from a list of XML elements.
        ''' </summary>
        ''' <param name="data">The list of <see cref="XElement"/> objects containing the serialized state.</param>
        ''' <returns><c>True</c> if the data was loaded successfully.</returns>
        Public Overrides Function LoadData(data As System.Collections.Generic.List(Of System.Xml.Linq.XElement)) As Boolean

            Dim ci As Globalization.CultureInfo = Globalization.CultureInfo.InvariantCulture

            MyBase.LoadData(data)

            Dim xel As XElement

            xel = (From xel2 As XElement In data Select xel2 Where xel2.Name = "SourceObjectData").SingleOrDefault

            If Not xel Is Nothing Then

                With SourceObjectData
                    .ID = xel.@ID
                    .Name = xel.@Name
                    .PropertyName = xel.@Property
                    .ObjectType = xel.@ObjectType
                End With

            End If

            xel = (From xel2 As XElement In data Select xel2 Where xel2.Name = "TargetObjectData").SingleOrDefault

            If Not xel Is Nothing Then

                With TargetObjectData
                    .ID = xel.@ID
                    .Name = xel.@Name
                    .PropertyName = xel.@Property
                    .ObjectType = xel.@ObjectType
                End With

            End If

            xel = (From xel2 As XElement In data Select xel2 Where xel2.Name = "TargetObjectData2").SingleOrDefault

            If Not xel Is Nothing Then

                With TargetObjectData2
                    .ID = xel.@ID
                    .Name = xel.@Name
                    .PropertyName = xel.@Property
                    .ObjectType = xel.@ObjectType
                End With

            End If

            xel = (From xel2 As XElement In data Select xel2 Where xel2.Name = "TargetObjectData3").SingleOrDefault

            If Not xel Is Nothing Then

                With TargetObjectData3
                    .ID = xel.@ID
                    .Name = xel.@Name
                    .PropertyName = xel.@Property
                    .ObjectType = xel.@ObjectType
                End With

            End If

            Try
                Me.SourceObject = Me.FlowSheet.SimulationObjects(Me.SourceObjectData.ID)
                If Not Me.SourceObject Is Nothing Then Me.SourceObject.IsSpecAttached = True
            Catch ex As Exception
            End Try

            Try
                Me.TargetObject = Me.FlowSheet.SimulationObjects(Me.TargetObjectData.ID)
                If Not Me.TargetObject Is Nothing Then Me.TargetObject.IsSpecAttached = True
            Catch ex As Exception
            End Try

            Try
                Me.TargetObject2 = Me.FlowSheet.SimulationObjects(Me.TargetObjectData2.ID)
                If Not Me.TargetObject2 Is Nothing Then Me.TargetObject2.IsSpecAttached = True
            Catch ex As Exception
            End Try

            Try
                Me.TargetObject3 = Me.FlowSheet.SimulationObjects(Me.TargetObjectData3.ID)
                If Not Me.TargetObject3 Is Nothing Then Me.TargetObject3.IsSpecAttached = True
            Catch ex As Exception
            End Try

            Return True

        End Function

        ''' <summary>
        ''' Serializes the information carrier state to a list of XML elements for persistence.
        ''' </summary>
        ''' <returns>A list of <see cref="XElement"/> objects representing the current state.</returns>
        Public Overrides Function SaveData() As System.Collections.Generic.List(Of System.Xml.Linq.XElement)

            Dim elements As System.Collections.Generic.List(Of System.Xml.Linq.XElement) = MyBase.SaveData()
            Dim ci As Globalization.CultureInfo = Globalization.CultureInfo.InvariantCulture

            If SourceObjectData Is Nothing Then SourceObjectData = New Helpers.SpecialOpObjectInfo()

            If SourceObjectData.ID = Nothing Then SourceObjectData.ID = ""
            If SourceObjectData.Name = Nothing Then SourceObjectData.Name = ""
            If SourceObjectData.PropertyName = Nothing Then SourceObjectData.PropertyName = ""
            If SourceObjectData.ObjectType = Nothing Then SourceObjectData.ObjectType = ""

            If TargetObjectData Is Nothing Then TargetObjectData = New Helpers.SpecialOpObjectInfo()

            If TargetObjectData.ID = Nothing Then TargetObjectData.ID = ""
            If TargetObjectData.Name = Nothing Then TargetObjectData.Name = ""
            If TargetObjectData.PropertyName = Nothing Then TargetObjectData.PropertyName = ""
            If TargetObjectData.ObjectType = Nothing Then TargetObjectData.ObjectType = ""

            If TargetObjectData2 Is Nothing Then TargetObjectData2 = New Helpers.SpecialOpObjectInfo()

            If TargetObjectData2.ID = Nothing Then TargetObjectData2.ID = ""
            If TargetObjectData2.Name = Nothing Then TargetObjectData2.Name = ""
            If TargetObjectData2.PropertyName = Nothing Then TargetObjectData2.PropertyName = ""
            If TargetObjectData2.ObjectType = Nothing Then TargetObjectData2.ObjectType = ""

            If TargetObjectData3 Is Nothing Then TargetObjectData3 = New Helpers.SpecialOpObjectInfo()

            If TargetObjectData3.ID = Nothing Then TargetObjectData3.ID = ""
            If TargetObjectData3.Name = Nothing Then TargetObjectData3.Name = ""
            If TargetObjectData3.PropertyName = Nothing Then TargetObjectData3.PropertyName = ""
            If TargetObjectData3.ObjectType = Nothing Then TargetObjectData3.ObjectType = ""


            With elements
                .Add(New XElement("SourceObjectData", New XAttribute("ID", SourceObjectData.ID),
                                  New XAttribute("Name", SourceObjectData.Name),
                                  New XAttribute("Property", SourceObjectData.PropertyName),
                                  New XAttribute("Type", SourceObjectData.ObjectType)))
                .Add(New XElement("TargetObjectData", New XAttribute("ID", TargetObjectData.ID),
                                  New XAttribute("Name", TargetObjectData.Name),
                                  New XAttribute("Property", TargetObjectData.PropertyName),
                                  New XAttribute("Type", TargetObjectData.ObjectType)))
                .Add(New XElement("TargetObjectData2", New XAttribute("ID", TargetObjectData2.ID),
                                  New XAttribute("Name", TargetObjectData2.Name),
                                  New XAttribute("Property", TargetObjectData2.PropertyName),
                                  New XAttribute("Type", TargetObjectData2.ObjectType)))
                .Add(New XElement("TargetObjectData3", New XAttribute("ID", TargetObjectData3.ID),
                                  New XAttribute("Name", TargetObjectData3.Name),
                                  New XAttribute("Property", TargetObjectData3.PropertyName),
                                  New XAttribute("Type", TargetObjectData3.ObjectType)))
            End With

            Return elements

        End Function

        ''' <summary>Initializes a new default instance of the <see cref="InformationCarrier"/> class.</summary>
        Public Sub New()
            MyBase.New()
        End Sub

        ''' <summary>
        ''' Initializes a new instance of the <see cref="InformationCarrier"/> class with a name and description.
        ''' </summary>
        ''' <param name="name">The display name of the information carrier block.</param>
        ''' <param name="description">A brief description of the block.</param>
        Public Sub New(ByVal name As String, ByVal description As String)

            MyBase.CreateNew()

            SourceObjectData = New SpecialOps.Helpers.SpecialOpObjectInfo
            TargetObjectData = New SpecialOps.Helpers.SpecialOpObjectInfo
            TargetObjectData2 = New SpecialOps.Helpers.SpecialOpObjectInfo
            TargetObjectData3 = New SpecialOps.Helpers.SpecialOpObjectInfo

            Me.ComponentName = name
            Me.ComponentDescription = description

        End Sub

        ''' <summary>
        ''' Marks this information carrier as calculated when the graphic object is active.
        ''' </summary>
        ''' <param name="args">Optional calculation arguments (not used).</param>
        Public Overrides Sub Calculate(Optional ByVal args As Object = Nothing)

            If GraphicObject.Active Then

                GraphicObject.Calculated = True

            End If

        End Sub

        ''' <summary>
        ''' Returns the value of the specified property converted to the given unit system.
        ''' </summary>
        ''' <param name="prop">The property identifier string.</param>
        ''' <param name="su">The unit system to use; defaults to SI if not provided.</param>
        ''' <returns>The property value as an <see cref="Object"/>, or <c>Nothing</c> if not found.</returns>
        Public Overrides Function GetPropertyValue(ByVal prop As String, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As Object

            Dim val0 As Object = MyBase.GetPropertyValue(prop, su)

            If Not val0 Is Nothing Then
                Return val0
            Else
                Return Nothing
            End If
        End Function

        ''' <summary>
        ''' Returns the list of property identifiers available for this information carrier (always empty).
        ''' </summary>
        ''' <param name="proptype">The type of properties to retrieve.</param>
        ''' <returns>An empty array - information carriers expose no settable properties.</returns>
        Public Overloads Overrides Function GetProperties(ByVal proptype As Interfaces.Enums.PropertyType) As String()
            Dim proplist As New ArrayList
            Return proplist.ToArray(GetType(System.String))
            proplist = Nothing
        End Function

        ''' <summary>
        ''' Sets the value of the specified property; delegates to the base implementation.
        ''' </summary>
        ''' <param name="prop">The property identifier string.</param>
        ''' <param name="propval">The new property value.</param>
        ''' <param name="su">The unit system of the supplied value; defaults to SI if not provided.</param>
        ''' <returns><c>True</c> always.</returns>
        Public Overrides Function SetPropertyValue(ByVal prop As String, ByVal propval As Object, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As Boolean

            If MyBase.SetPropertyValue(prop, propval, su) Then Return True

            Return True

        End Function

        ''' <summary>
        ''' Returns the unit string for the specified property (always an empty string for this block).
        ''' </summary>
        ''' <param name="prop">The property identifier string.</param>
        ''' <param name="su">The unit system to use; defaults to SI if not provided.</param>
        ''' <returns>An empty string.</returns>
        Public Overrides Function GetPropertyUnit(ByVal prop As String, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As String

            Return ""

        End Function

        ''' <summary>Returns the raw bytes of the information carrier icon image resource.</summary>
        ''' <returns>A byte array containing the PNG image data for the icon.</returns>
        Public Overrides Function GetIconBitmapBytes() As Byte()

            Return GetBytesFromResource("DWSIM.UnitOperations.infcarrier.png")

        End Function

        ''' <summary>Returns the localized display description for the information carrier block type.</summary>
        ''' <returns>A localized description string.</returns>
        Public Overrides Function GetDisplayDescription() As String
            Return ResMan.GetLocalString("Information Carrier")
        End Function

        ''' <summary>Returns the localized display name for the information carrier block type.</summary>
        ''' <returns>A localized name string.</returns>
        Public Overrides Function GetDisplayName() As String
            Return ResMan.GetLocalString("Information Carrier")
        End Function

        ''' <summary>Gets a value indicating whether this block is compatible with the DWSIM mobile interface.</summary>
        Public Overrides ReadOnly Property MobileCompatible As Boolean
            Get
                Return False
            End Get
        End Property

    End Class

End Namespace




