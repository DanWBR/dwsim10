'    CrossflowUF - Trajectory container for dynamic concentration / diafiltration runs.
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
    ''' Time-series capture for CrossflowUF dynamic modes. Flux, retentate volume,
    ''' VCF (instantaneous), diavolumes swept, and per-compound retentate concentrations (g/L).
    ''' </summary>
    <Serializable()> Public Class CrossflowUFTrajectoryResult

        Public Property Mode As String = "ConcentrationDynamic"
        Public Property Times As New List(Of Double)()          ' seconds
        Public Property J As New List(Of Double)()              ' kg/m2/s
        Public Property V_ret As New List(Of Double)()          ' m3
        Public Property VCF_instant As New List(Of Double)()
        Public Property Diavolumes As New List(Of Double)()
        Public Property Concentrations As New Dictionary(Of String, List(Of Double))()  ' per compound, g/L

        Public Function AvailableSeries() As String()
            Dim res As New List(Of String)() From {"J", "V_ret", "VCF_instant", "Diavolumes"}
            For Each k In Concentrations.Keys
                res.Add("C_" & k)
            Next
            Return res.ToArray()
        End Function

        Public Function GetSeries(name As String) As Double()
            Select Case name
                Case "J" : Return J.ToArray()
                Case "V_ret" : Return V_ret.ToArray()
                Case "VCF_instant" : Return VCF_instant.ToArray()
                Case "Diavolumes" : Return Diavolumes.ToArray()
                Case Else
                    If name.StartsWith("C_") Then
                        Dim k = name.Substring(2)
                        If Concentrations.ContainsKey(k) Then Return Concentrations(k).ToArray()
                    End If
                    Return New Double() {}
            End Select
        End Function

        Public Function GetTimes() As Double()
            Return Times.ToArray()
        End Function

        Public Function ToCSV() As String
            Dim sb As New StringBuilder()
            Dim keys = AvailableSeries()
            sb.Append("time_s")
            For Each k In keys : sb.Append(",") : sb.Append(k) : Next
            sb.AppendLine()
            Dim ci = CultureInfo.InvariantCulture
            For i = 0 To Times.Count - 1
                sb.Append(Times(i).ToString("G10", ci))
                For Each k In keys
                    Dim arr = GetSeries(k)
                    Dim v = If(i < arr.Length, arr(i), Double.NaN)
                    sb.Append(",") : sb.Append(v.ToString("G10", ci))
                Next
                sb.AppendLine()
            Next
            Return sb.ToString()
        End Function

        Public Function ToDataTable() As DataTable
            Dim dt As New DataTable("CrossflowUFTrajectory")
            dt.Columns.Add("time_s", GetType(Double))
            Dim keys = AvailableSeries()
            For Each k In keys : dt.Columns.Add(k, GetType(Double)) : Next
            For i = 0 To Times.Count - 1
                Dim row = dt.NewRow()
                row("time_s") = Times(i)
                For Each k In keys
                    Dim arr = GetSeries(k)
                    If i < arr.Length Then row(k) = arr(i) Else row(k) = DBNull.Value
                Next
                dt.Rows.Add(row)
            Next
            Return dt
        End Function

    End Class

End Namespace
