Imports DWSIM.Interfaces.Enums

Public Class Dimension

    Implements IDimension, ICustomXMLSerialization

    Public Property ID As String = "" Implements IDimension.ID

    Public Property Name As DimensionName = DimensionName.NotDefined Implements IDimension.Name

    Public Property Value As Double Implements IDimension.Value

    Public Property IsUserDefined As Boolean = False Implements IDimension.IsUserDefined

    Public Property UserDefinedValue As Double Implements IDimension.UserDefinedValue

    Public Sub New()

        ID = Guid.NewGuid().ToString()

    End Sub

    Public Function SaveData() As List(Of XElement) Implements ICustomXMLSerialization.SaveData
        Return XMLSerializer.XMLSerializer.Serialize(Me)
    End Function

    Public Function LoadData(data As List(Of XElement)) As Boolean Implements ICustomXMLSerialization.LoadData
        Return XMLSerializer.XMLSerializer.Deserialize(Me, data)
    End Function

    Public Function GetDisplayName() As String Implements IDimension.GetDisplayName

        Select Case Name
            Case DimensionName.Area
                Return "Area"
            Case DimensionName.Diameter
                Return "Diameter"
            Case DimensionName.Flow
                Return "Flow"
            Case DimensionName.Efficiency
                Return "Efficiency"
            Case DimensionName.Head
                Return "Head"
            Case DimensionName.HeatDuty
                Return "Heat Duty"
            Case DimensionName.Height
                Return "Height"
            Case DimensionName.Length
                Return "Length"
            Case DimensionName.NotDefined
                Return "Not Defined"
            Case DimensionName.NumberOfCells
                Return "Number of Cells"
            Case DimensionName.NumberofPackings
                Return "Number of Packings"
            Case DimensionName.NumberOfSections
                Return "Number of Sections"
            Case DimensionName.NumberOfTrays
                Return "Number of Trays"
            Case DimensionName.NumberOfTubes
                Return "Number of Tubes"
            Case DimensionName.Power
                Return "Power"
            Case DimensionName.Pressure
                Return "Pressure"
            Case DimensionName.PressureDifference
                Return "Pressure Difference"
            Case DimensionName.Volume
                Return "Volume"
        End Select

    End Function

    Public Function GetUnitsType() As UnitOfMeasure Implements IDimension.GetUnitsType

        Select Case Name
            Case DimensionName.Area
                Return UnitOfMeasure.area
            Case DimensionName.Diameter
                Return UnitOfMeasure.diameter
            Case DimensionName.Efficiency
                Return UnitOfMeasure.none
            Case DimensionName.Flow
                Return UnitOfMeasure.volumetricFlow
            Case DimensionName.Head
                Return UnitOfMeasure.distance
            Case DimensionName.HeatDuty
                Return UnitOfMeasure.heatflow
            Case DimensionName.Height
                Return UnitOfMeasure.distance
            Case DimensionName.Length
                Return UnitOfMeasure.distance
            Case DimensionName.NotDefined
                Return UnitOfMeasure.none
            Case DimensionName.NumberOfCells
                Return UnitOfMeasure.none
            Case DimensionName.NumberofPackings
                Return UnitOfMeasure.none
            Case DimensionName.NumberOfSections
                Return UnitOfMeasure.none
            Case DimensionName.NumberOfTrays
                Return UnitOfMeasure.none
            Case DimensionName.NumberOfTubes
                Return UnitOfMeasure.none
            Case DimensionName.Power
                Return UnitOfMeasure.heatflow
            Case DimensionName.Pressure
                Return UnitOfMeasure.pressure
            Case DimensionName.PressureDifference
                Return UnitOfMeasure.deltaP
            Case DimensionName.Volume
                Return UnitOfMeasure.volume
        End Select


    End Function

End Class
