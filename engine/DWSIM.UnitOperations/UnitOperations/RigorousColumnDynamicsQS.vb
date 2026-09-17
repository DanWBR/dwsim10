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
                                            ByRef floodingDetected As Boolean, ByRef weepingDetected As Boolean)

            Dim ns = Stages.Count
            Dim colArea = Math.PI * EstimatedDiameter ^ 2 / 4.0
            Dim sump = streams.Count - 1

            ' ---------------------------------------------------------------- pressures from the last vapor rates
            'The stage pressures follow the tray hydraulics of the vapor rates of the previous sub-step, relaxed, and
            'the vapor rates of this sub-step are limited to a fraction of change. Solved together the pair (a lower
            'pressure flashes the holdup, the burst of vapor raises the pressure, the higher pressure condenses it)
            'is an algebraic loop that an explicit sweep turns into an oscillation.
            Dim relax = 0.5
            For i = 1 To ns - 1
                Dim above = streams(i - 1)
                Dim st = streams(i)
                Dim Vprev = Stages(i).Vout.Value
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
                Dim side = sideDraws.Where(Function(f) f.AssociatedStage = stageid).FirstOrDefault()
                If side IsNot Nothing Then
                    Dim sidestream = DirectCast(FlowSheet.SimulationObjects(side.StreamID), MaterialStream)
                    If sidestream.GetMassFlow().IsValidDouble() AndAlso sidestream.GetMassFlow() > 0 Then streams(i) = streams(i).Subtract(sidestream, dt)
                End If
                If i = 0 Then
                    If topProduct IsNot Nothing Then
                        Dim topstream = DirectCast(FlowSheet.SimulationObjects(topProduct.StreamID), MaterialStream)
                        If topstream.GetMassFlow().IsValidDouble() AndAlso topstream.GetMassFlow() > 0 Then streams(i) = streams(i).Subtract(topstream, dt)
                    End If
                    If distillate IsNot Nothing Then
                        Dim diststream = DirectCast(FlowSheet.SimulationObjects(distillate.StreamID), MaterialStream)
                        If diststream.GetMassFlow().IsValidDouble() AndAlso diststream.GetMassFlow() > 0 Then streams(i) = streams(i).Subtract(diststream, dt)
                    End If
                ElseIf i = sump Then
                    If bottomsProduct IsNot Nothing Then
                        Dim bottomstream = DirectCast(FlowSheet.SimulationObjects(bottomsProduct.StreamID), MaterialStream)
                        If bottomstream.GetMassFlow().IsValidDouble() AndAlso bottomstream.GetMassFlow() > 0 Then streams(i) = streams(i).Subtract(bottomstream, dt)
                    End If
                End If
                Dim duty = heatStreams.Where(Function(f) f.AssociatedStage = stageid).FirstOrDefault()
                If duty IsNot Nothing AndAlso streams(i).GetMassFlow() > 0 Then
                    Dim estream = DirectCast(FlowSheet.SimulationObjects(duty.StreamID), EnergyStream)
                    Dim dutySign As Double = If(duty.StreamBehavior = StreamInformation.Behavior.Distillate, -1.0, 1.0)
                    streams(i).SetMassEnthalpy(streams(i).GetMassEnthalpy() + dutySign * estream.EnergyFlow.GetValueOrDefault() * dt / streams(i).GetMassFlow())
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
            For i = sump To 1 Step -1
                Dim st = streams(i)
                FlashHoldup(st)
                Dim vap = st.Phases(2).Properties
                Dim nV = vap.molarflow.GetValueOrDefault()
                Dim vvap = vap.volumetric_flow.GetValueOrDefault()
                Vflow(i) = 0.0
                If nV > 0 AndAlso vvap > 0 Then
                    Dim vv = vvap / nV
                    Dim freeVol = Math.Max(StageVolume(i, streams.Count) - st.OverallLiquid.Properties.volumetric_flow.GetValueOrDefault(), 0.0)
                    Dim excess = nV - freeVol / vv
                    Dim V = Math.Max(excess, 0.0) / dt
                    'the change of rate from the previous sub-step is limited, both ways
                    Dim Vprev = If(i < ns, Stages(i).Vout.Value, Stages(ns - 1).Vin.Value)
                    Dim r = Math.Max(maxDV, 1.0) / 100.0
                    If Vprev > 1.0E-6 Then
                        V = Math.Max(Math.Min(V, Vprev * (1.0 + r)), Vprev * (1.0 - r))
                    Else
                        V = Math.Min(V, 1.0)
                    End If
                    V = Math.Min(V, 0.9 * nV / dt)
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
                If i < ns Then
                    Stages(i).Vout.Value = Vflow(i)
                    Stages(i - 1).Vin.Value = Vflow(i)
                Else
                    Stages(ns - 1).Vin.Value = Vflow(i)
                End If
            Next
            Stages(0).Vout.Value = 0.0

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
                Stages(i).LiquidLevel = st.OverallLiquid.Properties.volumetric_flow.GetValueOrDefault() / (colArea - Stages(i).DowncomerArea)
                Stages(i).P = st.GetPressure()
                Stages(i).T = st.GetTemperature()
            Next
            BottomLiquidLevel = streams(sump).OverallLiquid.Properties.volumetric_flow.GetValueOrDefault() / colArea

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
            If i = streamCount - 1 Then Return colArea * (Stages(Stages.Count - 1).StageHeight + TopSpacing)
            If i = 0 Then Return colArea * BottomSpacing
            Return colArea * Math.Max(Stages(i).StageHeight, 0.05)
        End Function

        ''' <summary>A pressure-enthalpy flash of a holdup at its own pressure.</summary>
        Private Sub FlashHoldup(st As MaterialStream)
            st.SetFlowsheet(FlowSheet)
            st.PropertyPackage = PropertyPackage
            st.AssignSelfToPP()
            st.SetFlashSpec("PH")
            st.Calculate()
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
