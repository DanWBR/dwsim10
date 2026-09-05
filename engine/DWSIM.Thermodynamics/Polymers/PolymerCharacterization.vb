Imports DotNumerics.LinearAlgebra

Namespace Polymers

    ''' <summary>
    ''' Discretizes a polymer molar-mass distribution into pseudo-components, so a polydisperse polymer can
    ''' be modelled with an equation of state (PC-SAFT) as a mixture of cuts of the same chemistry and
    ''' different molar mass. The cuts share the base compound's CAS, so the equation of state reuses the
    ''' same segment parameters (m/M, sigma, epsilon) and only the molar mass, hence the segment number
    ''' m = (m/M) * M, differs between cuts.
    ''' </summary>
    ''' <summary>Molar-mass distribution shape a polymer sample is discretized from.</summary>
    Public Enum PolymerDistribution
        SchulzZimm = 0
        LogNormal = 1
    End Enum

    Public Class PolymerCharacterization

        ''' <summary>
        ''' N-cut Schulz-Zimm (Gamma) discretization by generalized Gauss-Laguerre quadrature. Returns the
        ''' cut molar masses M(k) (g/mol) and their number (mole) fractions z(k), z summing to one. Because
        ''' the quadrature is exact for polynomials up to degree 2N-1, the number-average Mn (N >= 1), the
        ''' weight-average Mw and the z-average Mz (both N >= 2) of the cuts equal those of the continuous
        ''' Schulz-Zimm distribution exactly; higher cuts refine the shape of the curve.
        ''' </summary>
        ''' <param name="Mn">Number-average molar mass (g/mol).</param>
        ''' <param name="PDI">Polydispersity index Mw/Mn (greater than one).</param>
        ''' <param name="N">Number of pseudo-components (at least one).</param>
        ''' <param name="M">Output: cut molar masses (g/mol), ascending.</param>
        ''' <param name="z">Output: cut number (mole) fractions, summing to one.</param>
        Public Shared Sub SchulzZimmCuts(ByVal Mn As Double, ByVal PDI As Double, ByVal N As Integer,
                                         ByRef M As Double(), ByRef z As Double())

            If N < 1 Then N = 1
            If PDI <= 1.0 Then PDI = 1.0000001

            Dim k As Double = 1.0 / (PDI - 1.0)   ' Gamma shape parameter
            Dim theta As Double = Mn / k          ' Gamma scale parameter
            Dim alpha As Double = k - 1.0         ' exponent of the generalized Laguerre weight x^alpha e^-x

            M = New Double(N - 1) {}
            z = New Double(N - 1) {}

            If N = 1 Then
                M(0) = Mn
                z(0) = 1.0
                Return
            End If

            ' Golub-Welsch: the nodes and weights of the generalized Gauss-Laguerre rule are the eigenvalues
            ' and the squared first components of the eigenvectors of the Jacobi matrix of the recurrence for
            ' the generalized Laguerre polynomials (weight x^alpha e^-x).
            Dim J As New SymmetricMatrix(N)
            For i As Integer = 0 To N - 1
                J(i, i) = 2.0 * i + alpha + 1.0
            Next
            For i As Integer = 1 To N - 1
                Dim b As Double = Math.Sqrt(i * (i + alpha))
                J(i, i - 1) = b   ' the symmetric matrix mirrors this into (i-1, i)
            Next

            Dim eig As New EigenSystem()
            Dim vecs As Matrix = Nothing
            Dim vals As Matrix = eig.GetEigenvalues(J, vecs)   ' eigenvalues ascending, eigenvectors in columns

            For i As Integer = 0 To N - 1
                M(i) = theta * vals(i, 0)      ' node x_i scaled by the Gamma scale
                Dim v0 As Double = vecs(0, i)  ' first component of the i-th eigenvector
                z(i) = v0 * v0                 ' quadrature weight normalised to a number fraction
            Next

        End Sub

        ''' <summary>
        ''' N-cut log-normal discretization. A log-normal number distribution has ln(M) normal, so the cuts
        ''' are placed at M(k) = exp(mu + sqrt(2) sigma t(k)) on the stable Gauss-Hermite nodes t(k) with the
        ''' Gauss-Hermite number fractions z(k). Unlike the Schulz-Zimm case M is exponential (not linear) in
        ''' the node, so a fixed quadrature would not reproduce the moments; instead the spread sigma is solved
        ''' so the discrete polydispersity S2/S1^2 equals PDI exactly (it depends on sigma alone and rises
        ''' monotonically), and mu is then set from Mn. Both the cut Mn and Mw therefore equal the targets
        ''' exactly (for N >= 2; Mz is approximate). A finite cut count caps the reachable PDI (two cuts reach
        ''' at most PDI = 2); a target beyond that returns the widest spread the cut count allows.
        ''' </summary>
        ''' <param name="Mn">Number-average molar mass (g/mol).</param>
        ''' <param name="PDI">Polydispersity index Mw/Mn (greater than one).</param>
        ''' <param name="N">Number of pseudo-components (at least one).</param>
        ''' <param name="M">Output: cut molar masses (g/mol), ascending.</param>
        ''' <param name="z">Output: cut number (mole) fractions, summing to one.</param>
        Public Shared Sub LogNormalCuts(ByVal Mn As Double, ByVal PDI As Double, ByVal N As Integer,
                                        ByRef M As Double(), ByRef z As Double())

            If N < 1 Then N = 1
            If PDI <= 1.0 Then PDI = 1.0000001

            M = New Double(N - 1) {}
            z = New Double(N - 1) {}

            If N = 1 Then
                M(0) = Mn
                z(0) = 1.0
                Return
            End If

            ' Gauss-Hermite nodes (weight e^-t^2) by Golub-Welsch: symmetric tridiagonal Jacobi matrix with a
            ' zero diagonal and off-diagonal sqrt(i/2). Nodes are the eigenvalues; the number fractions are the
            ' squared first eigenvector components (the Gauss-Hermite weights normalised by their sum sqrt(pi)).
            Dim J As New SymmetricMatrix(N)
            For i As Integer = 1 To N - 1
                J(i, i - 1) = Math.Sqrt(i / 2.0)
            Next

            Dim eig As New EigenSystem()
            Dim vecs As Matrix = Nothing
            Dim vals As Matrix = eig.GetEigenvalues(J, vecs)

            Dim t As Double() = New Double(N - 1) {}
            For i As Integer = 0 To N - 1
                t(i) = vals(i, 0)
                Dim v0 As Double = vecs(0, i)
                z(i) = v0 * v0
            Next

            ' Solve s = sqrt(2)*sigma so the discrete PDI matches the target, then scale to the target Mn.
            Dim s As Double = SolveLogNormalSpread(t, z, PDI)
            Dim S1 As Double = 0.0
            For i As Integer = 0 To N - 1
                S1 += z(i) * Math.Exp(s * t(i))
            Next
            For i As Integer = 0 To N - 1
                M(i) = Mn * Math.Exp(s * t(i)) / S1
            Next

        End Sub

        ' Discrete polydispersity S2/S1^2 of the Gauss-Hermite cuts at spread s = sqrt(2)*sigma. It equals one
        ' at s = 0 and rises monotonically, so the target PDI is found by bisection.
        Private Shared Function LogNormalPDI(t As Double(), z As Double(), s As Double) As Double
            Dim S1 As Double = 0.0, S2 As Double = 0.0
            For i As Integer = 0 To t.Length - 1
                S1 += z(i) * Math.Exp(s * t(i))
                S2 += z(i) * Math.Exp(2.0 * s * t(i))
            Next
            Return S2 / (S1 * S1)
        End Function

        Private Shared Function SolveLogNormalSpread(t As Double(), z As Double(), PDI As Double) As Double
            Dim lo As Double = 0.0, hi As Double = 6.0
            If LogNormalPDI(t, z, hi) <= PDI Then Return hi   ' target beyond the reach of this cut count
            For it As Integer = 1 To 100
                Dim mid As Double = 0.5 * (lo + hi)
                If LogNormalPDI(t, z, mid) < PDI Then lo = mid Else hi = mid
            Next
            Return 0.5 * (lo + hi)
        End Function

        ''' <summary>
        ''' Builds N pseudo-component compounds by cloning a base polymer, one per cut of the chosen
        ''' distribution, each with its own molar mass and a distinguishing name and sharing the base CAS so
        ''' the equation of state reuses its parameters. Returns the compounds; the matching relative mole
        ''' fractions z(k) come back in <paramref name="z"/> and are scaled by the total polymer mole fraction
        ''' when setting the feed.
        ''' </summary>
        Public Shared Function BuildCuts(ByVal basePolymer As BaseClasses.ConstantProperties,
                                         ByVal Mn As Double, ByVal PDI As Double, ByVal N As Integer,
                                         ByVal distribution As PolymerDistribution,
                                         ByRef z As Double()) As List(Of BaseClasses.ConstantProperties)

            Dim M As Double() = Nothing
            Select Case distribution
                Case PolymerDistribution.LogNormal
                    LogNormalCuts(Mn, PDI, N, M, z)
                Case Else
                    SchulzZimmCuts(Mn, PDI, N, M, z)
            End Select

            Dim cuts As New List(Of BaseClasses.ConstantProperties)
            For i As Integer = 0 To M.Length - 1
                Dim c = DirectCast(basePolymer.Clone(), BaseClasses.ConstantProperties)
                c.Molar_Weight = M(i)
                c.Name = basePolymer.Name & " (M=" & CInt(M(i)).ToString() & ")"
                cuts.Add(c)
            Next
            Return cuts

        End Function

        ''' <summary>Backward-compatible overload that builds Schulz-Zimm cuts.</summary>
        Public Shared Function BuildCuts(ByVal basePolymer As BaseClasses.ConstantProperties,
                                         ByVal Mn As Double, ByVal PDI As Double, ByVal N As Integer,
                                         ByRef z As Double()) As List(Of BaseClasses.ConstantProperties)
            Return BuildCuts(basePolymer, Mn, PDI, N, PolymerDistribution.SchulzZimm, z)
        End Function

    End Class

End Namespace
