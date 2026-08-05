'    Full ADM1 - Trajectory container (time series of states) for charting & export.
'    Copyright 2026 Daniel Wagner O. de Medeiros
'
'    This file is part of DWSIM.

Imports System
Imports System.Collections.Generic
Imports System.Data
Imports System.Globalization
Imports System.Text

Namespace Reactors.ADM1

    ''' <summary>
    ''' Result of a full-ADM1 dynamic simulation: full time series of states plus
    ''' convenience accessors for charts and CSV/DataTable export.
    ''' </summary>
    <Serializable()> Public Class ADM1TrajectoryResult

        Public Property Parameters As ADM1Parameters
        Public Property Times As New List(Of Double)()     ' days
        Public Property States As New List(Of ADM1State)()
        Public Property FinalState As ADM1State

        ''' <summary>
        ''' True when the integration actually reached the requested horizon. Callers must check
        ''' this before reading any result: a False here means FinalState is wherever the solver
        ''' ran out of budget, which for ADM1 is typically an early transient and not a steady state.
        ''' </summary>
        Public Property Converged As Boolean = False

        ''' <summary>Simulated time actually reached (days). Equals the requested tEnd when Converged.</summary>
        Public Property ReachedTime_d As Double = 0.0

        ''' <summary>Total RK steps attempted (accepted + rejected).</summary>
        Public Property Steps As Integer = 0

        ''' <summary>
        ''' Empty when the run was clean. Otherwise says what went wrong - either the step budget
        ''' ran out short of the horizon (Converged False), or the horizon was reached but some
        ''' steps had to be forced through below the minimum step size (Converged True, accuracy
        ''' not guaranteed).
        ''' </summary>
        Public Property StopReason As String = ""

        ''' <summary>
        ''' Largest relative rate of change left at the final state (1/d): max over states of
        ''' |dy/dt| / max(|y|, 1e-6). Diagnostic, not a verdict - read it alongside Converged, which
        ''' only says the horizon was reached and says nothing about having settled there.
        ''' </summary>
        ''' <remarks>
        ''' Small against the dilution rate means the run has stopped moving. It does not fall to
        ''' zero for a washed-out population: a state decaying towards zero keeps a relative rate of
        ''' roughly (D + k_dec) however negligible it has become in absolute terms, so a digester
        ''' that has genuinely settled with a dead group still reports that group's decay rate here.
        ''' </remarks>
        Public Property SteadyStateResidual_perDay As Double = Double.NaN

        ''' <summary>Keys available via GetSeries(): all ADM1State variables + derived outputs.</summary>
        Public Shared ReadOnly DerivedKeys As String() = {
            "pH", "S_H_ion", "Q_gas", "x_CH4", "x_CO2", "x_H2", "x_H2S", "Total_VFA", "X_srb_total"
        }

        Public Function AvailableSeries() As String()
            Dim res As New List(Of String)(ADM1State.VarNames)
            res.AddRange(DerivedKeys)
            Return res.ToArray()
        End Function

        Public Function GetSeries(name As String) As Double()
            Dim n = States.Count
            Dim a(n - 1) As Double
            For i = 0 To n - 1
                a(i) = ExtractValue(States(i), name)
            Next
            Return a
        End Function

        Public Function GetTimes() As Double()
            Return Times.ToArray()
        End Function

        Private Function ExtractValue(st As ADM1State, name As String) As Double
            Select Case name
                Case "S_su" : Return st.S_su
                Case "S_aa" : Return st.S_aa
                Case "S_fa" : Return st.S_fa
                Case "S_va" : Return st.S_va
                Case "S_bu" : Return st.S_bu
                Case "S_pro" : Return st.S_pro
                Case "S_ac" : Return st.S_ac
                Case "S_h2" : Return st.S_h2
                Case "S_ch4" : Return st.S_ch4
                Case "S_IC" : Return st.S_IC
                Case "S_IN" : Return st.S_IN
                Case "S_I" : Return st.S_I
                Case "X_c" : Return st.X_c
                Case "X_ch" : Return st.X_ch
                Case "X_pr" : Return st.X_pr
                Case "X_li" : Return st.X_li
                Case "X_su" : Return st.X_su
                Case "X_aa" : Return st.X_aa
                Case "X_fa" : Return st.X_fa
                Case "X_c4" : Return st.X_c4
                Case "X_pro" : Return st.X_pro
                Case "X_ac" : Return st.X_ac
                Case "X_h2" : Return st.X_h2
                Case "X_I" : Return st.X_I
                Case "S_cat" : Return st.S_cat
                Case "S_an" : Return st.S_an
                Case "S_h2_gas" : Return st.S_h2_gas
                Case "S_ch4_gas" : Return st.S_ch4_gas
                Case "S_co2_gas" : Return st.S_co2_gas
                Case "S_IS" : Return st.S_IS
                Case "S_h2s_gas" : Return st.S_h2s_gas
                Case "S_so4" : Return st.S_so4
                Case "X_srb_h2" : Return st.X_srb_h2
                Case "X_srb_ac" : Return st.X_srb_ac
                Case "X_srb_pro" : Return st.X_srb_pro
                Case "X_srb_bu" : Return st.X_srb_bu
                Case "X_srb_total" : Return st.X_srb_h2 + st.X_srb_ac + st.X_srb_pro + st.X_srb_bu
                Case "pH" : Return st.pH
                Case "S_H_ion" : Return st.S_H_ion
                Case "Q_gas" : Return If(Parameters Is Nothing, 0.0, ADM1Equations.BiogasFlow_Nm3_d(st, Parameters))
                Case "x_CH4" : Return If(Parameters Is Nothing, 0.0, ADM1Equations.CH4MoleFraction(st, Parameters))
                Case "x_CO2" : Return If(Parameters Is Nothing, 0.0, ADM1Equations.CO2MoleFraction(st, Parameters))
                Case "x_H2" : Return If(Parameters Is Nothing, 0.0, ADM1Equations.H2MoleFraction(st, Parameters))
                Case "x_H2S" : Return If(Parameters Is Nothing, 0.0, ADM1Equations.H2SMoleFraction(st, Parameters))
                Case "Total_VFA" : Return st.S_va + st.S_bu + st.S_pro + st.S_ac
                Case Else : Return Double.NaN
            End Select
        End Function

        Public Function ToCSV() As String
            Dim sb As New StringBuilder()
            Dim keys = AvailableSeries()
            sb.Append("time_d")
            For Each k In keys : sb.Append(",") : sb.Append(k) : Next
            sb.AppendLine()
            Dim ci = CultureInfo.InvariantCulture
            For i = 0 To States.Count - 1
                sb.Append(Times(i).ToString("G10", ci))
                For Each k In keys
                    sb.Append(",")
                    sb.Append(ExtractValue(States(i), k).ToString("G10", ci))
                Next
                sb.AppendLine()
            Next
            Return sb.ToString()
        End Function

        Public Function ToDataTable() As DataTable
            Dim dt As New DataTable("ADM1Trajectory")
            dt.Columns.Add("time_d", GetType(Double))
            Dim keys = AvailableSeries()
            For Each k In keys : dt.Columns.Add(k, GetType(Double)) : Next
            For i = 0 To States.Count - 1
                Dim row = dt.NewRow()
                row("time_d") = Times(i)
                For Each k In keys
                    row(k) = ExtractValue(States(i), k)
                Next
                dt.Rows.Add(row)
            Next
            Return dt
        End Function

    End Class

End Namespace
