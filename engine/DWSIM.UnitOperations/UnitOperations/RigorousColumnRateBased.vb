'    Rigorous column: rate-based mode, stage efficiencies from mass transfer.
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

Imports System.Xml.Linq
Imports DWSIM.Automation.DynamicRunner.ColumnInternals
Imports DWSIM.Thermodynamics.Streams
Imports DWSIM.UnitOperations.UnitOperations.Auxiliary.SepOps

Namespace UnitOperations

    ''' <summary>
    ''' Rate-based mode of the rigorous column. The equilibrium-stage solvers keep their MESH equations,
    ''' but the Murphree vapour efficiency of every component on every stage comes from mass transfer
    ''' instead of a number the user types: on a tray from the AIChE or Chan and Fair transfer units,
    ''' the Bennett clear liquid height and the Gerster partial-mixing relation; on a packed stage from
    ''' the HETP of each component (Onda, Billet and Schultes or Rocha, Bravo and Fair) against the slice
    ''' of bed the stage stands for. The efficiencies depend on the flows and properties of the solution,
    ''' so the column solves, rates its stages, solves again with the new efficiencies and repeats until
    ''' they settle. The tray geometry and the packings come from the column internals case saved in the
    ''' column; stages outside it take the standard sieve tray geometry.
    ''' </summary>
    Partial Public MustInherit Class Column

        ''' <summary>Stage efficiencies from mass transfer (rate-based mode) instead of the values on the stages.</summary>
        Public Property RateBased As Boolean = False

        ''' <summary>Gas-phase transfer units of a tray: 0 AIChE, 1 Chan and Fair (sieve trays).</summary>
        Public Property RateBasedTrayMethod As Integer = 0

        ''' <summary>Solve, rate, solve again at most this many times.</summary>
        Public Property RateBasedMaxPasses As Integer = 8

        ''' <summary>The largest change of any component efficiency that ends the passes.</summary>
        Public Property RateBasedTolerance As Double = 0.01

        ''' <summary>The component efficiencies of the last rate-based pass, stage by stage (condenser to reboiler) and compound by compound; Nothing before the first pass.</summary>
        <Xml.Serialization.XmlIgnore> Public RateBasedEfficiencies As Double()() = Nothing

        ''' <summary>What the rate-based passes did, one line per pass.</summary>
        <Xml.Serialization.XmlIgnore> Public RateBasedLog As New List(Of String)

        ''' <summary>How the last rating saw each stage (clear liquid height, transfer units, point efficiency, Peclet number; HETP range of a packed stage).</summary>
        <Xml.Serialization.XmlIgnore> Public RateBasedStageNotes As New List(Of String)

        Private _rateBasedPass As Boolean = False

        ''' <summary>The efficiency the solvers use for the stage as a whole (the mean over the components in rate-based mode).</summary>
        Public Function StageEfficiencyForSolver(i As Integer) As Double
            If RateBased AndAlso RateBasedEfficiencies IsNot Nothing AndAlso i < RateBasedEfficiencies.Length AndAlso RateBasedEfficiencies(i) IsNot Nothing AndAlso RateBasedEfficiencies(i).Length > 0 Then
                Return RateBasedEfficiencies(i).Average()
            End If
            Return Stages(i).Efficiency
        End Function

        ''' <summary>The mean component efficiency of a stage from the last rate-based pass, or NaN.</summary>
        Public Function RateBasedStageEfficiency(i As Integer) As Double
            If RateBasedEfficiencies Is Nothing OrElse i >= RateBasedEfficiencies.Length OrElse RateBasedEfficiencies(i) Is Nothing Then Return Double.NaN
            Return RateBasedEfficiencies(i).Average()
        End Function

        ''' <summary>The component efficiencies as the solver input wants them, or Nothing when the mode is off or nothing was rated yet.</summary>
        Public Function ComponentEfficienciesForSolver() As List(Of Double())
            If Not RateBased OrElse RateBasedEfficiencies Is Nothing OrElse RateBasedEfficiencies.Length <> Stages.Count Then Return Nothing
            Return RateBasedEfficiencies.ToList()
        End Function

        ''' <summary>After a solve: rate the stages from the solution, and solve again while the efficiencies still move.</summary>
        Private Sub RunRateBasedPasses(args As Object)
            If _rateBasedPass Then Return
            _rateBasedPass = True
            Try
                RateBasedLog.Clear()
                Dim ci = Globalization.CultureInfo.InvariantCulture
                For pass = 1 To Math.Max(1, RateBasedMaxPasses)
                    Dim fresh = ComputeRateBasedEfficiencies()
                    Dim change As Double = 1.0
                    If RateBasedEfficiencies IsNot Nothing AndAlso RateBasedEfficiencies.Length = fresh.Length Then
                        change = 0.0
                        For i = 0 To fresh.Length - 1
                            For j = 0 To fresh(i).Length - 1
                                change = Math.Max(change, Math.Abs(fresh(i)(j) - RateBasedEfficiencies(i)(j)))
                            Next
                        Next
                    End If
                    RateBasedEfficiencies = fresh
                    Dim mean = fresh.Where(Function(e) e.Length > 0).Select(Function(e) e.Average()).DefaultIfEmpty(1.0).Average()
                    RateBasedLog.Add(String.Format(ci, "pass {0}: mean stage efficiency {1:F3}, largest change {2:F3}", pass, mean, change))
                    If change <= RateBasedTolerance Then Exit For
                    If pass = RateBasedMaxPasses Then
                        RateBasedLog.Add("The passes ran out before the efficiencies settled; the column carries the last set.")
                        Exit For
                    End If
                    Calculate(args)
                Next
            Finally
                _rateBasedPass = False
            End Try
        End Sub

        ''' <summary>
        ''' Component Murphree efficiencies of every stage from the last solution: the flows and compositions the
        ''' column solved, the phase properties of its property package, diffusivities estimated by Fuller and
        ''' Wilke-Chang (or the ones the internals section gives), and the tray or packing of the stage.
        ''' </summary>
        Public Function ComputeRateBasedEfficiencies() As Double()()
            Dim ns = Stages.Count
            Dim names = DirectCast(PropertyPackage, DWSIM.Thermodynamics.PropertyPackages.PropertyPackage).RET_VNAMES()
            Dim nc = names.Length
            Dim result(ns - 1)() As Double
            For i = 0 To ns - 1
                result(i) = Enumerable.Repeat(1.0, nc).ToArray()
            Next
            RateBasedStageNotes.Clear()
            Dim ci = Globalization.CultureInfo.InvariantCulture
            If Tf Is Nothing OrElse Tf.Length < ns OrElse Vf Is Nothing OrElse Lf Is Nothing OrElse xf.Count < ns OrElse yf.Count < ns OrElse Kf.Count < ns Then Return result

            Dim pp = DirectCast(PropertyPackage, DWSIM.Thermodynamics.PropertyPackages.PropertyPackage)
            Dim M(nc - 1), Vc(nc - 1) As Double
            Dim waterIndex As Integer = -1
            For j = 0 To nc - 1
                Dim cp = FlowSheet.SelectedCompounds(names(j))
                M(j) = cp.Molar_Weight
                Vc(j) = cp.Critical_Volume
                If cp.Name.ToLowerInvariant() = "water" Then waterIndex = j
            Next

            Dim internals As ColumnInternalsInput = Nothing
            If Not String.IsNullOrWhiteSpace(InternalsCase) Then
                Try
                    internals = ColumnInternalsInput.FromXml(XElement.Parse(InternalsCase))
                Catch
                    internals = Nothing
                End Try
            End If
            Dim diameter = If(EstimatedDiameter > 0 AndAlso Not Double.IsNaN(EstimatedDiameter), EstimatedDiameter, 1.0)

            Dim ms = New MaterialStream("", "", FlowSheet, pp)
            FlowSheet.AddCompoundsToMaterialStream(ms)
            Dim previous = pp.CurrentMaterialStream
            pp.CurrentMaterialStream = ms
            Try
                Dim first = If(TypeOf Me Is DistillationColumn, 1, 0)
                Dim last = If(TypeOf Me Is DistillationColumn, ns - 2, ns - 1)
                For i = first To last
                    Dim x = DirectCast(xf(i), Double())
                    Dim y = DirectCast(yf(i), Double())
                    Dim K = DirectCast(Kf(i), Double())
                    Dim iv = If(i + 1 < ns, i + 1, i)
                    Dim yv = DirectCast(yf(iv), Double())
                    Dim T = Tf(i)
                    Dim P = If(Stages(i).P > 0, Stages(i).P, 101325.0)
                    Dim Vm = Vf(iv), Lm = Lf(i)   'mol/s
                    If Vm <= 0 OrElse Lm <= 0 Then Continue For

                    Dim mwV = pp.AUX_MMM(yv), mwL = pp.AUX_MMM(x)
                    ms.SetOverallComposition(yv)
                    ms.SetPhaseComposition(yv, 5)
                    Dim rhoV = pp.AUX_VAPDENS(T, P)
                    Dim muV As Double = 0.00001
                    Try
                        Dim e = pp.AUX_VAPVISCm(T, rhoV, mwV)
                        If e > 0 AndAlso Not Double.IsNaN(e) Then muV = Math.Max(0.000002, Math.Min(0.0005, e))
                    Catch
                    End Try
                    ms.SetOverallComposition(x)
                    ms.SetPhaseComposition(x, 0)
                    ms.SetPhaseComposition(x, 1)
                    Dim rhoL = pp.AUX_LIQDENS(T, x, P)
                    Dim muL As Double = 0.001
                    Try
                        Dim e = pp.AUX_LIQVISCm(T, P)
                        If e > 0 AndAlso Not Double.IsNaN(e) Then muL = e
                    Catch
                    End Try
                    Dim sigma As Double = 0.02
                    Try
                        Dim e = pp.AUX_SURFTM(T)
                        If e > 0 AndAlso Not Double.IsNaN(e) Then sigma = e
                    Catch
                    End Try
                    If rhoV <= 0 OrElse rhoL <= 0 Then Continue For

                    Dim QG = Vm / 1000.0 * mwV / rhoV
                    Dim QL = Lm / 1000.0 * mwL / rhoL
                    Dim lambda(nc - 1), DG(nc - 1), DL(nc - 1) As Double
                    For j = 0 To nc - 1
                        lambda(j) = Math.Max(1e-4, K(j) * Vm / Lm)
                        DG(j) = Diffusivities.GasInMixture(T, P, M, Vc, yv, j)
                        DL(j) = Diffusivities.LiquidInMixture(T, muL, M, Vc, x, j, waterIndex)
                    Next

                    Dim sec As InternalsSection = Nothing
                    If internals IsNot Nothing Then sec = internals.Sections.FirstOrDefault(Function(s) i + 1 >= s.FromStage AndAlso i + 1 <= s.ToStage)
                    Dim packed = If(sec IsNot Nothing, Not sec.IsTray, Stages(i).IsPacked)
                    If sec IsNot Nothing Then
                        If sec.Diameter > 0 Then diameter = sec.Diameter
                        For j = 0 To nc - 1
                            If sec.VapourDiffusivity > 0 Then DG(j) = sec.VapourDiffusivity
                            If sec.LiquidDiffusivity > 0 Then DL(j) = sec.LiquidDiffusivity
                        Next
                    End If

                    If packed Then
                        Dim pk As PackingData = If(sec IsNot Nothing, sec.ResolvePacking(), Nothing)
                        If pk Is Nothing Then pk = StagePacking(Stages(i))
                        Dim psec As InternalsSection = If(sec IsNot Nothing, sec.Clone(), New InternalsSection With {.Type = If(pk.Structured, InternalType.StructuredPacking, InternalType.RandomPacking), .PackingModel = CType(Stages(i).PackingModel, PackingModel), .HetpModel = If(pk.Structured, HetpModel.RochaBravoFair, HetpModel.Onda)})
                        psec.CustomPacking = pk
                        If psec.HetpModel = HetpModel.RuleOfThumb Then psec.HetpModel = If(pk.Structured, HetpModel.RochaBravoFair, HetpModel.Onda)
                        Dim H = If(Stages(i).StageHeight > 0, Stages(i).StageHeight, TraySpacing)
                        Dim hetpMin = Double.MaxValue, hetpMax = 0.0
                        For j = 0 To nc - 1
                            Dim sp As New StageProperties With {.Stage = i + 1, .T = T, .P = P, .VaporMassFlow = Vm / 1000.0 * mwV, .LiquidMassFlow = Lm / 1000.0 * mwL,
                                .VaporDensity = rhoV, .LiquidDensity = rhoL, .VaporViscosity = muV, .LiquidViscosity = muL, .SurfaceTension = sigma,
                                .VaporDiffusivity = DG(j), .LiquidDiffusivity = DL(j), .StrippingFactor = lambda(j)}
                            Dim r = PackingHydraulics.RatePacking(psec, pk, sp, diameter)
                            Dim hetp = If(r.HETP > 0 AndAlso Not Double.IsNaN(r.HETP), r.HETP, r.HETPRuleOfThumb)
                            hetpMin = Math.Min(hetpMin, hetp) : hetpMax = Math.Max(hetpMax, hetp)
                            result(i)(j) = TrayMassTransfer.PackedStageEfficiency(H / Math.Max(hetp, 0.01), lambda(j))
                        Next
                        RateBasedStageNotes.Add(String.Format(ci, "stage {0}: packed, slice {1:F3} m, HETP {2:F3} to {3:F3} m, E {4:F3}", i + 1, H, hetpMin, hetpMax, result(i).Average()))
                    Else
                        Dim hw = If(sec IsNot Nothing, sec.WeirHeight, 0.05)
                        Dim fd = If(sec IsNot Nothing, sec.DowncomerAreaFraction, 0.12)
                        Dim Ac = Math.PI * diameter ^ 2 / 4.0
                        Dim lw = TrayHydraulics.WeirLengthRatio(fd) * diameter
                        Dim Aa = Ac * (1.0 - 2.0 * fd)
                        Dim Z = diameter - 2.0 * TrayHydraulics.ChordHeight(diameter, lw)
                        Dim flood As Double = 0.6
                        Try
                            Dim tsec As InternalsSection = If(sec IsNot Nothing, sec, New InternalsSection With {.TraySpacing = TraySpacing})
                            Dim spt As New StageProperties With {.Stage = i + 1, .T = T, .P = P, .VaporMassFlow = Vm / 1000.0 * mwV, .LiquidMassFlow = Lm / 1000.0 * mwL,
                                .VaporDensity = rhoV, .LiquidDensity = rhoL, .VaporViscosity = muV, .LiquidViscosity = muL, .SurfaceTension = sigma}
                            Dim rt = TrayHydraulics.RateTray(tsec, spt, diameter, 1.0, 0.0, 1.0)
                            If rt.FloodFraction > 0 AndAlso Not Double.IsNaN(rt.FloodFraction) Then flood = rt.FloodFraction
                        Catch
                        End Try
                        Dim eog(), emv(), ng(), nlu() As Double
                        Dim hL, pe As Double
                        TrayMassTransfer.Efficiencies(CType(RateBasedTrayMethod, TrayMassTransferMethod), hw, lw, Z, Aa, QL, QG, rhoV, rhoL, muV, DG, DL, lambda, flood, eog, emv, hL, pe, ng, nlu)
                        result(i) = emv
                        RateBasedStageNotes.Add(String.Format(ci, "stage {0}: tray, h_L {1:F1} mm, flood {2:F2}, N_G {3:F2}, N_L {4:F2}, E_OG {5:F3}, Pe {6:F1}, E_MV {7:F3}",
                                                              i + 1, hL * 1000, flood, ng.Average(), nlu.Average(), eog.Average(), pe, emv.Average()))
                    End If
                Next
            Finally
                pp.CurrentMaterialStream = previous
            End Try
            Return result
        End Function

    End Class

End Namespace
