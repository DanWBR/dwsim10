Imports DWSIM.Thermodynamics.Streams

Namespace UnitOperations

    ''' <summary>
    ''' Pressure and inventory of a liquid-service holdup (a heater or cooler full of liquid). A volume
    ''' flash on such a content either finds no bracket or swings the pressure by bars for a gram of
    ''' mass, and a little gas coming out of solution does not make it better, so the holdup is taken
    ''' as incompressible instead: it keeps the mass that fills its volume, any vapour leaving with the
    ''' liquid, and sits at the pressure the valve downstream needs to pass the inflow.
    ''' </summary>
    Friend Module IncompressibleHoldup

        Friend Const PropertyName As String = "Incompressible Liquid Holdup"

        Friend Const PropertyDescription As String = "Liquid service: keep the mass that fills the volume (any vapour leaves with the liquid) and set the pressure the downstream valve needs to pass the inflow, instead of a volume flash (which fails or rings on a liquid-full holdup)"

        ''' <summary>True when the unit has the option switched on.</summary>
        Friend Function Enabled(uo As UnitOpBaseClass) As Boolean
            Dim v = uo.GetDynamicProperty(PropertyName)
            If v Is Nothing Then Return False
            Try
                Return Convert.ToBoolean(v)
            Catch
                Return False
            End Try
        End Function

        ''' <summary>
        ''' Returns in <paramref name="pressure"/> the pressure at which the downstream valve passes the
        ''' inflow (from the upstream valve at that pressure, when there is one) plus whatever brings the
        ''' content back to the mass that fills <paramref name="Vol"/> over a few steps. The content keeps its own mass balance, so gas coming out of solution pushes
        ''' liquid out instead of vanishing. Returns False (nothing changed) when the holdup is empty.
        ''' </summary>
        Friend Function TrySolve(uo As UnitOpBaseClass, acc As MaterialStream, ims As MaterialStream, oms As MaterialStream,
                                 Vol As Double, Pmin As Double, timestep As Double, ByRef pressure As Double) As Boolean

            If acc Is Nothing OrElse acc.GetMassFlow() <= 0.0 Then Return False
            Dim rho = acc.Phases(0).Properties.density.GetValueOrDefault
            If rho <= 0.0 Then Return False

            Dim fs = uo.FlowSheet
            Dim downstream = NextValve(fs, oms)
            Dim upstream = PreviousValve(fs, ims)

            If upstream Is Nothing Then
                'nothing upstream sets the flow (a pump, a vessel): the content passes the pressure on, less
                'its own drop, and the valve downstream draws what it passes at that pressure. Solving for the
                'pressure that passes what arrived instead pinned the flow, whatever the valve opening.
                Dim dp = 0.0
                If TypeOf uo Is Heater Then
                    dp = DirectCast(uo, Heater).DeltaP.GetValueOrDefault()
                ElseIf TypeOf uo Is Cooler Then
                    dp = DirectCast(uo, Cooler).DeltaP.GetValueOrDefault()
                End If
                Dim pin = ims.GetPressure()
                If pin > 0.0 Then
                    pressure = Math.Max(pin - dp, Pmin)
                    Return True
                End If
            End If

            'what has to leave on top of the inflow: the excess (or shortfall) of the content over three steps
            Dim win = Math.Max(0.0, ims.GetMassFlow())
            Dim correction = (acc.GetMassFlow() - rho * Vol) / (3.0 * Math.Max(timestep, 0.001))
            Dim p = If(pressure > 0.0, pressure, ims.GetPressure())

            If downstream IsNot Nothing Then

                Dim pout = downstream.GetOutletMaterialStream(0).GetPressure()
                Dim pin As Double
                If upstream IsNot Nothing Then
                    pin = upstream.GetInletMaterialStream(0).GetPressure()
                Else
                    pin = Math.Max(p, pout) + 50.0E+5
                End If
                If pin <= pout Then pin = pout + 1.0E+5

                'with a valve on each side the content is the junction between them: the pressure where the
                'outflow matches the inflow (which falls as the pressure rises) plus the correction; with
                'anything else upstream the inflow is what arrived
                Dim balance = Function(px As Double) As Double
                                  Dim wup = If(upstream IsNot Nothing, upstream.CalculateDynamicMassFlow(pin, px), win)
                                  Return downstream.CalculateDynamicMassFlow(px, pout) - wup - correction
                              End Function

                If balance(pin) <= 0.0 Then
                    p = pin
                ElseIf balance(pout) >= 0.0 Then
                    p = pout
                Else
                    Dim lo = pout, hi = pin
                    For i = 1 To 60
                        Dim mid = 0.5 * (lo + hi)
                        If balance(mid) < 0.0 Then lo = mid Else hi = mid
                        If hi - lo < 10.0 Then Exit For
                    Next
                    p = 0.5 * (lo + hi)
                End If

            End If

            pressure = Math.Max(p, Pmin)
            Return True

        End Function

        Private Function NextValve(fs As Interfaces.IFlowsheet, oms As MaterialStream) As Valve
            If oms Is Nothing OrElse oms.GraphicObject Is Nothing Then Return Nothing
            Dim c = oms.GraphicObject.OutputConnectors(0)
            If Not c.IsAttached OrElse c.AttachedConnector Is Nothing Then Return Nothing
            Dim target = c.AttachedConnector.AttachedTo
            If target Is Nothing OrElse Not fs.SimulationObjects.ContainsKey(target.Name) Then Return Nothing
            Return TryCast(fs.SimulationObjects(target.Name), Valve)
        End Function

        Private Function PreviousValve(fs As Interfaces.IFlowsheet, ims As MaterialStream) As Valve
            If ims Is Nothing OrElse ims.GraphicObject Is Nothing Then Return Nothing
            Dim c = ims.GraphicObject.InputConnectors(0)
            If Not c.IsAttached OrElse c.AttachedConnector Is Nothing Then Return Nothing
            Dim source = c.AttachedConnector.AttachedFrom
            If source Is Nothing OrElse Not fs.SimulationObjects.ContainsKey(source.Name) Then Return Nothing
            Return TryCast(fs.SimulationObjects(source.Name), Valve)
        End Function

    End Module

End Namespace
