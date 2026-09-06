Imports System.Math
Imports System.Linq
Imports DWSIM.ExtensionMethods
Imports DWSIM.SharedClasses

Namespace DWSIM.Thermodynamics.AdvancedEOS

    Public Class PCSAFTResult

        Public Property Z As Double

        Public Property Ar As Double

        Public Property Hr As Double

        Public Property Sr As Double

        Public Property Cp As Double

        Public Property Cv As Double

        Public Property W As Double

        Public Property JT As Double

    End Class

    Public Class mixture

        Public Property comp As New List(Of pccompound)

        Public Property x As Double()

        Public Property k1 As Double(,)

        Public Property numC As Integer

        Public Property MW As Double

        ' Segment-level representation (Gross, Spuhl, Tumakaka & Sadowski, Ind. Eng. Chem. Res. 42 (2003)
        ' 1266, copolymer PC-SAFT). Each compound contributes one segment type unless it is a copolymer,
        ' which contributes one segment per repeat unit. All arrays are 1-based (index 0 is a dummy) and
        ' are built by BuildSegments once the compounds are set. A one-segment-per-compound mixture makes
        ' the segment sums reduce exactly to the original per-compound sums.
        Public Property nseg As Integer
        Public Property segParent As Integer()   ' parent compound (1-based) of each segment
        Public Property segM As Double()         ' segment count m_iR of each segment type
        Public Property segSigma As Double()
        Public Property segEps As Double()
        Public Property segK As Double(,)        ' segment-segment kij (1-based, nseg x nseg)
        ' Hard-chain bonds, per compound (element i-1 holds compound i, 1-based). A homopolymer or small
        ' molecule has a single self-bond with bonding fraction 1; a copolymer has the Table 1 bonds.
        Public Property bondA As List(Of Integer())   ' global segment index of bond end A
        Public Property bondB As List(Of Integer())   ' global segment index of bond end B
        Public Property bondF As List(Of Double())    ' bonding fraction B of each bond
        Public Property hasCopolymer As Boolean        ' true if any compound expands to more than one segment

    End Class

    Public Class pccompound

        Public Property EosParam() As Object

    End Class

    Public Class PCSAFT2

        Public Property mix As mixture

        Private numC As Integer

        ' Universal dispersion-term constants (Gross & Sadowski 2001, Tables 1-2), 1-indexed with a
        ' dummy 0 slot. Hoisted to shared read-only arrays so they are not re-assigned on every call
        ' of Z_disp / mu_Disp / the density objective (which runs many times per fugacity evaluation).
        Private Shared ReadOnly adisp0 As Double() = {0.0, 0.9105631445, 0.6361281449, 2.6861347891, -26.547362491, 97.759208784, -159.59154087, 91.297774084}
        Private Shared ReadOnly adisp1 As Double() = {0.0, -0.3084016918, 0.1860531159, -2.5030047259, 21.419793629, -65.25588533, 83.318680481, -33.74692293}
        Private Shared ReadOnly adisp2 As Double() = {0.0, -0.0906148351, 0.4527842806, 0.5962700728, -1.7241829131, -4.1302112531, 13.77663187, -8.6728470368}
        Private Shared ReadOnly bdisp0 As Double() = {0.0, 0.7240946941, 2.2382791861, -4.0025849485, -21.003576815, 26.855641363, 206.55133841, -355.60235612}
        Private Shared ReadOnly bdisp1 As Double() = {0.0, -0.5755498075, 0.6995095521, 3.892567339, -17.215471648, 192.67226447, -161.82646165, -165.20769346}
        Private Shared ReadOnly bdisp2 As Double() = {0.0, 0.0976883116, -0.2557574982, -9.155856153, 20.642075974, -38.804430052, 93.626774077, -29.666905585}

        ' The temperature-dependent segment diameter d(i) is a function of T only (Eq. 3), but it was
        ' recomputed - each with an Exp - in every Z_hc/Z_disp/Z_ass/mu call, i.e. once per component
        ' on every density-solver iteration. mix is fixed for the life of this instance, so cache d by T.
        Private _dCacheT As Double = Double.NaN
        Private _dCache As Double()

        Private Function GetD(T As Double) As Double()
            If _dCache IsNot Nothing AndAlso _dCacheT = T Then Return _dCache
            Dim d = zeros(mix.numC)
            For i = 1 To mix.numC
                d(i) = HardSphereDiameter(T, mix.comp(i).EoSParam(1), mix.comp(i).EoSParam(2), mix.comp(i).EoSParam(3))
            Next
            _dCache = d
            _dCacheT = T
            Return d
        End Function

        Private _segDCacheT As Double = Double.NaN
        Private _segDCache As Double()

        ' Temperature-dependent diameter d of every SEGMENT type (Eq. 3, independent of m). Cached by T on
        ' the instance, like GetD. For a one-segment-per-compound mixture these equal the compound diameters.
        Private Function GetSegD(mixt As mixture, T As Double) As Double()
            If _segDCache IsNot Nothing AndAlso _segDCacheT = T Then Return _segDCache
            Dim d = zeros(mixt.nseg)
            For s = 1 To mixt.nseg
                d(s) = mixt.segSigma(s) * (1 - 0.12 * Exp(-3 * mixt.segEps(s) / T))
            Next
            _segDCache = d
            _segDCacheT = T
            Return d
        End Function

        ' Segment-segment interaction parameter, looked up in pcsaft_ip.dat by the two segment CAS numbers
        ' in either order. Covers both the copolymer's internal repeat-unit correction and the ordinary
        ' cross-molecule kij; a missing pair is zero.
        Private Function SegKij(pp As PCSAFT2PropertyPackage, casA As String, casB As String) As Double
            If pp.InteractionParameters.ContainsKey(casA) AndAlso pp.InteractionParameters(casA).ContainsKey(casB) Then
                Return pp.InteractionParameters(casA)(casB).kij
            End If
            If pp.InteractionParameters.ContainsKey(casB) AndAlso pp.InteractionParameters(casB).ContainsKey(casA) Then
                Return pp.InteractionParameters(casB)(casA).kij
            End If
            Return 0.0
        End Function

        ' Builds the segment-level view of the mixture (Gross et al. 2003). An ordinary compound is one
        ' segment (its own parameters and CAS, a self-bond of fraction 1). A copolymer (PCSParam.copolymer
        ' set) expands to one segment per repeat unit: segment parameters come from the homopolymer keyed by
        ' the repeat-unit CAS, the segment number is m_iR = w_iR * M_copoly * (m/M)_R, the segment fractions
        ' and Table 1 bonding fractions follow, and the segment-segment kij is read from pcsaft_ip.dat by
        ' CAS. A one-segment-per-compound mixture reproduces the per-compound sums exactly.
        Private Sub BuildSegments(pp As PCSAFT2PropertyPackage, compounds As Object, mixt As mixture)
            Dim nc = mixt.numC

            Dim casByComp As New List(Of List(Of String))
            Dim mByComp As New List(Of List(Of Double))
            Dim sigByComp As New List(Of List(Of Double))
            Dim epsByComp As New List(Of List(Of Double))
            Dim bLocA As New List(Of List(Of Integer))
            Dim bLocB As New List(Of List(Of Integer))
            Dim bFrac As New List(Of List(Of Double))

            For i = 1 To nc
                Dim c = compounds(i - 1)
                Dim cas As String = c.CAS_Number
                Dim prm = pp.CompoundParameters(cas)
                Dim casL As New List(Of String), mL As New List(Of Double), sigL As New List(Of Double), epsL As New List(Of Double)
                Dim bA As New List(Of Integer), bB As New List(Of Integer), bF As New List(Of Double)

                If prm.copolymer Is Nothing OrElse prm.copolymer.Trim() = "" Then
                    casL.Add(cas)
                    mL.Add(mixt.comp(i).EoSParam(1))
                    sigL.Add(mixt.comp(i).EoSParam(2))
                    epsL.Add(mixt.comp(i).EoSParam(3))
                    bA.Add(0) : bB.Add(0) : bF.Add(1.0)
                Else
                    For Each part In prm.copolymer.Split(";"c)
                        Dim kv = part.Split(":"c)
                        Dim scas = kv(0).Trim()
                        Dim wR = Double.Parse(kv(1).Trim(), Globalization.CultureInfo.InvariantCulture)
                        Dim sprm = pp.CompoundParameters(scas)
                        casL.Add(scas)
                        mL.Add(wR * c.Molar_Weight * sprm.m_over_M)
                        sigL.Add(sprm.sigma)
                        epsL.Add(sprm.epsilon)
                    Next
                    Dim mtot = mL.Sum()
                    mixt.comp(i).EoSParam(1) = mtot
                    Dim savg = 0.0, eavg = 0.0
                    For si = 0 To mL.Count - 1
                        savg += (mL(si) / mtot) * sigL(si)
                        eavg += (mL(si) / mtot) * epsL(si)
                    Next
                    mixt.comp(i).EoSParam(2) = savg
                    mixt.comp(i).EoSParam(3) = eavg

                    If mL.Count = 2 Then
                        Dim z0 = mL(0) / mtot, z1 = mL(1) / mtot
                        Dim seq As String = If(prm.coseq Is Nothing, "", prm.coseq.Trim().ToLowerInvariant())
                        Dim Brr, Brb, Bbb As Double
                        If seq = "alternating" Then
                            Brb = 1.0 : Brr = 0.0 : Bbb = 0.0
                        ElseIf z1 <= z0 Then
                            Brb = 2.0 * z1 * mtot / (mtot - 1.0) : Bbb = 0.0 : Brr = 1.0 - Brb
                        Else
                            Brb = 2.0 * z0 * mtot / (mtot - 1.0) : Brr = 0.0 : Bbb = 1.0 - Brb
                        End If
                        If Brr > 0.0 Then bA.Add(0) : bB.Add(0) : bF.Add(Brr)
                        If Brb > 0.0 Then bA.Add(0) : bB.Add(1) : bF.Add(Brb)
                        If Bbb > 0.0 Then bA.Add(1) : bB.Add(1) : bF.Add(Bbb)
                    Else
                        bA.Add(0) : bB.Add(0) : bF.Add(1.0)
                    End If
                End If

                casByComp.Add(casL) : mByComp.Add(mL) : sigByComp.Add(sigL) : epsByComp.Add(epsL)
                bLocA.Add(bA) : bLocB.Add(bB) : bFrac.Add(bF)
            Next

            Dim total = 0
            For i = 0 To nc - 1
                total += casByComp(i).Count
            Next
            mixt.nseg = total
            mixt.segParent = New Integer(total) {}
            mixt.segM = New Double(total) {}
            mixt.segSigma = New Double(total) {}
            mixt.segEps = New Double(total) {}
            Dim segCasFlat(total) As String
            Dim compFirstSeg(nc) As Integer
            Dim g = 0
            For i = 1 To nc
                compFirstSeg(i) = g + 1
                For si = 0 To casByComp(i - 1).Count - 1
                    g += 1
                    mixt.segParent(g) = i
                    mixt.segM(g) = mByComp(i - 1)(si)
                    mixt.segSigma(g) = sigByComp(i - 1)(si)
                    mixt.segEps(g) = epsByComp(i - 1)(si)
                    segCasFlat(g) = casByComp(i - 1)(si)
                Next
            Next

            mixt.bondA = New List(Of Integer())
            mixt.bondB = New List(Of Integer())
            mixt.bondF = New List(Of Double())
            For i = 1 To nc
                Dim la = bLocA(i - 1), lb = bLocB(i - 1), lf = bFrac(i - 1)
                Dim ga(lf.Count - 1) As Integer, gb(lf.Count - 1) As Integer, gf(lf.Count - 1) As Double
                For bi = 0 To lf.Count - 1
                    ga(bi) = compFirstSeg(i) + la(bi)
                    gb(bi) = compFirstSeg(i) + lb(bi)
                    gf(bi) = lf(bi)
                Next
                mixt.bondA.Add(ga) : mixt.bondB.Add(gb) : mixt.bondF.Add(gf)
            Next

            mixt.segK = New Double(total, total) {}
            For a = 1 To total
                For b = 1 To total
                    mixt.segK(a, b) = SegKij(pp, segCasFlat(a), segCasFlat(b))
                Next
            Next

            mixt.hasCopolymer = (total > nc)
        End Sub

        ' Segment-view reduced density (Eq. 9) and the two dispersion perturbation sums (Eqs. A12, A13),
        ' summed over segment types with weight w_s = x_i * m_iR and the segment-pair combining rules
        ' (Eqs. A14, A15). For a one-segment-per-compound mixture these reduce to the per-compound sums.
        Private Sub SegDispSums(mixt As mixture, T As Double, dens_num As Double,
                                ByRef dens_red As Double, ByRef prom1 As Double, ByRef prom2 As Double)
            Dim segd = GetSegD(mixt, T)
            Dim ns = mixt.nseg
            Dim w = zeros(ns)
            Dim sa, sb As Integer
            For sa = 1 To ns
                w(sa) = mixt.x(mixt.segParent(sa)) * mixt.segM(sa)
            Next
            dens_red = 0
            For sa = 1 To ns
                dens_red = dens_red + w(sa) * segd(sa) ^ 3
            Next
            dens_red = dens_red * PI / 6 * dens_num
            prom1 = 0
            prom2 = 0
            For sa = 1 To ns
                For sb = 1 To ns
                    Dim sij As Double = 0.5 * (mixt.segSigma(sa) + mixt.segSigma(sb))
                    Dim eij As Double = Sqrt(mixt.segEps(sa) * mixt.segEps(sb)) * (1 - mixt.segK(sa, sb))
                    prom1 = prom1 + w(sa) * w(sb) * eij / T * sij ^ 3
                    prom2 = prom2 + w(sa) * w(sb) * (eij / T) ^ 2 * sij ^ 3
                Next
            Next
        End Sub

        ' Hard-sphere radial distribution at contact for a bonded segment pair of diameters da, db (Eq. 8),
        ' given the zeta auxiliaries (1-based: auxil(1..4) = zeta_0..zeta_3).
        Private Function GhsSeg(da As Double, db As Double, auxil As Double()) As Double
            Dim t1 As Double = 1 / (1 - auxil(4))
            Dim t2 As Double = da * db / (da + db) * 3 * auxil(3) / (1 - auxil(4)) ^ 2
            Dim t3 As Double = (da * db / (da + db)) ^ 2 * 2 * auxil(3) ^ 2 / (1 - auxil(4)) ^ 3
            Return t1 + t2 + t3
        End Function

        ' Density derivative of the hard-sphere radial distribution at a bonded segment-pair contact
        ' (Eq. A27), used by the hard-chain compressibility.
        Private Function DensDgDensSeg(da As Double, db As Double, auxil As Double()) As Double
            Dim t1 As Double = auxil(4) / (1 - auxil(4)) ^ 2
            Dim t2 As Double = (da * db) / (da + db) * (3 * auxil(3) / (1 - auxil(4)) ^ 2 + 6 * auxil(3) * auxil(4) / (1 - auxil(4)) ^ 3)
            Dim t3 As Double = (da * db / (da + db)) ^ 2 * (4 * auxil(3) ^ 2 / (1 - auxil(4)) ^ 3 + 6 * auxil(3) ^ 2 * auxil(4) / (1 - auxil(4)) ^ 4)
            Return t1 + t2 + t3
        End Function

        Public Sub New(pp As PCSAFT2PropertyPackage, molefractions() As Double)


            'InitTests()

            InitPP(pp, molefractions)

        End Sub

        Private Sub InitTests()

            numC = 2 'compounds.Count

            Dim CO2 As New pccompound
            CO2.EosParam = New List(Of Object)
            CO2.EosParam.Add(0.0)
            CO2.EosParam.Add(2.0729) 'm
            CO2.EosParam.Add(2.7852) 'sigma
            CO2.EosParam.Add(169.21) 'epsilon/k
            CO2.EosParam.Add(0) 'NumAss
            CO2.EosParam.Add(New Double(,) {})
            CO2.EosParam.Add(New Double(,) {})

            Dim H2O As New pccompound
            H2O.EosParam = New List(Of Object)
            H2O.EosParam.Add(0.0)
            H2O.EosParam.Add(1.09528) 'm
            H2O.EosParam.Add(2.8898) 'sigma
            H2O.EosParam.Add(365.956) 'epsilon/k
            H2O.EosParam.Add(2) 'NumAss
            H2O.EosParam.Add(New Double(,) {{0.0, 0.0, 0.0}, {0.0, 0.0, 0.03487}, {0.0, 0.03487, 0.0}})
            H2O.EosParam.Add(New Double(,) {{0.0, 0.0, 0.0}, {0.0, 0.0, 2515.7}, {0.0, 2515.7, 0.0}})

            mix = New mixture
            mix.numC = 2
            mix.comp.Add(Nothing)
            mix.comp.Add(CO2)
            mix.comp.Add(H2O)
            mix.x = New Double() {0.0, 0.014971914105686482, 0.98502808589431357}

            mix.MW = 18

            mix.k1 = New Double(,) {{0.0, 0.0, 0.0}, {0.0, 0.0, 0.0}, {0.0, 0.0, 0.0}}

        End Sub

        Private Sub InitPP(pp As PCSAFT2PropertyPackage, molefractions() As Double)

            Dim i, j As Integer

            Dim compounds = pp.DW_GetConstantProperties

            numC = compounds.Count

            mix = New mixture
            mix.numC = compounds.Count
            mix.comp.Add(Nothing)
            mix.x = zeros(molefractions.Length)
            molefractions.CopyTo(mix.x, 1)

            mix.MW = pp.AUX_MMM(molefractions)

            Dim nk As Double = numC

            Dim kvec(nk, nk) As Double

            For i = 1 To nk
                For j = 1 To nk
                    If pp.InteractionParameters.ContainsKey(compounds(i - 1).CAS_Number) Then
                        If pp.InteractionParameters(compounds(i - 1).CAS_Number).ContainsKey(compounds(j - 1).CAS_Number) Then
                            kvec(i, j) = pp.InteractionParameters(compounds(i - 1).CAS_Number)(compounds(j - 1).CAS_Number).kij
                            kvec(j, i) = kvec(i, j)
                        End If
                    ElseIf pp.InteractionParameters.ContainsKey(compounds(j - 1).CAS_Number) Then
                        If pp.InteractionParameters(compounds(j - 1).CAS_Number).ContainsKey(compounds(i - 1).CAS_Number) Then
                            kvec(i, j) = pp.InteractionParameters(compounds(j - 1).CAS_Number)(compounds(i - 1).CAS_Number).kij
                            kvec(j, i) = kvec(i, j)
                        End If
                    End If
                Next
            Next

            mix.k1 = kvec

            Dim assocparam, assocparaml(), vm, em As String
            Dim na As Integer

            For Each c In compounds

                Dim cproxy As New pccompound

                Dim prm = pp.CompoundParameters(c.CAS_Number)

                ' A polymer (m_over_M > 0) has a segment number proportional to its molar mass:
                ' m = (m/M) * Molar_Weight. Small molecules keep their tabulated absolute m.
                Dim mSeg As Double = If(prm.m_over_M > 0.0, prm.m_over_M * c.Molar_Weight, prm.m)

                cproxy.EosParam = New List(Of Object)
                cproxy.EosParam.Add(0.0)
                cproxy.EosParam.Add(mSeg) 'm
                cproxy.EosParam.Add(prm.sigma) 'sigma
                cproxy.EosParam.Add(prm.epsilon) 'epsilon/k

                assocparam = prm.associationparams

                If assocparam <> "" Then

                    assocparaml = assocparam.Split(vbCrLf)
                    na = Integer.Parse(assocparam(0))
                    vm = assocparaml(1).Trim().Trim(vbLf).Trim("[", "]")
                    em = assocparaml(2).Trim().Trim(vbLf).Trim("[", "]")

                    cproxy.EosParam.Add(na) 'NumAss

                    Dim vmvec(na, na), emvec(na, na) As Double

                    i = 1
                    For Each line In vm.Split(";")
                        j = 1
                        For Each value In line.Trim().Split(" ")
                            vmvec(i, j) = value.ToDoubleFromInvariant
                            j += 1
                        Next
                        i += 1
                    Next

                    cproxy.EosParam.Add(vmvec)

                    i = 1
                    For Each line In em.Split(";")
                        j = 1
                        For Each value In line.Trim().Split(" ")
                            emvec(i, j) = value.ToDoubleFromInvariant
                            j += 1
                        Next
                        i += 1
                    Next

                    cproxy.EosParam.Add(emvec)

                    ' Site multiplicities (how many of each site type). Default one per type (2B). A 4C
                    ' scheme has two donors and two acceptors; a PEG-type 4C/ETHER chain adds
                    ' N_ether = 0.022*Mn - 1.409 ether-oxygen acceptor sites to the acceptor type
                    ' (Kontogeorgis & Folas eq. 14.9). Site 1 is the donor type, site 2 the acceptor type.
                    Dim mult(na) As Double
                    For si As Integer = 1 To na
                        mult(si) = 1.0
                    Next
                    Dim sch As String = prm.scheme.Trim().ToUpperInvariant()
                    If (sch = "4C" OrElse sch = "4C/ETHER") AndAlso na >= 2 Then
                        mult(1) = 2.0
                        mult(2) = 2.0
                        If sch = "4C/ETHER" Then
                            Dim nEther As Double = 0.022 * c.Molar_Weight - 1.409
                            If nEther < 0.0 Then nEther = 0.0
                            mult(2) += nEther
                        End If
                    End If

                    cproxy.EosParam.Add(mult) 'site multiplicities

                    If sum2(vmvec) + sum2(emvec) = 0.0 Then
                        cproxy.EosParam(4) = 0
                    End If

                Else

                    cproxy.EosParam.Add(0) 'NumAss
                    cproxy.EosParam.Add(New Double(,) {})
                    cproxy.EosParam.Add(New Double(,) {})
                    cproxy.EosParam.Add(New Double() {}) 'site multiplicities

                End If

                mix.comp.Add(cproxy)

            Next

            BuildSegments(pp, compounds, mix)

        End Sub

        Public Function CalcFugCoeff(T As Double, P As Double, liq_or_gas As String, Zestimate As Double) As Double()

            Return FugF(T, P, mix, liq_or_gas, Zestimate)

        End Function

        Public Function CalcLnFugCoeff(T As Double, P As Double, liq_or_gas As String, Zestimate As Double) As Double()

            Return LogFugF(T, P, mix, liq_or_gas, Zestimate)

        End Function

        Public Function CalcZ(T As Double, P As Double, liq_or_gas As String, Zestimate As Double) As Double

            Dim Z = compr(T, P, mix, liq_or_gas, Zestimate)

            Return Z

        End Function

        Public Function CalcCp(T As Double, P As Double, liq_or_gas As String, Zestimate As Double, HidFunc As Func(Of Double, Double)) As Double

            ' Cp = dH/dT by a central difference (second order in h, so it drops the leading truncation
            ' bias a forward difference carries).
            Dim h = 0.1

            Dim hplus, hminus As Double
            Dim t1, t2 As Task

            t1 = TaskHelper.Run(Sub() hplus = CalcHr(T + h, P, liq_or_gas, Zestimate) + HidFunc.Invoke(T + h) * mix.MW)
            t2 = TaskHelper.Run(Sub() hminus = CalcHr(T - h, P, liq_or_gas, Zestimate) + HidFunc.Invoke(T - h) * mix.MW)

            Task.WaitAll(t1, t2)

            Dim cp = (hplus - hminus) / (2.0 * h)

            Return cp / mix.MW

        End Function

        Public Function CalcCv2(T As Double, P As Double, liq_or_gas As String, Zestimate As Double, Cp As Double) As Double

            Dim R = 8.314

            Dim timespans As New List(Of TimeSpan)

            Dim d1 As Date = Date.Now

            Dim Z = compr(T, P, mix, liq_or_gas, Zestimate)

            timespans.Add(Date.Now - d1)
            d1 = Date.Now

            Dim V = Z * R * T / P

            Dim dFdV, d2FdV2, dPdV, dPdT As Double

            dFdV = CalcdFdV(T, P, liq_or_gas, Z)

            Dim Pcheck = -R * T * dFdV + R * T / V

            Dim dFdVcheck = (P - R * T / V) / (-R * T)

            d2FdV2 = CalcdF2dV2(T, P, liq_or_gas, Z)

            dPdV = -R * T * d2FdV2 - R * T / V ^ 2

            dPdT = -R * T * Calcd2FdTdV(T, P, liq_or_gas, Z) + P / T

            Dim Cv = Cp + T * dPdT ^ 2 / dPdV / mix.MW

            Return Cv

        End Function

        Public Function CalcCv(T As Double, P As Double, liq_or_gas As String, Zestimate As Double, SidFunc As Func(Of Double, Double, Double)) As Double

            Dim epsilon = If(liq_or_gas = "gas", 0.1, 0.0001)

            Dim R = 8.314

            Dim Z = compr(T, P, mix, liq_or_gas, Zestimate)

            Dim P2, Z2, Z2_ant, P2_ant, fP As Double

            Dim V = Z * R * T / P

            Dim nloops As Integer = 0

            P2 = P
            Z2 = Z

            If liq_or_gas = "gas" Then

                Do

                    Z2_ant = Z2
                    Z2 = compr(T + epsilon, P2, mix, liq_or_gas, Z2_ant)

                    fP = (Z2 - Z2_ant)

                    If Abs(fP) < 0.0000000001 Then Exit Do

                    P2_ant = P2
                    P2 = (Z2 * R * (T + epsilon) / V)

                    nloops += 1

                Loop

            Else

                P2 = P
                Z2 = compr(T + epsilon, P2, mix, liq_or_gas, Z)

            End If

            Dim t1, t2 As Task
            Dim s1, s2 As Double

            t1 = TaskHelper.Run(Sub() s1 = CalcSr(T, P, liq_or_gas, Z) + SidFunc.Invoke(T, P) * mix.MW)
            t2 = TaskHelper.Run(Sub() s2 = CalcSr(T + epsilon, P2, liq_or_gas, Z2) + SidFunc.Invoke(T + epsilon, P2) * mix.MW)

            Task.WaitAll(t1, t2)

            Dim cv = (s2 - s1) * T / epsilon

            Return cv / mix.MW

        End Function

        Public Function CalcHr(T As Double, P As Double, liq_or_gas As String, Zestimate As Double) As Double

            ' Residual enthalpy: Hr/RT = -T*(d a_res / dT)_rho + (Z - 1), where the derivative of the
            ' dimensionless residual Helmholtz energy is taken at CONSTANT DENSITY. The density is held
            ' fixed by scaling Z so that dens_num = P/(Z*k*T) is unchanged at T +/- h (Z scales as T^-1),
            ' and a central difference makes the derivative second order. The previous version differenced
            ' at constant pressure (density recomputed at T+eps) with a one-sided step, which carried both
            ' a spurious (d a/d rho)(d rho/dT)_P term and a first-order truncation bias.

            Dim R = 8.314
            Dim h = 0.1

            Dim Z = compr(T, P, mix, liq_or_gas, Zestimate)

            ' Z at T +/- h that reproduces the same number density as at (T, P)
            Dim Zp = Z * T / (T + h)
            Dim Zm = Z * T / (T - h)

            Dim Ap, Am As Double
            Dim t1, t2 As Task

            t1 = TaskHelper.Run(Sub() Ap = Helmholtz(T + h, P, mix, liq_or_gas, Zp))
            t2 = TaskHelper.Run(Sub() Am = Helmholtz(T - h, P, mix, liq_or_gas, Zm))

            Task.WaitAll(t1, t2)

            Dim dArdT = (Ap - Am) / (2.0 * h)

            Return R * T * (-T * dArdT + (Z - 1)) 'kJ/kmol

        End Function

        Public Function CalcSr(T As Double, P As Double, liq_or_gas As String, Zestimate As Double) As Double

            Dim R = 8.314

            Dim Z As Double = compr(T, P, mix, liq_or_gas, Zestimate)

            Dim Ar = Helmholtz(T, P, mix, liq_or_gas, Z)

            Dim Gr_RT = Ar + (Z - 1) - Log(Z)

            Dim Hr_RT = CalcHr(T, P, liq_or_gas, Zestimate) / (R * T)

            Dim Sr = R * (Hr_RT - Gr_RT)

            Return Sr

        End Function

        Private Function Calcd2FdT2(T As Double, P As Double, liq_or_gas As String, Zestimate As Double) As Double

            Dim epsilon = 1.0

            Dim R = 8.314

            Dim Z = Zestimate

            Dim t1, t2 As Task

            Dim Ar, Ar2, P2, Z2, Z2_ant As Double

            Dim V = Z * R * T / P

            Dim nloops As Integer = 0

            If liq_or_gas = "gas" Then

                P2 = P
                Z2 = Z
                Do
                    Z2_ant = Z2
                    Z2 = compr(T + epsilon, P2, mix, liq_or_gas, Z2_ant)
                    P2 = (Z2 * R * (T + epsilon) / V) * 0.5 + P2 * 0.5
                    nloops += 1
                Loop Until Abs((Z2 - Z2_ant) / Z2) < 0.00001

            Else

                P2 = P
                Z2 = compr(T + epsilon, P2, mix, liq_or_gas, Z2_ant)

            End If

            t1 = TaskHelper.Run(Sub() Ar = CalcdFdT(T, P, liq_or_gas, Z))
            t2 = TaskHelper.Run(Sub() Ar2 = CalcdFdT(T + epsilon, P2, liq_or_gas, Z2))

            Task.WaitAll(t1, t2)

            Dim dF = (Ar2 - Ar) / epsilon

            Return dF

        End Function

        Public Function CalcJT(T As Double, P As Double, liq_or_gas As String, Zestimate As Double, Cp As Double) As PCSAFTResult

            Dim R = 8.314

            Dim epsilon = 0.001

            Dim Z = compr(T, P, mix, liq_or_gas, Zestimate)

            Dim V = Z * R * T / P

            Dim d2FdV2, dPdV, dPdT As Double

            Dim t1, t2 As Task

            t1 = TaskHelper.Run(Sub()
                                    d2FdV2 = CalcdF2dV2(T, P, liq_or_gas, Z)
                                    dPdV = -R * T * d2FdV2 - R * T / V ^ 2
                                End Sub)

            t2 = TaskHelper.Run(Sub() dPdT = -R * T * Calcd2FdTdV(T, P, liq_or_gas, Z) + P / T)

            Task.WaitAll(t1, t2)

            Dim results As New PCSAFTResult

            With results

                .Z = Z
                .JT = -1 / (Cp * mix.MW) * (V + T * dPdT / dPdV)

            End With

            Return results

        End Function

        Private Function CalcdFdV(T As Double, P As Double, liq_or_gas As String, Zestimate As Double) As Double

            Dim R = 8.314

            Dim Z = Zestimate

            Dim V = Z * R * T / P

            Dim delta = If(liq_or_gas = "gas", 0.01, 0.001)

            Dim epsilon = V * delta

            Dim t1, t2 As Task

            Dim Ar, Ar2, P2, P2_ant, P2_ant2, Z2, Z2_ant, fP_ant, fP_ant2, fP As Double

            Dim nloops As Integer = 0

            Z2 = P * (V + epsilon) / (R * T)
            P2 = P

            If liq_or_gas = "gas" Then

                Do

                    Z2_ant = Z2
                    Z2 = compr(T, P2, mix, liq_or_gas, Z2_ant)

                    P2_ant2 = P2_ant
                    P2_ant = P2

                    If nloops > 3 Then
                        P2 = P2 - fP * (P2 - P2_ant2) / (fP - fP_ant2)
                    Else
                        P2 = (Z2 * R * T / (V + epsilon))
                    End If

                    fP_ant2 = fP_ant
                    fP_ant = fP
                    fP = (P2 - P2_ant)

                    nloops += 1

                Loop Until Abs(fP) < 0.0000000001 And nloops > 3

            End If

            t1 = TaskHelper.Run(Sub() Ar = (Helmholtz(T, P, mix, liq_or_gas, Z) + R * T * Log(Z)) / (R * T))

            t2 = TaskHelper.Run(Sub() Ar2 = (Helmholtz(T, P2, mix, liq_or_gas, Z2) + R * T * Log(Z2)) / (R * T))

            Task.WaitAll(t1, t2)

            Dim dF = (Ar2 - Ar) / epsilon

            Return dF

        End Function

        Private Function CalcdFdT(T As Double, P As Double, liq_or_gas As String, Zestimate As Double) As Double

            Dim R = 8.314

            Dim epsilon = If(liq_or_gas = "gas", 1.0, -0.001)

            Dim Z = Zestimate

            Dim Ar, Ar2, P2, Z2, Z2_ant, P2_ant, P2_ant2, fP, fP_ant2, fP_ant As Double

            Dim V = Z * R * T / P

            Dim nloops As Integer = 0

            If liq_or_gas = "gas" Then

                P2 = P
                Z2 = Z

                Do

                    Z2_ant = Z2
                    Z2 = compr(T + epsilon, P2, mix, liq_or_gas, Z2_ant)

                    P2_ant2 = P2_ant
                    P2_ant = P2

                    If nloops > 3 Then
                        P2 = P2 - fP * (P2 - P2_ant2) / (fP - fP_ant2)
                    Else
                        P2 = (Z2 * R * (T + epsilon) / V)
                    End If

                    fP_ant2 = fP_ant
                    fP_ant = fP
                    fP = (P2 - P2_ant)

                    nloops += 1

                Loop Until Abs(fP) < 0.0000000001 And nloops > 3

            Else

                P2 = P
                Z2 = compr(T + epsilon, P2, mix, liq_or_gas, Z)

            End If

            Dim t1, t2 As Task

            t1 = TaskHelper.Run(Sub() Ar = (Helmholtz(T, P, mix, liq_or_gas, Z) + R * T * Log(Z)) / (R * T))

            t2 = TaskHelper.Run(Sub() Ar2 = (Helmholtz(T + epsilon, P2, mix, liq_or_gas, Z2) + R * (T + epsilon) * Log(Z2)) / (R * (T + epsilon)))

            Task.WaitAll(t1, t2)

            Dim dF = (Ar2 - Ar) / epsilon

            Return dF

        End Function

        Private Function CalcdF2dV2(T As Double, P As Double, liq_or_gas As String, Zestimate As Double) As Double

            Dim R = 8.314

            Dim Z = Zestimate

            Dim V = Z * R * T / P

            Dim epsilon = V * 0.01

            Dim t1, t2 As Task

            Dim Ar, Ar2, P2, P2_ant, P2_ant2, Z2, Z2_ant, fP_ant, fP_ant2, fP As Double

            Dim nloops As Integer = 0

            Z2 = P * (V + epsilon) / (R * T)
            P2 = P

            If liq_or_gas = "gas" Then

                Do

                    Z2_ant = Z2
                    Z2 = compr(T, P2, mix, liq_or_gas, Z2_ant)

                    P2_ant2 = P2_ant
                    P2_ant = P2

                    If nloops > 3 Then
                        P2 = P2 - fP * (P2 - P2_ant2) / (fP - fP_ant2)
                    Else
                        P2 = (Z2 * R * T / (V + epsilon))
                    End If

                    fP_ant2 = fP_ant
                    fP_ant = fP
                    fP = (P2 - P2_ant)

                    nloops += 1

                Loop Until Abs(fP) < 0.0000000001 And nloops > 3

            End If

            t1 = TaskHelper.Run(Sub() Ar = (Helmholtz(T, P, mix, liq_or_gas, Z) + R * T * Log(Z)) / (R * T))
            t2 = TaskHelper.Run(Sub() Ar2 = (Helmholtz(T, P2, mix, liq_or_gas, Z2) + R * T * Log(Z2)) / (R * T))

            Task.WaitAll(t1, t2)

            Dim dF = (Ar2 - Ar) / epsilon

            Return dF

        End Function

        Private Function Calcd2FdTdV(T As Double, P As Double, liq_or_gas As String, Zestimate As Double) As Double

            Dim R = 8.314

            Dim Z = Zestimate

            Dim V = Z * R * T / P

            Dim epsilon = V * 0.01

            Dim t1, t2 As Task

            Dim Ar, Ar2, P2, P2_ant, P2_ant2, Z2, Z2_ant, fP_ant, fP_ant2, fP As Double

            Dim nloops As Integer = 0

            Z2 = P * (V + epsilon) / (R * T)
            P2 = P

            If liq_or_gas = "gas" Then

                Do

                    Z2_ant = Z2
                    Z2 = compr(T, P2, mix, liq_or_gas, Z2_ant)

                    P2_ant2 = P2_ant
                    P2_ant = P2

                    If nloops > 3 Then
                        P2 = P2 - fP * (P2 - P2_ant2) / (fP - fP_ant2)
                    Else
                        P2 = (Z2 * R * T / (V + epsilon))
                    End If

                    fP_ant2 = fP_ant
                    fP_ant = fP
                    fP = (P2 - P2_ant)

                    nloops += 1

                Loop Until Abs(fP) < 0.0000000001 And nloops > 3

            End If

            t1 = TaskHelper.Run(Sub() Ar = CalcdFdT(T, P, liq_or_gas, Z))

            t2 = TaskHelper.Run(Sub() Ar2 = CalcdFdT(T, P2, liq_or_gas, Z2))

            Task.WaitAll(t1, t2)

            Dim dF = (Ar2 - Ar) / epsilon

            Return dF

        End Function

        Friend Function zeros(n, m) As Double(,)

            Dim matrix(n, m) As Double

            Return matrix

        End Function

        Friend Function zeros(n, m, o) As Double(,,)

            Dim matrix(n, m, o) As Double

            Return matrix

        End Function

        Friend Function zeros(n) As Double()

            Dim vector(n) As Double

            Return vector

        End Function

        Friend Function sum(v() As Double) As Double

            Return v.Sum

        End Function

        Public Function max(v(,) As Double) As Double

            Dim maxval As Double = Double.MinValue

            For i = 0 To v.GetUpperBound(0)
                For j = 0 To v.GetUpperBound(1)
                    If v(i, j) > maxval Then maxval = v(i, j)
                Next
            Next

            Return maxval

        End Function

        Public Function sum2(v(,) As Double) As Double

            Dim s As Double = 0

            For i = 0 To v.GetUpperBound(0)
                For j = 0 To v.GetUpperBound(0)
                    s += v(i, j)
                Next
            Next

            Return s

        End Function

        Friend Function FugF(T, P, mix, phase, Zestimate)
            ' Fugacity coefficients. A high segment-number polymer has a log coefficient on the order
            ' of -1e3, so the exponential underflows to zero here; callers that must keep the true
            ' chemical potential (stability test, phase-split estimates) use LogFugF instead.
            Return LogFugF(T, P, mix, phase, Zestimate).Select(Function(lf) Math.Exp(lf)).ToArray()
        End Function

        Friend Function LogFugF(T, P, mix, phase, Zestimate) As Double()

            'Calculates the fugacity And compresibility coefficient of mixture mix at temperature T
            'And pressure P using PC-SAFT EoS
            '
            'Parameters:
            'EoS Equation of state used for calculations
            'T: Temperature(K)
            'P: Pressure(K)
            'mix: cMixture Object
            'phase: set phase = 'liq' to calculate the fugacity of a liquid phase or
            '   phase = 'gas' to calculate the fugacity of a gas phase
            '
            'Optional parameters (set [] to keep default value)
            'Z_ini: Initial guess for the compressibility coefficient
            '   If Not defined, the program uses an initial guess Z_ini = 0.8 for gas
            '   phase And a Z_ini corresponding to a liquid density of 800 kg/m3 for
            '   the liquid phase
            'options: parameters of the fsolve numerical resolution method (structure
            '   generated with "optimset")
            '
            'Results:
            'f fugacity coefficient
            'Z: compresibility coefficient
            'EoS: returns EoS used for calculations
            '
            'Reference: Gross And Sadowski, Ind.Eng.Chem.Res. 40 (2001) 1244-1260
            'Reference 2: Chapman et al., Ind.Eng.Chem.Res. 29 (1990) 1709-1721

            'Copyright (c) 2011 Ángel Martín, University of Valladolid (Spain)
            'This program Is free software: you can redistribute it And/Or modify
            'it under the terms of the GNU General Public License as published by
            'the Free Software Foundation, either version 3 of the License, Or
            '(at your option) any later version.
            'This program Is distributed in the hope that it will be useful,
            'but WITHOUT ANY WARRANTY without even the implied warranty of
            'MERCHANTABILITY Or FITNESS FOR A PARTICULAR PURPOSE.  See the
            'GNU General Public License for more details.
            'You should have received a copy of the GNU General Public License
            'along with this program.  If Not, see <http://www.gnu.org/licenses/>.

            '**************************************************************************
            'Calculates the compresibility coefficient
            '**************************************************************************
            'Constants

            Dim kb, Z, muHC(), muDisp(), NumAss(), muAss(), dens_num, logf() As Double

            kb = 1.3806504E-23 'Boltzmann K (J/K)

            Z = compr(T, P, mix, phase, Zestimate)

            dens_num = P / (Z * kb * T) * 1 / (10000000000.0) ^ 3

            '**************************************************************************
            'Calculates the contributions to the chemical potential
            '**************************************************************************

            If mix.hasCopolymer Then

                ' A copolymer's segments make the per-compound analytical hard-chain and dispersion
                ' derivatives invalid, so take the residual chemical potential of those two terms as a
                ' finite difference of the segment-based Helmholtz energy. Run serially, since it perturbs
                ' mix.x. The association term (zero for the non-associating copolymers) stays analytical.
                muHC = NumMuHCDisp(T, dens_num, mix)
                muDisp = zeros(mix.numC)
                NumAss = zeros(mix.numC)
                For i = 1 To mix.numC
                    NumAss(i) = mix.comp(i).EoSParam(4)
                Next
                If sum(NumAss) > 0 Then muAss = mu_Ass(T, dens_num, mix) Else muAss = zeros(mix.numC)

            Else

                Dim t1, t2, t3 As Task

                'Hard chain contribution
                t1 = TaskHelper.Run(Sub() muHC = mu_HC(T, dens_num, mix))

                'Dispersive contribution
                t2 = TaskHelper.Run(Sub() muDisp = mu_Disp(T, dens_num, mix))

                'Association contribution
                t3 = TaskHelper.Run(Sub()
                                        NumAss = zeros(mix.numC)

                                        For i = 1 To mix.numC
                                            NumAss(i) = mix.comp(i).EoSParam(4)
                                        Next

                                        If sum(NumAss) > 0 Then
                                            muAss = mu_Ass(T, dens_num, mix)
                                        Else
                                            muAss = zeros(mix.numC)
                                        End If
                                    End Sub)

                Task.WaitAll(t1, t2, t3)

            End If

            '**************************************************************************
            'Calculates the fugacity coefficient
            '**************************************************************************

            logf = zeros(mix.numC - 1)
            For i = 1 To mix.numC
                logf(i - 1) = muHC(i) + muDisp(i) + muAss(i) - Log(Z) 'Eq. A32 Of reference
            Next

            Return logf

        End Function

        ' Residual chemical potential of the hard-chain plus dispersion terms, per compound, by a central
        ' finite difference of the segment-based Helmholtz energy at constant temperature and volume
        ' (mu_i = d(n a_res)/dn_i). Used for copolymer mixtures, where the per-compound analytical
        ' derivatives do not hold; it reproduces mu_HC + mu_Disp for ordinary mixtures.
        Private Function NumMuHCDisp(T As Double, dens_num As Double, mixt As mixture) As Double()
            Dim nc = mixt.numC
            Dim x0 = mixt.x
            ' Step each mole number relative to its own value: a high-molar-mass polymer has a tiny mole
            ' fraction, so a fixed absolute step would be a large fraction of it and swamp the derivative.
            Dim rel As Double = 0.00001
            Dim mu = zeros(nc)
            For k = 1 To nc
                Dim hk As Double = rel * Math.Max(x0(k), 0.0000000001)
                mu(k) = (NA_HCDisp(T, dens_num, mixt, x0, k, hk) - NA_HCDisp(T, dens_num, mixt, x0, k, -hk)) / (2.0 * hk)
            Next
            mixt.x = x0
            Return mu
        End Function

        ' n*a_res (hard chain + dispersion) with the mole number of compound k perturbed by dh at constant
        ' volume: the total mole count becomes 1+dh, the mole fractions rescale, and the number density
        ' scales with the mole count. Restores nothing (the caller resets mixt.x).
        Private Function NA_HCDisp(T As Double, dens_num As Double, mixt As mixture, x0 As Double(), k As Integer, dh As Double) As Double
            Dim nc = mixt.numC
            Dim Np As Double = 1.0 + dh
            Dim xp = zeros(nc)
            For i = 1 To nc
                xp(i) = x0(i) / Np
            Next
            xp(k) = (x0(k) + dh) / Np
            Dim rhop = dens_num * Np
            mixt.x = xp
            Dim a = HelmholtzHC(T, rhop, mixt) + HelmholtzDisp(T, rhop, mixt)
            Return Np * a
        End Function

        ''' <summary>
        ''' Log fugacity coefficients and pressure at a GIVEN number density, without solving for the
        ''' density. This is the closed-form EoS evaluated at a fixed rho, used by the analytical
        ''' composition derivative to avoid a density solve on every perturbation.
        ''' </summary>
        Friend Function EvalAtDens(T As Double, dens_num As Double, mixt As mixture, ByRef Pcalc As Double) As Double()

            Dim kb As Double = 1.3806504E-23

            Dim muHC() As Double = mu_HC(T, dens_num, mixt)
            Dim muDisp() As Double = mu_Disp(T, dens_num, mixt)

            Dim NumAss = zeros(mixt.numC)
            For i = 1 To mixt.numC
                NumAss(i) = mixt.comp(i).EoSParam(4)
            Next
            Dim muAss() As Double = If(sum(NumAss) > 0, mu_Ass(T, dens_num, mixt), zeros(mixt.numC))

            Dim Z As Double = 1.0 + Z_hc(T, dens_num, mixt) + Z_disp(T, dens_num, mixt) + Z_ass(T, dens_num, mixt)

            Pcalc = Z * kb * T * dens_num * (10000000000.0) ^ 3

            Dim logf = zeros(mixt.numC - 1)
            For i = 1 To mixt.numC
                logf(i - 1) = muHC(i) + muDisp(i) + muAss(i) - Log(Z)
            Next
            Return logf

        End Function

        ''' <summary>Sets the mixture mole fractions (1-indexed internally) without rebuilding parameters,
        ''' for the composition perturbations of the analytical derivative. The mean molar mass is not
        ''' needed by the chemical-potential/compressibility terms, so it is left untouched.</summary>
        Public Sub SetComposition(molefractions() As Double)
            mix.x = zeros(molefractions.Length)
            molefractions.CopyTo(mix.x, 1)
        End Sub

        Friend Function HardSphereDiameter(T, m, sigma, epsilon)

            'Hard Sphere Diameter with PC-SAFT EoS
            'Auxiliary function, Not to be used directly
            '
            'Reference: Gross And Sadowski, Ind.Eng.Chem.Res. 40 (2001) 1244-1260

            'Copyright (c) 2011 Ángel Martín, University of Valladolid (Spain)
            'This program Is free software: you can redistribute it And/Or modify
            'it under the terms of the GNU General Public License as published by
            'the Free Software Foundation, either version 3 of the License, Or
            '(at your option) any later version.
            'This program Is distributed in the hope that it will be useful,
            'but WITHOUT ANY WARRANTY without even the implied warranty of
            'MERCHANTABILITY Or FITNESS FOR A PARTICULAR PURPOSE.  See the
            'GNU General Public License for more details.
            'You should have received a copy of the GNU General Public License
            'along with this program.  If Not, see <http://www.gnu.org/licenses/>.

            Return sigma * (1 - 0.12 * Exp(-3 * epsilon / T)) 'Eq. 3 Of reference

        End Function

        Friend Function Helmholtz(T, P, mix, phase, Z)

            'Calculates the residual Helmholtz energy And compresibility coefficient of mixture mix at temperature T
            'And pressure P using PC-SAFT EoS
            '
            'Parameters:
            'EoS Equation of state used for calculations
            'T: Temperature(K)
            'P: Pressure(K)
            'mix: cMixture Object
            'phase: set phase = 'liq' to calculate the fugacity of a liquid phase or
            '   phase = 'gas' to calculate the fugacity of a gas phase
            '
            'Optional parameters (set [] to keep default value)
            'Z_ini: Initial guess for the compressibility coefficient
            '   If Not defined, the program uses an initial guess Z_ini = 0.8 for gas
            '   phase And a Z_ini corresponding to a liquid density of 800 kg/m3 for
            '   the liquid phase
            'options: parameters of the fsolve numerical resolution method (structure
            '   generated with "optimset")
            '
            'Results:
            'Ares residual Helmholtz energy
            'Z: compresibility coefficient
            'EoS: returns EoS used for calculations
            '
            'Reference: Gross And Sadowski, Ind.Eng.Chem.Res. 40 (2001) 1244-1260
            'Reference 2: Chapman et al., Ind.Eng.Chem.Res. 29 (1990) 1709-1721

            'Copyright (c) 2011 Ángel Martín, University of Valladolid (Spain)
            'This program Is free software: you can redistribute it And/Or modify
            'it under the terms of the GNU General Public License as published by
            'the Free Software Foundation, either version 3 of the License, Or
            '(at your option) any later version.
            'This program Is distributed in the hope that it will be useful,
            'but WITHOUT ANY WARRANTY without even the implied warranty of
            'MERCHANTABILITY Or FITNESS FOR A PARTICULAR PURPOSE.  See the
            'GNU General Public License for more details.
            'You should have received a copy of the GNU General Public License
            'along with this program.  If Not, see <http://www.gnu.org/licenses/>.

            '**************************************************************************
            'Calculates the compresibility coefficient 
            '**************************************************************************

            'Constants

            Dim kb, dens_num, ahc, adisp, aass, Ares As Double

            kb = 1.3806504E-23 'Boltzmann K (J/K)

            'Z = compr(T, P, mix, phase, Zestimate)
            dens_num = P / (Z * kb * T) * 1 / (10000000000.0) ^ 3

            '**************************************************************************
            'Hard-Chain Reference Contribution
            '**************************************************************************

            ahc = HelmholtzHC(T, dens_num, mix)

            '**************************************************************************
            'Dispersion Contribution
            '**************************************************************************

            adisp = HelmholtzDisp(T, dens_num, mix)

            '**************************************************************************
            'Association Contribution
            '**************************************************************************

            Dim NumAss() As Double

            NumAss = zeros(mix.numC)
            For i = 1 To mix.numC
                NumAss(i) = mix.comp(i).EoSParam(4)
            Next

            If sum(NumAss) > 0 Then
                aass = HelmholtzAss(T, dens_num, mix)
            Else
                aass = 0
            End If

            '**************************************************************************
            'Residual Helmholtz energy
            '**************************************************************************
            Ares = ahc + adisp + aass

            Return Ares

        End Function

        Friend Function HelmholtzDisp(T As Double, dens_num As Double, mix As mixture)

            'Calculates the dispersion contribution to the residual Helmholtz energy 
            'of mixture mix at temperature T And pressure P using PC-SAFT EoS
            '
            'Parameters:
            'EoS Equation of state used for calculations
            'T: Temperature(K)
            'dens_num: Number density(molecule / Angstrom ^ 3)
            'mix: cMixture Object
            '
            'Results:
            'Ahc residual Helmholtz energy, association contribution
            'EoS: returns EoS used for calculations
            '
            'Reference: Gross And Sadowski, Ind.Eng.Chem.Res. 40 (2001) 1244-1260

            'Copyright (c) 2011 Ángel Martín, University of Valladolid (Spain)
            'This program Is free software: you can redistribute it And/Or modify
            'it under the terms of the GNU General Public License as published by
            'the Free Software Foundation, either version 3 of the License, Or
            '(at your option) any later version.
            'This program Is distributed in the hope that it will be useful,
            'but WITHOUT ANY WARRANTY without even the implied warranty of
            'MERCHANTABILITY Or FITNESS FOR A PARTICULAR PURPOSE.  See the
            'GNU General Public License for more details.
            'You should have received a copy of the GNU General Public License
            'along with this program.  If Not, see <http://www.gnu.org/licenses/>.

            Dim a0 = adisp0, a1 = adisp1, a2 = adisp2, b0 = bdisp0, b1 = bdisp1, b2 = bdisp2


            Dim x, m, sigma, epsilon, d As Double()
            Dim k1(,) As Double

            'Reads pure-component properties
            numC = mix.numC
            x = mix.x
            m = zeros(numC)
            sigma = zeros(numC)
            epsilon = zeros(numC)
            For i = 1 To numC
                m(i) = mix.comp(i).EoSParam(1)
                sigma(i) = mix.comp(i).EoSParam(2)
                epsilon(i) = mix.comp(i).EoSParam(3)
            Next
            k1 = mix.k1

            'Calculates the temperature-depNextant segment diameter
            d = GetD(T)

            Dim m_prom As Double

            'mean segment number
            m_prom = 0
            For i = 1 To numC
                m_prom = m_prom + m(i) * x(i) 'Eq. 6 Of reference
            Next

            Dim a, b As Double()

            'Calculates the a And b parameters
            a = zeros(7)
            b = zeros(7)
            For j = 1 To 7
                a(j) = a0(j) + (m_prom - 1) / m_prom * a1(j) + (m_prom - 1) / m_prom * (m_prom - 2) / m_prom * a2(j) 'Eq. 18 Of reference
                b(j) = b0(j) + (m_prom - 1) / m_prom * b1(j) + (m_prom - 1) / m_prom * (m_prom - 2) / m_prom * b2(j) 'Eq. 19 Of reference
            Next

            Dim dens_red, prom1, prom2 As Double

            'Reduced density (Eq. 9) and the dispersion perturbation sums (Eqs. A12, A13), over segments.
            SegDispSums(mix, T, dens_num, dens_red, prom1, prom2)

            Dim term1, term2, C1 As Double

            'Dispersion Contribution
            term1 = (m_prom) * (8 * dens_red - 2 * dens_red ^ 2) / (1 - dens_red) ^ 4
            term2 = (1 - m_prom) * (20 * dens_red - 27 * dens_red ^ 2 + 12 * dens_red ^ 3 - 2 * dens_red ^ 4) / ((1 - dens_red) * (2 - dens_red)) ^ 2
            C1 = (1 + term1 + term2) ^ -1 'Eq. A11 of reference

            Dim I1, I2 As Double, Adisp

            I1 = 0
            I2 = 0
            For j = 1 To 7
                I1 = I1 + a(j) * dens_red ^ (j - 1) 'Eq. A16 of reference
                I2 = I2 + b(j) * dens_red ^ (j - 1) 'Eq. A17 of reference
            Next

            term1 = -2 * PI * dens_num * I1 * prom1
            term2 = -PI * dens_num * m_prom * C1 * I2 * prom2

            Adisp = term1 + term2 'Eq. A10 of reference

            Return Adisp

        End Function

        Friend Function HelmholtzHC(T As Double, dens_num As Double, mix As mixture)

            'Calculates the Hard Chain contribution to the residual Helmholtz energy 
            'of mixture mix at temperature T and pressure P using PC-SAFT EoS
            '
            'Parameters:
            'EoS: Equation of state used for calculations
            'T: Temperature(K)
            'dens_num: Number density (molecule/Angstrom^3)
            'mix: cMixture object
            '
            'Results:
            'Ahc: residual Helmholtz energy, association contribution
            'EoS: returns EoS used for calculations
            '
            'Reference: Gross and Sadowski, Ind. Eng. Chem. Res. 40 (2001) 1244-1260

            'Copyright (c) 2011 Ángel Martín, University of Valladolid (Spain)
            'This program is free software: you can redistribute it and/or modify
            'it under the terms of the GNU General Public License as published by
            'the Free Software Foundation, either version 3 of the License, or
            '(at your option) any later version.
            'This program is distributed in the hope that it will be useful,
            'but WITHOUT ANY WARRANTY without even the implied warranty of
            'MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
            'GNU General Public License for more details.
            'You should have received a copy of the GNU General Public License
            'along with this program.  If not, see <http://www.gnu.org/licenses/>.

            Dim x, m, sigma, epsilon, d As Double()

            'Reads pure-component properties
            numC = mix.numC
            x = mix.x
            m = zeros(numC)
            sigma = zeros(numC)
            epsilon = zeros(numC)
            For i = 1 To numC
                m(i) = mix.comp(i).EoSParam(1)
                sigma(i) = mix.comp(i).EoSParam(2)
                epsilon(i) = mix.comp(i).EoSParam(3)
            Next

            'Calculates the temperature-depNextant segment diameter
            d = GetD(T)

            Dim m_prom As Double

            'mean segment number
            m_prom = 0
            For i = 1 To numC
                m_prom = m_prom + m(i) * x(i) 'Eq. 6 of reference
            Next

            Dim auxil(), term1, term2, term3, a_hs, sum1, Ahc As Double

            'Segment weights w_s = x_i m_iR and segment diameters (copolymer segment view).
            Dim segd = GetSegD(mix, T)
            Dim ns = mix.nseg
            Dim w = zeros(ns)
            Dim sa As Integer
            For sa = 1 To ns
                w(sa) = x(mix.segParent(sa)) * mix.segM(sa)
            Next

            'auxiliary functions (zeta_0..zeta_3), summed over segment types (Eq. 9 / A.10)
            auxil = zeros(4)
            For j = 1 To 4
                For sa = 1 To ns
                    auxil(j) = auxil(j) + w(sa) * segd(sa) ^ (j - 1)
                Next
                auxil(j) = auxil(j) * PI / 6 * dens_num
            Next

            'Helmholtz energy
            term1 = 3 * auxil(2) * auxil(3) / (1 - auxil(4))
            term2 = auxil(3) ^ 3 / (auxil(4) * (1 - auxil(4)) ^ 2)
            term3 = (auxil(3) ^ 3 / auxil(4) ^ 2 - auxil(1)) * Log(1 - auxil(4))
            a_hs = (1 / auxil(1)) * (term1 + term2 + term3)

            'Hard-chain term (Eq. A.6): each molecule's bonds weighted by the bonding fraction B and the
            'radial distribution at the bonded segment-pair contact. A homopolymer/small molecule has one
            'self-bond of fraction 1, reducing to (m_i - 1) ln g_ii.
            sum1 = 0
            Dim bi As Integer
            For i = 1 To numC
                Dim bAcc As Double = 0.0
                Dim ba = mix.bondA(i - 1), bb = mix.bondB(i - 1), bf = mix.bondF(i - 1)
                For bi = 0 To bf.Length - 1
                    bAcc = bAcc + bf(bi) * Log(GhsSeg(segd(ba(bi)), segd(bb(bi)), auxil))
                Next
                sum1 = sum1 + x(i) * (m(i) - 1) * bAcc
            Next

            Ahc = m_prom * a_hs - sum1 'Eq. A4 of reference

            Return Ahc

        End Function

        Friend Function mu_Disp(T As Double, dens_num As Double, mix As mixture)

            'Calculates the dispersion contribution to the residual chemical potential 
            'of mixture mix at temperature T and pressure P using PC-SAFT EoS
            '
            'Parameters:
            'EoS: Equation of state used for calculations
            'T: Temperature(K)
            'P: Pressure (K)
            'dens_num: Number density (molecule/Angstrom^3)
            'mix: cMixture object
            '
            'Results:
            'muass: residual chemical potential, association contribution
            'EoS: returns EoS used for calculations
            '
            'Reference: Gross and Sadowski, Ind. Eng. Chem. Res. 40 (2001) 1244-1260

            'Copyright (c) 2011 Ángel Martín, University of Valladolid (Spain)
            'This program is free software: you can redistribute it and/or modify
            'it under the terms of the GNU General Public License as published by
            'the Free Software Foundation, either version 3 of the License, or
            '(at your option) any later version.
            'This program is distributed in the hope that it will be useful,
            'but WITHOUT ANY WARRANTY without even the implied warranty of
            'MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
            'GNU General Public License for more details.
            'You should have received a copy of the GNU General Public License
            'along with this program.  If not, see <http://www.gnu.org/licenses/>.

            Dim a0 = adisp0, a1 = adisp1, a2 = adisp2, b0 = bdisp0, b1 = bdisp1, b2 = bdisp2


            Dim m, sigma, epsilon, d As Double()

            'Reads pure-component properties
            m = zeros(mix.numC)
            sigma = zeros(mix.numC)
            epsilon = zeros(mix.numC)
            For i = 1 To mix.numC
                m(i) = mix.comp(i).EoSParam(1)
                sigma(i) = mix.comp(i).EoSParam(2)
                epsilon(i) = mix.comp(i).EoSParam(3)
            Next

            'Calculates the temperature-depNextant segment diameter
            d = GetD(T)

            Dim m_prom As Double

            'mean segment number
            m_prom = 0
            For i = 1 To mix.numC
                m_prom = m_prom + m(i) * mix.x(i) 'Eq. 6 of reference
            Next

            Dim a(), b(), dens_red, sigmaij(,), epsilonij(,) As Double

            'Calculates the a and b parameters
            a = zeros(7)
            b = zeros(7)
            For j = 1 To 7
                a(j) = a0(j) + (m_prom - 1) / m_prom * a1(j) + (m_prom - 1) / m_prom * (m_prom - 2) / m_prom * a2(j) 'Eq. 18 of reference
                b(j) = b0(j) + (m_prom - 1) / m_prom * b1(j) + (m_prom - 1) / m_prom * (m_prom - 2) / m_prom * b2(j) 'Eq. 19 of reference
            Next

            'Reduced density
            dens_red = 0
            For i = 1 To mix.numC
                dens_red = dens_red + mix.x(i) * m(i) * d(i) ^ 3
            Next
            dens_red = dens_red * PI / 6 * dens_num 'Eq. 9 of reference

            'Mixing rules
            sigmaij = zeros(mix.numC, mix.numC)
            epsilonij = zeros(mix.numC, mix.numC)
            For i = 1 To mix.numC
                For j = 1 To mix.numC
                    sigmaij(i, j) = 0.5 * (sigma(i) + sigma(j)) 'Eq. A14 of reference
                    epsilonij(i, j) = Sqrt(epsilon(i) * epsilon(j)) * (1 - mix.k1(i, j)) 'Eq A15 of reference           
                Next
            Next

            Dim Zdisp, Adisp, dauxil_dxk(,), prom1, prom2, der_prom1(), der_prom2(), sum1, sum2 As Double

            'Compressibility coefficient
            Zdisp = Z_disp(T, dens_num, mix)

            'Helmholtz energy
            Adisp = HelmholtzDisp(T, dens_num, mix)

            'Dispersion contribution
            dauxil_dxk = zeros(4, mix.numC)
            For j = 1 To 4
                For i = 1 To mix.numC
                    dauxil_dxk(j, i) = PI / 6 * dens_num * m(i) * d(i) ^ (j - 1) 'Eq. A34 of reference
                Next
            Next

            prom1 = 0
            prom2 = 0
            For i = 1 To mix.numC
                For j = 1 To mix.numC
                    prom1 = prom1 + mix.x(i) * mix.x(j) * m(i) * m(j) * epsilonij(i, j) / T * sigmaij(i, j) ^ 3 'Eq. A12 of reference
                    prom2 = prom2 + mix.x(i) * mix.x(j) * m(i) * m(j) * (epsilonij(i, j) / T) ^ 2 * sigmaij(i, j) ^ 3 'Eq. A13 of reference
                Next
            Next

            der_prom1 = zeros(mix.numC)
            der_prom2 = zeros(mix.numC)
            For i = 1 To mix.numC
                sum1 = 0
                sum2 = 0
                For j = 1 To mix.numC
                    sum1 = sum1 + mix.x(j) * m(j) * (epsilonij(i, j) / T) * sigmaij(i, j) ^ 3
                    sum2 = sum2 + mix.x(j) * m(j) * (epsilonij(i, j) / T) ^ 2 * sigmaij(i, j) ^ 3
                Next
                der_prom1(i) = 2 * m(i) * sum1 'Eq. A39 of reference
                der_prom2(i) = 2 * m(i) * sum2 'Eq. A40 of reference
            Next

            Dim I1, I2, term1, term2, C1, C2, der_C1(), der_a(,), der_b(,) As Double

            I1 = 0
            I2 = 0
            For j = 1 To 7
                I1 = I1 + a(j) * dens_red ^ (j - 1) 'Eq. A16 of reference
                I2 = I2 + b(j) * dens_red ^ (j - 1) 'Eq. A17 of reference
            Next

            term1 = (m_prom) * (8 * dens_red - 2 * dens_red ^ 2) / (1 - dens_red) ^ 4
            term2 = (1 - m_prom) * (20 * dens_red - 27 * dens_red ^ 2 + 12 * dens_red ^ 3 - 2 * dens_red ^ 4) / ((1 - dens_red) * (2 - dens_red)) ^ 2
            C1 = (1 + term1 + term2) ^ -1 'Eq. A11 of reference

            term1 = m_prom * (-4 * dens_red ^ 2 + 20 * dens_red + 8) / (1 - dens_red) ^ 5
            term2 = (1 - m_prom) * (2 * dens_red ^ 3 + 12 * dens_red ^ 2 - 48 * dens_red + 40) / ((1 - dens_red) * (2 - dens_red)) ^ 3
            C2 = -C1 ^ 2 * (term1 + term2) 'Eq. A31 of reference

            der_C1 = zeros(mix.numC)
            For i = 1 To mix.numC
                term1 = m(i) * (8 * dens_red - 2 * dens_red ^ 2) / (1 - dens_red) ^ 4
                term2 = m(i) * (20 * dens_red - 27 * dens_red ^ 2 + 12 * dens_red ^ 3 - 2 * dens_red ^ 4) / ((1 - dens_red) * (2 - dens_red)) ^ 2

                der_C1(i) = C2 * dauxil_dxk(4, i) - C1 ^ 2 * (term1 - term2) 'Eq. A41 of reference
            Next

            der_a = zeros(7, mix.numC)
            der_b = zeros(7, mix.numC)
            For i = 1 To 7
                For j = 1 To mix.numC
                    der_a(i, j) = m(j) / m_prom ^ 2 * a1(i) + m(j) / m_prom ^ 2 * (3 - 4 / m_prom) * a2(i) 'Eq. A44 of reference
                    der_b(i, j) = m(j) / m_prom ^ 2 * b1(i) + m(j) / m_prom ^ 2 * (3 - 4 / m_prom) * b2(i) 'Eq. A45 of reference
                Next
            Next

            Dim der_I1(), der_I2(), dadisp_dxk(), muDisp() As Double

            der_I1 = zeros(mix.numC)
            der_I2 = zeros(mix.numC)
            For i = 1 To mix.numC
                sum1 = 0
                sum2 = 0
                For j = 1 To 7
                    sum1 = sum1 + a(j) * (j - 1) * dauxil_dxk(4, i) * dens_red ^ (j - 2) + der_a(j, i) * dens_red ^ (j - 1) 'Eq. A42 of reference
                    sum2 = sum2 + b(j) * (j - 1) * dauxil_dxk(4, i) * dens_red ^ (j - 2) + der_b(j, i) * dens_red ^ (j - 1) 'Eq. A43 of reference
                Next
                der_I1(i) = sum1
                der_I2(i) = sum2
            Next

            dadisp_dxk = zeros(mix.numC)
            For i = 1 To mix.numC
                term1 = -2 * PI * dens_num * (der_I1(i) * prom1 + I1 * der_prom1(i))
                term2 = -PI * dens_num * ((m(i) * C1 * I2 + m_prom * der_C1(i) * I2 + m_prom * C1 * der_I2(i)) * prom2 + m_prom * C1 * I2 * der_prom2(i))
                dadisp_dxk(i) = term1 + term2 'Eq. A38 of reference
            Next

            'Chemical potential
            sum1 = 0
            For i = 1 To mix.numC
                sum1 = sum1 + mix.x(i) * dadisp_dxk(i)
            Next

            muDisp = zeros(mix.numC)
            For i = 1 To mix.numC
                muDisp(i) = Adisp + Zdisp + dadisp_dxk(i) - sum1  'Eq. A33 of reference
            Next

            Return muDisp

        End Function

        Friend Function mu_HC(T As Double, dens_num As Double, mix As mixture)

            'Calculates the hard chain contribution to the residual chemical potential 
            'of mixture mix at temperature T and pressure P using PC-SAFT EoS
            '
            'Parameters:
            'EoS: Equation of state used for calculations
            'T: Temperature(K)
            'P: Pressure (K)
            'dens_num: Number density (molecule/Angstrom^3)
            'mix: cMixture object
            '
            'Results:
            'muass: residual chemical potential, association contribution
            'EoS: returns EoS used for calculations
            '
            'Reference: Gross and Sadowski, Ind. Eng. Chem. Res. 40 (2001) 1244-1260

            'Copyright (c) 2011 Ángel Martín, University of Valladolid (Spain)
            'This program is free software: you can redistribute it and/or modify
            'it under the terms of the GNU General Public License as published by
            'the Free Software Foundation, either version 3 of the License, or
            '(at your option) any later version.
            'This program is distributed in the hope that it will be useful,
            'but WITHOUT ANY WARRANTY without even the implied warranty of
            'MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
            'GNU General Public License for more details.
            'You should have received a copy of the GNU General Public License
            'along with this program.  If not, see <http://www.gnu.org/licenses/>.

            Dim m, sigma, epsilon, d As Double()

            'Reads pure-component properties
            m = zeros(mix.numC)
            sigma = zeros(mix.numC)
            epsilon = zeros(mix.numC)
            For i = 1 To mix.numC
                m(i) = mix.comp(i).EoSParam(1)
                sigma(i) = mix.comp(i).EoSParam(2)
                epsilon(i) = mix.comp(i).EoSParam(3)
            Next

            'Calculates the temperature-depNextant segment diameter
            d = GetD(T)

            Dim m_prom, auxil(), ghs(,), term1, term2, term3 As Double

            'mean segment number
            m_prom = 0
            For i = 1 To mix.numC
                m_prom = m_prom + m(i) * mix.x(i) 'Eq. 6 of reference
            Next

            'auxiliary functions
            auxil = zeros(4)
            For j = 1 To 4
                For i = 1 To mix.numC
                    auxil(j) = auxil(j) + mix.x(i) * m(i) * d(i) ^ (j - 1)
                Next
                auxil(j) = auxil(j) * PI / 6 * dens_num 'Eq. 9 of reference
            Next

            'radial distribution function
            ghs = zeros(mix.numC, mix.numC)
            For i = 1 To mix.numC
                For j = 1 To mix.numC
                    term1 = 1 / (1 - auxil(4))
                    term2 = d(i) * d(j) / (d(i) + d(j)) * 3 * auxil(3) / (1 - auxil(4)) ^ 2
                    term3 = (d(i) * d(j) / (d(i) + d(j))) ^ 2 * 2 * auxil(3) ^ 2 / (1 - auxil(4)) ^ 3
                    ghs(i, j) = term1 + term2 + term3 'Eq. 8 of reference
                Next
            Next

            Dim Zhc, a_hs, sum1, Ahc, dauxil_dxk(,), dahs_dxk(), term4, term5, term6, term7 As Double

            'Compressibility coefficient
            Zhc = Z_hc(T, dens_num, mix)

            'Helmholtz energy
            term1 = 3 * auxil(2) * auxil(3) / (1 - auxil(4))
            term2 = auxil(3) ^ 3 / (auxil(4) * (1 - auxil(4)) ^ 2)
            term3 = (auxil(3) ^ 3 / auxil(4) ^ 2 - auxil(1)) * Log(1 - auxil(4))
            a_hs = (1 / auxil(1)) * (term1 + term2 + term3) 'Eq. A6 of reference
            sum1 = 0
            For i = 1 To mix.numC
                sum1 = sum1 + mix.x(i) * (m(i) - 1) * Log(ghs(i, i))
            Next
            Ahc = m_prom * a_hs - sum1 'Eq. A4 of reference

            'Chemical potential
            dauxil_dxk = zeros(4, mix.numC)
            For j = 1 To 4
                For i = 1 To mix.numC
                    dauxil_dxk(j, i) = PI / 6 * dens_num * m(i) * d(i) ^ (j - 1) 'Eq. A34 of reference
                Next
            Next

            dahs_dxk = zeros(mix.numC)
            For i = 1 To mix.numC
                term1 = -dauxil_dxk(1, i) / auxil(1) * a_hs
                term2 = 3 * (dauxil_dxk(2, i) * auxil(3) + auxil(2) * dauxil_dxk(3, i)) / (1 - auxil(4))
                term3 = 3 * auxil(2) * auxil(3) * dauxil_dxk(4, i) / (1 - auxil(4)) ^ 2
                term4 = 3 * auxil(3) ^ 2 * dauxil_dxk(3, i) / (auxil(4) * (1 - auxil(4)) ^ 2)
                term5 = auxil(3) ^ 3 * dauxil_dxk(4, i) * (3 * auxil(4) - 1) / (auxil(4) ^ 2 * (1 - auxil(4)) ^ 3)
                term6 = ((3 * auxil(3) ^ 2 * dauxil_dxk(3, i) * auxil(4) - 2 * auxil(3) ^ 3 * dauxil_dxk(4, i)) / auxil(4) ^ 3 - dauxil_dxk(1, i)) * Log(1 - auxil(4))
                term7 = (auxil(1) - auxil(3) ^ 3 / auxil(4) ^ 2) * dauxil_dxk(4, i) / (1 - auxil(4))

                dahs_dxk(i) = term1 + 1 / auxil(1) * (term2 + term3 + term4 + term5 + term6 + term7) 'Eq. A36 of reference
            Next

            Dim dgij_dxk(,,), dahc_dxk(), muHC() As Double

            dgij_dxk = zeros(mix.numC, mix.numC, mix.numC)
            For i = 1 To mix.numC
                For j = 1 To mix.numC
                    For k = 1 To mix.numC
                        term1 = dauxil_dxk(4, k) / (1 - auxil(4)) ^ 2
                        term2 = (d(i) * d(j) / (d(i) + d(j))) * (3 * dauxil_dxk(3, k) / (1 - auxil(4)) ^ 2 + 6 * auxil(3) * dauxil_dxk(4, k) / (1 - auxil(4)) ^ 3)
                        term3 = (d(i) * d(j) / (d(i) + d(j))) ^ 2 * (4 * auxil(3) * dauxil_dxk(3, k) / (1 - auxil(4)) ^ 3 + 6 * auxil(3) ^ 2 * dauxil_dxk(4, k) / (1 - auxil(4)) ^ 4)
                        dgij_dxk(i, j, k) = term1 + term2 + term3 'Eq. A37 of reference
                    Next
                Next
            Next

            dahc_dxk = zeros(mix.numC)
            For i = 1 To mix.numC
                sum1 = 0
                For j = 1 To mix.numC
                    sum1 = sum1 + mix.x(j) * (m(j) - 1) / ghs(j, j) * dgij_dxk(j, j, i)
                Next
                dahc_dxk(i) = m(i) * a_hs + m_prom * dahs_dxk(i) - sum1 + (1 - m(i)) * Log(ghs(i, i)) 'Eq. A35 of reference
            Next

            'Chemical potential
            sum1 = 0
            For i = 1 To mix.numC
                sum1 = sum1 + mix.x(i) * dahc_dxk(i)
            Next

            muHC = zeros(mix.numC)
            For i = 1 To mix.numC
                muHC(i) = Ahc + Zhc + dahc_dxk(i) - sum1  'Eq. A33 of reference
            Next

            Return muHC

        End Function

        Friend Function obj_SAFT(dens_red As Double, T As Double, P As Double, mix As mixture)

            'Objective function for the calculation of Z with PC-SAFT EoS
            'Auxiliary function, not to be used directly
            '
            'Reference: Gross and Sadowski, Ind. Eng. Chem. Res. 40 (2001) 1244-1260
            'Reference 2: Chapman et al., Ind. Eng. Chem. Res. 29 (1990) 1709-1721

            'Copyright (c) 2011 Ángel Martín, University of Valladolid (Spain)
            'This program is free software: you can redistribute it and/or modify
            'it under the terms of the GNU General Public License as published by
            'the Free Software Foundation, either version 3 of the License, or
            '(at your option) any later version.
            'This program is distributed in the hope that it will be useful,
            'but WITHOUT ANY WARRANTY without even the implied warranty of
            'MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
            'GNU General Public License for more details.
            'You should have received a copy of the GNU General Public License
            'along with this program.  If not, see <http://www.gnu.org/licenses/>.

            Dim x, m, sigma, epsilon, d As Double()

            'Reads pure-component properties
            numC = mix.numC
            x = mix.x
            m = zeros(numC)
            sigma = zeros(numC)
            epsilon = zeros(numC)
            For i = 1 To numC
                m(i) = mix.comp(i).EoSParam(1)
                sigma(i) = mix.comp(i).EoSParam(2)
                epsilon(i) = mix.comp(i).EoSParam(3)
            Next

            'Calculates the temperature-depNextant segment diameter
            d = GetD(T)

            Dim sum1, dens_Num, ZHc, Zdisp, Zass, kb, Zcalc, Pcalc, result As Double

            'Calculates density according to the iterated dens_red
            sum1 = 0
            For i = 1 To numC
                sum1 = sum1 + x(i) * m(i) * d(i) ^ 3
            Next
            dens_Num = 6 / PI * dens_red * sum1 ^ -1 'Eq. 9 of reference

            '**************************************************************************
            'Compressibility coefficient 
            '**************************************************************************
            ZHc = Z_hc(T, dens_Num, mix)

            Zdisp = Z_disp(T, dens_Num, mix)

            Zass = Z_ass(T, dens_Num, mix)

            '**************************************************************************
            'Ojective function
            '**************************************************************************
            kb = 1.3806504E-23 'Boltzmann K (J/K)

            Zcalc = 1 + ZHc + Zdisp + Zass

            Pcalc = Zcalc * kb * T * dens_Num * (10000000000.0) ^ 3

            'Dim fac As Decimal = dens_Num * (10000000000.0) ^ 3

            'Dim Zcalc2 = P / (kb * T * fac)

            result = P - Pcalc

            'result = Zcalc2 - Zcalc

            Return New Double() {result, Zcalc}

        End Function

        Friend Function Z_disp(T As Double, dens_num As Double, mix As mixture)

            'Dispersive contribution to the compressibility coefficient with PC-SAFT EoS
            'Auxiliary function, Not to be used directly
            '
            'Reference: Gross And Sadowski, Ind.Eng.Chem.Res. 40 (2001) 1244-1260

            'Copyright (c) 2011 Ángel Martín, University of Valladolid (Spain)
            'This program Is free software: you can redistribute it And/Or modify
            'it under the terms of the GNU General Public License as published by
            'the Free Software Foundation, either version 3 of the License, Or
            '(at your option) any later version.
            'This program Is distributed in the hope that it will be useful,
            'but WITHOUT ANY WARRANTY without even the implied warranty of
            'MERCHANTABILITY Or FITNESS FOR A PARTICULAR PURPOSE.  See the
            'GNU General Public License for more details.
            'You should have received a copy of the GNU General Public License
            'along with this program.  If Not, see <http://www.gnu.org/licenses/>.

            Dim a0 = adisp0, a1 = adisp1, a2 = adisp2, b0 = bdisp0, b1 = bdisp1, b2 = bdisp2


            Dim x, m, sigma, epsilon, d As Double()
            Dim k1(,) As Double

            'Reads pure-component properties

            numC = mix.numC
            x = mix.x
            k1 = mix.k1
            m = zeros(numC)
            sigma = zeros(numC)
            epsilon = zeros(numC)
            For i = 1 To numC
                m(i) = mix.comp(i).EoSParam(1)
                sigma(i) = mix.comp(i).EoSParam(2)
                epsilon(i) = mix.comp(i).EoSParam(3)
            Next

            'Calculates the temperature-depNextant segment diameter
            d = GetD(T)

            Dim m_prom, a(), b(), dens_red, sigmaij(,), epsilonij(,) As Double

            'mean segment number
            m_prom = 0
            For i = 1 To numC
                m_prom = m_prom + m(i) * x(i) 'Eq. 6 Of reference
            Next

            'Calculates the a And b parameters
            a = zeros(7)
            b = zeros(7)
            For j = 1 To 7
                a(j) = a0(j) + (m_prom - 1) / m_prom * a1(j) + (m_prom - 1) / m_prom * (m_prom - 2) / m_prom * a2(j) 'Eq. 18 Of reference
                b(j) = b0(j) + (m_prom - 1) / m_prom * b1(j) + (m_prom - 1) / m_prom * (m_prom - 2) / m_prom * b2(j) 'Eq. 19 Of reference
            Next

            'Reduced density and the dispersion sums are computed over segment types below (SegDispSums).

            '**************************************************************************
            'Zdisp
            '**************************************************************************

            Dim dnuI1_dnu, dnuI2_dnu, term1, term2, C1, C2, prom1, prom2, I2, Zdisp As Double

            SegDispSums(mix, T, dens_num, dens_red, prom1, prom2)

            dnuI1_dnu = 0
            dnuI2_dnu = 0

            For j = 1 To 7
                dnuI1_dnu = dnuI1_dnu + a(j) * (j) * dens_red ^ (j - 1) 'Eq. A29 Of reference
                dnuI2_dnu = dnuI2_dnu + b(j) * (j) * dens_red ^ (j - 1) 'Eq. A30 Of reference
            Next

            term1 = (m_prom) * (8 * dens_red - 2 * dens_red ^ 2) / (1 - dens_red) ^ 4
            term2 = (1 - m_prom) * (20 * dens_red - 27 * dens_red ^ 2 + 12 * dens_red ^ 3 - 2 * dens_red ^ 4) / ((1 - dens_red) * (2 - dens_red)) ^ 2
            C1 = (1 + term1 + term2) ^ -1 'Eq. A11 Of reference

            term1 = m_prom * (-4 * dens_red ^ 2 + 20 * dens_red + 8) / (1 - dens_red) ^ 5
            term2 = (1 - m_prom) * (2 * dens_red ^ 3 + 12 * dens_red ^ 2 - 48 * dens_red + 40) / ((1 - dens_red) * (2 - dens_red)) ^ 3
            C2 = -C1 ^ 2 * (term1 + term2) 'Eq. A31 Of reference

            I2 = 0
            For j = 1 To 7
                I2 = I2 + b(j) * dens_red ^ (j - 1) 'Eq. A17 Of reference
            Next

            term1 = -2 * PI * dens_num * dnuI1_dnu * prom1
            term2 = -PI * dens_num * m_prom * (C1 * dnuI2_dnu + C2 * dens_red * I2) * prom2

            Zdisp = term1 + term2 'Eq. A28 Of reference

            Return Zdisp

        End Function

        Friend Function Z_hc(T As Double, dens_num As Double, mix As mixture)

            'Hard-chain contribution to the compressibility coefficient with PC-SAFT EoS
            'Auxiliary function, not to be used directly
            '
            'Reference: Gross and Sadowski, Ind. Eng. Chem. Res. 40 (2001) 1244-1260

            'Copyright (c) 2011 Ángel Martín, University of Valladolid (Spain)
            'This program is free software: you can redistribute it and/or modify
            'it under the terms of the GNU General Public License as published by
            'the Free Software Foundation, either version 3 of the License, or
            '(at your option) any later version.
            'This program is distributed in the hope that it will be useful,
            'but WITHOUT ANY WARRANTY without even the implied warranty of
            'MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
            'GNU General Public License for more details.
            'You should have received a copy of the GNU General Public License
            'along with this program.  If not, see <http://www.gnu.org/licenses/>.

            Dim x, m, sigma, epsilon, d As Double()

            'Reads pure-component properties
            numC = mix.numC
            x = mix.x
            m = zeros(numC)
            sigma = zeros(numC)
            epsilon = zeros(numC)
            For i = 1 To numC
                m(i) = mix.comp(i).EoSParam(1)
                sigma(i) = mix.comp(i).EoSParam(2)
                epsilon(i) = mix.comp(i).EoSParam(3)
            Next

            'Calculates the temperature-depNextant segment diameter
            d = GetD(T)

            Dim m_prom, auxil() As Double

            'mean segment number
            m_prom = 0
            For i = 1 To numC
                m_prom = m_prom + m(i) * x(i) 'Eq. 6 of reference
            Next

            'Segment weights w_s = x_i m_iR and segment diameters (copolymer segment view).
            Dim segd = GetSegD(mix, T)
            Dim ns = mix.nseg
            Dim w = zeros(ns)
            Dim sa As Integer
            For sa = 1 To ns
                w(sa) = x(mix.segParent(sa)) * mix.segM(sa)
            Next

            'auxiliary functions (zeta_0..zeta_3), over segment types
            auxil = zeros(4)
            For j = 1 To 4
                For sa = 1 To ns
                    auxil(j) = auxil(j) + w(sa) * segd(sa) ^ (j - 1)
                Next
                auxil(j) = auxil(j) * PI / 6 * dens_num
            Next

            Dim term1, term2, term3, Zhs As Double

            '**************************************************************************
            'Zhc
            '**************************************************************************
            term1 = auxil(4) / (1 - auxil(4))
            term2 = 3 * auxil(2) * auxil(3) / (auxil(1) * (1 - auxil(4)) ^ 2)
            term3 = (3 * auxil(3) ^ 3 - auxil(4) * auxil(3) ^ 3) / (auxil(1) * (1 - auxil(4)) ^ 3)
            Zhs = term1 + term2 + term3 'Eq. A26 of reference

            Dim sum1, Zhc As Double

            'Hard-chain compressibility (Eq. A25): sum over each molecule's bonds, at the bonded segment-pair
            'contact. A homopolymer/small molecule has one self-bond of fraction 1.
            sum1 = 0
            Dim bi As Integer
            For i = 1 To numC
                Dim bAcc As Double = 0.0
                Dim ba = mix.bondA(i - 1), bb = mix.bondB(i - 1), bf = mix.bondF(i - 1)
                For bi = 0 To bf.Length - 1
                    Dim gg As Double = GhsSeg(segd(ba(bi)), segd(bb(bi)), auxil)
                    bAcc = bAcc + bf(bi) * (gg ^ (-1)) * DensDgDensSeg(segd(ba(bi)), segd(bb(bi)), auxil)
                Next
                sum1 = sum1 + x(i) * (m(i) - 1) * bAcc
            Next

            Zhc = m_prom * Zhs - sum1 'Eq. A25 of reference

            Return Zhc

        End Function

        Friend Function mu_Ass(T As Double, dens_num As Double, mix As mixture) As Double()

            'Calculates the association contribution to the residual chemical potential
            'of mixture mix at temperature T And pressure P using SAFT EoS
            '
            'Parameters:
            'EoS Equation of state used for calculations
            'T: Temperature(K)
            'P: Pressure(K)
            'dens_num: Number density(molecule / Angstrom ^ 3)
            'mix: cMixture Object
            '
            'Results:
            'muass residual chemical potential, association contribution
            'EoS: returns EoS used for calculations
            '
            'Reference: Chapman et al., Ind.Eng.Chem.Res. 29 (1990) 1709-1721

            'Copyright (c) 2011 Ángel Martín, University of Valladolid (Spain)
            'This program Is free software: you can redistribute it And/Or modify
            'it under the terms of the GNU General Public License as published by
            'the Free Software Foundation, either version 3 of the License, Or
            '(at your option) any later version.
            'This program Is distributed in the hope that it will be useful,
            'but WITHOUT ANY WARRANTY without even the implied warranty of
            'MERCHANTABILITY Or FITNESS FOR A PARTICULAR PURPOSE.  See the
            'GNU General Public License for more details.
            'You should have received a copy of the GNU General Public License
            'along with this program.  If Not, see <http://www.gnu.org/licenses/>.

            Dim numC, x(), indx1, indx2, kappa(,), epsilon(,), kappa_, epsilon_, kappa1, kappa2, epsilon1, epsilon2 As Double
            Dim m, sigma, epsilon0, d As Double()
            Dim NumAss() As Double

            'Reads pure-component properties
            numC = mix.numC
            x = mix.x
            m = zeros(numC)
            sigma = zeros(numC)
            epsilon0 = zeros(numC)
            NumAss = zeros(numC)
            For i = 1 To numC
                m(i) = mix.comp(i).EoSParam(1)
                sigma(i) = mix.comp(i).EoSParam(2)
                epsilon0(i) = mix.comp(i).EoSParam(3)
                NumAss(i) = mix.comp(i).EoSParam(4)
            Next

            Dim kappa_v, epsilon_v As New List(Of Double(,))

            kappa_v.Add(New Double(,) {})
            epsilon_v.Add(New Double(,) {})

            numC = mix.numC
            x = mix.x
            For i = 1 To numC
                kappa_v.Add(mix.comp(i).EoSParam(5))
                epsilon_v.Add(mix.comp(i).EoSParam(6))
            Next

            'Calculates the temperature-depNextant segment diameter
            d = zeros(mix.numC)
            For i = 1 To numC
                d(i) = HardSphereDiameter(T, m(i), sigma(i), epsilon0(i))
            Next

            Dim auxil(), ghs(,), term1, term2, term3 As Double

            'auxiliary functions
            auxil = zeros(4)
            For j = 1 To 4
                For i = 1 To numC
                    auxil(j) = auxil(j) + x(i) * m(i) * d(i) ^ (j - 1)
                Next
                auxil(j) = auxil(j) * PI / 6 * dens_num 'Eq. 27 Of reference
            Next

            'radial distribution function
            ghs = zeros(numC, numC)
            For i = 1 To mix.numC
                For j = 1 To mix.numC
                    term1 = 1 / (1 - auxil(4))
                    term2 = d(i) * d(j) / (d(i) + d(j)) * 3 * auxil(3) / (1 - auxil(4)) ^ 2
                    term3 = (d(i) * d(j) / (d(i) + d(j))) ^ 2 * 2 * auxil(3) ^ 2 / (1 - auxil(4)) ^ 3
                    ghs(i, j) = term1 + term2 + term3 'Eq. 25 Of reference
                Next
            Next

            'Calculates the molar fraction of molecules Not bonded at association

            Dim Xa = SolveXa(mix, T, NumAss, sigma, d, ghs, dens_num)
            Dim multG = GlobalMult(mix, NumAss)

            Dim dgij_drok(,,), term4, term5, term6, term7 As Double

            'Derivatives for calculation of chemical potential

            dgij_drok = zeros(numC, numC, numC)

            For i = 1 To numC
                For j = 1 To numC
                    For k = 1 To numC
                        term1 = d(i) ^ 3 / (1 - auxil(4)) ^ 2
                        term2 = 3 * d(j) * d(k) / (d(j) + d(k))
                        term3 = d(i) ^ 2 / (1 - auxil(4)) ^ 2
                        term4 = 2 * d(i) ^ 3 * auxil(3) / (1 - auxil(4)) ^ 3
                        term5 = 2 * (d(j) * d(k) / (d(j) + d(k))) ^ 2
                        term6 = 2 * d(i) ^ 2 * auxil(3) / (1 - auxil(4)) ^ 3
                        term7 = 3 * d(i) ^ 3 * auxil(3) ^ 2 / (1 - auxil(4)) ^ 4
                        dgij_drok(j, k, i) = PI / 6 * m(i) * (term1 + term2 * (term3 + term4) + term5 * (term6 + term7)) 'Eq. A5 Of reference
                    Next
                Next
            Next

            Dim ddeltaAB_droi(,,) As Double

            ddeltaAB_droi = zeros((numC) * NumAss.Max, (numC) * NumAss.Max, numC)

            For i2 = 1 To numC
                indx1 = 0
                For i = 1 To numC
                    For j = 1 To NumAss(i)
                        indx1 = indx1 + 1
                        indx2 = 0
                        For k = 1 To numC
                            For l = 1 To NumAss(k)
                                indx2 = indx2 + 1
                                If i = k Then 'retrieves value from component matrix
                                    kappa = mix.comp(i).EoSParam(5)
                                    kappa_ = kappa(j, l)
                                    epsilon = mix.comp(i).EoSParam(6)
                                    epsilon_ = epsilon(j, l)
                                Else 'applies mixing rules
                                    kappa1 = max(mix.comp(i).EoSParam(5))
                                    epsilon1 = max(mix.comp(i).EoSParam(6))
                                    kappa2 = max(mix.comp(k).EoSParam(5))
                                    epsilon2 = max(mix.comp(k).EoSParam(6))
                                    kappa_ = Sqrt(kappa1 * kappa2) * (Sqrt(sigma(i) * sigma(k)) / (0.5 * (sigma(i) + sigma(k)))) ^ 3
                                    epsilon_ = 0.5 * (epsilon1 + epsilon2)
                                End If
                                ddeltaAB_droi(indx1, indx2, i2) = ((d(i) + d(k)) / 2) ^ 3 * dgij_drok(i, k, i2) * (Exp(epsilon_ / T) - 1) * kappa_
                            Next
                        Next
                    Next
                Next
            Next

            Dim dXaj_droi(,), dXaj_droi_v() As Double

            dXaj_droi_v = obj_muAss(mix, Xa, ddeltaAB_droi, T, NumAss, sigma, d, ghs, dens_num) 'Eq. A3 Of reference

            dXaj_droi = zeros(numC * NumAss.Max, numC)

            'Transforms column-vector parameter dXaj_droi into a matrix
            For i2 = 1 To numC
                indx1 = 0
                For i = 1 To numC
                    For j = 1 To NumAss(i)
                        indx1 = indx1 + 1
                        dXaj_droi(indx1, i2) = dXaj_droi_v((i2 - 1) * sum(NumAss) + indx1)
                    Next
                Next
            Next

            Dim sum1, term1_(), term2_(), muass() As Double

            term1_ = zeros(numC)
            term2_ = zeros(numC)

            'Association contribution to the chemical potential
            indx1 = 0
            For i = 1 To numC
                sum1 = 0
                Dim tm As Double = 0.0
                For j = 1 To NumAss(i)
                    indx1 = indx1 + 1
                    sum1 = sum1 + multG(indx1) * (Log(Xa(indx1)) - Xa(indx1) / 2)
                    tm += multG(indx1)
                Next
                term1_(i) = sum1 + 0.5 * tm
            Next

            For i = 1 To numC
                indx1 = 0
                sum1 = 0
                For j = 1 To numC
                    For k = 1 To NumAss(j)
                        indx1 = indx1 + 1
                        sum1 = sum1 + dens_num * x(j) * multG(indx1) * (dXaj_droi(indx1, i) * (1 / Xa(indx1) - 0.5))
                    Next
                Next
                term2_(i) = sum1
            Next

            muass = zeros(numC)
            For i = 1 To numC
                muass(i) = term1_(i) + term2_(i) 'Eq A2 Of reference
            Next

            Return muass

        End Function

        Friend Function obj_muAss(mix As mixture, Xa As Double(), ddeltaAB_droi As Double(,,), T As Double, NumAss As Double(), sigma As Double(), d As Double(), ghs As Double(,), dens_num As Double) As Double()

            'Auxiliary function for the calculation of associaton chemical potential
            'with SAFT (calculates eq. A3 of reference)
            '
            'Reference: Chapman et al., Ind.Eng.Chem.Res. 29 (1990) 1709-1721

            'Copyright (c) 2011 Ángel Martín, University of Valladolid (Spain)
            'This program Is free software: you can redistribute it And/Or modify
            'it under the terms of the GNU General Public License as published by
            'the Free Software Foundation, either version 3 of the License, Or
            '(at your option) any later version.
            'This program Is distributed in the hope that it will be useful,
            'but WITHOUT ANY WARRANTY without even the implied warranty of
            'MERCHANTABILITY Or FITNESS FOR A PARTICULAR PURPOSE.  See the
            'GNU General Public License for more details.
            'You should have received a copy of the GNU General Public License
            'along with this program.  If Not, see <http://www.gnu.org/licenses/>.

            numC = mix.numC

            Dim A(,), B(), indx1, indx2, indx3, sum1, kappa, epsilon, kappa_(,), epsilon_(,) As Double
            Dim epsilon1, epsilon2, kappa1, kappa2 As Double
            Dim delta(,), delta_ As Double
            Dim sum2 As Double

            Dim multG = GlobalMult(mix, NumAss)

            A = zeros(sum(NumAss) * numC, sum(NumAss) * numC)
            B = zeros(sum(NumAss) * numC)

            delta = zeros(numC * numC, numC * DirectCast(NumAss, Double()).Max)

            indx3 = 0
            For i2 = 1 To numC
                indx1 = 0
                For i = 1 To numC
                    For j = 1 To NumAss(i)
                        indx1 = indx1 + 1
                        indx3 = indx3 + 1
                        indx2 = 0
                        sum1 = 0
                        For k = 1 To numC
                            For l = 1 To NumAss(k)
                                indx2 = indx2 + 1
                                If i = k Then 'retrieves value from component matrix
                                    kappa_ = mix.comp(i).EoSParam(5)
                                    kappa = kappa_(j, l)
                                    epsilon_ = mix.comp(i).EoSParam(6)
                                    epsilon = epsilon_(j, l)
                                Else 'applies mixing rules
                                    kappa1 = max(mix.comp(i).EoSParam(5))
                                    epsilon1 = max(mix.comp(i).EoSParam(6))
                                    kappa2 = max(mix.comp(k).EoSParam(5))
                                    epsilon2 = max(mix.comp(k).EoSParam(6))
                                    kappa = Sqrt(kappa1 * kappa2) * (Sqrt(sigma(i) * sigma(k)) / (0.5 * (sigma(i) + sigma(k)))) ^ 3
                                    epsilon = 0.5 * (epsilon1 + epsilon2)
                                End If
                                delta(indx1, indx2) = ((d(i) + d(k)) / 2) ^ 3 * ghs(i, k) * kappa * (Exp(epsilon / T) - 1)
                                sum1 = sum1 + dens_num * mix.x(k) * multG(indx2) * (Xa(indx2) * ddeltaAB_droi(indx1, indx2, i2))
                                A(indx1 + (i2 - 1) * sum(NumAss), indx2 + (i2 - 1) * sum(NumAss)) = A(indx1 + (i2 - 1) * sum(NumAss), indx2 + (i2 - 1) * sum(NumAss)) + Xa(indx1) ^ 2 * dens_num * mix.x(k) * multG(indx2) * delta(indx1, indx2)
                            Next
                        Next

                        sum2 = 0
                        For k = 1 To NumAss(i2)
                            If i = i2 Then 'retrieves value from component matrix
                                kappa_ = mix.comp(i).EoSParam(5)
                                kappa = kappa_(j, k)
                                epsilon_ = mix.comp(i).EoSParam(6)
                                epsilon = epsilon_(j, k)
                            Else 'applies mixing rules
                                kappa1 = max(mix.comp(i).EoSParam(5))
                                epsilon1 = max(mix.comp(i).EoSParam(6))
                                kappa2 = max(mix.comp(i2).EoSParam(5))
                                epsilon2 = max(mix.comp(i2).EoSParam(6))
                                kappa = Sqrt(kappa1 * kappa2) * (Sqrt(sigma(i) * sigma(i2)) / (0.5 * (sigma(i) + sigma(i2)))) ^ 3
                                epsilon = 0.5 * (epsilon1 + epsilon2)
                            End If
                            delta_ = ((d(i) + d(i2)) / 2) ^ 3 * ghs(i, i2) * kappa * (Exp(epsilon / T) - 1)
                            Dim gk As Integer = CInt(DirectCast(NumAss, Double()).Take(i2 - 1).Sum) + k
                            sum2 = sum2 + multG(gk) * Xa(gk) * delta_
                        Next
                        A(indx3, indx3) = A(indx3, indx3) + 1
                        B(indx3) = -(Xa(indx1)) ^ 2 * (sum1 + sum2)
                    Next
                Next
            Next

            'Solves linear system of equations

            Dim A2 = zeros(sum(NumAss) * numC - 1, sum(NumAss) * numC - 1)
            Dim B2 = zeros(sum(NumAss) * numC - 1)

            Dim solution = zeros(sum(NumAss) * numC)
            Dim solution2 = zeros(sum(NumAss) * numC - 1)

            For i = 0 To A.GetLength(0) - 2
                For j = 0 To A.GetLength(1) - 2
                    A2(i, j) = A(i + 1, j + 1)
                Next
                B2(i) = B(i + 1)
            Next

            Dim result = DWSIM.MathOps.MathEx.SysLin.rsolve.rmatrixsolve(A2, B2, B2.Length, solution2)

            solution2.Take(solution2.Length - 1).ToArray.CopyTo(solution, 1)

            Return solution

        End Function

        Friend Function Z_ass(T As Double, dens_num As Double, mix As mixture) As Double

            'Associating contribution to the compressibility coefficient with SAFT EoS
            'Auxiliary function, Not to be used directly
            '
            'Reference: Chapman et al., Ind.Eng.Chem.Res. 29 (1990) 1709-1721

            'Copyright (c) 2011 Ángel Martín, University of Valladolid (Spain)
            'This program Is free software: you can redistribute it And/Or modify
            'it under the terms of the GNU General Public License as published by
            'the Free Software Foundation, either version 3 of the License, Or
            '(at your option) any later version.
            'This program Is distributed in the hope that it will be useful,
            'but WITHOUT ANY WARRANTY without even the implied warranty of
            'MERCHANTABILITY Or FITNESS FOR A PARTICULAR PURPOSE.  See the
            'GNU General Public License for more details.
            'You should have received a copy of the GNU General Public License
            'along with this program.  If Not, see <http://www.gnu.org/licenses/>.

            Dim x As Double()

            'Reads pure-component properties
            numC = mix.numC
            x = mix.x

            Dim NumAss(), muass(), Aass, sum1, Zass As Double

            NumAss = zeros(numC)
            For i = 1 To numC
                NumAss(i) = mix.comp(i).EoSParam(4)
            Next

            If sum(NumAss) > 0 Then

                muass = mu_Ass(T, dens_num, mix)
                Aass = HelmholtzAss(T, dens_num, mix)

                sum1 = 0
                For i = 1 To numC
                    sum1 = sum1 + x(i) * muass(i)
                Next

                Zass = sum1 - Aass 'Eq. A10 Of reference

            Else

                Zass = 0

            End If

            Return Zass

        End Function

        Friend Function HelmholtzAss(T As Double, dens_num As Double, mix As mixture) As Double

            'Calculates the association contribution to the residual Helmholtz energy
            'of mixture mix at temperature T And pressure P using SAFT EoS
            '
            'Parameters:
            'EoS Equation of state used for calculations
            'T: Temperature(K)
            'dens_num: Number density(molecule / Angstrom ^ 3)
            'mix: cMixture Object
            '
            'Results:
            'Aass residual Helmholtz energy, association contribution
            'Xa: Fraction of associated sites
            'EoS: returns EoS used for calculations
            '
            'Reference Chapman et al., Ind. Eng. Chem. Res. 29 (1990) 1709-1721

            'Copyright (c) 2011 Ángel Martín, University of Valladolid (Spain)
            'This program Is free software: you can redistribute it And/Or modify
            'it under the terms of the GNU General Public License as published by
            'the Free Software Foundation, either version 3 of the License, Or
            '(at your option) any later version.
            'This program Is distributed in the hope that it will be useful,
            'but WITHOUT ANY WARRANTY without even the implied warranty of
            'MERCHANTABILITY Or FITNESS FOR A PARTICULAR PURPOSE.  See the
            'GNU General Public License for more details.
            'You should have received a copy of the GNU General Public License
            'along with this program.  If Not, see <http://www.gnu.org/licenses/>.

            Dim x, m, sigma, epsilon, d As Double()
            Dim NumAss() As Double

            'Reads pure-component properties
            numC = mix.numC
            x = mix.x
            m = zeros(numC)
            sigma = zeros(numC)
            epsilon = zeros(numC)
            NumAss = zeros(numC)
            For i = 1 To numC
                m(i) = mix.comp(i).EoSParam(1)
                sigma(i) = mix.comp(i).EoSParam(2)
                epsilon(i) = mix.comp(i).EoSParam(3)
                NumAss(i) = mix.comp(i).EoSParam(4)
            Next

            'Calculates the temperature-depNextant segment diameter
            d = GetD(T)

            Dim auxil(), ghs(,), term1, term2, term3 As Double

            'auxiliary functions
            auxil = zeros(4)
            For j = 1 To 4
                For i = 1 To numC
                    auxil(j) = auxil(j) + x(i) * m(i) * d(i) ^ (j - 1)
                Next
                auxil(j) = auxil(j) * PI / 6 * dens_num 'Eq. 27 Of reference
            Next

            'radial distribution function
            ghs = zeros(numC, numC)
            For i = 1 To numC
                For j = 1 To numC
                    term1 = 1 / (1 - auxil(4))
                    term2 = d(i) * d(j) / (d(i) + d(j)) * 3 * auxil(3) / (1 - auxil(4)) ^ 2
                    term3 = (d(i) * d(j) / (d(i) + d(j))) ^ 2 * 2 * auxil(3) ^ 2 / (1 - auxil(4)) ^ 3
                    ghs(i, j) = term1 + term2 + term3 'Eq. 25 Of reference
                Next
            Next

            Dim Aass, indx1, sum1 As Double

            'Calculates the molar fraction of molecules Not bonded at association

            Dim Xa = SolveXa(mix, T, NumAss, sigma, d, ghs, dens_num)
            Dim multG = GlobalMult(mix, NumAss)

            'Association contribution to Helmholtz energy
            Aass = 0
            indx1 = 0
            For i = 1 To numC
                sum1 = 0
                Dim tm As Double = 0.0
                For j = 1 To NumAss(i)
                    indx1 = indx1 + 1
                    sum1 = sum1 + multG(indx1) * (Log(Xa(indx1)) - Xa(indx1) / 2)
                    tm += multG(indx1)
                Next
                Aass = Aass + x(i) * (sum1 + 0.5 * tm) 'Eq. 21 Of reference (site multiplicities weighted)
            Next

            Return Aass

        End Function

        Friend Function compr(T As Double, P As Double, mix As mixture, phase As String, Zestimate As Double)

            'Calculates the compressibility coefficient of mixture mix at temperature T
            'And pressure P using SAFT EoS
            '
            'Parameters:
            'EoS Equation of state used for calculations
            'T: Temperature(K)
            'P: Pressure(Pa)
            'mix: cMixture Object
            'phase: set  phase = 'liq' to get the coefficient of the liquid phase, phase = 'gas'  
            '   to get the coefficient of the gas phase 
            '
            'Optional parameters (set [] to keep default value)
            'Z_ini: Initial guess for the compressibility coefficient
            '   If Not defined, the program uses an initial guess Z_ini = 0.8 for gas
            '   phase And a Z_ini corresponding to a liquid density of 800 kg/m3 for
            '   the liquid phase
            'options: parameters of the fsolve numerical resolution method (structure
            '   generated with "optimset")
            '
            'Results:
            'Z compresibility coefficient
            'EoS: returns EoS used for calculations

            'Copyright (c) 2011 Ángel Martín, University of Valladolid (Spain)
            'This program Is free software: you can redistribute it And/Or modify
            'it under the terms of the GNU General Public License as published by
            'the Free Software Foundation, either version 3 of the License, Or
            '(at your option) any later version.
            'This program Is distributed in the hope that it will be useful,
            'but WITHOUT ANY WARRANTY without even the implied warranty of
            'MERCHANTABILITY Or FITNESS FOR A PARTICULAR PURPOSE.  See the
            'GNU General Public License for more details.
            'You should have received a copy of the GNU General Public License
            'along with this program.  If Not, see <http://www.gnu.org/licenses/>.

            '**************************************************************************
            'Initial guess
            '**************************************************************************

            Dim ro, sumat, ini, Zini As Double

            If Zestimate = -1 Then
                If phase = "gas" Then
                    ro = P / (0.8 * 8.31 * T) * 1.0E-30 * 6.022E+23 'molecule/A3
                    sumat = 0
                    For i = 1 To mix.numC
                        sumat = sumat + mix.x(i) * mix.comp(i).EoSParam(1) * (mix.comp(i).EoSParam(2)) ^ 3
                    Next
                    ini = PI / 6 * ro * sumat
                ElseIf phase = "liq" Then
                    ro = 800 * 1000 / mix.MW * 1.0E-30 * 6.022E+23 'molecule/A3
                    sumat = 0
                    For i = 1 To mix.numC
                        sumat = sumat + mix.x(i) * mix.comp(i).EoSParam(1) * (mix.comp(i).EoSParam(2)) ^ 3
                    Next
                    ini = PI / 6 * ro * sumat
                Else
                    Throw New Exception("Undefined phase type, must be liq or gas")
                End If
            Else
                Zini = Zestimate
                ro = P / (Zini * 8.31 * T) * 1.0E-30 * 6.022E+23 'molecule/A3
                sumat = 0
                For i = 1 To mix.numC
                    sumat = sumat + mix.x(i) * mix.comp(i).EoSParam(1) * (mix.comp(i).EoSParam(2)) ^ 3
                Next
                ini = PI / 6 * ro * sumat
            End If

            '**************************************************************************
            'Calculates Z And mu with SAFT
            '**************************************************************************

            'If phase = "liq" Then

            '    Dim intervals As New List(Of Tuple(Of Double, Double))

            '    Dim minval, maxval As Double

            '    ro = 1400 * 1000 / mix.MW * 1.0E-30 * 6.022E+23 'molecule/A3
            '    sumat = 0
            '    For i = 1 To mix.numC
            '        sumat = sumat + mix.x(i) * mix.comp(i).EoSParam(1) * (mix.comp(i).EoSParam(2)) ^ 3
            '    Next
            '    maxval = PI / 6 * ro * sumat

            '    ro = 300 * 1000 / mix.MW * 1.0E-30 * 6.022E+23 'molecule/A3
            '    sumat = 0
            '    For i = 1 To mix.numC
            '        sumat = sumat + mix.x(i) * mix.comp(i).EoSParam(1) * (mix.comp(i).EoSParam(2)) ^ 3
            '    Next
            '    minval = PI / 6 * ro * sumat

            '    Dim f1, f2, x1, delta, dens As Double
            '    delta = (maxval - minval) / 20
            '    x1 = minval
            '    While x1 <= maxval
            '        Do
            '            f1 = obj_SAFT(x1, T, P, mix)(0)
            '            f2 = obj_SAFT(x1 + delta, T, P, mix)(0)
            '            x1 += delta
            '        Loop Until f1 * f2 < 0.0 Or x1 >= maxval
            '        If x1 < maxval Then intervals.Add(New Tuple(Of Double, Double)(x1 - delta, x1))
            '    End While

            '    Dim brent As New DWSIM.MathOps.MathEx.BrentOpt.Brent()
            '    brent.DefineFuncDelegate(Function(x, otherargs) obj_SAFT(x, T, P, mix)(0))
            '    Dim Zvec As New List(Of Double)
            '    For Each interval In intervals
            '        dens = brent.BrentOpt(interval.Item1, interval.Item2, 10, 0.0001, 1000, Nothing)
            '        Zvec.Add(obj_SAFT(dens, T, P, mix)(1))
            '    Next

            '    Return Zvec.Min

            'Else

            ' The variable is the reduced density (packing fraction eta), physically in (0, ~0.74).
            ' obj_SAFT(eta) = P - Pcalc(eta) is finite over that range and crosses zero at each real
            ' root; beyond close packing the hard-sphere (1 - eta) terms turn singular and it goes NaN.
            ' Scan for sign changes and pick the liquid (highest-eta) or gas (lowest-eta) root - robust
            ' for a polymer-rich phase, where the old simplex-on-squared-objective slid onto a spurious
            ' low-density root or wandered into the NaN region.
            Dim etaMax As Double = 0.7404
            Dim etaMin As Double = 0.000001
            Dim npts As Integer = 60
            Dim roots As New List(Of Double)
            Dim etaPrev As Double = etaMin
            Dim fPrev As Double = obj_SAFT(etaPrev, T, P, mix)(0)
            For k As Integer = 1 To npts
                Dim eta As Double = etaMin + (etaMax - etaMin) * k / npts
                Dim fCur As Double = obj_SAFT(eta, T, P, mix)(0)
                If Not Double.IsNaN(fPrev) AndAlso Not Double.IsNaN(fCur) AndAlso fPrev * fCur < 0.0 Then
                    Dim a As Double = etaPrev, b As Double = eta, fa As Double = fPrev
                    For it As Integer = 1 To 60
                        Dim mmid As Double = 0.5 * (a + b)
                        If (b - a) < 0.000000000001 Then Exit For
                        Dim fm As Double = obj_SAFT(mmid, T, P, mix)(0)
                        If Double.IsNaN(fm) Then Exit For
                        If fa * fm <= 0.0 Then
                            b = mmid
                        Else
                            a = mmid : fa = fm
                        End If
                    Next
                    roots.Add(0.5 * (a + b))
                End If
                etaPrev = eta : fPrev = fCur
            Next

            Dim etaSol As Double
            If roots.Count = 0 Then
                ' No bracketed root (e.g. numerical noise): fall back to the ideal-density guess.
                etaSol = Math.Min(Math.Max(ini, etaMin), etaMax)
            ElseIf phase = "liq" Then
                etaSol = roots.Max
            Else
                etaSol = roots.Min
            End If

            Return obj_SAFT(etaSol, T, P, mix)(1)

            'End If

        End Function

        Public Function eval_g(ByVal n As Integer, ByVal x As Double(), ByVal new_x As Boolean, ByVal m As Integer, ByRef g As Double()) As Boolean
            Return True
        End Function

        Public Function eval_jac_g(ByVal n As Integer, ByVal x As Double(), ByVal new_x As Boolean, ByVal m As Integer, ByVal nele_jac As Integer, ByRef iRow As Integer(),
         ByRef jCol As Integer(), ByRef values As Double()) As Boolean
            Return False
        End Function

        Public Function eval_h(ByVal n As Integer, ByVal x As Double(), ByVal new_x As Boolean, ByVal obj_factor As Double, ByVal m As Integer, ByVal lambda As Double(),
         ByVal new_lambda As Boolean, ByVal nele_hess As Integer, ByRef iRow As Integer(), ByRef jCol As Integer(), ByRef values As Double()) As Boolean
            Return False
        End Function

        Friend Function GlobalMult(mix, NumAss) As Double()

            'Flattens the per-compound site multiplicities (EoSParam(7)) into one global site vector
            'aligned with the flattened site index used throughout the association routines. A site
            'type with multiplicity n stands for n identical sites (they share one site fraction), so
            'the association sums are weighted by it. Defaults to one per site when a compound carries
            'no multiplicity vector, which reproduces the plain one-site-per-type (2B/4C) behaviour.

            Dim nSit As Integer = CInt(sum(NumAss))
            Dim mg(nSit) As Double
            Dim s As Integer = 0
            For i = 1 To mix.numC
                Dim mv As Double() = Nothing
                Try
                    mv = DirectCast(mix.comp(i).EoSParam(7), Double())
                Catch
                    mv = Nothing
                End Try
                For j = 1 To CInt(NumAss(i))
                    s += 1
                    If mv IsNot Nothing AndAlso mv.Length > j Then
                        mg(s) = mv(j)
                    Else
                        mg(s) = 1.0
                    End If
                Next
            Next
            Return mg

        End Function

        Friend Function SolveXa(mix As mixture, T As Double, NumAss As Double(), sigma As Double(), d As Double(), ghs As Double(,), dens_num As Double) As Double()

            'Solves the fraction of non-bonded association sites Xa by successive substitution of
            'Xa_a = 1 / (1 + sum_b rho x_b n_b Xa_b delta_ab), where n_b is the site multiplicity. The
            'iteration keeps every fraction in (0,1] by construction, which the previous unconstrained
            'simplex minimisation did not: it could return negative site fractions and turn the log(Xa)
            'terms in the Helmholtz energy and chemical potential into NaN, above all for high
            'segment-number polymers with a 4C association scheme.

            Dim numC As Integer = mix.numC
            Dim nSit As Integer = CInt(sum(NumAss))

            Dim Xa(nSit) As Double
            If nSit = 0 Then Return Xa

            Dim multG = GlobalMult(mix, NumAss)

            'site -> component map
            Dim compOf(nSit) As Integer
            Dim s As Integer = 0
            For i = 1 To numC
                For j = 1 To CInt(NumAss(i))
                    s += 1
                    compOf(s) = i
                Next
            Next

            'association-strength matrix delta(a,b): site a (of comp i) with site b (of comp k)
            Dim delta(nSit, nSit) As Double
            Dim ka(,), ea(,), kappa_, epsilon_ As Double
            Dim inda As Integer = 0
            For i = 1 To numC
                For j = 1 To CInt(NumAss(i))
                    inda += 1
                    Dim indb As Integer = 0
                    For k = 1 To numC
                        For l = 1 To CInt(NumAss(k))
                            indb += 1
                            If i = k Then 'value from the component site matrix
                                ka = mix.comp(i).EoSParam(5)
                                kappa_ = ka(j, l)
                                ea = mix.comp(i).EoSParam(6)
                                epsilon_ = ea(j, l)
                            Else 'combining rules for unlike components
                                kappa_ = Sqrt(max(mix.comp(i).EoSParam(5)) * max(mix.comp(k).EoSParam(5))) * (Sqrt(sigma(i) * sigma(k)) / (0.5 * (sigma(i) + sigma(k)))) ^ 3
                                epsilon_ = 0.5 * (max(mix.comp(i).EoSParam(6)) + max(mix.comp(k).EoSParam(6)))
                            End If
                            delta(inda, indb) = ((d(i) + d(k)) / 2) ^ 3 * ghs(i, k) * kappa_ * (Exp(epsilon_ / T) - 1)
                        Next
                    Next
                Next
            Next

            'successive substitution with light damping
            For a = 1 To nSit
                Xa(a) = 0.2
            Next
            For it As Integer = 1 To 500
                Dim maxd As Double = 0.0
                For a = 1 To nSit
                    Dim acc As Double = 0.0
                    For b = 1 To nSit
                        acc += dens_num * mix.x(compOf(b)) * multG(b) * Xa(b) * delta(a, b)
                    Next
                    Dim xn As Double = 1.0 / (1.0 + acc)
                    Dim diff As Double = xn - Xa(a)
                    If diff < 0.0 Then diff = -diff
                    If diff > maxd Then maxd = diff
                    Xa(a) = 0.5 * (Xa(a) + xn)
                Next
                If maxd < 0.000000000001 Then Exit For
            Next

            Return Xa

        End Function

        Friend Function obj_HelmholtzAss(Xa, mix, T, NumAss, sigma, d, ghs, dens_num)

            'Calculates the fraction for association site in the PC-SAFT EoS
            'Auxiliary function, Not to be used directly
            '
            'Reference: Chapman et al., Ind.Eng.Chem.Res. 29 (1990) 1709-1721

            'Copyright (c) 2011 Ángel Martín, University of Valladolid (Spain)
            'This program Is free software: you can redistribute it And/Or modify
            'it under the terms of the GNU General Public License as published by
            'the Free Software Foundation, either version 3 of the License, Or
            '(at your option) any later version.
            'This program Is distributed in the hope that it will be useful,
            'but WITHOUT ANY WARRANTY without even the implied warranty of
            'MERCHANTABILITY Or FITNESS FOR A PARTICULAR PURPOSE.  See the
            'GNU General Public License for more details.
            'You should have received a copy of the GNU General Public License
            'along with this program.  If Not, see <http://www.gnu.org/licenses/>.

            Dim numC, x(), indx1, indx2, kappa(,), epsilon(,), kappa_, epsilon_, kappa1, kappa2, epsilon1, epsilon2 As Double

            Dim kappa_v, epsilon_v As New List(Of Double(,))

            kappa_v.Add(New Double(,) {})
            epsilon_v.Add(New Double(,) {})

            numC = mix.numC
            x = mix.x
            For i = 1 To numC
                kappa_v.Add(mix.comp(i).EoSParam(5))
                epsilon_v.Add(mix.comp(i).EoSParam(6))
            Next

            Dim delta = zeros(sum(NumAss), sum(NumAss))

            indx1 = 0

            For i = 1 To numC
                For j = 1 To NumAss(i)
                    indx1 = indx1 + 1
                    indx2 = 0
                    For k = 1 To numC
                        For l = 1 To NumAss(k)
                            indx2 = indx2 + 1
                            If i = k Then 'retrieves value from component matrix
                                kappa = kappa_v(i)
                                kappa_ = kappa(j, l)
                                epsilon = epsilon_v(i)
                                epsilon_ = epsilon(j, l)
                            Else 'applies mixing rules
                                kappa1 = max(kappa_v(i))
                                epsilon1 = max(epsilon_v(i))
                                kappa2 = max(kappa_v(k))
                                epsilon2 = max(epsilon_v(k))
                                kappa_ = Sqrt(kappa1 * kappa2) * (Sqrt(sigma(i) * sigma(k)) / (0.5 * (sigma(i) + sigma(k)))) ^ 3
                                epsilon_ = 0.5 * (epsilon1 + epsilon2)
                            End If
                            delta(indx1, indx2) = ((d(i) + d(k)) / 2) ^ 3 * ghs(i, k) * kappa_ * (Exp(epsilon_ / T) - 1)
                        Next
                    Next
                Next
            Next

            Dim sum1 As Double

            'Calculates the equations
            Dim Res = zeros(sum(NumAss))
            indx1 = 0
            For i = 1 To numC
                For j = 1 To NumAss(i)
                    indx1 = indx1 + 1
                    indx2 = 0
                    sum1 = 0
                    For k = 1 To numC
                        For l = 1 To NumAss(k)
                            indx2 = indx2 + 1
                            sum1 = sum1 + x(k) * dens_num * Xa(indx2) * delta(indx1, indx2)
                        Next
                    Next
                    Res(indx1) = Xa(indx1) - (1 + sum1) ^ -1
                Next
            Next

            'Calculates the jacobian
            'indx1 = 0
            'Dim jv = zeros(sum(NumAss), sum(NumAss))

            'For i = 1 To numC
            '    For j = 1 To NumAss(i)
            '        indx1 = indx1 + 1
            '        indx2 = 0
            '        sum1 = 0
            '        For k = 1 To numC
            '            For l = 1 To NumAss(k)
            '                indx2 = indx2 + 1
            '                sum1 = sum1 + x(k) * dens_num * Xa(indx2) * delta(indx1, indx2)
            '            Next
            '        Next
            '        indx2 = 0
            '        For k = 1 To numC
            '            For l = 1 To NumAss(k)
            '                indx2 = indx2 + 1
            '                jv(indx1, indx2) = jv(indx1, indx2) + x(k) * dens_num * delta(indx1, indx2) / (1 + sum1) ^ 2
            '            Next
            '        Next
            '        jv(indx1, indx1) = jv(indx1, indx1) + 1
            '    Next
            'Next

            Return Res.AbsSqrSumY

        End Function

    End Class

End Namespace