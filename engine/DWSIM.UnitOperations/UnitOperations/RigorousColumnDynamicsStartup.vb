'    Rigorous column: starting the dynamic model from an empty column
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

        ''' <summary>The film of liquid an "empty" stage holds (m): enough to give every holdup a state the
        ''' flashes can work with, too little to matter to the balances.</summary>
        Private Const EmptyStageFilm As Double = 0.001

        ''' <summary>Seeds the dynamic holdup of an empty column: every stage and the sump hold a film of liquid of
        ''' the feed composition at the initial temperature and pressure (the sump holds the initial level), with
        ''' no vapor and no flows. The run that follows is a startup driven by the schedule: the feed fills the
        ''' column downward (through the holes of the trays when nothing holds the liquid up), the reboiler duty
        ''' ramps, the vapor climbs, the drum fills, the reflux starts, the level controllers are put in automatic.
        ''' When the tray coefficients are to be calibrated and a steady-state solution exists, the steady-state
        ''' seeding runs first so the trays keep the calibrated coefficients.</summary>
        Public Sub InitializeDynamicsEmpty()

            Dim P0 As Double = 101325.0, T0 As Double = 298.15, hSump As Double = 0.0
            Try
                P0 = Convert.ToDouble(GetDynamicProperty("Initial Pressure"))
                T0 = Convert.ToDouble(GetDynamicProperty("Initial Temperature"))
                hSump = Convert.ToDouble(GetDynamicProperty("Initial Sump Level"))
            Catch ex As Exception
            End Try
            If Not P0.IsValidDouble() OrElse P0 <= 0 Then P0 = 101325.0
            If Not T0.IsValidDouble() OrElse T0 <= 0 Then T0 = 298.15
            If Not hSump.IsValidDouble() OrElse hSump < 0 Then hSump = 0.0

            Dim calibrate As Boolean = False
            Try
                calibrate = Convert.ToBoolean(GetDynamicProperty("Calibrate Tray Coefficients"))
            Catch ex As Exception
            End Try
            If calibrate AndAlso GetLastSolution() IsNot Nothing Then
                Try
                    InitializeDynamicsFromSteadyStateSolution()
                Catch ex As Exception
                End Try
            End If

            ResetQuasiSteadyState()
            CalculateDowncomerAreas()
            For Each s In Stages
                If s.StageHeight <= 0.0 Then s.StageHeight = TraySpacing
            Next
            Dim colArea = Math.PI * EstimatedDiameter ^ 2 / 4.0

            'the composition the column will see: the feed with the largest flow, else equimolar
            Dim z As Double() = Nothing
            Dim best As Double = 0.0
            For Each si In MaterialStreams.Values
                If si.StreamType = StreamInformation.Type.Material AndAlso si.StreamBehavior = StreamInformation.Behavior.Feed AndAlso
                   si.StreamID <> "" AndAlso FlowSheet.SimulationObjects.ContainsKey(si.StreamID) Then
                    Dim feedstream = DirectCast(FlowSheet.SimulationObjects(si.StreamID), MaterialStream)
                    Dim w = feedstream.GetMassFlow()
                    If w.IsValidDouble() AndAlso w > best Then
                        best = w
                        z = feedstream.GetOverallComposition()
                    End If
                End If
            Next

            For i As Integer = 0 To Stages.Count - 1
                Dim s = Stages(i)
                Dim area = Math.Max(colArea - s.DowncomerArea, 1.0E-6)
                'the reboiler stage is the sump of the quasi-steady model: it takes the initial sump level
                If i = Stages.Count - 1 Then area = colArea
                s.AccumulationStream = FilmHoldup(z, T0, P0, If(i = Stages.Count - 1, Math.Max(hSump, EmptyStageFilm), EmptyStageFilm) * area)
                s.LiquidLevel = s.AccumulationStream.OverallLiquid.Properties.volumetric_flow.GetValueOrDefault() / area
                s.P = P0
                s.T = T0
                s.Vin.Value = 0.0
                s.Vout.Value = 0.0
                s.Lin.Value = 0.0
                s.Lout.Value = 0.0
            Next

            BottomsAccumulationStream = FilmHoldup(z, T0, P0, EmptyStageFilm * colArea)
            BottomLiquidLevel = Stages(Stages.Count - 1).LiquidLevel

        End Sub

        ''' <summary>A holdup of the given composition at T0 and P0 whose liquid fills the given volume.</summary>
        Private Function FilmHoldup(z As Double(), T0 As Double, P0 As Double, liqVol As Double) As MaterialStream

            Dim st As New MaterialStream("", "", FlowSheet, PropertyPackage)
            FlowSheet.AddCompoundsToMaterialStream(st)
            Dim n = st.Phases(0).Compounds.Count
            If z Is Nothing OrElse z.Length <> n OrElse z.Sum() <= 0 Then z = Enumerable.Repeat(1.0 / n, n).ToArray()
            With st
                .SetOverallMolarComposition(z.NormalizeY())
                .SetMolarFlow(1.0)
                .SetTemperature(T0)
                .SetPressure(P0)
                .SetFlashSpec("PT")
                .AssignSelfToPP()
                .Calculate()
            End With
            'the volume one mole occupies: as liquid, or as it comes when the mixture is vapor at T0 and P0
            Dim vliq = st.OverallLiquid.Properties.volumetric_flow.GetValueOrDefault()
            Dim ql = st.OverallLiquid.Properties.molarflow.GetValueOrDefault()
            Dim vm As Double
            If vliq > 0 AndAlso ql > 0 Then
                vm = vliq / ql
            Else
                vm = st.Phases(0).Properties.volumetric_flow.GetValueOrDefault() / st.GetMolarFlow()
            End If
            If Not vm.IsValidDouble() OrElse vm <= 0 Then vm = 1.0E-4
            st.SetMolarFlow(liqVol / vm)
            st.SetFlashSpec("PT")
            st.Calculate()
            Return st

        End Function

        ''' <summary>Adds a duty (kW, positive into the holdup) to a holdup over a sub-step, within what an
        ''' exchanger can do: the enthalpy change of one sub-step is capped at the sensible heat of a 25 K change
        ''' of the holdup (a reboiler or condenser stage that holds only a film of liquid, as in a column starting
        ''' up empty, warms or cools at a finite rate instead of overshooting to a temperature the flash cannot
        ''' resolve), and no heat is removed from a holdup already colder than the coolant.</summary>
        Private Sub ApplyDuty(st As MaterialStream, Q As Double, dt As Double, coolantT As Double)

            Dim W = st.GetMassFlow()
            If W <= 0 OrElse Not Q.IsValidDouble() OrElse Q = 0.0 Then Exit Sub
            If Q < 0 AndAlso coolantT > 0 AndAlso st.GetTemperature() <= coolantT Then Exit Sub
            Dim dh = Q * dt / W 'kJ/kg
            Dim cp = st.Phases(0).Properties.heatCapacityCp.GetValueOrDefault() 'kJ/(kg.K)
            If Not cp.IsValidDouble() OrElse cp <= 0 Then cp = 2.0
            Dim cap = 25.0 * cp
            'the condenser cannot cool below its coolant
            If Q < 0 AndAlso coolantT > 0 Then cap = Math.Min(cap, cp * (st.GetTemperature() - coolantT))
            If cap <= 0 Then Exit Sub
            If Math.Abs(dh) > cap Then dh = cap * Math.Sign(dh)
            st.SetMassEnthalpy(st.GetMassEnthalpy() + dh)

        End Sub

        ''' <summary>The liquid that weeps through the holes of tray i (mol/s): the orifice flow of the clear liquid
        ''' head through the hole area, scaled by how far the dry pressure drop of the vapor through the holes
        ''' falls short of holding that head (Fair: weeping sets in when the dry drop is below about 0.4 of the
        ''' liquid head). With no vapor the tray drains; with enough vapor nothing weeps.</summary>
        Private Function WeepMolarFlow(i As Integer, streams As List(Of MaterialStream), vl As Double) As Double

            Dim st = streams(i)
            Dim hL = Stages(i).LiquidLevel
            Dim Ah = Stages(i).TotalHoleArea
            Dim rhol = st.OverallLiquid.Properties.density.GetValueOrDefault()
            If hL <= 0 OrElse Ah <= 0 OrElse rhol <= 0 OrElse vl <= 0 Then Return 0.0
            'the vapor through the holes of tray i is the vapor the stage below sends up
            Dim V = Stages(i).Vin.Value
            Dim dPdry As Double = 0.0
            If V > 0 Then
                Dim vap = st.Phases(2).Properties
                Dim nV = vap.molarflow.GetValueOrDefault(), vvap = vap.volumetric_flow.GetValueOrDefault(), rhov = vap.density.GetValueOrDefault()
                If (nV <= 0 OrElse vvap <= 0 OrElse rhov <= 0) AndAlso i + 1 < streams.Count Then
                    vap = streams(i + 1).Phases(2).Properties
                    nV = vap.molarflow.GetValueOrDefault() : vvap = vap.volumetric_flow.GetValueOrDefault() : rhov = vap.density.GetValueOrDefault()
                End If
                If nV > 0 AndAlso vvap > 0 AndAlso rhov > 0 Then
                    Dim uh = V * (vvap / nV) / Ah
                    dPdry = 101325.0 * Stages(i).DryTrayPressureDropCoefficient * rhov * uh ^ 2
                End If
            End If
            Dim w = 1.0 - dPdry / (0.4 * rhol * 9.80665 * hL)
            If w <= 0 Then Return 0.0
            If w > 1 Then w = 1.0
            'the orifice flow of the whole hole area would dump a tray within a sub-step, and a tray that dumps,
            'refills and dumps again as the vapor hovers around the weep point sends liquid slugs down the column:
            'the weep rate is smooth in the weep fraction and no faster than a fiftieth of the tray liquid per
            'second (a tray with no vapor at all drains with a time constant of about a minute)
            Dim weep = w * w * 0.6 * Ah * Math.Sqrt(2.0 * 9.80665 * hL) / vl
            Dim ql = st.OverallLiquid.Properties.molarflow.GetValueOrDefault()
            Return Math.Min(weep, 0.02 * ql)

        End Function

    End Class

End Namespace
