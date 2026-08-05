Imports System.Threading.Tasks
Imports DotNumerics
Imports DotNumerics.Optimization
Imports DWSIM.Thermodynamics.BaseClasses
Imports DWSIM.Thermodynamics.PropertyPackages
Imports DWSIM.Thermodynamics.Streams

Namespace Global.DWSIM.SharedClasses.DataRegression.Estimation

    ''' <summary>
    ''' Initial-guess estimators for binary activity-coefficient model parameters
    ''' (NRTL, UNIQUAC, Wilson) by fitting against UNIFAC-family group-contribution
    ''' predictions at three fixed compositions (x1 = 0.25, 0.5, 0.75).
    ''' </summary>
    Public NotInheritable Class ParameterEstimator

        Private Sub New()
        End Sub

        ''' <summary>
        ''' Returns {A12, A21, alpha12} where alpha12 is fixed at 0.2 (matching legacy behavior).
        ''' </summary>
        Public Shared Function EstimateNRTL(comp1 As ConstantProperties, comp2 As ConstantProperties,
                                            unifacModel As String, initial As Double(),
                                            doParallel As Boolean) As Double()
            Dim job As New NRTLJob() With {.DoParallel = doParallel}
            Try
                job.MaterialStream = CreateMaterialStream(comp1, comp2)
                job.PP = New NRTLPropertyPackage(True) With {.CurrentMaterialStream = job.MaterialStream}
                job.Aux = New PropertyPackages.Auxiliary.NRTL
                job.TargetActivities = ComputeTargetActivities(job.MaterialStream, unifacModel, doParallel)

                Dim result = RunSimplex(initial, AddressOf job.Evaluate)
                Return New Double() {result(0), result(1), 0.2}
            Finally
                job.Dispose()
            End Try
        End Function

        ''' <summary>
        ''' Returns {A12, A21}.
        ''' </summary>
        Public Shared Function EstimateUNIQUAC(comp1 As ConstantProperties, comp2 As ConstantProperties,
                                               unifacModel As String, initial As Double(),
                                               doParallel As Boolean) As Double()
            Dim job As New UNIQUACJob() With {.DoParallel = doParallel}
            Try
                job.MaterialStream = CreateMaterialStream(comp1, comp2)
                job.PP = New UNIQUACPropertyPackage(True) With {.CurrentMaterialStream = job.MaterialStream}
                job.Aux = New PropertyPackages.Auxiliary.UNIQUAC
                job.TargetActivities = ComputeTargetActivities(job.MaterialStream, unifacModel, doParallel)

                Dim result = RunSimplex(initial, AddressOf job.Evaluate)
                Return New Double() {result(0), result(1)}
            Finally
                job.Dispose()
            End Try
        End Function

        ''' <summary>
        ''' Returns {A12, A21}.
        ''' </summary>
        Public Shared Function EstimateWilson(comp1 As ConstantProperties, comp2 As ConstantProperties,
                                              unifacModel As String, initial As Double(),
                                              doParallel As Boolean) As Double()
            Dim job As New WilsonJob() With {.DoParallel = doParallel}
            Try
                job.MaterialStream = CreateMaterialStream(comp1, comp2)
                job.PP = New WilsonPropertyPackage() With {.CurrentMaterialStream = job.MaterialStream}
                job.TargetActivities = ComputeTargetActivities(job.MaterialStream, unifacModel, doParallel)

                Dim result = RunSimplex(initial, AddressOf job.Evaluate)
                Return New Double() {result(0), result(1)}
            Finally
                job.Dispose()
            End Try
        End Function

#Region "Shared helpers"

        Private Shared Function CreateMaterialStream(comp1 As ConstantProperties, comp2 As ConstantProperties) As MaterialStream
            Dim ms As New MaterialStream("", "")
            For Each phase In ms.Phases.Values
                phase.Compounds.Add(comp1.Name, New Compound(comp1.Name, ""))
                phase.Compounds(comp1.Name).ConstantProperties = comp1
                phase.Compounds.Add(comp2.Name, New Compound(comp2.Name, ""))
                phase.Compounds(comp2.Name).ConstantProperties = comp2
            Next
            Return ms
        End Function

        ''' <summary>
        ''' Computes target activity coefficients [γ1@x1=0.25, γ1@0.5, γ1@0.75,
        ''' γ2@0.25, γ2@0.5, γ2@0.75] using the requested UNIFAC-family model.
        ''' </summary>
        Private Shared Function ComputeTargetActivities(ms As MaterialStream, unifacModel As String,
                                                        doParallel As Boolean) As Double()
            Const Temp As Double = 298.15
            Dim a1(1), a2(1), a3(1) As Double

            Dim ppuf As UNIFACPropertyPackage = Nothing
            Dim ppufll As UNIFACLLPropertyPackage = Nothing
            Dim ppmu As MODFACPropertyPackage = Nothing
            Dim ppmun As NISTMFACPropertyPackage = Nothing
            Dim unif As PropertyPackages.Auxiliary.Unifac = Nothing
            Dim unifll As PropertyPackages.Auxiliary.UnifacLL = Nothing
            Dim modf As PropertyPackages.Auxiliary.Modfac = Nothing
            Dim nmodf As PropertyPackages.Auxiliary.NISTMFAC = Nothing

            Select Case unifacModel
                Case "UNIFAC"
                    ppuf = New UNIFACPropertyPackage(True) With {.CurrentMaterialStream = ms}
                    unif = New PropertyPackages.Auxiliary.Unifac
                Case "UNIFAC-LL"
                    ppufll = New UNIFACLLPropertyPackage(True) With {.CurrentMaterialStream = ms}
                    unifll = New PropertyPackages.Auxiliary.UnifacLL
                Case "MODFAC"
                    ppmu = New MODFACPropertyPackage(True) With {.CurrentMaterialStream = ms}
                    modf = New PropertyPackages.Auxiliary.Modfac
                Case Else
                    ppmun = New NISTMFACPropertyPackage(True) With {.CurrentMaterialStream = ms}
                    nmodf = New PropertyPackages.Auxiliary.NISTMFAC
            End Select

            Dim calcAt = Function(comp As Double()) As Double()
                             Select Case unifacModel
                                 Case "UNIFAC" : Return unif.GAMMA_MR(Temp, comp, ppuf.RET_VQ, ppuf.RET_VR, ppuf.RET_VEKI)
                                 Case "UNIFAC-LL" : Return unifll.GAMMA_MR(Temp, comp, ppufll.RET_VQ, ppufll.RET_VR, ppufll.RET_VEKI)
                                 Case "MODFAC" : Return modf.GAMMA_MR(Temp, comp, ppmu.RET_VQ, ppmu.RET_VR, ppmu.RET_VEKI)
                                 Case Else : Return nmodf.GAMMA_MR(Temp, comp, ppmun.RET_VQ, ppmun.RET_VR, ppmun.RET_VEKI)
                             End Select
                         End Function

            If doParallel Then
                Try
                    Dim t1 = Task.Run(Sub() a1 = calcAt(New Double() {0.25, 0.75}))
                    Dim t2 = Task.Run(Sub() a2 = calcAt(New Double() {0.5, 0.5}))
                    Dim t3 = Task.Run(Sub() a3 = calcAt(New Double() {0.75, 0.25}))
                    Task.WaitAll(t1, t2, t3)
                Catch ae As AggregateException
                    Throw ae.Flatten().InnerException
                End Try
            Else
                a1 = calcAt(New Double() {0.25, 0.75})
                a2 = calcAt(New Double() {0.5, 0.5})
                a3 = calcAt(New Double() {0.75, 0.25})
            End If

            Return New Double() {a1(0), a2(0), a3(0), a1(1), a2(1), a3(1)}
        End Function

        Private Shared Function RunSimplex(initial As Double(), objective As OptMultivariateFunction) As Double()
            Dim variables(1) As OptBoundVariable
            For i As Integer = 0 To 1
                variables(i) = New OptBoundVariable("x" & CStr(i + 1), initial(i), False, -10000.0#, 10000.0#)
            Next
            Dim solver As New Simplex With {.Tolerance = 0.01, .MaxFunEvaluations = 1000}
            Return solver.ComputeMin(objective, variables)
        End Function

        Private Shared Function ComputeResidual(actu As Double(), actn As Double()) As Double
            Dim fval As Double = 0.0#
            For i As Integer = 0 To 5
                fval += (actn(i) - actu(i)) ^ 2
            Next
            Return fval
        End Function

#End Region

#Region "Per-call job state"

        ' Each Job instance carries the state that the legacy FunctionValueXxx closures
        ' read via form-level fields. Holding it on a per-call instance lets two
        ' estimations run concurrently without state-clobber.

        Private MustInherit Class JobBase
            Public Property MaterialStream As MaterialStream
            Public Property TargetActivities As Double()
            Public Property DoParallel As Boolean

            Public Overridable Sub Dispose()
                If MaterialStream IsNot Nothing Then
                    MaterialStream.Dispose()
                    MaterialStream = Nothing
                End If
            End Sub
        End Class

        Private NotInheritable Class NRTLJob
            Inherits JobBase
            Public Property PP As NRTLPropertyPackage
            Public Property Aux As PropertyPackages.Auxiliary.NRTL

            Public Function Evaluate(x As Double()) As Double
                Aux.InteractionParameters.Clear()
                Aux.InteractionParameters.Add(PP.RET_VIDS()(0), New Dictionary(Of String, PropertyPackages.Auxiliary.NRTL_IPData))
                Aux.InteractionParameters(PP.RET_VIDS()(0)).Add(PP.RET_VIDS()(1), New PropertyPackages.Auxiliary.NRTL_IPData())
                Aux.InteractionParameters(PP.RET_VIDS()(0))(PP.RET_VIDS()(1)).A12 = x(0)
                Aux.InteractionParameters(PP.RET_VIDS()(0))(PP.RET_VIDS()(1)).A21 = x(1)
                Aux.InteractionParameters(PP.RET_VIDS()(0))(PP.RET_VIDS()(1)).alpha12 = 0.2

                Dim ids = PP.RET_VIDS()
                Dim a1(1), a2(1), a3(1) As Double
                If DoParallel Then
                    Try
                        Dim t1 = Task.Run(Sub() a1 = Aux.GAMMA_MR(298.15, New Double() {0.25, 0.75}, ids))
                        Dim t2 = Task.Run(Sub() a2 = Aux.GAMMA_MR(298.15, New Double() {0.5, 0.5}, ids))
                        Dim t3 = Task.Run(Sub() a3 = Aux.GAMMA_MR(298.15, New Double() {0.75, 0.25}, ids))
                        Task.WaitAll(t1, t2, t3)
                    Catch ae As AggregateException
                        Throw ae.Flatten().InnerException
                    End Try
                Else
                    a1 = Aux.GAMMA_MR(298.15, New Double() {0.25, 0.75}, ids)
                    a2 = Aux.GAMMA_MR(298.15, New Double() {0.5, 0.5}, ids)
                    a3 = Aux.GAMMA_MR(298.15, New Double() {0.75, 0.25}, ids)
                End If

                Return ComputeResidual(TargetActivities, New Double() {a1(0), a2(0), a3(0), a1(1), a2(1), a3(1)})
            End Function

            Public Overrides Sub Dispose()
                If PP IsNot Nothing Then
                    PP.Dispose()
                    PP = Nothing
                End If
                Aux = Nothing
                MyBase.Dispose()
            End Sub
        End Class

        Private NotInheritable Class UNIQUACJob
            Inherits JobBase
            Public Property PP As UNIQUACPropertyPackage
            Public Property Aux As PropertyPackages.Auxiliary.UNIQUAC

            Public Function Evaluate(x As Double()) As Double
                Aux.InteractionParameters.Clear()
                Aux.InteractionParameters.Add(PP.RET_VIDS()(0), New Dictionary(Of String, PropertyPackages.Auxiliary.UNIQUAC_IPData))
                Aux.InteractionParameters(PP.RET_VIDS()(0)).Add(PP.RET_VIDS()(1), New PropertyPackages.Auxiliary.UNIQUAC_IPData())
                Aux.InteractionParameters(PP.RET_VIDS()(0))(PP.RET_VIDS()(1)).A12 = x(0)
                Aux.InteractionParameters(PP.RET_VIDS()(0))(PP.RET_VIDS()(1)).A21 = x(1)

                Dim ids = PP.RET_VIDS()
                Dim vq = PP.RET_VQ(), vr = PP.RET_VR()
                Dim a1(1), a2(1), a3(1) As Double
                If DoParallel Then
                    Try
                        Dim t1 = Task.Run(Sub() a1 = Aux.GAMMA_MR(298.15, New Double() {0.25, 0.75}, ids, vq, vr))
                        Dim t2 = Task.Run(Sub() a2 = Aux.GAMMA_MR(298.15, New Double() {0.5, 0.5}, ids, vq, vr))
                        Dim t3 = Task.Run(Sub() a3 = Aux.GAMMA_MR(298.15, New Double() {0.75, 0.25}, ids, vq, vr))
                        Task.WaitAll(t1, t2, t3)
                    Catch ae As AggregateException
                        Throw ae.Flatten().InnerException
                    End Try
                Else
                    a1 = Aux.GAMMA_MR(298.15, New Double() {0.25, 0.75}, ids, vq, vr)
                    a2 = Aux.GAMMA_MR(298.15, New Double() {0.5, 0.5}, ids, vq, vr)
                    a3 = Aux.GAMMA_MR(298.15, New Double() {0.75, 0.25}, ids, vq, vr)
                End If

                Return ComputeResidual(TargetActivities, New Double() {a1(0), a2(0), a3(0), a1(1), a2(1), a3(1)})
            End Function

            Public Overrides Sub Dispose()
                If PP IsNot Nothing Then
                    PP.Dispose()
                    PP = Nothing
                End If
                Aux = Nothing
                MyBase.Dispose()
            End Sub
        End Class

        Private NotInheritable Class WilsonJob
            Inherits JobBase
            Public Property PP As WilsonPropertyPackage

            Public Function Evaluate(x As Double()) As Double
                PP.WilsonM.BIPs.Clear()
                PP.WilsonM.BIPs.Add(PP.RET_VCAS()(0), New Dictionary(Of String, Double()))
                PP.WilsonM.BIPs(PP.RET_VCAS()(0)).Add(PP.RET_VCAS()(1), New Double() {0.0, 0.0})
                PP.WilsonM.BIPs(PP.RET_VCAS()(0))(PP.RET_VCAS()(1))(0) = x(0)
                PP.WilsonM.BIPs(PP.RET_VCAS()(0))(PP.RET_VCAS()(1))(1) = x(1)

                Dim args = PP.GetArguments()
                Dim a1(1), a2(1), a3(1) As Double
                If DoParallel Then
                    Try
                        Dim t1 = Task.Run(Sub() a1 = PP.WilsonM.CalcActivityCoefficients(298.15, New Double() {0.25, 0.75}, args))
                        Dim t2 = Task.Run(Sub() a2 = PP.WilsonM.CalcActivityCoefficients(298.15, New Double() {0.5, 0.5}, args))
                        Dim t3 = Task.Run(Sub() a3 = PP.WilsonM.CalcActivityCoefficients(298.15, New Double() {0.75, 0.25}, args))
                        Task.WaitAll(t1, t2, t3)
                    Catch ae As AggregateException
                        Throw ae.Flatten().InnerException
                    End Try
                Else
                    a1 = PP.WilsonM.CalcActivityCoefficients(298.15, New Double() {0.25, 0.75}, args)
                    a2 = PP.WilsonM.CalcActivityCoefficients(298.15, New Double() {0.5, 0.5}, args)
                    a3 = PP.WilsonM.CalcActivityCoefficients(298.15, New Double() {0.75, 0.25}, args)
                End If

                Return ComputeResidual(TargetActivities, New Double() {a1(0), a2(0), a3(0), a1(1), a2(1), a3(1)})
            End Function

            Public Overrides Sub Dispose()
                If PP IsNot Nothing Then
                    PP.Dispose()
                    PP = Nothing
                End If
                MyBase.Dispose()
            End Sub
        End Class

#End Region

    End Class

End Namespace
