'    Gel (Trommsdorff-Norrish) and glass effect for free-radical polymerization
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

Namespace Polymers

    Public Enum GelModelType
        None = 0
        Exponential = 1     ' g = exp(-(c1*X + c2*X^2 + c3*X^3))
    End Enum

    ''' <summary>
    ''' Diffusion-limited rate-constant reduction at high conversion. As monomer converts and the medium
    ''' thickens, chain termination becomes diffusion controlled and its rate constant collapses (the gel or
    ''' Trommsdorff-Norrish effect, driving auto-acceleration); near vitrification propagation slows too (the
    ''' glass effect). Both are represented as multiplicative factors g(X) in (0,1] on the base Arrhenius
    ''' constants, with the empirical exponential-polynomial form g = exp(-(c1*X + c2*X^2 + c3*X^3)) used for
    ''' auto-acceleration correlations (e.g. Ross-Laurence, Balke-Hamielec). The default model is None, which
    ''' returns a factor of exactly 1 so the kinetics are unchanged.
    ''' </summary>
    Public Class GelEffect

        Public ModelType As GelModelType = GelModelType.None

        ' termination (gel / Trommsdorff) coefficients
        Public GtC1 As Double = 0.0
        Public GtC2 As Double = 0.0
        Public GtC3 As Double = 0.0

        ' propagation (glass / vitrification) coefficients
        Public GpC1 As Double = 0.0
        Public GpC2 As Double = 0.0
        Public GpC3 As Double = 0.0

        Public ReadOnly Property IsActive As Boolean
            Get
                Return ModelType <> GelModelType.None
            End Get
        End Property

        ''' <summary>Factor on the termination rate constant, kt(X) = kt0 * g_t(X); 1 at zero conversion.</summary>
        Public Function TerminationFactor(X As Double) As Double
            If ModelType = GelModelType.None Then Return 1.0
            Dim e = GtC1 * X + GtC2 * X * X + GtC3 * X * X * X
            Return Math.Exp(-Math.Max(0.0, e))   ' clamp so the factor never exceeds 1 (a reduction only)
        End Function

        ''' <summary>Factor on the propagation rate constant, kp(X) = kp0 * g_p(X); 1 at zero conversion.</summary>
        Public Function PropagationFactor(X As Double) As Double
            If ModelType = GelModelType.None Then Return 1.0
            Dim e = GpC1 * X + GpC2 * X * X + GpC3 * X * X * X
            Return Math.Exp(-Math.Max(0.0, e))
        End Function

    End Class

End Namespace
