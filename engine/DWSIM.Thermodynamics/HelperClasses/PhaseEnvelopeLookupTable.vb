Imports System.Linq
Imports System.Xml.Linq
Imports System.Xml.Serialization

Namespace PropertyPackages

    Public Enum PhaseRegion
        Liquid = 0
        Vapor = 1
        VaporLiquid = 2
        LiquidLike = 3
        VaporLike = 4
        Solid = 5
        SolidLiquid = 6
        Unknown = 7
    End Enum

    Public Enum PhaseEnvelopeLookupMode
        WidomOnly = 0
        FullEnvelope = 1
    End Enum

    <Serializable>
    Public Class PhaseEnvelopeLookupTable
        Implements Interfaces.ICustomXMLSerialization

        Public Property BubbleCurveT As List(Of Double)
        Public Property BubbleCurveP As List(Of Double)
        Public Property BubbleCurveH As List(Of Double)
        Public Property BubbleCurveS As List(Of Double)
        Public Property DewCurveT As List(Of Double)
        Public Property DewCurveP As List(Of Double)
        Public Property DewCurveH As List(Of Double)
        Public Property DewCurveS As List(Of Double)
        Public Property WidomAvgT As List(Of Double)
        Public Property WidomAvgP As List(Of Double)
        Public Property SLE1_T As List(Of Double)
        Public Property SLE1_P As List(Of Double)
        Public Property SLE2_T As List(Of Double)
        Public Property SLE2_P As List(Of Double)

        Public Property CriticalT As Double
        Public Property CriticalP As Double

        Public Property CompositionHash As Double()

        Public Property IsReady As Boolean = False
        <XmlIgnore> Public Property IsBuilding As Boolean = False
        Public Property Mode As PhaseEnvelopeLookupMode = PhaseEnvelopeLookupMode.WidomOnly

        Public Sub New()
            BubbleCurveT = New List(Of Double)
            BubbleCurveP = New List(Of Double)
            BubbleCurveH = New List(Of Double)
            BubbleCurveS = New List(Of Double)
            DewCurveT = New List(Of Double)
            DewCurveP = New List(Of Double)
            DewCurveH = New List(Of Double)
            DewCurveS = New List(Of Double)
            WidomAvgT = New List(Of Double)
            WidomAvgP = New List(Of Double)
            SLE1_T = New List(Of Double)
            SLE1_P = New List(Of Double)
            SLE2_T = New List(Of Double)
            SLE2_P = New List(Of Double)
        End Sub

        Public Function SaveData() As List(Of XElement) Implements Interfaces.ICustomXMLSerialization.SaveData
            Return XMLSerializer.XMLSerializer.Serialize(Me)
        End Function

        Public Function LoadData(data As List(Of XElement)) As Boolean Implements Interfaces.ICustomXMLSerialization.LoadData
            XMLSerializer.XMLSerializer.Deserialize(Me, data)
            Return True
        End Function

        Public Function CompositionMatches(Vz As Double(), Optional tol As Double = 0.001) As Boolean
            If CompositionHash Is Nothing OrElse Vz Is Nothing Then Return False
            If CompositionHash.Length <> Vz.Length Then Return False
            For i = 0 To Vz.Length - 1
                If Math.Abs(CompositionHash(i) - Vz(i)) > tol Then Return False
            Next
            Return True
        End Function

        Public Sub BuildFromEnvelopeResult(envResult As Object(), Tc As Double, Pc As Double)

            CriticalT = Tc
            CriticalP = Pc

            ' Extract critical point from CP array (index 15) if available
            Dim cpArr = TryCast(envResult(15), System.Collections.ArrayList)
            If cpArr IsNot Nothing AndAlso cpArr.Count > 0 Then
                Dim cp0 = DirectCast(cpArr(0), Object())
                CriticalT = CDbl(cp0(0))
                CriticalP = CDbl(cp0(1))
            End If

            ' Bubble curve: indices 0=TVB, 1=PB, 2=HB, 3=SB
            CopySortedByP(TryCast(envResult(0), List(Of Double)),
                          TryCast(envResult(1), List(Of Double)),
                          TryCast(envResult(2), List(Of Double)),
                          TryCast(envResult(3), List(Of Double)),
                          BubbleCurveT, BubbleCurveP, BubbleCurveH, BubbleCurveS)

            ' Dew curve: indices 5=TVD, 6=PO, 7=HO, 8=SO
            CopySortedByP(TryCast(envResult(5), List(Of Double)),
                          TryCast(envResult(6), List(Of Double)),
                          TryCast(envResult(7), List(Of Double)),
                          TryCast(envResult(8), List(Of Double)),
                          DewCurveT, DewCurveP, DewCurveH, DewCurveS)

            ' Widom avg: indices 43=TWidomAvg, 44=PWidomAvg
            If envResult.Length > 44 Then
                CopySortedByP(TryCast(envResult(43), List(Of Double)),
                              TryCast(envResult(44), List(Of Double)),
                              WidomAvgT, WidomAvgP)
            End If

            ' SLE curves: indices 35-38
            If envResult.Length > 38 Then
                CopySortedByP(TryCast(envResult(35), List(Of Double)),
                              TryCast(envResult(36), List(Of Double)),
                              SLE1_T, SLE1_P)
                CopySortedByP(TryCast(envResult(37), List(Of Double)),
                              TryCast(envResult(38), List(Of Double)),
                              SLE2_T, SLE2_P)
            End If

            IsBuilding = False
            IsReady = (BubbleCurveT.Count >= 2 AndAlso DewCurveT.Count >= 2)

        End Sub

        Public Function Query(T As Double, P As Double) As PhaseRegion
            If Not IsReady Then Return PhaseRegion.Unknown

            ' 1. SLE check
            If SLE1_T.Count >= 2 Then
                Dim Tsol = InterpolateTAtP(SLE1_T, SLE1_P, P)
                If Not Double.IsNaN(Tsol) Then
                    If T < Tsol Then Return PhaseRegion.Solid
                    If SLE2_T.Count >= 2 Then
                        Dim Tliq = InterpolateTAtP(SLE2_T, SLE2_P, P)
                        If Not Double.IsNaN(Tliq) AndAlso T <= Tliq Then
                            Return PhaseRegion.SolidLiquid
                        End If
                    End If
                End If
            End If

            ' 2. VLE envelope check (ray-casting point-in-polygon)
            If IsInsideVLEEnvelope(T, P) Then
                Return PhaseRegion.VaporLiquid
            End If

            ' 3. Supercritical / Widom check
            If P >= CriticalP AndAlso WidomAvgT.Count >= 2 Then
                Dim Twidom = InterpolateTAtP(WidomAvgT, WidomAvgP, P)
                If Not Double.IsNaN(Twidom) Then
                    If T <= Twidom Then Return PhaseRegion.LiquidLike
                    Return PhaseRegion.VaporLike
                End If
            End If

            ' 4. Subcritical, outside VLE
            If T <= CriticalT Then
                Return PhaseRegion.Liquid
            Else
                Return PhaseRegion.Vapor
            End If

        End Function

        Public Function EstimateTemperaturePH(P As Double, H As Double) As Double
            If Not IsReady Then Return Double.NaN

            Dim bestT = Double.NaN
            Dim bestErr = Double.MaxValue

            ' Search bubble curve
            Dim t1 = InterpolateTAtPAndValue(BubbleCurveT, BubbleCurveP, BubbleCurveH, P, H)
            If Not Double.IsNaN(t1) Then Return t1

            ' Search dew curve
            Dim t2 = InterpolateTAtPAndValue(DewCurveT, DewCurveP, DewCurveH, P, H)
            If Not Double.IsNaN(t2) Then Return t2

            ' Interpolate between bubble and dew at this pressure
            Dim Tbub = InterpolateTAtP(BubbleCurveT, BubbleCurveP, P)
            Dim Tdew = InterpolateTAtP(DewCurveT, DewCurveP, P)
            Dim Hbub = InterpolateValueAtP(BubbleCurveH, BubbleCurveP, P)
            Dim Hdew = InterpolateValueAtP(DewCurveH, DewCurveP, P)

            If Not Double.IsNaN(Hbub) AndAlso Not Double.IsNaN(Hdew) Then
                If (H >= Math.Min(Hbub, Hdew)) AndAlso (H <= Math.Max(Hbub, Hdew)) Then
                    ' Inside two-phase region: interpolate linearly between bubble and dew T
                    If Math.Abs(Hdew - Hbub) > 1.0E-10 Then
                        Dim frac = (H - Hbub) / (Hdew - Hbub)
                        Return Tbub + frac * (Tdew - Tbub)
                    End If
                End If
                ' Outside VLE: use the closest boundary as estimate
                If Math.Abs(H - Hbub) < Math.Abs(H - Hdew) Then
                    Return Tbub
                Else
                    Return Tdew
                End If
            End If

            Return Double.NaN
        End Function

        Public Function EstimateTemperaturePS(P As Double, S As Double) As Double
            If Not IsReady Then Return Double.NaN

            ' Search bubble curve
            Dim t1 = InterpolateTAtPAndValue(BubbleCurveT, BubbleCurveP, BubbleCurveS, P, S)
            If Not Double.IsNaN(t1) Then Return t1

            ' Search dew curve
            Dim t2 = InterpolateTAtPAndValue(DewCurveT, DewCurveP, DewCurveS, P, S)
            If Not Double.IsNaN(t2) Then Return t2

            ' Interpolate between bubble and dew at this pressure
            Dim Tbub = InterpolateTAtP(BubbleCurveT, BubbleCurveP, P)
            Dim Tdew = InterpolateTAtP(DewCurveT, DewCurveP, P)
            Dim Sbub = InterpolateValueAtP(BubbleCurveS, BubbleCurveP, P)
            Dim Sdew = InterpolateValueAtP(DewCurveS, DewCurveP, P)

            If Not Double.IsNaN(Sbub) AndAlso Not Double.IsNaN(Sdew) Then
                If (S >= Math.Min(Sbub, Sdew)) AndAlso (S <= Math.Max(Sbub, Sdew)) Then
                    If Math.Abs(Sdew - Sbub) > 1.0E-10 Then
                        Dim frac = (S - Sbub) / (Sdew - Sbub)
                        Return Tbub + frac * (Tdew - Tbub)
                    End If
                End If
                If Math.Abs(S - Sbub) < Math.Abs(S - Sdew) Then
                    Return Tbub
                Else
                    Return Tdew
                End If
            End If

            Return Double.NaN
        End Function

        Public Sub Clear()
            BubbleCurveT.Clear()
            BubbleCurveP.Clear()
            BubbleCurveH.Clear()
            BubbleCurveS.Clear()
            DewCurveT.Clear()
            DewCurveP.Clear()
            DewCurveH.Clear()
            DewCurveS.Clear()
            WidomAvgT.Clear()
            WidomAvgP.Clear()
            SLE1_T.Clear()
            SLE1_P.Clear()
            SLE2_T.Clear()
            SLE2_P.Clear()
            IsReady = False
        End Sub

        Private Function IsInsideVLEEnvelope(T As Double, P As Double) As Boolean
            ' Build closed polygon: bubble curve + reversed dew curve
            Dim n = BubbleCurveT.Count + DewCurveT.Count
            If n < 4 Then Return False

            Dim polyT As New List(Of Double)(n)
            Dim polyP As New List(Of Double)(n)

            ' Bubble curve (already sorted by P ascending)
            polyT.AddRange(BubbleCurveT)
            polyP.AddRange(BubbleCurveP)

            ' Dew curve reversed (P descending)
            For i = DewCurveT.Count - 1 To 0 Step -1
                polyT.Add(DewCurveT(i))
                polyP.Add(DewCurveP(i))
            Next

            ' Ray-casting algorithm
            Dim inside = False
            Dim j = polyT.Count - 1
            For i = 0 To polyT.Count - 1
                Dim yi = polyP(i)
                Dim yj = polyP(j)
                If (yi > P) <> (yj > P) Then
                    Dim xIntersect = polyT(i) + (P - yi) / (yj - yi) * (polyT(j) - polyT(i))
                    If T < xIntersect Then
                        inside = Not inside
                    End If
                End If
                j = i
            Next
            Return inside
        End Function

        Private Function InterpolateTAtP(curveT As List(Of Double),
                                          curveP As List(Of Double),
                                          P As Double) As Double
            If curveT.Count < 2 Then Return Double.NaN
            If P < curveP(0) OrElse P > curveP(curveP.Count - 1) Then Return Double.NaN

            For i = 0 To curveP.Count - 2
                If P >= curveP(i) AndAlso P <= curveP(i + 1) Then
                    If Math.Abs(curveP(i + 1) - curveP(i)) < 1.0E-10 Then
                        Return (curveT(i) + curveT(i + 1)) / 2.0
                    End If
                    Dim frac = (P - curveP(i)) / (curveP(i + 1) - curveP(i))
                    Return curveT(i) + frac * (curveT(i + 1) - curveT(i))
                End If
            Next
            Return Double.NaN
        End Function

        Private Function InterpolateValueAtP(curveV As List(Of Double),
                                              curveP As List(Of Double),
                                              P As Double) As Double
            If curveV Is Nothing OrElse curveV.Count < 2 Then Return Double.NaN
            If P < curveP(0) OrElse P > curveP(curveP.Count - 1) Then Return Double.NaN

            For i = 0 To curveP.Count - 2
                If P >= curveP(i) AndAlso P <= curveP(i + 1) Then
                    If Math.Abs(curveP(i + 1) - curveP(i)) < 1.0E-10 Then
                        Return (curveV(i) + curveV(i + 1)) / 2.0
                    End If
                    Dim frac = (P - curveP(i)) / (curveP(i + 1) - curveP(i))
                    Return curveV(i) + frac * (curveV(i + 1) - curveV(i))
                End If
            Next
            Return Double.NaN
        End Function

        Private Function InterpolateTAtPAndValue(curveT As List(Of Double),
                                                  curveP As List(Of Double),
                                                  curveV As List(Of Double),
                                                  P As Double, V As Double) As Double
            If curveT.Count < 2 OrElse curveV Is Nothing OrElse curveV.Count < 2 Then Return Double.NaN
            If P < curveP(0) OrElse P > curveP(curveP.Count - 1) Then Return Double.NaN

            ' Find segment at pressure P, then check if V is bracketed
            For i = 0 To curveP.Count - 2
                If P >= curveP(i) AndAlso P <= curveP(i + 1) Then
                    If Math.Abs(curveP(i + 1) - curveP(i)) < 1.0E-10 Then
                        ' Constant pressure segment: interpolate by value
                        If Math.Abs(curveV(i + 1) - curveV(i)) < 1.0E-10 Then
                            Return (curveT(i) + curveT(i + 1)) / 2.0
                        End If
                        Dim fv = (V - curveV(i)) / (curveV(i + 1) - curveV(i))
                        If fv >= -0.1 AndAlso fv <= 1.1 Then
                            Return curveT(i) + fv * (curveT(i + 1) - curveT(i))
                        End If
                    Else
                        ' Interpolate V and T at this P
                        Dim fp = (P - curveP(i)) / (curveP(i + 1) - curveP(i))
                        Dim Vi = curveV(i) + fp * (curveV(i + 1) - curveV(i))
                        Dim Ti = curveT(i) + fp * (curveT(i + 1) - curveT(i))
                        ' Check how close V is to the interpolated value
                        Dim Vrange = Math.Max(Math.Abs(curveV(i)), Math.Abs(curveV(i + 1)))
                        If Vrange < 1.0E-10 Then Vrange = 1.0
                        If Math.Abs(V - Vi) / Vrange < 0.3 Then
                            Return Ti
                        End If
                    End If
                End If
            Next
            Return Double.NaN
        End Function

        Private Sub CopySortedByP(srcT As List(Of Double), srcP As List(Of Double),
                                   dstT As List(Of Double), dstP As List(Of Double))
            dstT.Clear()
            dstP.Clear()
            If srcT Is Nothing OrElse srcP Is Nothing OrElse srcT.Count = 0 Then Return

            Dim indices = Enumerable.Range(0, srcT.Count).OrderBy(Function(idx) srcP(idx)).ToList()
            For Each idx In indices
                dstP.Add(srcP(idx))
                dstT.Add(srcT(idx))
            Next
        End Sub

        Private Sub CopySortedByP(srcT As List(Of Double), srcP As List(Of Double),
                                   srcH As List(Of Double), srcS As List(Of Double),
                                   dstT As List(Of Double), dstP As List(Of Double),
                                   dstH As List(Of Double), dstS As List(Of Double))
            dstT.Clear()
            dstP.Clear()
            dstH.Clear()
            dstS.Clear()
            If srcT Is Nothing OrElse srcP Is Nothing OrElse srcT.Count = 0 Then Return

            Dim n = srcT.Count
            Dim hasH = (srcH IsNot Nothing AndAlso srcH.Count = n)
            Dim hasS = (srcS IsNot Nothing AndAlso srcS.Count = n)

            Dim indices = Enumerable.Range(0, n).OrderBy(Function(idx) srcP(idx)).ToList()
            For Each idx In indices
                dstP.Add(srcP(idx))
                dstT.Add(srcT(idx))
                If hasH Then dstH.Add(srcH(idx))
                If hasS Then dstS.Add(srcS(idx))
            Next
        End Sub

    End Class

End Namespace
