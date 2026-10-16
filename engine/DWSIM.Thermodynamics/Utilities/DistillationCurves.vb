'    Distillation Curves of Petroleum Streams
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

Imports System.Math
Imports DWSIM.Thermodynamics.BaseClasses
Imports DWSIM.Thermodynamics.Streams
Imports DWSIM.Thermodynamics.Utilities.PetroleumCharacterization.Methods
Imports DWSIM.SharedClasses.Utilities.PetroleumCharacterization.Contaminants

Namespace Utilities.PetroleumProperties

    Public Enum DistillationCurveType
        TBP = 0
        ASTM_D86 = 1
        ASTM_D1160 = 2
        ASTM_D2887 = 3
        EFV = 4
    End Enum

    Public Enum DistillationCurveBasis
        LiquidVolume = 0
        Mass = 1
        Mole = 2
    End Enum

    Public Enum EFVCalculationMethod
        ''' <summary>Flashes the stream with its own property package.</summary>
        Flash = 0
        ''' <summary>Converts the D86 curve with the Riazi-Daubert correlation.</summary>
        Correlation = 1
    End Enum

    ''' <summary>A distillation curve: temperatures (K) against percent distilled.</summary>
    Public Class DistillationCurve

        Public Property CurveType As DistillationCurveType

        ''' <summary>The basis of the percent distilled.</summary>
        Public Property Basis As DistillationCurveBasis

        ''' <summary>The pressure the curve refers to, in Pa.</summary>
        Public Property Pressure As Double

        Public Property Percents As Double() = {}

        Public Property Temperatures As Double() = {}

        ''' <summary>Temperature at a percent distilled, interpolated linearly between the curve points.</summary>
        Public Function TemperatureAt(percent As Double) As Double
            Return DistillationCurves.InterpolateLinear(Percents, Temperatures, percent)
        End Function

    End Class

    ''' <summary>The options of a distillation curve calculation.</summary>
    Public Class DistillationCurveSettings

        ''' <summary>Uses the composition of the liquid phase instead of the whole stream.</summary>
        Public Property LiquidPhaseOnly As Boolean = False

        ''' <summary>The basis of the TBP curve reported. The conversions to the other curves always use liquid volume.</summary>
        Public Property TBPBasis As DistillationCurveBasis = DistillationCurveBasis.LiquidVolume

        ''' <summary>The percents distilled reported, from 0 (initial boiling point) to 100 (final boiling point).</summary>
        Public Property Percents As Double() = {0, 5, 10, 30, 50, 70, 90, 95, 100}

        ''' <summary>The pressure of the ASTM D1160 curve, in Pa. The correlation works at 10 mmHg.</summary>
        Public Property D1160Pressure As Double = 1333.224

        ''' <summary>The pressure of the EFV curve, in Pa.</summary>
        Public Property EFVPressure As Double = 101325

        Public Property EFVMethod As EFVCalculationMethod = EFVCalculationMethod.Flash

    End Class

    ''' <summary>Petroleum product properties of a stream. All values in SI units unless noted.</summary>
    Public Class PetroleumProductProperties

        Public Property MolecularWeight As Double
        ''' <summary>Specific gravity 60F/60F of the ideal liquid mixture.</summary>
        Public Property SpecificGravity As Double
        Public Property API As Double
        Public Property WatsonK As Double
        Public Property MeanAverageBoilingPoint As Double
        ''' <summary>Liquid density at 15 °C and 1 atm, from the property package, in kg/m3.</summary>
        Public Property Density15C As Double
        ''' <summary>Liquid density at 20 °C and 1 atm, from the property package, in kg/m3.</summary>
        Public Property Density20C As Double
        ''' <summary>Kinematic viscosity at 40 °C, in m2/s.</summary>
        Public Property KinematicViscosity40C As Double
        ''' <summary>Kinematic viscosity at 100 °C, in m2/s.</summary>
        Public Property KinematicViscosity100C As Double
        Public Property FlashPointClosedCup As Double
        Public Property FlashPointOpenCup As Double
        Public Property PourPoint As Double
        Public Property CloudPoint As Double
        Public Property FreezingPoint As Double
        Public Property AnilinePoint As Double
        ''' <summary>Smoke point, in m.</summary>
        Public Property SmokePoint As Double
        Public Property RefractionIndex As Double
        ''' <summary>Cetane index from the API gravity and the mean average boiling point (API 2B13.1).</summary>
        Public Property CetaneIndex As Double
        ''' <summary>Calculated cetane index from the D86 curve and the density (ASTM D4737, procedure A).</summary>
        Public Property CetaneIndexD4737 As Double
        Public Property TrueVaporPressure As Double
        Public Property ReidVaporPressure As Double
        ''' <summary>Sulfur content in weight percent, from the compounds' contaminant data. Zero when there is none.</summary>
        Public Property SulfurWtPct As Double

    End Class

    Public Class DistillationCurveResults

        Public Property Curves As New Dictionary(Of DistillationCurveType, DistillationCurve)

        Public Property Properties As New PetroleumProductProperties

        ''' <summary>Notes on what could not be calculated, or was calculated outside the correlations' range.</summary>
        Public Property Messages As New List(Of String)

    End Class

    ''' <summary>
    ''' Distillation curves (TBP, ASTM D86, D1160, D2887 and EFV) and product properties of a material stream.
    ''' </summary>
    ''' <remarks>
    ''' The TBP curve comes from the compounds themselves: each compound occupies its share of the
    ''' stream on the chosen basis, its normal boiling point sits at the middle of that share, and a
    ''' monotone interpolation joins the points. A stream of pseudocomponents cut from a TBP curve
    ''' therefore returns that curve. The ASTM curves are converted from the TBP curve on a liquid
    ''' volume basis with the API Technical Data Book procedures: 3A1.1 for D86, 3A3.1 for D2887 and
    ''' figure 3A2.1 with procedure 5A1.19 for D1160. The EFV curve is either flashed with the stream's
    ''' property package or converted from D86 with the Riazi-Daubert correlation.
    ''' </remarks>
    Public Class DistillationCurves

        ''' <summary>60 F, in K.</summary>
        Public Const StandardTemperature As Double = 288.706

        ''' <summary>Density of water at 60 F, in kg/m3.</summary>
        Public Const WaterDensity60F As Double = 999.016

        ''' <summary>The points the API conversions work on, in percent distilled.</summary>
        Private Shared ReadOnly D86Points As Double() = {0, 10, 30, 50, 70, 90, 100}
        Private Shared ReadOnly D2887Points As Double() = {5, 10, 30, 50, 70, 90, 95, 100}

        Private Const Patm As Double = 101325

        ''' <summary>
        ''' Calculates the distillation curves and the product properties of a stream. The stream is not changed.
        ''' </summary>
        Public Shared Function Calculate(ms As MaterialStream, Optional settings As DistillationCurveSettings = Nothing) As DistillationCurveResults

            If settings Is Nothing Then settings = New DistillationCurveSettings

            Dim res As New DistillationCurveResults

            Dim pp As PropertyPackages.PropertyPackage = ms.PropertyPackage
            Dim oldStream = pp.CurrentMaterialStream

            Dim mat = ms.ShallowClone()

            Try

                pp.CurrentMaterialStream = mat

                Dim comps = mat.Phases(0).Compounds.Values.ToList()
                Dim n = comps.Count

                Dim z(n - 1) As Double
                Dim phaseIndex = 0
                If settings.LiquidPhaseOnly Then
                    If mat.Phases(1).Properties.molarfraction.GetValueOrDefault() > 0.0 Then
                        phaseIndex = 1
                    Else
                        res.Messages.Add("The stream has no liquid phase. The curves use the whole stream.")
                    End If
                End If
                Dim i = 0
                For Each c In mat.Phases(phaseIndex).Compounds.Values
                    z(i) = c.MoleFraction.GetValueOrDefault()
                    i += 1
                Next
                Dim zsum = z.Sum()
                If zsum <= 0.0 Then Throw New ArgumentException("The stream has no composition.")
                For i = 0 To n - 1 : z(i) /= zsum : Next

                Dim nbp(n - 1), mw(n - 1), rho(n - 1) As Double
                For i = 0 To n - 1
                    Dim cp = comps(i).ConstantProperties
                    nbp(i) = cp.Normal_Boiling_Point
                    mw(i) = cp.Molar_Weight
                    rho(i) = StandardLiquidDensity(pp, cp)
                Next

                Dim vol(n - 1), mass(n - 1) As Double
                For i = 0 To n - 1
                    mass(i) = z(i) * mw(i)
                    vol(i) = mass(i) / rho(i)
                Next

                ' TBP on liquid volume, used by all the conversions
                Dim tbpv = TBPFromComposition(nbp, vol, D86Points)
                Dim tbpv8 = TBPFromComposition(nbp, vol, D2887Points)

                Dim tbpAmounts As Double()
                Select Case settings.TBPBasis
                    Case DistillationCurveBasis.Mass : tbpAmounts = mass
                    Case DistillationCurveBasis.Mole : tbpAmounts = z
                    Case Else : tbpAmounts = vol
                End Select

                res.Curves(DistillationCurveType.TBP) = New DistillationCurve With {
                    .CurveType = DistillationCurveType.TBP, .Basis = settings.TBPBasis, .Pressure = Patm,
                    .Percents = settings.Percents.ToArray(),
                    .Temperatures = TBPFromComposition(nbp, tbpAmounts, settings.Percents)}

                ' properties needed by the conversions
                Dim props = res.Properties
                props.MolecularWeight = mass.Sum()
                props.SpecificGravity = mass.Sum() / vol.Sum() / WaterDensity60F
                props.API = 141.5 / props.SpecificGravity - 131.5

                Dim volsum = vol.Sum()
                Dim mabp = 0.0, cabp = 0.0
                For i = 0 To n - 1
                    mabp += z(i) * nbp(i)
                    cabp += vol(i) / volsum * nbp(i) ^ (1 / 3)
                Next
                cabp = cabp ^ 3
                props.MeanAverageBoilingPoint = (mabp + cabp) / 2
                props.WatsonK = (1.8 * props.MeanAverageBoilingPoint) ^ (1 / 3) / props.SpecificGravity

                ' ASTM D86
                Dim d86 = DistillationCurveConversion.TBPToASTMD86_Daubert(tbpv)
                res.Curves(DistillationCurveType.ASTM_D86) = New DistillationCurve With {
                    .CurveType = DistillationCurveType.ASTM_D86, .Basis = DistillationCurveBasis.LiquidVolume, .Pressure = Patm,
                    .Percents = settings.Percents.ToArray(),
                    .Temperatures = settings.Percents.Select(Function(p) InterpolateMonotone(D86Points, d86, p)).ToArray()}
                If d86(3) - 273.15 > 316 Then
                    res.Messages.Add("The D86 50 % point is above 600 F, the upper limit of API procedure 3A1.1.")
                End If

                ' ASTM D2887 (weight percent)
                Dim sd = DistillationCurveConversion.TBPToASTMD2887_Daubert(tbpv8)
                Dim sdPoints = {0.0}.Concat(D2887Points).ToArray()
                Dim sd0 = sd(0) - (sd(1) - sd(0))
                Dim sdT = {sd0}.Concat(sd).ToArray()
                res.Curves(DistillationCurveType.ASTM_D2887) = New DistillationCurve With {
                    .CurveType = DistillationCurveType.ASTM_D2887, .Basis = DistillationCurveBasis.Mass, .Pressure = Patm,
                    .Percents = settings.Percents.ToArray(),
                    .Temperatures = settings.Percents.Select(Function(p) InterpolateMonotone(sdPoints, sdT, p)).ToArray()}

                ' ASTM D1160 at 10 mmHg, then at the pressure asked for
                Const P10 As Double = 1333.224
                Dim kw = props.WatsonK
                Dim tbp10 = tbpv.Select(Function(t) DistillationCurveConversion.PEVToPEVsub_MaxwellBonnel(t, P10, kw)).ToArray()
                Dim d1160 = DistillationCurveConversion.TBP10mmHgToASTMD1160_EdmisterOkamoto(tbp10)
                If Abs(settings.D1160Pressure - P10) > 1.0 Then
                    d1160 = d1160.Select(Function(t)
                                             Dim tnormal = DistillationCurveConversion.PEVsubToPEV_MaxwellBonnel(t, P10, kw)
                                             Return DistillationCurveConversion.PEVToPEVsub_MaxwellBonnel(tnormal, settings.D1160Pressure, kw)
                                         End Function).ToArray()
                End If
                res.Curves(DistillationCurveType.ASTM_D1160) = New DistillationCurve With {
                    .CurveType = DistillationCurveType.ASTM_D1160, .Basis = DistillationCurveBasis.LiquidVolume, .Pressure = settings.D1160Pressure,
                    .Percents = settings.Percents.ToArray(),
                    .Temperatures = settings.Percents.Select(Function(p) InterpolateMonotone(D86Points, d1160, p)).ToArray()}

                ' EFV
                Dim efv As DistillationCurve = Nothing
                If settings.EFVMethod = EFVCalculationMethod.Flash Then
                    Try
                        efv = FlashEFV(pp, z, mw, rho, settings.EFVPressure, settings.Percents)
                    Catch ex As Exception
                        res.Messages.Add("The EFV flash failed (" & ex.Message & "). The EFV curve comes from the Riazi-Daubert correlation.")
                    End Try
                End If
                If efv Is Nothing Then
                    Dim efv7 = DistillationCurveConversion.ASTMD86ToEFV_RiaziDaubert(d86, props.SpecificGravity)
                    If Abs(settings.EFVPressure - Patm) > 1.0 Then
                        efv7 = efv7.Select(Function(t) DistillationCurveConversion.PEVToPEVsub_MaxwellBonnel(t, settings.EFVPressure, kw)).ToArray()
                    End If
                    efv = New DistillationCurve With {
                        .CurveType = DistillationCurveType.EFV, .Basis = DistillationCurveBasis.LiquidVolume, .Pressure = settings.EFVPressure,
                        .Percents = settings.Percents.ToArray(),
                        .Temperatures = settings.Percents.Select(Function(p) InterpolateMonotone(D86Points, efv7, p)).ToArray()}
                End If
                res.Curves(DistillationCurveType.EFV) = efv

                ' product properties
                CalculateProperties(ms, mat, pp, z, d86, props, res)

            Finally

                pp.CurrentMaterialStream = oldStream

            End Try

            Return res

        End Function

        ''' <summary>
        ''' Density of a compound as a liquid at 60 F, in kg/m3: the specific gravity of a petroleum
        ''' fraction, or the property package's liquid density, taken at the normal boiling point for
        ''' compounds that boil below 60 F.
        ''' </summary>
        Public Shared Function StandardLiquidDensity(pp As PropertyPackages.PropertyPackage, cp As ConstantProperties) As Double

            If cp.PF_SG.GetValueOrDefault() > 0.0 AndAlso (cp.IsPF = 1 OrElse cp.IsHYPO = 1) Then
                Return cp.PF_SG.Value * WaterDensity60F
            End If

            Dim T = StandardTemperature
            If cp.Normal_Boiling_Point > 0.0 Then T = Min(T, cp.Normal_Boiling_Point)
            Dim d = pp.AUX_LIQDENSi(cp, T)
            If Double.IsNaN(d) OrElse Double.IsInfinity(d) OrElse d <= 0.0 Then
                If cp.PF_SG.GetValueOrDefault() > 0.0 Then Return cp.PF_SG.Value * WaterDensity60F
                Throw New ArgumentException("No liquid density for " & cp.Name & ".")
            End If
            Return d

        End Function

        ''' <summary>
        ''' TBP temperatures at the given percents from the compounds' normal boiling points and amounts.
        ''' </summary>
        ''' <param name="nbp">Normal boiling points, in K.</param>
        ''' <param name="amounts">Amounts on the curve basis (liquid volume, mass or moles), in any scale.</param>
        ''' <param name="percents">Percents distilled, from 0 to 100.</param>
        ''' <returns>Temperatures in K.</returns>
        Public Shared Function TBPFromComposition(nbp As Double(), amounts As Double(), percents As IEnumerable(Of Double)) As Double()

            Dim items = Enumerable.Range(0, nbp.Length).
                Where(Function(k) amounts(k) > 0.0).
                Select(Function(k) New With {.T = nbp(k), .A = amounts(k)}).
                OrderBy(Function(it) it.T).ToList()

            If items.Count = 0 Then Throw New ArgumentException("The stream has no composition.")

            Dim total = items.Sum(Function(it) it.A)
            Dim x As New List(Of Double), y As New List(Of Double)
            Dim cum = 0.0
            For Each it In items
                Dim xm = 100 * (cum + it.A / 2) / total
                cum += it.A
                ' traces share the percent of their neighbour: keep one point per percent
                If x.Count > 0 AndAlso xm - x(x.Count - 1) < 0.000000001 Then
                    y(y.Count - 1) = it.T
                    Continue For
                End If
                x.Add(xm)
                y.Add(it.T)
            Next

            Return percents.Select(Function(p) InterpolateMonotone(x.ToArray(), y.ToArray(), p)).ToArray()

        End Function

        ''' <summary>
        ''' Monotone cubic (Fritsch-Carlson) interpolation. Outside the points it extrapolates with the
        ''' slope of the nearest segment.
        ''' </summary>
        Public Shared Function InterpolateMonotone(x As Double(), y As Double(), xi As Double) As Double

            Dim n = x.Length
            If n = 1 Then Return y(0)

            If xi <= x(0) Then Return y(0) + (y(1) - y(0)) / (x(1) - x(0)) * (xi - x(0))
            If xi >= x(n - 1) Then Return y(n - 1) + (y(n - 1) - y(n - 2)) / (x(n - 1) - x(n - 2)) * (xi - x(n - 1))

            Dim d(n - 2), m(n - 1) As Double
            For k = 0 To n - 2
                d(k) = (y(k + 1) - y(k)) / (x(k + 1) - x(k))
            Next
            m(0) = d(0)
            m(n - 1) = d(n - 2)
            For k = 1 To n - 2
                If d(k - 1) * d(k) <= 0 Then
                    m(k) = 0
                Else
                    Dim w1 = 2 * (x(k + 1) - x(k)) + (x(k) - x(k - 1))
                    Dim w2 = (x(k + 1) - x(k)) + 2 * (x(k) - x(k - 1))
                    m(k) = (w1 + w2) / (w1 / d(k - 1) + w2 / d(k))
                End If
            Next

            Dim j = 0
            While xi > x(j + 1) : j += 1 : End While

            Dim h = x(j + 1) - x(j)
            Dim t = (xi - x(j)) / h
            Dim h00 = (1 + 2 * t) * (1 - t) ^ 2
            Dim h10 = t * (1 - t) ^ 2
            Dim h01 = t ^ 2 * (3 - 2 * t)
            Dim h11 = t ^ 2 * (t - 1)

            Return h00 * y(j) + h10 * h * m(j) + h01 * y(j + 1) + h11 * h * m(j + 1)

        End Function

        ''' <summary>Linear interpolation, extrapolated with the slope of the nearest segment.</summary>
        Public Shared Function InterpolateLinear(x As Double(), y As Double(), xi As Double) As Double

            Dim n = x.Length
            If n = 0 Then Return Double.NaN
            If n = 1 Then Return y(0)
            Dim j = 0
            If xi > x(0) Then
                j = n - 2
                For k = 0 To n - 2
                    If xi <= x(k + 1) Then j = k : Exit For
                Next
            End If
            Return y(j) + (y(j + 1) - y(j)) / (x(j + 1) - x(j)) * (xi - x(j))

        End Function

        ''' <summary>
        ''' EFV curve by flash: at each percent, the temperature where the vapour is that percent of the
        ''' stream's standard liquid volume.
        ''' </summary>
        Private Shared Function FlashEFV(pp As PropertyPackages.PropertyPackage, z As Double(), mw As Double(), rho As Double(),
                                         P As Double, percents As Double()) As DistillationCurve

            Dim n = z.Length
            Dim vtot = 0.0
            For i = 0 To n - 1 : vtot += z(i) * mw(i) / rho(i) : Next

            Dim tbub = Convert.ToDouble(pp.DW_CalcBubT(z, P)(4))
            Dim tdew = Convert.ToDouble(pp.DW_CalcDewT(z, P)(4))
            If Double.IsNaN(tbub) OrElse Double.IsNaN(tdew) OrElse tdew < tbub Then
                Throw New Exception("bubble and dew points not found")
            End If

            Dim vapourVolume = Function(T As Double) As Double
                                   Dim r = pp.FlashBase.Flash_PT(z, P, T, pp)
                                   Dim V = Convert.ToDouble(r(1))
                                   Dim y = DirectCast(r(3), Double())
                                   Dim vv = 0.0
                                   For k = 0 To n - 1 : vv += V * y(k) * mw(k) / rho(k) : Next
                                   Return vv / vtot
                               End Function

            Dim temps As New List(Of Double)
            For Each pct In percents
                Dim f = pct / 100
                If f <= 0.0 Then
                    temps.Add(tbub)
                ElseIf f >= 1.0 Then
                    temps.Add(tdew)
                Else
                    temps.Add(SolveIllinois(Function(T) vapourVolume(T) - f, tbub, tdew, -f, 1 - f))
                End If
            Next

            Return New DistillationCurve With {
                .CurveType = DistillationCurveType.EFV, .Basis = DistillationCurveBasis.LiquidVolume, .Pressure = P,
                .Percents = percents.ToArray(), .Temperatures = temps.ToArray()}

        End Function

        ''' <summary>Root of an increasing function inside a bracket, by the Illinois variant of regula falsi.</summary>
        Private Shared Function SolveIllinois(f As Func(Of Double, Double), a As Double, b As Double, fa As Double, fb As Double) As Double

            Dim side = 0
            For iter = 1 To 100
                Dim c = (a * fb - b * fa) / (fb - fa)
                Dim fc = f(c)
                If Abs(fc) < 0.00001 OrElse Abs(b - a) < 0.001 Then Return c
                If Sign(fc) = Sign(fb) Then
                    b = c : fb = fc
                    If side = -1 Then fa /= 2
                    side = -1
                Else
                    a = c : fa = fc
                    If side = 1 Then fb /= 2
                    side = 1
                End If
            Next
            Return (a + b) / 2

        End Function

        Private Shared Sub CalculateProperties(source As MaterialStream, mat As MaterialStream, pp As PropertyPackages.PropertyPackage,
                                               z As Double(), d86 As Double(), props As PetroleumProductProperties,
                                               res As DistillationCurveResults)

            Dim liquidProp = Function(prop As String, T As Double) As Double
                                 mat.Phases(0).Properties.temperature = T
                                 mat.Phases(0).Properties.pressure = Patm
                                 Dim k = 0
                                 For Each c In mat.Phases(1).Compounds.Values
                                     c.MoleFraction = z(k)
                                     k += 1
                                 Next
                                 pp.DW_CalcProp(prop, PropertyPackages.Phase.Liquid)
                                 If prop = "density" Then Return mat.Phases(1).Properties.density.GetValueOrDefault()
                                 Return mat.Phases(1).Properties.viscosity.GetValueOrDefault()
                             End Function

            ' densities are anchored on the standard liquid density of the compounds (their specific gravity, what
            ' the assay measured); the property package gives only how the density changes with temperature, as its
            ' liquid density for heavy pseudocomponents can be far off (a residue at 1200 kg/m3 for SG 0.94)
            Dim rhoScale = 1.0
            Try
                Dim rho60 = liquidProp("density", 288.7056)
                If props.SpecificGravity > 0.0 AndAlso rho60 > 0.0 Then rhoScale = props.SpecificGravity * WaterDensity60F / rho60
            Catch
            End Try
            Dim liquidDensity = Function(T As Double) liquidProp("density", T) * rhoScale

            Try
                props.Density15C = liquidDensity(288.15)
                props.Density20C = liquidDensity(293.15)
                props.KinematicViscosity40C = liquidProp("viscosity", 313.15) / liquidDensity(313.15)
                props.KinematicViscosity100C = liquidProp("viscosity", 373.15) / liquidDensity(373.15)
            Catch ex As Exception
                res.Messages.Add("Liquid density and viscosity could not be calculated (" & ex.Message & ").")
            End Try

            Dim kv38 = 0.0
            Try
                kv38 = liquidProp("viscosity", 310.95) / liquidDensity(310.95)
            Catch
            End Try

            Try
                props.TrueVaporPressure = Convert.ToDouble(pp.DW_CalcBubP(z, 310.95, Patm)(4))
                props.ReidVaporPressure = PetroleumCorrelations.ReidVaporPressure(props.TrueVaporPressure)
            Catch ex As Exception
                res.Messages.Add("The vapour pressure could not be calculated (" & ex.Message & ").")
            End Try

            Dim meabp = props.MeanAverageBoilingPoint
            Dim sg = props.SpecificGravity
            Dim kw = props.WatsonK

            props.FlashPointClosedCup = PetroleumCorrelations.FlashPointClosedCup(d86(1))
            props.FlashPointOpenCup = PetroleumCorrelations.FlashPointOpenCup(d86(1))
            props.PourPoint = PetroleumCorrelations.PourPoint(sg, meabp, kv38)
            props.FreezingPoint = PetroleumCorrelations.FreezingPoint(sg, meabp, kw)
            props.CloudPoint = PetroleumCorrelations.CloudPoint(sg, meabp)
            props.AnilinePoint = PetroleumCorrelations.AnilinePoint(sg, meabp, kw)
            props.SmokePoint = PetroleumCorrelations.SmokePoint(meabp, kw)
            props.RefractionIndex = PetroleumCorrelations.RefractionIndex(sg, meabp)
            props.CetaneIndex = PetroleumCorrelations.CetaneIndex(props.API, meabp)
            If props.Density15C > 0.0 Then
                props.CetaneIndexD4737 = PetroleumCorrelations.CetaneIndexD4737(d86(1), d86(3), d86(5), props.Density15C)
            End If

            ' sulfur, mass weighted from the compounds' contaminant data
            Dim sulfur = 0.0, wsum = 0.0, k2 = 0
            For Each c In source.Phases(0).Compounds.Values
                Dim w = z(k2) * c.ConstantProperties.Molar_Weight
                sulfur += w * CompoundContaminants.Get(c, CompoundContaminants.K_WtPctSulfur)
                wsum += w
                k2 += 1
            Next
            If wsum > 0.0 Then props.SulfurWtPct = sulfur / wsum

        End Sub

    End Class

    ''' <summary>
    ''' API Technical Data Book correlations for petroleum product properties (chapter 2), in SI units.
    ''' </summary>
    Public Class PetroleumCorrelations

        Private Shared Function KToF(T As Double) As Double
            Return (T - 273.15) * 9 / 5 + 32
        End Function

        Private Shared Function FToK(T As Double) As Double
            Return (T - 32) * 5 / 9 + 273.15
        End Function

        ''' <summary>Pensky-Martens closed cup flash point (ASTM D93), API 2B7.1, from the D86 10 % temperature. K.</summary>
        Public Shared Function FlashPointClosedCup(T10D86 As Double) As Double
            Return FToK(0.69 * KToF(T10D86) - 118.2)
        End Function

        ''' <summary>Cleveland open cup flash point (ASTM D92), API 2B7.1, from the D86 10 % temperature. K.</summary>
        Public Shared Function FlashPointOpenCup(T10D86 As Double) As Double
            Return FToK(0.68 * KToF(T10D86) - 109.6)
        End Function

        ''' <summary>Pour point, API 2B8.1. MeABP in K, kinematic viscosity at 100 F in m2/s. K.</summary>
        Public Shared Function PourPoint(SG As Double, MeABP As Double, KinViscosity100F As Double) As Double
            ' the correlation was fitted on distillates with viscosities at 100 F up to about 1000 cSt
            If Not (KinViscosity100F > 0.0 AndAlso KinViscosity100F < 0.001) Then Return Double.NaN
            Return (753 + 136 * (1 - Exp(-0.15 * KinViscosity100F * 1000000.0)) - 572 * SG + 0.0512 * KinViscosity100F * 1000000.0 + 0.139 * (MeABP * 1.8)) / 1.8
        End Function

        ''' <summary>Freezing point, API 2B11.1. K.</summary>
        Public Shared Function FreezingPoint(SG As Double, MeABP As Double, Kw As Double) As Double
            Return (-2390.42 + 1826 * SG + 122.49 * Kw - 0.135 * 1.8 * MeABP) / 1.8
        End Function

        ''' <summary>Cloud point, API 2B12.1. K.</summary>
        Public Shared Function CloudPoint(SG As Double, MeABP As Double) As Double
            Return (10 ^ (-7.41 + 5.49 * Log10(MeABP * 1.8) - 0.712 * (1.8 * MeABP) ^ 0.315 - 0.133 * SG)) / 1.8
        End Function

        ''' <summary>Aniline point, API 2B9.1. K.</summary>
        Public Shared Function AnilinePoint(SG As Double, MeABP As Double, Kw As Double) As Double
            Return (-1253.7 - 0.139 * (1.8 * MeABP) + 107.8 * Kw + 868.7 * SG) / 1.8
        End Function

        ''' <summary>Smoke point, API 2B10.1. m.</summary>
        Public Shared Function SmokePoint(MeABP As Double, Kw As Double) As Double
            Return Exp(-1.028 + 0.474 * Kw - 0.00168 * (1.8 * MeABP)) / 1000
        End Function

        ''' <summary>Refractive index at 20 °C, API 2B5.1 (Huang).</summary>
        Public Shared Function RefractionIndex(SG As Double, MeABP As Double) As Double
            Dim Huang_I = 0.02266 * Exp(0.0003905 * (1.8 * MeABP) + 2.468 * SG - 0.0005704 * (1.8 * MeABP) * SG) * (1.8 * MeABP) ^ 0.0572 * SG ^ -0.72
            Return ((1 + 2 * Huang_I) / (1 - Huang_I)) ^ 0.5
        End Function

        ''' <summary>Cetane index, API 2B13.1, from the API gravity and the mean average boiling point (K).</summary>
        Public Shared Function CetaneIndex(API As Double, MeABP As Double) As Double
            Return 415.26 - 7.673 * API + 0.186 * (MeABP * 1.8 - 459.67) + 3.503 * API * Log10(1.8 * MeABP - 459.67) - 193.816 * Log10(MeABP * 1.8 - 459.67)
        End Function

        ''' <summary>
        ''' Calculated cetane index by the four-variable equation (ASTM D4737, procedure A), from the D86 10, 50
        ''' and 90 % temperatures (K) and the density at 15 °C (kg/m3).
        ''' </summary>
        Public Shared Function CetaneIndexD4737(T10 As Double, T50 As Double, T90 As Double, Density15C As Double) As Double
            Dim DN = Density15C / 1000 - 0.85
            Dim B = Exp(-3.5 * DN) - 1
            Dim T10N = T10 - 273.15 - 215
            Dim T50N = T50 - 273.15 - 260
            Dim T90N = T90 - 273.15 - 310
            Return 45.2 + 0.0892 * T10N + (0.131 + 0.901 * B) * T50N + (0.0523 - 0.42 * B) * T90N +
                0.00049 * (T10N ^ 2 - T90N ^ 2) + 107 * B + 60 * B ^ 2
        End Function

        ''' <summary>
        ''' Reid vapor pressure from the true vapor pressure at 100 F, both in Pa (EPA AP-42, chapter 7, page 7.1-82).
        ''' </summary>
        Public Shared Function ReidVaporPressure(TVP As Double) As Double
            Return (10 ^ ((Log(TVP / 6894.76) + 7261 / (310.95 * 1.8) - 12.82) / (2799 / (310.95 * 1.8) - 2.227))) * 6894.76
        End Function

    End Class

End Namespace
