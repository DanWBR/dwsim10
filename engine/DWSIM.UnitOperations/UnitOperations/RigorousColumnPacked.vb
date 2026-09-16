'    Rigorous column: packed stages in the dynamic model.
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

Imports DWSIM.Automation.DynamicRunner.ColumnInternals
Imports DWSIM.Thermodynamics.Streams
Imports DWSIM.UnitOperations.UnitOperations.Auxiliary.SepOps

Namespace UnitOperations

    ''' <summary>
    ''' A stage marked as packed (by the Column Internals tool) is a slice of bed of height StageHeight
    ''' with the packing recorded on the stage. In the dynamic model its vapour flow comes from the bed
    ''' pressure drop correlation (Robbins, Billet and Schultes or Rocha, Bravo and Fair) inverted for
    ''' the pressure difference between the stages, and its liquid flow from the bed holdup correlation
    ''' inverted for the liquid held on the slice, in place of the orifice and weir equations of a tray.
    ''' </summary>
    Partial Public MustInherit Class Column

        ''' <summary>The packing of a packed stage: the catalogue entry it names, or the constants stored on the stage.</summary>
        Public Shared Function StagePacking(s As Stage) As PackingData
            Dim p As PackingData = Nothing
            If s.PackingName <> "" Then p = PackingCatalogue.Find(s.PackingName)
            If p Is Nothing Then
                p = New PackingData With {.Name = If(s.PackingName <> "", s.PackingName, "stage packing"), .Structured = s.PackingStructured,
                    .Fp = s.PackingFp, .Fpd = s.PackingFpd, .a = s.PackingArea, .Epsilon = s.PackingVoid,
                    .Ch = s.PackingCh, .Cp = s.PackingCp, .Cs = s.PackingCs, .CorrugationSide = s.PackingCorrugationSide, .CorrugationAngle = s.PackingCorrugationAngle}
            End If
            If Double.IsNaN(p.Fp) AndAlso Double.IsNaN(p.Fpd) Then p.Fp = 80.0   'a middling packing factor when the stage carries none
            Return p
        End Function

        ''' <summary>Pressure drop per metre of the packed slice at the superficial velocities, Pa/m, with the model recorded on the stage
        ''' (Billet and Schultes or Rocha, Bravo and Fair when their constants exist, Robbins otherwise). Above the flood point of the
        ''' Rocha, Bravo and Fair model the flood pressure drop is returned.</summary>
        Public Shared Function PackedBedPressureDrop(s As Stage, p As PackingData, uV As Double, uL As Double, rhoV As Double, rhoL As Double,
                                                     muV As Double, muL As Double, sigma As Double, diameter As Double) As Double
            Dim model = CType(s.PackingModel, PackingModel)
            Dim side = p.EffectiveCorrugationSide
            If model = PackingModel.RochaBravoFair AndAlso p.Structured AndAlso Not Double.IsNaN(side) AndAlso side > 0 AndAlso Not Double.IsNaN(p.Epsilon) Then
                Dim dpFlood = If(Not Double.IsNaN(p.Fp), PackingHydraulics.KisterGillFloodPressureDrop(p.Fp), PackingHydraulics.RbfDefaultFloodPressureDrop)
                Dim h As Double
                Dim dp = PackingHydraulics.RbfPressureDrop(uV, uL, rhoV, rhoL, muV, muL, sigma, side, p.Epsilon, p.CorrugationAngle, dpFlood, h)
                Return If(Double.IsNaN(dp), dpFlood, dp)
            End If
            If model = PackingModel.BilletSchultes AndAlso p.HasBilletHydraulics AndAlso Not Double.IsNaN(p.a) AndAlso Not Double.IsNaN(p.Epsilon) Then
                Return PackingHydraulics.BilletPressureDrop(uV, uL, rhoV, rhoL, muV, muL, p.a, p.Epsilon, p.Ch, p.Cp, diameter)
            End If
            Dim fpd = If(Not Double.IsNaN(p.Fpd), p.Fpd, p.Fp)
            Return PackingHydraulics.RobbinsPressureDrop(uV * rhoV, uL * rhoL, rhoV, rhoL, muL, fpd)
        End Function

        ''' <summary>Superficial vapour velocity that gives the pressure drop per metre across the packed slice, m/s (the pressure drop grows with the velocity, so a bisection).</summary>
        Public Shared Function PackedBedVaporVelocity(s As Stage, p As PackingData, dpPerM As Double, uL As Double, rhoV As Double, rhoL As Double,
                                                      muV As Double, muL As Double, sigma As Double, diameter As Double) As Double
            If dpPerM <= 0.0 OrElse Double.IsNaN(dpPerM) Then Return 0.0
            Dim lo = 0.0, hi = 20.0
            If PackedBedPressureDrop(s, p, hi, uL, rhoV, rhoL, muV, muL, sigma, diameter) < dpPerM Then Return hi
            For it = 1 To 60
                Dim mid = 0.5 * (lo + hi)
                If PackedBedPressureDrop(s, p, mid, uL, rhoV, rhoL, muV, muL, sigma, diameter) < dpPerM Then lo = mid Else hi = mid
            Next
            Return 0.5 * (lo + hi)
        End Function

        ''' <summary>Liquid holdup of the packed slice at a superficial liquid velocity, m3 of liquid per m3 of bed: Rocha, Bravo and Fair for a
        ''' structured packing with its corrugation side, Billet and Schultes otherwise (with C_h = 0.7 when the packing has none).</summary>
        Public Shared Function PackedBedHoldup(s As Stage, p As PackingData, uL As Double, rhoV As Double, rhoL As Double, muL As Double, sigma As Double) As Double
            If uL <= 0.0 Then Return 0.0
            Dim side = p.EffectiveCorrugationSide
            If p.Structured AndAlso Not Double.IsNaN(side) AndAlso side > 0 AndAlso Not Double.IsNaN(p.Epsilon) Then
                Dim ft = PackingHydraulics.RbfHoldupFactor(uL, rhoL, muL, Math.Max(sigma, 0.001), side, p.Epsilon, p.CorrugationAngle)
                Return PackingHydraulics.RbfHoldup(ft, uL, rhoL, muL, side, p.Epsilon, p.CorrugationAngle, PackingHydraulics.g * (rhoL - rhoV) / rhoL)
            End If
            Dim a = If(Not Double.IsNaN(p.a) AndAlso p.a > 0, p.a, 150.0)
            Dim ch = If(Not Double.IsNaN(p.Ch) AndAlso p.Ch > 0, p.Ch, 0.7)
            Return PackingHydraulics.BilletHoldup(uL, rhoL, muL, a, ch)
        End Function

        ''' <summary>Superficial liquid velocity that leaves the given holdup on the packed slice, m/s (the holdup grows with the velocity, so a bisection).</summary>
        Public Shared Function PackedBedLiquidVelocity(s As Stage, p As PackingData, holdup As Double, rhoV As Double, rhoL As Double, muL As Double, sigma As Double) As Double
            If holdup <= 0.0 OrElse Double.IsNaN(holdup) Then Return 0.0
            Dim lo = 0.0, hi = 0.5
            If PackedBedHoldup(s, p, hi, rhoV, rhoL, muL, sigma) < holdup Then Return hi
            For it = 1 To 60
                Dim mid = 0.5 * (lo + hi)
                If PackedBedHoldup(s, p, mid, rhoV, rhoL, muL, sigma) < holdup Then lo = mid Else hi = mid
            Next
            Return 0.5 * (lo + hi)
        End Function

        ''' <summary>Phase properties of a stage content the bed correlations need, with the fallbacks of the internals rating when the package gave none.</summary>
        Private Shared Sub PackedBedProperties(st As MaterialStream, ByRef rhoV As Double, ByRef rhoL As Double, ByRef muV As Double, ByRef muL As Double, ByRef sigma As Double)
            rhoV = st.Vapor.Properties.density.GetValueOrDefault()
            rhoL = st.OverallLiquid.Properties.density.GetValueOrDefault()
            muV = st.Vapor.Properties.viscosity.GetValueOrDefault()
            muL = st.OverallLiquid.Properties.viscosity.GetValueOrDefault()
            sigma = st.OverallLiquid.Properties.surfaceTension.GetValueOrDefault()
            If Double.IsNaN(muV) OrElse muV <= 0.0 Then muV = 0.00001 Else muV = Math.Max(0.000002, Math.Min(0.0005, muV))
            If Double.IsNaN(muL) OrElse muL <= 0.0 Then muL = 0.001
            If Double.IsNaN(sigma) OrElse sigma <= 0.0 Then sigma = 0.02
        End Sub

        ''' <summary>Vapour molar flow through the packed slice of stage i for the pressure difference across it, mol/s.</summary>
        Private Function PackedVaporMolarFlow(i As Integer, dP As Double, st As MaterialStream, vv As Double, vl As Double) As Double
            Dim s = Stages(i)
            Dim p = StagePacking(s)
            Dim A = Math.PI * EstimatedDiameter ^ 2 / 4.0
            Dim H = Math.Max(s.StageHeight, 0.05)
            Dim rhoV, rhoL, muV, muL, sigma As Double
            PackedBedProperties(st, rhoV, rhoL, muV, muL, sigma)
            If rhoV <= 0.0 OrElse rhoL <= 0.0 OrElse vv <= 0.0 Then Return 0.0
            Dim uL = If(vl > 0.0, Math.Max(0.0, s.Lout.Value) * vl / A, 0.0)
            Dim uV = PackedBedVaporVelocity(s, p, dP / H, uL, rhoV, rhoL, muV, muL, sigma, EstimatedDiameter)
            Return uV * A / vv
        End Function

        ''' <summary>Liquid molar flow draining from the packed slice of stage i for the liquid it holds, mol/s.</summary>
        Private Function PackedLiquidMolarFlow(i As Integer, st As MaterialStream, vl As Double) As Double
            Dim s = Stages(i)
            Dim p = StagePacking(s)
            Dim A = Math.PI * EstimatedDiameter ^ 2 / 4.0
            Dim H = Math.Max(s.StageHeight, 0.05)
            Dim rhoV, rhoL, muV, muL, sigma As Double
            PackedBedProperties(st, rhoV, rhoL, muV, muL, sigma)
            If rhoL <= 0.0 OrElse vl <= 0.0 Then Return 0.0
            Dim holdup = st.OverallLiquid.Properties.volumetric_flow.GetValueOrDefault() / (A * H)
            Dim uL = PackedBedLiquidVelocity(s, p, holdup, rhoV, rhoL, muL, sigma)
            Return uL * A / vl
        End Function

        ''' <summary>Fraction of flood and wetting ratio of the packed slice at its current flows (the internals rating of the stage).</summary>
        Private Sub PackedBedCheck(i As Integer, st As MaterialStream, Fv As Double, Fl As Double, vv As Double, vl As Double, ByRef floodFraction As Double, ByRef wettingRatio As Double)
            floodFraction = 0.0 : wettingRatio = 1.0
            Dim s = Stages(i)
            Dim p = StagePacking(s)
            Dim rhoV, rhoL, muV, muL, sigma As Double
            PackedBedProperties(st, rhoV, rhoL, muV, muL, sigma)
            If rhoV <= 0.0 OrElse rhoL <= 0.0 OrElse Fv <= 0.0 OrElse Fl <= 0.0 Then Return
            Dim sec As New InternalsSection With {.Type = If(p.Structured, InternalType.StructuredPacking, InternalType.RandomPacking), .CustomPacking = p,
                .PackingModel = CType(s.PackingModel, PackingModel), .HetpModel = HetpModel.RuleOfThumb, .Diameter = EstimatedDiameter}
            Dim sp As New StageProperties With {.Stage = i + 1, .T = st.GetTemperature(), .P = st.GetPressure(),
                .VaporMassFlow = Fv * vv * rhoV, .LiquidMassFlow = Fl * vl * rhoL, .VaporDensity = rhoV, .LiquidDensity = rhoL,
                .VaporViscosity = muV, .LiquidViscosity = muL, .SurfaceTension = sigma}
            Dim r = PackingHydraulics.RatePacking(sec, p, sp, EstimatedDiameter)
            floodFraction = If(Double.IsNaN(r.FloodFraction), 0.0, r.FloodFraction)
            wettingRatio = If(Double.IsNaN(r.WettingRatio), 1.0, r.WettingRatio)
        End Sub

    End Class

End Namespace
