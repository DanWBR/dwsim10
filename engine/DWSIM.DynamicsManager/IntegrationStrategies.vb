Imports DWSIM.Interfaces

Public Class IntegrationStrategies

    Public Shared Property LastErrorEstimate As Double = 0.0

    Public Shared Property ActualStepTaken As Double = 0.0

    Public Shared Sub ExecuteStep(
        flowsheet As IFlowsheet,
        integrator As IDynamicsIntegrator,
        solveAction As Action,
        interval As Double)

        Select Case integrator.IntegrationMethod
            Case Enums.Dynamics.IntegrationMethod.ExplicitEuler
                ExecuteExplicitEuler(solveAction, interval)
            Case Enums.Dynamics.IntegrationMethod.RungeKutta4
                ExecuteStepDoubling(flowsheet, integrator, solveAction, interval)
            Case Enums.Dynamics.IntegrationMethod.ImplicitEuler
                ExecuteImplicitEuler(flowsheet, integrator, solveAction, interval)
            Case Enums.Dynamics.IntegrationMethod.AdaptiveRK45
                ExecuteAdaptiveStep(flowsheet, integrator, solveAction, interval)
        End Select
    End Sub

    Private Shared Sub ExecuteExplicitEuler(solveAction As Action, interval As Double)
        solveAction()
        ActualStepTaken = interval
        LastErrorEstimate = 0.0
    End Sub

    Private Shared Sub ExecuteStepDoubling(
        flowsheet As IFlowsheet,
        integrator As IDynamicsIntegrator,
        solveAction As Action,
        interval As Double)

        Dim originalStep = integrator.IntegrationStep
        Dim savedStates = SaveAllStates(flowsheet)

        solveAction()
        Dim fullStepContents = CaptureDynamicContents(flowsheet)

        RestoreAllStates(flowsheet, savedStates)

        integrator.IntegrationStep = TimeSpan.FromSeconds(interval / 2.0)
        solveAction()
        solveAction()

        Dim halfStepContents = CaptureDynamicContents(flowsheet)

        LastErrorEstimate = ComputeErrorFromContents(fullStepContents, halfStepContents)

        integrator.IntegrationStep = originalStep
        ActualStepTaken = interval
    End Sub

    Private Shared Sub ExecuteImplicitEuler(
        flowsheet As IFlowsheet,
        integrator As IDynamicsIntegrator,
        solveAction As Action,
        interval As Double)

        Dim savedStates = SaveAllStates(flowsheet)
        Dim maxIter = integrator.MaxIterations
        Dim tol = integrator.ConvergenceTolerance

        solveAction()

        For iter = 1 To maxIter - 1
            Dim prevContents = CaptureDynamicContents(flowsheet)

            RestoreAllStates(flowsheet, savedStates)
            solveAction()

            Dim newContents = CaptureDynamicContents(flowsheet)
            Dim err = ComputeErrorFromContents(prevContents, newContents)

            If err < tol Then Exit For
        Next

        ActualStepTaken = interval
        LastErrorEstimate = 0.0
    End Sub

    Private Shared Sub ExecuteAdaptiveStep(
        flowsheet As IFlowsheet,
        integrator As IDynamicsIntegrator,
        solveAction As Action,
        interval As Double)

        Dim originalStep = integrator.IntegrationStep
        Dim minStep = integrator.MinimumStep.TotalSeconds
        Dim maxStep = integrator.MaximumStep.TotalSeconds
        Dim errTol = integrator.ErrorTolerance
        Dim currentInterval = interval
        Dim timeRemaining = interval
        Dim totalError = 0.0

        While timeRemaining > minStep / 2.0
            Dim stepToTake = Math.Min(currentInterval, timeRemaining)
            Dim savedStates = SaveAllStates(flowsheet)

            integrator.IntegrationStep = TimeSpan.FromSeconds(stepToTake)
            solveAction()
            Dim fullStepContents = CaptureDynamicContents(flowsheet)

            RestoreAllStates(flowsheet, savedStates)

            integrator.IntegrationStep = TimeSpan.FromSeconds(stepToTake / 2.0)
            solveAction()
            solveAction()

            Dim halfStepContents = CaptureDynamicContents(flowsheet)
            Dim err = ComputeErrorFromContents(fullStepContents, halfStepContents)

            If err > errTol AndAlso stepToTake > minStep Then
                RestoreAllStates(flowsheet, savedStates)
                currentInterval = stepToTake / 2.0
                Continue While
            End If

            timeRemaining -= stepToTake
            totalError = Math.Max(totalError, err)

            If err < errTol / 4.0 Then
                currentInterval = Math.Min(stepToTake * 2.0, maxStep)
            End If
        End While

        integrator.IntegrationStep = originalStep
        ActualStepTaken = interval
        LastErrorEstimate = totalError
    End Sub

    Private Shared Function SaveAllStates(flowsheet As IFlowsheet) As Dictionary(Of String, Object)
        Dim states As New Dictionary(Of String, Object)
        For Each obj In flowsheet.SimulationObjects.Values
            Dim state = obj.SaveDynamicState()
            If state IsNot Nothing Then
                states(obj.Name) = state
            End If
        Next
        Return states
    End Function

    Private Shared Sub RestoreAllStates(flowsheet As IFlowsheet, states As Dictionary(Of String, Object))
        For Each obj In flowsheet.SimulationObjects.Values
            If states.ContainsKey(obj.Name) Then
                obj.RestoreDynamicState(states(obj.Name))
            End If
        Next
    End Sub

    Private Shared Function CaptureDynamicContents(flowsheet As IFlowsheet) As Dictionary(Of String, Double)
        Dim contents As New Dictionary(Of String, Double)
        For Each obj In flowsheet.SimulationObjects.Values
            Dim mass = obj.GetDynamicContents()
            If Not Double.IsNaN(mass) Then
                contents(obj.Name) = mass
            End If
        Next
        Return contents
    End Function

    Private Shared Function ComputeErrorFromContents(
        contents1 As Dictionary(Of String, Double),
        contents2 As Dictionary(Of String, Double)) As Double

        Dim maxRelError = 0.0

        For Each kvp In contents1
            If contents2.ContainsKey(kvp.Key) Then
                Dim m1 = kvp.Value
                Dim m2 = contents2(kvp.Key)
                If m1 > 1.0E-20 AndAlso m2 > 1.0E-20 Then
                    Dim relErr = Math.Abs(m2 - m1) / Math.Max(m1, 1.0E-10)
                    maxRelError = Math.Max(maxRelError, relErr)
                End If
            End If
        Next

        Return maxRelError
    End Function

End Class
