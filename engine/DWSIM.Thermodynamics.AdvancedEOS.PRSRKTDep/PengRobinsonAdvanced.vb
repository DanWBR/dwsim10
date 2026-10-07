Imports System.IO
Imports System.Windows.Forms
Imports DWSIM.Interfaces
Imports DWSIM.Interfaces.Enums
Imports DWSIM.Thermodynamics.PropertyPackages
Imports CubicDerivs = DWSIM.Thermodynamics.PropertyPackages.ThermoPlugs.CubicEOSDerivatives

Namespace DWSIM.Thermodynamics.AdvancedEOS

    <System.Serializable> Public Partial Class PengRobinson1978AdvancedPropertyPackage

        Inherits PengRobinson1978PropertyPackage

        Private TInternal As Double = 0.0
        Private PInternal As Double = 0.0

        ' T and P of the call in progress, per call flow. One instance serves several calls at once (K at T+-eps,
        ' bubble and dew together, column stages in parallel), so each kij(T) expression reads the T and P set by
        ' its own call and by nothing running beside it. TInternal/PInternal still hold the last values set on the
        ' instance, read only by a call flow that has set none.
        <NonSerialized> Private _callTP As System.Threading.AsyncLocal(Of Double())

        Private Sub SetTP(T As Double, P As Double)
            TInternal = T
            PInternal = P
            If _callTP Is Nothing Then System.Threading.Interlocked.CompareExchange(_callTP, New System.Threading.AsyncLocal(Of Double()), Nothing)
            ' a task started inside this call already carries the same pair: setting it again only costs
            Dim cur = _callTP.Value
            If cur Is Nothing OrElse BitConverter.DoubleToInt64Bits(cur(0)) <> BitConverter.DoubleToInt64Bits(T) OrElse
               BitConverter.DoubleToInt64Bits(cur(1)) <> BitConverter.DoubleToInt64Bits(P) Then
                _callTP.Value = New Double() {T, P}
            End If
        End Sub

        ''' <summary>Holds the compiled kij expressions between calls.</summary>
        <NonSerialized> Private ec As New DWSIM.SharedClasses.ExpressionCache

        Public KijExpressions As New Dictionary(Of String, String)

        Public Sub New()

            ComponentName = "Peng-Robinson 1978 (PR78) Advanced"
            ComponentDescription = "Peng-Robinson 1978 EOS with T/P-dependent Interaction Parameters"

            IsConfigurable = True

            Dim filestr As Stream = System.Reflection.Assembly.GetExecutingAssembly.GetManifestResourceStream("mercury.txt")
            Dim filedata As String

            Using t As StreamReader = New StreamReader(filestr)
                filedata = t.ReadToEnd()
            End Using

            For Each line In filedata.Split(vbCrLf)
                Dim items = line.Trim(vbLf).Trim(vbCr).Trim().Split(vbTab)
                KijExpressions.Add(items(0) + "/" + items(1), items(2))
            Next

        End Sub

        Public Overrides Function ReturnInstance(typename As String) As Object

            Return New PengRobinson1978AdvancedPropertyPackage()

        End Function

        Public Overrides Function RET_VKij() As Double(,)

            Dim tp = _callTP?.Value
            Return KijMatrix(If(tp Is Nothing, TInternal, tp(0)), If(tp Is Nothing, PInternal, tp(1)))

        End Function

        Public Overrides Function RET_VKijAt(T As Double, P As Double) As Double(,)

            Return KijMatrix(T, P)

        End Function

        Private Function KijMatrix(T As Double, P As Double) As Double(,)

            Dim vn As String() = RET_VNAMES()
            Dim n As Integer = vn.Length - 1

            Dim val(Me.CurrentMaterialStream.Phases(0).Compounds.Count - 1, Me.CurrentMaterialStream.Phases(0).Compounds.Count - 1) As Double
            Dim i As Integer = 0
            Dim l As Integer = 0

            For i = 0 To n
                For l = 0 To n
                    Dim kval = KIJ2(vn(i), vn(l), T, P)
                    If kval = 0.0 Then
                        val(i, l) = KIJ(vn(i), vn(l))
                    Else
                        val(i, l) = kval
                    End If
                Next
            Next

            Return val

        End Function

        ' d(kij)/dT of every pair, by central difference on the kij expressions; Nothing when no kij depends on T.
        Private Function KijTDerivatives(T As Double, P As Double) As Double(,)

            Const h As Double = 0.01

            Dim kp = KijMatrix(T + h, P)
            Dim km = KijMatrix(T - h, P)
            Dim n0 As Integer = kp.GetLength(0) - 1, n1 As Integer = kp.GetLength(1) - 1
            Dim d(n0, n1) As Double
            Dim tdep As Boolean = False

            For i As Integer = 0 To n0
                For j As Integer = 0 To n1
                    d(i, j) = (kp(i, j) - km(i, j)) / (2.0 * h)
                    If d(i, j) <> 0.0 Then tdep = True
                Next
            Next

            Return If(tdep, d, Nothing)

        End Function

        Public Function KIJ2(id1 As String, id2 As String) As Double

            Dim tp = _callTP?.Value
            Return KIJ2(id1, id2, If(tp Is Nothing, TInternal, tp(0)), If(tp Is Nothing, PInternal, tp(1)))

        End Function

        Public Function KIJ2(id1 As String, id2 As String, T As Double, P As Double) As Double

            SyncLock ec

                Dim context = ec.GetContext("PT")

                DWSIM.SharedClasses.ExpressionCache.SetVariable(context, "P", P)
                DWSIM.SharedClasses.ExpressionCache.SetVariable(context, "T", T)

                Dim pair As String = id1 + "/" + id2
                Dim pair2 As String = id2 + "/" + id1

                Try
                    If KijExpressions.ContainsKey(pair) Then
                        Return ec.GetCompiled("PT", KijExpressions(pair)).Evaluate()
                    ElseIf KijExpressions.ContainsKey(pair2) Then
                        Return ec.GetCompiled("PT", KijExpressions(pair2)).Evaluate()
                    Else
                        Return 0.0
                    End If
                Catch ex As Exception
                    Flowsheet?.ShowMessage(String.Format("PR/SRK Adv: Error evaluating kij expression for {0}/{1}: {2}", id1, id2, ex.Message), IFlowsheet.MessageType.GeneralError)
                    Return 0.0
                End Try

            End SyncLock

        End Function

        Public Function KIJ(ByVal id1 As String, ByVal id2 As String) As Double

            If Me.m_pr.InteractionParameters.ContainsKey(id1) Then
                If Me.m_pr.InteractionParameters(id1).ContainsKey(id2) Then
                    Return m_pr.InteractionParameters(id1)(id2).kij
                Else
                    If Me.m_pr.InteractionParameters.ContainsKey(id2) Then
                        If Me.m_pr.InteractionParameters(id2).ContainsKey(id1) Then
                            Return m_pr.InteractionParameters(id2)(id1).kij
                        Else
                            Return 0.0
                        End If
                    Else
                        Return 0.0
                    End If
                End If
            Else
                Return 0.0
            End If

        End Function

        Public Overrides Function AUX_LIQDENS(T As Double, Optional P As Double = 0, Optional Pvp As Double = 0, Optional phaseid As Integer = 3, Optional FORCE_EOS As Boolean = False) As Double
            SetTP(T, P)
            Return MyBase.AUX_LIQDENS(T, P, Pvp, phaseid, FORCE_EOS)
        End Function

        Public Overrides Function AUX_LIQDENS(T As Double, Vx As Array, Optional P As Double = 0, Optional Pvp As Double = 0, Optional FORCE_EOS As Boolean = False) As Double
            SetTP(T, P)
            Return MyBase.AUX_LIQDENS(T, Vx, P, Pvp, FORCE_EOS)
        End Function

        Public Overrides Function AUX_VAPDENS(T As Double, P As Double) As Double
            SetTP(T, P)
            Return MyBase.AUX_VAPDENS(T, P)
        End Function

        Public Overrides Function AUX_Z(Vx() As Double, T As Double, P As Double, state As PhaseName) As Double
            SetTP(T, P)
            Return MyBase.AUX_Z(Vx, T, P, state)
        End Function

        Public Overrides Function CalcSpeedOfSound(p As IPhase) As Double
            SetTP(CurrentMaterialStream.Phases(0).Properties.temperature.GetValueOrDefault, CurrentMaterialStream.Phases(0).Properties.pressure.GetValueOrDefault)
            Return MyBase.CalcSpeedOfSound(p)
        End Function

        Public Overrides Sub DW_CalcPhaseProps(Phase As Phase)
            SetTP(CurrentMaterialStream.Phases(0).Properties.temperature.GetValueOrDefault, CurrentMaterialStream.Phases(0).Properties.pressure.GetValueOrDefault)
            MyBase.DW_CalcPhaseProps(Phase)
        End Sub

        Public Overrides Sub DW_CalcCompPartialVolume(phase As Phase, T As Double, P As Double)
            SetTP(T, P)
            MyBase.DW_CalcCompPartialVolume(phase, T, P)
        End Sub

        Public Overrides Function DW_CalcCp_ISOL(Phase1 As Phase, T As Double, P As Double) As Double
            SetTP(T, P)
            Return MyBase.DW_CalcCp_ISOL(Phase1, T, P)
        End Function

        Public Overrides Function DW_CalcCv_ISOL(Phase1 As Phase, T As Double, P As Double) As Double
            SetTP(T, P)
            Return MyBase.DW_CalcCv_ISOL(Phase1, T, P)
        End Function

        Public Overrides Function DW_CalcEnthalpy(Vx As Array, T As Double, P As Double, st As State) As Double
            SetTP(T, P)
            Return MyBase.DW_CalcEnthalpy(Vx, T, P, st)
        End Function

        Public Overrides Function DW_CalcEntropy(Vx As Array, T As Double, P As Double, st As State) As Double
            SetTP(T, P)
            Return MyBase.DW_CalcEntropy(Vx, T, P, st)
        End Function

        Public Overrides Function DW_CalcFugCoeff(Vx As Array, T As Double, P As Double, st As State) As Double()
            SetTP(T, P)
            Return MyBase.DW_CalcFugCoeff(Vx, T, P, st)
        End Function

        Public Overrides Function DW_CalcKvalue(Vx As Array, T As Double, P As Double) As Double()
            SetTP(T, P)
            Return MyBase.DW_CalcKvalue(Vx, T, P)
        End Function

        Public Overrides Function DW_CalcKvalue(Vx() As Double, Vy() As Double, T As Double, P As Double, Optional type As String = "LV") As Double()
            SetTP(T, P)
            Return MyBase.DW_CalcKvalue(Vx, Vy, T, P, type)
        End Function

        Public Overrides Function DW_CalcMassaEspecifica_ISOL(Phase1 As Phase, T As Double, P As Double, Optional pvp As Double = 0) As Double
            SetTP(T, P)
            Return MyBase.DW_CalcMassaEspecifica_ISOL(Phase1, T, P, pvp)
        End Function

        ' The entries below come from the base package and read kij too, but had no T of their own here: kij(T)
        ' was evaluated at the T of whichever call set it last. Each now sets the T and P of its own call.

        Public Overrides Function DW_CalcEnthalpyDeparture(Vx As Array, T As Double, P As Double, st As State) As Double
            SetTP(T, P)
            Return MyBase.DW_CalcEnthalpyDeparture(Vx, T, P, st)
        End Function

        Public Overrides Function DW_CalcEntropyDeparture(Vx As Array, T As Double, P As Double, st As State) As Double
            SetTP(T, P)
            Return MyBase.DW_CalcEntropyDeparture(Vx, T, P, st)
        End Function

        ' The analytical derivatives of the base package hold kij constant; with kij(T) they also carry d(kij)/dT.
        Public Overrides Function DW_CalcdLnFugCoeffdT(Vx As Double(), T As Double, P As Double, st As State) As Double()
            SetTP(T, P)
            Dim dKijdT = If(AnalyticalDerivativesDisabled, Nothing, KijTDerivatives(T, P))
            If dKijdT Is Nothing Then Return MyBase.DW_CalcdLnFugCoeffdT(Vx, T, P, st)
            Dim res = CubicDerivs.Calc(CubicDerivs.EOS_PR78, T, P, Vx, KijMatrix(T, P), RET_VTC, RET_VPC, RET_VW, If(st = State.Liquid, 0, 1), dKijdT)
            Return DirectCast(res(1), Double())
        End Function

        Public Overrides Function DW_CalcdLnFugCoeffdn(Vx As Double(), T As Double, P As Double, st As State) As Double(,)
            SetTP(T, P)
            Return MyBase.DW_CalcdLnFugCoeffdn(Vx, T, P, st)
        End Function

        Public Overrides Function DW_CalcdKdT(Vx As Double(), Vy As Double(), T As Double, P As Double, Optional type As String = "LV") As Double()
            SetTP(T, P)
            Dim dKijdT = If(AnalyticalDerivativesDisabled OrElse type <> "LV", Nothing, KijTDerivatives(T, P))
            If dKijdT Is Nothing Then Return MyBase.DW_CalcdKdT(Vx, Vy, T, P, type)
            Dim kij = KijMatrix(T, P)
            Dim resL = CubicDerivs.Calc(CubicDerivs.EOS_PR78, T, P, Vx, kij, RET_VTC, RET_VPC, RET_VW, 0, dKijdT)
            Dim resV = CubicDerivs.Calc(CubicDerivs.EOS_PR78, T, P, Vy, kij, RET_VTC, RET_VPC, RET_VW, 1, dKijdT)
            Dim dLdT = DirectCast(resL(1), Double()), dVdT = DirectCast(resV(1), Double())
            Dim K As Double() = DW_CalcKvalue(Vx, Vy, T, P, type)
            Dim deriv(Vx.Length - 1) As Double
            For i As Integer = 0 To Vx.Length - 1
                deriv(i) = K(i) * (dLdT(i) - dVdT(i))
            Next
            Return deriv
        End Function

        Public Overrides Function DW_CalcdKdComposition(Vx As Double(), Vy As Double(), T As Double, P As Double, withRespectTo As State, Optional type As String = "LV") As Double(,)
            SetTP(T, P)
            Return MyBase.DW_CalcdKdComposition(Vx, Vy, T, P, withRespectTo, type)
        End Function

        Public Overrides Function DW_CalcEnergyFlowMistura_ISOL(T As Double, P As Double) As Double
            SetTP(T, P)
            Return MyBase.DW_CalcEnergyFlowMistura_ISOL(T, P)
        End Function

        Public Overrides Sub DW_CalcProp([property] As String, phase As Phase)
            SetTP(CurrentMaterialStream.Phases(0).Properties.temperature.GetValueOrDefault, CurrentMaterialStream.Phases(0).Properties.pressure.GetValueOrDefault)
            MyBase.DW_CalcProp([property], phase)
        End Sub

        ' No T of its own: the stream's, as in DW_CalcPhaseProps.
        Public Overrides Function DW_CalculateCriticalPoints() As List(Of Double())
            SetTP(CurrentMaterialStream.Phases(0).Properties.temperature.GetValueOrDefault, CurrentMaterialStream.Phases(0).Properties.pressure.GetValueOrDefault)
            Return MyBase.DW_CalculateCriticalPoints()
        End Function

        ' T and V given, no P: keep the P of the call flow (or the last one set on the instance).
        Public Overrides Function DW_CalcFugCoeff(Vz() As Double, T As Double, V As Double) As Double()
            SetTP(T, CurrentP())
            Return MyBase.DW_CalcFugCoeff(Vz, T, V)
        End Function

        Public Overrides Function DW_CalcP(Vz() As Double, T As Double, V As Double) As Double
            SetTP(T, CurrentP())
            Return MyBase.DW_CalcP(Vz, T, V)
        End Function

        Private Function CurrentP() As Double
            Dim tp = _callTP?.Value
            Return If(tp Is Nothing, PInternal, tp(1))
        End Function
        Public Overrides Function SaveData() As List(Of XElement)

            Dim elements = MyBase.SaveData()

            elements.Add(New XElement("NewInteractionParameters"))
            For Each kvp In KijExpressions
                elements((elements.Count - 1)).Add(New XElement("NewInteractionParameter", New XAttribute("Pair", kvp.Key), New XAttribute("Value", kvp.Value)))
            Next

            Return elements

        End Function

        Public Overrides Function LoadData(data As List(Of XElement)) As Boolean

            KijExpressions = New Dictionary(Of String, String)

            For Each xel As XElement In (From xel2 In data Where xel2.Name = "NewInteractionParameters" Select xel2).SingleOrDefault().Elements().ToList()
                KijExpressions.Add(xel.Attribute("Pair").Value, xel.Attribute("Value").Value)
            Next

            Return MyBase.LoadData(data)

        End Function

    End Class

End Namespace
