'    Copyright 2020 Daniel Wagner O. de Medeiros
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

Imports DWSIM.SharedClasses
Imports DWSIM.Interfaces.Enums

Namespace UnitOperations

    ''' <summary>
    ''' Input box placed on the flowsheet: a value entered in it is written directly to the property
    ''' <see cref="SelectedProperty"/> of the object named by <see cref="SelectedObjectID"/>, and reading
    ''' its "Value" property returns the current value of that target property. It has no calculation of its own.
    ''' </summary>
    <System.Serializable()> Public Partial Class Input

        Inherits UnitOperations.UnitOpBaseClass

        Implements Interfaces.IInput, IControllableObject

        ''' <summary>
        ''' Gets or sets the simulation object class category (Inputs).
        ''' </summary>
        Public Overrides Property ObjectClass As SimulationObjectClass = SimulationObjectClass.Inputs

        ''' <summary>
        ''' The classic (WinForms) editor window open for this input box, if any. Not saved with the flowsheet.
        ''' </summary>
        <NonSerialized> <Xml.Serialization.XmlIgnore> Public f As Object

        <Xml.Serialization.XmlIgnore> Public Property ControlPanel As Object Implements IControllableObject.ControlPanel

        Public Property SelectedObjectID As String = "" Implements IInput.SelectedObjectID

        Public Property SelectedProperty As String = "" Implements IInput.SelectedProperty

        Public Property SelectedPropertyType As UnitOfMeasure = UnitOfMeasure.none Implements IInput.SelectedPropertyType

        Public Property SelectedPropertyUnits As String = "" Implements IInput.SelectedPropertyUnits

        ''' <summary>
        ''' Gets a value indicating whether this input box can run in dynamic simulation mode. Always <c>True</c>.
        ''' </summary>
        Public Overrides ReadOnly Property SupportsDynamicMode As Boolean = True

        ''' <summary>
        ''' Initializes a new instance of the <see cref="Input"/> class with a name and description.
        ''' </summary>
        ''' <param name="name">The name of this input box.</param>
        ''' <param name="description">A brief description of this input box.</param>
        Public Sub New(ByVal name As String, ByVal description As String)

            MyBase.CreateNew()
            Me.ComponentName = name
            Me.ComponentDescription = description

        End Sub

        ''' <summary>
        ''' Creates a deep copy of this object by round-tripping through XML serialization.
        ''' </summary>
        ''' <returns>A new <see cref="Input"/> instance with the same property values.</returns>
        Public Overrides Function CloneXML() As Object
            Dim obj As ICustomXMLSerialization = New Input()
            obj.LoadData(Me.SaveData)
            Return obj
        End Function

        ''' <summary>
        ''' Initializes a new default instance of the <see cref="Input"/> class.
        ''' </summary>
        Public Sub New()
            MyBase.New()
        End Sub

        Public Overrides Sub Calculate(Optional ByVal args As Object = Nothing)

        End Sub

        Public Overrides Sub DeCalculate()

        End Sub

        ''' <summary>The object the input writes to, or Nothing when none is selected or it no longer exists.</summary>
        Private Function GetSelectedObject() As Interfaces.ISimulationObject
            Dim fs = GetFlowsheet()
            If fs Is Nothing OrElse String.IsNullOrEmpty(SelectedObjectID) Then Return Nothing
            Return fs.SimulationObjects.Values.Where(Function(x) x.Name = SelectedObjectID).FirstOrDefault
        End Function

        ''' <summary>Readable names for the property identifiers, which are the .NET property names.</summary>
        Public Overrides Function GetPropertyDescription(prop As String) As String
            Select Case prop
                Case "Value" : Return "Input Value"
                Case "SelectedObjectTag" : Return "Target Object"
                Case "SelectedProperty" : Return "Target Property"
                Case "SelectedPropertyUnits" : Return "Display Units"
                Case Else : Return MyBase.GetPropertyDescription(prop)
            End Select
        End Function

        ''' <summary>
        ''' Returns the value of the specified property. "Value" is the current value of the target
        ''' property, in the given unit system (SI when none is given).
        ''' </summary>
        ''' <param name="prop">The property identifier string.</param>
        ''' <param name="su">The unit system to use; defaults to SI if not provided.</param>
        ''' <returns>The property value as an <see cref="Object"/>.</returns>
        Public Overrides Function GetPropertyValue(ByVal prop As String, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As Object

            Dim val0 As Object = MyBase.GetPropertyValue(prop, su)

            If Not val0 Is Nothing Then
                Return val0
            Else
                Select Case prop
                    Case "Value"
                        Dim obj = GetSelectedObject()
                        If obj Is Nothing OrElse String.IsNullOrEmpty(SelectedProperty) Then Return Double.NaN
                        Return obj.GetPropertyValue(SelectedProperty, If(su, New SystemsOfUnits.SI))
                    Case "SelectedObjectTag"
                        Dim obj = GetSelectedObject()
                        If obj IsNot Nothing AndAlso obj.GraphicObject IsNot Nothing Then Return obj.GraphicObject.Tag
                        Return ""
                    Case "SelectedProperty"
                        Return SelectedProperty
                    Case "SelectedPropertyUnits"
                        Return SelectedPropertyUnits
                    Case Else
                        Return Nothing
                End Select
            End If

        End Function

        ''' <summary>
        ''' Returns the list of property identifiers available for this input. The object and property
        ''' it writes to are text and are listed only under <see cref="PropertyType.ALL"/>.
        ''' </summary>
        ''' <param name="proptype">The type of properties to retrieve.</param>
        ''' <returns>An array of property identifier strings.</returns>
        Public Overloads Overrides Function GetProperties(ByVal proptype As Interfaces.Enums.PropertyType) As String()

            Dim proplist As New List(Of String)
            Dim basecol = MyBase.GetProperties(proptype)
            If basecol.Length > 0 Then proplist.AddRange(basecol)
            Select Case proptype
                Case PropertyType.RW, PropertyType.WR
                    proplist.Add("Value")
                Case PropertyType.ALL
                    proplist.AddRange({"Value", "SelectedObjectTag", "SelectedProperty", "SelectedPropertyUnits"})
            End Select
            Return proplist.ToArray()

        End Function

        ''' <summary>
        ''' Sets the value of the specified property. Setting "Value" writes it to the target property,
        ''' in the given unit system (SI when none is given).
        ''' </summary>
        ''' <param name="prop">The property identifier string.</param>
        ''' <param name="propval">The new value to assign.</param>
        ''' <param name="su">The unit system of the supplied value; defaults to SI if not provided.</param>
        ''' <returns><c>True</c> if the property was set successfully.</returns>
        Public Overrides Function SetPropertyValue(ByVal prop As String, ByVal propval As Object, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As Boolean

            If MyBase.SetPropertyValue(prop, propval, su) Then Return True

            Select Case prop
                Case "Value"
                    Dim obj = GetSelectedObject()
                    If obj IsNot Nothing AndAlso Not String.IsNullOrEmpty(SelectedProperty) Then
                        Return obj.SetPropertyValue(SelectedProperty, propval, If(su, New SystemsOfUnits.SI))
                    End If
            End Select
            Return True

        End Function

        ''' <summary>
        ''' Returns the unit string for the specified property.
        ''' </summary>
        ''' <param name="prop">The property identifier string.</param>
        ''' <param name="su">The unit system to use; defaults to SI if not provided.</param>
        ''' <returns>A unit string, or an empty string if the property has no units.</returns>
        Public Overrides Function GetPropertyUnit(ByVal prop As String, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As String

            Select Case prop
                Case "Value"
                    If su Is Nothing Then su = New SystemsOfUnits.SI
                    Dim obj = GetSelectedObject()
                    If obj IsNot Nothing AndAlso Not String.IsNullOrEmpty(SelectedProperty) Then
                        Try
                            Dim u = obj.GetPropertyUnit(SelectedProperty, su)
                            If u IsNot Nothing AndAlso u <> "NF" Then Return u
                        Catch ex As Exception
                        End Try
                    End If
                    If SelectedPropertyType <> UnitOfMeasure.none Then Return su.GetCurrentUnits(SelectedPropertyType)
                    Return ""
                Case "SelectedObjectTag", "SelectedProperty", "SelectedPropertyUnits"
                    Return ""
            End Select

            Dim u0 As String = MyBase.GetPropertyUnit(prop, su)

            If u0 <> "NF" Then
                Return u0
            Else
                Return ""
            End If

        End Function

        ''' <summary>
        ''' Returns the raw bytes of the icon image for this input box.
        ''' </summary>
        ''' <returns>A byte array containing the PNG image data for the icon.</returns>
        Public Overrides Function GetIconBitmapBytes() As Byte()

            Return GetBytesFromResource("DWSIM.UnitOperations.input.png")

        End Function

        ''' <summary>
        ''' Returns the localized description string for this object type.
        ''' </summary>
        ''' <returns>A translated description string identifying this object type.</returns>
        Public Overrides Function GetDisplayDescription() As String
            Return ResMan.GetLocalString("IN_Desc")
        End Function

        ''' <summary>
        ''' Returns the localized display name for this object type.
        ''' </summary>
        ''' <returns>A translated name string for this object type.</returns>
        Public Overrides Function GetDisplayName() As String
            Return ResMan.GetLocalString("IN_Name")
        End Function

        ''' <summary>
        ''' Gets a value indicating whether this input box is compatible with mobile interfaces. Always <c>False</c>.
        ''' </summary>
        Public Overrides ReadOnly Property MobileCompatible As Boolean
            Get
                Return False
            End Get
        End Property

    End Class

End Namespace


