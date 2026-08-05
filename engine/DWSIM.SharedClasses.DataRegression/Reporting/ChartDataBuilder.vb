Imports DWSIM.SharedClasses.DataRegression.Models

Namespace Global.DWSIM.SharedClasses.DataRegression.Reporting

    ''' <summary>
    ''' Builds RegressionChartData from a RegressionCase post-evaluation. The
    ''' per-datatype field assignments (axis titles, curve series mapping,
    ''' style codes) move here from the form's UpdateData.
    ''' </summary>
    Public Module ChartDataBuilder

        Public Function Build(c As RegressionCase, titlePrefix As String) As RegressionChartData
            Dim r As New RegressionChartData With {
                .DataType = c.datatype,
                .Title = titlePrefix & " / " & c.datatype.ToString()
            }

            Select Case c.datatype
                Case DataType.Txy
                    ForEachActivePoint(c, Sub(i, j)
                                              r.Px.Add(Double.Parse(c.x1p(i)))
                                              r.Py1.Add(Double.Parse(c.tp(i)))
                                              r.Py2.Add(SystemsOfUnits.Converter.ConvertFromSI(c.tunit, c.calct(j)))
                                              r.Py4.Add(SystemsOfUnits.Converter.ConvertFromSI(c.tunit, c.calct(j)))
                                              r.Px2.Add(Double.Parse(c.yp(i)))
                                              r.Py3.Add(Double.Parse(c.tp(i)))
                                              r.Py5.Add(Double.Parse(c.calcy(j)))
                                          End Sub)
                    r.XAxisTitle = "Liquid Phase Mole Fraction " & c.comp1
                    r.YAxisTitle = "T / " & c.tunit
                    r.SecondaryYAxisTitle = "Vapor Phase Mole Fraction " & c.comp1
                    r.Y1Title = "Tx exp." : r.Y2Title = "Tx calc."
                    r.Y3Title = "Ty exp." : r.Y4Title = "Ty calc."
                    r.Y5Title = "y exp." : r.Y6Title = "y calc."
                    r.CurveStyles.AddRange({CurveStyle.PointsOnly, CurveStyle.LineOnly,
                                             CurveStyle.PointsOnly, CurveStyle.LineOnly,
                                             CurveStyle.PointsOnly, CurveStyle.LineOnly})
                Case DataType.Pxy
                    ForEachActivePoint(c, Sub(i, j)
                                              r.Px.Add(Double.Parse(c.x1p(i)))
                                              r.Py1.Add(Double.Parse(c.pp(i)))
                                              r.Py2.Add(SystemsOfUnits.Converter.ConvertFromSI(c.punit, c.calcp(j)))
                                              r.Py4.Add(SystemsOfUnits.Converter.ConvertFromSI(c.punit, c.calcp(j)))
                                              r.Px2.Add(Double.Parse(c.yp(i)))
                                              r.Py3.Add(Double.Parse(c.pp(i)))
                                              r.Py5.Add(Double.Parse(c.calcy(j)))
                                          End Sub)
                    r.XAxisTitle = "Liquid Phase Mole Fraction " & c.comp1
                    r.YAxisTitle = "P / " & c.punit
                    r.SecondaryYAxisTitle = "Vapor Phase Mole Fraction " & c.comp1
                    r.Y1Title = "Px exp." : r.Y2Title = "Px calc."
                    r.Y3Title = "Py exp." : r.Y4Title = "Py calc."
                    r.Y5Title = "y exp." : r.Y6Title = "y calc."
                    r.CurveStyles.AddRange({CurveStyle.PointsOnly, CurveStyle.LineOnly,
                                             CurveStyle.PointsOnly, CurveStyle.LineOnly,
                                             CurveStyle.PointsOnly, CurveStyle.LineOnly})
                Case DataType.TPxy
                    ForEachActivePoint(c, Sub(i, j)
                                              r.Px.Add(Double.Parse(c.x1p(i)))
                                              r.Py1.Add(Double.Parse(c.tp(i)))
                                              r.Py2.Add(SystemsOfUnits.Converter.ConvertFromSI(c.tunit, c.calct(j)))
                                              r.Py4.Add(SystemsOfUnits.Converter.ConvertFromSI(c.punit, c.calcp(j)))
                                              r.Py3.Add(Double.Parse(c.pp(i)))
                                              r.Py5.Add(Double.Parse(c.calcy(j)))
                                          End Sub)
                    r.XAxisTitle = "Liquid Phase Mole Fraction " & c.comp1
                    r.YAxisTitle = "T / " & c.tunit & " - P / " & c.punit
                    r.SecondaryYAxisTitle = "Vapor Phase Mole Fraction " & c.comp1
                    r.Y5Title = "y exp." : r.Y6Title = "y calc."
                    r.CurveStyles.AddRange({CurveStyle.PointsOnly, CurveStyle.LineOnly,
                                             CurveStyle.PointsOnly, CurveStyle.LineOnly,
                                             CurveStyle.PointsOnly, CurveStyle.LineOnly})
                Case DataType.Txx
                    ForEachActivePoint(c, Sub(i, j)
                                              r.Px.Add(Double.Parse(c.x1p(i))) : r.Py1.Add(Double.Parse(c.tp(i)))
                                              r.Px2.Add(Double.Parse(c.x2p(i))) : r.Py2.Add(Double.Parse(c.tp(i)))
                                              r.Px3.Add(Double.Parse(c.calcx1l1(j))) : r.Py3.Add(Double.Parse(c.tp(i)))
                                              r.Px4.Add(Double.Parse(c.calcx1l2(j))) : r.Py4.Add(Double.Parse(c.tp(i)))
                                          End Sub)
                    r.XAxisTitle = "Mole Fraction " & c.comp1
                    r.YAxisTitle = "T / " & c.tunit
                    r.Y1Title = "Tx1' exp." : r.Y3Title = "Tx1' calc."
                    r.Y2Title = "Tx1'' exp." : r.Y4Title = "Tx1'' calc."
                    r.CurveStyles.AddRange({CurveStyle.PointsOnly, CurveStyle.PointsOnly,
                                             CurveStyle.LineOnly, CurveStyle.LineOnly})
                Case DataType.Pxx
                    ForEachActivePoint(c, Sub(i, j)
                                              r.Px.Add(Double.Parse(c.x1p(i))) : r.Py1.Add(Double.Parse(c.pp(i)))
                                              r.Px2.Add(Double.Parse(c.x2p(i))) : r.Py2.Add(Double.Parse(c.pp(i)))
                                              r.Px3.Add(Double.Parse(c.calcx1l1(j))) : r.Py3.Add(Double.Parse(c.pp(i)))
                                              r.Px4.Add(Double.Parse(c.calcx1l2(j))) : r.Py4.Add(Double.Parse(c.pp(i)))
                                          End Sub)
                    r.XAxisTitle = "Mole Fraction " & c.comp1
                    ' NOTE: legacy used .tunit here - preserved verbatim (likely a typo in
                    ' the original; should be .punit for a P-x-x plot).
                    r.YAxisTitle = "P / " & c.tunit
                    r.Y1Title = "Px1' exp." : r.Y3Title = "Px1' calc."
                    r.Y2Title = "Px1'' exp." : r.Y4Title = "Px1'' calc."
                    r.CurveStyles.AddRange({CurveStyle.PointsOnly, CurveStyle.PointsOnly,
                                             CurveStyle.LineOnly, CurveStyle.LineOnly})
                Case DataType.TPxx
                    ForEachActivePoint(c, Sub(i, j)
                                              r.Px.Add(Double.Parse(c.x1p(i))) : r.Py1.Add(Double.Parse(c.tp(i)))
                                              r.Px2.Add(Double.Parse(c.x2p(i))) : r.Py2.Add(Double.Parse(c.tp(i)))
                                              r.Px3.Add(Double.Parse(c.calcx1l1(j))) : r.Py3.Add(Double.Parse(c.tp(i)))
                                              r.Px4.Add(Double.Parse(c.calcx1l2(j))) : r.Py4.Add(Double.Parse(c.tp(i)))
                                          End Sub)
                    r.XAxisTitle = "Mole Fraction " & c.comp1
                    r.YAxisTitle = "T / " & c.tunit & " - P / " & c.punit
                    r.Y1Title = "Tx1' exp." : r.Y3Title = "Tx1' calc."
                    r.Y2Title = "Tx1'' exp." : r.Y4Title = "Tx1'' calc."
                    r.CurveStyles.AddRange({CurveStyle.PointsOnly, CurveStyle.PointsOnly,
                                             CurveStyle.LineOnly, CurveStyle.LineOnly})
                Case DataType.TTxSE, DataType.TTxSS
                    ForEachActivePoint(c, Sub(i, j)
                                              r.Px.Add(Double.Parse(c.x1p(i))) : r.Py1.Add(Double.Parse(c.tl(i)))
                                              r.Px2.Add(Double.Parse(c.x1p(i))) : r.Py2.Add(Double.Parse(c.ts(i)))
                                              r.Px3.Add(Double.Parse(c.x1p(i))) : r.Py3.Add(Double.Parse(c.calctl(j)))
                                              r.Px4.Add(Double.Parse(c.x1p(i))) : r.Py4.Add(Double.Parse(c.calcts(j)))
                                          End Sub)
                    r.XAxisTitle = "Mole Fraction " & c.comp1
                    r.YAxisTitle = "T / " & c.tunit
                    r.Y1Title = "TL exp." : r.Y3Title = "TL calc."
                    r.Y2Title = "TS exp." : r.Y4Title = "TS calc."
                    r.CurveStyles.AddRange({CurveStyle.PointsOnly, CurveStyle.PointsOnly,
                                             CurveStyle.LineOnly, CurveStyle.LineOnly})
            End Select

            Return r
        End Function

        Private Sub ForEachActivePoint(c As RegressionCase, action As Action(Of Integer, Integer))
            Dim i As Integer = 0
            Dim j As Integer = 0
            For Each b As Boolean In c.checkp
                If b Then
                    Try
                        action(i, j)
                    Catch
                        ' Match legacy "swallow per-point parsing failures" behavior.
                    End Try
                    j += 1
                End If
                i += 1
            Next
        End Sub

    End Module

End Namespace
