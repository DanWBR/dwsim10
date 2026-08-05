Imports System.IO
Imports DWSIM.UnitOperations.UnitOperations

Namespace UnitOperations

    ''' <summary>
    ''' Represents a PEM Fuel Cell modelled with the Chamberline–Kim polarisation curve.
    ''' Uses the OPEM Python library for the static fuel-cell calculation.
    ''' </summary>
    Public Class PEMFC_ChamberLineKim

        Inherits PEMFuelCellUnitOpBase

        ''' <summary>Gets or sets the default name prefix for this unit operation.</summary>
        Public Overrides Property Prefix As String = "FCA-"

        ''' <summary>Returns the display name for this unit operation.</summary>
        Public Overrides Function GetDisplayName() As String
            Return "PEM Fuel Cell (Chamberline-Kim)"
        End Function

        ''' <summary>Returns the display description for this unit operation.</summary>
        Public Overrides Function GetDisplayDescription() As String
            Return "PEM Fuel Cell (OPEM Chamberline-Kim Static Model)"
        End Function

        ''' <summary>Initializes a new default instance.</summary>
        Public Sub New()

            MyBase.New()

        End Sub

        ''' <summary>Creates and returns a new instance for deserialization.</summary>
        Public Overrides Function ReturnInstance(typename As String) As Object

            Return New PEMFC_ChamberLineKim

        End Function

        Public Overrides Function GetIconBitmapBytes() As Byte()

            Return GetBytesFromResource("DWSIM.UnitOperations.fuel_cell.png")

        End Function

        ''' <summary>Creates a deep copy via XML serialization.</summary>
        Public Overrides Function CloneXML() As Object

            Dim obj As ICustomXMLSerialization = New PEMFC_ChamberLineKim()
            obj.LoadData(Me.SaveData)
            Return obj

        End Function

        ''' <summary>Creates a deep copy via JSON serialization (not implemented).</summary>
        Public Overrides Function CloneJSON() As Object

            Throw New NotImplementedException()

        End Function

    End Class

End Namespace
