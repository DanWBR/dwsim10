'    Rigorous column: quasi-steady vapor formulation of the dynamic model
'    Copyright 2026 Daniel Wagner O. de Medeiros
'
'    This file is part of DWSIM.
'
'    DWSIM is free software: you can redistribute it and/or modify
'    it under the terms of the GNU General Public License as published by
'    the Free Software Foundation, either version 3 of the License, or
'    (at your option) any later version.
'
'    DWSIM is distributed in the hope that it will be useful,
'    but WITHOUT ANY WARRANTY; without even the implied warranty of
'    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
'    GNU General Public License for more details.
'
'    You should have received a copy of the GNU General Public License
'    along with DWSIM.  If not, see <http://www.gnu.org/licenses/>.

Imports DWSIM.Interfaces.Enums
Imports DWSIM.Thermodynamics.Streams
Imports DWSIM.UnitOperations.Streams
Imports DWSIM.UnitOperations.UnitOperations.Auxiliary.SepOps

Namespace UnitOperations

    Partial Public MustInherit Class Column

        'the vapor every holdup kept at the end of the previous sub-step and the filtered vapor rate the hydraulics read
        Private _qsExcess As Double() = Nothing
        Private _qsVfilt As Double() = Nothing

        ''' <summary>Forgets the state of the quasi-steady sweep (called when the holdups are seeded).</summary>
        Private Sub ResetQuasiSteadyState()
            _qsExcess = Nothing
            _qsVfilt = Nothing
        End Sub

        ''' <summary>
        ''' One sub-step of the dynamic column with quasi-steady vapor. The liquid holdups are the states: each
        ''' tray passes liquid over its weir (Francis relation, or the bed correlation of a packed stage) and keeps
        ''' the rest. The vapor is not a state on the trays: a stage keeps the vapor its free volume holds at its
        ''' pressure and sends the excess up within the sub-step, swept from the sump to the top, so a change of
        ''' boilup reaches the condenser at once, as it does in a real column where the vapor transit time is far
        ''' below any liquid time constant. The condenser drum is the one pressure state (a volume-temperature
        ''' flash of its content); every stage below sits at the drum pressure plus the dry drop of the vapor
        ''' through the tray above it and the liquid head on that tray, top-down. The explicit pressure-driven
        ''' formulation, which integrates the vapor holdup of every tray against a square-root pressure law, has a
        ''' time constant of milliseconds and cannot be run at any step a user would choose; this one has none.
        ''' </summary>
        Private Sub QuasiSteadyVaporSubstep(streams As List(Of MaterialStream), stageIDs As List(Of String),
                                            feeds As List(Of StreamInformation), sideDraws As List(Of StreamInformation),
                                            heatStreams As List(Of StreamInformation),
                                            topProduct As StreamInformation, distillate As StreamInformation, bottomsProduct As StreamInformation,
                                            dt As Double, maxDP As Double, maxDV As Double, C_SB As Double, applyMurphree As Boolean,
                                            minP As Double, coolantT As Double, weeping As Boolean,
                                            ByRef floodingDetected As Boolean, ByRef weepingDetected As Boolean)

            Dim ns = Stages.Count
            Dim colArea = Math.PI * EstimatedDiameter ^ 2 / 4.0
            Dim sump = streams.Count - 1
            'the reboiler stage is the sump: its liquid does not overflow, it fills the bottom of the column (the
            'stage height plus the top spacing), the reboiler duty boils it and the bottoms product is drawn from
            'it, as a kettle or thermosiphon reboiler does with the column bottoms. The separate sump holdup of the
            'explicit formulation is carried along empty.
            Dim reb = ns - 1

            If _qsExcess Is Nothing OrElse _qsExcess.Length <> streams.Count Then
                _qsExcess = Enumerable.Repeat(Double.NaN, streams.Count).ToArray()
                _qsVfilt = Enumerable.Repeat(Double.NaN, streams.Count).ToArray()
            End If

            ' ---------------------------------------------------------------- pressures from the last vapor rates
            'The stage pressures follow the tray hydraulics of the vapor rates of the previous sub-steps (a running
            'average over about five of them), relaxed. Solved together with the flashes the pair (a lower pressure
            'flashes the holdup, the burst of vapor raises the pressure, the higher pressure condenses it) is an
            'algebraic loop that an explicit sweep turns into an oscillation.
            Dim relax = 0.5
            For i = 1 To ns - 1
                Dim above = streams(i - 1)
                Dim st = streams(i)
                Dim Vprev = If(_qsVfilt(i).IsValidDouble(), _qsVfilt(i), Stages(i).Vout.Value)
                Dim dPdry As Double = 0.0
                Dim vap = st.Phases(2).Properties
                Dim nV = vap.molarflow.GetValueOrDefault(), vvap = vap.volumetric_flow.GetValueOrDefault(), rhov = vap.density.GetValueOrDefault()
                If Vprev > 0 AndAlso nV > 0 AndAlso vvap > 0 AndAlso rhov > 0 Then
                    Dim vv = vvap / nV
                    If Stages(i - 1).IsPacked Then
                        dPdry = PackedBedPressureDropForFlow(i - 1, st, Vprev, vv)
                    ElseIf Stages(i - 1).TotalHoleArea > 0 Then
                        Dim uh = Vprev * vv / Stages(i - 1).TotalHoleArea
                        dPdry = 101325.0 * Stages(i - 1).DryTrayPressureDropCoefficient * rhov * uh ^ 2
                    End If
                End If
                Dim head = If(Stages(i - 1).IsPacked OrElse i - 1 = 0, 0.0, above.OverallLiquid.Properties.density.GetValueOrDefault() * 9.80665 * Stages(i - 1).LiquidLevel)
                If Not head.IsValidDouble() Then head = 0.0
                Dim Phyd = above.GetPressure() + dPdry + head
                Dim Pnew = st.GetPressure() + relax * (Phyd - st.GetPressure())
                If Pnew.IsValidDouble() AndAlso Pnew > 0 Then st.SetPressure(Pnew)
            Next
            streams(sump).SetPressure(streams(ns - 1).GetPressure())

            ' ---------------------------------------------------------------- external streams and duties
            For i = 0 To streams.Count - 1
                Dim stageid = stageIDs(i)
                Dim feed = feeds.Where(Function(f) f.AssociatedStage = stageid).FirstOrDefault()
                If feed IsNot Nothing Then
                    Dim feedstream = DirectCast(FlowSheet.SimulationObjects(feed.StreamID), MaterialStream)
                    If feedstream.GetMassFlow().IsValidDouble() AndAlso feedstream.GetMassFlow() > 0 Then streams(i) = streams(i).Add(feedstream, dt)
                End If
                'a product or side draw leaves with the state of the phase it is drawn from, at the rate the stream
                'downstream (a valve) asks for, and never more than the phase holds
                Dim side = sideDraws.Where(Function(f) f.AssociatedStage = stageid).FirstOrDefault()
                If side IsNot Nothing Then
                    Dim sidestream = DirectCast(FlowSheet.SimulationObjects(side.StreamID), MaterialStream)
                    streams(i) = DrawProduct(streams(i), sidestream, If(side.StreamPhase = StreamInformation.Phase.V, PhaseLabel.Vapor, PhaseLabel.Liquid1), dt)
                End If
                If i = 0 Then
                    If topProduct IsNot Nothing Then
                        Dim topstream = DirectCast(FlowSheet.SimulationObjects(topProduct.StreamID), MaterialStream)
                        streams(i) = DrawProduct(streams(i), topstream, PhaseLabel.Vapor, dt)
                    End If
                    If distillate IsNot Nothing Then
                        Dim diststream = DirectCast(FlowSheet.SimulationObjects(distillate.StreamID), MaterialStream)
                        streams(i) = DrawProduct(streams(i), diststream, PhaseLabel.Liquid1, dt)
                    End If
                ElseIf i = reb Then
                    If bottomsProduct IsNot Nothing Then
                        Dim bottomstream = DirectCast(FlowSheet.SimulationObjects(bottomsProduct.StreamID), MaterialStream)
                        streams(i) = DrawProduct(streams(i), bottomstream, PhaseLabel.Liquid1, dt)
                    End If
                End If
                Dim duty = heatStreams.Where(Function(f) f.AssociatedStage = stageid).FirstOrDefault()
                If duty IsNot Nothing AndAlso streams(i).GetMassFlow() > 0 Then
                    Dim estream = DirectCast(FlowSheet.SimulationObjects(duty.StreamID), EnergyStream)
                    Dim dutySign As Double = If(duty.StreamBehavior = StreamInformation.Behavior.Distillate, -1.0, 1.0)
                    ApplyDuty(streams(i), dutySign * estream.EnergyFlow.GetValueOrDefault(), dt, coolantT)
                End If
                FlashHoldup(streams(i))
            Next

            ' ---------------------------------------------------------------- liquid over the weirs, top-down
            Dim lTrans(streams.Count - 1) As MaterialStream
            For i = 0 To ns - 1
                Dim st = streams(i)
                Dim liq = st.OverallLiquid.Properties
                Dim ql = liq.molarflow.GetValueOrDefault()
                Dim vliq = liq.volumetric_flow.GetValueOrDefault()
                If ql <= 0 OrElse vliq <= 0 Then
                    Stages(i).Lout.Value = 0.0
                    If i < ns - 1 Then Stages(i + 1).Lin.Value = 0.0
                    Continue For
                End If
                Dim vl = vliq / ql
                If i = reb Then
                    'the reboiler keeps its liquid
                    Stages(i).LiquidLevel = vliq / colArea
                    Stages(i).Lout.Value = 0.0
                    Continue For
                End If
                Dim area = colArea - Stages(i).DowncomerArea
                Stages(i).LiquidLevel = vliq / area
                Dim Fl As Double
                If Stages(i).IsPacked Then
                    Fl = PackedLiquidMolarFlow(i, st, vl)
                Else
                    Dim beta = Stages(i).LiquidFlowEquationCoefficient_Beta
                    Dim head = Stages(i).LiquidLevel - beta * Stages(i).DowncomerHeight
                    Fl = If(head > 0, Stages(i).LiquidFlowEquationCoefficient_Alpha * Stages(i).DowncomerLength / vl * (head / beta) ^ 1.5, 0.0)
                End If
                If Not Fl.IsValidDouble() OrElse Fl < 0 Then Fl = 0.0
                'a sieve tray the vapor does not hold up drains through its holes
                If weeping AndAlso i > 0 AndAlso i < ns - 1 AndAlso Not Stages(i).IsPacked Then
                    Dim weep = WeepMolarFlow(i, streams, vl)
                    If weep.IsValidDouble() AndAlso weep > 0 Then
                        Fl += weep
                        weepingDetected = True
                    End If
                End If
                'no more than what is there
                Fl = Math.Min(Fl, 0.9 * ql / dt)
                Stages(i).Lout.Value = Fl
                If i < ns - 1 Then Stages(i + 1).Lin.Value = Fl
                If Fl > 0 Then
                    Dim lt = DirectCast(st.CloneXML(), MaterialStream)
                    lt.AssignFromPhase(PhaseLabel.Liquid1, st, True)
                    lt.SetMassFlow(Fl * vl * liq.density.GetValueOrDefault())
                    lt.PropertyPackage = PropertyPackage
                    lt.SetFlowsheet(FlowSheet)
                    lTrans(i) = lt
                End If
            Next
            For i = 0 To ns - 1
                If lTrans(i) IsNot Nothing Then
                    streams(i) = streams(i).Subtract(lTrans(i), dt)
                    streams(i + 1) = streams(i + 1).Add(lTrans(i), dt)
                End If
            Next

            ' ---------------------------------------------------------------- vapor, swept from the sump upward
            Dim Vflow(streams.Count - 1) As Double
            Dim vTrans(streams.Count - 1) As MaterialStream
            For i = reb To 1 Step -1
                Dim st = streams(i)
                FlashHoldup(st)
                Dim vap = st.Phases(2).Properties
                Dim nV = vap.molarflow.GetValueOrDefault()
                Dim vvap = vap.volumetric_flow.GetValueOrDefault()
                Vflow(i) = 0.0
                If nV <= 0 OrElse vvap <= 0 Then _qsExcess(i) = Double.NaN
                If nV > 0 AndAlso vvap > 0 Then
                    Dim vv = vvap / nV
                    Dim freeVol = Math.Max(StageVolume(i, streams.Count) - st.OverallLiquid.Properties.volumetric_flow.GetValueOrDefault(), 0.0)
                    'the vapor a stage sends up is what came to it during the sub-step (from below, and from its own
                    'flash) plus a slow correction of its inventory towards what the free volume holds: a mass balance,
                    'not a search. Sending the whole excess within a sub-step, or letting the rate grow by a fraction per
                    'sub-step while any excess remained, made the rate hunt (it grew past the generation, drained the
                    'inventory, collapsed and grew again), and a controller on the excess alone hunted with the pressure
                    'loop. The vapor kept at the end of the previous sub-step is remembered per stage.
                    Dim capacity = freeVol / vv
                    Dim kept = If(_qsExcess(i).IsValidDouble(), _qsExcess(i), nV - Stages(i).Vout.Value * dt)
                    Dim supply = nV - kept
                    Dim V = supply / dt + (nV - capacity) / (5.0 * dt)
                    If Not V.IsValidDouble() OrElse V < 0 Then V = 0.0
                    V = Math.Min(V, 0.9 * nV / dt)
                    _qsExcess(i) = nV - V * dt
                    If V > 0 Then
                        Dim vt = DirectCast(st.CloneXML(), MaterialStream)
                        vt.AssignFromPhase(PhaseLabel.Vapor, st, True)
                        vt.SetMassFlow(V * vv * vap.density.GetValueOrDefault())
                        vt.PropertyPackage = PropertyPackage
                        vt.SetFlowsheet(FlowSheet)
                        vTrans(i) = vt
                        Vflow(i) = V
                        streams(i) = streams(i).Subtract(vt, dt)
                        streams(i - 1) = streams(i - 1).Add(vt, dt)
                    End If
                End If
                Stages(i).Vout.Value = Vflow(i)
                Stages(i - 1).Vin.Value = Vflow(i)
                _qsVfilt(i) = If(_qsVfilt(i).IsValidDouble(), 0.8 * _qsVfilt(i) + 0.2 * Vflow(i), Vflow(i))
            Next
            Stages(0).Vout.Value = 0.0
            Stages(reb).Vin.Value = 0.0

            'Murphree vapor efficiency: the vapor a stage sends up is E parts of its own equilibrium vapor and
            '(1-E) parts of the vapor it received, at the same transfer mass (the same blend as the explicit path)
            If applyMurphree Then
                For i = 1 To ns - 1
                    If vTrans(i) IsNot Nothing AndAlso vTrans(i + 1) IsNot Nothing AndAlso Stages(i).Efficiency < 1.0 AndAlso Stages(i).Efficiency > 0.0 Then
                        Dim eff = Stages(i).Efficiency
                        Dim transferMass = vTrans(i).GetMassFlow()
                        Dim Meq = vTrans(i).GetMolarFlow(), Min = vTrans(i + 1).GetMolarFlow()
                        If transferMass > 0 AndAlso Meq > 0 AndAlso Min > 0 Then
                            Dim blended = DirectCast(vTrans(i).CloneXML(), MaterialStream).Add(DirectCast(vTrans(i + 1).CloneXML(), MaterialStream), (1.0 - eff) / eff * Meq / Min)
                            blended.SetMassFlow(transferMass)
                            blended.PropertyPackage = PropertyPackage
                            blended.SetFlowsheet(FlowSheet)
                            'swap the composition the stage above received: take the equilibrium vapor back, give the blend
                            streams(i - 1) = streams(i - 1).Subtract(vTrans(i), dt).Add(blended, dt)
                        End If
                    End If
                Next
            End If

            ' ---------------------------------------------------------------- the drum pressure
            'A closed drum with no inerts sits at the bubble pressure of its liquid: more condensing duty than the
            'arriving vapor brings sub-cools the liquid and the pressure falls, which is what a pressure controller
            'on the condenser duty reads. The change per sub-step is limited by "Max. P change".
            FlashHoldup(streams(0))
            If streams(0).GetMolarFlow() > 1.0E-10 Then
                Dim P1i = streams(0).GetPressure()
                Try
                    streams(0).AssignSelfToPP()
                    Dim result = PropertyPackage.CalculateEquilibrium2(FlashCalculationType.TemperatureVaporFraction, streams(0).GetTemperature(), 0.0, P1i)
                    'a hair above the bubble pressure: at the bubble point itself the pressure-enthalpy flash of the
                    'drum content sits on the phase boundary and can flip to a spurious vapor split
                    Dim P1 As Double = 1.003 * Convert.ToDouble(result.CalculatedPressure)
                    'an inert blanket or a vent holds the drum at no less than the minimum pressure
                    If minP > 0 AndAlso (Not P1.IsValidDouble() OrElse P1 < minP) Then P1 = minP
                    'a drum that holds no liquid yet (under a twentieth of its height) has no bubble pressure to
                    'speak of: the first moles of vapor that reach it would set the pressure of the whole column.
                    'It stays at the blanket (or where it is) until it holds liquid.
                    Dim drumLiquid = streams(0).OverallLiquid.Properties.volumetric_flow.GetValueOrDefault() / Math.Max(colArea - Stages(0).DowncomerArea, 1.0E-6)
                    If drumLiquid < 0.05 * Math.Max(BottomSpacing, 1.0E-3) Then P1 = If(minP > 0, minP, P1i)
                    If P1.IsValidDouble() AndAlso P1 > 0 Then
                        If Math.Abs((P1 - P1i) / P1i * 100) > maxDP Then P1 = P1i * (1 + maxDP / 100.0 * Math.Sign(P1 - P1i))
                        streams(0).SetPressure(P1)
                        FlashHoldup(streams(0))
                    End If
                Catch ex As Exception
                    'the drum keeps its pressure for this sub-step
                End Try
            End If
            For i = 1 To streams.Count - 1
                FlashHoldup(streams(i))
            Next

            ' ---------------------------------------------------------------- levels, temperatures, checks
            For i = 0 To ns - 1
                Dim st = streams(i)
                Stages(i).LiquidLevel = st.OverallLiquid.Properties.volumetric_flow.GetValueOrDefault() / If(i = reb, colArea, colArea - Stages(i).DowncomerArea)
                Stages(i).P = st.GetPressure()
                Stages(i).T = st.GetTemperature()
            Next
            BottomLiquidLevel = Stages(reb).LiquidLevel

            If C_SB > 0 OrElse Stages.Any(Function(s) s.IsPacked) Then
                For i = 1 To ns - 2
                    Dim st = streams(i)
                    Dim vap = st.Phases(2).Properties
                    Dim nV = vap.molarflow.GetValueOrDefault(), vvap = vap.volumetric_flow.GetValueOrDefault()
                    Dim ql = st.OverallLiquid.Properties.molarflow.GetValueOrDefault(), vliq = st.OverallLiquid.Properties.volumetric_flow.GetValueOrDefault()
                    If nV <= 0 OrElse vvap <= 0 OrElse ql <= 0 OrElse vliq <= 0 Then Continue For
                    Dim vv = vvap / nV, vl = vliq / ql
                    If Stages(i).IsPacked Then
                        Dim floodFraction, wettingRatio As Double
                        PackedBedCheck(i, st, Stages(i).Vout.Value, Stages(i).Lout.Value, vv, vl, floodFraction, wettingRatio)
                        If floodFraction > 1.0 Then floodingDetected = True
                        If wettingRatio < 1.0 Then weepingDetected = True
                    ElseIf C_SB > 0 Then
                        Dim rhov = vap.density.GetValueOrDefault(), rhol = st.OverallLiquid.Properties.density.GetValueOrDefault()
                        Dim activeArea = colArea - Stages(i).DowncomerArea
                        If rhov > 0 AndAlso rhol > rhov AndAlso activeArea > 0 Then
                            Dim uv = Stages(i).Vout.Value * vv / activeArea
                            If uv > C_SB * Math.Sqrt((rhol - rhov) / rhov) Then floodingDetected = True
                        End If
                    End If
                Next
            End If

        End Sub

        ''' <summary>The volume a stage (or the sump) holds: the drum for the condenser stage, the tray spacing for a
        ''' tray, the reboiler stage height plus the top spacing for the sump.</summary>
        Private Function StageVolume(i As Integer, streamCount As Integer) As Double
            Dim colArea = Math.PI * EstimatedDiameter ^ 2 / 4.0
            If i >= Stages.Count - 1 Then Return colArea * (Stages(Stages.Count - 1).StageHeight + TopSpacing)
            If i = 0 Then Return colArea * BottomSpacing
            Return colArea * Math.Max(Stages(i).StageHeight, 0.05)
        End Function

        ''' <summary>Takes a product from a holdup over a sub-step: the mass flow the product stream carries (what the
        ''' valve downstream passes), drawn from the given phase of the holdup with that phase's own composition and
        ''' enthalpy, and no more than nine tenths of what the phase holds. The product stream itself may still carry
        ''' the state of an earlier solution (the first step of a run, a column starting up empty), which is why it is
        ''' not subtracted as it is.</summary>
        Private Function DrawProduct(holdup As MaterialStream, product As MaterialStream, phase As PhaseLabel, dt As Double) As MaterialStream
            Dim W = product.GetMassFlow()
            If Not W.IsValidDouble() OrElse W <= 0 Then Return holdup
            Dim avail As Double
            If phase = PhaseLabel.Vapor Then
                avail = holdup.Phases(2).Properties.massflow.GetValueOrDefault()
            Else
                avail = holdup.OverallLiquid.Properties.massflow.GetValueOrDefault()
            End If
            If Not avail.IsValidDouble() OrElse avail <= 0 Then Return holdup
            W = Math.Min(W, 0.9 * avail / dt)
            Dim tr = DirectCast(holdup.CloneXML(), MaterialStream)
            tr.AssignFromPhase(phase, holdup, True)
            tr.SetMassFlow(W)
            tr.PropertyPackage = PropertyPackage
            tr.SetFlowsheet(FlowSheet)
            Return holdup.Subtract(tr, dt)
        End Function

        ''' <summary>A pressure-enthalpy flash of a holdup at its own pressure.</summary>
        Private Sub FlashHoldup(st As MaterialStream)
            st.SetFlowsheet(FlowSheet)
            st.PropertyPackage = PropertyPackage
            st.AssignSelfToPP()
            Dim H0 = st.GetMassEnthalpy(), T0 = st.GetTemperature()
            Dim wl0 = st.OverallLiquid.Properties.massfraction.GetValueOrDefault()
            st.SetFlashSpec("PH")
            st.Calculate()
            'A pressure-enthalpy flash of a holdup sitting exactly on its bubble point can come back on the wrong
            'side of the boundary, as vapor at the bubble temperature, with an enthalpy a latent heat above the
            'one asked for. That is not a state the holdup can have (a reboiler that "boiled dry" in one sub-step
            'with no energy to do it): the holdup is put back at the temperature it had, on the liquid side, and
            'a hair below it if the boundary flips that flash as well.
            Dim H1 = st.GetMassEnthalpy()
            If H0.IsValidDouble() AndAlso H1.IsValidDouble() AndAlso T0 > 0 AndAlso Math.Abs(H1 - H0) > 20.0 + 0.02 * Math.Abs(H0) Then
                st.SetTemperature(T0)
                st.SetFlashSpec("PT")
                st.Calculate()
                If wl0 > 0.5 AndAlso st.OverallLiquid.Properties.massfraction.GetValueOrDefault() < 0.5 Then
                    st.SetTemperature(T0 - 1.0)
                    st.SetFlashSpec("PT")
                    st.Calculate()
                End If
            End If
        End Sub

        ''' <summary>The pressure drop of a packed stage at a given vapor rate: the bed correlation of the explicit
        ''' path solved the other way round by bisection on the drop.</summary>
        Private Function PackedBedPressureDropForFlow(i As Integer, st As MaterialStream, V As Double, vv As Double) As Double
            Dim vl As Double = 1.0E-4
            Dim liq = st.OverallLiquid.Properties
            If liq.molarflow.GetValueOrDefault() > 0 AndAlso liq.volumetric_flow.GetValueOrDefault() > 0 Then vl = liq.volumetric_flow.GetValueOrDefault() / liq.molarflow.GetValueOrDefault()
            Dim lo = 0.0, hi = 1.0
            While PackedVaporMolarFlow(i, hi, st, vv, vl) < V AndAlso hi < 1.0E6
                hi *= 2.0
            End While
            For k = 1 To 40
                Dim mid = 0.5 * (lo + hi)
                If PackedVaporMolarFlow(i, mid, st, vv, vl) < V Then lo = mid Else hi = mid
            Next
            Return 0.5 * (lo + hi)
        End Function

    End Class

End Namespace
