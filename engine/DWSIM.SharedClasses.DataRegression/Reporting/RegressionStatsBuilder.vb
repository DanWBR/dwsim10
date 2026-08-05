Imports DWSIM.SharedClasses.DataRegression.Models

Namespace Global.DWSIM.SharedClasses.DataRegression.Reporting

    ''' <summary>
    ''' Stable identifiers for the residuals/stats grid columns. The
    ''' StatsColumns.VisibleFor(DataType) function returns the subset that
    ''' should be rendered for a given experiment type - same column-visibility
    ''' map used by the legacy WinForms gridstats.
    ''' </summary>
    Public Enum StatsColumn
        X1L1Exp
        X1L1Calc
        X1L2Exp
        X1L2Calc
        Y1Exp
        Y1Calc
        TExp
        TCalc
        PExp
        PCalc
        DeltaY
        DeltaYRel
        DeltaYPct
        DeltaP
        DeltaPRel
        DeltaPPct
        DeltaT
        DeltaTRel
        DeltaTPct
        DeltaX1L1
        DeltaX1L1Rel
        DeltaX1L1Pct
        DeltaX1L2
        DeltaX1L2Rel
        DeltaX1L2Pct
        TLErrPct
        TSErrPct
    End Enum

    Public Module StatsColumns

        ''' <summary>
        ''' Returns the columns to display for a given DataType, mirroring the
        ''' legacy WinForms gridstats.Columns(N).Visible pattern (Pxx/Txx/TPxx
        ''' show LLE residuals; Pxy shows pressure residuals; Txy shows
        ''' temperature residuals; TTxSE/TTxSS show solidus/liquidus % errors).
        ''' </summary>
        Public Function VisibleFor(dt As DataType) As IReadOnlyList(Of StatsColumn)
            Select Case dt
                Case DataType.Txx, DataType.Pxx, DataType.TPxx
                    Return {
                        StatsColumn.X1L1Exp, StatsColumn.X1L1Calc,
                        StatsColumn.X1L2Exp, StatsColumn.X1L2Calc,
                        StatsColumn.DeltaX1L1, StatsColumn.DeltaX1L1Rel, StatsColumn.DeltaX1L1Pct,
                        StatsColumn.DeltaX1L2, StatsColumn.DeltaX1L2Rel, StatsColumn.DeltaX1L2Pct
                    }
                Case DataType.Pxy
                    Return {
                        StatsColumn.X1L1Exp,
                        StatsColumn.Y1Exp, StatsColumn.Y1Calc, StatsColumn.TExp,
                        StatsColumn.PExp, StatsColumn.PCalc,
                        StatsColumn.DeltaY, StatsColumn.DeltaYRel, StatsColumn.DeltaYPct,
                        StatsColumn.DeltaP, StatsColumn.DeltaPRel, StatsColumn.DeltaPPct
                    }
                Case DataType.Txy
                    Return {
                        StatsColumn.X1L1Exp,
                        StatsColumn.Y1Exp, StatsColumn.Y1Calc, StatsColumn.TExp, StatsColumn.TCalc,
                        StatsColumn.PExp,
                        StatsColumn.DeltaY, StatsColumn.DeltaYRel, StatsColumn.DeltaYPct,
                        StatsColumn.DeltaT, StatsColumn.DeltaTRel, StatsColumn.DeltaTPct
                    }
                Case DataType.TTxSE, DataType.TTxSS
                    Return {StatsColumn.X1L1Exp, StatsColumn.PExp, StatsColumn.TLErrPct, StatsColumn.TSErrPct}
                Case Else
                    Return {StatsColumn.X1L1Exp, StatsColumn.PExp, StatsColumn.TExp}
            End Select
        End Function

        Public Function HeaderFor(col As StatsColumn) As String
            Select Case col
                Case StatsColumn.X1L1Exp : Return "x1' exp"
                Case StatsColumn.X1L1Calc : Return "x1' calc"
                Case StatsColumn.X1L2Exp : Return "x1'' exp"
                Case StatsColumn.X1L2Calc : Return "x1'' calc"
                Case StatsColumn.Y1Exp : Return "y1 exp"
                Case StatsColumn.Y1Calc : Return "y1 calc"
                Case StatsColumn.TExp : Return "T exp"
                Case StatsColumn.TCalc : Return "T calc"
                Case StatsColumn.PExp : Return "P exp"
                Case StatsColumn.PCalc : Return "P calc"
                Case StatsColumn.DeltaY : Return "Δy"
                Case StatsColumn.DeltaYRel : Return "Δy/y"
                Case StatsColumn.DeltaYPct : Return "Δy %"
                Case StatsColumn.DeltaP : Return "ΔP"
                Case StatsColumn.DeltaPRel : Return "ΔP/P"
                Case StatsColumn.DeltaPPct : Return "ΔP %"
                Case StatsColumn.DeltaT : Return "ΔT"
                Case StatsColumn.DeltaTRel : Return "ΔT/T"
                Case StatsColumn.DeltaTPct : Return "ΔT %"
                Case StatsColumn.DeltaX1L1 : Return "Δx1'"
                Case StatsColumn.DeltaX1L1Rel : Return "Δx1'/x1'"
                Case StatsColumn.DeltaX1L1Pct : Return "Δx1' %"
                Case StatsColumn.DeltaX1L2 : Return "Δx1''"
                Case StatsColumn.DeltaX1L2Rel : Return "Δx1''/x1''"
                Case StatsColumn.DeltaX1L2Pct : Return "Δx1'' %"
                Case StatsColumn.TLErrPct : Return "TL %err"
                Case StatsColumn.TSErrPct : Return "TS %err"
                Case Else : Return col.ToString()
            End Select
        End Function

        Public Function ValueOf(row As RegressionStatsRow, col As StatsColumn) As Double
            Select Case col
                Case StatsColumn.X1L1Exp : Return row.X1L1Exp
                Case StatsColumn.X1L1Calc : Return row.X1L1Calc
                Case StatsColumn.X1L2Exp : Return row.X1L2Exp
                Case StatsColumn.X1L2Calc : Return row.X1L2Calc
                Case StatsColumn.Y1Exp : Return row.Y1Exp
                Case StatsColumn.Y1Calc : Return row.Y1Calc
                Case StatsColumn.TExp : Return row.TExp
                Case StatsColumn.TCalc : Return row.TCalc
                Case StatsColumn.PExp : Return row.PExp
                Case StatsColumn.PCalc : Return row.PCalc
                Case StatsColumn.DeltaY : Return row.DeltaY
                Case StatsColumn.DeltaYRel : Return row.DeltaYRel
                Case StatsColumn.DeltaYPct : Return row.DeltaYPct
                Case StatsColumn.DeltaP : Return row.DeltaP
                Case StatsColumn.DeltaPRel : Return row.DeltaPRel
                Case StatsColumn.DeltaPPct : Return row.DeltaPPct
                Case StatsColumn.DeltaT : Return row.DeltaT
                Case StatsColumn.DeltaTRel : Return row.DeltaTRel
                Case StatsColumn.DeltaTPct : Return row.DeltaTPct
                Case StatsColumn.DeltaX1L1 : Return row.DeltaX1L1
                Case StatsColumn.DeltaX1L1Rel : Return row.DeltaX1L1Rel
                Case StatsColumn.DeltaX1L1Pct : Return row.DeltaX1L1Pct
                Case StatsColumn.DeltaX1L2 : Return row.DeltaX1L2
                Case StatsColumn.DeltaX1L2Rel : Return row.DeltaX1L2Rel
                Case StatsColumn.DeltaX1L2Pct : Return row.DeltaX1L2Pct
                Case StatsColumn.TLErrPct : Return row.TLErrPct
                Case StatsColumn.TSErrPct : Return row.TSErrPct
                Case Else : Return Double.NaN
            End Select
        End Function

    End Module

    Public Module RegressionStatsBuilder

        ''' <summary>
        ''' Builds one stats row per active experimental point, populating the
        ''' subset of fields meaningful for the case's DataType. T and P
        ''' calculated values are converted to the case's display units
        ''' (case.tunit / case.punit).
        ''' </summary>
        Public Function Build(c As RegressionCase) As List(Of RegressionStatsRow)
            Dim rows As New List(Of RegressionStatsRow)
            If c Is Nothing Then Return rows

            Dim i As Integer = 0
            Dim j As Integer = 0
            For Each b As Boolean In c.checkp
                If b Then
                    Try
                        rows.Add(BuildOne(c, i, j))
                    Catch
                        ' Match legacy "swallow per-point parsing/lookup failures" behavior.
                    End Try
                    j += 1
                End If
                i += 1
            Next
            Return rows
        End Function

        Private Function BuildOne(c As RegressionCase, i As Integer, j As Integer) As RegressionStatsRow
            Dim r As New RegressionStatsRow
            r.X1L1Exp = SafeAt(c.x1p, i)
            r.X1L1Calc = SafeAt(c.calcx1l1, j)
            r.X1L2Exp = SafeAt(c.x2p, i)
            r.X1L2Calc = SafeAt(c.calcx1l2, j)
            r.Y1Exp = SafeAt(c.yp, i)
            r.Y1Calc = SafeAt(c.calcy, j)
            r.TExp = SafeAt(c.tp, i)
            r.TCalc = SystemsOfUnits.Converter.ConvertFromSI(c.tunit, SafeAt(c.calct, j))
            r.PExp = SafeAt(c.pp, i)
            r.PCalc = SystemsOfUnits.Converter.ConvertFromSI(c.punit, SafeAt(c.calcp, j))

            ' Composition (y) residuals
            Dim yExp = r.Y1Exp
            r.DeltaY = r.Y1Calc - yExp
            r.DeltaYRel = SafeRatio(r.DeltaY, yExp)
            r.DeltaYPct = r.DeltaYRel * 100.0

            ' Pressure residuals (in display units)
            Dim pExp = r.PExp
            r.DeltaP = r.PCalc - pExp
            r.DeltaPRel = SafeRatio(r.DeltaP, pExp)
            r.DeltaPPct = r.DeltaPRel * 100.0

            ' Temperature residuals (in display units)
            Dim tExp = r.TExp
            r.DeltaT = r.TCalc - tExp
            r.DeltaTRel = SafeRatio(r.DeltaT, tExp)
            r.DeltaTPct = r.DeltaTRel * 100.0

            ' LLE composition residuals
            r.DeltaX1L1 = r.X1L1Calc - r.X1L1Exp
            r.DeltaX1L1Rel = SafeRatio(r.DeltaX1L1, r.X1L1Exp)
            r.DeltaX1L1Pct = r.DeltaX1L1Rel * 100.0

            r.DeltaX1L2 = r.X1L2Calc - r.X1L2Exp
            r.DeltaX1L2Rel = SafeRatio(r.DeltaX1L2, r.X1L2Exp)
            r.DeltaX1L2Pct = r.DeltaX1L2Rel * 100.0

            ' SLE % errors (TL/TS calc are already in K from the engine; tl/ts experimental
            ' are in display units, so compute the % error in matched units by converting
            ' the calculated values back to the user's tunit before differencing).
            Dim tlExp = SafeAt(c.tl, i)
            Dim tsExp = SafeAt(c.ts, i)
            Dim tlCalc = SystemsOfUnits.Converter.ConvertFromSI(c.tunit, SafeAt(c.calctl, j))
            Dim tsCalc = SystemsOfUnits.Converter.ConvertFromSI(c.tunit, SafeAt(c.calcts, j))
            r.TLErrPct = SafeRatio(tlCalc - tlExp, tlExp) * 100.0
            r.TSErrPct = SafeRatio(tsCalc - tsExp, tsExp) * 100.0

            Return r
        End Function

        Private Function SafeAt(list As ArrayList, i As Integer) As Double
            If list Is Nothing OrElse i >= list.Count OrElse list(i) Is Nothing Then Return 0.0
            Return Convert.ToDouble(list(i))
        End Function

        Private Function SafeRatio(num As Double, denom As Double) As Double
            If denom = 0.0 OrElse Double.IsNaN(num) OrElse Double.IsNaN(denom) Then Return Double.NaN
            Return num / denom
        End Function

    End Module

End Namespace
