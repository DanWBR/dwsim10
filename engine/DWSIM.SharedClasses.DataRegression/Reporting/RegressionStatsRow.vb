Namespace Global.DWSIM.SharedClasses.DataRegression.Reporting

    ''' <summary>
    ''' Per-experimental-point residuals row built by RegressionStatsBuilder
    ''' for the "results" / "fit quality" grid. Cells that are not meaningful
    ''' for the current DataType are left at Double.NaN; the UI hides those
    ''' columns via StatsColumns.VisibleFor(DataType).
    ''' </summary>
    Public Class RegressionStatsRow
        ' Composition (LLE)
        Public Property X1L1Exp As Double = Double.NaN
        Public Property X1L1Calc As Double = Double.NaN
        Public Property X1L2Exp As Double = Double.NaN
        Public Property X1L2Calc As Double = Double.NaN

        ' Composition (VLE)
        Public Property Y1Exp As Double = Double.NaN
        Public Property Y1Calc As Double = Double.NaN

        ' T / P
        Public Property TExp As Double = Double.NaN
        Public Property TCalc As Double = Double.NaN
        Public Property PExp As Double = Double.NaN
        Public Property PCalc As Double = Double.NaN

        ' Δy (composition residuals)
        Public Property DeltaY As Double = Double.NaN
        Public Property DeltaYRel As Double = Double.NaN
        Public Property DeltaYPct As Double = Double.NaN

        ' Δp
        Public Property DeltaP As Double = Double.NaN
        Public Property DeltaPRel As Double = Double.NaN
        Public Property DeltaPPct As Double = Double.NaN

        ' Δt
        Public Property DeltaT As Double = Double.NaN
        Public Property DeltaTRel As Double = Double.NaN
        Public Property DeltaTPct As Double = Double.NaN

        ' Δx1' (LLE phase 1)
        Public Property DeltaX1L1 As Double = Double.NaN
        Public Property DeltaX1L1Rel As Double = Double.NaN
        Public Property DeltaX1L1Pct As Double = Double.NaN

        ' Δx1'' (LLE phase 2)
        Public Property DeltaX1L2 As Double = Double.NaN
        Public Property DeltaX1L2Rel As Double = Double.NaN
        Public Property DeltaX1L2Pct As Double = Double.NaN

        ' SLE % errors
        Public Property TLErrPct As Double = Double.NaN
        Public Property TSErrPct As Double = Double.NaN
    End Class

End Namespace
