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

    <System.Serializable()> Public Partial Class Switch

        Inherits UnitOperations.UnitOpBaseClass

        Implements Interfaces.ISwitch

        Public Overrides Property ObjectClass As SimulationObjectClass = SimulationObjectClass.Switches

        <NonSerialized> <Xml.Serialization.XmlIgnore> Public f As Object

        Public Property SelectedObjectID As String = "" Implements ISwitch.SelectedObjectID

        Public Property SelectedProperty As String = "" Implements ISwitch.SelectedProperty

        Public Property SelectedPropertyType As UnitOfMeasure = UnitOfMeasure.none Implements ISwitch.SelectedPropertyType

        Public Property SelectedPropertyUnits As String = "" Implements ISwitch.SelectedPropertyUnits

        Public Property OffValue As Double = 0.0 Implements ISwitch.OffValue

        Public Property OnValue As Double = 0.0 Implements ISwitch.OnValue

        Public Property IsOn As Boolean = False Implements ISwitch.IsOn

        Public Overrides ReadOnly Property SupportsDynamicMode As Boolean = True

        Public Sub New(ByVal name As String, ByVal description As String)

            MyBase.CreateNew()
            Me.ComponentName = name
            Me.ComponentDescription = description

        End Sub

        Public Overrides Function CloneXML() As Object
            Dim obj As ICustomXMLSerialization = New Switch()
            obj.LoadData(Me.SaveData)
            Return obj
        End Function

        Public Overrides Function CloneJSON() As Object
            Return Newtonsoft.Json.JsonConvert.DeserializeObject(Of Switch)(Newtonsoft.Json.JsonConvert.SerializeObject(Me))
        End Function

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

        Public Overrides Function GetPropertyValue(ByVal prop As String, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As Object

            Return ""

        End Function

        Public Overloads Overrides Function GetProperties(ByVal proptype As Interfaces.Enums.PropertyType) As String()

            Return New String() {}

        End Function

        Public Overrides Function SetPropertyValue(ByVal prop As String, ByVal propval As Object, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As Boolean

            Return True

        End Function

        Public Overrides Function GetPropertyUnit(ByVal prop As String, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As String

            Return ""

        End Function

        Public Overrides Function GetIconBitmapBytes() As Byte()

            Return GetBytesFromResource("DWSIM.UnitOperations.switch_on.png")

        End Function

        Public Overrides Function GetDisplayDescription() As String
            Return ResMan.GetLocalString("SW_Desc")
        End Function

        Public Overrides Function GetDisplayName() As String
            Return ResMan.GetLocalString("SW_Name")
        End Function

        Public Overrides ReadOnly Property MobileCompatible As Boolean
            Get
                Return False
            End Get
        End Property

    End Class

End Namespace


