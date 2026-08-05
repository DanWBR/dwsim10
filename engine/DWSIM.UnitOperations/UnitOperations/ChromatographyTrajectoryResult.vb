'    Chromatography - Trajectory container for Thomas breakthrough curve.
'    Copyright 2026 Daniel Wagner O. de Medeiros
'
'    This file is part of DWSIM.

Imports System
Imports System.Collections.Generic
Imports System.Data
Imports System.Globalization
Imports System.Text

Namespace UnitOperations

    ''' <summary>
    ''' Time series of a Thomas-model breakthrough curve for a loading step.
    ''' </summary>
    <Serializable()> Public Class ChromatographyTrajectoryResult

        Public Property Mode As String = "BindElute_Dynamic"
        Public Property Times As New List(Of Double)()          ' seconds
        Public Property BedVolumes As New List(Of Double)()     ' Q·t / V_col
        Public Property C_over_C0 As New List(Of Double)()      ' breakthrough fraction
        Public Property QLoaded As New List(Of Double)()        ' g/L resin, cumulative
        Public Property Breakthrough As New List(Of Double)()   ' 1 - C/C0

        Public Function AvailableSeries() As String()
            Return New String() {"BedVolumes", "C_over_C0", "QLoaded", "Breakthrough"}
        End Function

        Public Function GetSeries(name As String) As Double()
            Select Case name
                Case "BedVolumes" : Return BedVolumes.ToArray()
                Case "C_over_C0" : Return C_over_C0.ToArray()
                Case "QLoaded" : Return QLoaded.ToArray()
                Case "Breakthrough" : Return Breakthrough.ToArray()
                Case Else : Return New Double() {}
            End Select
        End Function

        Public Function GetTimes() As Double()
            Return Times.ToArray()
        End Function

        Public Function ToCSV() As String
            Dim sb As New StringBuilder()
            sb.AppendLine("time_s,BedVolumes,C_over_C0,QLoaded,Breakthrough")
            Dim ci = CultureInfo.InvariantCulture
            For i = 0 To Times.Count - 1
                sb.Append(Times(i).ToString("G10", ci)) : sb.Append(",")
                sb.Append(BedVolumes(i).ToString("G10", ci)) : sb.Append(",")
                sb.Append(C_over_C0(i).ToString("G10", ci)) : sb.Append(",")
                sb.Append(QLoaded(i).ToString("G10", ci)) : sb.Append(",")
                sb.Append(Breakthrough(i).ToString("G10", ci)) : sb.AppendLine()
            Next
            Return sb.ToString()
        End Function

        Public Function ToDataTable() As DataTable
            Dim dt As New DataTable("ChromatographyTrajectory")
            dt.Columns.Add("time_s", GetType(Double))
            dt.Columns.Add("BedVolumes", GetType(Double))
            dt.Columns.Add("C_over_C0", GetType(Double))
            dt.Columns.Add("QLoaded", GetType(Double))
            dt.Columns.Add("Breakthrough", GetType(Double))
            For i = 0 To Times.Count - 1
                dt.Rows.Add(Times(i), BedVolumes(i), C_over_C0(i), QLoaded(i), Breakthrough(i))
            Next
            Return dt
        End Function

    End Class

End Namespace
