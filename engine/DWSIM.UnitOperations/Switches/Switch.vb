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
    ''' Logical block that writes one of two values to a property of another flowsheet object: when
    ''' <see cref="IsOn"/> is <c>True</c> it applies <see cref="OnValue"/>, otherwise <see cref="OffValue"/>,
    ''' to the property <see cref="SelectedProperty"/> of the object named by <see cref="SelectedObjectID"/>.
    ''' </summary>
    <System.Serializable()> Public Partial Class Switch

        Inherits UnitOperations.UnitOpBaseClass

        Implements Interfaces.ISwitch

        ''' <summary>
        ''' Gets or sets the simulation object class category (Switches).
        ''' </summary>
        Public Overrides Property ObjectClass As SimulationObjectClass = SimulationObjectClass.Switches

        ''' <summary>
        ''' The classic (WinForms) editor window open for this switch, if any. Not saved with the flowsheet.
        ''' </summary>
        <NonSerialized> <Xml.Serialization.XmlIgnore> Public f As Object

        Public Property SelectedObjectID As String = "" Implements ISwitch.SelectedObjectID

        Public Property SelectedProperty As String = "" Implements ISwitch.SelectedProperty

        Public Property SelectedPropertyType As UnitOfMeasure = UnitOfMeasure.none Implements ISwitch.SelectedPropertyType

        Public Property SelectedPropertyUnits As String = "" Implements ISwitch.SelectedPropertyUnits

        Public Property OffValue As Double = 0.0 Implements ISwitch.OffValue

        Public Property OnValue As Double = 0.0 Implements ISwitch.OnValue

        Public Property IsOn As Boolean = False Implements ISwitch.IsOn

        ''' <summary>
        ''' Gets a value indicating whether this switch can run in dynamic simulation mode. Always <c>True</c>.
        ''' </summary>
        Public Overrides ReadOnly Property SupportsDynamicMode As Boolean = True

        ''' <summary>
        ''' Initializes a new instance of the <see cref="Switch"/> class with a name and description.
        ''' </summary>
        ''' <param name="name">The name of this switch.</param>
        ''' <param name="description">A brief description of this switch.</param>
        Public Sub New(ByVal name As String, ByVal description As String)

            MyBase.CreateNew()
            Me.ComponentName = name
            Me.ComponentDescription = description

        End Sub

        ''' <summary>
        ''' Creates a deep copy of this object by round-tripping through XML serialization.
        ''' </summary>
        ''' <returns>A new <see cref="Switch"/> instance with the same property values.</returns>
        Public Overrides Function CloneXML() As Object
            Dim obj As ICustomXMLSerialization = New Switch()
            obj.LoadData(Me.SaveData)
            Return obj
        End Function

        ''' <summary>
        ''' Creates a deep copy of this object by round-tripping through JSON serialization.
        ''' </summary>
        ''' <returns>A new <see cref="Switch"/> instance with the same property values.</returns>
        Public Overrides Function CloneJSON() As Object
            Return Newtonsoft.Json.JsonConvert.DeserializeObject(Of Switch)(Newtonsoft.Json.JsonConvert.SerializeObject(Me))
        End Function

        ''' <summary>
        ''' Initializes a new default instance of the <see cref="Switch"/> class.
        ''' </summary>
        Public Sub New()
            MyBase.New()
        End Sub

        Public Overrides Sub Calculate(Optional ByVal args As Object = Nothing)

            Dim SimObject = GetFlowsheet.SimulationObjects.Values.Where(Function(x) x.Name = SelectedObjectID).SingleOrDefault

            If IsOn Then
                SimObject.SetPropertyValue(SelectedProperty, OnValue.ConvertToSI(SelectedPropertyUnits))
            Else
                SimObject.SetPropertyValue(SelectedProperty, OffValue.ConvertToSI(SelectedPropertyUnits))
            End If

        End Sub

        Public Overrides Sub DeCalculate()

        End Sub

        ''' <summary>The object the switch writes to, or Nothing when none is selected or it no longer exists.</summary>
        Private Function GetSelectedObject() As Interfaces.ISimulationObject
            Dim fs = GetFlowsheet()
            If fs Is Nothing OrElse String.IsNullOrEmpty(SelectedObjectID) Then Return Nothing
            Return fs.SimulationObjects.Values.Where(Function(x) x.Name = SelectedObjectID).FirstOrDefault
        End Function

        ''' <summary>
        ''' The units of the switched property in the given unit system (SI when none is given): the unit
        ''' the target object reports for it, else the one of the selected unit type. Without either, the
        ''' units the on and off values are stored in.
        ''' </summary>
        Private Function GetSwitchedPropertyUnits(su As Interfaces.IUnitsOfMeasure) As String
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
            Return If(SelectedPropertyUnits, "")
        End Function

        ''' <summary>A value stored in the switch's own units, in the units of the given unit system.</summary>
        Private Function FromStoredUnits(value As Double, su As Interfaces.IUnitsOfMeasure) As Double
            Return value.ConvertToSI(If(SelectedPropertyUnits, "")).ConvertFromSI(GetSwitchedPropertyUnits(su))
        End Function

        ''' <summary>A value given in the units of the given unit system, in the switch's own units.</summary>
        Private Function ToStoredUnits(value As Double, su As Interfaces.IUnitsOfMeasure) As Double
            Return value.ConvertToSI(GetSwitchedPropertyUnits(su)).ConvertFromSI(If(SelectedPropertyUnits, ""))
        End Function

        ''' <summary>Readable names for the property identifiers, which are the .NET property names.</summary>
        Public Overrides Function GetPropertyDescription(prop As String) As String
            Select Case prop
                Case "IsOn" : Return "Switch State (1 = On, 0 = Off)"
                Case "OnValue" : Return "Value When On"
                Case "OffValue" : Return "Value When Off"
                Case "AppliedValue" : Return "Applied Value"
                Case "SelectedObjectTag" : Return "Switched Object"
                Case "SelectedProperty" : Return "Switched Property"
                Case "SelectedPropertyUnits" : Return "Value Units"
                Case Else : Return MyBase.GetPropertyDescription(prop)
            End Select
        End Function

        ''' <summary>
        ''' Returns the value of the specified property. The on, off and applied values come in the units
        ''' the target object uses for the switched property in the given unit system (SI when none is given).
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
                    Case "IsOn"
                        Return If(IsOn, 1.0, 0.0)
                    Case "OnValue"
                        Return FromStoredUnits(OnValue, su)
                    Case "OffValue"
                        Return FromStoredUnits(OffValue, su)
                    Case "AppliedValue"
                        Return FromStoredUnits(If(IsOn, OnValue, OffValue), su)
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
        ''' Returns the list of property identifiers available for this switch. The object and property
        ''' it switches are text and are listed only under <see cref="PropertyType.ALL"/>.
        ''' </summary>
        ''' <param name="proptype">The type of properties to retrieve.</param>
        ''' <returns>An array of property identifier strings.</returns>
        Public Overloads Overrides Function GetProperties(ByVal proptype As Interfaces.Enums.PropertyType) As String()

            Dim proplist As New List(Of String)
            Dim basecol = MyBase.GetProperties(proptype)
            If basecol.Length > 0 Then proplist.AddRange(basecol)
            Select Case proptype
                Case PropertyType.RO
                    proplist.Add("AppliedValue")
                Case PropertyType.RW, PropertyType.WR
                    proplist.AddRange({"IsOn", "OnValue", "OffValue"})
                Case PropertyType.ALL
                    proplist.AddRange({"IsOn", "OnValue", "OffValue", "AppliedValue",
                                      "SelectedObjectTag", "SelectedProperty", "SelectedPropertyUnits"})
            End Select
            Return proplist.ToArray()

        End Function

        ''' <summary>
        ''' Sets the value of the specified property. The on and off values are taken in the units the
        ''' target object uses for the switched property in the given unit system (SI when none is given).
        ''' </summary>
        ''' <param name="prop">The property identifier string.</param>
        ''' <param name="propval">The new value to assign.</param>
        ''' <param name="su">The unit system of the supplied value; defaults to SI if not provided.</param>
        ''' <returns><c>True</c> if the property was set successfully.</returns>
        Public Overrides Function SetPropertyValue(ByVal prop As String, ByVal propval As Object, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As Boolean

            If MyBase.SetPropertyValue(prop, propval, su) Then Return True

            Select Case prop
                Case "IsOn"
                    IsOn = Convert.ToBoolean(propval)
                Case "OnValue"
                    OnValue = ToStoredUnits(Convert.ToDouble(propval), su)
                Case "OffValue"
                    OffValue = ToStoredUnits(Convert.ToDouble(propval), su)
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
                Case "OnValue", "OffValue", "AppliedValue"
                    Return GetSwitchedPropertyUnits(su)
                Case "IsOn", "SelectedObjectTag", "SelectedProperty", "SelectedPropertyUnits"
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
        ''' Returns the raw bytes of the icon image for this switch.
        ''' </summary>
        ''' <returns>A byte array containing the PNG image data for the icon.</returns>
        Public Overrides Function GetIconBitmapBytes() As Byte()

            Return GetBytesFromResource("DWSIM.UnitOperations.switch_on.png")

        End Function

        ''' <summary>
        ''' Returns the localized description string for this object type.
        ''' </summary>
        ''' <returns>A translated description string identifying this object type.</returns>
        Public Overrides Function GetDisplayDescription() As String
            Return ResMan.GetLocalString("SW_Desc")
        End Function

        ''' <summary>
        ''' Returns the localized display name for this object type.
        ''' </summary>
        ''' <returns>A translated name string for this object type.</returns>
        Public Overrides Function GetDisplayName() As String
            Return ResMan.GetLocalString("SW_Name")
        End Function

        ''' <summary>
        ''' Gets a value indicating whether this switch is compatible with mobile interfaces. Always <c>False</c>.
        ''' </summary>
        Public Overrides ReadOnly Property MobileCompatible As Boolean
            Get
                Return False
            End Get
        End Property

    End Class

End Namespace


