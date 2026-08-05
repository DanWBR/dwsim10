Imports System.Reflection
Imports System.Runtime.Serialization
Imports OxyPlot
Imports OxyPlot.Axes
Imports OxyPlot.Series
Imports DWSIM.Thermodynamics.PropertyPackages
Imports DWSIM.Thermodynamics.Streams
Imports System.Globalization
Imports DWSIM.SharedClasses.SystemsOfUnits
Imports DWSIM.ExtensionMethods
Imports System.Linq
Imports DWSIM.DrawingTools

''' <summary>
''' Contains utility classes used by shortcut thermodynamic methods for property envelopes,
''' critical point calculations, and phase diagrams.
''' </summary>
Namespace ShortcutUtilities

    Public Enum CalculationType

        CriticalPoint = 1
        PhaseEnvelopePT = 2
        PhaseEnvelopePH = 3
        PhaseEnvelopePS = 4
        PhaseEnvelopeTH = 5
        PhaseEnvelopeTS = 6
        PhaseEnvelopeVT = 7
        PhaseEnvelopeVP = 8
        BinaryEnvelopeTxy = 9
        BinaryEnvelopePxy = 10

    End Enum

    Public Class CalculationResults

        Public Property Data As Dictionary(Of String, List(Of Double))
        Public Property DataUnits As Dictionary(Of String, String)
        Public Property CompoundData As Dictionary(Of String, Object)
        Public Property TextOutput As String = ""
        Public Property PlotModels As List(Of Global.OxyPlot.Model)
        Public Property Units As Units
        Public Property ExceptionResult As Exception
        Public Property NumberFormat As String = "0.####"
        Public Property Language As String = "en"

        Sub New()
            Data = New Dictionary(Of String, List(Of Double))
            DataUnits = New Dictionary(Of String, String)
            CompoundData = New Dictionary(Of String, Object)
            PlotModels = New List(Of Global.OxyPlot.Model)
            Units = New SI
        End Sub

    End Class

    Public Class Calculation

        Public Property CalcType As CalculationType = CalculationType.CriticalPoint
        Public Property Language As String = "en"
        Public Property PhaseEnvelopeOptions As PropertyPackages.PhaseEnvelopeOptions
        Public Property BinaryEnvelopeOptions As Object()

        Public Property DisplayEnvelopeAreas As Boolean = False

        Private _MaterialStream As MaterialStream

        Sub New(ByVal stream As MaterialStream)

            _MaterialStream = stream

        End Sub

        ''' <summary>
        ''' Reads one curve out of the Object() returned by DW_ReturnPhaseEnvelope /
        ''' DW_ReturnBinaryEnvelope. The two builders do not agree on the container type:
        ''' the phase envelope collects its curves in List(Of Double), the binary envelope
        ''' in ArrayList. Accepting both keeps this reader working whichever one changes.
        ''' </summary>
        Private Shared Function ToDoubles(ByVal o As Object) As List(Of Double)

            If o Is Nothing Then Return New List(Of Double)

            Dim dlist = TryCast(o, List(Of Double))
            If dlist IsNot Nothing Then Return New List(Of Double)(dlist)

            Dim alist = TryCast(o, ArrayList)
            If alist IsNot Nothing Then Return alist.ToDoubleList()

            Dim enumerable = TryCast(o, IEnumerable)
            If enumerable IsNot Nothing Then
                Dim result As New List(Of Double)
                For Each item In enumerable
                    result.Add(Convert.ToDouble(item))
                Next
                Return result
            End If

            Return New List(Of Double)

        End Function

        Function Calculate() As CalculationResults

            Dim Compounds As String() = _MaterialStream.Phases(0).Compounds.Values.Select(Function(x) x.Name).ToArray()
            Dim pp As PropertyPackage = _MaterialStream.PropertyPackage
            pp.CurrentMaterialStream = _MaterialStream
            Dim PropertyPackageName As String = pp.ComponentName
            Dim MixName As String = ""

            If _MaterialStream.GraphicObject IsNot Nothing Then
                MixName = _MaterialStream.GraphicObject.Tag
            End If

            Dim MixTemperature As Double = _MaterialStream.Phases(0).Properties.temperature.GetValueOrDefault
            Dim MixPressure As Double = _MaterialStream.Phases(0).Properties.pressure.GetValueOrDefault
            Dim MixEnthalpy As Double = _MaterialStream.Phases(0).Properties.enthalpy.GetValueOrDefault
            Dim MixEntropy As Double = _MaterialStream.Phases(0).Properties.entropy.GetValueOrDefault
            Dim FlashAlgorithm = pp.FlashBase.AlgoType
            Dim NumberFormat As String = _MaterialStream.FlowSheet.FlowsheetOptions.NumberFormat
            Dim Units As Units = _MaterialStream.FlowSheet.FlowsheetOptions.SelectedUnitSystem

            Dim results As New CalculationResults() With {.Units = Units, .NumberFormat = NumberFormat, .Language = Language}

            Try

                Select Case CalcType

                    Case CalculationType.CriticalPoint

                        If pp.ComponentName.Equals("Peng-Robinson (PR)") Then

                            Dim res As ArrayList = DirectCast(pp, PengRobinsonPropertyPackage).ReturnCriticalPoints()

                            For Each dl As Double() In res
                                results.Data.Add("CriticalPoint", New List(Of Double) From {dl(0).ConvertFromSI(Units.temperature), dl(1).ConvertFromSI(Units.pressure), dl(2).ConvertFromSI(Units.molar_volume)})
                            Next

                            If results.Language.Equals("pt-BR") Then

                                results.TextOutput += "Resultados do cálculo do Ponto Crítico Verdadeiro para a mistura " & MixName & " " & Compounds.ToArrayString & System.Environment.NewLine
                                results.TextOutput += "Pacote de Propriedades: " & PropertyPackageName & System.Environment.NewLine & System.Environment.NewLine
                                If res(0)(3) = 0.0# Then results.TextOutput += "Cálculo do Ponto Crítico Verdadeiro falhou. Mostrando informações como uma média ponderada das propriedades individuais." & System.Environment.NewLine
                                results.TextOutput += "Temperatura crítica: " & results.Data("CriticalPoint")(0).ToString(results.NumberFormat) & " " & Units.temperature & System.Environment.NewLine
                                results.TextOutput += "Pressão crítica: " & results.Data("CriticalPoint")(1).ToString(results.NumberFormat) & " " & Units.pressure & System.Environment.NewLine
                                results.TextOutput += "Volume crítico: " & results.Data("CriticalPoint")(2).ToString(results.NumberFormat) & " " & Units.molar_volume & System.Environment.NewLine

                            Else

                                results.TextOutput += "True Critical Point calculation results for " & MixName & " " & Compounds.ToArrayString & System.Environment.NewLine
                                results.TextOutput += "Property Package: " & PropertyPackageName & System.Environment.NewLine & System.Environment.NewLine
                                If res(0)(3) = 0.0# Then results.TextOutput += "True Critical Point calculation failed. Showing mole-fraction averaged compound values." & System.Environment.NewLine
                                results.TextOutput += "Critical Temperature: " & results.Data("CriticalPoint")(0).ToString(results.NumberFormat) & " " & Units.temperature & System.Environment.NewLine
                                results.TextOutput += "Critical Pressure: " & results.Data("CriticalPoint")(1).ToString(results.NumberFormat) & " " & Units.pressure & System.Environment.NewLine
                                results.TextOutput += "Critical Volume: " & results.Data("CriticalPoint")(2).ToString(results.NumberFormat) & " " & Units.molar_volume & System.Environment.NewLine

                            End If

                        ElseIf pp.ComponentName.Equals("Soave-Redlich-Kwong (SRK)") Then

                            Dim res As New List(Of Double()) '= DirectCast(pp, SimulationObjects.PropertyPackages.SoaveRedlichKwongPropertyPackage).ReturnCriticalPoints()

                            For Each dl As Double() In res
                                results.Data.Add("CriticalPoint", New List(Of Double) From {dl(0).ConvertFromSI(Units.temperature), dl(1).ConvertFromSI(Units.pressure), dl(2).ConvertFromSI(Units.molar_volume)})
                            Next

                            If results.Language.Equals("pt-BR") Then

                                results.TextOutput += "Resultados do cálculo do Ponto Crítico Verdadeiro para a mistura " & MixName & " " & Compounds.ToArrayString & System.Environment.NewLine
                                results.TextOutput += "Pacote de Propriedades: " & PropertyPackageName & System.Environment.NewLine & System.Environment.NewLine
                                If res(0)(3) = 0.0# Then results.TextOutput += "Cálculo do Ponto Crítico Verdadeiro falhou. Mostrando informações como uma média ponderada das propriedades individuais." & System.Environment.NewLine
                                results.TextOutput += "Temperatura crítica: " & results.Data("CriticalPoint")(0).ToString(results.NumberFormat) & " " & Units.temperature & System.Environment.NewLine
                                results.TextOutput += "Pressão crítica: " & results.Data("CriticalPoint")(1).ToString(results.NumberFormat) & " " & Units.pressure & System.Environment.NewLine
                                results.TextOutput += "Volume crítico: " & results.Data("CriticalPoint")(2).ToString(results.NumberFormat) & " " & Units.molar_volume & System.Environment.NewLine

                            Else

                                results.TextOutput += "True Critical Point calculation results for " & MixName & " " & Compounds.ToArrayString & System.Environment.NewLine
                                results.TextOutput += "Property Package: " & PropertyPackageName & System.Environment.NewLine & System.Environment.NewLine
                                If res(0)(3) = 0.0# Then results.TextOutput += "True Critical Point calculation failed. Showing mole-fraction averaged compound values." & System.Environment.NewLine
                                results.TextOutput += "Critical Temperature: " & results.Data("CriticalPoint")(0).ToString(results.NumberFormat) & " " & Units.temperature & System.Environment.NewLine
                                results.TextOutput += "Critical Pressure: " & results.Data("CriticalPoint")(1).ToString(results.NumberFormat) & " " & Units.pressure & System.Environment.NewLine
                                results.TextOutput += "Critical Volume: " & results.Data("CriticalPoint")(2).ToString(results.NumberFormat) & " " & Units.molar_volume & System.Environment.NewLine

                            End If

                        Else

                            If results.Language.Equals("pt-BR") Then
                                Throw New Exception("Modelo inválido.")
                            Else
                                Throw New Exception("The Critical Point utility works with PR or SRK Property Package only.")
                            End If

                        End If

                    Case CalculationType.BinaryEnvelopeTxy

                        Dim res As Object() = Nothing

                        BinaryEnvelopeOptions(0) = "T-x-y"
                        BinaryEnvelopeOptions(1) = MixPressure
                        BinaryEnvelopeOptions(2) = MixTemperature
                        res = pp.DW_ReturnBinaryEnvelope(BinaryEnvelopeOptions)

                        results.Data.Add("px", ToDoubles(res(0)))
                        results.DataUnits.Add("px", "")
                        results.Data.Add("py1", ToDoubles(res(1)).ConvertFromSI(Units.temperature))
                        results.DataUnits.Add("py1", Units.temperature)
                        results.Data.Add("py2", ToDoubles(res(2)).ConvertFromSI(Units.temperature))
                        results.DataUnits.Add("py2", Units.temperature)
                        results.Data.Add("px1l1", ToDoubles(res(3)))
                        results.DataUnits.Add("px1l1", "")
                        results.Data.Add("px1l2", ToDoubles(res(4)))
                        results.DataUnits.Add("px1l2", "")
                        results.Data.Add("py3", ToDoubles(res(5)).ConvertFromSI(Units.temperature))
                        results.DataUnits.Add("py3", Units.temperature)
                        results.Data.Add("pxs1", ToDoubles(res(6)))
                        results.DataUnits.Add("pxs1", "")
                        results.Data.Add("pys1", ToDoubles(res(7)).ConvertFromSI(Units.temperature))
                        results.DataUnits.Add("pys1", Units.temperature)
                        results.Data.Add("pxs2", ToDoubles(res(8)))
                        results.DataUnits.Add("pxs2", "")
                        results.Data.Add("pys2", ToDoubles(res(9)).ConvertFromSI(Units.temperature))
                        results.DataUnits.Add("pys2", Units.temperature)
                        results.Data.Add("pxc", ToDoubles(res(10)))
                        results.DataUnits.Add("pxc", "")
                        results.Data.Add("pyc", ToDoubles(res(11)).ConvertFromSI(Units.temperature))
                        results.DataUnits.Add("pyc", Units.temperature)

                        Dim model1 As New Global.OxyPlot.PlotModel With {.Title = "Binary Envelope (Txy) @ " &
                                            MixPressure.ConvertFromSI(Units.pressure).ToString(results.NumberFormat) & " " &
                                            Units.pressure, .Subtitle = MixName & " " & Compounds.ToArrayString() & " / " & "Model: " & PropertyPackageName}

                        With model1

                            .TitleFontSize = 14
                            .SubtitleFontSize = 10

                            .Axes.Add(New LinearAxis() With {.MajorGridlineStyle = LineStyle.Dash, .MinorGridlineStyle = LineStyle.Dot, .Position = AxisPosition.Bottom, .Title = "Mole Fraction " & Compounds(0), .FontSize = 12})
                            .Axes.Add(New LinearAxis() With {.MajorGridlineStyle = LineStyle.Dash, .MinorGridlineStyle = LineStyle.Dot, .Position = AxisPosition.Left, .Title = "Temperature (" & Units.temperature & ")", .FontSize = 12})

                            If DisplayEnvelopeAreas Then

                                'draw areas

                                .AddAreaSeries(results.Data("px").ToArray, results.Data("py1").ToArray, results.Data("py2").ToArray, OxyColors.LightGreen, "V+L", True)
                                If results.Data("pxc").Count > 0 Then
                                    .AddAreaSeriesAbove(results.Data("px").ToArray, results.Data("pyc").ToArray, results.Data("pyc").Max * 1.2, OxyColors.Salmon, "NC", True)
                                    .AddAreaSeries(results.Data("px").ToArray, results.Data("py2").ToArray, results.Data("pxc"), results.Data("pyc").ToArray, OxyColors.LightYellow, "V", True)
                                Else
                                    Dim maxt = _MaterialStream.Phases(0).Compounds.First.Value.ConstantProperties.Critical_Temperature + _MaterialStream.Phases(0).Compounds.Last.Value.ConstantProperties.Critical_Temperature
                                    maxt /= 2
                                    .AddAreaSeriesAbove(results.Data("px").ToArray, results.Data("py2").ToArray, maxt, OxyColors.Salmon, "V", True)
                                End If
                                If results.Data("px1l1").Count > 0 Then
                                    Dim i As Integer
                                    Dim minxl1, maxxl1, minxl2, maxxl2 As Double
                                    Dim curve1, curve2, curve3, curve4 As New List(Of Point.Point)
                                    For i = 0 To results.Data("px").Count - 1
                                        curve1.Add(New Point.Point(results.Data("px")(i), results.Data("py1")(i)))
                                    Next
                                    For i = 0 To results.Data("px1l1").Count - 1
                                        curve2.Add(New Point.Point(results.Data("px1l1")(i), results.Data("py3")(i)))
                                    Next
                                    For i = 0 To results.Data("px1l2").Count - 1
                                        curve3.Add(New Point.Point(results.Data("px1l2")(i), results.Data("py3")(i)))
                                    Next
                                    If results.Data("pxs1").Count > 0 Then
                                        'SVLLE
                                        If results.Data("pys2").Sum > results.Data("pys1").Sum Then
                                            For i = 0 To results.Data("pxs2").Count - 1
                                                curve4.Add(New Point.Point(results.Data("pxs2")(i), results.Data("pys2")(i)))
                                            Next
                                        Else
                                            For i = 0 To results.Data("pxs1").Count - 1
                                                curve4.Add(New Point.Point(results.Data("pxs1")(i), results.Data("pys1")(i)))
                                            Next
                                        End If
                                    Else
                                        'VLLE
                                        For i = 0 To results.Data("px").Count - 1
                                            curve4.Add(New Point.Point(results.Data("px")(i), results.Data("py3").Min))
                                        Next
                                    End If
                                    minxl1 = curve2.Select(Function(_x) _x.X).Min
                                    maxxl1 = curve2.Select(Function(_x) _x.X).Max
                                    minxl2 = curve3.Select(Function(_x) _x.X).Min
                                    maxxl2 = curve3.Select(Function(_x) _x.X).Max
                                    Dim iBL1, iBL2, iSL1, iSL2 As New List(Of Point.Point)
                                    iBL1 = MathEx.Intersection.FindIntersection(curve1.Where(Function(_x) _x.X > minxl1 * 0.9 And _x.X < maxxl1 * 1.1).ToList, curve2, MathEx.LMFit.FitType.Linear, MathEx.LMFit.FitType.Linear, 1.0, 0.0, 1.0, 10000)
                                    iBL2 = MathEx.Intersection.FindIntersection(curve1.Where(Function(_x) _x.X > minxl2 * 0.9 And _x.X < maxxl2 * 1.1).ToList, curve3, MathEx.LMFit.FitType.Linear, MathEx.LMFit.FitType.Linear, 1.0, 0.0, 1.0, 10000)
                                    If results.Data("pxs1").Count > 0 Then
                                        'SVLLE
                                        iSL1 = MathEx.Intersection.FindIntersection(curve4.Where(Function(_x) _x.X > minxl1 * 0.9 And _x.X < maxxl1 * 1.1).ToList, curve2, MathEx.LMFit.FitType.ThirdDegreePoly, MathEx.LMFit.FitType.Linear, 2.0, 0.0, 1.0, 10000)
                                        iSL2 = MathEx.Intersection.FindIntersection(curve4.Where(Function(_x) _x.X > minxl2 * 0.9 And _x.X < maxxl2 * 1.1).ToList, curve3, MathEx.LMFit.FitType.ThirdDegreePoly, MathEx.LMFit.FitType.Linear, 2.0, 0.0, 1.0, 10000)
                                    Else
                                        'VLLE
                                        iSL1 = MathEx.Intersection.FindIntersection(curve4.Where(Function(_x) _x.X > minxl1 * 0.9 And _x.X < maxxl1 * 1.1).ToList, curve2, MathEx.LMFit.FitType.Linear, MathEx.LMFit.FitType.Linear, 2.0, 0.0, 1.0, 10000)
                                        iSL2 = MathEx.Intersection.FindIntersection(curve4.Where(Function(_x) _x.X > minxl2 * 0.9 And _x.X < maxxl2 * 1.1).ToList, curve3, MathEx.LMFit.FitType.Linear, MathEx.LMFit.FitType.Linear, 2.0, 0.0, 1.0, 10000)
                                    End If
                                    If iBL1.Count > 0 And iSL1.Count > 0 Then
                                        'L1
                                        Dim p1, p2 As New List(Of Point.Point)
                                        Dim i1 = iBL1(0)
                                        Dim i2 = iSL1(0)
                                        If i1.X > i2.X Then
                                            For i = 0 To results.Data("px").Count - 1
                                                If results.Data("px")(i) <= i1.X Then
                                                    p1.Add(New Point.Point(results.Data("px")(i), results.Data("py1")(i)))
                                                End If
                                            Next
                                            For i = 0 To curve4.Count - 1
                                                If curve4(i).X <= i2.X Then
                                                    p2.Add(curve4(i))
                                                End If
                                            Next
                                            For i = 0 To results.Data("px1l1").Count - 1
                                                p2.Add(New Point.Point(results.Data("px1l1")(i), results.Data("py3")(i)))
                                            Next
                                            p1.Add(i1)
                                            p2.Add(i1)
                                        Else
                                            For i = 0 To results.Data("px").Count - 1
                                                If results.Data("px")(i) <= i1.X Then
                                                    p1.Add(New Point.Point(results.Data("px")(i), results.Data("py1")(i)))
                                                End If
                                            Next
                                            For i = 0 To results.Data("px1l1").Count - 1
                                                p1.Add(New Point.Point(results.Data("px1l1")(i), results.Data("py3")(i)))
                                            Next
                                            For i = 0 To curve4.Count - 1
                                                If curve4(i).X <= i2.X Then
                                                    p2.Add(curve4(i))
                                                End If
                                            Next
                                            p1.Add(i2)
                                            p2.Add(i2)
                                        End If
                                        p1 = p1.OrderBy(Function(_p) _p.X).ToList
                                        p2 = p2.OrderBy(Function(_p) _p.X).ToList
                                        .AddAreaSeries(p1.Select(Function(_p) _p.X), p1.Select(Function(_p) _p.Y), p2.Select(Function(_p) _p.X), p2.Select(Function(_p) _p.Y), OxyColors.LightBlue, "L1", True)
                                    End If
                                    If iBL2.Count > 0 And iSL2.Count > 0 Then
                                        'L2
                                        Dim p1, p2 As New List(Of Point.Point)
                                        Dim i1 = iBL2(0)
                                        Dim i2 = iSL2(0)
                                        If i1.X < i2.X Then
                                            For i = 0 To results.Data("px").Count - 1
                                                If results.Data("px")(i) >= i1.X Then
                                                    p1.Add(New Point.Point(results.Data("px")(i), results.Data("py1")(i)))
                                                End If
                                            Next
                                            For i = 0 To results.Data("px1l2").Count - 1
                                                p2.Add(New Point.Point(results.Data("px1l2")(i), results.Data("py3")(i)))
                                            Next
                                            For i = 0 To curve4.Count - 1
                                                If curve4(i).X >= i2.X Then
                                                    p2.Add(curve4(i))
                                                End If
                                            Next
                                            p1.Add(i1)
                                            p2.Add(i1)
                                        Else
                                            For i = 0 To results.Data("px1l2").Count - 1
                                                p1.Add(New Point.Point(results.Data("px1l2")(i), results.Data("py3")(i)))
                                            Next
                                            For i = 0 To results.Data("px").Count - 1
                                                If results.Data("px")(i) >= i1.X Then
                                                    p1.Add(New Point.Point(results.Data("px")(i), results.Data("py1")(i)))
                                                End If
                                            Next
                                            For i = 0 To curve4.Count - 1
                                                If curve4(i).X >= i2.X Then
                                                    p2.Add(curve4(i))
                                                End If
                                            Next
                                            p1.Add(i2)
                                            p2.Add(i2)
                                        End If
                                        p1 = p1.OrderBy(Function(_p) _p.X).ToList
                                        p2 = p2.OrderBy(Function(_p) _p.X).ToList
                                        .AddAreaSeries(p1.Select(Function(_p) _p.X), p1.Select(Function(_p) _p.Y), p2.Select(Function(_p) _p.X), p2.Select(Function(_p) _p.Y), OxyColors.LightBlue, "L2", True)
                                    End If
                                    If iBL1.Count > 0 And iSL1.Count > 0 And iBL2.Count > 0 And iSL2.Count > 0 Then
                                        'L1+L2
                                        Dim p1, p2 As New List(Of Point.Point)
                                        Dim i1 = iBL1(0)
                                        Dim i2 = iSL1(0)
                                        Dim i3 = iBL2(0)
                                        Dim i4 = iSL2(0)
                                        If i1.X > i2.X And i3.X > i4.X Then
                                            For i = 0 To results.Data("px1l1").Count - 1
                                                p1.Add(New Point.Point(results.Data("px1l1")(i), results.Data("py3")(i)))
                                            Next
                                            For i = 0 To results.Data("px").Count - 1
                                                If results.Data("px")(i) > i1.X And results.Data("px")(i) < i3.X Then
                                                    p1.Add(New Point.Point(results.Data("px")(i), results.Data("py1")(i)))
                                                End If
                                            Next
                                            For i = 0 To curve4.Count - 1
                                                If curve4(i).X > i2.X And curve4(i).X < i4.X Then
                                                    p2.Add(curve4(i))
                                                End If
                                            Next
                                            For i = 0 To results.Data("px1l1").Count - 1
                                                p2.Add(New Point.Point(results.Data("px1l1")(i), results.Data("py3")(i)))
                                            Next
                                            p1.Add(i2)
                                            p2.Add(i2)
                                            p1.Add(i3)
                                            p2.Add(i3)
                                        ElseIf i1.X > i2.X And i3.X < i4.X Then
                                            For i = 0 To results.Data("px1l1").Count - 1
                                                p1.Add(New Point.Point(results.Data("px1l1")(i), results.Data("py3")(i)))
                                            Next
                                            For i = 0 To results.Data("px").Count - 1
                                                If results.Data("px")(i) > i1.X And results.Data("px")(i) < i3.X Then
                                                    p1.Add(New Point.Point(results.Data("px")(i), results.Data("py1")(i)))
                                                End If
                                            Next
                                            For i = 0 To results.Data("px1l1").Count - 1
                                                p1.Add(New Point.Point(results.Data("px1l1")(i), results.Data("py3")(i)))
                                            Next
                                            For i = 0 To curve4.Count - 1
                                                If curve4(i).X > i2.X And curve4(i).X < i4.X Then
                                                    p2.Add(curve4(i))
                                                End If
                                            Next
                                            p1.Add(i2)
                                            p2.Add(i2)
                                            p1.Add(i4)
                                            p2.Add(i4)
                                        ElseIf i1.X < i2.X And i3.X < i4.X Then
                                            For i = 0 To results.Data("px").Count - 1
                                                If results.Data("px")(i) > i1.X And results.Data("px")(i) < i3.X Then
                                                    p1.Add(New Point.Point(results.Data("px")(i), results.Data("py1")(i)))
                                                End If
                                            Next
                                            For i = 0 To results.Data("px1l2").Count - 1
                                                p1.Add(New Point.Point(results.Data("px1l2")(i), results.Data("py3")(i)))
                                            Next
                                            For i = 0 To results.Data("px1l1").Count - 1
                                                p2.Add(New Point.Point(results.Data("px1l1")(i), results.Data("py3")(i)))
                                            Next
                                            For i = 0 To curve4.Count - 1
                                                If curve4(i).X > i2.X And curve4(i).X < i4.X Then
                                                    p2.Add(curve4(i))
                                                End If
                                            Next
                                            p1.Add(i1)
                                            p2.Add(i1)
                                            p1.Add(i4)
                                            p2.Add(i4)
                                        ElseIf i1.X < i2.X And i3.X > i4.X Then
                                            For i = 0 To results.Data("px1l1").Count - 1
                                                p2.Add(New Point.Point(results.Data("px1l1")(i), results.Data("py3")(i)))
                                            Next
                                            For i = 0 To curve4.Count - 1
                                                If curve4(i).X > i2.X And curve4(i).X < i4.X Then
                                                    p2.Add(curve4(i))
                                                End If
                                            Next
                                            For i = 0 To results.Data("px1l1").Count - 1
                                                p2.Add(New Point.Point(results.Data("px1l1")(i), results.Data("py3")(i)))
                                            Next
                                            For i = 0 To results.Data("px").Count - 1
                                                If results.Data("px")(i) > i1.X And results.Data("px")(i) < i3.X Then
                                                    p1.Add(New Point.Point(results.Data("px")(i), results.Data("py1")(i)))
                                                Else
                                                    Exit For
                                                End If
                                            Next
                                            p1.Add(i1)
                                            p2.Add(i1)
                                            p1.Add(i3)
                                            p2.Add(i3)
                                        End If
                                        p1 = p1.OrderBy(Function(_p) _p.X).ToList
                                        p2 = p2.OrderBy(Function(_p) _p.X).ToList
                                        .AddAreaSeries(p1.Select(Function(_p) _p.X), p1.Select(Function(_p) _p.Y), p2.Select(Function(_p) _p.X), p2.Select(Function(_p) _p.Y), OxyColors.CornflowerBlue, "L1+L2", True)
                                    End If
                                    If results.Data("pxs1").Count > 0 Then
                                        If results.Data("pys2").Sum > results.Data("pys1").Sum Then
                                            .AddAreaSeries(results.Data("px").ToArray, results.Data("pys1").ToArray, results.Data("pys2").ToArray, OxyColors.LightSteelBlue, "S+L", True)
                                            .AddAreaSeriesBeyond(results.Data("px").ToArray, results.Data("pys1").ToArray, OxyColors.GhostWhite, "S", True)
                                        Else
                                            .AddAreaSeries(results.Data("px").ToArray, results.Data("pys2").ToArray, results.Data("pys1").ToArray, OxyColors.LightSteelBlue, "S+L", True)
                                            .AddAreaSeriesBeyond(results.Data("px").ToArray, results.Data("pys2").ToArray, OxyColors.GhostWhite, "S", True)
                                        End If
                                    End If
                                Else
                                    If results.Data("pxs1").Count > 0 Then
                                        'SVLE
                                        If results.Data("pys2").Sum > results.Data("pys1").Sum Then
                                            .AddAreaSeries(results.Data("px").ToArray, results.Data("pys1").ToArray, results.Data("pys2").ToArray, OxyColors.LightSteelBlue, "S+L", True)
                                            .AddAreaSeriesBeyond(results.Data("px").ToArray, results.Data("pys1").ToArray, OxyColors.GhostWhite, "S", True)
                                            .AddAreaSeries(results.Data("px").ToArray, results.Data("pys2").ToArray, results.Data("py1").ToArray, OxyColors.LightBlue, "L", True)
                                        Else
                                            .AddAreaSeries(results.Data("px").ToArray, results.Data("pys2").ToArray, results.Data("pys1").ToArray, OxyColors.LightSteelBlue, "S+L", True)
                                            .AddAreaSeriesBeyond(results.Data("px").ToArray, results.Data("pys2").ToArray, OxyColors.GhostWhite, "S", True)
                                            .AddAreaSeries(results.Data("px").ToArray, results.Data("pys1").ToArray, results.Data("py1").ToArray, OxyColors.LightBlue, "L", True)
                                        End If
                                    Else
                                        'VLE
                                        .AddAreaSeriesBeyond(results.Data("px").ToArray, results.Data("py1").ToArray, OxyColors.LightBlue, "L", True)
                                    End If
                                End If

                            End If

                            .AddLineSeries(results.Data("px").ToArray, results.Data("py1").ToArray, OxyColors.DarkGreen)
                            .Series(.Series.Count - 1).Title = "Bubble Points"
                            .AddLineSeries(results.Data("px").ToArray, results.Data("py2").ToArray, OxyColors.DarkOrange)
                            .Series(.Series.Count - 1).Title = "Dew Points"


                            If results.Data("px1l1").Count > 0 Then
                                .AddLineSeries(results.Data("px1l1").ToArray, results.Data("py3").ToArray, OxyColors.SlateBlue)
                                .Series(.Series.Count - 1).Title = "Liquid-Liquid (1)"
                                .AddLineSeries(results.Data("px1l2").ToArray, results.Data("py3").ToArray, OxyColors.SlateBlue)
                                .Series(.Series.Count - 1).Title = "Liquid-Liquid (2)"
                            End If
                            If results.Data("pxs1").Count > 0 Then
                                .AddLineSeries(results.Data("pxs1").ToArray, results.Data("pys1").ToArray, OxyColors.Gray)
                                .Series(.Series.Count - 1).Title = "Solid-Liquid (1)"
                                .AddLineSeries(results.Data("pxs2").ToArray, results.Data("pys2").ToArray, OxyColors.LightGray)
                                .Series(.Series.Count - 1).Title = "Solid-Liquid (2)"
                            End If
                            If results.Data("pxc").Count > 0 Then
                                .AddLineSeries(results.Data("pxc").ToArray, results.Data("pyc").ToArray, OxyColors.Red)
                                .Series(.Series.Count - 1).Title = "Critical Line"
                            End If
                            .LegendFontSize = 10
                            .LegendPosition = LegendPosition.TopCenter
                            .LegendPlacement = LegendPlacement.Outside
                            .LegendOrientation = LegendOrientation.Horizontal
                            .TitleHorizontalAlignment = TitleHorizontalAlignment.CenteredWithinView
                        End With

                        results.PlotModels.Add(model1)

                        results.TextOutput += "Binary Envelope calculation results @ " & MixPressure.ConvertFromSI(Units.pressure).ToString(results.NumberFormat) & " " &
                                            Units.pressure & " for " & MixName & " " & Compounds.ToArrayString & System.Environment.NewLine
                        results.TextOutput += "Property Package: " & PropertyPackageName & System.Environment.NewLine & System.Environment.NewLine
                        results.TextOutput += System.Environment.NewLine
                        results.TextOutput += (Compounds(0) & " Mole Fraction").PadRight(20) & ("Bubble Point (" & Units.temperature & ")").PadRight(20) & "Dew Point (" & Units.temperature & ")" & System.Environment.NewLine
                        For i As Integer = 0 To results.Data("px").Count - 1
                            results.TextOutput += results.Data("px")(i).ToString(results.NumberFormat).PadRight(20) & results.Data("py1")(i).ToString(results.NumberFormat).PadRight(20) &
                                results.Data("py2")(i).ToString(results.NumberFormat) & System.Environment.NewLine
                        Next
                        If results.Data("px1l1").Count > 0 Then
                            results.TextOutput += System.Environment.NewLine
                            results.TextOutput += (Compounds(0) & " Mole Fraction").PadRight(20) & ("LL Line 1 (" & Units.temperature & ")").PadRight(20) & System.Environment.NewLine
                            For i As Integer = 0 To results.Data("px1l1").Count - 1
                                results.TextOutput += results.Data("px1l1")(i).ToString(results.NumberFormat).PadRight(20) & results.Data("py3")(i).ToString(results.NumberFormat).PadRight(20) & System.Environment.NewLine
                            Next
                            results.TextOutput += System.Environment.NewLine
                            results.TextOutput += (Compounds(0) & " Mole Fraction").PadRight(20) & ("LL Line 2 (" & Units.temperature & ")").PadRight(20) & System.Environment.NewLine
                            For i As Integer = 0 To results.Data("px1l2").Count - 1
                                results.TextOutput += results.Data("px1l2")(i).ToString(results.NumberFormat).PadRight(20) & results.Data("py3")(i).ToString(results.NumberFormat).PadRight(20) & System.Environment.NewLine
                            Next
                        End If
                        If results.Data("pxs1").Count > 0 Then
                            results.TextOutput += System.Environment.NewLine
                            results.TextOutput += (Compounds(0) & " Mole Fraction").PadRight(20) & ("SL Line 1 (" & Units.temperature & ")").PadRight(20) & System.Environment.NewLine
                            For i As Integer = 0 To results.Data("pxs1").Count - 1
                                results.TextOutput += results.Data("pxs1")(i).ToString(results.NumberFormat).PadRight(20) & results.Data("pys1")(i).ToString(results.NumberFormat).PadRight(20) & System.Environment.NewLine
                            Next
                            results.TextOutput += System.Environment.NewLine
                            results.TextOutput += (Compounds(0) & " Mole Fraction").PadRight(20) & ("SL Line 2 (" & Units.temperature & ")").PadRight(20) & System.Environment.NewLine
                            For i As Integer = 0 To results.Data("pxs2").Count - 1
                                results.TextOutput += results.Data("pxs2")(i).ToString(results.NumberFormat).PadRight(20) & results.Data("pys2")(i).ToString(results.NumberFormat).PadRight(20) & System.Environment.NewLine
                            Next
                        End If
                        If results.Data("pxc").Count > 0 Then
                            results.TextOutput += System.Environment.NewLine
                            results.TextOutput += (Compounds(0) & " Mole Fraction").PadRight(20) & ("Critical Line (" & Units.temperature & ")").PadRight(20) & System.Environment.NewLine
                            For i As Integer = 0 To results.Data("pxc").Count - 1
                                results.TextOutput += results.Data("pxc")(i).ToString(results.NumberFormat).PadRight(20) & results.Data("pyc")(i).ToString(results.NumberFormat).PadRight(20) & System.Environment.NewLine
                            Next
                        End If

                    Case CalculationType.BinaryEnvelopePxy

                        BinaryEnvelopeOptions(0) = "P-x-y"
                        BinaryEnvelopeOptions(1) = MixPressure
                        BinaryEnvelopeOptions(2) = MixTemperature
                        Dim res As Object() = pp.DW_ReturnBinaryEnvelope(BinaryEnvelopeOptions)

                        results.Data.Add("px", ToDoubles(res(0)))
                        results.DataUnits.Add("px", "")
                        results.Data.Add("py1", ToDoubles(res(1)).ConvertFromSI(Units.pressure))
                        results.DataUnits.Add("py1", Units.pressure)
                        results.Data.Add("py2", ToDoubles(res(2)).ConvertFromSI(Units.pressure))
                        results.DataUnits.Add("py2", Units.pressure)
                        results.Data.Add("px1l1", ToDoubles(res(3)))
                        results.DataUnits.Add("px1l1", "")
                        results.Data.Add("px1l2", ToDoubles(res(4)))
                        results.DataUnits.Add("px1l2", "")
                        results.Data.Add("py3", ToDoubles(res(5)).ConvertFromSI(Units.pressure))
                        results.DataUnits.Add("py3", Units.pressure)

                        Dim model1 As New Global.OxyPlot.PlotModel With {.Title = "Binary Envelope (Pxy) @ " &
                                            MixTemperature.ConvertFromSI(Units.temperature).ToString(results.NumberFormat) & " " &
                                            Units.temperature, .Subtitle = MixName & " " & Compounds.ToArrayString() & " / " & "Model: " & PropertyPackageName}

                        With model1
                            .TitleFontSize = 14
                            .SubtitleFontSize = 10
                            .Axes.Add(New LinearAxis() With {.MajorGridlineStyle = LineStyle.Dash, .MinorGridlineStyle = LineStyle.Dot, .Position = AxisPosition.Bottom, .Title = "Mole Fraction " & Compounds(0), .FontSize = 12})
                            .Axes.Add(New LinearAxis() With {.MajorGridlineStyle = LineStyle.Dash, .MinorGridlineStyle = LineStyle.Dot, .Position = AxisPosition.Left, .Title = "Pressure (" & Units.pressure & ")", .FontSize = 12})
                            .AddLineSeries(results.Data("px").ToArray, results.Data("py1").ToArray)
                            .AddLineSeries(results.Data("px").ToArray, results.Data("py2").ToArray)
                            .Series(0).Title = "Bubble Points"
                            .Series(1).Title = "Dew Points"
                            If results.Data("px1l1").Count > 0 Then
                                .AddLineSeries(results.Data("px1l1").ToArray, results.Data("py3").ToArray)
                                .AddLineSeries(results.Data("px1l2").ToArray, results.Data("py3").ToArray)
                                .Series(2).Title = "Liquid-Liquid (1)"
                                .Series(3).Title = "Liquid-Liquid (2)"
                            End If
                            .LegendFontSize = 10
                            .LegendPosition = LegendPosition.TopCenter
                            .LegendPlacement = LegendPlacement.Outside
                            .LegendOrientation = LegendOrientation.Horizontal
                            .TitleHorizontalAlignment = TitleHorizontalAlignment.CenteredWithinView
                        End With

                        results.PlotModels.Add(model1)

                        results.TextOutput += "Binary Envelope calculation results @ " & MixTemperature.ConvertFromSI(Units.temperature).ToString(results.NumberFormat) & " " &
                                        Units.temperature & " for " & MixName & " " & Compounds.ToArrayString & System.Environment.NewLine
                        results.TextOutput += "Property Package: " & PropertyPackageName & System.Environment.NewLine & System.Environment.NewLine
                        results.TextOutput += (Compounds(0) & " mole fraction").PadRight(20) & ("Bubble Point (" & Units.pressure & ")").PadRight(20) & "Dew Point (" & Units.pressure & ")" & System.Environment.NewLine
                        For i As Integer = 0 To results.Data("px").Count - 1
                            results.TextOutput += results.Data("px")(i).ToString(results.NumberFormat).PadRight(20) & results.Data("py1")(i).ToString(results.NumberFormat).PadRight(20) &
                                results.Data("py2")(i).ToString(results.NumberFormat) & System.Environment.NewLine
                        Next
                        If results.Data("px1l1").Count > 0 Then
                            results.TextOutput += System.Environment.NewLine
                            results.TextOutput += (Compounds(0) & " Mole Fraction").PadRight(20) & ("LL Line 1 (" & Units.pressure & ")").PadRight(20) & System.Environment.NewLine
                            For i As Integer = 0 To results.Data("px1l1").Count - 1
                                results.TextOutput += results.Data("px1l1")(i).ToString(results.NumberFormat).PadRight(20) & results.Data("py3")(i).ToString(results.NumberFormat).PadRight(20) & System.Environment.NewLine
                            Next
                            results.TextOutput += System.Environment.NewLine
                            results.TextOutput += (Compounds(0) & " Mole Fraction").PadRight(20) & ("LL Line 2 (" & Units.pressure & ")").PadRight(20) & System.Environment.NewLine
                            For i As Integer = 0 To results.Data("px1l2").Count - 1
                                results.TextOutput += results.Data("px1l2")(i).ToString(results.NumberFormat).PadRight(20) & results.Data("py3")(i).ToString(results.NumberFormat).PadRight(20) & System.Environment.NewLine
                            Next
                        End If

                    Case Else

                        Dim res As Object()

                        res = pp.DW_ReturnPhaseEnvelope(PhaseEnvelopeOptions)

                        '{TVB, PB, HB, SB, VB, TVD, PO, HO, SO, VO, TE, PE, TH, PHsI, PHsII, CP, TQ, PQ, TI, PI, TOWF, POWF, HOWF, SOWF, VOWF}</returns>

                        results.Data.Add("TB", ToDoubles(res(0)).ConvertFromSI(Units.temperature))
                        results.DataUnits.Add("TB", Units.temperature)
                        results.Data.Add("PB", ToDoubles(res(1)).ConvertFromSI(Units.pressure))
                        results.DataUnits.Add("PB", Units.pressure)
                        results.Data.Add("HB", ToDoubles(res(2)).ConvertFromSI(Units.enthalpy))
                        results.DataUnits.Add("HB", Units.enthalpy)
                        results.Data.Add("SB", ToDoubles(res(3)).ConvertFromSI(Units.entropy))
                        results.DataUnits.Add("SB", Units.entropy)
                        results.Data.Add("VB", ToDoubles(res(4)).ConvertFromSI(Units.molar_volume))
                        results.DataUnits.Add("VB", Units.molar_volume)

                        results.Data.Add("TD", ToDoubles(res(5)).ConvertFromSI(Units.temperature))
                        results.DataUnits.Add("TD", Units.temperature)
                        results.Data.Add("PD", ToDoubles(res(6)).ConvertFromSI(Units.pressure))
                        results.DataUnits.Add("PD", Units.pressure)
                        results.Data.Add("HD", ToDoubles(res(7)).ConvertFromSI(Units.enthalpy))
                        results.DataUnits.Add("HD", Units.enthalpy)
                        results.Data.Add("SD", ToDoubles(res(8)).ConvertFromSI(Units.entropy))
                        results.DataUnits.Add("SD", Units.entropy)
                        results.Data.Add("VD", ToDoubles(res(9)).ConvertFromSI(Units.molar_volume))
                        results.DataUnits.Add("VD", Units.molar_volume)

                        results.Data.Add("TE", ToDoubles(res(10)).ConvertFromSI(Units.temperature))
                        results.DataUnits.Add("TE", Units.temperature)
                        results.Data.Add("PE", ToDoubles(res(11)).ConvertFromSI(Units.pressure))
                        results.DataUnits.Add("PE", Units.pressure)

                        Dim cpdata As Object = res(15)

                        results.Data.Add("CP", New List(Of Double) From {Convert.ToDouble(cpdata(0)(0).ToString).ConvertFromSI(Units.temperature),
                                                                         Convert.ToDouble(cpdata(0)(1).ToString).ConvertFromSI(Units.pressure),
                                                                         Convert.ToDouble(cpdata(0)(2).ToString).ConvertFromSI(Units.molar_volume)})

                        results.DataUnits.Add("CP", "")

                        results.Data.Add("TQ", ToDoubles(res(16)).ConvertFromSI(Units.temperature))
                        results.DataUnits.Add("TQ", Units.temperature)
                        results.Data.Add("PQ", ToDoubles(res(17)).ConvertFromSI(Units.pressure))
                        results.DataUnits.Add("PQ", Units.pressure)

                        results.Data.Add("TI", ToDoubles(res(18)).ConvertFromSI(Units.temperature))
                        results.DataUnits.Add("TI", Units.temperature)
                        results.Data.Add("PI", ToDoubles(res(19)).ConvertFromSI(Units.pressure))
                        results.DataUnits.Add("PI", Units.pressure)

                        If res.Length > 35 Then
                            Dim tsle1List = TryCast(res(35), List(Of Double))
                            Dim psle1List = TryCast(res(36), List(Of Double))
                            Dim tsle2List = TryCast(res(37), List(Of Double))
                            Dim psle2List = TryCast(res(38), List(Of Double))
                            If tsle1List IsNot Nothing AndAlso tsle1List.Count > 0 Then
                                results.Data.Add("TSLE1", tsle1List.ConvertFromSI(Units.temperature))
                                results.DataUnits.Add("TSLE1", Units.temperature)
                                results.Data.Add("PSLE1", psle1List.ConvertFromSI(Units.pressure))
                                results.DataUnits.Add("PSLE1", Units.pressure)
                            End If
                            If tsle2List IsNot Nothing AndAlso tsle2List.Count > 0 Then
                                results.Data.Add("TSLE2", tsle2List.ConvertFromSI(Units.temperature))
                                results.DataUnits.Add("TSLE2", Units.temperature)
                                results.Data.Add("PSLE2", psle2List.ConvertFromSI(Units.pressure))
                                results.DataUnits.Add("PSLE2", Units.pressure)
                            End If
                        End If

                        If res.Length > 39 Then
                            Dim tWidomCpList = TryCast(res(39), List(Of Double))
                            Dim pWidomCpList = TryCast(res(40), List(Of Double))
                            Dim tWidomBtList = TryCast(res(41), List(Of Double))
                            Dim pWidomBtList = TryCast(res(42), List(Of Double))
                            If tWidomCpList IsNot Nothing AndAlso tWidomCpList.Count > 0 Then
                                results.Data.Add("TWidomCp", tWidomCpList.ConvertFromSI(Units.temperature))
                                results.DataUnits.Add("TWidomCp", Units.temperature)
                                results.Data.Add("PWidomCp", pWidomCpList.ConvertFromSI(Units.pressure))
                                results.DataUnits.Add("PWidomCp", Units.pressure)
                            End If
                            If tWidomBtList IsNot Nothing AndAlso tWidomBtList.Count > 0 Then
                                results.Data.Add("TWidomBetaT", tWidomBtList.ConvertFromSI(Units.temperature))
                                results.DataUnits.Add("TWidomBetaT", Units.temperature)
                                results.Data.Add("PWidomBetaT", pWidomBtList.ConvertFromSI(Units.pressure))
                                results.DataUnits.Add("PWidomBetaT", Units.pressure)
                            End If
                        End If

                        If res.Length > 43 Then
                            Dim tWidomAvgList = TryCast(res(43), List(Of Double))
                            Dim pWidomAvgList = TryCast(res(44), List(Of Double))
                            If tWidomAvgList IsNot Nothing AndAlso tWidomAvgList.Count > 0 Then
                                results.Data.Add("TWidomAvg", tWidomAvgList.ConvertFromSI(Units.temperature))
                                results.DataUnits.Add("TWidomAvg", Units.temperature)
                                results.Data.Add("PWidomAvg", pWidomAvgList.ConvertFromSI(Units.pressure))
                                results.DataUnits.Add("PWidomAvg", Units.pressure)
                            End If
                        End If

                        Select Case CalcType

                            Case CalculationType.PhaseEnvelopePT

                                Dim model1 As New Global.OxyPlot.PlotModel With {.Title = "Pressure/Temperature diagram",
                                                                                     .Subtitle = MixName & " " & Compounds.ToArrayString() & " / " & "Model: " & PropertyPackageName}

                                With model1
                                    .TitleFontSize = 14
                                    .SubtitleFontSize = 10
                                    .Axes.Add(New LinearAxis() With {.MajorGridlineStyle = LineStyle.Dash, .MinorGridlineStyle = LineStyle.Dot, .Position = AxisPosition.Bottom, .Title = "Temperature (" & Units.temperature & ")", .FontSize = 12})
                                    .Axes.Add(New LinearAxis() With {.MajorGridlineStyle = LineStyle.Dash, .MinorGridlineStyle = LineStyle.Dot, .Position = AxisPosition.Left, .Title = "Pressure (" & Units.pressure & ")", .FontSize = 12})
                                    .AddLineSeries(results.Data("TB").ToArray, results.Data("PB").ToArray)
                                    .Series(0).Title = "Bubble Points"
                                    .AddLineSeries(results.Data("TD").ToArray, results.Data("PD").ToArray)
                                    .Series(1).Title = "Dew Points"
                                    .AddScatterSeries(New Double() {results.Data("CP")(0)}, New Double() {results.Data("CP")(1)})
                                    DirectCast(.Series(2), ScatterSeries).MarkerSize = 3
                                    .Series(2).Title = "Critical Point"
                                    If PhaseEnvelopeOptions.PhaseIdentificationCurve Then
                                        .AddLineSeries(results.Data("TI").ToArray, results.Data("PI").ToArray)
                                        .Series(.Series.Count - 1).Title = "Phase Identification Parameter"
                                    End If
                                    If PhaseEnvelopeOptions.QualityLine Then
                                        .AddLineSeries(results.Data("TQ").ToArray, results.Data("PQ").ToArray)
                                        .Series(.Series.Count - 1).Title = "Quality Curve VF = " & PhaseEnvelopeOptions.QualityValue.ToString
                                    End If
                                    If PhaseEnvelopeOptions.StabilityCurve Then
                                        .AddLineSeries(results.Data("TE").ToArray, results.Data("PE").ToArray)
                                        .Series(.Series.Count - 1).Title = "Stability Curve"
                                    End If
                                    If PhaseEnvelopeOptions.OperatingPoint Then
                                        .AddScatterSeries(New Double() {MixTemperature.ConvertFromSI(Units.temperature)}, New Double() {MixPressure.ConvertFromSI(Units.pressure)})
                                        DirectCast(.Series(.Series.Count - 1), ScatterSeries).MarkerSize = 3
                                        .Series(.Series.Count - 1).Title = "Operating Point"
                                    End If
                                    If results.Data.ContainsKey("TSLE1") Then
                                        .AddLineSeries(results.Data("TSLE1").ToArray, results.Data("PSLE1").ToArray)
                                        .Series(.Series.Count - 1).Title = "SLE Liquidus"
                                    End If
                                    If results.Data.ContainsKey("TSLE2") Then
                                        .AddLineSeries(results.Data("TSLE2").ToArray, results.Data("PSLE2").ToArray)
                                        .Series(.Series.Count - 1).Title = "SLE Solidus"
                                    End If
                                    If results.Data.ContainsKey("TWidomCp") Then
                                        .AddLineSeries(results.Data("TWidomCp").ToArray, results.Data("PWidomCp").ToArray)
                                        .Series(.Series.Count - 1).Title = "Widom Line (Cp)"
                                        DirectCast(.Series(.Series.Count - 1), LineSeries).LineStyle = LineStyle.Dash
                                    End If
                                    If results.Data.ContainsKey("TWidomBetaT") Then
                                        .AddLineSeries(results.Data("TWidomBetaT").ToArray, results.Data("PWidomBetaT").ToArray)
                                        .Series(.Series.Count - 1).Title = "Widom Line (kT)"
                                        DirectCast(.Series(.Series.Count - 1), LineSeries).LineStyle = LineStyle.DashDot
                                    End If
                                    If results.Data.ContainsKey("TWidomAvg") Then
                                        .AddLineSeries(results.Data("TWidomAvg").ToArray, results.Data("PWidomAvg").ToArray)
                                        .Series(.Series.Count - 1).Title = "Widom Line (avg)"
                                    End If

                                    .LegendFontSize = 10
                                    .LegendPosition = LegendPosition.TopCenter
                                    .LegendPlacement = LegendPlacement.Outside
                                    .LegendOrientation = LegendOrientation.Horizontal
                                    .TitleHorizontalAlignment = TitleHorizontalAlignment.CenteredWithinView
                                End With

                                results.PlotModels.Add(model1)

                                results.TextOutput += "Phase Envelope calculation results for " & MixName & " " & Compounds.ToArrayString & System.Environment.NewLine
                                results.TextOutput += "Property Package: " & PropertyPackageName & System.Environment.NewLine & System.Environment.NewLine
                                results.TextOutput += Environment.NewLine
                                results.TextOutput += ("Bubble Temp. (" & Units.temperature & ")").PadRight(20) &
                                    ("Bubble Pressure (" & Units.pressure & ")").PadRight(20) & System.Environment.NewLine
                                For i As Integer = 0 To results.Data("PB").Count - 1
                                    results.TextOutput += results.Data("TB")(i).ToString(results.NumberFormat).PadRight(20) & results.Data("PB")(i).ToString(results.NumberFormat).PadRight(20) & System.Environment.NewLine
                                Next
                                results.TextOutput += Environment.NewLine
                                results.TextOutput += ("Dew Temp. (" & Units.temperature & ")").PadRight(20) &
                                    "Dew Pressure (" & Units.pressure & ")" & System.Environment.NewLine
                                For i As Integer = 0 To results.Data("PD").Count - 1
                                    results.TextOutput += results.Data("TD")(i).ToString(results.NumberFormat).PadRight(20) & results.Data("PD")(i).ToString(results.NumberFormat) & System.Environment.NewLine
                                Next
                                If PhaseEnvelopeOptions.PhaseIdentificationCurve Then
                                    results.TextOutput += Environment.NewLine
                                    results.TextOutput += ("PIP Temp. (" & Units.temperature & ")").PadRight(20) &
                                    ("PIP Pressure (" & Units.pressure & ")").PadRight(20) & System.Environment.NewLine
                                    For i As Integer = 0 To results.Data("PI").Count - 1
                                        results.TextOutput += results.Data("TI")(i).ToString(results.NumberFormat).PadRight(20) & results.Data("PI")(i).ToString(results.NumberFormat).PadRight(20) & System.Environment.NewLine
                                    Next
                                End If
                                If PhaseEnvelopeOptions.QualityLine Then
                                    results.TextOutput += Environment.NewLine
                                    results.TextOutput += ("Quality Line Temp. (" & Units.temperature & ")").PadRight(20) &
                                    ("Quality Line Pressure (" & Units.pressure & ")").PadRight(20) & System.Environment.NewLine
                                    For i As Integer = 0 To results.Data("PQ").Count - 1
                                        results.TextOutput += results.Data("TQ")(i).ToString(results.NumberFormat).PadRight(20) & results.Data("PQ")(i).ToString(results.NumberFormat).PadRight(20) & System.Environment.NewLine
                                    Next
                                End If
                                If PhaseEnvelopeOptions.StabilityCurve Then
                                    results.TextOutput += Environment.NewLine
                                    results.TextOutput += ("Stability Curve Temp. (" & Units.temperature & ")").PadRight(20) &
                                    ("Stability Curve Pressure (" & Units.pressure & ")").PadRight(20) & System.Environment.NewLine
                                    For i As Integer = 0 To results.Data("PE").Count - 1
                                        results.TextOutput += results.Data("TE")(i).ToString(results.NumberFormat).PadRight(20) & results.Data("PE")(i).ToString(results.NumberFormat).PadRight(20) & System.Environment.NewLine
                                    Next
                                End If
                                If results.Data.ContainsKey("TSLE1") Then
                                    results.TextOutput += Environment.NewLine
                                    results.TextOutput += ("SLE Liquidus Temp. (" & Units.temperature & ")").PadRight(20) &
                                    ("SLE Liquidus Pressure (" & Units.pressure & ")").PadRight(20) & System.Environment.NewLine
                                    For i As Integer = 0 To results.Data("TSLE1").Count - 1
                                        results.TextOutput += results.Data("TSLE1")(i).ToString(results.NumberFormat).PadRight(20) & results.Data("PSLE1")(i).ToString(results.NumberFormat).PadRight(20) & System.Environment.NewLine
                                    Next
                                End If
                                If results.Data.ContainsKey("TSLE2") Then
                                    results.TextOutput += Environment.NewLine
                                    results.TextOutput += ("SLE Solidus Temp. (" & Units.temperature & ")").PadRight(20) &
                                    ("SLE Solidus Pressure (" & Units.pressure & ")").PadRight(20) & System.Environment.NewLine
                                    For i As Integer = 0 To results.Data("TSLE2").Count - 1
                                        results.TextOutput += results.Data("TSLE2")(i).ToString(results.NumberFormat).PadRight(20) & results.Data("PSLE2")(i).ToString(results.NumberFormat).PadRight(20) & System.Environment.NewLine
                                    Next
                                End If
                                If results.Data.ContainsKey("TWidomCp") Then
                                    results.TextOutput += Environment.NewLine
                                    results.TextOutput += ("Widom Cp Temp. (" & Units.temperature & ")").PadRight(20) &
                                    ("Widom Cp Pressure (" & Units.pressure & ")").PadRight(20) & System.Environment.NewLine
                                    For i As Integer = 0 To results.Data("TWidomCp").Count - 1
                                        results.TextOutput += results.Data("TWidomCp")(i).ToString(results.NumberFormat).PadRight(20) & results.Data("PWidomCp")(i).ToString(results.NumberFormat).PadRight(20) & System.Environment.NewLine
                                    Next
                                End If
                                If results.Data.ContainsKey("TWidomBetaT") Then
                                    results.TextOutput += Environment.NewLine
                                    results.TextOutput += ("Widom kT Temp. (" & Units.temperature & ")").PadRight(20) &
                                    ("Widom kT Pressure (" & Units.pressure & ")").PadRight(20) & System.Environment.NewLine
                                    For i As Integer = 0 To results.Data("TWidomBetaT").Count - 1
                                        results.TextOutput += results.Data("TWidomBetaT")(i).ToString(results.NumberFormat).PadRight(20) & results.Data("PWidomBetaT")(i).ToString(results.NumberFormat).PadRight(20) & System.Environment.NewLine
                                    Next
                                End If
                                If results.Data.ContainsKey("TWidomAvg") Then
                                    results.TextOutput += Environment.NewLine
                                    results.TextOutput += ("Widom avg Temp. (" & Units.temperature & ")").PadRight(20) &
                                    ("Widom avg Pressure (" & Units.pressure & ")").PadRight(20) & System.Environment.NewLine
                                    For i As Integer = 0 To results.Data("TWidomAvg").Count - 1
                                        results.TextOutput += results.Data("TWidomAvg")(i).ToString(results.NumberFormat).PadRight(20) & results.Data("PWidomAvg")(i).ToString(results.NumberFormat).PadRight(20) & System.Environment.NewLine
                                    Next
                                End If

                            Case CalculationType.PhaseEnvelopePH

                                Dim model1 As New Global.OxyPlot.PlotModel With {.Title = "Pressure/Enthalpy diagram",
                                                                                     .Subtitle = MixName & " " & Compounds.ToArrayString() & " / " & "Model: " & PropertyPackageName}

                                With model1
                                    .TitleFontSize = 14
                                    .SubtitleFontSize = 10
                                    .Axes.Add(New LinearAxis() With {.MajorGridlineStyle = LineStyle.Dash, .MinorGridlineStyle = LineStyle.Dot, .Position = AxisPosition.Bottom, .Title = "Enthalpy (" & Units.enthalpy & ")", .FontSize = 12})
                                    .Axes.Add(New LinearAxis() With {.MajorGridlineStyle = LineStyle.Dash, .MinorGridlineStyle = LineStyle.Dot, .Position = AxisPosition.Left, .Title = "Pressure (" & Units.pressure & ")", .FontSize = 12})
                                    .AddLineSeries(results.Data("HB").ToArray, results.Data("PB").ToArray)
                                    .AddLineSeries(results.Data("HD").ToArray, results.Data("PD").ToArray)
                                    .Series(0).Title = "Bubble Points"
                                    .Series(1).Title = "Dew Points"
                                    If PhaseEnvelopeOptions.OperatingPoint Then
                                        .AddScatterSeries(New Double() {MixEnthalpy.ConvertFromSI(Units.enthalpy)}, New Double() {MixPressure.ConvertFromSI(Units.pressure)})
                                        DirectCast(.Series(.Series.Count - 1), ScatterSeries).MarkerSize = 3
                                        .Series(.Series.Count - 1).Title = "Operating Point"
                                    End If
                                    .LegendFontSize = 10
                                    .LegendPosition = LegendPosition.TopCenter
                                    .LegendPlacement = LegendPlacement.Outside
                                    .LegendOrientation = LegendOrientation.Horizontal
                                    .TitleHorizontalAlignment = TitleHorizontalAlignment.CenteredWithinView
                                End With

                                results.PlotModels.Add(model1)

                                results.TextOutput += "Phase Envelope calculation results for " & MixName & " " & Compounds.ToArrayString & System.Environment.NewLine
                                results.TextOutput += "Property Package: " & PropertyPackageName & System.Environment.NewLine & System.Environment.NewLine
                                results.TextOutput += ("Bubble Enthalpy (" & Units.enthalpy & ")").PadRight(20) &
                                    ("Bubble Press. (" & Units.pressure & ")").PadRight(20) & System.Environment.NewLine
                                For i As Integer = 0 To results.Data("PB").Count - 1
                                    results.TextOutput += results.Data("HB")(i).ToString(results.NumberFormat).PadRight(20) & results.Data("PB")(i).ToString(results.NumberFormat).PadRight(20) & System.Environment.NewLine
                                Next
                                results.TextOutput += ("Dew Enthalpy (" & Units.enthalpy & ")").PadRight(20) &
                                    ("Dew Press. (" & Units.pressure & ")") & System.Environment.NewLine
                                For i As Integer = 0 To results.Data("PD").Count - 1
                                    results.TextOutput += results.Data("HD")(i).ToString(results.NumberFormat).PadRight(20) & results.Data("PD")(i).ToString(results.NumberFormat) & System.Environment.NewLine
                                Next

                            Case CalculationType.PhaseEnvelopePS

                                Dim model1 As New Global.OxyPlot.PlotModel With {.Title = "Pressure/Entropy diagram",
                                                                                     .Subtitle = MixName & " " & Compounds.ToArrayString() & " / " & "Model: " & PropertyPackageName}

                                With model1
                                    .TitleFontSize = 14
                                    .SubtitleFontSize = 10
                                    .Axes.Add(New LinearAxis() With {.MajorGridlineStyle = LineStyle.Dash, .MinorGridlineStyle = LineStyle.Dot, .Position = AxisPosition.Bottom, .Title = "Entropy (" & Units.entropy & ")", .FontSize = 12})
                                    .Axes.Add(New LinearAxis() With {.MajorGridlineStyle = LineStyle.Dash, .MinorGridlineStyle = LineStyle.Dot, .Position = AxisPosition.Left, .Title = "Pressure (" & Units.pressure & ")", .FontSize = 12})
                                    .AddLineSeries(results.Data("SB").ToArray, results.Data("PB").ToArray)
                                    .AddLineSeries(results.Data("SD").ToArray, results.Data("PD").ToArray)
                                    .Series(0).Title = "Bubble Points"
                                    .Series(1).Title = "Dew Points"
                                    If PhaseEnvelopeOptions.OperatingPoint Then
                                        .AddScatterSeries(New Double() {MixEntropy.ConvertFromSI(Units.entropy)}, New Double() {MixPressure.ConvertFromSI(Units.pressure)})
                                        DirectCast(.Series(.Series.Count - 1), ScatterSeries).MarkerSize = 3
                                        .Series(.Series.Count - 1).Title = "Operating Point"
                                    End If
                                    .LegendFontSize = 10
                                    .LegendPosition = LegendPosition.TopCenter
                                    .LegendPlacement = LegendPlacement.Outside
                                    .LegendOrientation = LegendOrientation.Horizontal
                                    .TitleHorizontalAlignment = TitleHorizontalAlignment.CenteredWithinView
                                End With

                                results.PlotModels.Add(model1)

                                results.TextOutput += "Phase Envelope calculation results for " & MixName & " " & Compounds.ToArrayString & System.Environment.NewLine
                                results.TextOutput += "Property Package: " & PropertyPackageName & System.Environment.NewLine & System.Environment.NewLine
                                results.TextOutput += ("Bubble Entropy (" & Units.entropy & ")").PadRight(20) &
                                    ("Bubble Press. (" & Units.pressure & ")").PadRight(20) & System.Environment.NewLine
                                For i As Integer = 0 To results.Data("PB").Count - 1
                                    results.TextOutput += results.Data("SB")(i).ToString(results.NumberFormat).PadRight(20) & results.Data("PB")(i).ToString(results.NumberFormat).PadRight(20) & System.Environment.NewLine
                                Next
                                results.TextOutput += ("Dew Entropy (" & Units.entropy & ")").PadRight(20) & ("Dew Press. (" & Units.pressure & ")") & System.Environment.NewLine
                                For i As Integer = 0 To results.Data("PD").Count - 1
                                    results.TextOutput += results.Data("SD")(i).ToString(results.NumberFormat).PadRight(20) & results.Data("PD")(i).ToString(results.NumberFormat) & System.Environment.NewLine
                                Next

                            Case CalculationType.PhaseEnvelopeTH

                                Dim model1 As New Global.OxyPlot.PlotModel With {.Title = "Temperature/Enthalpy diagram",
                                                                                     .Subtitle = MixName & " " & Compounds.ToArrayString() & " / " & "Model: " & PropertyPackageName}

                                With model1
                                    .TitleFontSize = 14
                                    .SubtitleFontSize = 10
                                    .Axes.Add(New LinearAxis() With {.MajorGridlineStyle = LineStyle.Dash, .MinorGridlineStyle = LineStyle.Dot, .Position = AxisPosition.Bottom, .Title = "Enthalpy (" & Units.enthalpy & ")", .FontSize = 12})
                                    .Axes.Add(New LinearAxis() With {.MajorGridlineStyle = LineStyle.Dash, .MinorGridlineStyle = LineStyle.Dot, .Position = AxisPosition.Left, .Title = "Temperature (" & Units.temperature & ")", .FontSize = 12})
                                    .AddLineSeries(results.Data("HB").ToArray, results.Data("TB").ToArray)
                                    .AddLineSeries(results.Data("HD").ToArray, results.Data("TD").ToArray)
                                    .Series(0).Title = "Bubble Points"
                                    .Series(1).Title = "Dew Points"
                                    If PhaseEnvelopeOptions.OperatingPoint Then
                                        .AddScatterSeries(New Double() {MixEnthalpy.ConvertFromSI(Units.enthalpy)}, New Double() {MixTemperature.ConvertFromSI(Units.temperature)})
                                        DirectCast(.Series(.Series.Count - 1), ScatterSeries).MarkerSize = 3
                                        .Series(.Series.Count - 1).Title = "Operating Point"
                                    End If
                                    .LegendFontSize = 10
                                    .LegendPosition = LegendPosition.TopCenter
                                    .LegendPlacement = LegendPlacement.Outside
                                    .LegendOrientation = LegendOrientation.Horizontal
                                    .TitleHorizontalAlignment = TitleHorizontalAlignment.CenteredWithinView
                                End With

                                results.PlotModels.Add(model1)

                                results.TextOutput += "Phase Envelope calculation results for " & MixName & " " & Compounds.ToArrayString & System.Environment.NewLine
                                results.TextOutput += "Property Package: " & PropertyPackageName & System.Environment.NewLine & System.Environment.NewLine
                                results.TextOutput += ("Bubble Enthalpy (" & Units.enthalpy & ")").PadRight(20) &
                                    ("Bubble Temp. (" & Units.temperature & ")").PadRight(20) & System.Environment.NewLine
                                For i As Integer = 0 To results.Data("TD").Count - 1
                                    results.TextOutput += results.Data("HB")(i).ToString(results.NumberFormat).PadRight(20) & results.Data("TD")(i).ToString(results.NumberFormat) & System.Environment.NewLine
                                Next
                                results.TextOutput += ("Dew Enthalpy (" & Units.enthalpy & ")").PadRight(20) &
                                   ("Dew Temp. (" & Units.temperature & ")") & System.Environment.NewLine
                                For i As Integer = 0 To results.Data("TD").Count - 1
                                    results.TextOutput += results.Data("HD")(i).ToString(results.NumberFormat).PadRight(20) & results.Data("TD")(i).ToString(results.NumberFormat) & System.Environment.NewLine
                                Next

                            Case CalculationType.PhaseEnvelopeTS

                                Dim model1 As New Global.OxyPlot.PlotModel With {.Title = "Pressure/Entropy diagram",
                                                                                     .Subtitle = MixName & " " & Compounds.ToArrayString() & " / " & "Model: " & PropertyPackageName}

                                With model1
                                    .TitleFontSize = 14
                                    .SubtitleFontSize = 10
                                    .Axes.Add(New LinearAxis() With {.MajorGridlineStyle = LineStyle.Dash, .MinorGridlineStyle = LineStyle.Dot, .Position = AxisPosition.Bottom, .Title = "Entropy (" & Units.entropy & ")", .FontSize = 12})
                                    .Axes.Add(New LinearAxis() With {.MajorGridlineStyle = LineStyle.Dash, .MinorGridlineStyle = LineStyle.Dot, .Position = AxisPosition.Left, .Title = "Temperature (" & Units.temperature & ")", .FontSize = 12})
                                    .AddLineSeries(results.Data("SB").ToArray, results.Data("TB").ToArray)
                                    .AddLineSeries(results.Data("SD").ToArray, results.Data("TD").ToArray)
                                    .Series(0).Title = "Bubble Points"
                                    .Series(1).Title = "Dew Points"
                                    If PhaseEnvelopeOptions.OperatingPoint Then
                                        .AddScatterSeries(New Double() {MixEntropy.ConvertFromSI(Units.entropy)}, New Double() {MixTemperature.ConvertFromSI(Units.temperature)})
                                        DirectCast(.Series(.Series.Count - 1), ScatterSeries).MarkerSize = 3
                                        .Series(.Series.Count - 1).Title = "Operating Point"
                                    End If
                                    .LegendFontSize = 10
                                    .LegendPosition = LegendPosition.TopCenter
                                    .LegendPlacement = LegendPlacement.Outside
                                    .LegendOrientation = LegendOrientation.Horizontal
                                    .TitleHorizontalAlignment = TitleHorizontalAlignment.CenteredWithinView
                                End With

                                results.PlotModels.Add(model1)

                                results.TextOutput += "Phase Envelope calculation results for " & MixName & " " & Compounds.ToArrayString & System.Environment.NewLine
                                results.TextOutput += "Property Package: " & PropertyPackageName & System.Environment.NewLine & System.Environment.NewLine
                                results.TextOutput += ("Bubble Entropy (" & Units.entropy & ")").PadRight(20) &
                                    ("Bubble Temp. (" & Units.temperature & ")").PadRight(20) & System.Environment.NewLine
                                For i As Integer = 0 To results.Data("PB").Count - 1
                                    results.TextOutput += results.Data("SB")(i).ToString(results.NumberFormat).PadRight(20) & results.Data("TB")(i).ToString(results.NumberFormat).PadRight(20) & System.Environment.NewLine
                                Next
                                results.TextOutput += ("Dew Entropy (" & Units.entropy & ")").PadRight(20) &
                                 ("Dew Temp. (" & Units.temperature & ")") & System.Environment.NewLine
                                For i As Integer = 0 To results.Data("PD").Count - 1
                                    results.TextOutput += results.Data("SD")(i).ToString(results.NumberFormat).PadRight(20) & results.Data("TD")(i).ToString(results.NumberFormat) & System.Environment.NewLine
                                Next

                            Case CalculationType.PhaseEnvelopeVT

                                Dim model1 As New Global.OxyPlot.PlotModel With {.Title = "Volume/Temperature diagram",
                                                                                     .Subtitle = MixName & " " & Compounds.ToArrayString() & " / " & "Model: " & PropertyPackageName}

                                With model1
                                    .TitleFontSize = 14
                                    .SubtitleFontSize = 10
                                    .Axes.Add(New LinearAxis() With {.MajorGridlineStyle = LineStyle.Dash, .MinorGridlineStyle = LineStyle.Dot, .Position = AxisPosition.Bottom, .Title = "Temperature (" & Units.temperature & ")", .FontSize = 12})
                                    .Axes.Add(New LinearAxis() With {.MajorGridlineStyle = LineStyle.Dash, .MinorGridlineStyle = LineStyle.Dot, .Position = AxisPosition.Left, .Title = "Volume (" & Units.molar_volume & ")", .FontSize = 12})
                                    .AddLineSeries(results.Data("TB").ToArray, results.Data("VB").ToArray)
                                    .AddLineSeries(results.Data("TD").ToArray, results.Data("VD").ToArray)
                                    .AddScatterSeries(New Double() {results.Data("CP")(0)}, New Double() {results.Data("CP")(2)})
                                    .Series(0).Title = "Bubble Points"
                                    .Series(1).Title = "Dew Points"
                                    .Series(2).Title = "Critical Point"
                                    .LegendFontSize = 10
                                    .LegendPosition = LegendPosition.TopCenter
                                    .LegendPlacement = LegendPlacement.Outside
                                    .LegendOrientation = LegendOrientation.Horizontal
                                    .TitleHorizontalAlignment = TitleHorizontalAlignment.CenteredWithinView
                                End With

                                results.PlotModels.Add(model1)

                                results.TextOutput += "Phase Envelope calculation results for " & MixName & " " & Compounds.ToArrayString & System.Environment.NewLine
                                results.TextOutput += "Property Package: " & PropertyPackageName & System.Environment.NewLine & System.Environment.NewLine
                                results.TextOutput += ("Bubble Temp. (" & Units.temperature & ")").PadRight(20) &
                                    ("Bubble Volume (" & Units.molar_volume & ")").PadRight(20) & System.Environment.NewLine
                                For i As Integer = 0 To results.Data("VB").Count - 1
                                    results.TextOutput += results.Data("TB")(i).ToString(results.NumberFormat).PadRight(20) & results.Data("VB")(i).ToString(results.NumberFormat).PadRight(20) & System.Environment.NewLine
                                Next
                                results.TextOutput += ("Dew Temp. (" & Units.temperature & ")").PadRight(20) &
                                 ("Dew Volume (" & Units.molar_volume & ")") & System.Environment.NewLine
                                For i As Integer = 0 To results.Data("VD").Count - 1
                                    results.TextOutput += results.Data("TD")(i).ToString(results.NumberFormat).PadRight(20) & results.Data("VD")(i).ToString(results.NumberFormat) & System.Environment.NewLine
                                Next

                            Case CalculationType.PhaseEnvelopeVP

                                Dim model1 As New Global.OxyPlot.PlotModel With {.Title = "Volume/Pressure diagram",
                                                                                     .Subtitle = MixName & " " & Compounds.ToArrayString() & " / " & "Model: " & PropertyPackageName}

                                With model1
                                    .TitleFontSize = 14
                                    .SubtitleFontSize = 10
                                    .Axes.Add(New LinearAxis() With {.MajorGridlineStyle = LineStyle.Dash, .MinorGridlineStyle = LineStyle.Dot, .Position = AxisPosition.Bottom, .Title = "Pressure (" & Units.pressure & ")", .FontSize = 12})
                                    .Axes.Add(New LinearAxis() With {.MajorGridlineStyle = LineStyle.Dash, .MinorGridlineStyle = LineStyle.Dot, .Position = AxisPosition.Left, .Title = "Volume (" & Units.molar_volume & ")", .FontSize = 12})
                                    .AddLineSeries(results.Data("PB").ToArray, results.Data("VB").ToArray)
                                    .AddLineSeries(results.Data("PD").ToArray, results.Data("VD").ToArray)
                                    .AddScatterSeries(New Double() {results.Data("CP")(1)}, New Double() {results.Data("CP")(2)})
                                    ' DirectCast(.Series(0), LineSeries).Smooth = True
                                    ' DirectCast(.Series(1), LineSeries).Smooth = True
                                    .Series(0).Title = "Bubble Points"
                                    .Series(1).Title = "Dew Points"
                                    .Series(2).Title = "Critical Point"
                                    .LegendFontSize = 10
                                    .LegendPosition = LegendPosition.TopCenter
                                    .LegendPlacement = LegendPlacement.Outside
                                    .LegendOrientation = LegendOrientation.Horizontal
                                    .TitleHorizontalAlignment = TitleHorizontalAlignment.CenteredWithinView
                                End With

                                results.PlotModels.Add(model1)

                                results.TextOutput += "Phase Envelope calculation results for " & MixName & " " & Compounds.ToArrayString & System.Environment.NewLine
                                results.TextOutput += "Property Package: " & PropertyPackageName & System.Environment.NewLine & System.Environment.NewLine
                                results.TextOutput += ("Bubble Press. (" & Units.pressure & ")").PadRight(20) &
                                    ("Bubble Volume (" & Units.molar_volume & ")").PadRight(20) & System.Environment.NewLine
                                For i As Integer = 0 To results.Data("VB").Count - 1
                                    results.TextOutput += results.Data("PB")(i).ToString(results.NumberFormat).PadRight(20) & results.Data("VB")(i).ToString(results.NumberFormat).PadRight(20) & System.Environment.NewLine
                                Next
                                results.TextOutput += ("Dew Press. (" & Units.pressure & ")").PadRight(20) &
                                   ("Dew Volume (" & Units.molar_volume & ")") & System.Environment.NewLine
                                For i As Integer = 0 To results.Data("VD").Count - 1
                                    results.TextOutput += results.Data("PD")(i).ToString(results.NumberFormat).PadRight(20) & results.Data("VD")(i).ToString(results.NumberFormat) & System.Environment.NewLine
                                Next

                        End Select

                End Select

            Catch agex As AggregateException

                results.ExceptionResult = agex.GetBaseException

            Catch ex As Exception

                results.ExceptionResult = ex

            End Try

            Return results

        End Function

    End Class

End Namespace

