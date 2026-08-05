Namespace Global.DWSIM.SharedClasses.DataRegression.Models

    <System.Serializable()> Public Class RegressionCase
        Public comp1, comp2, comp3 As String
        Public filename As String = ""
        Public databasepath As String = ""
        Public model As String = "Peng-Robinson"
        Public datatype As DataType = DataType.Pxy
        Public tp, x1p, x2p, yp, pp, calct, calcp, calcy, calcx1l1, calcx1l2, checkp, ts, tl, calcts, calctl As New ArrayList
        Public method As String = "IPOPT"
        Public objfunction As String = "Least Squares (min T/P)"
        Public includesd As Boolean = False
        Public results As String = ""
        Public advsettings As Object = Nothing
        Public tunit As String = "C"
        Public punit As String = "bar"
        Public cunit As String = ""
        Public tolerance As Double = 0.00001
        Public maxits As Double = 250
        Public iepar1 As Double = 0.0#
        Public iepar2 As Double = 0.0#
        Public iepar3 As Double = 0.0#
        Public llim1 As Double = 0.0#
        Public llim2 As Double = 0.0#
        Public llim3 As Double = 0.0#
        Public ulim1 As Double = 0.0#
        Public ulim2 As Double = 0.0#
        Public ulim3 As Double = 0.0#
        Public fixed1 As Boolean = False
        Public fixed2 As Boolean = False
        Public fixed3 As Boolean = False
        Public title As String = ""
        Public description As String = ""
        Public idealvapormodel As Boolean = True
        Public useTLdata As Boolean = True
        Public useTSdata As Boolean = True
    End Class

    Public Enum DataType
        Txy = 0
        Pxy = 1
        TPxy = 2
        Txx = 3
        Pxx = 4
        TPxx = 5
        TTxSE = 6
        TTxSS = 7
    End Enum

End Namespace
