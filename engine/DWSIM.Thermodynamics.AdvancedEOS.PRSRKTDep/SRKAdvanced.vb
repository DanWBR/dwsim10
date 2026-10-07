Imports System.Windows.Forms
Imports DWSIM.Interfaces
Imports DWSIM.Interfaces.Enums
Imports DWSIM.Thermodynamics.PropertyPackages

Namespace DWSIM.Thermodynamics.AdvancedEOS

    <System.Serializable> Public Partial Class SoaveRedlichKwongAdvancedPropertyPackage

        Inherits SRKPropertyPackage

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

            ComponentName = "Soave-Redlich-Kwong (SRK) Advanced"
            ComponentDescription = "Soave-Redlich-Kwong EOS with T/P-dependent Interaction Parameters"

            IsConfigurable = True

        End Sub

        Public Overrides Function ReturnInstance(typename As String) As Object

            Return New SoaveRedlichKwongAdvancedPropertyPackage()

        End Function

        Public Overrides Function RET_VKij() As Double(,)

            Dim vn As String() = RET_VNAMES()
            Dim n As Integer = vn.Length - 1

            Dim val(Me.CurrentMaterialStream.Phases(0).Compounds.Count - 1, Me.CurrentMaterialStream.Phases(0).Compounds.Count - 1) As Double
            Dim i As Integer = 0
            Dim l As Integer = 0

            For i = 0 To n
                For l = 0 To n
                    Dim kval = KIJ2(vn(i), vn(l))
                    If kval = 0.0 Then
                        val(i, l) = KIJ(vn(i), vn(l))
                    Else
                        val(i, l) = kval
                    End If
                Next
            Next

            Return val

        End Function

        Public Function KIJ2(id1 As String, id2 As String) As Double

            SyncLock ec

                Dim context = ec.GetContext("PT")

                Dim tp = _callTP?.Value
                DWSIM.SharedClasses.ExpressionCache.SetVariable(context, "P", If(tp Is Nothing, PInternal, tp(1)))
                DWSIM.SharedClasses.ExpressionCache.SetVariable(context, "T", If(tp Is Nothing, TInternal, tp(0)))

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
