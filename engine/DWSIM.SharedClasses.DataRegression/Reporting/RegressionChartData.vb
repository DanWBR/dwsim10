Imports DWSIM.SharedClasses.DataRegression.Models

Namespace Global.DWSIM.SharedClasses.DataRegression.Reporting

    ''' <summary>
    ''' Curve style codes accepted by IRegressionChartRenderer. Same encoding
    ''' that legacy ycurvetypes used.
    ''' </summary>
    Public Enum CurveStyle As Integer
        PointsOnly = 1
        PointsAndLine = 2
        LineOnly = 3
        DashedLine = 4
        DashedLineWithPoints = 5
        LineNoSymbol = 6
    End Enum

    ''' <summary>
    ''' UI-agnostic chart payload assembled from a RegressionCase. Renderers
    ''' (ZedGraph/OxyPlot/Eto) consume this to produce platform-specific output.
    ''' </summary>
    Public Class RegressionChartData
        Public Property DataType As DataType
        Public Property Title As String
        Public Property XAxisTitle As String
        Public Property YAxisTitle As String
        Public Property SecondaryYAxisTitle As String

        ' x-axis arrays, one per curve series (px → 1st curve, px2 → 2nd, etc.).
        ' For datatypes that share an x-axis across curves, the same px is reused.
        Public Property Px As New ArrayList
        Public Property Px2 As New ArrayList
        Public Property Px3 As New ArrayList
        Public Property Px4 As New ArrayList

        ' y-axis arrays, one per curve series.
        Public Property Py1 As New ArrayList
        Public Property Py2 As New ArrayList
        Public Property Py3 As New ArrayList
        Public Property Py4 As New ArrayList
        Public Property Py5 As New ArrayList

        Public Property Y1Title As String
        Public Property Y2Title As String
        Public Property Y3Title As String
        Public Property Y4Title As String
        Public Property Y5Title As String
        Public Property Y6Title As String

        ''' <summary>One CurveStyle per series (length matches active series count).</summary>
        Public Property CurveStyles As New List(Of CurveStyle)
    End Class

    ''' <summary>
    ''' Renderer abstraction. WinForms form supplies a ZedGraph adapter; the
    ''' future Eto port supplies an OxyPlot adapter.
    ''' </summary>
    Public Interface IRegressionChartRenderer
        Sub Render(data As RegressionChartData)
    End Interface

End Namespace
