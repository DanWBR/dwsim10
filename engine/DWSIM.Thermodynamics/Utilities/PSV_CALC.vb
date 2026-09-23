Namespace Utilities.PSV

    ''' <summary>
    ''' Pressure relief valve orifice sizing by API RP 520 Part I.
    ''' </summary>
    ''' <remarks>
    ''' <para>Pressure convention. The SI methods (<see cref="GasArea"/>, <see cref="LiquidArea"/>,
    ''' <see cref="TwoPhaseArea"/>) take absolute pressures in Pa. P1 (P0 in the omega method) is the
    ''' relieving pressure as API 520 defines it: set pressure (gauge) plus the allowable overpressure
    ''' plus the atmospheric pressure. <see cref="RelievingPressure"/> builds it from an absolute set
    ''' pressure.</para>
    ''' <para>The PSV_* methods are the older interface and keep their signatures: pressures in
    ''' kgf/cm2 GAUGE, converted to absolute by adding 1.033 kgf/cm2, then passed to the SI methods.
    ''' Never pass an absolute pressure to them.</para>
    ''' <para>All areas are returned in square inches, the unit of the API 526 orifice table
    ''' (<see cref="ORIF_API"/>).</para>
    ''' </remarks>
    <System.Serializable()> Public Class Sizing

        ''' <summary>Atmospheric pressure used to go from gauge to absolute pressure, Pa.</summary>
        Public Const AtmosphericPressure As Double = 101325.0

        ''' <summary>Pa per kgf/cm2 as the older interface counts it (1.033 kgf/cm2 = 1 atm).</summary>
        Private Const PaPerKgfCm2 As Double = 101325.0 / 1.033

        Private Const SquareMillimetresPerSquareInch As Double = 645.16

        Sub New()

        End Sub

        ''' <summary>
        ''' API 520 relieving pressure P1 = set pressure (gauge) x (1 + overpressure) + atmospheric pressure.
        ''' </summary>
        ''' <param name="setPressure">Set pressure, Pa ABSOLUTE.</param>
        ''' <param name="overpressurePercent">Allowable overpressure, % of the gauge set pressure.</param>
        ''' <returns>Relieving pressure, Pa absolute.</returns>
        Public Shared Function RelievingPressure(setPressure As Double, overpressurePercent As Double) As Double
            Return AtmosphericPressure + (setPressure - AtmosphericPressure) * (1.0 + overpressurePercent / 100.0)
        End Function

        ''' <summary>
        ''' Returns a warning when the vapour is far from the ideal gas the API 520 gas equation assumes,
        ''' or Nothing when it is close enough.
        ''' </summary>
        ''' <remarks>
        ''' The gas equation (API 520 Part I) takes k as an ideal-gas ratio of specific heats. The ratio
        ''' DWSIM reports is the real Cp/Cv, which near the critical point can be well above any ideal-gas
        ''' value (a natural gas at 240 K and 70 bar gives Cp/Cv of about 2.9 with Z of 0.6). The result
        ''' then needs a check by the direct integration (HDI) method of API 520 Annex B.
        ''' </remarks>
        Public Shared Function IdealGasWarning(Z As Double, k As Double) As String
            If Z < 0.8 OrElse Z > 1.1 OrElse k < 1.0 OrElse k > 1.7 Then
                Return String.Format(Globalization.CultureInfo.InvariantCulture,
                    "The relieved vapour has Z = {0:0.00} and Cp/Cv = {1:0.00}. The API 520 gas equation assumes an ideal gas with an ideal-gas k, and this state is far from one. " &
                    "Check the orifice area with the direct integration (HDI) method of API 520 Annex B.", Z, k)
            End If
            Return Nothing
        End Function

        ''' <summary>
        ''' Required effective discharge area for gas or vapour relief (API 520 Part I, critical and subcritical
        ''' flow equations). The regime is chosen from the critical flow pressure.
        ''' </summary>
        ''' <param name="P1">Relieving pressure, Pa absolute (set + overpressure + atmospheric).</param>
        ''' <param name="P2">Total back pressure, Pa absolute.</param>
        ''' <param name="T">Relieving temperature, K.</param>
        ''' <param name="W">Relief rate, kg/s.</param>
        ''' <param name="Z">Compressibility factor at the inlet.</param>
        ''' <param name="M">Molar mass, kg/kmol.</param>
        ''' <param name="k">Ratio of specific heats, ideal-gas value.</param>
        ''' <param name="Kd">Effective discharge coefficient.</param>
        ''' <param name="Kb">Back pressure correction factor (critical flow only).</param>
        ''' <param name="Kc">Rupture disk combination factor.</param>
        ''' <returns>Area, in2.</returns>
        Public Shared Function GasArea(P1 As Double, P2 As Double, T As Double, W As Double, Z As Double, M As Double, k As Double,
                                       Kd As Double, Kb As Double, Kc As Double) As Double

            ' the equations are singular at k = 1; no real gas has Cp/Cv below one.
            If k < 1.001 Then k = 1.001

            Dim p1kPa = P1 / 1000.0
            Dim p2kPa = P2 / 1000.0
            Dim Wkgh = W * 3600.0

            Dim A As Double ' mm2

            If P2 <= CriticalFlowPressure(P1, k) Then
                Dim C = 0.03948 * Math.Sqrt(k * (2.0 / (k + 1.0)) ^ ((k + 1.0) / (k - 1.0)))
                A = Wkgh / (C * Kd * p1kPa * Kb * Kc) * Math.Sqrt(T * Z / M)
            Else
                Dim r = P2 / P1
                Dim F2 = Math.Sqrt(k / (k - 1.0) * r ^ (2.0 / k) * (1.0 - r ^ ((k - 1.0) / k)) / (1.0 - r))
                A = 17.9 * Wkgh / (F2 * Kd * Kc) * Math.Sqrt(Z * T / (M * p1kPa * (p1kPa - p2kPa)))
            End If

            Return A / SquareMillimetresPerSquareInch

        End Function

        ''' <summary>Critical flow pressure P1 (2 / (k + 1))^(k / (k - 1)), same units as P1 (absolute).</summary>
        Public Shared Function CriticalFlowPressure(P1 As Double, k As Double) As Double
            If k < 1.001 Then k = 1.001
            Return P1 * (2.0 / (k + 1.0)) ^ (k / (k - 1.0))
        End Function

        ''' <summary>
        ''' Required effective discharge area for liquid relief with a capacity-certified valve (API 520 Part I),
        ''' including the viscosity correction Kv.
        ''' </summary>
        ''' <param name="Q">Flow rate at the flowing temperature, m3/s.</param>
        ''' <param name="P1">Relieving pressure, Pa absolute (only P1 - P2 enters the equation).</param>
        ''' <param name="P2">Total back pressure, Pa absolute.</param>
        ''' <param name="rho">Liquid density, kg/m3 (the specific gravity is rho / 1000).</param>
        ''' <param name="mu">Liquid viscosity, Pa.s. Zero skips the viscosity correction.</param>
        ''' <param name="Kd">Effective discharge coefficient.</param>
        ''' <param name="Kw">Back pressure correction factor for balanced bellows valves (1 for conventional valves).</param>
        ''' <param name="Kc">Rupture disk combination factor.</param>
        ''' <returns>Area, in2.</returns>
        Public Shared Function LiquidArea(Q As Double, P1 As Double, P2 As Double, rho As Double, mu As Double,
                                          Kd As Double, Kw As Double, Kc As Double) As Double

            Dim QLmin = Q * 60000.0
            Dim G = rho / 1000.0
            Dim dPkPa = (P1 - P2) / 1000.0

            Dim AR = 11.78 * QLmin / (Kd * Kw * Kc) * Math.Sqrt(G / dPkPa)

            Return ViscosityCorrectedArea(AR, QLmin, G, mu * 1000.0) / SquareMillimetresPerSquareInch

        End Function

        ''' <summary>
        ''' Applies the API 520 viscosity correction to a liquid area: take the next larger API 526 orifice, compute
        ''' the Reynolds number through it, correct the area by Kv and repeat with the next orifice while the corrected
        ''' area does not fit.
        ''' </summary>
        ''' <param name="AR">Area without viscosity correction, mm2.</param>
        ''' <param name="QLmin">Flow rate, L/min.</param>
        ''' <param name="G">Specific gravity.</param>
        ''' <param name="mucP">Viscosity, cP.</param>
        ''' <returns>Corrected area, mm2.</returns>
        Private Shared Function ViscosityCorrectedArea(AR As Double, QLmin As Double, G As Double, mucP As Double) As Double

            If Not (mucP > 0.0) OrElse Double.IsNaN(AR) OrElse AR <= 0.0 Then Return AR

            Dim A = AR

            For i = 1 To 50
                ' beyond the largest API 526 orifice the Reynolds number is taken on the area itself
                Dim orifice = StandardOrifice(A / SquareMillimetresPerSquareInch).Item2 * SquareMillimetresPerSquareInch
                Dim Aref = If(orifice > 0.0, orifice, A)
                Dim R = QLmin * 18800.0 * G / (mucP * Math.Sqrt(Aref))
                Dim Kv = 1.0 / (0.9935 + 2.878 / R ^ 0.5 + 342.75 / R ^ 1.5)
                Dim Anew = AR / Kv
                If orifice > 0.0 Then
                    If Anew <= orifice Then Return Anew
                Else
                    If Math.Abs(Anew - A) <= 0.000001 * A Then Return Anew
                End If
                A = Anew
            Next

            Return A

        End Function

        ''' <summary>
        ''' Omega method for two-phase flow (API 520 Part I, Annex D, omega from the specific volume at 90 % of
        ''' the inlet pressure).
        ''' </summary>
        ''' <param name="v0">Two-phase specific volume at the valve inlet, m3/kg.</param>
        ''' <param name="v9">Specific volume at 90 % of the inlet pressure after an isentropic (or isenthalpic) flash, m3/kg.</param>
        ''' <param name="P0">Relieving pressure, Pa absolute (set + overpressure + atmospheric).</param>
        ''' <param name="Pa">Back pressure, Pa absolute.</param>
        ''' <param name="W">Relief rate, kg/s.</param>
        ''' <param name="Kd">Discharge coefficient.</param>
        ''' <param name="Kb">Back pressure correction factor.</param>
        ''' <param name="Kc">Rupture disk combination factor.</param>
        ''' <returns>{area in2, omega, critical pressure ratio, mass flux kg/(s m2), 1 if the flow is critical else 0}.
        ''' The area is NaN when omega is not positive (the mixture does not expand).</returns>
        Public Shared Function TwoPhaseArea(v0 As Double, v9 As Double, P0 As Double, Pa As Double, W As Double,
                                            Kd As Double, Kb As Double, Kc As Double) As Double()

            Dim omega = 9.0 * (v9 / v0 - 1.0)

            If Not (omega > 0.0) Then Return New Double() {Double.NaN, omega, Double.NaN, Double.NaN, 0.0}

            Dim etac = OmegaCriticalPressureRatio(omega)

            Dim G As Double
            Dim critical = etac * P0 >= Pa

            If critical Then
                G = etac * Math.Sqrt(P0 / (v0 * omega))
            Else
                Dim eta = Pa / P0
                G = Math.Sqrt(-2.0 * (omega * Math.Log(eta) + (omega - 1.0) * (1.0 - eta))) * Math.Sqrt(P0 / v0) /
                    (omega * (1.0 / eta - 1.0) + 1.0)
            End If

            Dim A = W / (Kd * Kb * Kc * G) * 1000000.0 / SquareMillimetresPerSquareInch

            Return New Double() {A, omega, etac, G, If(critical, 1.0, 0.0)}

        End Function

        ''' <summary>
        ''' Critical pressure ratio of the omega method, the root in (0, 1) of
        ''' eta^2 + (w^2 - 2w)(1 - eta)^2 + 2w^2 ln(eta) + 2w^2 (1 - eta) = 0.
        ''' </summary>
        Public Shared Function OmegaCriticalPressureRatio(omega As Double) As Double

            Dim f = Function(eta As Double) eta * eta + (omega * omega - 2.0 * omega) * (1.0 - eta) ^ 2 +
                                            2.0 * omega * omega * Math.Log(eta) + 2.0 * omega * omega * (1.0 - eta)

            ' f goes to minus infinity at 0 and f(1) = 1, so the bracket always holds a root
            Dim lo = 0.000000000001, hi = 1.0
            For i = 1 To 200
                Dim mid = 0.5 * (lo + hi)
                If f(mid) < 0.0 Then lo = mid Else hi = mid
                If hi - lo < 0.000000000001 Then Exit For
            Next

            Return 0.5 * (lo + hi)

        End Function

        ''' <summary>
        ''' Specific volumes for the omega method from a flowsheet stream: v0 of the stream as it is and v9 after an
        ''' isentropic flash to 90 % of its pressure, as API 520 Annex D recommends. The stream is left untouched.
        ''' </summary>
        ''' <returns>{v0, v9}, m3/kg.</returns>
        Public Shared Function OmegaSpecificVolumes(stream As Global.DWSIM.Thermodynamics.Streams.MaterialStream) As Double()

            Dim props = stream.Phases(0).Properties
            Dim v0 = 1.0 / props.density.GetValueOrDefault()

            Dim pp = stream.PropertyPackage
            Dim previous = pp.CurrentMaterialStream

            Dim clone = DirectCast(stream.Clone(), Global.DWSIM.Thermodynamics.Streams.MaterialStream)
            Try
                clone.PropertyPackage = pp
                clone.SpecType = Interfaces.Enums.StreamSpec.Pressure_and_Entropy
                clone.Phases(0).Properties.temperature = props.temperature.GetValueOrDefault()
                clone.Phases(0).Properties.pressure = 0.9 * props.pressure.GetValueOrDefault()
                clone.Phases(0).Properties.entropy = props.entropy.GetValueOrDefault()
                pp.CurrentMaterialStream = clone
                clone.Calculate(True, True)
                Dim v9 = 1.0 / clone.Phases(0).Properties.density.GetValueOrDefault()
                Return New Double() {v0, v9}
            Finally
                pp.CurrentMaterialStream = previous
            End Try

        End Function

        ''' <summary>
        ''' Liquid relief with a valve that does not require capacity certification (API 520 Part I). Older interface.
        ''' </summary>
        ''' <param name="Q">Flow rate, m3/d.</param>
        ''' <param name="P">SET pressure, kgf/cm2 gauge (the overpressure enters through Kp).</param>
        ''' <param name="BP">Back pressure, kgf/cm2 gauge.</param>
        ''' <param name="G">Density, kg/m3.</param>
        ''' <param name="visc">Viscosity, cP.</param>
        ''' <param name="OP_P">Overpressure, %.</param>
        ''' <returns>Area, in2.</returns>
        Function PSV_LNCC_D(ByVal Q, ByVal P, ByVal BP, ByVal G, ByVal visc, ByVal Kd, ByVal Kc, ByVal OP_P)

            Dim Kp As Double

            ' gauge kgf/cm2 to gauge kPa
            Dim pkPa = P * PaPerKgfCm2 / 1000.0
            Dim bpkPa = BP * PaPerKgfCm2 / 1000.0

            Dim QLmin = Q / 86400.0 * 60000.0
            Dim SG = G / 1000.0

            If OP_P < 25 Then
                Kp = 0.00009 * OP_P ^ 3 - 0.006 * OP_P ^ 2 + 0.1457 * OP_P - 0.35
            Else
                Kp = 0.004 * OP_P + 0.9
            End If

            Dim AR = 11.78 * QLmin / (Kd * Kc * Kp) * Math.Sqrt(SG / (1.25 * pkPa - bpkPa))

            Return ViscosityCorrectedArea(AR, QLmin, SG, visc) / SquareMillimetresPerSquareInch

        End Function

        ''' <summary>
        ''' Older interface of <see cref="LiquidArea"/> (Kw = 1).
        ''' </summary>
        ''' <param name="Q">Flow rate, m3/d.</param>
        ''' <param name="P">Relieving pressure (set + overpressure), kgf/cm2 GAUGE.</param>
        ''' <param name="BP">Back pressure, kgf/cm2 GAUGE.</param>
        ''' <param name="G">Density, kg/m3.</param>
        ''' <param name="visc">Viscosity, cP.</param>
        ''' <returns>Area, in2.</returns>
        Function PSV_LCC_D(ByVal Q, ByVal P, ByVal BP, ByVal G, ByVal visc, ByVal Kd, ByVal Kc)

            Return LiquidArea(Q / 86400.0, GaugeKgfCm2ToPa(P), GaugeKgfCm2ToPa(BP), G, visc / 1000.0, Kd, 1.0, Kc)

        End Function

        ''' <summary>
        ''' Older interface of <see cref="GasArea"/>.
        ''' </summary>
        ''' <param name="P">Relieving pressure (set + overpressure), kgf/cm2 GAUGE.</param>
        ''' <param name="BP">Back pressure, kgf/cm2 GAUGE.</param>
        ''' <param name="T">Temperature, K.</param>
        ''' <param name="W">Relief rate, kg/h.</param>
        ''' <returns>Area, in2.</returns>
        Function PSV_G_D(ByVal P, ByVal BP, ByVal T, ByVal W, ByVal Z, ByVal M, ByVal k, ByVal Kd, ByVal Kb, ByVal Kc)

            Return GasArea(GaugeKgfCm2ToPa(P), GaugeKgfCm2ToPa(BP), T, W / 3600.0, Z, M, k, Kd, Kb, Kc)

        End Function

        ''' <summary>
        ''' Older interface of <see cref="TwoPhaseArea"/>.
        ''' </summary>
        ''' <param name="x">Vapour mass fraction.</param>
        ''' <param name="rhog">Vapour density, kg/m3.</param>
        ''' <param name="rhom">Mixture density, kg/m3.</param>
        ''' <param name="rhom90">Mixture density at 90 % of the pressure, kg/m3.</param>
        ''' <param name="P">Relieving pressure (set + overpressure), kgf/cm2 GAUGE.</param>
        ''' <param name="BP">Back pressure, kgf/cm2 GAUGE.</param>
        ''' <param name="Q">MASS flow rate of the mixture, kg/h.</param>
        ''' <returns>{area in2, x, vg0 ft3/lb, v0 ft3/lb, v9 ft3/lb, alpha0, omega, G lb/(s ft2)}.</returns>
        Function PSV_GL_D23_D(ByVal x, ByVal rhog, ByVal rhom, ByVal rhom90, ByVal P, ByVal BP, ByVal Q, ByVal Kd, ByVal Kb, ByVal Kc)

            Const ft3lbPerM3kg As Double = 16.0185

            Dim res = TwoPhaseArea(1.0 / rhom, 1.0 / rhom90, GaugeKgfCm2ToPa(P), GaugeKgfCm2ToPa(BP), Q / 3600.0, Kd, Kb, Kc)

            Dim vg0 = 1.0 / rhog * ft3lbPerM3kg
            Dim v0 = 1.0 / rhom * ft3lbPerM3kg
            Dim v9 = 1.0 / rhom90 * ft3lbPerM3kg

            Dim tmp(7)

            tmp(0) = res(0)
            tmp(1) = x
            tmp(2) = vg0
            tmp(3) = v0
            tmp(4) = v9
            tmp(5) = x * vg0 / v0
            tmp(6) = res(1)
            tmp(7) = res(3) * 0.204816 ' kg/(s m2) to lb/(s ft2)

            Return tmp

        End Function

        Private Shared Function GaugeKgfCm2ToPa(P As Double) As Double
            Return (P + 1.033) * PaPerKgfCm2
        End Function

        ''' <summary>
        ''' Smallest API 526 standard orifice that holds the area.
        ''' </summary>
        ''' <param name="A">Area, in2.</param>
        ''' <returns>{Nothing, letter, area in2}; letter "" and area 0 above the T orifice.</returns>
        Function ORIF_API(ByVal A)

            Dim o = StandardOrifice(Convert.ToDouble(A))

            Dim tmp(2)

            tmp(1) = o.Item1
            tmp(2) = o.Item2

            Return tmp

        End Function

        ''' <summary>Smallest API 526 standard orifice that holds the area (in2): letter and area, or ("", 0) above T.</summary>
        Public Shared Function StandardOrifice(A As Double) As Tuple(Of String, Double)

            Dim letters = New String() {"D", "E", "F", "G", "H", "J", "K", "L", "M", "N", "P", "Q", "R", "T"}
            Dim areas = New Double() {0.11, 0.196, 0.307, 0.503, 0.785, 1.287, 1.838, 2.853, 3.6, 4.34, 6.38, 11.05, 16.0, 26.0}

            For i = 0 To areas.Length - 1
                If A <= areas(i) Then Return Tuple.Create(letters(i), areas(i))
            Next

            Return Tuple.Create("", 0.0)

        End Function

    End Class

End Namespace
