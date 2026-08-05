Imports DWSIM.SharedClasses.DataRegression.Models

Namespace Global.DWSIM.SharedClasses.DataRegression.Engine

    ''' <summary>
    ''' Experimental data points unpacked from a RegressionCase, with units converted
    ''' to SI. Mirrors the per-DataType marshalling that previously lived inline in
    ''' FormDataRegression.FunctionValue/UpdateData.
    ''' </summary>
    Public Class InputVectors
        Public Property Vx1 As New ArrayList
        Public Property Vx2 As New ArrayList
        Public Property Vy As New ArrayList
        Public Property VP As New ArrayList
        Public Property VT As New ArrayList
        Public Property VTL As New ArrayList
        Public Property VTS As New ArrayList

        Public Property Vx1c As New ArrayList
        Public Property Vx2c As New ArrayList
        Public Property Vyc As New ArrayList
        Public Property VPc As New ArrayList
        Public Property VTc As New ArrayList
        Public Property VTLc As New ArrayList
        Public Property VTSc As New ArrayList

        Public Property Np As Integer
        ''' <summary>True for Txy data: bubble-T flash with VP fixed.</summary>
        Public Property PVF As Boolean
    End Class

    Public Module InputUnpacker

        Public Function Unpack(c As RegressionCase) As InputVectors

            Dim r As New InputVectors

            For Each b As Boolean In c.checkp
                If b Then r.Np += 1
            Next

            For i = 0 To r.Np - 1
                r.Vx1c.Add(0.0#)
                r.Vx2c.Add(0.0#)
                r.Vyc.Add(0.0#)
                r.VPc.Add(0.0#)
                r.VTc.Add(0.0#)
                r.VTLc.Add(0.0#)
                r.VTSc.Add(0.0#)
            Next

            Select Case c.datatype
                Case DataType.Pxy
                    ForEachActivePoint(c, Sub(i)
                                              r.Vx1.Add(c.x1p(i))
                                              r.Vy.Add(c.yp(i))
                                              r.VP.Add(SystemsOfUnits.Converter.ConvertToSI(c.punit, c.pp(i)))
                                              r.VT.Add(SystemsOfUnits.Converter.ConvertToSI(c.tunit, c.tp(0)))
                                          End Sub)
                Case DataType.Txy
                    ForEachActivePoint(c, Sub(i)
                                              r.Vx1.Add(c.x1p(i))
                                              r.Vy.Add(c.yp(i))
                                              r.VP.Add(SystemsOfUnits.Converter.ConvertToSI(c.punit, c.pp(0)))
                                              r.VT.Add(SystemsOfUnits.Converter.ConvertToSI(c.tunit, c.tp(i)))
                                          End Sub)
                    r.PVF = True
                Case DataType.TPxy
                    ForEachActivePoint(c, Sub(i)
                                              r.Vx1.Add(c.x1p(i))
                                              r.Vy.Add(c.yp(i))
                                              r.VP.Add(SystemsOfUnits.Converter.ConvertToSI(c.punit, c.pp(i)))
                                              r.VT.Add(SystemsOfUnits.Converter.ConvertToSI(c.tunit, c.tp(i)))
                                          End Sub)
                Case DataType.Pxx
                    ForEachActivePoint(c, Sub(i)
                                              r.Vx1.Add(c.x1p(i))
                                              r.Vx2.Add(c.x2p(i))
                                              r.VP.Add(SystemsOfUnits.Converter.ConvertToSI(c.punit, c.pp(i)))
                                              r.VT.Add(SystemsOfUnits.Converter.ConvertToSI(c.tunit, c.tp(0)))
                                          End Sub)
                Case DataType.Txx
                    ForEachActivePoint(c, Sub(i)
                                              r.Vx1.Add(c.x1p(i))
                                              r.Vx2.Add(c.x2p(i))
                                              r.VP.Add(SystemsOfUnits.Converter.ConvertToSI(c.punit, c.pp(0)))
                                              r.VT.Add(SystemsOfUnits.Converter.ConvertToSI(c.tunit, c.tp(i)))
                                          End Sub)
                Case DataType.TPxx
                    ForEachActivePoint(c, Sub(i)
                                              r.Vx1.Add(c.x1p(i))
                                              r.Vx2.Add(c.x2p(i))
                                              r.VP.Add(SystemsOfUnits.Converter.ConvertToSI(c.punit, c.pp(i)))
                                              r.VT.Add(SystemsOfUnits.Converter.ConvertToSI(c.tunit, c.tp(i)))
                                          End Sub)
                Case DataType.TTxSE, DataType.TTxSS
                    ForEachActivePoint(c, Sub(i)
                                              r.Vx1.Add(c.x1p(i))
                                              r.VP.Add(SystemsOfUnits.Converter.ConvertToSI(c.punit, c.pp(0)))
                                              r.VTL.Add(SystemsOfUnits.Converter.ConvertToSI(c.tunit, c.tl(i)))
                                              r.VTS.Add(SystemsOfUnits.Converter.ConvertToSI(c.tunit, c.ts(i)))
                                          End Sub)
            End Select

            Return r
        End Function

        Private Sub ForEachActivePoint(c As RegressionCase, action As Action(Of Integer))
            Dim i As Integer = 0
            For Each b As Boolean In c.checkp
                If b Then action(i)
                i += 1
            Next
        End Sub

    End Module

End Namespace
