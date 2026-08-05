Imports System.Threading.Tasks
Imports Cureos.Numerics
Imports DotNumerics
Imports DWSIM.GlobalSettings
Imports DWSIM.Interfaces
Imports DWSIM.SharedClasses.DataRegression.Models
Imports DWSIM.Thermodynamics
Imports DWSIM.Thermodynamics.BaseClasses
Imports DWSIM.Thermodynamics.PropertyPackages
Imports DWSIM.Thermodynamics.PropertyPackages.Auxiliary

Namespace Global.DWSIM.SharedClasses.DataRegression.Engine

    Public Class IterationEventArgs
        Inherits EventArgs
        Public Property Iteration As Integer
        Public Property FunctionValue As Double
        Public Property ParameterText As String
        Public Property IsException As Boolean
        Public Property ExceptionMessage As String
    End Class

    Public Class LogEventArgs
        Inherits EventArgs
        Public Property Message As String
        Public Sub New(msg As String)
            Message = msg
        End Sub
    End Class

    ''' <summary>
    ''' Temperature-dependent interaction parameter coefficients used during the
    ''' second pass of t-dep regression. Set by the caller between Run() invocations.
    ''' </summary>
    Public Class TDepCoefficients
        Public Property A12 As Double
        Public Property A21 As Double
        Public Property B12 As Double
        Public Property B21 As Double
        Public Property C12 As Double
        Public Property C21 As Double
        Public Property Enabled As Boolean
    End Class

    ''' <summary>
    ''' UI-agnostic regression engine. Owns the property package, optimizer
    ''' dispatch, objective function, and per-(model, datatype) flash helpers.
    ''' Raises events for iteration progress, free-text log lines, and
    ''' "objective evaluation completed" so the UI can refresh derived views.
    ''' </summary>
    Public Class RegressionEngine

        Public Event IterationCompleted As EventHandler(Of IterationEventArgs)
        Public Event LogLine As EventHandler(Of LogEventArgs)

        ''' <summary>Raised after every successful FunctionValue evaluation. UI
        ''' subscribes to refresh chart/grids based on currcase.calc* arrays.</summary>
        Public Event ObjectiveEvaluated As EventHandler

        Public Property CurrentCase As RegressionCase
        Public Property PropertyPackage As PropertyPackage
        Public Property RegressedParameters As New Dictionary(Of String, Double)
        Public Property TDep As New TDepCoefficients
        Public Property IterationCount As Integer
        Public Property Output As Boolean = True

        ''' <summary>Caller-supplied parallelism flags. Form project reads
        ''' My.Settings.EnableParallelProcessing/MaxDegreeOfParallelism and
        ''' assigns these before calling Run().</summary>
        Public Property EnableParallel As Boolean = True
        Public Property MaxDegreeOfParallelism As Integer = 4

        Private _cancel As Boolean

        ''' <summary>Property-package name given to PropertyPackageFactory.Create.</summary>
        Public Property PropertyPackageName As String

        Public Sub Cancel()
            _cancel = True
        End Sub

        Public ReadOnly Property IsCancelled As Boolean
            Get
                Return _cancel
            End Get
        End Property

        Public Sub ResetCancel()
            _cancel = False
        End Sub

#Region "Run - single-pass regression (replaces form's DoRegression)"

        ''' <summary>
        ''' Allocates the property package for the model in currcase and seeds it
        ''' with the available compounds. Callers that want to evaluate the
        ''' objective once (without optimization) should call this before
        ''' EvaluateOnce. Run() invokes it internally.
        ''' </summary>
        ''' <summary>
        ''' Caller passes the host's compound dictionary by interface type
        ''' (FormMain.AvailableComponents is Dictionary&lt;string, ICompoundConstantProperties&gt;).
        ''' Each value is downcast to ConstantProperties when populating the
        ''' proppack - DWSIM's compound DB always produces ConstantProperties
        ''' instances so the cast succeeds in practice.
        ''' </summary>
        Public Sub PreparePropertyPackage(currcase As RegressionCase,
                                          availableComps As IDictionary(Of String, ICompoundConstantProperties))
            Me.CurrentCase = currcase
            Dim modelDef = ModelRegistry.GetDefinition(currcase.model)
            If modelDef Is Nothing Then Throw New InvalidOperationException("Unknown regression model: " & currcase.model)

            PropertyPackageName = modelDef.PropertyPackageName
            PropertyPackage = PropertyPackages.PropertyPackageFactory.Create(PropertyPackageName)
            PropertyPackage.ComponentName = PropertyPackageName
            PropertyPackage._availablecomps = New Dictionary(Of String, ConstantProperties)
            For Each kvp In availableComps
                PropertyPackage._availablecomps.Add(kvp.Key, DirectCast(kvp.Value, ConstantProperties))
            Next
            PropertyPackage.VaporPhaseFugacityCalculationMode = Not currcase.idealvapormodel
            PropertyPackage.ActivityCoefficientModels_IgnoreMissingInteractionParameters = True
        End Sub

        Public Sub Run(currcase As RegressionCase, initval As Double(),
                       availableComps As IDictionary(Of String, ICompoundConstantProperties))
            PreparePropertyPackage(currcase, availableComps)

            Dim modelDef = ModelRegistry.GetDefinition(currcase.model)
            Dim nvar As Integer = modelDef.ParameterCount
            Dim initval2() As Double = initval
            Dim lconstr2() As Double = CaseAccessor.GetLowerBoundVector(currcase, nvar)
            Dim uconstr2() As Double = CaseAccessor.GetUpperBoundVector(currcase, nvar)
            Dim fixed() As Boolean = CaseAccessor.GetFixedVector(currcase, nvar)

            IterationCount = 0
            RaiseLog("Starting experimental data regression for " & currcase.model & " model parameter estimation..." & vbCrLf)

            Select Case currcase.method
                Case "Limited Memory BFGS"
                    Dim solver As New Optimization.L_BFGS_B
                    solver.Tolerance = currcase.tolerance
                    solver.MaxFunEvaluations = currcase.maxits
                    solver.ComputeMin(AddressOf FunctionValue, AddressOf FunctionGradient,
                                      BuildBoundVariables(nvar, initval2, fixed, lconstr2, uconstr2))
                Case "Truncated Newton"
                    Dim solver As New Optimization.TruncatedNewton
                    solver.Tolerance = currcase.tolerance
                    solver.MaxFunEvaluations = currcase.maxits
                    solver.ComputeMin(AddressOf FunctionValue, AddressOf FunctionGradient,
                                      BuildBoundVariables(nvar, initval2, fixed, lconstr2, uconstr2))
                Case "Nelder-Mead Simplex Downhill"
                    Dim solver As New Optimization.Simplex
                    solver.Tolerance = currcase.tolerance
                    solver.MaxFunEvaluations = currcase.maxits
                    solver.ComputeMin(AddressOf FunctionValue,
                                      BuildBoundVariables(nvar, initval2, fixed, lconstr2, uconstr2))
                Case "IPOPT"
                    ApplyFixedBounds(nvar, initval2, fixed, lconstr2, uconstr2)
                    Dim obj As Double
                    Dim status As IpoptReturnCode
                    Using problem As New Ipopt(initval2.Length, lconstr2, uconstr2, 0, Nothing, Nothing,
                     0, 0, AddressOf eval_f, AddressOf eval_g,
                     AddressOf eval_grad_f, AddressOf eval_jac_g, AddressOf eval_h)
                        problem.AddOption("tol", currcase.tolerance)
                        problem.AddOption("max_iter", Convert.ToInt32(currcase.maxits))
                        problem.AddOption("mu_strategy", "adaptive")
                        problem.AddOption("hessian_approximation", "limited-memory")
                        status = problem.SolveProblem(initval2, obj, Nothing, Nothing, Nothing, Nothing)
                    End Using
                Case "Particle Swarm", "Local Unimodal Sampling", "Gradient Descent", "Differential Evolution",
                    "Particle Swarm Optimization", "Many Optimizing Liaisons", "Mesh"

                    SwarmOps.Globals.Random = New RandomOps.MersenneTwister()
                    ApplyFixedBounds(nvar, initval2, fixed, lconstr2, uconstr2)

                    Dim sproblem As New RegressionProblem(AddressOf FunctionValue, AddressOf FunctionGradient) With {
                        ._Dim = initval2.Length, ._LB = lconstr2, ._UB = uconstr2, ._INIT = initval2, ._Name = "Regression"}
                    sproblem.MaxIterations = currcase.maxits * initval2.Length
                    sproblem.MinIterations = currcase.maxits
                    sproblem.Tolerance = currcase.tolerance
                    Dim opt As SwarmOps.Optimizer = GetSolver(currcase.method)
                    opt.Problem = sproblem
                    opt.RequireFeasible = True
                    Dim sresult = opt.Optimize(opt.DefaultParameters)

                    If Not sresult.Feasible Then Throw New Exception("Error: Feasible solution not found after " & sresult.Iterations & " iterations.")
            End Select

            RaiseLog("Finished!")
        End Sub

        ''' <summary>One-shot evaluation of FunctionValue (used to refresh
        ''' the chart with t-dep computed parameters).</summary>
        Public Sub EvaluateOnce(currcase As RegressionCase, x As Double())
            Me.CurrentCase = currcase
            FunctionValue(x)
        End Sub

        Private Function GetSolver(solver As String) As SwarmOps.Optimizer
            Select Case solver
                Case "Differential Evolution" : Return New SwarmOps.Optimizers.DE()
                Case "Gradient Descent" : Return New SwarmOps.Optimizers.GD()
                Case "Local Unimodal Sampling" : Return New SwarmOps.Optimizers.LUS()
                Case "Many Optimizing Liaisons" : Return New SwarmOps.Optimizers.MOL()
                Case "Mesh" : Return New SwarmOps.Optimizers.MESH()
                Case "Particle Swarm" : Return New SwarmOps.Optimizers.PS()
                Case "Particle Swarm Optimization" : Return New SwarmOps.Optimizers.PSO()
                Case Else : Return Nothing
            End Select
        End Function

        Private Function BuildBoundVariables(nvar As Integer, initval As Double(), fixed As Boolean(),
                                             lconstr As Double(), uconstr As Double()) As Optimization.OptBoundVariable()
            Dim variables(nvar - 1) As Optimization.OptBoundVariable
            For i = 0 To nvar - 1
                variables(i) = New Optimization.OptBoundVariable("x" & CStr(i + 1), initval(i), fixed(i), lconstr(i), uconstr(i))
            Next
            Return variables
        End Function

        Private Sub ApplyFixedBounds(nvar As Integer, initval As Double(), fixed As Boolean(),
                                     lconstr As Double(), uconstr As Double())
            For i = 0 To nvar - 1
                If fixed(i) Then
                    lconstr(i) = initval(i)
                    uconstr(i) = initval(i)
                End If
            Next
        End Sub

#End Region

#Region "IPOPT delegates"

        Private Function eval_f(n As Integer, x As Double(), new_x As Boolean, ByRef obj_value As Double) As Boolean
            obj_value = FunctionValue(x)
            Return True
        End Function

        Private Function eval_grad_f(n As Integer, x As Double(), new_x As Boolean, ByRef grad_f As Double()) As Boolean
            grad_f = FunctionGradient(x)
            Return True
        End Function

        Private Function eval_g(n As Integer, x As Double(), new_x As Boolean, m As Integer, ByRef g As Double()) As Boolean
            Return True
        End Function

        Private Function eval_jac_g(n As Integer, x As Double(), new_x As Boolean, m As Integer, nele_jac As Integer,
                                    ByRef iRow As Integer(), ByRef jCol As Integer(), ByRef values As Double()) As Boolean
            Return False
        End Function

        Private Function eval_h(n As Integer, x As Double(), new_x As Boolean, obj_factor As Double, m As Integer,
                                lambda As Double(), new_lambda As Boolean, nele_hess As Integer,
                                ByRef iRow As Integer(), ByRef jCol As Integer(), ByRef values As Double()) As Boolean
            Return False
        End Function

#End Region

#Region "Objective function"

        Public Function FunctionValue(x As Double()) As Double

            If _cancel Then Return 0

            Dim doparallel As Boolean = EnableParallel
            Dim poptions As New ParallelOptions() With {.MaxDegreeOfParallelism = MaxDegreeOfParallelism}

            Dim inputs = InputUnpacker.Unpack(CurrentCase)
            Dim Vx1 = inputs.Vx1, Vx2 = inputs.Vx2, Vy = inputs.Vy
            Dim VP = inputs.VP, VT = inputs.VT, VTL = inputs.VTL, VTS = inputs.VTS
            Dim Vx1c = inputs.Vx1c, Vx2c = inputs.Vx2c, Vyc = inputs.Vyc
            Dim VPc = inputs.VPc, VTc = inputs.VTc, VTLc = inputs.VTLc, VTSc = inputs.VTSc
            Dim np As Integer = inputs.Np
            Dim PVF As Boolean = inputs.PVF
            Dim i As Integer = 0

            Dim f As Double = 0.0#
            Dim vartext As String = ""

            RegressedParameters.Clear()

            Try

                CurrentCase.calcp.Clear()
                CurrentCase.calct.Clear()
                CurrentCase.calcy.Clear()
                CurrentCase.calcx1l1.Clear()
                CurrentCase.calcx1l2.Clear()
                CurrentCase.calctl.Clear()
                CurrentCase.calcts.Clear()

                Select Case CurrentCase.datatype
                    Case DataType.Pxy, DataType.Txy
                        PropertyPackage.FlashAlgorithm = New FlashAlgorithms.NestedLoops
                        ConfigureProppackIPs(CurrentCase.model, x, useTDep:=False)
                        If PVF Then
                            RunBubT(CurrentCase.model, x, np, Vx1, VP, VT, VTc, Vyc, doparallel, poptions)
                        Else
                            RunBubP(CurrentCase.model, x, np, Vx1, VT, VP, VPc, Vyc, doparallel, poptions)
                        End If
                        vartext = BuildParameterText(CurrentCase.model, x)
                    Case DataType.TPxy
                    Case DataType.Pxx, DataType.Txx
                        PropertyPackage.FlashAlgorithm = New FlashAlgorithms.SimpleLLE
                        Dim flashinstance As FlashAlgorithms.SimpleLLE = TryCast(PropertyPackage.FlashBase, FlashAlgorithms.SimpleLLE)
                        If flashinstance IsNot Nothing Then
                            With flashinstance
                                .UseInitialEstimatesForPhase1 = True
                                .UseInitialEstimatesForPhase2 = True
                            End With
                        End If
                        ConfigureProppackIPs(CurrentCase.model, x, useTDep:=TDep.Enabled)
                        RunLLEFlashPTLoop(flashinstance, np, Vx1, Vx2, VP, VT, Vx1c, Vx2c)
                        vartext = BuildParameterText(CurrentCase.model, x)
                    Case DataType.TPxx
                        RunTPxxFlashLoop(CurrentCase.model, x, np, Vx1, Vx2, VT, VP, Vx1c, Vx2c)
                        vartext = BuildParameterText(CurrentCase.model, x)
                    Case DataType.TTxSE, DataType.TTxSS
                        If CurrentCase.datatype = DataType.TTxSE Then
                            PropertyPackage.FlashAlgorithm = New FlashAlgorithms.NestedLoopsSLE
                        Else
                            PropertyPackage.FlashAlgorithm = New FlashAlgorithms.NestedLoopsSLE With {.SolidSolution = True}
                        End If
                        ConfigureProppackIPs(CurrentCase.model, x, useTDep:=False)
                        RunSLEFlashes(np, Vx1, VP, VTL, VTS, VTLc, VTSc,
                                      CurrentCase.useTLdata, CurrentCase.useTSdata,
                                      doparallel, poptions)
                        vartext = BuildParameterText(CurrentCase.model, x)
                End Select

                ' --- residual computation ---
                Select Case CurrentCase.datatype
                    Case DataType.Pxy, DataType.Txy
                        For i = 0 To np - 1
                            CurrentCase.calct.Add(VTc(i))
                            CurrentCase.calcp.Add(VPc(i))
                            CurrentCase.calcy.Add(Vyc(i))
                            CurrentCase.calcx1l1.Add(0.0#)
                            CurrentCase.calcx1l2.Add(0.0#)
                            CurrentCase.calctl.Add(0.0#)
                            CurrentCase.calcts.Add(0.0#)
                            f += ComputeVLEResidual(CurrentCase.objfunction, PVF, VTc(i), VT(i), VPc(i), VP(i), Vyc(i), Vy(i))
                        Next
                    Case DataType.Pxx, DataType.Txx
                        If Math.Abs(Vx1(0) - Vx1c(0)) > Math.Abs(Vx1(0) - Vx2c(0)) Then
                            Dim tmpvec As ArrayList = Vx1c.Clone
                            Vx1c = Vx2c.Clone
                            Vx2c = tmpvec
                        End If
                        For i = 0 To np - 1
                            CurrentCase.calcx1l1.Add(Vx1c(i))
                            CurrentCase.calcx1l2.Add(Vx2c(i))
                            CurrentCase.calct.Add(0.0#)
                            CurrentCase.calcp.Add(0.0#)
                            CurrentCase.calcy.Add(0.0#)
                            CurrentCase.calctl.Add(0.0#)
                            CurrentCase.calcts.Add(0.0#)
                            f += ComputeLLEResidual(CurrentCase.objfunction, Vx1c(i), Vx1(i), Vx2c(i), Vx2(i))
                        Next
                    Case DataType.TPxx
                        If Math.Abs(Vx1(0) - Vx1c(0)) > Math.Abs(Vx1(0) - Vx2c(0)) Then
                            Dim tmpvec As ArrayList = Vx1c.Clone
                            Vx1c = Vx2c.Clone
                            Vx2c = tmpvec
                        End If
                        For i = 0 To np - 1
                            CurrentCase.calcx1l1.Add(Vx1c(i))
                            CurrentCase.calcx1l2.Add(Vx2c(i))
                            CurrentCase.calct.Add(0.0#)
                            CurrentCase.calcp.Add(0.0#)
                            CurrentCase.calcy.Add(0.0#)
                            CurrentCase.calctl.Add(0.0#)
                            CurrentCase.calcts.Add(0.0#)
                            f += ComputeTPxxResidual(CurrentCase.objfunction, Vx1c(i), Vx1(i), Vx2c(i), Vx2(i))
                        Next
                    Case DataType.TTxSE, DataType.TTxSS
                        For i = 0 To np - 1
                            CurrentCase.calct.Add(VTc(i))
                            CurrentCase.calctl.Add(VTLc(i))
                            CurrentCase.calcts.Add(VTSc(i))
                            CurrentCase.calcp.Add(VPc(i))
                            CurrentCase.calcy.Add(Vyc(i))
                            CurrentCase.calcx1l1.Add(Vx1c(i))
                            CurrentCase.calcx1l2.Add(Vx2c(i))
                            If CurrentCase.useTLdata Then f += (VTLc(i) - VTL(i)) ^ 2
                            If CurrentCase.useTSdata Then f += (VTSc(i) - VTS(i)) ^ 2
                        Next
                End Select

                IterationCount += 1
                If Output Then
                    RaiseEvent IterationCompleted(Me, New IterationEventArgs With {
                        .Iteration = IterationCount,
                        .FunctionValue = f,
                        .ParameterText = vartext
                    })
                End If

                RaiseEvent ObjectiveEvaluated(Me, EventArgs.Empty)

            Catch ex As Exception
                IterationCount += 1
                RaiseEvent IterationCompleted(Me, New IterationEventArgs With {
                    .Iteration = IterationCount,
                    .FunctionValue = Double.MaxValue,
                    .IsException = True,
                    .ExceptionMessage = ex.Message
                })
                f = Double.MaxValue
                Console.WriteLine(ex.ToString())
            End Try

            Return f
        End Function

        Public Function FunctionGradient(x As Double()) As Double()
            If _cancel Then Return x

            Dim g(x.Length - 1) As Double
            Dim epsilon As Double = 0.01
            Dim f2(x.Length - 1), f3(x.Length - 1) As Double
            Dim x2(x.Length - 1), x3(x.Length - 1) As Double

            For i = 0 To x.Length - 1
                For j = 0 To x.Length - 1
                    x2(j) = x(j)
                    x3(j) = x(j)
                Next
                If x(i) <> 0.0# Then
                    x2(i) = x(i) * (1 + epsilon)
                    x3(i) = x(i) * (1 - epsilon)
                Else
                    x2(i) = x(i) + epsilon / 1000
                    x3(i) = x(i) - epsilon / 1000
                End If
                f2(i) = FunctionValue(x2)
                f3(i) = FunctionValue(x3)
                g(i) = (f2(i) - f3(i)) / (x2(i) - x3(i))
            Next
            Return g
        End Function

#End Region

#Region "Per-objective residual formulas"

        Private Shared Function ComputeVLEResidual(objfn As String, PVF As Boolean,
                                                   VTci As Double, VTi As Double,
                                                   VPci As Double, VPi As Double,
                                                   Vyci As Double, Vyi As Double) As Double
            Select Case objfn
                Case "Least Squares (min T/P+y/x)"
                    If PVF Then Return (VTci - VTi) ^ 2 + (Vyci - Vyi) ^ 2
                    Return (VPci - VPi) ^ 2 + (Vyci - Vyi) ^ 2
                Case "Least Squares (min T/P)"
                    If PVF Then Return (VTci - VTi) ^ 2
                    Return (VPci - VPi) ^ 2
                Case "Least Squares (min y/x)"
                    Return (Vyci - Vyi) ^ 2
                Case "Weighted Least Squares (min T/P+y/x)"
                    If PVF Then Return ((VTci - VTi) / VTi) ^ 2 + ((Vyci - Vyi) / Vyi) ^ 2
                    Return ((VPci - VPi) / VPi) ^ 2 + ((Vyci - Vyi) / Vyi) ^ 2
                Case "Weighted Least Squares (min T/P)"
                    If PVF Then Return ((VTci - VTi) / VTi) ^ 2
                    Return ((VPci - VPi) / VPi) ^ 2
                Case "Weighted Least Squares (min y/x)"
                    Return ((Vyci - Vyi) / Vyi) ^ 2
                Case Else
                    Return 0.0
            End Select
        End Function

        Private Shared Function ComputeLLEResidual(objfn As String,
                                                   Vx1ci As Double, Vx1i As Double,
                                                   Vx2ci As Double, Vx2i As Double) As Double
            Const SmallGapPenalty As Double = 10000000000.0
            Dim closePhases = Math.Abs(Vx1ci - Vx2ci) < 0.001
            Select Case objfn
                Case "Least Squares (min T/P+y/x)", "Least Squares (min T/P)", "Least Squares (min y/x)"
                    If closePhases Then
                        Return (-Vx1ci + Vx1i) ^ 2 * SmallGapPenalty
                    End If
                    Return (-Vx1ci + Vx1i - Vx2ci + Vx2i) ^ 2
                Case "Weighted Least Squares (min T/P+y/x)", "Weighted Least Squares (min T/P)", "Weighted Least Squares (min y/x)"
                    If closePhases Then
                        Return ((Vx1ci - Vx1i) / Vx1i) ^ 2 * SmallGapPenalty
                    End If
                    Return ((Vx1ci - Vx1i) / Vx1i) ^ 2 + ((Vx2ci - Vx2i) / Vx2i) ^ 2
                Case Else
                    Return 0.0
            End Select
        End Function

        Private Shared Function ComputeTPxxResidual(objfn As String,
                                                    Vx1ci As Double, Vx1i As Double,
                                                    Vx2ci As Double, Vx2i As Double) As Double
            Select Case objfn
                Case "Least Squares (min T/P+y/x)", "Least Squares (min T/P)", "Least Squares (min y/x)"
                    Return (Vx1ci - Vx1i) ^ 2 + (Vx2ci - Vx2i) ^ 2
                Case "Weighted Least Squares (min T/P+y/x)", "Weighted Least Squares (min T/P)", "Weighted Least Squares (min y/x)"
                    Return ((Vx1ci - Vx1i) / Vx1i) ^ 2 + ((Vx2ci - Vx2i) / Vx2i) ^ 2
                Case Else
                    Return 0.0
            End Select
        End Function

#End Region

#Region "Per-(model, datatype) flash helpers"

        Private Function FlashCompoundIds(model As String) As Object()
            If model = "Wilson" Then Return PropertyPackage.RET_VCAS()
            Return New Object() {CurrentCase.comp1, CurrentCase.comp2}
        End Function

        Private Sub ConfigureProppackIPs(model As String, x As Double(), useTDep As Boolean)
            Dim comps = New Object() {CurrentCase.comp1, CurrentCase.comp2}
            Dim ipComps = FlashCompoundIds(model)
            ExcelAddIn.ExcelIntegrationNoAttr.AddCompounds(PropertyPackage, comps)

            Select Case model
                Case "Peng-Robinson", "Soave-Redlich-Kwong", "Lee-Kesler-Plöcker"
                    ExcelAddIn.ExcelIntegrationNoAttr.SetIP(PropertyPackage.ComponentName, PropertyPackage, comps,
                        New Double(,) {{0.0#, x(0)}, {x(0), 0.0#}}, Nothing, Nothing, Nothing, Nothing, Nothing, Nothing, Nothing)
                Case "PRSV2-M", "PRSV2-VL"
                    ExcelAddIn.ExcelIntegrationNoAttr.SetIP(PropertyPackage.ComponentName, PropertyPackage, comps,
                        New Double(,) {{0.0#, x(0)}, {x(0), 0.0#}},
                        New Double(,) {{0.0#, x(1)}, {x(1), 0.0#}}, Nothing, Nothing, Nothing, Nothing, Nothing, Nothing)
                Case "UNIQUAC", "Wilson"
                    If useTDep Then
                        ExcelAddIn.ExcelIntegrationNoAttr.SetIP(PropertyPackage.ComponentName, PropertyPackage, ipComps,
                            New Double(,) {{0.0#, 0.0#}, {0.0#, 0.0#}},
                            New Double(,) {{0.0#, TDep.A12}, {TDep.A21, 0.0#}}, New Double(,) {{0.0#, TDep.A21}, {TDep.A12, 0.0#}},
                            New Double(,) {{0.0#, TDep.B12}, {TDep.B21, 0.0#}}, New Double(,) {{0.0#, TDep.B21}, {TDep.B12, 0.0#}},
                            New Double(,) {{0.0#, TDep.C12}, {TDep.C21, 0.0#}}, New Double(,) {{0.0#, TDep.C21}, {TDep.C12, 0.0#}}, Nothing)
                    Else
                        ExcelAddIn.ExcelIntegrationNoAttr.SetIP(PropertyPackage.ComponentName, PropertyPackage, ipComps,
                            New Double(,) {{0.0#, 0.0#}, {0.0#, 0.0#}},
                            New Double(,) {{0.0#, x(0)}, {x(1), 0.0#}}, New Double(,) {{0.0#, x(1)}, {x(0), 0.0#}},
                            Nothing, Nothing, Nothing, Nothing, Nothing)
                    End If
                Case "NRTL"
                    If useTDep Then
                        ExcelAddIn.ExcelIntegrationNoAttr.SetIP(PropertyPackage.ComponentName, PropertyPackage, comps,
                            New Double(,) {{0.0#, 0.0#}, {0.0#, 0.0#}},
                            New Double(,) {{0.0#, TDep.A12}, {TDep.A21, 0.0#}}, New Double(,) {{0.0#, TDep.A21}, {TDep.A12, 0.0#}},
                            New Double(,) {{0.0#, x(2)}, {x(2), 0.0#}},
                            New Double(,) {{0.0#, TDep.B12}, {TDep.B21, 0.0#}}, New Double(,) {{0.0#, TDep.B21}, {TDep.B12, 0.0#}},
                            New Double(,) {{0.0#, TDep.C12}, {TDep.C21, 0.0#}}, New Double(,) {{0.0#, TDep.C21}, {TDep.C12, 0.0#}})
                    Else
                        ExcelAddIn.ExcelIntegrationNoAttr.SetIP(PropertyPackage.ComponentName, PropertyPackage, comps,
                            New Double(,) {{0.0#, 0.0#}, {0.0#, 0.0#}},
                            New Double(,) {{0.0#, x(0)}, {x(1), 0.0#}}, New Double(,) {{0.0#, x(1)}, {x(0), 0.0#}},
                            New Double(,) {{0.0#, x(2)}, {x(2), 0.0#}}, Nothing, Nothing, Nothing, Nothing)
                    End If
            End Select
        End Sub

        Private Function BuildParameterText(model As String, x As Double()) As String
            Dim sb As New System.Text.StringBuilder(", Parameters = {")
            Select Case model
                Case "Peng-Robinson", "Soave-Redlich-Kwong", "Lee-Kesler-Plöcker"
                    sb.Append("kij = ")
                    For i = 0 To x.Length - 1
                        sb.Append(x(i).ToString("N4"))
                    Next
                    RegressedParameters.Add("kij", x(0))
                Case "PRSV2-M", "PRSV2-VL"
                    sb.AppendFormat("kij = {0}, kji = {1}", x(0).ToString("N4"), x(1).ToString("N4"))
                    RegressedParameters.Add("kij", x(0))
                    RegressedParameters.Add("kji", x(1))
                Case "UNIQUAC", "Wilson"
                    sb.AppendFormat("A12 = {0}, A21 = {1}", x(0).ToString("N4"), x(1).ToString("N4"))
                    RegressedParameters.Add("A12", x(0))
                    RegressedParameters.Add("A21", x(1))
                Case "NRTL"
                    sb.AppendFormat("A12 = {0}, A21 = {1}, alpha12 = {2}",
                                    x(0).ToString("N4"), x(1).ToString("N4"), x(2).ToString("N4"))
                    RegressedParameters.Add("A12", x(0))
                    RegressedParameters.Add("A21", x(1))
                    RegressedParameters.Add("alpha12", x(2))
            End Select
            sb.Append("}")
            Return sb.ToString()
        End Function

        Private Sub RunBubT(model As String, x As Double(), np As Integer,
                           Vx1 As ArrayList, VP As ArrayList, VT As ArrayList,
                           VTc As ArrayList, Vyc As ArrayList,
                           doparallel As Boolean, poptions As ParallelOptions)
            If doparallel Then
                Try
                    Dim task1 As Task = Task.Factory.StartNew(Sub() Parallel.For(0, np, poptions,
                        Sub(ipar)
                            Dim r As Object = PropertyPackage.DW_CalcBubT(New Double() {Vx1(ipar), 1 - Vx1(ipar)}, VP(0), VT(ipar))
                            VTc(ipar) = r(4)
                            Vyc(ipar) = r(3)(0)
                        End Sub))
                    task1.Wait()
                Catch ae As AggregateException
                    Throw ae.Flatten().InnerException
                End Try
            Else
                Dim comps = New Object() {CurrentCase.comp1, CurrentCase.comp2}
                For i = 0 To np - 1
                    Dim r As Object = ExcelAddIn.ExcelIntegrationNoAttr.PVFFlash(PropertyPackage, 2, VP(0), 0.0#,
                        comps, New Double() {Vx1(i), 1 - Vx1(i)},
                        SerialIPMatrix1(model, x), SerialIPMatrix2(model, x),
                        SerialIPMatrix3(model, x), SerialIPMatrix4(model, x),
                        Nothing, Nothing, Nothing, Nothing)
                    VTc(i) = r(4, 0)
                    Vyc(i) = r(2, 0)
                Next
            End If
        End Sub

        Private Sub RunBubP(model As String, x As Double(), np As Integer,
                           Vx1 As ArrayList, VT As ArrayList, VP As ArrayList,
                           VPc As ArrayList, Vyc As ArrayList,
                           doparallel As Boolean, poptions As ParallelOptions)
            If doparallel Then
                Try
                    Dim task1 As Task = Task.Factory.StartNew(Sub() Parallel.For(0, np, poptions,
                        Sub(ipar)
                            Dim r As Object = PropertyPackage.DW_CalcBubP(New Double() {Vx1(ipar), 1 - Vx1(ipar)}, VT(0), VP(ipar))
                            VPc(ipar) = r(4)
                            Vyc(ipar) = r(3)(0)
                        End Sub))
                    task1.Wait()
                Catch ae As AggregateException
                    Throw ae.Flatten().InnerException
                End Try
            Else
                Dim comps = New Object() {CurrentCase.comp1, CurrentCase.comp2}
                Dim vfArg As Integer = If(model = "UNIQUAC" OrElse model = "Wilson", 1, 2)
                For i = 0 To np - 1
                    Dim r As Object = ExcelAddIn.ExcelIntegrationNoAttr.TVFFlash(PropertyPackage, vfArg, VT(0), 0.0#,
                        comps, New Double() {Vx1(i), 1 - Vx1(i)},
                        SerialIPMatrix1(model, x), SerialIPMatrix2(model, x),
                        SerialIPMatrix3(model, x), SerialIPMatrix4(model, x),
                        Nothing, Nothing, Nothing, Nothing)
                    VPc(i) = r(4, 0)
                    Vyc(i) = r(2, 0)
                Next
            End If
        End Sub

        Private Function SerialIPMatrix1(model As String, x As Double()) As Double(,)
            Select Case model
                Case "UNIQUAC", "Wilson", "NRTL"
                    Return New Double(,) {{0.0#, 0.0#}, {0.0#, 0.0#}}
                Case Else
                    Return New Double(,) {{0.0#, x(0)}, {x(0), 0.0#}}
            End Select
        End Function
        Private Function SerialIPMatrix2(model As String, x As Double()) As Double(,)
            Select Case model
                Case "PRSV2-M", "PRSV2-VL"
                    Return New Double(,) {{0.0#, x(1)}, {x(1), 0.0#}}
                Case "UNIQUAC", "Wilson", "NRTL"
                    Return New Double(,) {{0.0#, x(0)}, {x(1), 0.0#}}
                Case Else
                    Return Nothing
            End Select
        End Function
        Private Function SerialIPMatrix3(model As String, x As Double()) As Double(,)
            Select Case model
                Case "UNIQUAC", "Wilson", "NRTL"
                    Return New Double(,) {{0.0#, x(1)}, {x(0), 0.0#}}
                Case Else
                    Return Nothing
            End Select
        End Function
        Private Function SerialIPMatrix4(model As String, x As Double()) As Double(,)
            If model = "NRTL" Then Return New Double(,) {{0.0#, x(2)}, {x(2), 0.0#}}
            Return Nothing
        End Function

        Private Sub RunLLEFlashPTLoop(flashinstance As FlashAlgorithms.SimpleLLE,
                                      np As Integer,
                                      Vx1 As ArrayList, Vx2 As ArrayList, VP As ArrayList, VT As ArrayList,
                                      Vx1c As ArrayList, Vx2c As ArrayList)
            For i = 0 To np - 1
                With flashinstance
                    .InitialEstimatesForPhase1 = New Double() {Vx1(i), 1 - Vx1(i)}
                    .InitialEstimatesForPhase2 = New Double() {Vx2(i), 1 - Vx2(i)}
                End With
                Dim r = PropertyPackage.FlashBase.Flash_PT(
                    New Double() {(Vx1(i) + Vx2(i)) / 2, 1 - (Vx1(i) + Vx2(i)) / 2},
                    VP(0), VT(i), PropertyPackage)
                Vx1c(i) = r(2)(0)
                Vx2c(i) = r(6)(0)
            Next
        End Sub

        Private Sub RunTPxxFlashLoop(model As String, x As Double(), np As Integer,
                                     Vx1 As ArrayList, Vx2 As ArrayList,
                                     VT As ArrayList, VP As ArrayList,
                                     Vx1c As ArrayList, Vx2c As ArrayList)
            Dim comps = New Object() {CurrentCase.comp1, CurrentCase.comp2}
            For i = 0 To np - 1
                Dim fz = New Double() {(Vx1(i) + Vx2(i)) / 2, 1 - (Vx1(i) + Vx2(i)) / 2}
                Dim r As Object = ExcelAddIn.ExcelIntegrationNoAttr.PTFlash(
                    PropertyPackage, 3, VP(i), VT(i), comps, fz,
                    SerialIPMatrix1(model, x), SerialIPMatrix2(model, x),
                    SerialIPMatrix3(model, x), SerialIPMatrix4(model, x),
                    Nothing, Nothing, Nothing, Nothing)
                Vx1c(i) = r(2, 1)
                Vx2c(i) = r(2, 2)
            Next
        End Sub

        Private Sub RunSLEFlashes(np As Integer,
                                  Vx1 As ArrayList, VP As ArrayList,
                                  VTL As ArrayList, VTS As ArrayList,
                                  VTLc As ArrayList, VTSc As ArrayList,
                                  useTL As Boolean, useTS As Boolean,
                                  doparallel As Boolean, poptions As ParallelOptions)
            If doparallel Then
                Try
                    Dim task1 As Task = Task.Factory.StartNew(Sub() Parallel.For(0, np, poptions,
                        Sub(ipar)
                            If useTL Then
                                Dim r = PropertyPackage.FlashBase.Flash_PV(New Double() {Vx1(ipar), 1 - Vx1(ipar)}, VP(0), 0.999, VTL(ipar), PropertyPackage)
                                VTLc(ipar) = r(4)
                            Else
                                VTLc(ipar) = 0.0#
                            End If
                            If useTS Then
                                Dim r = PropertyPackage.FlashBase.Flash_PV(New Double() {Vx1(ipar), 1 - Vx1(ipar)}, VP(0), 0.001, VTS(ipar), PropertyPackage)
                                VTSc(ipar) = r(4)
                            Else
                                VTSc(ipar) = 0.0#
                            End If
                        End Sub))
                    task1.Wait()
                Catch ae As AggregateException
                    Throw ae.Flatten().InnerException
                End Try
            Else
                For i = 0 To np - 1
                    If useTL Then
                        Dim r = PropertyPackage.FlashBase.Flash_PV(New Double() {Vx1(i), 1 - Vx1(i)}, VP(0), 0.999, VTL(i), PropertyPackage)
                        VTLc(i) = r(4)
                    Else
                        VTLc(i) = 0.0#
                    End If
                    If useTS Then
                        Dim r = PropertyPackage.FlashBase.Flash_PV(New Double() {Vx1(i), 1 - Vx1(i)}, VP(0), 0.001, VTS(i), PropertyPackage)
                        VTSc(i) = r(4)
                    Else
                        VTSc(i) = 0.0#
                    End If
                Next
            End If
        End Sub

#End Region

        Private Sub RaiseLog(msg As String)
            RaiseEvent LogLine(Me, New LogEventArgs(msg))
        End Sub

    End Class

End Namespace
