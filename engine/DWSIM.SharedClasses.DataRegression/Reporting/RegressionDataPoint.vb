Namespace Global.DWSIM.SharedClasses.DataRegression.Reporting

    ''' <summary>
    ''' Normalized experimental data point produced by external-database loaders
    ''' (KDB, ThermoML/PhaseEq). Values are in the user's selected display units -
    ''' callers supply tUnit / pUnit when invoking the loaders.
    ''' </summary>
    Public Class RegressionDataPoint
        Public Property Use As Boolean = True
        Public Property T As Double
        Public Property P As Double
        Public Property X1 As Double
        Public Property X2 As Double
        Public Property Y1 As Double
        Public Property TL As Double
        Public Property TS As Double
    End Class

End Namespace
