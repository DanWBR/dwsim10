Imports DotNumerics.LinearAlgebra

Namespace Polymers

    ''' <summary>
    ''' Discretizes a polymer molar-mass distribution into pseudo-components, so a polydisperse polymer can
    ''' be modelled with an equation of state (PC-SAFT) as a mixture of cuts of the same chemistry and
    ''' different molar mass. The cuts share the base compound's CAS, so the equation of state reuses the
    ''' same segment parameters (m/M, sigma, epsilon) and only the molar mass, hence the segment number
    ''' m = (m/M) * M, differs between cuts.
    ''' </summary>
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
        ''' Builds N pseudo-component compounds by cloning a base polymer, one per Schulz-Zimm cut, each with
        ''' its own molar mass and a distinguishing name and sharing the base CAS so the equation of state
        ''' reuses its parameters. Returns the compounds; the matching relative mole fractions z(k) come back
        ''' in <paramref name="z"/> and are scaled by the total polymer mole fraction when setting the feed.
        ''' </summary>
        Public Shared Function BuildCuts(ByVal basePolymer As BaseClasses.ConstantProperties,
                                         ByVal Mn As Double, ByVal PDI As Double, ByVal N As Integer,
                                         ByRef z As Double()) As List(Of BaseClasses.ConstantProperties)

            Dim M As Double() = Nothing
            SchulzZimmCuts(Mn, PDI, N, M, z)

            Dim cuts As New List(Of BaseClasses.ConstantProperties)
            For i As Integer = 0 To M.Length - 1
                Dim c = DirectCast(basePolymer.Clone(), BaseClasses.ConstantProperties)
                c.Molar_Weight = M(i)
                c.Name = basePolymer.Name & " (M=" & CInt(M(i)).ToString() & ")"
                cuts.Add(c)
            Next
            Return cuts

        End Function

    End Class

End Namespace
