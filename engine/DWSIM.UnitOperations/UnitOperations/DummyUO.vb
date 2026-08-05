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

Imports DWSIM.Interfaces.Enums

Namespace UnitOperations

    ''' <summary>
    ''' Represents a placeholder (dummy) unit operation that performs no calculation.
    ''' It can be used as a template for custom unit operations or as a no-op block in a flowsheet.
    ''' </summary>
    <System.Serializable()> Public Partial Class DummyUnitOperation

        Inherits UnitOperations.UnitOpBaseClass

        ''' <summary>Gets or sets the simulation object class category (Other).</summary>
        Public Overrides Property ObjectClass As SimulationObjectClass = SimulationObjectClass.Other

        ''' <summary>Gets a value indicating that this unit operation does not support dynamic simulation mode.</summary>
        Public Overrides ReadOnly Property SupportsDynamicMode As Boolean = False

        ''' <summary>Gets a value indicating that this unit operation has no dedicated dynamic mode properties.</summary>
        Public Overrides ReadOnly Property HasPropertiesForDynamicMode As Boolean = False

        ''' <summary>
        ''' Initializes a new instance of the <see cref="DummyUnitOperation"/> class with a name and description.
        ''' </summary>
        ''' <param name="name">The display name of the dummy unit operation.</param>
        ''' <param name="description">A brief description of the dummy unit operation.</param>
        Public Sub New(ByVal name As String, ByVal description As String)

            MyBase.CreateNew()
            Me.ComponentName = name
            Me.ComponentDescription = description

        End Sub

        ''' <summary>Creates a deep copy of this dummy unit operation via XML serialization.</summary>
        ''' <returns>A new <see cref="DummyUnitOperation"/> instance with the same state.</returns>
        Public Overrides Function CloneXML() As Object
            Dim obj As ICustomXMLSerialization = New DummyUnitOperation()
            obj.LoadData(Me.SaveData)
            Return obj
        End Function

        ''' <summary>Creates a deep copy of this dummy unit operation via JSON serialization.</summary>
        ''' <returns>A new <see cref="DummyUnitOperation"/> instance with the same state.</returns>
        Public Overrides Function CloneJSON() As Object
            Return Newtonsoft.Json.JsonConvert.DeserializeObject(Of DummyUnitOperation)(Newtonsoft.Json.JsonConvert.SerializeObject(Me))
        End Function

        ''' <summary>Initializes a new default instance of the <see cref="DummyUnitOperation"/> class.</summary>
        Public Sub New()
            MyBase.New()
        End Sub

        ''' <summary>Executes the dynamic model step (no-op for the dummy unit operation).</summary>
        Public Overrides Sub RunDynamicModel()


        End Sub

        ''' <summary>Performs the steady-state calculation (no-op for the dummy unit operation).</summary>
        ''' <param name="args">Optional calculation arguments (not used).</param>
        Public Overrides Sub Calculate(Optional ByVal args As Object = Nothing)



        End Sub

        ''' <summary>Clears calculation results (no-op for the dummy unit operation).</summary>
        Public Overrides Sub DeCalculate()

        End Sub

        ''' <summary>Returns the icon bitmap as a byte array.</summary>
        ''' <returns>Byte array containing the PNG image data.</returns>
        Public Overrides Function GetIconBitmapBytes() As Byte()

            Return GetBytesFromResource("DWSIM.UnitOperations.square_240px.png")

        End Function

        ''' <summary>Returns the display description for this unit operation (empty string).</summary>
        ''' <returns>An empty string.</returns>
        Public Overrides Function GetDisplayDescription() As String
            Return ""
        End Function

        ''' <summary>Returns the display name for this unit operation.</summary>
        ''' <returns>The string "Dummy Unit Operation".</returns>
        Public Overrides Function GetDisplayName() As String
            Return "Dummy Unit Operation"
        End Function

        ''' <summary>Gets a value indicating whether this unit operation is compatible with mobile/cross-platform interfaces.</summary>
        Public Overrides ReadOnly Property MobileCompatible As Boolean
            Get
                Return False
            End Get
        End Property

    End Class

End Namespace


