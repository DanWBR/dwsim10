'  ---------------------------------------------------------------------------
'  Petalas & Aziz (1997) mechanistic model for stabilized multiphase flow
'  in pipes - managed VB.NET port of the original FORTRAN 77 source
'  (PETAZ.FOR v2.01, 10/13/97, Stanford University Petroleum Engineering Dept.)
'
'       Petalas, N., and Aziz, K., "A Mechanistic Model for Stabilized
'       Multiphase Flow in Pipes", Stanford University, August 1997.
'
'  This is a line-by-line transliteration. The FORTRAN COMMON blocks
'  (/MFDATA/, /MBLNCE/, /MECH/, /TDPAR/, /FrTRN/) are reproduced as instance
'  fields, so the inter-routine side effects the original model relies on are
'  preserved exactly. Routines that wrote to COMMON still write to the same
'  fields here.
'
'  Differences from the FORTRAN, all deliberate:
'    - REAL (single precision) is carried as Double.
'    - Local variables that had FORTRAN static storage are ordinary locals,
'      except pPetAz's "eG", which is kept static because the DisBB branch
'      reads it before assigning it (see PPetAz).
'    - The "Root could not be bracketed" console message becomes LastWarning.
'    - VsGfrTr carries a recursion guard (RecursionLimit) so a pathological
'      Froth/Froth transition cannot overflow the stack inside a host process.
'
'  NOT thread-safe: the model is stateful. Use one instance per thread.
'  ---------------------------------------------------------------------------

Imports System
Imports System.Collections.Generic

Namespace PetalasAzizModel

    ''' <summary>Flow pattern codes, identical to the PARAMS.FOR values.</summary>
    Public Enum PetalasAzizRegime
        Undetermined = 0
        ElongatedBubble = 1
        Bubble = 2
        StratifiedSmooth = 3
        StratifiedWavy = 4
        Slug = 5
        AnnularMist = 6
        DispersedBubble = 7
        ''' <summary>Froth I - the DB1/AM1 transition region of Petalas &amp; Aziz.</summary>
        FrothI = 8
        Homogeneous = 9
        Froth = 10
        Stratified = 11
        Segregated = 12
        Transition = 13
        Intermittent = 14
        Distributed = 15
        SinglePhase = 16
    End Enum

    ''' <summary>Outcome of one Petalas &amp; Aziz point calculation (field units).</summary>
    Public Structure PetalasAzizResult
        ''' <summary>Predicted flow pattern.</summary>
        Public Regime As PetalasAzizRegime
        ''' <summary>In-situ volume fraction liquid (holdup), dimensionless.</summary>
        Public LiquidHoldup As Double
        ''' <summary>Total pressure gradient, psi/ft.</summary>
        Public TotalGradient As Double
        ''' <summary>Frictional component of the pressure gradient, psi/ft.</summary>
        Public FrictionalGradient As Double
        ''' <summary>Hydrostatic component of the pressure gradient, psi/ft.</summary>
        Public HydrostaticGradient As Double
        ''' <summary>Dimensionless liquid height, hL/D (stratified flow only).</summary>
        Public LiquidHeight As Double
        ''' <summary>Dimensionless film thickness, dL/D (annular-mist only).</summary>
        Public FilmThickness As Double
        ''' <summary>Liquid fraction in the film, eF (annular-mist only).</summary>
        Public FilmLiquidFraction As Double
        ''' <summary>Liquid fraction in the core, eCL (annular-mist only).</summary>
        Public CoreLiquidFraction As Double
        ''' <summary>Liquid fraction entrained in the gas core, FE.</summary>
        Public EntrainedFraction As Double
        ''' <summary>Warning raised during the solution, or Nothing.</summary>
        Public Warning As String

        ''' <summary>Long descriptive name of <see cref="Regime"/>.</summary>
        Public ReadOnly Property RegimeName As String
            Get
                Return PetalasAzizSolver.DescribeRegime(Regime, False)
            End Get
        End Property

        ''' <summary>Three-letter abbreviation of <see cref="Regime"/>.</summary>
        Public ReadOnly Property RegimeCode As String
            Get
                Return PetalasAzizSolver.DescribeRegime(Regime, True)
            End Get
        End Property
    End Structure

    ''' <summary>
    ''' Managed implementation of the Petalas &amp; Aziz (1997) mechanistic
    ''' multiphase flow model. Create one instance per calculation thread.
    ''' </summary>
    Public Class PetalasAzizSolver

#Region "Constants (PARAMS.FOR)"

        Private Const Pi As Double = 3.14159265359
        Private Const Pi180 As Double = 0.0174532925199
        ' ft/sec^2
        Private Const gCnst As Double = 32.2
        ' lb/ft.sec per cP
        Private Const amuCNV As Double = 0.00067196897514
        ' lb/sec^2 per dyne/cm
        Private Const sgmaCNV As Double = 0.0022046226218

        Private Const hLD_Llim As Double = 0.00001
        Private Const hLD_Ulim As Double = 0.999999
        Private Const hLD_tol As Double = 0.00001
        Private Const dLD_Llim As Double = 0.000001
        Private Const dLD_Ulim As Double = 0.499999
        Private Const dLD_tol As Double = 0.00001

        Private Const Vel_Lim As Double = 660.0
        Private Const CosLim As Double = 0.02
        Private Const Clift As Double = 0.8
        Private Const GammaC As Double = 1.3
        Private Const Tr_SPF As Double = 0.0000003

        Private Const IterLim As Integer = 20

        Private Const LiqHgt As Integer = 1
        Private Const FlmHgt As Integer = 2

#End Region

#Region "COMMON /MFDATA/ - model inputs"

        ''' <summary>Liquid density, lb/ft^3.</summary>
        Public DensL As Double
        ''' <summary>Gas density, lb/ft^3.</summary>
        Public DensG As Double
        ''' <summary>Liquid viscosity, centipoise.</summary>
        Public aMuL As Double
        ''' <summary>Gas viscosity, centipoise.</summary>
        Public aMuG As Double
        ''' <summary>Gas-liquid interfacial tension, dyne/cm.</summary>
        Public Sigma As Double
        ''' <summary>Pipe inside diameter, inches.</summary>
        Public Dia As Double
        ''' <summary>Relative roughness (absolute roughness in ft * 12 / Dia).</summary>
        Public aKbd As Double
        ''' <summary>Pipe angle of inclination, radians (positive = upward).</summary>
        Public Alpha As Double
        ''' <summary>Pressure. Carried for parity with the FORTRAN COMMON; unused.</summary>
        Public Press As Double

#End Region

#Region "COMMON /MBLNCE/, /MECH/, /TDPAR/, /FrTRN/ - solution state"

        Private hLD As Double
        Private dLD As Double
        Private X As Double
        Private Y As Double
        Private fsL As Double
        Private fsG As Double
        Private tauwL As Double
        Private taui As Double
        Private iFound As Integer
        Private ReadOnly Root As Double() = New Double(3) {}

        Private FE As Double
        Private eL As Double
        Private eF As Double
        Private eC As Double
        Private eCL As Double
        Private eLs As Double
        Private fi As Double
        Private fL As Double

        Private AtL As Double
        Private AtG As Double
        Private StL As Double
        Private StG As Double
        Private Sti As Double
        Private UtL As Double
        Private UtG As Double
        Private DtL As Double
        Private DtG As Double

        Private VsGloTr As Double
        Private VsGhiTr As Double
        Private VsLloTr As Double
        Private VsLhiTr As Double

        ' pPetAz's "eG" had FORTRAN static storage and is read before being
        ' assigned in the dispersed-bubble branch. Kept as a field so the
        ' original arithmetic is reproduced bit for bit.
        Private pPetAzEG As Double

        Private recursionDepth As Integer

#End Region

#Region "Options and diagnostics"

        ''' <summary>
        ''' Maximum nesting depth allowed for the Froth transition search
        ''' (VsGfrTr -&gt; pPetAz -&gt; VsGfrTr). Beyond this the transition search
        ''' reports failure instead of recursing further.
        ''' </summary>
        Public Property RecursionLimit As Integer = 4

        ''' <summary>
        ''' Reproduces a defect in the 1997 PETAZ.FOR listing: its
        ''' dispersed-bubble branch reads the gas fraction "eG" from a static
        ''' local that the branch never assigns, so the mixture density and
        ''' viscosity are built from whatever the previous call left behind.
        '''
        ''' Default False, which uses eG = 1 - eL. That is what the PetAz.dll
        ''' DWSIM ships does, and it is what makes this port agree with the DLL
        ''' to within single-precision noise. Set True only to reproduce the
        ''' 1997 listing verbatim.
        ''' </summary>
        Public Property LegacyDispersedBubbleHoldup As Boolean = False

        ''' <summary>Warning text from the most recent solve, or Nothing.</summary>
        Public Property LastWarning As String

#End Region

#Region "Public API"

        ''' <summary>
        ''' Solves the model for one point, in the original field units.
        ''' </summary>
        ''' <param name="liquidDensity">Liquid density, lb/ft^3.</param>
        ''' <param name="gasDensity">Gas density, lb/ft^3.</param>
        ''' <param name="liquidViscosity">Liquid viscosity, cP.</param>
        ''' <param name="gasViscosity">Gas viscosity, cP.</param>
        ''' <param name="surfaceTension">Interfacial tension, dyne/cm.</param>
        ''' <param name="diameter">Pipe inside diameter, inches.</param>
        ''' <param name="roughness">Pipe absolute roughness, ft.</param>
        ''' <param name="inclination">Pipe inclination, degrees (positive = upward).</param>
        ''' <param name="vsl">Superficial liquid velocity, ft/sec.</param>
        ''' <param name="vsg">Superficial gas velocity, ft/sec.</param>
        Public Function Calculate(liquidDensity As Double,
                                  gasDensity As Double,
                                  liquidViscosity As Double,
                                  gasViscosity As Double,
                                  surfaceTension As Double,
                                  diameter As Double,
                                  roughness As Double,
                                  inclination As Double,
                                  vsl As Double,
                                  vsg As Double) As PetalasAzizResult

            DensL = liquidDensity
            DensG = gasDensity
            aMuL = liquidViscosity
            aMuG = gasViscosity
            Sigma = surfaceTension
            Dia = diameter
            aKbd = roughness * 12.0 / diameter
            Alpha = inclination * Pi180

            LastWarning = Nothing
            recursionDepth = 0
            ' The Froth-transition cache and pPetAz's "eG" are not carried
            ' between points, so a single Calculate call reproduces the first
            ' line of a fresh PETAZ run regardless of what preceded it.
            VsGloTr = 0.0
            VsGhiTr = 0.0
            VsLloTr = 0.0
            VsLhiTr = 0.0
            pPetAzEG = 0.0
            SetMFlow()

            Dim iReg As Integer = IPetAz(vsl, vsg)

            Dim eLx, dPfr, dPhh As Double
            Dim dP As Double = PPetAz(iReg, vsl, vsg, eLx, dPfr, dPhh)

            Dim r As New PetalasAzizResult()
            r.Regime = CType(iReg, PetalasAzizRegime)
            r.LiquidHoldup = eLx
            r.TotalGradient = dP
            r.FrictionalGradient = dPfr
            r.HydrostaticGradient = dPhh
            r.LiquidHeight = hLD
            r.FilmThickness = dLD
            r.FilmLiquidFraction = eF
            r.CoreLiquidFraction = eCL
            r.EntrainedFraction = FE
            r.Warning = LastWarning
            Return r
        End Function

        ''' <summary>
        ''' Solves the model for one point in SI units. Gradients are returned
        ''' in Pa/m in <see cref="PetalasAzizResult.TotalGradient"/> and its
        ''' companions; the dimensionless outputs are unchanged.
        ''' </summary>
        ''' <param name="liquidDensity">Liquid density, kg/m^3.</param>
        ''' <param name="gasDensity">Gas density, kg/m^3.</param>
        ''' <param name="liquidViscosity">Liquid viscosity, Pa.s.</param>
        ''' <param name="gasViscosity">Gas viscosity, Pa.s.</param>
        ''' <param name="surfaceTension">Interfacial tension, N/m.</param>
        ''' <param name="diameter">Pipe inside diameter, m.</param>
        ''' <param name="roughness">Pipe absolute roughness, m.</param>
        ''' <param name="inclination">Pipe inclination, degrees (positive = upward).</param>
        ''' <param name="vsl">Superficial liquid velocity, m/s.</param>
        ''' <param name="vsg">Superficial gas velocity, m/s.</param>
        Public Function CalculateSI(liquidDensity As Double,
                                    gasDensity As Double,
                                    liquidViscosity As Double,
                                    gasViscosity As Double,
                                    surfaceTension As Double,
                                    diameter As Double,
                                    roughness As Double,
                                    inclination As Double,
                                    vsl As Double,
                                    vsg As Double) As PetalasAzizResult

            Const KGM3_TO_LBFT3 As Double = 0.062427960576145
            Const M_TO_FT As Double = 1.0 / 0.3048
            Const M_TO_IN As Double = 1.0 / 0.0254
            ' psi/ft -> Pa/m
            Const PSIFT_TO_PAM As Double = 6894.757293168361 / 0.3048

            Dim r As PetalasAzizResult = Calculate(liquidDensity * KGM3_TO_LBFT3,
                                                   gasDensity * KGM3_TO_LBFT3,
                                                   liquidViscosity * 1000.0,
                                                   gasViscosity * 1000.0,
                                                   surfaceTension * 1000.0,
                                                   diameter * M_TO_IN,
                                                   roughness * M_TO_FT,
                                                   inclination,
                                                   vsl * M_TO_FT,
                                                   vsg * M_TO_FT)

            r.TotalGradient *= PSIFT_TO_PAM
            r.FrictionalGradient *= PSIFT_TO_PAM
            r.HydrostaticGradient *= PSIFT_TO_PAM
            Return r
        End Function

        ''' <summary>Flow pattern name, long form or three-letter code (WhatFlow).</summary>
        Public Shared Function DescribeRegime(regime As PetalasAzizRegime, abbreviated As Boolean) As String
            Select Case regime
                Case PetalasAzizRegime.DispersedBubble : Return If(abbreviated, "DB", "Dispersed bubble")
                Case PetalasAzizRegime.Slug : Return If(abbreviated, "SLG", "Slug")
                Case PetalasAzizRegime.AnnularMist : Return If(abbreviated, "AM", "Annular-mist")
                Case PetalasAzizRegime.StratifiedSmooth : Return If(abbreviated, "SS", "Stratified smooth")
                Case PetalasAzizRegime.StratifiedWavy : Return If(abbreviated, "SW", "Stratified wavy")
                Case PetalasAzizRegime.Intermittent : Return If(abbreviated, "INT", "Intermittent")
                Case PetalasAzizRegime.Stratified : Return If(abbreviated, "STR", "Stratified")
                Case PetalasAzizRegime.ElongatedBubble : Return If(abbreviated, "EB", "Elongated bubble")
                Case PetalasAzizRegime.Segregated : Return If(abbreviated, "SEG", "Segregated")
                Case PetalasAzizRegime.Froth : Return If(abbreviated, "FR", "Froth")
                Case PetalasAzizRegime.FrothI : Return If(abbreviated, "FR", "Froth")
                Case PetalasAzizRegime.Homogeneous : Return If(abbreviated, "HOM", "Homogeneous")
                Case PetalasAzizRegime.Bubble : Return If(abbreviated, "BBL", "Bubble")
                Case PetalasAzizRegime.Distributed : Return If(abbreviated, "DST", "Distributed")
                Case PetalasAzizRegime.Transition : Return If(abbreviated, "TRN", "Transition")
                Case PetalasAzizRegime.SinglePhase : Return If(abbreviated, "SPF", "Single phase flow")
                Case Else : Return If(abbreviated, "---", "Unknown")
            End Select
        End Function

        ''' <summary>SUBROUTINE SetMFlow - resets the multiphase flow state.</summary>
        Public Sub SetMFlow()
            dLD = 0.0
            hLD = 0.0
            eL = 0.0
            eF = 0.0
            eC = 0.0
            eCL = 0.0
            eLs = 0.0
            FE = 0.0
            fi = 0.0
            fL = 0.0
        End Sub

#End Region

        Private Delegate Function Residual(vsl As Double, vsg As Double, xLD As Double) As Double

#Region "FUNCTION iPetAz - flow pattern determination"

        Private Function IPetAz(vsl As Double, vsg As Double) As Integer

            TaDuk(vsl, vsg)

            ' SHELT = sheltering coefficient
            Const SHELT As Double = 0.06
            Dim Vm As Double = vsl + vsg
            Dim CG As Double = vsg / Vm
            Dim CL As Double = vsl / Vm
            Dim cosA As Double = Math.Cos(Alpha)
            Dim sinA As Double = Math.Sin(Alpha)
            Dim D As Double = Dia / 12.0
            Dim Sgma As Double = Sigma * sgmaCNV
            Dim xMuG As Double = aMuG * amuCNV
            Dim xMuL As Double = aMuL * amuCNV
            Dim dRho As Double = DensL - DensG

            Dim result As Integer = 0
            Dim fi0 As Double = fi

            ' Holdup based on slug flow (eLsl) and liquid holdup in slug (eLs)
            Dim eLsl As Double = ESlug(vsl, vsg)

            If CL <= Tr_SPF OrElse CG <= Tr_SPF Then
                result = PetalasAzizRegime.SinglePhase
            ElseIf eLs < 0.48 AndAlso CG <= 0.52 Then
                result = PetalasAzizRegime.DispersedBubble
            Else
                hLD = HLsolve(vsl, vsg, AddressOf TDres, LiqHgt)
                Dim fiST As Double = fi
                eL = 4.0 * AtL / Pi
                If IsStrat(vsl, vsg, hLD) Then
                    ' Flow is stratified; now check whether it is wavy.
                    Dim xCosA As Double
                    If cosA = 0.0 Then
                        xCosA = cosA + CosLim
                    Else
                        xCosA = cosA
                    End If
                    Dim Tk1 As Double = (DensG * vsg * vsg / (dRho * gCnst * D * xCosA)) *
                                        (D * vsl * DensL / xMuL)
                    Tk1 = Math.Sqrt(Tk1)
                    Dim Tf2 As Double = 2.0 / (Math.Sqrt(SHELT * UtL) * UtG)
                    Dim VL As Double = UtL * vsl
                    Dim Frd As Double = VL / Math.Sqrt(gCnst * hLD * D)
                    If Tk1 < Tf2 Then
                        ' For downflow (up to 5 deg downward), check the
                        ' wave inception condition of Barnea et al. (1982)
                        If (Alpha < 0.0 AndAlso Frd > 1.4) OrElse (Alpha < -5.0 * Pi180) Then
                            result = PetalasAzizRegime.StratifiedWavy
                        Else
                            result = PetalasAzizRegime.StratifiedSmooth
                        End If
                    Else
                        result = PetalasAzizRegime.StratifiedWavy
                    End If
                End If
            End If

            If result = 0 Then
                dLD = HLsolve(vsl, vsg, AddressOf AMres, FlmHgt)
                Dim fiAM As Double = fi
                If IsAnlrM(vsl, vsg, dLD, FE) Then
                    result = PetalasAzizRegime.AnnularMist
                ElseIf eLs >= 0.48 Then
                    Dim eLbb As Double = EBubble(vsl, vsg)
                    If eLbb > 0.0 AndAlso IsBubble(vsl, vsg, eLbb) Then
                        result = PetalasAzizRegime.Bubble
                    ElseIf eLsl > 0.24 Then
                        If eLs > 0.9 Then
                            result = PetalasAzizRegime.ElongatedBubble
                        Else
                            result = PetalasAzizRegime.Slug
                        End If
                    Else
                        result = PetalasAzizRegime.FrothI
                    End If
                Else
                    result = PetalasAzizRegime.FrothI
                End If
            End If

            Return result
        End Function

#End Region

#Region "Flow pattern transition tests"

        ''' <summary>FUNCTION isStrat - test for stratified flow.</summary>
        Private Function IsStrat(vsl As Double, vsg As Double, xLD As Double) As Boolean

            Dim cosA As Double = Math.Cos(Alpha) + CosLim
            Dim sinA As Double = Math.Sin(Alpha)
            Dim D As Double = Dia / 12.0
            Dim xMuL As Double = aMuL * amuCNV
            Dim dRho As Double = DensL - DensG
            Dim CL As Double = vsl / (vsl + vsg)

            ' Stratified / non-stratified transition, Taitel & Dukler.
            ' Note: TDstrat overwrites AtG and UtG.
            Dim Tb1 As Double = TDstrat(vsg, xLD)

            ' Transition from bubbly to slug flow - Taitel et al. (1980)
            Dim eLst As Double = 4.0 * AtL / Pi
            Dim VL As Double = UtL * vsl
            Dim DL As Double = DtL * D
            Dim ReL As Double = DL * VL * DensL / xMuL
            fL = FLfacST(vsl, vsg)
            Dim VL2 As Double = VL * VL
            Dim Tstan As Double = gCnst * D * (1.0 - xLD) * cosA / fL

            Return Tb1 <= 1.0 AndAlso VL2 <= Tstan AndAlso Alpha < 0.00001
        End Function

        ''' <summary>FUNCTION isAnlrM - test for annular-mist flow.</summary>
        Private Function IsAnlrM(vsl As Double, vsg As Double, xLD As Double, xFE As Double) As Boolean

            Dim sinA As Double = Math.Sin(Alpha)
            Dim CL As Double = vsl / (vsl + vsg)

            Dim fdL As Double = 1.0 - 2.0 * xLD
            Dim xeC As Double = fdL * fdL
            Dim eLAM As Double
            If xeC <= 0.0 Then
                eLAM = 1.0
            Else
                Dim xVc As Double = (vsg + vsl * xFE) / xeC
                eLAM = 1.0 - vsg / xVc
            End If

            ' Find maximum dLD
            Dim dLlim As Double = DLlimAM(vsl, vsg, xLD)
            Return xLD < dLlim AndAlso eLAM <= 0.24
        End Function

        ''' <summary>LOGICAL FUNCTION isBubble - test for bubble flow.</summary>
        Private Function IsBubble(vsl As Double, vsg As Double, eLbb As Double) As Boolean

            Dim sinA As Double = Math.Sin(Alpha)
            Dim cosA As Double = Math.Cos(Alpha)
            Dim D As Double = Dia / 12.0
            Dim CL As Double = vsl / (vsl + vsg)
            Dim Sgma As Double = Sigma * sgmaCNV
            Dim xMuG As Double = aMuG * amuCNV
            Dim xMuL As Double = aMuL * amuCNV
            Dim dRho As Double = DensL - DensG

            ' Critical bubble diameter for dispersed bubbles
            Dim Dmax, Dcd, Dcb, DcFac As Double
            Dim Dc As Double = DcCalc(vsl, vsg, Dmax, Dcd, Dcb, DcFac)

            ' Critical diameter for bubble flow
            Dim Dcrit As Double = 19.0 * Math.Sqrt(dRho * Sgma / (DensL * DensL * gCnst))
            Dim Vo As Double = 1.53 * Math.Pow(gCnst * Sgma * dRho / (DensL * DensL), 0.25) * sinA

            ' Angle of inclination large enough to prevent migration of
            ' bubbles to the top wall, using d = 7 mm, CL = 0.8, gamma = 1.3
            Dim Db As Double = Math.Max(Dcd, Dcb)
            Dim Tang As Double = 0.5303 * Clift * GammaC * GammaC * Vo * Vo / (Db * gCnst)

            Return cosA <= Tang AndAlso D > Dcrit AndAlso eLbb < 1.0 AndAlso eLbb > 0.75
        End Function

#End Region

#Region "FUNCTION pPetAz - holdup and pressure gradient"

        Private Function PPetAz(iReg As Integer, vsl As Double, vsg As Double,
                                ByRef eLx As Double, ByRef dPfr As Double,
                                ByRef dPhh As Double) As Double

            Dim D As Double = Dia / 12.0
            Dim sinA As Double = Math.Sin(Alpha)
            Dim cosA As Double = Math.Cos(Alpha)
            Dim Sgma As Double = Sigma * sgmaCNV
            Dim xMuL As Double = aMuL * amuCNV
            Dim xMuG As Double = aMuG * amuCNV

            Dim dRho As Double = DensL - DensG
            Dim Vm As Double = vsl + vsg
            Dim CL As Double = vsl / Vm
            Dim CG As Double = vsg / Vm
            Dim UseHOM As Boolean = False

            Dim DensM As Double = 0.0

            If iReg = PetalasAzizRegime.SinglePhase Then
                If CL > CG Then
                    dPfr = PPAhom(vsl, 0.0, eL, DensM)
                Else
                    dPfr = PPAhom(0.0, vsg, eL, DensM)
                End If

            ElseIf iReg = PetalasAzizRegime.DispersedBubble Then
                eL = EDisBB(vsl, vsg)
                ' The 1997 listing reads its static local "eG" here without
                ' ever assigning it in this branch; on a fresh run it is zero.
                Dim eG As Double = If(LegacyDispersedBubbleHoldup, pPetAzEG, 1.0 - eL)
                DensM = DensL * eL + DensG * eG
                Dim xMuM As Double = xMuL * eL + xMuG * eG
                Dim ReMix As Double = D * Vm * DensM / xMuM
                Dim fm As Double = FrcFac(ReMix, aKbd)
                dPfr = 2.0 * fm * Vm * Vm * DensM / (gCnst * D)

            ElseIf iReg = PetalasAzizRegime.StratifiedSmooth OrElse iReg = PetalasAzizRegime.StratifiedWavy Then
                ' The only branch that assigns "eG"; the dispersed-bubble
                ' branch above is what reads it back on a later call.
                eL = 4.0 * AtL / Pi
                pPetAzEG = 1.0 - eL
                DensM = eL * DensL + pPetAzEG * DensG
                dPfr = PStrat(vsl, vsg, hLD)

            ElseIf iReg = PetalasAzizRegime.AnnularMist Then
                Dim res As Double = AMres(vsl, vsg, dLD)
                Dim DensC As Double = eCL * DensL + (1.0 - eCL) * DensG
                DensM = eF * DensL + eC * DensC
                dPfr = 4.0 * tauwL / D
                If sinA >= 0.0 AndAlso eL <= CL Then UseHOM = True

            ElseIf iReg = PetalasAzizRegime.FrothI Then
                Dim dPlo, eLlo, dPhi, eLhi As Double
                Dim VsGlo As Double = VsGfrTr(0, vsl, vsg, dPlo, eLlo)
                Dim VsGhi As Double = VsGfrTr(1, vsl, vsg, dPhi, eLhi)
                Dim fac As Double
                If VsGlo > 0.0 Then
                    If VsGhi > 0.0 Then
                        fac = Math.Log10(vsg / VsGlo) / Math.Log10(VsGhi / VsGlo)
                        dPfr = dPlo * Math.Pow(dPhi / dPlo, fac)
                        If eLlo = 0.0 Then
                            eL = Tr_SPF * Math.Pow(eLhi / Tr_SPF, fac)
                        ElseIf eLhi = 0.0 Then
                            eL = eLlo * Math.Pow(Tr_SPF / eLlo, fac)
                        Else
                            eL = eLlo * Math.Pow(eLhi / eLlo, fac)
                        End If
                        DensM = DensL * eL + DensG * (1.0 - eL)
                        UseHOM = False
                    Else
                        VsGhi = vsl * (1.0 / Tr_SPF - 1.0)
                        If VsGhi < Vel_Lim Then
                            dPhi = PPAhom(0.0, VsGhi, eLhi, DensM)
                            fac = Math.Log10(vsg / VsGlo) / Math.Log10(VsGhi / VsGlo)
                            dPfr = dPlo * Math.Pow(dPhi / dPlo, fac)
                            If eLlo = 0.0 Then
                                eL = 0.0
                            Else
                                eL = eLlo * Math.Pow(Tr_SPF / eLlo, fac)
                            End If
                            DensM = DensL * eL + DensG * (1.0 - eL)
                            UseHOM = False
                        Else
                            UseHOM = True
                        End If
                    End If
                ElseIf VsGhi > 0.0 Then
                    VsGlo = vsl / (1.0 / Tr_SPF - 1.0)
                    dPlo = PPAhom(0.0, VsGlo, eLlo, DensM)
                    fac = Math.Log10(vsg / VsGlo) / Math.Log10(VsGhi / VsGlo)
                    dPfr = dPlo * Math.Pow(dPhi / dPlo, fac)
                    eL = Tr_SPF * Math.Pow(eLhi / Tr_SPF, fac)
                    DensM = DensL * eL + DensG * (1.0 - eL)
                    UseHOM = False
                Else
                    UseHOM = True
                End If

            ElseIf iReg = PetalasAzizRegime.Bubble Then
                eL = EBubble(vsl, vsg)
                dPfr = PBubble(vsl, vsg, eL, DensM)

            Else
                ' Intermittent region: Slug, ElongatedBubble
                eL = ESlug(vsl, vsg)
                DensM = DensL * eL + DensG * (1.0 - eL)
                dPfr = PSlug(vsl, vsg, eL)
            End If

            If UseHOM Then dPfr = PPAhom(vsl, vsg, eL, DensM)

            dPhh = DensM * sinA
            dPfr = dPfr / 144.0
            dPhh = dPhh / 144.0
            eLx = eL
            Return dPfr + dPhh
        End Function

#End Region

#Region "Friction factor and holdup correlations"

        ''' <summary>
        ''' FUNCTION FrcFac - Fanning friction factor, Chen, N.H.,
        ''' I&amp;EC Fund., v18 No.3, p.296 (1979).
        ''' </summary>
        Private Shared Function FrcFac(Rey As Double, aKbd As Double) As Double
            Dim Re As Double = Rey
            Dim A As Double = 5.8506 / Math.Pow(Re, 0.8981)
            Dim B As Double = Math.Pow(aKbd, 1.1098) / 2.8257
            Dim C As Double = Math.Log10(A + B)
            Dim D As Double = aKbd / 3.7065 - (5.0452 / Re) * C
            Dim F As Double
            If D > 0.0 Then
                Dim E As Double = -4.0 * Math.Log10(D)
                F = 1.0 / (E * E)
            Else
                F = 1.0
            End If
            ' Turbulent-laminar transition
            Dim Flam As Double = 16.0 / Rey
            If Flam > F Then F = Flam
            Return F
        End Function

        ''' <summary>FUNCTION eDisBB - holdup for dispersed bubble flow.</summary>
        Private Function EDisBB(vsl As Double, vsg As Double) As Double
            Dim sinA As Double = Math.Sin(Alpha)
            Dim cosA As Double = Math.Cos(Alpha)
            Dim xMuL As Double = aMuL * amuCNV
            Dim xMuG As Double = aMuG * amuCNV
            Dim Sgma As Double = Sigma * sgmaCNV
            Dim D As Double = Dia / 12.0
            Dim dRho As Double = DensL - DensG
            Dim Vm As Double = vsl + vsg
            Dim CL As Double = vsl / Vm

            ' Velocity of dispersed bubbles
            Const C0g As Double = 1.2
            Dim Vb As Double = 1.53 * Math.Pow(Sgma * gCnst * dRho / (DensL * DensL), 0.25)
            Dim Vgdb As Double = C0g * Vm + Vb * sinA
            Dim eLx As Double
            If Vgdb <= 0.0 Then
                eLx = 1.0 - vsg / (C0g * Vm)
            Else
                eLx = 1.0 - vsg / Vgdb
            End If

            If eLx >= 1.0 Then Return CL
            Return eLx
        End Function

        ''' <summary>FUNCTION eSlug - holdup for slug flow. Sets eLs.</summary>
        Private Function ESlug(vsl As Double, vsg As Double) As Double
            Dim sinA As Double = Math.Sin(Alpha)
            Dim cosA As Double = Math.Cos(Alpha)
            Dim xMuL As Double = aMuL * amuCNV
            Dim xMuG As Double = aMuG * amuCNV
            Dim Sgma As Double = Sigma * sgmaCNV
            Dim D As Double = Dia / 12.0
            Dim dRho As Double = DensL - DensG
            Dim Vm As Double = vsl + vsg
            Dim CL As Double = vsl / Vm

            ' Liquid fraction in slug, Gregory et al. (1987)
            eLs = 1.0 / (1.0 + Math.Pow(Vm / 28.412, 1.39))
            If eLs > 1.0 Then eLs = 1.0

            Dim ReML As Double = D * Vm * DensL / xMuL
            Dim C0 As Double = (1.64 + 0.12 * sinA) / Math.Pow(ReML, 0.031)
            Dim Vd As Double = VDrift()
            Dim Vt As Double = C0 * Vm + Vd

            ' Velocity of dispersed bubbles
            Dim Vb As Double = 1.53 * Math.Pow(Sgma * gCnst * dRho / (DensL * DensL), 0.25)
            Dim Vgdb As Double = C0 * Vm + Vb * sinA
            If Vgdb < 0.0 Then Vgdb = 0.0

            Dim eLx As Double = eLs + (Vgdb * (1.0 - eLs) - vsg) / Vt

            If eLx >= 1.0 Then Return CL
            Return eLx
        End Function

        ''' <summary>FUNCTION vDrift - drift velocity of elongated bubbles.</summary>
        Private Function VDrift() As Double
            Const Cvert As Double = 0.345
            Const Chorz As Double = 0.54
            Dim sinA As Double = Math.Sin(Alpha)
            Dim cosA As Double = Math.Cos(Alpha)
            Dim xMuL As Double = aMuL * amuCNV
            Dim Sgma As Double = Sigma * sgmaCNV
            Dim D As Double = Dia / 12.0
            Dim dRho As Double = DensL - DensG
            Dim rtgD As Double = Math.Sqrt(gCnst * D * dRho / DensL)

            ' Bond number
            Dim Bo As Double = dRho * gCnst * D * D / Sgma
            ' Horizontal, high Reynolds number - Weber (1981)
            Dim Vdh As Double = (Chorz - 1.76 / Math.Pow(Bo, 0.56)) * rtgD
            ' Vertical, high Reynolds number - Wallis (1969)
            Dim fact As Double = Math.Exp(3.278 - 1.424 * Math.Log(Bo)) * Bo
            Dim Vdv As Double = Cvert * (1.0 - Math.Exp(-fact)) * rtgD
            ' Bendiksen (1984)
            Dim Vd As Double = Vdh * cosA + Vdv * sinA
            Dim Reo As Double = DensL * Math.Abs(Vd) * D / (2.0 * xMuL)
            Dim fmu As Double = 0.316 * Math.Sqrt(Reo)
            If fmu > 1.0 Then fmu = 1.0
            Return fmu * Vd
        End Function

        ''' <summary>FUNCTION eBubble - holdup for bubble flow. Sets eL.</summary>
        Private Function EBubble(vsl As Double, vsg As Double) As Double
            Dim Sgma As Double = Sigma * sgmaCNV
            Dim sinA As Double = Math.Sin(Alpha)
            Dim dRho As Double = DensL - DensG
            Dim Vm As Double = vsl + vsg
            Dim Vb As Double = 1.41 * Math.Pow(Sgma * gCnst * dRho / (DensL * DensL), 0.25)
            Dim Vbf As Double = 1.2 * Vm + Vb * sinA

            eL = 1.0 - vsg / Vbf
            Dim CL As Double = vsl / Vm

            If eL >= 1.0 Then Return CL
            Return eL
        End Function

        ''' <summary>
        ''' FUNCTION dcCalc - critical bubble diameter, the minimum of Dcd
        ''' (shape becomes non-spherical) and Dcb (buoyancy exceeds turbulence).
        ''' </summary>
        Private Function DcCalc(vsl As Double, vsg As Double,
                                ByRef Dmax As Double, ByRef Dcd As Double,
                                ByRef Dcb As Double, ByRef DcFac As Double) As Double

            Dim Vm As Double = vsl + vsg
            Dim CG As Double = vsg / Vm
            Dim CL As Double = vsl / Vm
            Dim cosA As Double = Math.Cos(-Alpha)
            Dim D As Double = Dia / 12.0
            Dim Sgma As Double = Sigma * sgmaCNV
            Dim xMuG As Double = aMuG * amuCNV
            Dim xMuL As Double = aMuL * amuCNV
            Dim dRho As Double = DensL - DensG
            Dim DensM As Double = CL * DensL + CG * DensG
            ' The FORTRAN pairs CL with the gas viscosity and CG with the
            ' liquid viscosity here. Reproduced verbatim.
            Dim xMuM As Double = CL * xMuG + CG * xMuL
            Dim ReMix As Double = D * Vm * DensM / xMuM
            Dim fm As Double = FrcFac(ReMix, aKbd)

            DcFac = Math.Pow(Sgma / DensL, 0.6) / Math.Pow(2.0 * fm * Vm * Vm * Vm / D, 0.4)
            Dmax = (0.725 + 4.15 * Math.Sqrt(CG)) * DcFac
            ' Bubble size above which the bubble is deformed
            Dcd = 2.0 * Math.Sqrt(0.4 * Sgma / (dRho * gCnst))

            ' Bubble size below which migration of bubbles to the upper part
            ' of the pipe is prevented
            If Math.Abs(cosA) <= 0.00001 Then
                Dcb = 0.0
                Return Dcd
            End If
            Dcb = 0.375 * (DensL / dRho) * fm * Vm * Vm / (gCnst * cosA)
            Return Math.Min(Dcd, Dcb)
        End Function

        ''' <summary>FUNCTION Fentrn - liquid fraction entrained in the gas core. Sets FE.</summary>
        Private Function Fentrn(vsl As Double, vsg As Double) As Double
            Dim Sgma As Double = Sigma * sgmaCNV
            Dim xMuL As Double = aMuL * amuCNV
            Dim Vr As Double = vsl / vsg
            Dim xNB As Double = xMuL * xMuL * vsg * vsg * DensG / (Sgma * Sgma * DensL)
            Dim xFE As Double = 0.735 * Math.Pow(xNB, 0.074) / Math.Pow(Vr, 0.2)
            FE = xFE / (1.0 + xFE)
            Return FE
        End Function

        ''' <summary>FUNCTION fiFac - interfacial friction factor, Petalas &amp; Aziz (1996).</summary>
        Private Function FiFac(vsl As Double, vsg As Double) As Double
            Dim D As Double = Dia / 12.0
            Dim Sgma As Double = Sigma * sgmaCNV
            Dim xMuL As Double = aMuL * amuCNV
            Dim xMuG As Double = aMuG * amuCNV
            Dim Vf As Double = vsl * (1.0 - FE) / eF
            Dim Vc As Double = (vsg + vsl * FE) / eC
            Dim Vcf As Double = Vc - Vf
            Dim EcG As Double = 1.0 - eCL
            Dim Dc As Double = (1.0 - 2.0 * dLD) * D
            Dim Df As Double = 4.0 * dLD * (1.0 - dLD) * D
            Dim DensC As Double = eCL * DensL + EcG * DensG
            Dim xMuC As Double = eCL * xMuL + EcG * xMuG
            Dim Ref As Double = Df * DensL * Vf / xMuL
            Dim ReC As Double = Dc * DensC * Vc / xMuC
            Dim fc As Double = FrcFac(ReC, aKbd)
            Dim xNc As Double = Sgma / (DensC * Vc * Vc * Dc)
            Return fc * 0.24 * Math.Pow(xNc, 0.085) * Math.Pow(Ref, 0.305)
        End Function

        ''' <summary>FUNCTION fLFacST - liquid/wall friction factor for stratified flow. Sets fL.</summary>
        Private Function FLfacST(vsl As Double, vsg As Double) As Double
            Dim D As Double = Dia / 12.0
            Dim Sgma As Double = Sigma * sgmaCNV
            Dim xMuL As Double = aMuL * amuCNV
            Dim ResL As Double = D * vsl * DensL / xMuL
            Dim xfsL As Double = FrcFac(ResL, aKbd)
            fL = 0.452 * Math.Pow(xfsL, 0.731)
            Return fL
        End Function

#End Region

#Region "Pressure gradient by regime"

        ''' <summary>FUNCTION pStrat - frictional gradient for stratified flow.</summary>
        Private Function PStrat(vsl As Double, vsg As Double, xLD As Double) As Double
            Dim D As Double = Dia / 12.0
            Dim xMuL As Double = aMuL * amuCNV
            Dim xMuG As Double = aMuG * amuCNV

            HLDparms(xLD)

            Dim VL As Double = UtL * vsl
            Dim DL As Double = DtL * D
            Dim ReL As Double = DL * VL * DensL / xMuL
            fL = FLfacST(vsl, vsg)
            Dim VG As Double = UtG * vsg
            Dim DG As Double = DtG * D
            Dim ReG As Double = DG * VG * DensG / xMuG
            Dim fG As Double = FrcFac(ReG, aKbd)
            Dim SLA As Double = 4.0 * StL / (Pi * D)
            Dim SGA As Double = 4.0 * StG / (Pi * D)
            Dim twG As Double = fG * DensG * VG * VG / (2.0 * gCnst)
            Dim twL As Double = fL * DensL * VL * VL / (2.0 * gCnst)
            Return twL * SLA + twG * SGA
        End Function

        ''' <summary>FUNCTION pSlug - frictional gradient for intermittent flow.</summary>
        Private Function PSlug(vsl As Double, vsg As Double, eLx As Double) As Double
            Dim D As Double = Dia / 12.0
            Dim sinA As Double = Math.Sin(Alpha)
            Dim Sgma As Double = Sigma * sgmaCNV
            Dim xMuL As Double = aMuL * amuCNV
            Dim xMuG As Double = aMuG * amuCNV
            Dim Vm As Double = vsl + vsg
            Dim CG As Double = vsg / Vm
            Dim CL As Double = vsl / Vm
            Dim eGx As Double = 1.0 - eLx

            ' Slug dP as if the entire liquid volume were in the slug portion
            ' with a film of zero thickness.
            Dim DensM As Double = DensL * eLx + DensG * eGx
            Dim xMuM As Double = xMuL * eLx + xMuG * eGx
            Dim ReLm As Double = D * DensL * Vm / xMuL
            Dim fLm As Double = FrcFac(ReLm, aKbd)
            Dim dPfrSL As Double = 2.0 * fLm * Vm * Vm * DensM / (gCnst * D)

            ' Film dP as if annular-mist flow, film height obtained from eLx
            Dim Cnst As Double = D * DensL * vsl / xMuL
            Dim FEi As Double = Fentrn(vsl, vsg)
            Dim Ref As Double = Cnst * (1.0 - FEi)
            Dim ffi As Double = FrcFac(Ref, aKbd)
            Dim dLDi As Double = (1.0 - Math.Sqrt(eGx * (FEi * vsl + vsg) / vsg)) / 2.0

            Dim dPfrAM As Double
            If dLDi > 0.0001 Then
                Dim fdL As Double = 1.0 - 2.0 * dLDi
                Dim eCi As Double = fdL * fdL
                Dim Vci As Double = (vsg + vsl * FEi) / eCi
                Dim EcLi As Double = FEi * vsl / (eCi * Vci)
                Dim eFi As Double = 4.0 * dLDi * (1.0 - dLDi)
                Dim Vfi As Double = vsl * (1.0 - FEi) / eFi
                Dim tauwLi As Double = ffi * DensL * Vfi * Vfi / (2.0 * gCnst)
                Dim DensCi As Double = EcLi * DensL + (1.0 - EcLi) * DensG
                Dim DensMi As Double = eFi * DensL + eCi * DensCi
                dPfrAM = 4.0 * tauwLi / D
            Else
                Dim ReMix As Double = D * Vm * DensM / xMuM
                Dim fm As Double = FrcFac(ReMix, aKbd)
                dPfrAM = 2.0 * fm * Vm * Vm * DensM / (gCnst * D)
            End If

            ' "eta" calibrates the intermittent-region pressure drop; it is
            ' related to the ratio Ls/Lu (slug length / slug unit length).
            Dim eta As Double = Math.Pow(CL, 0.75 - eLx)
            If eta < 0.0 Then
                eta = 0.0
            ElseIf eta > 1.0 Then
                eta = 1.0
            End If

            Return eta * dPfrSL + (1.0 - eta) * dPfrAM
        End Function

        ''' <summary>FUNCTION pBubble - frictional gradient for bubble flow.</summary>
        Private Function PBubble(vsl As Double, vsg As Double, eLx As Double,
                                 ByRef DensM As Double) As Double
            Dim D As Double = Dia / 12.0
            Dim xMuL As Double = aMuL * amuCNV
            Dim Vm As Double = vsl + vsg
            Dim eGx As Double = 1.0 - eLx

            Dim ReMix As Double = D * Vm * DensL / xMuL
            Dim fm As Double = FrcFac(ReMix, aKbd)
            DensM = eLx * DensL + eGx * DensG
            Return 2.0 * fm * Vm * Vm * DensM / (gCnst * D)
        End Function

        ''' <summary>FUNCTION pPAhom - homogeneous model. Sets eL.</summary>
        Private Function PPAhom(vsl As Double, vsg As Double,
                                ByRef eLx As Double, ByRef DensM As Double) As Double
            Dim D As Double = Dia / 12.0
            Dim xMuL As Double = aMuL * amuCNV
            Dim xMuG As Double = aMuG * amuCNV

            Dim Vm As Double = vsl + vsg
            Dim CL As Double = vsl / Vm
            Dim CG As Double = vsg / Vm

            eL = CL
            Dim eG As Double = CG
            DensM = DensL * eL + DensG * eG
            Dim xMuM As Double = xMuL * eL + xMuG * eG
            Dim ReMix As Double = D * Vm * DensM / xMuM
            Dim fm As Double = FrcFac(ReMix, aKbd)
            Dim dPfr As Double = 2.0 * fm * Vm * Vm * DensM / (gCnst * D)
            eLx = eL
            Return dPfr
        End Function

#End Region

#Region "Momentum balance residuals and geometry"

        ''' <summary>SUBROUTINE TaDuk - superficial friction factors and the Taitel-Dukler X, Y groups.</summary>
        Private Sub TaDuk(vsl As Double, vsg As Double)
            Dim D As Double = Dia / 12.0
            Dim ReL As Double = D * DensL * vsl / (aMuL * amuCNV)
            Dim ReG As Double = D * DensG * vsg / (aMuG * amuCNV)
            fsL = FrcFac(ReL, aKbd)
            fsG = FrcFac(ReG, aKbd)
            Dim dPdLG As Double = 2.0 * fsG * vsg * vsg * DensG / (gCnst * D)
            Dim dPdLL As Double = 2.0 * fsL * vsl * vsl * DensL / (gCnst * D)
            X = Math.Sqrt(dPdLL / dPdLG)
            Y = -((DensL - DensG) * Math.Sin(Alpha)) / dPdLG
        End Sub

        ''' <summary>FUNCTION TDstrat - Taitel-Dukler stratified transition group. Overwrites AtG and UtG.</summary>
        Private Function TDstrat(vsg As Double, xLD As Double) As Double
            Dim cosA As Double = Math.Cos(Alpha)
            If Math.Abs(cosA) < 0.00001 Then cosA = cosA + CosLim
            Dim C2 As Double = 1.0 - xLD
            Dim Tf1 As Double = vsg / Math.Sqrt(gCnst * Dia / 12.0 * cosA) *
                                Math.Sqrt(DensG / (DensL - DensG))
            Dim a As Double = 2.0 * xLD - 1.0
            Dim dAdh As Double = Math.Sqrt(1.0 - a * a)
            AtG = 0.25 * (Math.Acos(a) - a * dAdh)
            UtG = 0.25 * Pi / AtG
            Return Tf1 * Tf1 * (UtG * UtG * dAdh / (C2 * C2 * AtG))
        End Function

        ''' <summary>
        ''' SUBROUTINE hLDparms - dimensionless geometric relationships from the
        ''' dimensionless liquid height. Writes Sti, StL, StG, AtL, AtG, UtL, UtG, DtL, DtG.
        ''' </summary>
        Private Sub HLDparms(xLD As Double)
            Dim FhL As Double = 2.0 * xLD - 1.0
            Sti = Math.Sqrt(1.0 - FhL * FhL)
            StG = Math.Acos(FhL)
            If StG <= 0.0 Then StG = 0.0000000001
            StL = Pi - StG
            If StL <= 0.0 Then StL = 0.0000000001
            AtL = Math.Abs(0.25 * (StL + FhL * Sti))
            If AtL <= 0.0 Then AtL = 0.0000000001
            AtG = Math.Abs(0.25 * (StG - FhL * Sti))
            If AtG <= 0.0 Then AtG = 0.0000000001
            UtL = Pi / (4.0 * AtL)
            DtL = 4.0 * AtL / StL
            UtG = Pi / (4.0 * AtG)
            DtG = 4.0 * AtG / (StG + Sti)
        End Sub

        ''' <summary>
        ''' FUNCTION TDres - normalized residual of the stratified combined
        ''' momentum balance (revision 1.01 form).
        ''' </summary>
        Private Function TDres(vsl As Double, vsg As Double, xLD As Double) As Double
            hLD = xLD
            HLDparms(hLD)

            Dim Sgma As Double = Sigma * sgmaCNV
            Dim xMuL As Double = aMuL * amuCNV
            Dim xMuG As Double = aMuG * amuCNV
            Dim D As Double = Dia / 12.0
            Dim sinA As Double = Math.Sin(Alpha)
            Dim dRho As Double = DensL - DensG

            Dim DL As Double = DtL * D
            Dim VL As Double = UtL * vsl
            fL = FLfacST(vsl, vsg)

            Dim VG As Double = UtG * vsg
            Dim DG As Double = DtG * D
            Dim ReG As Double = DG * VG * DensG / xMuG
            Dim fG As Double = FrcFac(ReG, aKbd)
            Dim Vi As Double = VG - VL

            Dim Fr As Double = VL / Math.Sqrt(gCnst * hLD * D)
            Dim ResL As Double = D * vsl * DensL / xMuL
            fi = (0.004 + 0.0000005 * ResL) * Math.Pow(Fr, 1.335) *
                 (DensL * D * gCnst / (DensG * VG * VG))

            Dim twL As Double = fL * DensL * VL * Math.Abs(VL) / (2.0 * gCnst)
            Dim twG As Double = fG * DensG * VG * Math.Abs(VG) / (2.0 * gCnst)
            Dim ti As Double = fi * DensG * Vi * Math.Abs(Vi) / (2.0 * gCnst)
            Dim SALf As Double = StL / (AtL * D)
            Dim SAGf As Double = StG / (AtG * D)

            Return SALf - (twG * SAGf +
                           ti * Sti * (1.0 / AtL + 1.0 / AtG) / D -
                           dRho * sinA) / twL
        End Function

        ''' <summary>
        ''' FUNCTION AMres - normalized residual of the annular-mist combined
        ''' momentum balance (revision 1.01 form).
        ''' </summary>
        Private Function AMres(vsl As Double, vsg As Double, xLD As Double) As Double
            dLD = xLD
            Dim D As Double = Dia / 12.0
            Dim sinA As Double = Math.Sin(Alpha)
            Dim Sgma As Double = Sigma * sgmaCNV
            Dim xMuL As Double = aMuL * amuCNV
            Dim xMuG As Double = aMuG * amuCNV

            Dim fdL As Double = 1.0 - 2.0 * dLD
            eC = fdL * fdL
            eF = 4.0 * dLD * (1.0 - dLD)
            If eF <= 0.0 Then eF = 0.0000000001
            If eC <= 0.0 Then eC = 0.0000000001
            Dim Atf As Double = Pi * eF / 4.0
            Dim Atc As Double = Pi * eC / 4.0
            Dim Stf As Double = Pi
            Sti = Pi * fdL
            Dim Df As Double = eF * D
            Dim Dc As Double = fdL * D
            Dim Cnst As Double = D * DensL * vsl / xMuL
            FE = Fentrn(vsl, vsg)
            Dim Ref As Double = Cnst * (1.0 - FE)
            Dim Vf As Double = vsl * (1.0 - FE) / eF
            Dim Vc As Double = (vsg + vsl * FE) / eC
            eL = 1.0 - vsg / Vc
            eCL = FE * vsl / (eC * Vc)
            Dim Vtf As Double = Vf / vsl
            Dim ff As Double = FrcFac(Ref, aKbd)
            tauwL = ff * DensL * Vf * Vf / (2.0 * gCnst)
            Dim EcG As Double = 1.0 - eCL
            Dim DensC As Double = eCL * DensL + EcG * DensG
            Dim xMuC As Double = eCL * xMuL + EcG * xMuG
            fi = FiFac(vsl, vsg)
            Dim Vcf As Double = Vc - Vf
            taui = fi * DensC * Vcf * Math.Abs(Vcf) / (2.0 * gCnst)

            Dim SAf As Double = Stf / (Atf * D)
            Return SAf - (taui * Sti * (1.0 / Atf + 1.0 / Atc) / D -
                          (DensL - DensC) * sinA) / tauwL
        End Function

        ''' <summary>
        ''' FUNCTION dLlimAM - limiting dL/D at which the interfacial shear
        ''' stress is a minimum. Returns 0.5 when no root exists, meaning the
        ''' minimum-shear mechanism does not apply. Overwrites eC and eCL.
        ''' </summary>
        Private Function DLlimAM(vsl As Double, vsg As Double, xLD As Double) As Double
            Dim D As Double = Dia / 12.0
            Dim sinA As Double = Math.Sin(Alpha)
            If sinA = 0.0 Then Return 0.5

            Dim xMuL As Double = aMuL * amuCNV
            Dim VLx As Double = (1.0 - FE) * vsl
            Dim Ref As Double = D * DensL * VLx / xMuL
            Dim ff As Double = FrcFac(Ref, aKbd)
            Dim cnst0 As Double = 2.0 * ff * DensL * VLx * VLx / (gCnst * D * sinA)

            Dim xLDi As Double = xLD
            Dim converged As Boolean = False
            For Iter As Integer = 1 To 15
                Dim fdL As Double = 1.0 - 2.0 * xLDi
                eC = fdL * fdL
                Dim eFx As Double = 4.0 * xLDi * (1.0 - xLDi)
                Dim Vf As Double = vsl * (1.0 - FE) / eFx
                Dim Vc As Double = (vsg + vsl * FE) / eC
                eCL = FE * vsl / (eC * Vc)
                Dim DensC As Double = eCL * DensL + (1.0 - eCL) * DensG
                Dim fac As Double = cnst0 / (DensL - DensC)
                Dim eFx2 As Double = eFx * eFx
                Dim eFx3 As Double = eFx * eFx2
                Dim fx As Double = eFx3 * (1.0 - 1.5 * eFx) - fac * (2.0 - 1.5 * eFx)
                Dim dfx As Double = (eFx2 * (3.0 - 6.0 * eFx) + fac * 1.5) * 4.0 * fdL

                If Math.Abs(fx / xLDi) < 0.0001 Then
                    converged = True
                    Exit For
                End If

                Dim xLDj As Double = xLDi - fx / dfx
                If xLDj >= 0.5 Then
                    xLDi = Math.Sqrt(xLDi / 2.0)
                ElseIf xLDj < 0.0 Then
                    xLDi = xLDi * xLDi
                Else
                    xLDi = xLDj
                End If
            Next

            If Not converged Then xLDi = 0.5
            Return xLDi
        End Function

#End Region

#Region "Root solver"

        ''' <summary>
        ''' FUNCTION hLsolve - solves the momentum balance for hL/D or dL/D.
        '''
        ''' The routine scans the whole admissible range because the balance
        ''' can have multiple roots. A sign change in the residual triggers a
        ''' bisection down to 1% followed by a Fibonacci search; a sign change
        ''' in the slope switches to a finer scan interval so roots hidden
        ''' inside one coarse interval are still detected.
        ''' </summary>
        ''' <param name="mType">1 = liquid height (0..1), 2 = film height (0..0.5).</param>
        Private Function HLsolve(vsl As Double, vsg As Double,
                                 func As Residual, mType As Integer) As Double

            ' iRoot = 0 -> return the first root found, using coarse intervals.
            ' The FORTRAN hard-codes this; the other modes (lowest/middle/
            ' highest root) are retained in the branches below.
            Const iRoot As Integer = 0

            Dim MaxIntvl As Integer
            Dim xLrgDiv, xSmlDiv As Double
            If iRoot = 0 Then
                MaxIntvl = 1
                xLrgDiv = 10.0
                xSmlDiv = 10.0
            Else
                MaxIntvl = 1
                xLrgDiv = 100.0
                xSmlDiv = 20.0
            End If

            Dim Hmin, Hmax, Htol0 As Double
            If mType = FlmHgt Then
                Hmin = dLD_Llim
                Hmax = dLD_Ulim
                Htol0 = dLD_tol
            Else
                Hmin = hLD_Llim
                Hmax = hLD_Ulim
                Htol0 = hLD_tol
            End If

            Dim Intrval As Integer = 0
            Dim H1, H2, Htol, delta As Double
            Dim xLDi, xLDj, xLDn, xLD As Double
            Dim resi, resj, res As Double
            Dim slp, slpi As Double
            Dim dScan As Double = 0.0
            Dim xScan As Double = 0.0
            Dim sScan As Double = 0.0
            Dim rRes As Double = 0.0
            Dim scanning As Boolean

            Do ' label 10 - restart with a finer interval
                iFound = 0
                Root(1) = 0.0
                H1 = Hmin
                H2 = Hmax
                Htol = Htol0 / Math.Pow(10.0, 2 * Intrval)
                If H1 > Htol Then H1 = Htol
                delta = (H2 - H1) / (xLrgDiv * Math.Pow(10.0, Intrval))
                xLDi = H1
                resi = func(vsl, vsg, xLDi)
                slpi = 0.0
                scanning = False

                Dim restart As Boolean = False
                Dim finished As Boolean = False

                Do ' label 15 - scan one interval
                    If Math.Abs(H2 - xLDi) / xLDi < Htol Then
                        If iFound = 0 Then
                            If Intrval < MaxIntvl Then
                                Intrval += 1
                                restart = True
                                Exit Do
                            Else
                                ' label 800 - tried the smallest interval and
                                ' still found nothing; zeros are returned.
                                LastWarning = "Petalas-Aziz: root could not be bracketed."
                            End If
                        End If
                        Exit Do ' label 100
                    Else
                        If scanning Then
                            xLDj = xLDi + dScan
                            ' Turn off scanning at the end of the scan range
                            If xLDj > xScan Then scanning = False
                        End If
                        If Not scanning Then
                            xLDj = xLDi + delta
                            If xLDj > H2 Then xLDj = H2
                        End If
                    End If

                    resj = func(vsl, vsg, xLDj)
                    slp = (resj - resi) / (xLDj - xLDi)

                    Dim again As Boolean = True
                    Do While again ' label 20
                        again = False

                        If resj / resi < 0.0 Then
                            ' The residual changes sign. Reduce the interval
                            ' to within 1%, then locate the root.
                            Do While xLDj / xLDi > 1.01 ' label 21
                                xLD = (xLDi + xLDj) / 2.0
                                res = func(vsl, vsg, xLD)
                                If res / resi > 0.0 Then
                                    xLDi = xLD
                                    resi = res
                                Else
                                    xLDj = xLD
                                    resj = res
                                End If
                            Loop

                            xLD = Fibb(func, xLDi, xLDj, vsl, vsg)
                            res = func(vsl, vsg, xLD)
                            iFound += 1
                            Root(iFound) = xLD
                            ' rRes is the residual at (x + dx), where
                            ' x <= root <= (x + dx)
                            rRes = resj
                            If iFound = 3 OrElse iRoot = 0 Then
                                finished = True
                                Exit Do
                            End If

                        ElseIf slp = 0.0 Then
                            ' A minimum/maximum, not a root.
                            scanning = False
                            H1 = xLDj

                        ElseIf slpi / slp < 0.0 Then
                            ' The slope changed sign. Rescan from (x - dx) to
                            ' (x + dx) with the finer interval dScan.
                            If scanning Then
                                xLDn = xLDi - dScan
                            Else
                                xLDn = xLDi - delta
                            End If
                            If xLDn < H1 Then xLDn = H1
                            dScan = (xLDj - xLDn) / xSmlDiv

                            If dScan < Htol Then
                                ' dScan cannot go below Htol; stop scanning
                                ' and resume from the end of the scan range.
                                scanning = False
                                xLDj = xScan
                                resj = func(vsl, vsg, xLDj)
                                slp = (resj - resi) / (xLDj - xLDi)
                                If resj / resi < 0.0 Then
                                    again = True
                                    Continue Do
                                End If
                                H1 = xLDj
                            Else
                                If Not scanning Then
                                    scanning = True
                                    xScan = xLDj
                                    sScan = slp
                                    rRes = 0.0
                                End If
                                H1 = xLDn
                                xLDj = xLDn
                                resj = func(vsl, vsg, xLDj)
                                slp = slpi
                            End If

                        ElseIf scanning AndAlso sScan / slp > 0.0 Then
                            ' Scanning, and the slope still has the sign it
                            ' had at the start of the scan range. If the
                            ' current residual and the one at the last root
                            ' oppose, the only root in the region is found.
                            If rRes / resj <= 0.0 Then
                                scanning = False
                                xLDj = xScan
                                resj = func(vsl, vsg, xLDj)
                                slp = (resj - resi) / (xLDj - xLDi)
                                H1 = xLDj
                            End If
                        End If
                    Loop

                    If finished Then Exit Do

                    ' label 30 - carry values into the next interval
                    xLDi = xLDj
                    resi = resj
                    slpi = slp
                Loop

                If Not restart Then Exit Do
            Loop

            ' label 100 - sort the roots found
            For i As Integer = 1 To iFound - 1
                For j As Integer = i + 1 To iFound
                    If Root(i) > Root(j) Then
                        Dim t As Double = Root(i)
                        Root(i) = Root(j)
                        Root(j) = t
                    End If
                Next
            Next

            Dim answer As Double
            If iRoot <= 1 OrElse iFound <= 1 Then
                answer = Root(1)
            ElseIf iRoot = 2 AndAlso iFound > 2 Then
                answer = Root(2)
            Else
                answer = Root(iFound)
            End If

            ' Re-evaluate so the state fields hold the values at the root.
            res = func(vsl, vsg, answer)
            Return answer
        End Function

        ''' <summary>
        ''' SUBROUTINE FIBB - Fibonacci search for the minimum of |func| in
        ''' [hh1, hh2], to a tolerance of 0.001. Returns the located abscissa.
        ''' </summary>
        Private Shared Function Fibb(func As Residual, hh1 As Double, hh2 As Double,
                                     vsl As Double, vsg As Double) As Double

            Dim FIB(50) As Double
            Const TOL As Double = 0.001

            Dim A As Double = hh1
            Dim B As Double = hh2

            ' First three Fibonacci numbers
            FIB(1) = 1.0
            FIB(2) = 2.0

            Dim BB As Double = 1.0 / TOL

            Dim JJ As Integer = 2
            Dim CC As Double
            Do
                JJ += 1
                FIB(JJ) = FIB(JJ - 1) + FIB(JJ - 2)
                CC = FIB(JJ)
            Loop While CC < BB AndAlso JJ < 50

            ' First step in the tableau
            Dim KK As Integer = JJ - 2
            Dim IK As Integer = KK
            Dim BL As Double = B - A
            Dim ALL As Double = FIB(IK) * BL / FIB(JJ)
            Dim W As Double = A + ALL
            Dim V As Double = B - ALL
            Dim T As Double = Math.Abs(func(vsl, vsg, W))
            Dim U As Double = Math.Abs(func(vsl, vsg, V))

            ' Succeeding steps in the tableau
            IK -= 1
            JJ -= 1
            For i As Integer = 1 To KK
                If U <= T Then
                    A += ALL
                    BL = B - A
                    W = V
                    T = Math.Abs(func(vsl, vsg, W))
                    ALL = FIB(IK) * BL / FIB(JJ)
                    V = B - ALL
                    U = Math.Abs(func(vsl, vsg, V))
                Else
                    B -= ALL
                    BL = B - A
                    V = W
                    U = Math.Abs(func(vsl, vsg, V))
                    ALL = FIB(IK) * BL / FIB(JJ)
                    W = A + ALL
                    T = Math.Abs(func(vsl, vsg, W))
                End If
                IK -= 1
                JJ -= 1
                If IK < 1 Then IK = 1
                If JJ < 1 Then JJ = 1
            Next

            ' Final range of the dependent variable
            Dim EPS As Double = 0.001 * W
            Dim DL As Double = W + EPS
            Dim YL As Double = Math.Abs(func(vsl, vsg, DL))
            If YL <= T Then
                Return (W + B) / 2.0
            Else
                Return (W + A) / 2.0
            End If
        End Function

#End Region

#Region "FUNCTION VsGfrTr - superficial gas velocity at the Froth transitions"

        Private Function VsGfrTr(iTr As Integer, vsl As Double, vsg As Double,
                                 ByRef dPtr As Double, ByRef eLtr As Double) As Double

            If recursionDepth >= RecursionLimit Then Return 0.0
            recursionDepth += 1
            Try
                Dim VsLo As Double = vsl
                Dim VsGo As Double = vsg
                Dim dLDo As Double = dLD
                Dim hLDo As Double = hLD
                Dim low As Boolean = False
                Dim high As Boolean = False

                Dim iReg0 As Integer = IPetAz(vsl, vsg)
                Dim iReg As Integer
                Dim hiTran As Boolean

                If iTr = 0 Then
                    ' Lower transition (Region/FR); reuse the cached value if any
                    hiTran = False
                    If VsGloTr > 0.0 AndAlso VsGloTr < vsg Then
                        iReg = IPetAz(vsl, VsGloTr)
                        If iReg = iReg0 Then vsg = VsGloTr
                    End If
                Else
                    ' Higher transition (FR/Region); reuse the cached value if any
                    hiTran = True
                    If VsGhiTr > vsg Then
                        iReg = IPetAz(vsl, VsGhiTr)
                        If iReg = iReg0 Then vsg = VsGhiTr
                    End If
                End If

                Dim result As Double
                Dim VsGlo As Double = 0.0
                Dim VsGhi As Double = 0.0
                Dim iRegLo As Integer = 0
                Dim failed As Boolean = False

                iReg = iReg0

                ' label 100 - find upper and lower bounds
                Do
                    If (hiTran AndAlso iReg = iReg0) OrElse
                       (Not hiTran AndAlso iReg <> iReg0) Then
                        ' Low side of the transition
                        VsGlo = vsg
                        iRegLo = iReg
                        low = True
                        vsg = vsg * 1.1
                    Else
                        ' High side of the transition
                        VsGhi = vsg
                        high = True
                        vsg = vsg / 1.1
                    End If

                    If Not high OrElse Not low Then
                        If vsg > Vel_Lim Then
                            failed = True
                            Exit Do
                        End If
                        iReg = IPetAz(vsl, vsg)
                    Else
                        Exit Do
                    End If
                Loop

                If Not failed Then
                    ' label 110 - bisect between the bounds
                    Dim iter As Integer = 1
                    Do
                        vsg = (VsGlo + VsGhi) / 2.0
                        iReg = IPetAz(vsl, vsg)
                        If iReg = iRegLo Then
                            VsGlo = vsg
                        Else
                            VsGhi = vsg
                        End If
                        Dim fx As Double = (VsGlo - VsGhi) / vsg
                        If Math.Abs(fx) > 0.001 Then
                            If iter <= IterLim Then
                                iter += 1
                                Continue Do
                            Else
                                failed = True
                            End If
                        End If
                        Exit Do
                    Loop
                End If

                If failed Then
                    ' label 800 - unsuccessful; dPtr and eLtr are left alone,
                    ' exactly as the FORTRAN does.
                    result = 0.0
                Else
                    ' label 120
                    If hiTran Then
                        result = VsGhi
                        VsGhiTr = VsGlo
                    Else
                        result = VsGlo
                        VsGloTr = VsGhi
                    End If
                    iReg = IPetAz(vsl, result)
                    Dim dPfr, dPhh As Double
                    Dim dP As Double = PPetAz(iReg, vsl, result, eLtr, dPfr, dPhh)
                    dPtr = dPfr * 144.0
                End If

                ' label 900 - restore the state at the original VsG
                vsg = VsGo
                vsl = VsLo
                TaDuk(vsl, vsg)
                hLD = hLDo
                Dim r1 As Double = TDres(vsl, vsg, hLD)
                dLD = dLDo
                Dim r2 As Double = AMres(vsl, vsg, dLD)

                Return result
            Finally
                recursionDepth -= 1
            End Try
        End Function

#End Region

    End Class

End Namespace
