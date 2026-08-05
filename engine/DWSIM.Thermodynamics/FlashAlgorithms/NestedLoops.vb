'    DWSIM Nested Loops Flash Algorithms
'    Copyright 2010-2026 Daniel Wagner O. de Medeiros, Gregor Reichert
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

Imports System.Globalization
Imports System.Math
Imports DWSIM.MathOps.MathEx
Imports DWSIM.MathOps.MathEx.BrentOpt
Imports DWSIM.SharedClasses
Imports IronPython.Runtime.Operations
Imports MathNet.Numerics
Imports System.Linq
Imports DWSIM.ExtensionMethods

Namespace PropertyPackages.Auxiliary.FlashAlgorithms

    ''' <summary>
    ''' The Flash algorithms in this class are based on the Nested Loops approach to solve equilibrium calculations.
    ''' </summary>
    ''' <remarks></remarks>
    <System.Serializable()> Public Class NestedLoops

        Inherits FlashAlgorithm

        Protected etol As Double = 0.000001
        Protected itol As Double = 0.000001
        Protected maxit_i As Integer = 100
        Protected maxit_e As Integer = 100
        Protected dampingfactor As Double = 1.0
        Dim Hv0, Hvid, Hlid, Hf, Hv, Hl As Double
        Dim Sv0, Svid, Slid, Sf, Sv, Sl As Double

        Protected CalculatingAzeotrope As Boolean = False

        Public DisableParallelCalcs As Boolean = False

        Public Overrides ReadOnly Property MobileCompatible As Boolean
            Get
                Return True
            End Get
        End Property

        Public Property CalledFromSLE As Boolean = False

        Public Property LimitVaporFraction As Boolean = True

        Public PTFlashFunction As Func(Of Double(), Double, Double, PropertyPackages.PropertyPackage, Boolean, Double(), Object)

        Sub New()
            MyBase.New()
            Order = 1
        End Sub

        Public Overrides ReadOnly Property AlgoType As Interfaces.Enums.FlashMethod
            Get
                Return Interfaces.Enums.FlashMethod.Nested_Loops_VLE
            End Get
        End Property

        Public Overrides ReadOnly Property Description As String
            Get
                If GlobalSettings.Settings.CurrentCulture = "pt-BR" Then
                    Return "Algoritmo Flash para equil�brio L�quido-Vapor, baseado na equa��o de Rachford e Rice."
                Else
                    Return "Flash Algorithm for Vapor-Liquid Equilibria based on the Rachford-Rice VLE equations."
                End If
            End Get
        End Property

        Public Overrides ReadOnly Property Name As String
            Get
                Return "Nested Loops (VLE)"
            End Get
        End Property

        Public Overrides Function Flash_PT(ByVal Vz As Double(), ByVal P As Double, ByVal T As Double, ByVal PP As PropertyPackages.PropertyPackage, Optional ByVal ReuseKI As Boolean = False, Optional ByVal PrevKi As Double() = Nothing) As Object

            Dim result As Object()

            Dim estimate As Interfaces.IConvergenceHelperResponse = Nothing

            If Settings.AIAssistedConvergenceLevel = Settings.AIAssistedConvergenceMode.Provide_Initial_Estimates Or
                    Settings.AIAssistedConvergenceLevel = Settings.AIAssistedConvergenceMode.Provide_Initial_Estimates_and_Solutions Then
                estimate = DWSIM.SharedClasses.AI.ConvergenceAssistant.SolutionProvider?.GetSolutionEstimate(
                       New DWSIM.AI.ConvergenceAssistant.Classes.ConvergenceHelperRequest With {
                           .CompoundNames = PP.RET_VNAMES(),
                           .NumberOfCompounds = Vz.Count,
                           .MixtureMolarFlows = Vz,
                           .ModelName = PP.ComponentName,
                           .Pressure = P,
                           .Temperature = T,
                           .RequestType = Interfaces.ConvergenceHelperRequestType.PTFlash
                       })
            End If

            Dim calcex As Exception

            Try

                If estimate IsNot Nothing And (Settings.AIAssistedConvergenceLevel = Settings.AIAssistedConvergenceMode.Provide_Initial_Estimates Or
                    Settings.AIAssistedConvergenceLevel = Settings.AIAssistedConvergenceMode.Provide_Initial_Estimates_and_Solutions) Then

                    result = Flash_PT_1(Vz, P, T, PP, ReuseKI, PrevKi, estimate.VaporMolarFlows.Sum())

                Else

                    result = Flash_PT_1(Vz, P, T, PP, ReuseKI, PrevKi)

                End If

                Return result

            Catch ex As Exception

                calcex = ex

            End Try

            If Settings.AIAssistedConvergenceLevel = Settings.AIAssistedConvergenceMode.Provide_Initial_Estimates_2Pass Or
                        Settings.AIAssistedConvergenceLevel = Settings.AIAssistedConvergenceMode.Provide_Initial_Estimates_and_Solutions_2Pass Then

                estimate = DWSIM.SharedClasses.AI.ConvergenceAssistant.SolutionProvider?.GetSolutionEstimate(
                       New DWSIM.AI.ConvergenceAssistant.Classes.ConvergenceHelperRequest With {
                           .CompoundNames = PP.RET_VNAMES(),
                           .NumberOfCompounds = Vz.Count,
                           .MixtureMolarFlows = Vz,
                           .ModelName = PP.ComponentName,
                           .Pressure = P,
                           .Temperature = T,
                           .RequestType = Interfaces.ConvergenceHelperRequestType.PTFlash
                       })

                If estimate IsNot Nothing Then

                    Try

                        result = Flash_PT_1(Vz, P, T, PP, ReuseKI, PrevKi, estimate.VaporMolarFlows.Sum())

                    Catch ex As Exception

                        If Settings.AIAssistedConvergenceLevel = Settings.AIAssistedConvergenceMode.Provide_Initial_Estimates_and_Solutions Or
                        Settings.AIAssistedConvergenceLevel = Settings.AIAssistedConvergenceMode.Provide_Solutions Then

                            If estimate IsNot Nothing Then

                                Return New Object() {estimate.Liquid1MolarFlows.Sum,
                            estimate.VaporMolarFlows.Sum,
                            estimate.Liquid1MolarFlows.NormalizeY(),
                            estimate.VaporMolarFlows.NormalizeY(),
                            0, 0.0#, PP.RET_NullVector, 0.0#, PP.RET_NullVector, estimate.KValuesVL1}

                            Else

                                Throw New Exception(String.Format("{0}: Unable to calculate PT Flash with P = {1} and T = {2}, molar fractions = {3}",
                                    PP.ComponentName, P, T, Vz.ToArrayString(PP.RET_VNAMES(), "G3")))

                            End If

                        End If

                    End Try

                Else

                    Throw calcex

                End If

            Else

                Throw calcex

            End If

            Return Nothing

        End Function

        Public Function Flash_PT_1(ByVal Vz As Double(), ByVal P As Double, ByVal T As Double, ByVal PP As PropertyPackages.PropertyPackage, Optional ByVal ReuseKI As Boolean = False, Optional ByVal PrevKi As Double() = Nothing, Optional ByVal Vest As Double = -1) As Object

            Dim IObj As Inspector.InspectorItem = Inspector.Host.GetNewInspectorItem()

            Inspector.Host.CheckAndAdd(IObj, "", "Flash_PT", Name & " (PT Flash)", "Pressure-Temperature Flash Algorithm Routine", True)

            IObj?.Paragraphs.Add("This routine tries to find the compositions of a liquid and a vapor phase at equilibrium by solving the Rachford-Rice equation using a newton convergence approach.")

            IObj?.Paragraphs.Add("The Rachford-Rice equation is")

            IObj?.Paragraphs.Add("<math>\sum_i\frac{z_i \, (K_i - 1)}{1 + \beta \, (K_i - 1)}= 0</math>")

            IObj?.Paragraphs.Add("where:")

            IObj?.Paragraphs.Add("<math_inline>z_{i}</math_inline> is the mole fraction of component i in the feed liquid (assumed to be known);")
            IObj?.Paragraphs.Add("<math_inline>\beta</math_inline> is the fraction of feed that is vaporised;")
            IObj?.Paragraphs.Add("<math_inline>K_{i}</math_inline> is the equilibrium constant of component i.")

            IObj?.Paragraphs.Add("The equilibrium constants K<sub>i</sub> are in general functions of many parameters, though the most important is arguably temperature; they are defined as:")

            IObj?.Paragraphs.Add("<math>y_i = K_i \, x_i</math>")

            IObj?.Paragraphs.Add("where:")

            IObj?.Paragraphs.Add("<math_inline>x_i</math_inline> is the mole fraction of component i in liquid phase;")
            IObj?.Paragraphs.Add("<math_inline>y_i</math_inline> is the mole fraction of component i in gas phase.")

            IObj?.Paragraphs.Add("Once the Rachford-Rice equation has been solved for <math_inline>\beta</math_inline>, the compositions x<sub>i</sub> and y<sub>i</sub> can be immediately calculated as:")

            IObj?.Paragraphs.Add("<math>x_i =\frac{z_i}{1+\beta(K_i-1)}\\y_i=K_i\,x_i</math>")

            IObj?.Paragraphs.Add("The Rachford - Rice equation can have multiple solutions for <math_inline>\beta</math_inline>, at most one of which guarantees that all <math_inline>x_i</math_inline> and <math_inline>y_i</math_inline> will be positive. In particular, if there is only one <math_inline>\beta</math_inline> for which:")
            IObj?.Paragraphs.Add("<math>\frac{1}{1-K_\text{max}}=\beta_\text{min}<\beta<\beta_\text{max}=\frac{1}{1-K_\text{min}}</math>")
            IObj?.Paragraphs.Add("then that <math_inline>\beta</math_inline> is the solution; if there are multiple  such <math_inline>\beta</math_inline>s, it means that either <math_inline>K_{max}<1</math_inline> or <math_inline>K_{min}>1</math_inline>, indicating respectively that no gas phase can be sustained (and therefore <math_inline>\beta=0</math_inline>) or conversely that no liquid phase can exist (and therefore <math_inline>\beta=1</math_inline>).")

            IObj?.Paragraphs.Add("DWSIM initializes the current calculation with ideal K-values estimated from vapor pressure data for each compound, or by using previously calculated values from an earlier solution.")

            Dim i, n, ecount As Integer
            Dim Pb, Pd, Pmin, Pmax, Px As Double
            Dim d1, d2 As Date, dt As TimeSpan
            Dim L, V, F As Double

            d1 = Date.Now

            etol = Me.FlashSettings(Interfaces.Enums.FlashSetting.PTFlash_External_Loop_Tolerance).ToDoubleFromInvariant()
            maxit_e = Me.FlashSettings(Interfaces.Enums.FlashSetting.PTFlash_Maximum_Number_Of_External_Iterations)
            itol = Me.FlashSettings(Interfaces.Enums.FlashSetting.PTFlash_Internal_Loop_Tolerance).ToDoubleFromInvariant()
            maxit_i = Me.FlashSettings(Interfaces.Enums.FlashSetting.PTFlash_Maximum_Number_Of_Internal_Iterations)
            dampingfactor = Me.FlashSettings(Interfaces.Enums.FlashSetting.PTFlash_DampingFactor).ToDoubleFromInvariant()

            n = Vz.Length - 1

            Dim Vn(n) As String, Vx(n), Vy(n), Vx_ant(n), Vy_ant(n), Vp(n), Ki(n), Ki_ant(n), fi(n) As Double
            Dim Vx0(n), Vy0(n), Ki0(n) As Double
            Dim VPc(n), VTc(n), Vw(n) As Double

            VPc = PP.RET_VPC()
            VTc = PP.RET_VTC()
            Vw = PP.RET_VW()
            Vn = PP.RET_VNAMES()

            Array.Copy(Vz, fi, n + 1)

            'Calculate Ki`s

            Vp = PP.RET_VPVAP(T)

            If Not ReuseKI Then
                Ki = Vp.MultiplyConstY(1 / P)
                For i = 0 To n
                    If Double.IsNaN(Ki(i)) Or Double.IsInfinity(Ki(i)) Then Ki(i) = 1.0E+20
                    If Ki(i) = 0.0 Then Ki(i) = 1.0E-20
                Next
            Else
                For i = 0 To n
                    Ki(i) = PrevKi(i)
                    If Double.IsNaN(Ki(i)) Or Double.IsInfinity(Ki(i)) Then Ki(i) = 1.0E+20
                    If Ki(i) = 0.0 Then Ki(i) = 1.0E-20
                Next
            End If

            IObj?.Paragraphs.Add(String.Format("<h2>Input Parameters</h2>"))

            IObj?.Paragraphs.Add(String.Format("Temperature: {0} K", T))
            IObj?.Paragraphs.Add(String.Format("Pressure: {0} Pa", P))
            IObj?.Paragraphs.Add(String.Format("Compounds: {0}", PP.RET_VNAMES.ToMathArrayString))
            IObj?.Paragraphs.Add(String.Format("Mole Fractions: {0}", Vz.ToMathArrayString))
            IObj?.Paragraphs.Add(String.Format("Initial estimates for K: {0}", Ki.ToMathArrayString))

            'Estimate V

            If T > MathEx.Common.Max(VTc, Vz) Then
                Vy = Vz
                Vx = Vy.DivideY(Ki).NormalizeY
                Vx = Vx.ReplaceInvalidsWithZeroes()
                V = 1
                L = 0
                d2 = Date.Now
                GoTo out
            End If

            i = 0
            Px = 0
            Do
                If Vp(i) <> 0.0# Then Px = Px + (Vz(i) / Vp(i))
                i = i + 1
            Loop Until i = n + 1
            Px = 1 / Px
            Pmin = Px
            i = 0
            Px = 0
            Do
                Px = Px + Vz(i) * Vp(i)
                i = i + 1
            Loop Until i = n + 1
            Pmax = Px
            Pb = Pmax
            Pd = Pmin

            If Abs(Pb - Pd) / Pb < 0.0000001 And Vz.Max > 0.99 Then
                'one comp only
                Px = Vp.MultiplyY(Vz).Sum
                d2 = Date.Now
                If Px <= P Then
                    L = 1
                    V = 0
                    Vx = Vz
                    Vy = Vx.MultiplyY(Ki).NormalizeY()
                    Vy = Vy.ReplaceInvalidsWithZeroes()
                    GoTo out
                Else
                    L = 0
                    V = 1
                    Vy = Vz
                    Vx = Vy.DivideY(Ki).NormalizeY()
                    Vx = Vx.ReplaceInvalidsWithZeroes()
                    GoTo out
                End If
            End If

            If Vp.Max < P And Vp.Min > 0 Then

                'all liquid
                L = 1
                V = 0
                Vx = Vz
                Vy = Vx.MultiplyY(Ki).NormalizeY()
                Vy = Vy.ReplaceInvalidsWithZeroes()
                GoTo out

            ElseIf Vp.Min > P And Vp.Min > 0 Then

                'all vapor
                L = 0
                V = 1
                Vy = Vz
                Vx = Vy.DivideY(Ki).NormalizeY()
                Vx = Vx.ReplaceInvalidsWithZeroes()
                GoTo out

            End If

            Dim Vmin, Vmax, g As Double
            Vmin = 1.0#
            Vmax = 0.0#
            For i = 0 To n
                If (Ki(i) * Vz(i) - 1) / (Ki(i) - 1) < Vmin Then Vmin = (Ki(i) * Vz(i) - 1) / (Ki(i) - 1)
                If (1 - Vz(i)) / (1 - Ki(i)) > Vmax Then Vmax = (1 - Vz(i)) / (1 - Ki(i))
            Next

            If Vmin < 0.0# Then Vmin = 0.0#
            If Vmin = 1.0# Then Vmin = 0.0#
            If Vmax = 0.0# Then Vmax = 1.0#
            If Vmax > 1.0# Then Vmax = 1.0#

            If Vest >= 0 Then
                V = Vest
            Else
                V = (Vmin + Vmax) / 2
            End If

            g = 0.0#
            For i = 0 To n
                g += Vz(i) * (Ki(i) - 1) / (V + (1 - V) * Ki(i))
            Next

            If g > 0 Then Vmin = V Else Vmax = V

            V = Brent.BrentOpt3(Vmin, Vmax, 10, 0.001, 100,
                           Function(Vb)
                               Return Vz.MultiplyY(Ki.AddConstY(-1).DivideY(Ki.AddConstY(-1).MultiplyConstY(Vb).AddConstY(1))).SumY
                           End Function)

            If V > 1.0 Or V < 0.0 Then V = Vmin + (Vmax - Vmin) / 2

            L = 1 - V

            IObj?.Paragraphs.Add(String.Format("Initial estimate for V: {0}", V))
            IObj?.Paragraphs.Add(String.Format("Initial estimate for L (1-V): {0}", L))

            If n = 0 Then
                If Vp(0) <= P Then
                    V = 0.0#
                Else
                    V = 1.0#
                End If
            End If

            Vy = Vz.MultiplyY(Ki).DivideY(Ki.AddConstY(-1).MultiplyConstY(V).AddConstY(1)).NormalizeY
            Vx = Vy.DivideY(Ki).NormalizeY

            Array.Copy(Ki, Ki0, n + 1)
            Array.Copy(Vx, Vx0, n + 1)
            Array.Copy(Vy, Vy0, n + 1)

            Dim r1 As Object()

            'Return New Object() {V, Vx, Vy, Ki, F, ecount, overshoot}

            r1 = ConvergeVF(IObj, V, Vz, Vx0, Vy0, Ki0, P, T, PP, 0)

            Dim failed = False


            If r1(6) = True And Math.Abs(Vmax - Vmin) > 0.01 Then
                Try
                    r1 = ConvergeVF(IObj, V, Vz, Vx0, Vy0, Ki0, P, T, PP, 1.0)
                Catch ex As Exception
                    failed = True
                End Try
            End If

            If r1(6) = True And Math.Abs(Vmax - Vmin) > 0.01 Or failed Then
                r1 = ConvergeVF(IObj, V, Vz, r1(1), r1(2), r1(3), P, T, PP, 1.0)
            End If

            V = r1(0)
            L = 1 - V
            Vx = r1(1)
            Vy = r1(2)
            Ki = r1(3)
            F = r1(4)
            ecount = r1(5)

            If V <= 0.0# Then
                V = 0.0#
                L = 1.0#
                Vx = Vz
                Vy = Ki.MultiplyY(Vx).NormalizeY
            End If
            If V >= 1.0# Then
                V = 1.0#
                L = 0.0#
                Vy = Vz
                Vx = Vy.DivideY(Ki).NormalizeY
            End If
            If PP.AUX_CheckTrivial(Ki, 0.1) And L > 0.0 And V > 0.0 Then
                Dim gl = PP.DW_CalcGibbsEnergy(Vx, T, P, "L")
                Dim gv = PP.DW_CalcGibbsEnergy(Vy, T, P, "V")
                If Math.Abs(gl / gv - 1.0) < 0.01 Then
                    Dim zl = PP.AUX_Z(Vx, T, P, Interfaces.Enums.PhaseName.Liquid)
                    Dim zv = PP.AUX_Z(Vy, T, P, Interfaces.Enums.PhaseName.Vapor)
                    Dim zc = PP.RET_VZC().MultiplyY(Vz).Sum
                    If zl > zc And zv > zc Then
                        V = 1.0#
                        L = 0.0#
                        Vy = Vz
                        Vx = Vy.DivideY(Ki).NormalizeY
                    Else
                        V = 0.0#
                        L = 1.0#
                        Vx = Vz
                        Vy = Ki.MultiplyY(Vx).NormalizeY
                    End If
                End If
            End If

            d2 = Date.Now

            dt = d2 - d1

out:        WriteDebugInfo("PT Flash [NL]: Converged in " & ecount & " iterations. Time taken: " & dt.TotalMilliseconds & " ms. Error function value: " & F)

            IObj?.Paragraphs.Add("The algorithm converged in " & ecount & " iterations. Time taken: " & dt.TotalMilliseconds & " ms. Error function value: " & F)

            IObj?.Paragraphs.Add(String.Format("Final converged values for K: {0}", Ki.ToMathArrayString))

            IObj?.Close()

            If SharedClasses.AI.ConvergenceAssistant.Manager IsNot Nothing Then
                SharedClasses.AI.ConvergenceAssistant.Manager?.StoreData(
                        New AI.ConvergenceAssistant.Classes.ConvergenceHelperTrainingData With {
                        .CompoundNames = PP.RET_VNAMES(), .ModelName = PP.ComponentName, .NumberOfCompounds = Ki.Count,
                        .Temperature = T.ToString("F4", CultureInfo.InvariantCulture),
                        .Pressure = P.ToString("F4", CultureInfo.InvariantCulture),
                        .VaporMolarFraction = V.ToString("F4", CultureInfo.InvariantCulture),
                        .Liquid1MolarFlows = Vx.MultiplyConstY(L).ToString("F4"),
                        .VaporMolarFlows = Vy.MultiplyConstY(V).ToString("F4"),
                        .KValuesVL1 = Ki.ToString("F4"),
                        .MixtureMolarFlows = Vz.ToString("F4"),
                        .RequestType = Interfaces.ConvergenceHelperRequestType.PTFlash})
            End If

            If PP.ImmiscibleLiquids.Count > 0 Then

                Dim immscheck As Object() = ProcessImmiscibleLiquids(PP, L, 0.0, Vx, PP.RET_NullVector())

                Dim L1 = DirectCast(immscheck(0), Double)
                Dim Vx1 = DirectCast(immscheck(1), Double())
                Dim L2 = DirectCast(immscheck(2), Double)
                Dim Vx2 = DirectCast(immscheck(3), Double())

                Return New Object() {L1, V, Vx1, Vy, ecount, L2, Vx2, 0.0#, PP.RET_NullVector(), Ki}

            Else

                Return New Object() {L, V, Vx, Vy, ecount, 0.0#, PP.RET_NullVector(), 0.0#, PP.RET_NullVector(), Ki}

            End If

        End Function

        Protected Function ConvergeVF(IObj As InspectorItem, V As Double, Vz As Double(), Vx As Double(), Vy As Double(), Ki As Double(), P As Double, T As Double, PP As PropertyPackage, damplevel As Integer) As Object()

            Dim n As Integer = Vz.Length - 1

            Dim Vn(n) As String, Vx_ant(n), Vy_ant(n), Vp(n), Ki_ant(n), fi(n) As Double

            Dim ecount As Integer = 0
            Dim F, Vant, dF, e1, e2, e3 As Double
            Dim overshoot As Boolean = False
            Dim dfac As Double = dampingfactor

            IObj?.Paragraphs.Add(String.Format("Initial estimates for y: {0}", Vy.ToMathArrayString))
            IObj?.Paragraphs.Add(String.Format("Initial estimates for x: {0}", Vx.ToMathArrayString))

            Do

                IObj?.SetCurrent()

                Dim IObj2 As Inspector.InspectorItem = Inspector.Host.GetNewInspectorItem()

                Inspector.Host.CheckAndAdd(IObj2, "", "Flash_PT", "PT Flash Newton Iteration", "Pressure-Temperature Flash Algorithm Convergence Iteration Step")

                IObj2?.Paragraphs.Add(String.Format("This is the Newton convergence loop iteration #{0}. DWSIM will use the current values of y and x to calculate fugacity coefficients and update K using the Property Package rigorous models.", ecount))

                IObj2?.SetCurrent()

                Array.Copy(Ki, Ki_ant, n + 1)

                Ki = PP.DW_CalcKvalue(Vx, Vy, T, P)

                IObj2?.Paragraphs.Add(String.Format("K values where updated. Current values: {0}", Ki.ToMathArrayString))

                Array.Copy(Vy, Vy_ant, n + 1)
                Array.Copy(Vx, Vx_ant, n + 1)

                If V = 1.0# Then
                    Vy = Vz
                    Vx = Vy.DivideY(Ki).NormalizeY
                ElseIf V = 0.0# Then
                    Vx = Vz
                    Vy = Vx.MultiplyY(Ki).NormalizeY
                Else
                    Vy = Vz.MultiplyY(Ki).DivideY(Ki.AddConstY(-1).MultiplyConstY(V).AddConstY(1)).NormalizeY
                    Vx = Vy.DivideY(Ki).NormalizeY
                End If

                IObj2?.Paragraphs.Add(String.Format("y values (vapor phase composition) where updated. Current values: {0}", Vy.ToMathArrayString))
                IObj2?.Paragraphs.Add(String.Format("x values (liquid phase composition) where updated. Current values: {0}", Vx.ToMathArrayString))

                e1 = Vx.SubtractY(Vx_ant).AbsSumY
                e2 = Vy.SubtractY(Vy_ant).AbsSumY

                e3 = (V - Vant)

                IObj2?.Paragraphs.Add(String.Format("Current Vapor Fraction (<math_inline>\beta</math_inline>) error: {0}", e3))

                If Double.IsNaN(e1 + e2) Then

                    Dim ex As New Exception(Calculator.GetLocalString("PropPack_FlashError") & String.Format(" (T = {0} K, P = {1} Pa, MoleFracs = {2})", T.ToString("N2"), P.ToString("N2"), Vz.ToArrayString()))
                    ex.Data.Add("DetailedDescription", "The Flash Algorithm was unable to converge to a solution.")
                    ex.Data.Add("UserAction", "Try another Property Package and/or Flash Algorithm.")
                    Throw ex

                ElseIf Math.Abs(e3) < 0.000001 And ecount > 0 Then

                    Exit Do

                Else

                    Vant = V

                    F = Vz.MultiplyY(Ki.AddConstY(-1).DivideY(Ki.AddConstY(-1).MultiplyConstY(V).AddConstY(1))).SumY
                    dF = Vz.NegateY.MultiplyY(Ki.AddConstY(-1).MultiplyY(Ki.AddConstY(-1)).DivideY(Ki.AddConstY(-1).MultiplyConstY(V).AddConstY(1)).DivideY(Ki.AddConstY(-1).MultiplyConstY(V).AddConstY(1))).SumY

                    IObj2?.Paragraphs.Add(String.Format("Current value of the Rachford-Rice error function: {0}", F))

                    ' Check R-R error first (primary convergence criterion)
                    If Abs(F) < etol Then Exit Do

                    ' Secondary criterion: V stabilized
                    If Math.Abs(e3) < 0.000001 And ecount > 0 Then Exit Do

                    If damplevel = 1 Then
                        dfac = (ecount + 1) * 0.2
                        If dfac > 1.0 Then dfac = 1.0
                        If -F / dF * dfac + Vant > 1.0 Or -F / dF * dfac + Vant < 0.0 Then
                            dfac /= 10
                        End If
                    ElseIf damplevel = 2 Then
                        dfac = (ecount + 1) * 0.05
                        If dfac > 1.0 Then dfac = 1.0
                        If -F / dF * dfac + Vant > 1.0 Or -F / dF * dfac + Vant < 0.0 Then
                            dfac /= 50
                        End If
                    End If

                    V = -F / dF * dfac + Vant

                    If LimitVaporFraction Then
                        If V < 0.0 Then
                            overshoot = True
                            V = 0.0
                            Exit Do
                        End If
                        If V > 1.0 Then
                            overshoot = True
                            V = 1.0
                            Exit Do
                        End If
                    End If

                    IObj2?.Paragraphs.Add(String.Format("Updated Vapor Fraction (<math_inline>\beta</math_inline>) value: {0}", V))

                End If

                ecount += 1

                If Double.IsNaN(V) Then
                    Dim ex As New Exception(Calculator.GetLocalString("PropPack_FlashTPVapFracError") & String.Format(" (T = {0} K, P = {1} Pa, MoleFracs = {2})", T.ToString("N2"), P.ToString("N2"), Vz.ToArrayString()))
                    ex.Data.Add("DetailedDescription", "The Flash Algorithm was unable to converge to a solution.")
                    ex.Data.Add("UserAction", "Try another Property Package and/or Flash Algorithm.")
                    Throw ex
                End If
                If ecount > maxit_e Then
                    Dim ex As New Exception(Calculator.GetLocalString("PropPack_FlashMaxIt2") & String.Format(" (T = {0} K, P = {1} Pa, MoleFracs = {2})", T.ToString("N2"), P.ToString("N2"), Vz.ToArrayString()))
                    ex.Data.Add("DetailedDescription", "The Flash Algorithm was unable to converge to a solution.")
                    ex.Data.Add("UserAction", "Try another Property Package and/or Flash Algorithm.")
                    Throw ex
                End If

                WriteDebugInfo("PT Flash [NL]: Iteration #" & ecount & ", VF = " & V)

                If Math.IEEERemainder(ecount, 5) > 0.0 Then
                    If Not PP.CurrentMaterialStream.Flowsheet Is Nothing Then
                        If Not PP.CurrentMaterialStream.Flowsheet Is Nothing Then
                            PP.CurrentMaterialStream.Flowsheet.CheckStatus()
                        End If
                    End If
                End If

                IObj2?.Close()

            Loop

            Return New Object() {V, Vx, Vy, Ki, F, ecount, overshoot}

        End Function

        Private Function ConvergeVF2(Vmin As Double, Vmax As Double, V As Double, Vz As Double(), Vx As Double(), Vy As Double(), Ki As Double(), P As Double, T As Double, PP As PropertyPackage) As Object()

            Dim F As Double = 0.0

            Dim EvalF As Func(Of Double, Double) = Function(Vvar)

                                                       V = Vvar

                                                       Ki = PP.DW_CalcKvalue(Vx, Vy, T, P)

                                                       If V = 1.0# Then
                                                           Vy = Vz
                                                           Vx = Vy.DivideY(Ki).NormalizeY
                                                       ElseIf V = 0.0# Then
                                                           Vx = Vz
                                                           Vy = Vx.MultiplyY(Ki).NormalizeY
                                                       Else
                                                           Vy = Vz.MultiplyY(Ki).DivideY(Ki.AddConstY(-1).MultiplyConstY(V).AddConstY(1)).NormalizeY
                                                           Vx = Vy.DivideY(Ki).NormalizeY
                                                       End If

                                                       F = Vz.MultiplyY(Ki.AddConstY(-1).DivideY(Ki.AddConstY(-1).MultiplyConstY(V).AddConstY(1))).SumY

                                                       Return F ^ 2

                                                   End Function

            Dim bt As New BrentOpt.BrentMinimize

            V = bt.brentoptimize2(Vmin, Vmax, 1.0E-18, Function(x)
                                                           If x >= 0 And x <= 1 Then
                                                               Return EvalF.Invoke(x)
                                                           Else
                                                               Return 10000000000.0
                                                           End If
                                                       End Function)

            Return New Object() {V, Vx, Vy, Ki, F, 0.0}

        End Function

        Public Overrides Function Flash_PH(ByVal Vz As Double(), ByVal P As Double, ByVal H As Double, ByVal Tref As Double, ByVal PP As PropertyPackages.PropertyPackage, Optional ByVal ReuseKI As Boolean = False, Optional ByVal PrevKi As Double() = Nothing) As Object

            Dim result As Object()

            Dim estimate As Interfaces.IConvergenceHelperResponse = Nothing

            If Settings.AIAssistedConvergenceLevel = Settings.AIAssistedConvergenceMode.Provide_Initial_Estimates Or
                    Settings.AIAssistedConvergenceLevel = Settings.AIAssistedConvergenceMode.Provide_Initial_Estimates_and_Solutions Then

                estimate = DWSIM.SharedClasses.AI.ConvergenceAssistant.SolutionProvider?.GetSolutionEstimate(
                   New DWSIM.AI.ConvergenceAssistant.Classes.ConvergenceHelperRequest With {
                   .CompoundNames = PP.RET_VNAMES(),
                   .NumberOfCompounds = Vz.Count,
                   .MixtureMolarFlows = Vz,
                   .ModelName = PP.ComponentName,
                   .Pressure = P,
                   .MassEnthalpy = H,
                   .Temperature = Tref,
                   .RequestType = Interfaces.ConvergenceHelperRequestType.PHFlash
               })

            End If

            Dim calcex As Exception

            Try

                If estimate IsNot Nothing And (Settings.AIAssistedConvergenceLevel = Settings.AIAssistedConvergenceMode.Provide_Initial_Estimates Or
                    Settings.AIAssistedConvergenceLevel = Settings.AIAssistedConvergenceMode.Provide_Initial_Estimates_and_Solutions) Then

                    result = Flash_PH_0(Vz, P, H, estimate.Temperature, PP, True, estimate.KValuesVL1)

                Else

                    result = Flash_PH_0(Vz, P, H, Tref, PP, ReuseKI, PrevKi)

                End If

                Return result

            Catch ex As Exception

                calcex = ex

            End Try

            If Settings.AIAssistedConvergenceLevel = Settings.AIAssistedConvergenceMode.Provide_Initial_Estimates_2Pass Or
                        Settings.AIAssistedConvergenceLevel = Settings.AIAssistedConvergenceMode.Provide_Initial_Estimates_and_Solutions_2Pass Then

                estimate = DWSIM.SharedClasses.AI.ConvergenceAssistant.SolutionProvider?.GetSolutionEstimate(
                               New DWSIM.AI.ConvergenceAssistant.Classes.ConvergenceHelperRequest With {
                               .CompoundNames = PP.RET_VNAMES(),
                               .NumberOfCompounds = Vz.Count,
                               .MixtureMolarFlows = Vz,
                               .ModelName = PP.ComponentName,
                               .Pressure = P,
                               .MassEnthalpy = H,
                               .Temperature = Tref,
                               .RequestType = Interfaces.ConvergenceHelperRequestType.PHFlash
                           })

                If estimate IsNot Nothing Then

                    Try

                        result = Flash_PH_0(Vz, P, H, estimate.Temperature, PP, True, estimate.KValuesVL1)

                    Catch ex As Exception

                        If Settings.AIAssistedConvergenceLevel = Settings.AIAssistedConvergenceMode.Provide_Initial_Estimates_and_Solutions Or
                        Settings.AIAssistedConvergenceLevel = Settings.AIAssistedConvergenceMode.Provide_Solutions Then

                            If estimate IsNot Nothing Then

                                Return New Object() {estimate.Liquid1MolarFlows.Sum,
                                    estimate.VaporMolarFlows.Sum,
                                    estimate.Liquid1MolarFlows.NormalizeY(),
                                    estimate.VaporMolarFlows.NormalizeY(),
                                    estimate.Temperature, 0, estimate.KValuesVL1,
                                    0.0#, PP.RET_NullVector, 0.0#, PP.RET_NullVector}

                            Else

                                Throw New Exception(String.Format("{0}: Unable to calculate PH Flash with P = {1} and H = {2}, molar fractions = {3}",
                                    PP.ComponentName, P, H, Vz.ToArrayString(PP.RET_VNAMES(), "G3")))

                            End If

                        Else

                            Throw New Exception(String.Format("{0}: Unable to calculate PH Flash with P = {1} and H = {2}, molar fractions = {3}",
                                    PP.ComponentName, P, H, Vz.ToArrayString(PP.RET_VNAMES(), "G3")))

                        End If

                    End Try

                Else

                    Throw calcex

                End If

            Else

                Throw calcex

            End If

            Return Nothing

        End Function

        Public Function Flash_PH_0(ByVal Vz As Double(), ByVal P As Double, ByVal H As Double, ByVal Tref As Double, ByVal PP As PropertyPackages.PropertyPackage, Optional ByVal ReuseKI As Boolean = False, Optional ByVal PrevKi As Double() = Nothing) As Object

            Dim IObj As Inspector.InspectorItem = Inspector.Host.GetNewInspectorItem()

            Inspector.Host.CheckAndAdd(IObj, "", "Flash_PH", Name & " (PH Flash)", "Pressure-Enthalpy Flash Algorithm Routine")

            IObj?.Paragraphs.Add("The PH Flash calculates the equilibrium temperature and phase distribution given the mixture's pressure and overall enthalpy.")

            IObj?.SetCurrent()

            Dim FlashType As String = FlashSettings(Interfaces.Enums.FlashSetting.ForceEquilibriumCalculationType)
            Dim trigger As Boolean = False

            If FlashType = "Default" Or FlashType = "SVLE" Or FlashType = "SVLLE" Then
                Dim hres = PerformHeuristicsTest(Vz, Tref, P, PP)
                trigger = hres.SolidPhase
            End If

            If PP.ForcedSolids.Count > 0 Then trigger = False

            If Me.FlashSettings(Interfaces.Enums.FlashSetting.NL_FastMode) = False Or
                PP.AUX_IS_SINGLECOMP(Phase.Mixture) Or trigger Then

                IObj?.Paragraphs.Add("Using the normal version of the PH Flash Algorithm.")
                IObj?.Close()

                Dim haserror As Boolean = True
                Try
                    Return Flash_PH_2(Vz, P, H, Tref, PP, ReuseKI, PrevKi)
                    haserror = False
                Catch ex As Exception
                End Try
                If haserror Then
                    Return Flash_PH_1(Vz, P, H, Tref, PP, ReuseKI, PrevKi)
                End If

            Else

                IObj?.Paragraphs.Add("Using the fast version of the PH Flash Algorithm.")
                IObj?.Close()

                Return Flash_PH_1(Vz, P, H, Tref, PP, ReuseKI, PrevKi)

            End If

        End Function

        Public Overrides Function Flash_PS(ByVal Vz As Double(), ByVal P As Double, ByVal S As Double, ByVal Tref As Double, ByVal PP As PropertyPackages.PropertyPackage, Optional ByVal ReuseKI As Boolean = False, Optional ByVal PrevKi As Double() = Nothing) As Object

            Dim result As Object()

            Dim estimate As Interfaces.IConvergenceHelperResponse = Nothing

            If Settings.AIAssistedConvergenceLevel > 0 Then

                estimate = DWSIM.SharedClasses.AI.ConvergenceAssistant.SolutionProvider?.GetSolutionEstimate(
                   New DWSIM.AI.ConvergenceAssistant.Classes.ConvergenceHelperRequest With {
                   .CompoundNames = PP.RET_VNAMES(),
                   .NumberOfCompounds = Vz.Count,
                   .MixtureMolarFlows = Vz,
                   .ModelName = PP.ComponentName,
                   .Pressure = P,
                   .MassEntropy = S,
                   .Temperature = Tref,
                   .RequestType = Interfaces.ConvergenceHelperRequestType.PSFlash
               })

            End If

            Try

                If estimate IsNot Nothing And (Settings.AIAssistedConvergenceLevel = Settings.AIAssistedConvergenceMode.Provide_Initial_Estimates Or
                    Settings.AIAssistedConvergenceLevel = Settings.AIAssistedConvergenceMode.Provide_Initial_Estimates_and_Solutions) Then


                    result = Flash_PS_0(Vz, P, S, estimate.Temperature, PP, True, estimate.KValuesVL1)

                Else

                    result = Flash_PS_0(Vz, P, S, Tref, PP, ReuseKI, PrevKi)

                End If

                Return result

            Catch ex As Exception

                If Settings.AIAssistedConvergenceLevel = Settings.AIAssistedConvergenceMode.Provide_Initial_Estimates_and_Solutions Or
                        Settings.AIAssistedConvergenceLevel = Settings.AIAssistedConvergenceMode.Provide_Solutions Then

                    If estimate IsNot Nothing Then

                        Return New Object() {estimate.Liquid1MolarFlows.Sum,
                            estimate.VaporMolarFlows.Sum,
                            estimate.Liquid1MolarFlows.NormalizeY(),
                            estimate.VaporMolarFlows.NormalizeY(),
                            estimate.Temperature, 0, estimate.KValuesVL1,
                            0.0#, PP.RET_NullVector, 0.0#, PP.RET_NullVector}

                    Else

                        Throw New Exception(String.Format("{0}: Unable to calculate PS Flash with P = {1} and S = {2}, molar fractions = {3}",
                                    PP.ComponentName, P, S, Vz.ToArrayString(PP.RET_VNAMES(), "G3")))

                    End If

                Else

                    Throw New Exception(String.Format("{0}: Unable to calculate PS Flash with P = {1} and S = {2}, molar fractions = {3}",
                                    PP.ComponentName, P, S, Vz.ToArrayString(PP.RET_VNAMES(), "G3")))

                End If

            End Try

            Return Nothing

        End Function

        Public Function Flash_PS_0(ByVal Vz As Double(), ByVal P As Double, ByVal S As Double, ByVal Tref As Double, ByVal PP As PropertyPackages.PropertyPackage, Optional ByVal ReuseKI As Boolean = False, Optional ByVal PrevKi As Double() = Nothing) As Object

            Dim IObj As Inspector.InspectorItem = Inspector.Host.GetNewInspectorItem()

            Inspector.Host.CheckAndAdd(IObj, "", "Flash_PS", Name & " (PS Flash)", "Pressure-Entropy Flash Algorithm Routine")

            IObj?.Paragraphs.Add("The PS Flash calculates the equilibrium temperature and phase distribution given the mixture's pressure and overall entropy.")

            IObj?.SetCurrent()

            Dim FlashType As String = FlashSettings(Interfaces.Enums.FlashSetting.ForceEquilibriumCalculationType)
            Dim trigger As Boolean = False

            If FlashType = "Default" Or FlashType = "SVLE" Or FlashType = "SVLLE" Then
                Dim hres = PerformHeuristicsTest(Vz, Tref, P, PP)
                trigger = hres.SolidPhase
            End If

            If PP.ForcedSolids.Count > 0 Then trigger = False

            If Me.FlashSettings(Interfaces.Enums.FlashSetting.NL_FastMode) = False Or
                PP.AUX_IS_SINGLECOMP(Phase.Mixture) Or trigger Then
                IObj?.Paragraphs.Add("Using the normal version of the PS Flash Algorithm.")
                IObj?.Close()
                Return Flash_PS_2(Vz, P, S, Tref, PP, ReuseKI, PrevKi)
            Else
                IObj?.Paragraphs.Add("Using the fast version of the PS Flash Algorithm.")
                IObj?.Close()
                Return Flash_PS_1(Vz, P, S, Tref, PP, ReuseKI, PrevKi)
            End If
        End Function

        Public Function Flash_PH_1(ByVal Vz As Double(), ByVal P As Double, ByVal H As Double, ByVal Tref As Double, ByVal PP As PropertyPackages.PropertyPackage, Optional ByVal ReuseKI As Boolean = False, Optional ByVal PrevKi As Double() = Nothing) As Object

            Dim IObj As Inspector.InspectorItem = Inspector.Host.GetNewInspectorItem()

            Inspector.Host.CheckAndAdd(IObj, "", "Flash_PH", Name & " (PH Flash - Fast Mode)", "Pressure-Enthalpy Flash Algorithm Routine (Fast Mode)")

            IObj?.Paragraphs.Add("The PH Flash in fast mode uses two nested loops (hence the name) to calculate temperature and phase distribution. 
                                    The external one converges the temperature, while the internal one finds the phase distribution for the current temperature estimate in the external loop.
                                    The algorithm converges when the calculated overall enthalpy for the tentative phase distribution and temperature matches the specified one.")

            IObj?.SetCurrent()

            Dim i, n, ecount As Integer
            Dim d1, d2 As Date, dt As TimeSpan
            Dim L1, L2, V, T, Pf, Sx As Double

            d1 = Date.Now

            n = Vz.Length - 1

            Hf = H
            Pf = P

            Dim Vn(n) As String, Vx1(n), Vx2(n), Vy(n), Vs(n), Ki(n) As Double

            Vn = PP.RET_VNAMES()

            Dim maxitEXT As Integer = Me.FlashSettings(Interfaces.Enums.FlashSetting.PHFlash_Maximum_Number_Of_External_Iterations)
            Dim tolEXT As Double = Me.FlashSettings(Interfaces.Enums.FlashSetting.PHFlash_External_Loop_Tolerance).ToDoubleFromInvariant

            Dim Tmin, Tmax, maxDT As Double

            Tmax = 10000.0#
            Tmin = 20.0#
            maxDT = Me.FlashSettings(Interfaces.Enums.FlashSetting.PHFlash_MaximumTemperatureChange).ToDoubleFromInvariant

            Dim fx, fx2, fx_ant, dfdx, x1, dx As Double

            Dim cnt As Integer

            If Tref = 0.0# Then Tref = 298.15
            T = Tref

            IObj?.Paragraphs.Add(String.Format("<h2>Input Parameters</h2>"))

            IObj?.Paragraphs.Add(String.Format("Pressure: {0} Pa", P))
            IObj?.Paragraphs.Add(String.Format("Enthalpy: {0} kJ/kg", H))
            IObj?.Paragraphs.Add(String.Format("Compounds: {0}", PP.RET_VNAMES.ToMathArrayString))
            IObj?.Paragraphs.Add(String.Format("Mole Fractions: {0}", Vz.ToMathArrayString))
            IObj?.Paragraphs.Add(String.Format("Initial estimate for T: {0} K", T))

            cnt = 0
            x1 = Tref

            Dim x_prev As Double = Double.NaN
            Dim fx_secant As Double = Double.NaN
            Dim signChanges As Integer = 0
            Dim Ki_est As Double() = Nothing

            ' Continuous bracket tracking: T values where Herror has opposite signs
            Dim T_bracket_pos As Double = Double.NaN  ' T where fx > 0 (T too low)
            Dim T_bracket_neg As Double = Double.NaN  ' T where fx < 0 (T too high)
            Dim fx_bracket_pos As Double = Double.NaN
            Dim fx_bracket_neg As Double = Double.NaN

            Do

                IObj?.SetCurrent()

                Dim IObj2 As Inspector.InspectorItem = Inspector.Host.GetNewInspectorItem()

                Inspector.Host.CheckAndAdd(IObj2, "", "Flash_PH", "PH Flash Newton/Secant Iteration", "Pressure-Enthalpy Flash Algorithm (Fast Mode) Convergence Iteration Step")

                IObj2?.Paragraphs.Add(String.Format("This is the convergence loop iteration #{0}. Current T estimate: {1} K", cnt, x1))

                fx_ant = fx

                Dim herrobj As Object

                IObj2?.SetCurrent()
                herrobj = Herror("PT", x1, P, Vz, PP, Ki_est IsNot Nothing, Ki_est)
                fx = herrobj(0)

                ' Extract Ki from result for reuse in next iteration
                Dim Vy_tmp = DirectCast(herrobj(4), Double())
                Dim Vx1_tmp = DirectCast(herrobj(5), Double())
                Ki_est = New Double(n) {}
                For i = 0 To n
                    If Vx1_tmp(i) > 1.0E-20 Then
                        Ki_est(i) = Vy_tmp(i) / Vx1_tmp(i)
                    Else
                        Ki_est(i) = 1.0
                    End If
                Next

                IObj2?.Paragraphs.Add(String.Format("Current Enthalpy error: {0} kJ/kg", fx))

                If Double.IsNaN(fx) Then
                    IObj2?.Close()
                    Dim ex As New Exception("PH Flash [NL]: Invalid result: Temperature did not converge." & String.Format(" (T = {0} K, P = {1} Pa, MoleFracs = {2})", T.ToString("N2"), P.ToString("N2"), Vz.ToArrayString()))
                    ex.Data.Add("DetailedDescription", "The Flash Algorithm was unable to converge to a solution.")
                    ex.Data.Add("UserAction", "Try another Property Package and/or Flash Algorithm.")
                    Throw ex
                End If

                ' Update bracket tracking: keep the tightest bracket found
                If fx > 0 Then
                    If Double.IsNaN(T_bracket_pos) OrElse Math.Abs(fx) < Math.Abs(fx_bracket_pos) Then
                        T_bracket_pos = x1
                        fx_bracket_pos = fx
                    End If
                ElseIf fx < 0 Then
                    If Double.IsNaN(T_bracket_neg) OrElse Math.Abs(fx) < Math.Abs(fx_bracket_neg) Then
                        T_bracket_neg = x1
                        fx_bracket_neg = fx
                    End If
                End If

                ' Convergence check: absolute + relative
                If Abs(fx) <= tolEXT Then Exit Do
                If H <> 0.0 AndAlso Abs(fx / H) < 0.0001 Then Exit Do

                ' Track sign changes for oscillation detection
                If cnt > 0 AndAlso Math.Sign(fx) <> Math.Sign(fx_ant) Then
                    signChanges += 1
                End If

                ' Oscillation fallback: use Brent on bracketed interval
                If signChanges >= 3 Then

                    If Not Double.IsNaN(T_bracket_pos) AndAlso Not Double.IsNaN(T_bracket_neg) Then
                        Dim bmin As New Brent
                        Dim Kbrent = Ki_est
                        Dim Ta = Math.Min(T_bracket_pos, T_bracket_neg)
                        Dim Tb = Math.Max(T_bracket_pos, T_bracket_neg)
                        x1 = bmin.BrentOpt2(Ta, Tb, 100, tolEXT, maxitEXT,
                            Function(tval)
                                Return Herror("PT", tval, P, Vz, PP, Kbrent IsNot Nothing, Kbrent)(0)
                            End Function)
                        Exit Do
                    Else
                        ' Cannot bracket, fall back to rigorous mode
                        IObj2?.Close()
                        IObj?.Close()
                        Return Flash_PH_2(Vz, P, H, x1, PP, ReuseKI, PrevKi)
                    End If

                End If

                ' Compute derivative: secant method (reuse previous point) or forward difference
                Dim useSecant As Boolean = False
                If cnt > 0 AndAlso Not Double.IsNaN(fx_secant) AndAlso Math.Abs(x1 - x_prev) > 1.0E-15 Then
                    Dim secant_dfdx = (fx - fx_secant) / (x1 - x_prev)
                    ' Safeguard: secant derivative must have the correct sign (dH/dT should be negative
                    ' for the error function Hspec - Hcalc, so dfdx < 0 typically)
                    ' and must not produce a step larger than 2x maxDT
                    If Math.Abs(secant_dfdx) > 1.0E-20 AndAlso Math.Abs(fx / secant_dfdx) < 2.0 * maxDT Then
                        dfdx = secant_dfdx
                        useSecant = True
                    End If
                End If

                If Not useSecant Then
                    ' Forward difference with adaptive epsilon (0.1% of current T, clamped to [0.01, 1.0])
                    Dim eps_fd As Double = Math.Max(0.01, Math.Min(1.0, x1 * 0.001))
                    IObj2?.SetCurrent()
                    fx2 = Herror("PT", x1 + eps_fd, P, Vz, PP, Ki_est IsNot Nothing, Ki_est)(0)
                    dfdx = (fx2 - fx) / eps_fd
                End If

                ' Guard against near-zero derivative
                If Math.Abs(dfdx) < 1.0E-20 Then
                    dfdx = -Math.Sign(fx) * 1.0E-20
                End If

                dx = fx / dfdx
                If Abs(dx) > maxDT Then dx = maxDT * Sign(dx)

                x_prev = x1
                fx_secant = fx
                x1 = x1 - dx

                ' Clamp temperature to physically meaningful range
                If x1 < Tmin Then x1 = Tmin + (Tref - Tmin) * 0.1
                If x1 > Tmax Then x1 = Tmax - (Tmax - Tref) * 0.1

                ' If we have a bracket and the Newton/secant step went outside it, use bisection instead
                If Not Double.IsNaN(T_bracket_pos) AndAlso Not Double.IsNaN(T_bracket_neg) Then
                    Dim bracketLo = Math.Min(T_bracket_pos, T_bracket_neg)
                    Dim bracketHi = Math.Max(T_bracket_pos, T_bracket_neg)
                    If x1 < bracketLo OrElse x1 > bracketHi Then
                        ' Step went outside bracket: use bisection (safer)
                        x1 = (bracketLo + bracketHi) / 2.0
                    End If
                End If

                IObj2?.Paragraphs.Add(String.Format("Updated Temperature estimate: {0} K", x1))

                cnt += 1

                IObj2?.Close()

            Loop Until cnt > maxitEXT Or Double.IsNaN(x1)

            IObj?.Paragraphs.Add(String.Format("The PH Flash algorithm converged in {0} iterations. Final Temperature value: {1} K", cnt, x1))

            T = x1

            IObj?.Close()

            If Double.IsNaN(T) Or T <= Tmin Or T >= Tmax Or cnt > maxitEXT Then
                'switch to mode 2 if it doesn't converge using fast mode.
                WriteDebugInfo("PH Flash [NL]: Didn't converge in fast mode. Switching to rigorous...")
                Return Flash_PH_2(Vz, P, H, Tref, PP, ReuseKI, PrevKi)
            Else
                ' Reuse converged Ki as initial estimate for the final Flash_PT
                Dim useKi As Boolean = Ki_est IsNot Nothing
                If useKi Then Ki = Ki_est
                If PTFlashFunction IsNot Nothing Then
                    Dim tmp = PTFlashFunction.Invoke(Vz, P, T, PP, useKi, Ki)
                    L1 = tmp(0)
                    V = tmp(1)
                    Vx1 = tmp(2)
                    Vy = tmp(3)
                    ecount = tmp(4)
                    L2 = tmp(5)
                    Vx2 = tmp(6)
                    Sx = tmp(7)
                    Vs = tmp(8)
                Else
                    Dim tmp = Me.Flash_PT(Vz, P, T, PP, useKi, Ki)
                    L1 = tmp(0)
                    V = tmp(1)
                    Vx1 = tmp(2)
                    Vy = tmp(3)
                    ecount = tmp(4)
                    L2 = tmp(5)
                    Vx2 = tmp(6)
                    Sx = tmp(7)
                    Vs = tmp(8)
                End If
                For i = 0 To n
                    If Vx1(i) > 1.0E-20 Then
                        Ki(i) = Vy(i) / Vx1(i)
                    Else
                        Ki(i) = 1.0E+20
                    End If
                Next
                d2 = Date.Now
                dt = d2 - d1
                WriteDebugInfo("PH Flash [NL]: Converged in " & ecount & " iterations. Time taken: " & dt.TotalMilliseconds & " ms.")
                IObj?.Paragraphs.Add("The algorithm converged in " & ecount & " iterations. Time taken: " & dt.TotalMilliseconds & " ms.")

                If SharedClasses.AI.ConvergenceAssistant.Manager IsNot Nothing Then
                    DWSIM.SharedClasses.AI.ConvergenceAssistant.Manager?.StoreData(
                        New AI.ConvergenceAssistant.Classes.ConvergenceHelperTrainingData With {
                        .CompoundNames = PP.RET_VNAMES(), .ModelName = PP.ComponentName, .NumberOfCompounds = Ki.Count,
                        .Temperature = T.ToString("F4", CultureInfo.InvariantCulture),
                        .Pressure = P.ToString("F4", CultureInfo.InvariantCulture),
                        .MassEnthalpy = H.ToString("F4", CultureInfo.InvariantCulture),
                        .VaporMolarFraction = V.ToString("F4", CultureInfo.InvariantCulture),
                        .Liquid1MolarFlows = Vx1.MultiplyConstY(L1).ToString("F4"),
                        .VaporMolarFlows = Vy.MultiplyConstY(V).ToString("F4"),
                        .KValuesVL1 = Ki.ToString("F4"),
                        .MixtureMolarFlows = Vz.ToString("F4"),
                        .RequestType = Interfaces.ConvergenceHelperRequestType.PHFlash})
                End If

                Return New Object() {L1, V, Vx1, Vy, T, ecount, Ki, L2, Vx2, Sx, Vs}

            End If

        End Function

        Public Function Flash_PH_2(ByVal Vz As Double(), ByVal P As Double, ByVal H As Double, ByVal Tref As Double, ByVal PP As PropertyPackages.PropertyPackage, Optional ByVal ReuseKI As Boolean = False, Optional ByVal PrevKi As Double() = Nothing) As Object

            Dim IObj As Inspector.InspectorItem = Inspector.Host.GetNewInspectorItem()

            Inspector.Host.CheckAndAdd(IObj, "", "Flash_PH", Name & " (PH Flash - Default Mode)", "Pressure-Enthalpy Flash Algorithm Routine (Normal Mode)")

            IObj?.Paragraphs.Add("The PH Flash in default mode calculates the enthalpy at mixture bubble and dew points, in order to determine the state of the mixture. 
                                  It then converges the temperature or vapor fraction depending on the estimated state.")

            IObj?.SetCurrent()

            Dim doparallel As Boolean = Settings.EnableParallelProcessing

            Dim i, n, ecount As Integer
            Dim d1, d2 As Date, dt As TimeSpan
            Dim L1, L2, V, T, Pf, Sx As Double

            Dim resultFlash As Object
            Dim Tb, Td, Hb, Hd As Double
            Dim ErrRes As Object

            d1 = Date.Now

            n = Vz.Length - 1

            Hf = H
            Pf = P

            Dim Vn(n) As String, Vx1(n), Vx2(n), Vy(n), Vs(n), Ki(n), Ki_ant(n), fi(n) As Double

            Vn = PP.RET_VNAMES()
            fi = Vz.Clone

            Dim maxitINT As Integer = Me.FlashSettings(Interfaces.Enums.FlashSetting.PHFlash_Maximum_Number_Of_Internal_Iterations)
            Dim maxitEXT As Integer = Me.FlashSettings(Interfaces.Enums.FlashSetting.PHFlash_Maximum_Number_Of_External_Iterations)
            Dim tolINT As Double = Me.FlashSettings(Interfaces.Enums.FlashSetting.PHFlash_Internal_Loop_Tolerance).ToDoubleFromInvariant
            Dim tolEXT As Double = Me.FlashSettings(Interfaces.Enums.FlashSetting.PHFlash_External_Loop_Tolerance).ToDoubleFromInvariant

            Dim Tmin, Tmax As Double

            Tmax = 10000.0#
            Tmin = 20.0#

            If Tref = 0.0# Then Tref = 298.15

            Ki = PP.RET_VPVAP(Tref).MultiplyConstY(1 / P)

            IObj?.Paragraphs.Add(String.Format("<h2>Input Parameters</h2>"))

            IObj?.Paragraphs.Add(String.Format("Pressure: {0} Pa", P))
            IObj?.Paragraphs.Add(String.Format("Enthalpy: {0} kJ/kg", H))
            IObj?.Paragraphs.Add(String.Format("Compounds: {0}", PP.RET_VNAMES.ToMathArrayString))
            IObj?.Paragraphs.Add(String.Format("Mole Fractions: {0}", Vz.ToMathArrayString))
            IObj?.Paragraphs.Add(String.Format("Initial estimate for T: {0} K", T))

            'calculate dew point and boiling point

            IObj?.Paragraphs.Add(String.Format("Calculating Dew and Bubble points..."))

            Dim alreadymt As Boolean = False

            If Settings.EnableParallelProcessing And Not DisableParallelCalcs Then

                Dim task1 = TaskHelper.Run(Sub()
                                               Dim ErrRes1 = Herror("PV", 0, P, Vz, PP, False, Nothing)
                                               Hb = ErrRes1(0)
                                               Tb = ErrRes1(1)
                                           End Sub, Settings.TaskCancellationTokenSource.Token)
                Dim task2 = TaskHelper.Run(Sub()
                                               Dim ErrRes2 = Herror("PV", 1, P, Vz, PP, False, Nothing)
                                               Hd = ErrRes2(0)
                                               Td = ErrRes2(1)
                                           End Sub, Settings.TaskCancellationTokenSource.Token)
                Task.WaitAll(task1, task2)

            Else

                IObj?.SetCurrent()
                ErrRes = Herror("PV", 0, P, Vz, PP, False, Nothing)
                Hb = ErrRes(0)
                Tb = ErrRes(1)
                IObj?.SetCurrent()
                ErrRes = Herror("PV", 1, P, Vz, PP, False, Nothing)
                Hd = ErrRes(0)
                Td = ErrRes(1)

            End If

            IObj?.Paragraphs.Add(String.Format("Calculated Bubble Temperature: {0} K", Tb))

            IObj?.Paragraphs.Add(String.Format("Calculated Dew Temperature: {0} K", Td))

            IObj?.Paragraphs.Add(String.Format("Bubble Point Enthalpy Error (Spec - Calculated): {0}", Hb))

            IObj?.Paragraphs.Add(String.Format("Dew Point Enthalpy Error (Spec - Calculated): {0}", Hd))

            Dim herrfunc As Object

            If Hb > 0 And Hd < 0 Then

                IObj?.Paragraphs.Add(String.Format("Enthalpy at Bubble Point is lower than spec. Requires partial evaporation."))

                'specified enthalpy requires partial evaporation 
                'calculate vapour fraction

                Dim H1, H2, V1, V2 As Double
                ecount = 0
                V = 0
                Dim hres = PerformHeuristicsTest(Vz, T, P, PP)
                If hres.SolidPhase Then V = 0.5
                H1 = Hb
                Do

                    ecount += 1
                    V1 = V
                    If V1 < 1 Then
                        V2 = V1 + 0.01
                    Else
                        V2 = V1 - 0.01
                    End If
                    IObj?.SetCurrent()
                    herrfunc = Herror("PV", V2, P, Vz, PP, True, Ki)
                    H2 = herrfunc(0)
                    Vy = herrfunc(4)
                    Vx1 = herrfunc(5)
                    Ki = Vy.DivideY(Vx1)
                    V = V1 + (V2 - V1) * (0 - H1) / (H2 - H1)
                    If V < 0 Then V = 0.0#
                    If V > 1 Then V = 1.0#
                    IObj?.Paragraphs.Add(String.Format("Updated Vapor Fraction estimate: {0}", V))
                    IObj?.SetCurrent()
                    resultFlash = Herror("PV", V, P, Vz, PP, True, Ki)
                    H1 = resultFlash(0)
                    If V = 1.0 Or V = 0.0 And Math.Abs(H1) < 0.01 Then Exit Do
                    IObj?.Paragraphs.Add(String.Format("Enthalpy Error (Spec - Calculated): {0}", H1))
                Loop Until Abs(H1) < itol Or ecount > maxitEXT

                T = resultFlash(1)

                If T <= Tmin Or T >= Tmax Or ecount > maxitEXT Then
                    Dim ex As New Exception("PH Flash [NL]: Invalid result: Temperature did not converge." & String.Format(" (T = {0} K, P = {1} Pa, MoleFracs = {2})", T.ToString("N2"), P.ToString("N2"), Vz.ToArrayString()))
                    ex.Data.Add("DetailedDescription", "The Flash Algorithm was unable to converge to a solution.")
                    ex.Data.Add("UserAction", "Try another Property Package and/or Flash Algorithm.")
                    Throw ex
                End If

            ElseIf Hd > 0 Then

                IObj?.Paragraphs.Add(String.Format("Spec Enthalpy is higher than the calculated one at Dew Point. Single Vapor Phase detected."))

                'only gas phase
                'calculate temperature

                Dim H1, H2, T1, T2 As Double
                ecount = 0
                T = Td
                H1 = Hd
                Do
                    ecount += 1
                    T1 = T
                    T2 = T1 + 1
                    IObj?.SetCurrent()
                    H2 = Hf - PP.DW_CalcEnthalpy(Vz, T2, P, State.Vapor)
                    T = T1 + (T2 - T1) * (0 - H1) / (H2 - H1)
                    If T < 0 Then
                        Throw New Exception("PH Flash [NL]: Invalid result: Temperature did not converge." & String.Format(" (T = {0} K, P = {1} Pa, MoleFracs = {2})", T.ToString("N2"), P.ToString("N2"), Vz.ToArrayString()))
                    End If
                    IObj?.Paragraphs.Add(String.Format("Updated Temperature estimate: {0} K", T))
                    IObj?.SetCurrent()
                    H1 = Hf - PP.DW_CalcEnthalpy(Vz, T, P, State.Vapor)
                    IObj?.Paragraphs.Add(String.Format("Enthalpy Error (Spec - Calculated): {0}", H1))
                Loop Until Abs(H1) < itol Or ecount > maxitEXT

                L1 = 0
                V = 1
                Vy = Vz.Clone
                Vx1 = Vz.Clone
                L1 = 0
                For i = 0 To n
                    Ki(i) = 1
                Next

                If T <= Tmin Or T >= Tmax Or ecount > maxitEXT Then
                    Dim ex As New Exception("PH Flash [NL]: Invalid result: Temperature did not converge." & String.Format(" (T = {0} K, P = {1} Pa, MoleFracs = {2})", T.ToString("N2"), P.ToString("N2"), Vz.ToArrayString()))
                    ex.Data.Add("DetailedDescription", "The Flash Algorithm was unable to converge to a solution.")
                    ex.Data.Add("UserAction", "Try another Property Package and/or Flash Algorithm.")
                    Throw ex
                End If

            Else

                IObj?.Paragraphs.Add(String.Format("Spec Enthalpy is lower than the calculated one at Bubble Point. Liquid Phase detected."))

                'specified enthalpy requires pure liquid 
                'calculate temperature

                Dim H1, H2, T1, T2 As Double
                ecount = 0
                Dim hres = PerformHeuristicsTest(Vz, Tref, P, PP)
                If hres.SolidPhase Then
                    T = Tref
                Else
                    T = Tb
                End If
                H1 = Hb
                Do
                    ecount += 1
                    T1 = T
                    T2 = T1 - 1
                    IObj?.SetCurrent()
                    herrfunc = Herror("PT", T2, P, Vz, PP, True, Ki)
                    H2 = herrfunc(0)
                    Vy = herrfunc(4)
                    Vx1 = herrfunc(5)
                    Ki = Vy.DivideY(Vx1)
                    T = T1 + (T2 - T1) * (0 - H1) / (H2 - H1)
                    If T < 0 Then
                        Throw New Exception("PH Flash [NL]: Invalid result: Temperature did not converge." & String.Format(" (T = {0} K, P = {1} Pa, MoleFracs = {2})", T.ToString("N2"), P.ToString("N2"), Vz.ToArrayString()))
                    End If
                    IObj?.Paragraphs.Add(String.Format("Updated Temperature estimate: {0} K", T))
                    IObj?.SetCurrent()
                    resultFlash = Herror("PT", T, P, Vz, PP, True, Ki)
                    H1 = resultFlash(0)
                    IObj?.Paragraphs.Add(String.Format("Enthalpy Error (Spec - Calculated): {0}", H1))
                Loop Until Abs(H1) < itol Or ecount > maxitEXT

                If T <= Tmin Or T >= Tmax Or ecount > maxitEXT Then
                    Dim ex As New Exception("PH Flash [NL]: Invalid result: Temperature did not converge." & String.Format(" (T = {0} K, P = {1} Pa, MoleFracs = {2})", T.ToString("N2"), P.ToString("N2"), Vz.ToArrayString()))
                    ex.Data.Add("DetailedDescription", "The Flash Algorithm was unable to converge to a solution.")
                    ex.Data.Add("UserAction", "Try another Property Package and/or Flash Algorithm.")
                    Throw ex
                End If

            End If

            If V > 0 And V < 1 Then

                'partial vaporization.
                Dim tmp As Object
                Dim hres = PerformHeuristicsTest(Vz, T, P, PP)
                If hres.SolidPhase And CalledFromSLE Then
                    tmp = New NestedLoopsSLE().Flash_PV(Vz, P, V, 0.0, PP, ReuseKI, Ki)
                Else
                    tmp = Me.Flash_PV(Vz, P, V, 0.0#, PP, ReuseKI, Ki)
                End If
                L1 = tmp(0)
                V = tmp(1)
                Vx1 = tmp(2)
                Vy = tmp(3)
                T = tmp(4)
                L2 = tmp(7)
                Vx2 = tmp(8)
                Sx = tmp(9)
                Vs = tmp(10)

            Else

                If PTFlashFunction IsNot Nothing Then
                    Dim tmp = PTFlashFunction.Invoke(Vz, P, T, PP, ReuseKI, Ki)
                    L1 = tmp(0)
                    V = tmp(1)
                    Vx1 = tmp(2)
                    Vy = tmp(3)
                    ecount = tmp(4)
                    L2 = tmp(5)
                    Vx2 = tmp(6)
                    Sx = tmp(7)
                    Vs = tmp(8)
                Else
                    Dim tmp = Me.Flash_PT(Vz, P, T, PP, ReuseKI, Ki)
                    L1 = tmp(0)
                    V = tmp(1)
                    Vx1 = tmp(2)
                    Vy = tmp(3)
                    ecount = tmp(4)
                    L2 = tmp(5)
                    Vx2 = tmp(6)
                    Sx = tmp(7)
                    Vs = tmp(8)
                End If

            End If

            For i = 0 To n
                Ki(i) = Vy(i) / Vx1(i)
            Next

            IObj?.Paragraphs.Add(String.Format("Final converged value for T: {0} K", T))

            d2 = Date.Now

            dt = d2 - d1

            WriteDebugInfo("PH Flash [NL]: Converged in " & ecount & " iterations. Time taken: " & dt.TotalMilliseconds & " ms")

            IObj?.Paragraphs.Add("The algorithm converged in " & ecount & " iterations. Time taken: " & dt.TotalMilliseconds & " ms.")

            IObj?.Close()

            If SharedClasses.AI.ConvergenceAssistant.Manager IsNot Nothing Then
                DWSIM.SharedClasses.AI.ConvergenceAssistant.Manager?.StoreData(
                        New AI.ConvergenceAssistant.Classes.ConvergenceHelperTrainingData With {
                        .CompoundNames = PP.RET_VNAMES(), .ModelName = PP.ComponentName, .NumberOfCompounds = Ki.Count,
                        .Temperature = T.ToString("F4", CultureInfo.InvariantCulture),
                        .Pressure = P.ToString("F4", CultureInfo.InvariantCulture),
                        .MassEnthalpy = H.ToString("F4", CultureInfo.InvariantCulture),
                        .VaporMolarFraction = V.ToString("F4", CultureInfo.InvariantCulture),
                        .Liquid1MolarFlows = Vx1.MultiplyConstY(L1).ToString("F4"),
                        .VaporMolarFlows = Vy.MultiplyConstY(V).ToString("F4"),
                        .KValuesVL1 = Ki.ToString("F4"),
                        .MixtureMolarFlows = Vz.ToString("F4"),
                        .RequestType = Interfaces.ConvergenceHelperRequestType.PHFlash})
            End If

            Return New Object() {L1, V, Vx1, Vy, T, ecount, Ki, L2, Vx2, Sx, Vs}

        End Function

        Public Function Flash_PS_1(ByVal Vz As Double(), ByVal P As Double, ByVal S As Double, ByVal Tref As Double, ByVal PP As PropertyPackages.PropertyPackage, Optional ByVal ReuseKI As Boolean = False, Optional ByVal PrevKi As Double() = Nothing) As Object

            Dim IObj As Inspector.InspectorItem = Inspector.Host.GetNewInspectorItem()

            Inspector.Host.CheckAndAdd(IObj, "", "Flash_PS", Name & " (PS Flash - Fast Mode)", "Pressure-Entropy Flash Algorithm Routine (Fast Mode)")

            IObj?.Paragraphs.Add("The PS Flash in fast mode uses two nested loops (hence the name) to calculate temperature and phase distribution. 
                                    The external one converges the temperature, while the internal one finds the phase distribution for the current temperature estimate in the external loop.
                                    The algorithm converges when the calculated overall entropy for the tentative phase distribution and temperature matches the specified one.")

            IObj?.SetCurrent()

            Dim i, j, n, ecount As Integer
            Dim d1, d2 As Date, dt As TimeSpan
            Dim L1, L2, V, T, Pf, Sx As Double

            d1 = Date.Now

            n = Vz.Length - 1

            PP = PP
            Sf = S
            Pf = P

            Dim Vn(n) As String, Vx1(n), Vx2(n), Vy(n), Vs(n), Ki(n), Ki_ant(n), fi(n) As Double

            Vn = PP.RET_VNAMES()
            fi = Vz.Clone

            Dim maxitINT As Integer = Me.FlashSettings(Interfaces.Enums.FlashSetting.PHFlash_Maximum_Number_Of_Internal_Iterations)
            Dim maxitEXT As Integer = Me.FlashSettings(Interfaces.Enums.FlashSetting.PHFlash_Maximum_Number_Of_External_Iterations)
            Dim tolINT As Double = Me.FlashSettings(Interfaces.Enums.FlashSetting.PHFlash_Internal_Loop_Tolerance).ToDoubleFromInvariant
            Dim tolEXT As Double = Me.FlashSettings(Interfaces.Enums.FlashSetting.PHFlash_External_Loop_Tolerance).ToDoubleFromInvariant

            Dim Tmin, Tmax, epsilon(4), maxDT As Double

            Tmax = 10000.0#
            Tmin = 20.0#
            maxDT = Me.FlashSettings(Interfaces.Enums.FlashSetting.PHFlash_MaximumTemperatureChange).ToDoubleFromInvariant

            epsilon(0) = 1
            epsilon(1) = 0.1
            epsilon(2) = 0.01

            Dim fx, fx1, fx2, fx_ant, dfdx, x1, x0, dx As Double

            Dim cnt As Integer

            If Tref = 0 Then Tref = 298.15

            IObj?.Paragraphs.Add(String.Format("<h2>Input Parameters</h2>"))

            IObj?.Paragraphs.Add(String.Format("Pressure: {0} Pa", P))
            IObj?.Paragraphs.Add(String.Format("Entropy: {0} kJ/kg", S))
            IObj?.Paragraphs.Add(String.Format("Compounds: {0}", PP.RET_VNAMES.ToMathArrayString))
            IObj?.Paragraphs.Add(String.Format("Mole Fractions: {0}", Vz.ToMathArrayString))
            IObj?.Paragraphs.Add(String.Format("Initial estimate for T: {0} K", Tref))

            For j = 0 To 2

                cnt = 0
                x1 = Tref

                Dim fxvals, xvals As New List(Of Double)

                Dim serrobj As Object

                Do

                    IObj?.SetCurrent()

                    Dim IObj2 As Inspector.InspectorItem = Inspector.Host.GetNewInspectorItem()

                    Inspector.Host.CheckAndAdd(IObj2, "", "Flash_PS", "PS Flash Newton Iteration", "Pressure-Entropy Flash Algorithm (Fast Mode) Convergence Iteration Step")

                    IObj2?.Paragraphs.Add(String.Format("This is the Newton convergence loop iteration #{0}. DWSIM will use the current value of T to calculate the phase distribution by calling the Flash_PT routine.", cnt))

                    If cnt > 20 Then xvals.Add(x1)
                    fx_ant = fx

                    If Settings.EnableParallelProcessing And Not DisableParallelCalcs Then

                        Dim task0 = TaskHelper.Run(Sub()
                                                       serrobj = Serror("PT", x1, P, Vz, PP, False, Nothing)
                                                       fx = serrobj(0)
                                                   End Sub, Settings.TaskCancellationTokenSource.Token)
                        Dim task1 = TaskHelper.Run(Sub()
                                                       fx1 = Serror("PT", x1 - epsilon(j), P, Vz, PP, False, Nothing)(0)
                                                   End Sub, Settings.TaskCancellationTokenSource.Token)
                        Dim task2 = TaskHelper.Run(Sub()
                                                       fx2 = Serror("PT", x1 + epsilon(j), P, Vz, PP, False, Nothing)(0)
                                                   End Sub, Settings.TaskCancellationTokenSource.Token)
                        Task.WaitAll(task0, task1, task2)

                    Else

                        IObj2?.SetCurrent()
                        serrobj = Serror("PT", x1, P, Vz, PP, False, Nothing)
                        fx = serrobj(0)
                        IObj2?.SetCurrent()
                        serrobj = Serror("PT", x1 - epsilon(j), P, Vz, PP, False, Nothing)
                        fx1 = serrobj(0)
                        IObj2?.SetCurrent()
                        serrobj = Serror("PT", x1 + epsilon(j), P, Vz, PP, False, Nothing)
                        fx2 = serrobj(0)

                    End If

                    IObj2?.Paragraphs.Add(String.Format("Current Entropy error: {0}", fx2))

                    dfdx = (fx2 - fx1) / (2 * epsilon(j))


                    If Double.IsNaN(fx) Then
                        Dim ex As New Exception("PS Flash [NL]: Invalid result: Temperature did not converge." & String.Format(" (T = {0} K, P = {1} Pa, MoleFracs = {2})", T.ToString("N2"), P.ToString("N2"), Vz.ToArrayString()))
                        ex.Data.Add("DetailedDescription", "The Flash Algorithm was unable to converge to a solution.")
                        ex.Data.Add("UserAction", "Try another Property Package and/or Flash Algorithm.")
                        Throw ex
                    End If

                    If Abs(fx) < tolEXT Then Exit Do

                    If cnt > 20 Then fxvals.Add(fx)

                    dx = fx / dfdx

                    If Abs(dx) > maxDT Then dx = maxDT * Sign(dx)

                    x0 = x1

                    If cnt > 30 And Math.Sign(fx) <> Math.Sign(fx_ant) Then

                        'oscillating around the solution.

                        Dim bmin As New Brent

                        Dim interp As New MathNet.Numerics.Interpolation.BulirschStoerRationalInterpolation(xvals.ToArray(), fxvals.ToArray())

                        x1 = bmin.BrentOpt2(xvals.Min, xvals.Max, 5, 0.01, 100,
                                            Function(tval)
                                                Return interp.Interpolate(tval)
                                            End Function)

                        Exit Do

                    Else

                        x1 = x1 - dx

                    End If

                    If x1 < 0 Then
                        Throw New Exception("PS Flash [NL]: Invalid result: Temperature did not converge." & String.Format(" (T = {0} K, P = {1} Pa, MoleFracs = {2})", T.ToString("N2"), P.ToString("N2"), Vz.ToArrayString()))
                    End If

                    IObj2?.Paragraphs.Add(String.Format("Updated Temperature estimate: {0} K", x1))

                    cnt += 1

                    IObj2?.Close()

                Loop Until cnt > maxitEXT Or Double.IsNaN(x1)

                IObj?.Paragraphs.Add(String.Format("The PS Flash algorithm converged in {0} iterations. Final Temperature value: {1} K", cnt, x1))

                T = x1

                If Not Double.IsNaN(T) And Not Double.IsInfinity(T) And Not cnt > maxitEXT Then
                    If T > Tmin And T < Tmax Then Exit For
                End If

            Next

            IObj?.Close()

            If Double.IsNaN(T) Or T <= Tmin Or T >= Tmax Then
                Dim ex As New Exception("PS Flash [NL]: Invalid result: Temperature did not converge." & String.Format(" (T = {0} K, P = {1} Pa, MoleFracs = {2})", T.ToString("N2"), P.ToString("N2"), Vz.ToArrayString()))
                ex.Data.Add("DetailedDescription", "The Flash Algorithm was unable to converge to a solution.")
                ex.Data.Add("UserAction", "Try another Property Package and/or Flash Algorithm.")
                Throw ex
            End If

            If PTFlashFunction IsNot Nothing Then
                Dim tmp = PTFlashFunction.Invoke(Vz, P, T, PP, ReuseKI, Ki)
                L1 = tmp(0)
                V = tmp(1)
                Vx1 = tmp(2)
                Vy = tmp(3)
                ecount = tmp(4)
                L2 = tmp(5)
                Vx2 = tmp(6)
                Sx = tmp(7)
                Vs = tmp(8)
            Else
                Dim tmp = Me.Flash_PT(Vz, P, T, PP, ReuseKI, Ki)
                L1 = tmp(0)
                V = tmp(1)
                Vx1 = tmp(2)
                Vy = tmp(3)
                ecount = tmp(4)
                L2 = tmp(5)
                Vx2 = tmp(6)
                Sx = tmp(7)
                Vs = tmp(8)
            End If
            For i = 0 To n
                Ki(i) = Vy(i) / Vx1(i)
            Next

            d2 = Date.Now

            dt = d2 - d1

            WriteDebugInfo("PS Flash [NL]: Converged in " & ecount & " iterations. Time taken: " & dt.TotalMilliseconds & " ms.")

            IObj?.Paragraphs.Add("The algorithm converged in " & ecount & " iterations. Time taken: " & dt.TotalMilliseconds & " ms.")

            IObj?.Close()

            If SharedClasses.AI.ConvergenceAssistant.Manager IsNot Nothing Then
                DWSIM.SharedClasses.AI.ConvergenceAssistant.Manager?.StoreData(
                        New AI.ConvergenceAssistant.Classes.ConvergenceHelperTrainingData With {
                        .CompoundNames = PP.RET_VNAMES(), .ModelName = PP.ComponentName, .NumberOfCompounds = Ki.Count,
                        .Temperature = T.ToString("F4", CultureInfo.InvariantCulture),
                        .Pressure = P.ToString("F4", CultureInfo.InvariantCulture),
                        .MassEntropy = S.ToString("F4", CultureInfo.InvariantCulture),
                        .VaporMolarFraction = V.ToString("F4", CultureInfo.InvariantCulture),
                        .Liquid1MolarFlows = Vx1.MultiplyConstY(L1).ToString("F4"),
                        .VaporMolarFlows = Vy.MultiplyConstY(V).ToString("F4"),
                        .KValuesVL1 = Ki.ToString("F4"),
                        .MixtureMolarFlows = Vz.ToString("F4"),
                        .RequestType = Interfaces.ConvergenceHelperRequestType.PSFlash})
            End If

            Return New Object() {L1, V, Vx1, Vy, T, ecount, Ki, L2, Vx2, Sx, Vs}

        End Function

        Public Function Flash_PS_2(ByVal Vz As Double(), ByVal P As Double, ByVal S As Double, ByVal Tref As Double, ByVal PP As PropertyPackages.PropertyPackage, Optional ByVal ReuseKI As Boolean = False, Optional ByVal PrevKi As Double() = Nothing) As Object

            Dim IObj As Inspector.InspectorItem = Inspector.Host.GetNewInspectorItem()

            Inspector.Host.CheckAndAdd(IObj, "", "Flash_PS", Name & " (PS Flash - Normal Mode)", "Pressure-Entropy Flash Algorithm Routine (Normal Mode)")

            IObj?.Paragraphs.Add("The PS Flash in normal mode calculates the entropy at mixture bubble and dew points, in order to determine the state of the mixture. 
                                  It then converges the temperature or vapor fraction depending on the estimated state.")

            IObj?.SetCurrent()

            Dim doparallel As Boolean = Settings.EnableParallelProcessing

            Dim i, n, ecount As Integer
            Dim d1, d2 As Date, dt As TimeSpan
            Dim L1, L2, V, T, Pf, Sx As Double

            Dim resultFlash As Object
            Dim Tb, Td, Sb, Sd As Double
            Dim ErrRes As Object

            d1 = Date.Now

            n = Vz.Length - 1

            Sf = S
            Pf = P

            Dim Vn(n) As String, Vx1(n), Vx2(n), Vy(n), Vs(n), Ki(n), Ki_ant(n), fi(n) As Double

            Vn = PP.RET_VNAMES()
            fi = Vz.Clone

            Dim maxitINT As Integer = Me.FlashSettings(Interfaces.Enums.FlashSetting.PHFlash_Maximum_Number_Of_Internal_Iterations)
            Dim maxitEXT As Integer = Me.FlashSettings(Interfaces.Enums.FlashSetting.PHFlash_Maximum_Number_Of_External_Iterations)
            Dim tolINT As Double = Me.FlashSettings(Interfaces.Enums.FlashSetting.PHFlash_Internal_Loop_Tolerance).ToDoubleFromInvariant
            Dim tolEXT As Double = Me.FlashSettings(Interfaces.Enums.FlashSetting.PHFlash_External_Loop_Tolerance).ToDoubleFromInvariant

            Dim Tmin, Tmax As Double

            Tmax = 10000.0#
            Tmin = 20.0#

            If Tref = 0.0# Then Tref = 298.15

            Ki = PP.RET_VPVAP(Tref).MultiplyConstY(1 / P)

            IObj?.Paragraphs.Add(String.Format("<h2>Input Parameters</h2>"))

            IObj?.Paragraphs.Add(String.Format("Pressure: {0} Pa", P))
            IObj?.Paragraphs.Add(String.Format("Entropy: {0} kJ/kg", S))
            IObj?.Paragraphs.Add(String.Format("Compounds: {0}", PP.RET_VNAMES.ToMathArrayString))
            IObj?.Paragraphs.Add(String.Format("Mole Fractions: {0}", Vz.ToMathArrayString))
            IObj?.Paragraphs.Add(String.Format("Initial estimate for T: {0} K", T))

            'calculate dew point and boiling point

            IObj?.Paragraphs.Add(String.Format("Calculating Dew and Bubble points..."))

            Dim alreadymt As Boolean = False

            If Settings.EnableParallelProcessing Then

                Dim task1 = TaskHelper.Run(Sub()
                                               Dim ErrRes1 = Serror("PV", 0, P, Vz, PP, False, Nothing)
                                               Sb = ErrRes1(0)
                                               Tb = ErrRes1(1)
                                           End Sub, Settings.TaskCancellationTokenSource.Token)
                Dim task2 = TaskHelper.Run(Sub()
                                               Dim ErrRes2 = Serror("PV", 1, P, Vz, PP, False, Nothing)
                                               Sd = ErrRes2(0)
                                               Td = ErrRes2(1)
                                           End Sub, Settings.TaskCancellationTokenSource.Token)
                Task.WaitAll(task1, task2)

            Else
                IObj?.SetCurrent()
                ErrRes = Serror("PV", 0, P, Vz, PP, False, Nothing)
                Sb = ErrRes(0)
                Tb = ErrRes(1)
                IObj?.SetCurrent()
                ErrRes = Serror("PV", 1, P, Vz, PP, False, Nothing)
                Sd = ErrRes(0)
                Td = ErrRes(1)
            End If

            Dim S1, S2, T1, T2, V1, V2 As Double

            IObj?.Paragraphs.Add(String.Format("Calculated Bubble Temperature: {0} K", Tb))

            IObj?.Paragraphs.Add(String.Format("Calculated Dew Temperature: {0} K", Td))

            IObj?.Paragraphs.Add(String.Format("Bubble Point Entropy Error (Spec - Calculated): {0}", Sb))

            IObj?.Paragraphs.Add(String.Format("Dew Point Entropy Error (Spec - Calculated): {0}", Sd))

            Dim serrfunc As Object

            If Sb > 0 And Sd < 0 Then

                IObj?.Paragraphs.Add(String.Format("Entropy at Bubble Point is lower than spec. Requires partial evaporation."))

                'specified entropy requires partial evaporation 
                'calculate vapour fraction

                ecount = 0
                V = 0
                S1 = Sb
                Do
                    ecount += 1
                    V1 = V
                    If V1 < 1 Then
                        V2 = V1 + 0.01
                    Else
                        V2 = V1 - 0.01
                    End If

                    IObj?.SetCurrent()
                    serrfunc = Serror("PV", V2, P, Vz, PP, True, Ki)
                    S2 = serrfunc(0)
                    Vy = serrfunc(4)
                    Vx1 = serrfunc(5)
                    Ki = Vy.DivideY(Vx1)
                    V = V1 + (V2 - V1) * (0 - S1) / (S2 - S1)
                    If V < 0 Then V = 0
                    If V > 1 Then V = 1
                    IObj?.Paragraphs.Add(String.Format("Updated Vapor Fraction estimate: {0}", V))
                    IObj?.SetCurrent()
                    resultFlash = Serror("PV", V, P, Vz, PP, True, Ki)
                    S1 = resultFlash(0)
                    If V = 1.0 Or V = 0.0 And Math.Abs(S1) < 0.01 Then Exit Do
                    IObj?.Paragraphs.Add(String.Format("Entropy Error (Spec - Calculated): {0}", S1))
                Loop Until Abs(S1) < itol Or ecount > maxitEXT

                T = resultFlash(1)

            ElseIf Sd > 0 Then

                IObj?.Paragraphs.Add(String.Format("Spec Entropy is higher than the calculated one at Dew Point. Single Vapor Phase detected."))

                'only gas phase
                'calculate temperature

                ecount = 0
                T = Td
                S1 = Sd
                Do
                    ecount += 1
                    T1 = T
                    T2 = T1 + 1
                    IObj?.SetCurrent()
                    S2 = Sf - PP.DW_CalcEntropy(Vz, T2, P, State.Vapor)
                    T = T1 + (T2 - T1) * (0 - S1) / (S2 - S1)
                    If T < 0 Then
                        Throw New Exception("PS Flash [NL]: Invalid result: Temperature did not converge." & String.Format(" (T = {0} K, P = {1} Pa, MoleFracs = {2})", T.ToString("N2"), P.ToString("N2"), Vz.ToArrayString()))
                    End If
                    IObj?.Paragraphs.Add(String.Format("Updated Temperature estimate: {0} K", T))
                    IObj?.SetCurrent()
                    S1 = Sf - PP.DW_CalcEntropy(Vz, T, P, State.Vapor)
                    IObj?.Paragraphs.Add(String.Format("Entropy Error (Spec - Calculated): {0}", S1))
                Loop Until Abs(S1) < itol Or ecount > maxitEXT

                L1 = 0
                V = 1
                Vy = Vz.Clone
                Vx1 = Vz.Clone
                L1 = 0
                For i = 0 To n
                    Ki(i) = 1
                Next

                If T <= Tmin Or T >= Tmax Then
                    Dim ex As New Exception("PS Flash [NL]: Invalid result: Temperature did not converge." & String.Format(" (T = {0} K, P = {1} Pa, MoleFracs = {2})", T.ToString("N2"), P.ToString("N2"), Vz.ToArrayString()))
                    ex.Data.Add("DetailedDescription", "The Flash Algorithm was unable to converge to a solution.")
                    ex.Data.Add("UserAction", "Try another Property Package and/or Flash Algorithm.")
                    Throw ex
                End If

            Else

                IObj?.Paragraphs.Add(String.Format("Spec Entropy is lower than the calculated one at Bubble Point. Liquid Phase detected."))

                'specified enthalpy requires pure liquid 
                'calculate temperature

                ecount = 0
                T = Tb
                S1 = Sb
                Do
                    ecount += 1
                    T1 = T
                    T2 = T1 - 1
                    IObj?.SetCurrent()
                    serrfunc = Serror("PT", T2, P, Vz, PP, True, Ki)
                    S2 = serrfunc(0)
                    Vy = serrfunc(4)
                    Vx1 = serrfunc(5)
                    Ki = Vy.DivideY(Vx1)
                    T = T1 + (T2 - T1) * (0 - S1) / (S2 - S1)
                    If T < 0 Then
                        Throw New Exception("PS Flash [NL]: Invalid result: Temperature did not converge." & String.Format(" (T = {0} K, P = {1} Pa, MoleFracs = {2})", T.ToString("N2"), P.ToString("N2"), Vz.ToArrayString()))
                    End If
                    IObj?.Paragraphs.Add(String.Format("Updated Temperature estimate: {0} K", T))
                    IObj?.SetCurrent()
                    resultFlash = Serror("PT", T, P, Vz, PP, True, Ki)
                    S1 = resultFlash(0)
                    IObj?.Paragraphs.Add(String.Format("Entropy Error (Spec - Calculated): {0}", S1))
                Loop Until Abs(S1) < itol Or ecount > maxitEXT

                V = 0
                L1 = resultFlash(3)
                Vy = resultFlash(4)
                Vx1 = resultFlash(5)

                For i = 0 To n
                    Ki(i) = Vy(i) / Vx1(i)
                Next

                If T <= Tmin Or T >= Tmax Then
                    Dim ex As New Exception("PS Flash [NL]: Invalid result: Temperature did not converge." & String.Format(" (T = {0} K, P = {1} Pa, MoleFracs = {2})", T.ToString("N2"), P.ToString("N2"), Vz.ToArrayString()))
                    ex.Data.Add("DetailedDescription", "The Flash Algorithm was unable to converge to a solution.")
                    ex.Data.Add("UserAction", "Try another Property Package and/or Flash Algorithm.")
                    Throw ex
                End If

            End If

            If PTFlashFunction IsNot Nothing Then
                Dim tmp = PTFlashFunction.Invoke(Vz, P, T, PP, ReuseKI, Ki)
                L1 = tmp(0)
                V = tmp(1)
                Vx1 = tmp(2)
                Vy = tmp(3)
                ecount = tmp(4)
                L2 = tmp(5)
                Vx2 = tmp(6)
                Sx = tmp(7)
                Vs = tmp(8)
            Else
                Dim tmp = Me.Flash_PT(Vz, P, T, PP, ReuseKI, Ki)
                L1 = tmp(0)
                V = tmp(1)
                Vx1 = tmp(2)
                Vy = tmp(3)
                ecount = tmp(4)
                L2 = tmp(5)
                Vx2 = tmp(6)
                Sx = tmp(7)
                Vs = tmp(8)
            End If
            For i = 0 To n
                Ki(i) = Vy(i) / Vx1(i)
            Next

            IObj?.Paragraphs.Add(String.Format("Final converged value for T: {0} K", T))

            d2 = Date.Now

            dt = d2 - d1

            WriteDebugInfo("PS Flash [NL]: Converged in " & ecount & " iterations. Time taken: " & dt.TotalMilliseconds & " ms.")

            IObj?.Paragraphs.Add("The algorithm converged in " & ecount & " iterations. Time taken: " & dt.TotalMilliseconds & " ms.")

            IObj?.Close()

            If SharedClasses.AI.ConvergenceAssistant.Manager IsNot Nothing Then
                DWSIM.SharedClasses.AI.ConvergenceAssistant.Manager?.StoreData(
                        New AI.ConvergenceAssistant.Classes.ConvergenceHelperTrainingData With {
                        .CompoundNames = PP.RET_VNAMES(), .ModelName = PP.ComponentName, .NumberOfCompounds = Ki.Count,
                        .Temperature = T.ToString("F4", CultureInfo.InvariantCulture),
                        .Pressure = P.ToString("F4", CultureInfo.InvariantCulture),
                        .MassEntropy = S.ToString("F4", CultureInfo.InvariantCulture),
                        .VaporMolarFraction = V.ToString("F4", CultureInfo.InvariantCulture),
                        .Liquid1MolarFlows = Vx1.MultiplyConstY(L1).ToString("F4"),
                        .VaporMolarFlows = Vy.MultiplyConstY(V).ToString("F4"),
                        .KValuesVL1 = Ki.ToString("F4"),
                        .MixtureMolarFlows = Vz.ToString("F4"),
                        .RequestType = Interfaces.ConvergenceHelperRequestType.PSFlash})
            End If

            Return New Object() {L1, V, Vx1, Vy, T, ecount, Ki, L2, Vx2, Sx, Vs}

        End Function

        Public Overrides Function Flash_TV(ByVal Vz As Double(), ByVal T As Double, ByVal V As Double, ByVal Pref As Double, ByVal PP As PropertyPackages.PropertyPackage, Optional ByVal ReuseKI As Boolean = False, Optional ByVal PrevKi As Double() = Nothing) As Object

            Dim result As Object()

            Dim estimate As Interfaces.IConvergenceHelperResponse = Nothing

            If Settings.AIAssistedConvergenceLevel = Settings.AIAssistedConvergenceMode.Provide_Initial_Estimates Or
                    Settings.AIAssistedConvergenceLevel = Settings.AIAssistedConvergenceMode.Provide_Initial_Estimates_and_Solutions Then

                estimate = DWSIM.SharedClasses.AI.ConvergenceAssistant.SolutionProvider?.GetSolutionEstimate(
                   New DWSIM.AI.ConvergenceAssistant.Classes.ConvergenceHelperRequest With {
                   .CompoundNames = PP.RET_VNAMES(),
                   .NumberOfCompounds = Vz.Count,
                   .MixtureMolarFlows = Vz,
                   .ModelName = PP.ComponentName,
                   .Pressure = Pref,
                   .VaporMolarFraction = V,
                   .Temperature = T,
                   .RequestType = Interfaces.ConvergenceHelperRequestType.TVFlash
               })

            End If

            Dim calcex As Exception

            Try

                If estimate IsNot Nothing And (Settings.AIAssistedConvergenceLevel = Settings.AIAssistedConvergenceMode.Provide_Initial_Estimates Or
                    Settings.AIAssistedConvergenceLevel = Settings.AIAssistedConvergenceMode.Provide_Initial_Estimates_and_Solutions) Then

                    result = Flash_TV_1(Vz, T, V, estimate.Pressure, PP, True, estimate.KValuesVL1)

                Else

                    result = Flash_TV_1(Vz, T, V, Pref, PP, ReuseKI, PrevKi)

                End If

                Return result

            Catch ex As Exception

                calcex = ex

            End Try


            If Settings.AIAssistedConvergenceLevel = Settings.AIAssistedConvergenceMode.Provide_Initial_Estimates_2Pass Or
                        Settings.AIAssistedConvergenceLevel = Settings.AIAssistedConvergenceMode.Provide_Initial_Estimates_and_Solutions_2Pass Then

                estimate = DWSIM.SharedClasses.AI.ConvergenceAssistant.SolutionProvider?.GetSolutionEstimate(
                               New DWSIM.AI.ConvergenceAssistant.Classes.ConvergenceHelperRequest With {
                               .CompoundNames = PP.RET_VNAMES(),
                               .NumberOfCompounds = Vz.Count,
                               .MixtureMolarFlows = Vz,
                               .ModelName = PP.ComponentName,
                               .Pressure = Pref,
                               .VaporMolarFraction = V,
                               .Temperature = T,
                               .RequestType = Interfaces.ConvergenceHelperRequestType.TVFlash
                           })

                If estimate IsNot Nothing Then

                    Try

                        result = Flash_TV_1(Vz, T, V, estimate.Pressure, PP, True, estimate.KValuesVL1)

                    Catch ex As Exception

                        If Settings.AIAssistedConvergenceLevel = Settings.AIAssistedConvergenceMode.Provide_Initial_Estimates_and_Solutions Or
                        Settings.AIAssistedConvergenceLevel = Settings.AIAssistedConvergenceMode.Provide_Solutions Then

                            If estimate IsNot Nothing Then

                                Return New Object() {estimate.Liquid1MolarFlows.Sum,
                                    estimate.VaporMolarFlows.Sum,
                                    estimate.Liquid1MolarFlows.NormalizeY(),
                                    estimate.VaporMolarFlows.NormalizeY(),
                                    estimate.Pressure,
                                    0, estimate.KValuesVL1,
                                    0.0#, PP.RET_NullVector, 0.0#, PP.RET_NullVector}

                            Else

                                Throw New Exception(String.Format("{0}: Unable to calculate TV Flash with T = {1} and VF = {2}, molar fractions = {3}",
                                    PP.ComponentName, T, V, Vz.ToArrayString(PP.RET_VNAMES(), "G3")))

                            End If

                        End If

                    End Try

                Else

                    Throw calcex

                End If

            Else

                Throw calcex

            End If

            Return Nothing

        End Function

        Public Function Flash_TV_1(ByVal Vz As Double(), ByVal T As Double, ByVal V As Double, ByVal Pref As Double, ByVal PP As PropertyPackages.PropertyPackage, Optional ByVal ReuseKI As Boolean = False, Optional ByVal PrevKi As Double() = Nothing) As Object

            Dim IObj As Inspector.InspectorItem = Inspector.Host.GetNewInspectorItem()

            Inspector.Host.CheckAndAdd(IObj, "", "Flash_TV", Name & " (TV Flash)", "Temperature/Vapor Fraction Flash Algorithm Routine", True)

            IObj?.Paragraphs.Add("This routine calculates the pressure at which the specified mixture composition finds itself in vapor-liquid equilibrium with a vapor phase mole fraction equal to V at the specified T.")

            IObj?.Paragraphs.Add(String.Format("<h2>Input Parameters</h2>"))

            IObj?.Paragraphs.Add(String.Format("Temperature: {0} Pa", T))
            IObj?.Paragraphs.Add(String.Format("Vapor Mole Fraction: {0} ", V))
            IObj?.Paragraphs.Add(String.Format("Compounds: {0}", PP.RET_VNAMES.ToMathArrayString))
            IObj?.Paragraphs.Add(String.Format("Mole Fractions: {0}", Vz.ToMathArrayString))

            Dim Vn(1) As String, Vx(1), Vy(1), Vx_ant(1), Vy_ant(1), Vp(1), Ki(1), Ki_ant(1), fi(1) As Double
            Dim i, n, ecount As Integer
            Dim d1, d2 As Date, dt As TimeSpan
            Dim Pmin, Pmax, soma_x, soma_y As Double
            Dim L, Lf, Vf, P, Pf, deltaP As Double

            d1 = Date.Now

            etol = Me.FlashSettings(Interfaces.Enums.FlashSetting.PTFlash_External_Loop_Tolerance).ToDoubleFromInvariant
            maxit_e = Me.FlashSettings(Interfaces.Enums.FlashSetting.PTFlash_Maximum_Number_Of_External_Iterations)
            itol = Me.FlashSettings(Interfaces.Enums.FlashSetting.PTFlash_Internal_Loop_Tolerance).ToDoubleFromInvariant
            maxit_i = Me.FlashSettings(Interfaces.Enums.FlashSetting.PTFlash_Maximum_Number_Of_Internal_Iterations)

            n = Vz.Length - 1

            PP = PP
            Vf = V
            L = 1 - V
            Lf = 1 - Vf
            Pf = P

            ReDim Vn(n), Vx(n), Vy(n), Vx_ant(n), Vy_ant(n), Vp(n), Ki(n), fi(n)
            Dim dFdP As Double

            Dim VTc = PP.RET_VTC()

            Vn = PP.RET_VNAMES()
            fi = Vz.Clone

            If Pref = 0.0# Then

                i = 0
                Do
                    Vp(i) = PP.AUX_PVAPi(i, T)
                    i += 1
                Loop Until i = n + 1

                Pmin = Vp.Min
                Pmax = Vp.Max

                Pref = Pmin + (1 - V) * (Pmax - Pmin)

            Else

                Pmin = Pref * 0.8
                Pmax = Pref * 1.2

            End If

            P = Pref

            'Calculate Ki`s

            If Not ReuseKI Then
                i = 0
                Do
                    Vp(i) = PP.AUX_PVAPi(i, T)
                    Ki(i) = Vp(i) / P
                    If Double.IsNaN(Ki(i)) Or Double.IsInfinity(Ki(i)) Then Ki(i) = 1.0E+20
                    i += 1
                Loop Until i = n + 1
            Else
                If Not PP.AUX_CheckTrivial(PrevKi) Then
                    For i = 0 To n
                        Vp(i) = PP.AUX_PVAPi(i, T)
                        Ki(i) = PrevKi(i)
                        If Double.IsNaN(Ki(i)) Or Double.IsInfinity(Ki(i)) Then Ki(i) = 1.0E+20
                    Next
                Else
                    i = 0
                    Do
                        IObj?.SetCurrent
                        Vp(i) = PP.AUX_PVAPi(i, T)
                        Ki(i) = Vp(i) / P
                        If Double.IsNaN(Ki(i)) Or Double.IsInfinity(Ki(i)) Then Ki(i) = 1.0E+20
                        i += 1
                    Loop Until i = n + 1
                End If
            End If

            IObj?.Paragraphs.Add(String.Format("Initial estimates for P: {0} K", P))
            IObj?.Paragraphs.Add(String.Format("Initial estimates for K: {0}", Ki.ToMathArrayString))

            i = 0
            Do
                If Vz(i) <> 0 Then
                    Vy(i) = Vz(i) * Ki(i) / ((Ki(i) - 1) * V + 1)
                    Vx(i) = Vy(i) / Ki(i)
                Else
                    Vy(i) = 0
                    Vx(i) = 0
                End If
                i += 1
            Loop Until i = n + 1

            i = 0
            soma_x = 0
            soma_y = 0
            Do
                soma_x = soma_x + Vx(i)
                soma_y = soma_y + Vy(i)
                i = i + 1
            Loop Until i = n + 1
            i = 0
            Do
                Vx(i) = Vx(i) / soma_x
                Vy(i) = Vy(i) / soma_y
                i = i + 1
            Loop Until i = n + 1

            IObj?.Paragraphs.Add(String.Format("Initial estimates for x: {0}", Vx.ToMathArrayString))
            IObj?.Paragraphs.Add(String.Format("Initial estimates for y: {0}", Vy.ToMathArrayString))

            If PP.AUX_IS_SINGLECOMP(Vz) Then
                WriteDebugInfo("TV Flash [NL]: Converged in 1 iteration.")
                P = 0
                For i = 0 To n
                    IObj?.SetCurrent
                    P += Vz(i) * PP.AUX_PVAPi(i, T)
                Next
                IObj?.Close()
                Return New Object() {L, V, Vx, Vy, P, 0, Ki, 0.0#, PP.RET_NullVector, 0.0#, PP.RET_NullVector}
            End If

            Dim marcador3, marcador2, marcador As Integer
            Dim stmp4_ant, stmp4, Pant, fval As Double
            Dim chk As Boolean = False

            If V = 1.0# Or V = 0.0# Then

                If V = 1.0 Then
                    IObj?.Paragraphs.Add("This is a dew point calculation (V = 1).")
                Else
                    IObj?.Paragraphs.Add("This is a bubble point calculation (V = 0).")
                End If

                ecount = 0
                Do

                    IObj?.SetCurrent

                    Dim IObj2 As Inspector.InspectorItem = Inspector.Host.GetNewInspectorItem()

                    Inspector.Host.CheckAndAdd(IObj2, "", "Flash_TV", "TV Flash Newton Iteration", "Temperature-Vapor Fraction Flash Algorithm Convergence Iteration Step")

                    IObj2?.Paragraphs.Add(String.Format("This is the Newton convergence loop iteration #{0}. DWSIM will use the current values of P, y and x to calculate fugacity coefficients and update K using the Property Package rigorous models.", ecount))

                    IObj2?.SetCurrent()

                    IObj2?.Paragraphs.Add(String.Format("Tentative pressure value: {0} K", P))

                    marcador3 = 0

                    Dim cont_int = 0
                    Do

                        IObj2?.SetCurrent

                        Dim IObj3 As Inspector.InspectorItem = Inspector.Host.GetNewInspectorItem()

                        Inspector.Host.CheckAndAdd(IObj3, "", "Flash_TV", "TV Flash Inner Iteration", "Temperature-Vapor Fraction Flash Algorithm Convergence Inner Iteration Step")

                        IObj3?.Paragraphs.Add(String.Format("This is the inner convergence loop iteration #{0}. DWSIM will use the current value of P to converge x and y.", ecount))

                        IObj3?.SetCurrent()

                        IObj3?.Paragraphs.Add(String.Format("Tentative value for K: {0}", Ki.ToMathArrayString))

                        Ki = PP.DW_CalcKvalue(Vx, Vy, T, P)

                        marcador = 0
                        If stmp4_ant <> 0 Then
                            marcador = 1
                        End If
                        stmp4_ant = stmp4

                        If V = 0 Then
                            i = 0
                            stmp4 = 0
                            Do
                                stmp4 = stmp4 + Ki(i) * Vx(i)
                                i = i + 1
                            Loop Until i = n + 1
                        Else
                            i = 0
                            stmp4 = 0
                            Do
                                stmp4 = stmp4 + Vy(i) / Ki(i)
                                i = i + 1
                            Loop Until i = n + 1
                        End If

                        If V = 0 Then
                            i = 0
                            Do
                                Vy_ant(i) = Vy(i)
                                Vy(i) = Ki(i) * Vx(i) / stmp4
                                i = i + 1
                            Loop Until i = n + 1
                        Else
                            i = 0
                            Do
                                Vx_ant(i) = Vx(i)
                                Vx(i) = (Vy(i) / Ki(i)) / stmp4
                                i = i + 1
                            Loop Until i = n + 1
                        End If

                        marcador2 = 0
                        If marcador = 1 Then
                            If V = 0 Then
                                If Math.Abs(Vy(0) - Vy_ant(0)) < itol Then
                                    marcador2 = 1
                                End If
                            Else
                                If Math.Abs(Vx(0) - Vx_ant(0)) < itol Then
                                    marcador2 = 1
                                End If
                            End If
                        End If

                        cont_int = cont_int + 1

                        IObj3?.Paragraphs.Add(String.Format("Updated x: {0}", Vx.ToMathArrayString))
                        IObj3?.Paragraphs.Add(String.Format("Updated y: {0}", Vy.ToMathArrayString))

                        IObj3?.Close()

                    Loop Until marcador2 = 1 Or Double.IsNaN(stmp4) Or cont_int > maxit_i

                    IObj2?.Paragraphs.Add(String.Format("Updated x: {0}", Vx.ToMathArrayString))
                    IObj2?.Paragraphs.Add(String.Format("Updated y: {0}", Vy.ToMathArrayString))

                    Dim K1(n), K2(n), dKdP(n) As Double

                    IObj?.SetCurrent
                    K1 = PP.DW_CalcKvalue(Vx, Vy, T, P)
                    IObj?.SetCurrent
                    K2 = PP.DW_CalcKvalue(Vx, Vy, T, P * 1.001)

                    For i = 0 To n
                        dKdP(i) = (K2(i) - K1(i)) / (0.001 * P)
                    Next

                    IObj2?.Paragraphs.Add(String.Format("K: {0}", Ki.ToMathArrayString))

                    IObj2?.Paragraphs.Add(String.Format("dK/dP: {0}", dKdP.ToMathArrayString))

                    fval = stmp4 - 1

                    ecount += 1

                    i = 0
                    dFdP = 0
                    Do
                        If V = 0 Then
                            dFdP = dFdP + Vx(i) * dKdP(i)
                        Else
                            dFdP = dFdP - Vy(i) / (Ki(i) ^ 2) * dKdP(i)
                        End If
                        i = i + 1
                    Loop Until i = n + 1

                    If (P - fval / dFdP) < 0 Then

                        P = (P + Pant) / 2

                    Else

                        Pant = P

                        deltaP = -fval / dFdP

                        If Abs(deltaP) < etol / 1000 And ecount > 5 Then Exit Do

                        If Abs(deltaP) > 0.1 * P And ecount < 5 Then
                            P = P + Sign(deltaP) * 0.1 * P
                        Else
                            P = P + deltaP
                        End If

                    End If

                    IObj2?.Paragraphs.Add(String.Format("Pressure error: {0} K", deltaP))

                    IObj2?.Paragraphs.Add(String.Format("Updated Pressure: {0} K", P))

                    WriteDebugInfo("TV Flash [NL]: Iteration #" & ecount & ", P = " & P & ", VF = " & V)

                    If Not PP.CurrentMaterialStream.Flowsheet Is Nothing Then PP.CurrentMaterialStream.Flowsheet.CheckStatus()

                    IObj2?.Close()

                Loop Until Math.Abs(fval) < etol Or Double.IsNaN(P) = True Or ecount > maxit_e

            Else

                ecount = 0

                IObj?.SetCurrent

                Dim IObj2 As Inspector.InspectorItem = Inspector.Host.GetNewInspectorItem()

                Inspector.Host.CheckAndAdd(IObj2, "", "Flash_TV", "TV Flash Newton Iteration", "Temperature-Vapor Fraction Flash Algorithm Convergence Iteration Step")

                IObj2?.Paragraphs.Add(String.Format("This is the Newton convergence loop iteration #{0}. DWSIM will use the current values of P, y and x to calculate fugacity coefficients and update K using the Property Package rigorous models.", ecount))

                IObj2?.SetCurrent()

                IObj2?.Paragraphs.Add(String.Format("Tentative temperature value: {0} K", T))

                Do

                    IObj2?.SetCurrent

                    Dim IObj3 As Inspector.InspectorItem = Inspector.Host.GetNewInspectorItem()

                    Inspector.Host.CheckAndAdd(IObj3, "", "Flash_TV", "TV Flash Inner Iteration", "Temperature-Vapor Fraction Flash Algorithm Convergence Inner Iteration Step")

                    IObj3?.Paragraphs.Add(String.Format("This is the inner convergence loop iteration #{0}. DWSIM will use the current value of P to converge x and y.", ecount))

                    IObj3?.SetCurrent()

                    IObj3?.Paragraphs.Add(String.Format("Tentative value for K: {0}", Ki.ToMathArrayString))

                    Ki = PP.DW_CalcKvalue(Vx, Vy, T, P)

                    i = 0
                    Do
                        If Vz(i) <> 0 Then
                            Vy_ant(i) = Vy(i)
                            Vx_ant(i) = Vx(i)
                            Vy(i) = Vz(i) * Ki(i) / ((Ki(i) - 1) * V + 1)
                            Vx(i) = Vy(i) / Ki(i)
                        Else
                            Vy(i) = 0
                            Vx(i) = 0
                        End If
                        i += 1
                    Loop Until i = n + 1
                    i = 0
                    soma_x = 0
                    soma_y = 0
                    Do
                        soma_x = soma_x + Vx(i)
                        soma_y = soma_y + Vy(i)
                        i = i + 1
                    Loop Until i = n + 1
                    i = 0
                    Do
                        Vx(i) = Vx(i) / soma_x
                        Vy(i) = Vy(i) / soma_y
                        i = i + 1
                    Loop Until i = n + 1

                    IObj2?.Paragraphs.Add(String.Format("Updated x: {0}", Vx.ToMathArrayString))
                    IObj2?.Paragraphs.Add(String.Format("Updated y: {0}", Vy.ToMathArrayString))

                    If V <= 0.5 Then

                        i = 0
                        stmp4 = 0
                        Do
                            stmp4 = stmp4 + Ki(i) * Vx(i)
                            i = i + 1
                        Loop Until i = n + 1

                        Dim K1(n), K2(n), dKdP(n) As Double

                        IObj2?.SetCurrent

                        K1 = PP.DW_CalcKvalue(Vx, Vy, T, P)

                        IObj2?.SetCurrent

                        K2 = PP.DW_CalcKvalue(Vx, Vy, T, P * 1.001)

                        For i = 0 To n
                            dKdP(i) = (K2(i) - K1(i)) / (0.001 * P)
                        Next

                        i = 0
                        dFdP = 0
                        Do
                            dFdP = dFdP + Vx(i) * dKdP(i)
                            i = i + 1
                        Loop Until i = n + 1

                        IObj2?.Paragraphs.Add(String.Format("dK/dP: {0}", dKdP.ToMathArrayString))

                    Else

                        i = 0
                        stmp4 = 0
                        Do
                            stmp4 = stmp4 + Vy(i) / Ki(i)
                            i = i + 1
                        Loop Until i = n + 1

                        Dim K1(n), K2(n), dKdP(n) As Double

                        IObj2?.SetCurrent
                        K1 = PP.DW_CalcKvalue(Vx, Vy, T, P)
                        IObj2?.SetCurrent
                        K2 = PP.DW_CalcKvalue(Vx, Vy, T, P * 1.001)

                        For i = 0 To n
                            dKdP(i) = (K2(i) - K1(i)) / (0.001 * P)
                        Next

                        i = 0
                        dFdP = 0
                        Do
                            dFdP = dFdP - Vy(i) / (Ki(i) ^ 2) * dKdP(i)
                            i = i + 1
                        Loop Until i = n + 1

                        IObj2?.Paragraphs.Add(String.Format("dK/dP: {0}", dKdP.ToMathArrayString))

                    End If

                    ecount += 1

                    fval = stmp4 - 1

                    If (P - fval / dFdP) < 0 Then

                        P = (P + Pant) / 2

                    Else

                        Pant = P

                        deltaP = -fval / dFdP

                        If Abs(deltaP) < etol / 1000 And ecount > 5 Then Exit Do

                        If Abs(deltaP) > 0.1 * P And ecount < 5 Then
                            P = P + Sign(deltaP) * 0.1 * P
                        Else
                            P = P + deltaP
                        End If

                    End If

                    IObj2?.Paragraphs.Add(String.Format("Pressure error: {0} K", deltaP))

                    IObj2?.Paragraphs.Add(String.Format("Updated Pressure: {0} K", P))

                    WriteDebugInfo("TV Flash [NL]: Iteration #" & ecount & ", P = " & P & ", VF = " & V)

                    If Not PP.CurrentMaterialStream.Flowsheet Is Nothing Then PP.CurrentMaterialStream.Flowsheet.CheckStatus()

                    IObj2?.Close()

                Loop Until Math.Abs(fval) < etol Or Double.IsNaN(P) = True Or ecount > maxit_e

            End If

            d2 = Date.Now

            dt = d2 - d1

            If ecount > maxit_e Then
                Dim ex As New Exception(Calculator.GetLocalString("PropPack_FlashMaxIt2") & String.Format(" (T = {0} K, P = {1} Pa, MoleFracs = {2})", T.ToString("N2"), P.ToString("N2"), Vz.ToArrayString()))
                ex.Data.Add("DetailedDescription", "The Flash Algorithm was unable to converge to a solution.")
                ex.Data.Add("UserAction", "Try another Property Package and/or Flash Algorithm.")
                Throw ex
            End If

            If PP.AUX_CheckTrivial(Ki) Then Throw New Exception("TV Flash [NL]: Invalid result: converged to the trivial solution (P = " & P & " ).")

            WriteDebugInfo("TV Flash [NL]: Converged in " & ecount & " iterations. Time taken: " & dt.TotalMilliseconds & " ms.")

            IObj?.Paragraphs.Add("The algorithm converged in " & ecount & " iterations. Time taken: " & dt.TotalMilliseconds & " ms.")

            IObj?.Paragraphs.Add(String.Format("Final converged value for P: {0}", P))

            IObj?.Close()

            If SharedClasses.AI.ConvergenceAssistant.Manager IsNot Nothing Then
                DWSIM.SharedClasses.AI.ConvergenceAssistant.Manager?.StoreData(
                New AI.ConvergenceAssistant.Classes.ConvergenceHelperTrainingData With {
                    .CompoundNames = PP.RET_VNAMES(),
                    .ModelName = PP.ComponentName,
                    .NumberOfCompounds = Ki.Count,
                    .Temperature = T.ToString("F4", CultureInfo.InvariantCulture),
                    .Pressure = P.ToString("F4", CultureInfo.InvariantCulture),
                    .VaporMolarFraction = V.ToString("F4", CultureInfo.InvariantCulture),
                    .Liquid1MolarFlows = Vx.MultiplyConstY(L).ToString("F4"),
                    .VaporMolarFlows = Vy.MultiplyConstY(V).ToString("F4"), .KValuesVL1 = Ki.ToString("F4"), .MixtureMolarFlows = Vz.ToString("F4"),
                    .RequestType = Interfaces.ConvergenceHelperRequestType.TVFlash})
            End If

            Return New Object() {L, V, Vx, Vy, P, ecount, Ki, 0.0#, PP.RET_NullVector, 0.0#, PP.RET_NullVector}

        End Function

        ''' <summary>
        ''' 
        ''' </summary>
        ''' <param name="Vz">Vector of molar fractions</param>
        ''' <param name="P">Pressure in Pa</param>
        ''' <param name="V">Vapor Molar Fraction (V = 0 - bubble point, V = 1 - dew point)</param>
        ''' <param name="Tref">Initial estimate for temperature</param>
        ''' <param name="PP">Property Package object</param>
        ''' <param name="ReuseKI">true to use previous K-values</param>
        ''' <param name="PrevKi">Previous K-values</param>
        ''' <returns></returns>
        Public Overrides Function Flash_PV(ByVal Vz As Double(), ByVal P As Double, ByVal V As Double, ByVal Tref As Double, ByVal PP As PropertyPackages.PropertyPackage, Optional ByVal ReuseKI As Boolean = False, Optional ByVal PrevKi As Double() = Nothing) As Object

            Dim result As Object()
            Dim Kvals As Double()
            Dim trivial As Boolean = False

            Dim estimate As Interfaces.IConvergenceHelperResponse = Nothing

            If Settings.AIAssistedConvergenceLevel = Settings.AIAssistedConvergenceMode.Provide_Initial_Estimates Or
                    Settings.AIAssistedConvergenceLevel = Settings.AIAssistedConvergenceMode.Provide_Initial_Estimates_and_Solutions Then

                estimate = DWSIM.SharedClasses.AI.ConvergenceAssistant.SolutionProvider?.GetSolutionEstimate(
                   New DWSIM.AI.ConvergenceAssistant.Classes.ConvergenceHelperRequest With {
                   .CompoundNames = PP.RET_VNAMES(),
                   .NumberOfCompounds = Vz.Count,
                   .MixtureMolarFlows = Vz,
                   .ModelName = PP.ComponentName,
                   .Pressure = P,
                   .VaporMolarFraction = V,
                   .Temperature = Tref,
                   .RequestType = Interfaces.ConvergenceHelperRequestType.PVFlash
               })

            End If

            If estimate IsNot Nothing And (Settings.AIAssistedConvergenceLevel = Settings.AIAssistedConvergenceMode.Provide_Initial_Estimates Or
                    Settings.AIAssistedConvergenceLevel = Settings.AIAssistedConvergenceMode.Provide_Initial_Estimates_and_Solutions) Then

                result = Flash_PV_1(Vz, P, V, estimate.Temperature, PP, True, estimate.KValuesVL1)
                If result.Count = 1 Then result = Flash_PV_1(Vz, P, V, estimate.Temperature, PP, True, estimate.KValuesVL1, True)

            Else

                result = Flash_PV_1(Vz, P, V, Tref, PP, ReuseKI, PrevKi)
                If result.Count = 1 Then result = Flash_PV_1(Vz, P, V, Tref, PP, ReuseKI, PrevKi, True)

            End If

            'check if solution is valid.

            Dim deltaT As Double = 100

            If result.Count > 1 Then

                deltaT = result(11)

            End If

            If Math.Abs(deltaT) > 0.01 And (V = 0 Or V = 1) Then

                'solution is not valid. 

                If estimate Is Nothing And Settings.AIAssistedConvergenceLevel = Settings.AIAssistedConvergenceMode.Provide_Initial_Estimates_2Pass Or
                        Settings.AIAssistedConvergenceLevel = Settings.AIAssistedConvergenceMode.Provide_Initial_Estimates_and_Solutions_2Pass Then

                    estimate = DWSIM.SharedClasses.AI.ConvergenceAssistant.SolutionProvider?.GetSolutionEstimate(
                       New DWSIM.AI.ConvergenceAssistant.Classes.ConvergenceHelperRequest With {
                       .CompoundNames = PP.RET_VNAMES(),
                       .NumberOfCompounds = Vz.Count,
                       .MixtureMolarFlows = Vz,
                       .ModelName = PP.ComponentName,
                       .Pressure = P,
                       .VaporMolarFraction = V,
                       .Temperature = Tref,
                       .RequestType = Interfaces.ConvergenceHelperRequestType.PVFlash
                   })

                End If

                If estimate IsNot Nothing And (Settings.AIAssistedConvergenceLevel = Settings.AIAssistedConvergenceMode.Provide_Initial_Estimates_2Pass Or
                    Settings.AIAssistedConvergenceLevel = Settings.AIAssistedConvergenceMode.Provide_Initial_Estimates_and_Solutions_2Pass) Then

                    result = Flash_PV_1(Vz, P, V, estimate.Temperature, PP, True, estimate.KValuesVL1)
                    If result.Count = 1 Then result = Flash_PV_1(Vz, P, V, estimate.Temperature, PP, True, estimate.KValuesVL1, True)

                Else

                    Dim Tlist, Plist As New List(Of Double)
                    Dim Pl As Double = 101325
                    Dim deltaPl = (P / 2 - 101325) / 10
                    Dim Tl As Double = 0.0
                    For i = 0 To 10
                        result = Flash_PV_1(Vz, Pl, V, Tl, PP, ReuseKI, PrevKi)
                        If result.Count = 1 Then result = Flash_PV_1(Vz, Pl, V, Tl, PP, ReuseKI, PrevKi, True)
                        If result.Count = 1 Then Exit For
                        Tl = result(4)
                        Kvals = result(6)
                        Tlist.Add(Tl)
                        Plist.Add(Pl)
                        Pl += 101325
                    Next
                    If result.Count > 1 Then
                        'extrapolate Tl
                        Tl = Interpolate.RationalWithPoles(Plist, Tlist).Interpolate(P)
                        result = Flash_PV_1(Vz, P, V, Tl, PP, True, Kvals)
                        If result.Count = 1 Then result = Flash_PV_1(Vz, P, V, Tl, PP, True, Kvals, True)
                        If result.Count > 1 Then
                            deltaT = result(11)
                        Else
                            deltaT = 100
                        End If
                        If Math.Abs(deltaT) > 0.01 Then
                            'try previous calculation mode
                            result = Flash_PV_1(Vz, P, V, Tref, PP, ReuseKI, PrevKi)
                            If result.Count = 1 Then result = Flash_PV_1(Vz, P, V, Tref, PP, ReuseKI, PrevKi, True)
                        End If
                    End If

                End If

            End If

            'check if converged to the trivial solution.

            If result.Count > 1 Then
                Kvals = result(6)
                If PP.AUX_CheckTrivial(Kvals, 0.21) Then trivial = True
            End If

            If result.Count = 1 Or trivial Then
                result = Flash_PV_1(Vz, P, V, 0.0, PP, False, Nothing)
                If result.Count = 1 Then result = Flash_PV_1(Vz, P, V, 0.0, PP, False, Nothing, True)
                If result.Count > 1 Then
                    Kvals = result(6)
                    If PP.AUX_CheckTrivial(Kvals, 0.2) Then trivial = True
                End If
            End If

            If result.Count = 1 And P > 101325 Or trivial Then
                'Try quadratic extrapolation For initial T
                Dim Tlist, Plist As New List(Of Double)
                Dim Pl As Double = 101325
                Dim deltaPl = (P / 2 - 101325) / 10
                Dim Tl As Double = 0.0
                For i = 0 To 10
                    result = Flash_PV_1(Vz, Pl, V, Tl, PP, ReuseKI, PrevKi)
                    If result.Count = 1 Then result = Flash_PV_1(Vz, Pl, V, Tl, PP, ReuseKI, PrevKi, True)
                    If result.Count = 1 Then Exit For
                    Tl = result(4)
                    Kvals = result(6)
                    Tlist.Add(Tl)
                    Plist.Add(Pl)
                    Pl += 101325
                Next
                If result.Count > 1 Then
                    'extrapolate Tl
                    Tl = Interpolate.RationalWithPoles(Plist, Tlist).Interpolate(P)
                    result = Flash_PV_1(Vz, P, V, Tl, PP, True, Kvals)
                    If result.Count = 1 Then result = Flash_PV_1(Vz, P, V, Tl, PP, True, Kvals, True)
                End If
            End If

            Dim idealcalc As Boolean = Me.FlashSettings(Interfaces.Enums.FlashSetting.PVFlash_TryIdealCalcOnFailure)
            If result.Count = 1 And idealcalc Then
                Using IPP As New RaoultPropertyPackage()
                    IPP.CurrentMaterialStream = PP.CurrentMaterialStream
                    result = Flash_PV_1(Vz, P, V, 0.0, IPP, ReuseKI, PrevKi)
                    If result.Count = 1 Then result = Flash_PV_1(Vz, P, V, 0.0, IPP, ReuseKI, PrevKi, True)
                    If result.Count = 1 And V = 0.0 Then
                        result = Flash_PV_4(Vz, P, V, 0.0, IPP, ReuseKI, PrevKi)
                    End If
                End Using
            End If

            If result.Count = 1 Then

                If Settings.AIAssistedConvergenceLevel = Settings.AIAssistedConvergenceMode.Provide_Initial_Estimates_and_Solutions Or
                        Settings.AIAssistedConvergenceLevel = Settings.AIAssistedConvergenceMode.Provide_Solutions Or
                        Settings.AIAssistedConvergenceLevel = Settings.AIAssistedConvergenceMode.Provide_Initial_Estimates_and_Solutions_2Pass Then

                    If estimate IsNot Nothing Then

                        Return New Object() {estimate.Liquid1MolarFlows.Sum,
                            estimate.VaporMolarFlows.Sum,
                            estimate.Liquid1MolarFlows.NormalizeY(),
                            estimate.VaporMolarFlows.NormalizeY(),
                            estimate.Temperature,
                            0, estimate.KValuesVL1,
                            0.0#, PP.RET_NullVector, 0.0#, PP.RET_NullVector, deltaT}

                    Else

                        Throw New Exception(String.Format("{0}: Unable to calculate PV Flash with P = {1} and VF = {2}, molar fractions = {3}",
                                    PP.ComponentName, P, V, Vz.ToArrayString(PP.RET_VNAMES(), "G3")))

                    End If

                Else

                    Throw New Exception(String.Format("{0}: Unable to calculate PV Flash with P = {1} and VF = {2}, molar fractions = {3}",
                                    PP.ComponentName, P, V, Vz.ToArrayString(PP.RET_VNAMES(), "G3")))

                End If

            Else

                'Return New Object() {L, V, Vx, Vy, T, ecount, Ki, 0.0#, PP.RET_NullVector, 0.0#, PP.RET_NullVector, deltaT}

                If SharedClasses.AI.ConvergenceAssistant.Manager IsNot Nothing Then
                    DWSIM.SharedClasses.AI.ConvergenceAssistant.Manager?.StoreData(
                        New AI.ConvergenceAssistant.Classes.ConvergenceHelperTrainingData With {
                        .CompoundNames = PP.RET_VNAMES(), .ModelName = PP.ComponentName, .NumberOfCompounds = Vz.Count,
                        .Temperature = Convert.ToDouble(result(4)).ToString("F4", CultureInfo.InvariantCulture),
                        .Pressure = P.ToString("F4", CultureInfo.InvariantCulture),
                        .VaporMolarFraction = Convert.ToDouble(result(1)).ToString("F4", CultureInfo.InvariantCulture),
                        .Liquid1MolarFlows = DirectCast(result(2), Double()).MultiplyConstY(result(0)).ToString("F4"),
                        .VaporMolarFlows = DirectCast(result(3), Double()).MultiplyConstY(result(1)).ToString("F4"),
                        .KValuesVL1 = DirectCast(result(6), Double()).ToString("F4"), .MixtureMolarFlows = Vz.ToString("F4"),
                        .RequestType = Interfaces.ConvergenceHelperRequestType.PVFlash})
                End If

                Return result

            End If

        End Function

        Public Function Flash_PV_1(ByVal Vz2 As Double(), ByVal P As Double, ByVal V As Double, ByVal Tref As Double, ByVal PP As PropertyPackages.PropertyPackage, Optional ByVal ReuseKI As Boolean = False, Optional ByVal PrevKi As Double() = Nothing, Optional OldTempEstimation As Boolean = False) As Object

            Dim IObj As Inspector.InspectorItem = Inspector.Host.GetNewInspectorItem()

            Inspector.Host.CheckAndAdd(IObj, "", "Flash_PV", Name & " (PV Flash)", "Pressure/Vapor Fraction Flash Algorithm Routine", True)

            IObj?.Paragraphs.Add("This routine calculates the temperature at which the specified mixture composition finds itself in vapor-liquid equilibrium with a vapor phase mole fraction equal to V at the specified P.")

            IObj?.Paragraphs.Add(String.Format("<h2>Input Parameters</h2>"))

            IObj?.Paragraphs.Add(String.Format("Pressure: {0} Pa", P))
            IObj?.Paragraphs.Add(String.Format("Vapor Mole Fraction: {0} ", V))
            IObj?.Paragraphs.Add(String.Format("Compounds: {0}", PP.RET_VNAMES.ToMathArrayString))
            IObj?.Paragraphs.Add(String.Format("Mole Fractions: {0}", Vz2.ToMathArrayString))

            Dim i, n, ecount As Integer
            Dim d1, d2 As Date, dt As TimeSpan
            Dim L, Lf, Vf, T, deltaT, deltaT_ant, epsilon, df, maxdT As Double
            Dim e1 As Double

            d1 = Date.Now

            etol = Me.FlashSettings(Interfaces.Enums.FlashSetting.PTFlash_External_Loop_Tolerance).ToDoubleFromInvariant
            maxit_e = Me.FlashSettings(Interfaces.Enums.FlashSetting.PTFlash_Maximum_Number_Of_External_Iterations)
            itol = Me.FlashSettings(Interfaces.Enums.FlashSetting.PTFlash_Internal_Loop_Tolerance).ToDoubleFromInvariant
            maxit_i = Me.FlashSettings(Interfaces.Enums.FlashSetting.PTFlash_Maximum_Number_Of_Internal_Iterations)

            epsilon = Me.FlashSettings(Interfaces.Enums.FlashSetting.PVFlash_TemperatureDerivativeEpsilon).ToDoubleFromInvariant
            df = Me.FlashSettings(Interfaces.Enums.FlashSetting.PVFlash_FixedDampingFactor).ToDoubleFromInvariant
            maxdT = Me.FlashSettings(Interfaces.Enums.FlashSetting.PVFlash_MaximumTemperatureChange).ToDoubleFromInvariant

            Dim fpstencil As Boolean = FlashSettings(Interfaces.Enums.FlashSetting.PVFlash_FivePointStencilNumericalDerivative)

            n = Vz2.Length - 1

            PP = PP
            Vf = V
            L = 1 - V
            Lf = 1 - Vf

            Dim S, Vz(n), Vs(n), Vx(n), Vy(n), Vx_ant(n), Vy_ant(n), Vp(n), Ki(n), fi(n), dVxy(n) As Double
            Dim Vt(n), VTc(n), dFdT, Tsat(n) As Double

            Vz = Vz2.Clone()

            Dim cprops = PP.DW_GetConstantProperties()

            For i = 0 To n
                Tsat(i) = PP.AUX_TSATi(P, i)
                If cprops(i).IsSolid Or cprops(i).TemperatureOfFusion > 1000.0 Or cprops(i).Normal_Boiling_Point * 0.7 > 1000.0 Then
                    'solid. leave out of the calculation
                    Vs(i) = Vz2(i)
                    Vz(i) = 0.0
                End If
            Next

            S = Vs.SumY()

            If S > 0.0 Then
                Vs = Vs.NormalizeY()
                Vz = Vz.NormalizeY()
            End If

            VTc = PP.RET_VTC()
            fi = Vz.Clone

            If Tref = 0.0# Then
                If OldTempEstimation Then
                    i = 0
                    Tref = 0.0#
                    Do
                        Tref += Vz(i) * PP.AUX_TSATi(P, i)
                        i += 1
                    Loop Until i = n + 1
                Else
                    ' Weighted average as a robust baseline estimate
                    Dim TrefWeighted As Double = 0.0
                    For i = 0 To n
                        TrefWeighted += Vz(i) * Tsat(i)
                    Next

                    If V < 0.5 Then
                        ' Bubble point: start from the lowest Tsat among non-trace components
                        Tref = 5000.0
                        For i = 0 To n
                            If Tsat(i) < Tref And Vz(i) > 0.001 Then Tref = Tsat(i)
                        Next
                        ' Fallback to weighted average if no component passed the threshold
                        If Tref >= 5000.0 Then Tref = TrefWeighted
                    Else
                        ' Dew point: start from the highest Tsat
                        Tref = -1000.0
                        For i = 0 To n
                            If Tsat(i) > Tref And Vz(i) > 0.0 Then Tref = Tsat(i)
                        Next
                        If Tref <= 0.0 Then Tref = TrefWeighted
                    End If
                End If
            End If

            T = Tref

            'Calculate Ki`s

            If Not ReuseKI Then
                i = 0
                Do
                    IObj?.SetCurrent
                    Vp(i) = PP.AUX_PVAPi(i, T)
                    Ki(i) = Vp(i) / P
                    If Double.IsNaN(Ki(i)) Or Double.IsInfinity(Ki(i)) Then Ki(i) = 1.0E+20
                    i += 1
                Loop Until i = n + 1
            Else
                If Not PP.AUX_CheckTrivial(PrevKi) And Not Double.IsNaN(PrevKi(0)) Then
                    For i = 0 To n
                        IObj?.SetCurrent
                        Ki(i) = PrevKi(i)
                        If Double.IsNaN(Ki(i)) Or Double.IsInfinity(Ki(i)) Then Ki(i) = 1.0E+20
                    Next
                Else
                    i = 0
                    Do
                        IObj?.SetCurrent
                        Vp(i) = PP.AUX_PVAPi(i, T)
                        Ki(i) = Vp(i) / P
                        If Double.IsNaN(Ki(i)) Or Double.IsInfinity(Ki(i)) Then Ki(i) = 1.0E+20
                        i += 1
                    Loop Until i = n + 1
                End If
            End If

            IObj?.Paragraphs.Add(String.Format("Initial estimates for T: {0} K", T))
            IObj?.Paragraphs.Add(String.Format("Initial estimates for K: {0}", Ki.ToMathArrayString))

            i = 0
            Do
                If Vz(i) <> 0 Then
                    Vy(i) = Vz(i) * Ki(i) / ((Ki(i) - 1) * V + 1)
                    If Double.IsInfinity(Vy(i)) Then Vy(i) = 0.0#
                    Vx(i) = Vy(i) / Ki(i)
                Else
                    Vy(i) = 0
                    Vx(i) = 0
                End If
                i += 1
            Loop Until i = n + 1

            Vx = Vx.NormalizeY()
            Vy = Vy.NormalizeY()

            IObj?.Paragraphs.Add(String.Format("Initial estimates for x: {0}", Vx.ToMathArrayString))
            IObj?.Paragraphs.Add(String.Format("Initial estimates for y: {0}", Vy.ToMathArrayString))

            If PP.AUX_IS_SINGLECOMP(Vz) Then

                WriteDebugInfo("PV Flash [NL]: Converged in 1 iteration.")
                T = 0
                For i = 0 To n
                    IObj?.SetCurrent
                    T += Vz(i) * PP.AUX_TSATi(P, i)
                Next
                IObj?.Close()
                If Vz.Count = 1 Then
                    Vx = New Double() {1.0}
                    Vy = New Double() {1.0}
                    Ki = New Double() {1.0}
                End If

                If S > 0 Then

                    Dim VnL = Vx.MultiplyConstY(L)
                    Dim VnV = Vy.MultiplyConstY(V)
                    Dim VnS = Vs.MultiplyConstY(S)

                    L = VnL.AddY(VnS).SumY

                    Vx = VnL.AddY(VnS).MultiplyConstY(1 / (L + 0.0000000001))

                    For i = 0 To n
                        If Vs(i) > 0.0 Then Ki(i) = 1.0E-20 / Vx(i)
                    Next

                End If

                Return New Object() {L, V, Vx, Vy, T, 0, Ki, 0.0#, PP.RET_NullVector, 0.0#, PP.RET_NullVector, 0.0}

            End If

            Dim marcador3, marcador2, marcador As Integer
            Dim stmp4_ant, stmp4, Tant, fval, fval_ant As Double

            Dim K1(n), K2(n), K3(n), K4(n), dKdT(n) As Double

            Dim xvals, fvals As New List(Of Double)

            If V = 1.0# Or V = 0.0# Then

                If V = 1.0 Then
                    IObj?.Paragraphs.Add("This is a dew point calculation (V = 1).")
                Else
                    IObj?.Paragraphs.Add("This is a bubble point calculation (V = 0).")
                End If

                ecount = 0
                Do

                    IObj?.SetCurrent

                    Dim IObj2 As Inspector.InspectorItem = Inspector.Host.GetNewInspectorItem()

                    Inspector.Host.CheckAndAdd(IObj2, "", "Flash_PV", "PV Flash Newton Iteration", "Pressure-Vapor Fraction Flash Algorithm Convergence Iteration Step")

                    IObj2?.Paragraphs.Add(String.Format("This is the Newton convergence loop iteration #{0}. DWSIM will use the current values of T, y and x to calculate fugacity coefficients and update K using the Property Package rigorous models.", ecount))

                    IObj2?.SetCurrent()

                    IObj2?.Paragraphs.Add(String.Format("Tentative temperature value: {0} K", T))

                    marcador3 = 0

                    Dim cont_int = 0
                    Do

                        IObj2?.SetCurrent

                        Dim IObj3 As Inspector.InspectorItem = Inspector.Host.GetNewInspectorItem()

                        Inspector.Host.CheckAndAdd(IObj3, "", "Flash_PV", "PV Flash Inner Iteration", "Pressure-Vapor Fraction Flash Algorithm Convergence Inner Iteration Step")

                        IObj3?.Paragraphs.Add(String.Format("This is the inner convergence loop iteration #{0}. DWSIM will use the current value of T to converge x and y.", ecount))

                        IObj3?.SetCurrent()

                        IObj3?.Paragraphs.Add(String.Format("Tentative value for K: {0}", Ki.ToMathArrayString))

                        If PP.ShouldUseKvalueMethod2 Then
                            Ki = PP.DW_CalcKvalue(Vx.MultiplyConstY(L).AddY(Vy.MultiplyConstY(V)), T, P)
                        Else
                            Ki = PP.DW_CalcKvalue(Vx, Vy, T, P)
                        End If

                        marcador = 0
                        If Math.Abs(stmp4_ant) > 1.0E-20 Then marcador = 1

                        stmp4_ant = stmp4

                        If V = 0.0 Then
                            stmp4 = Ki.MultiplyY(Vx).SumY
                        Else
                            stmp4 = Vy.DivideY(Ki).SumY
                        End If

                        If V = 0.0 Then
                            Vy_ant = Vy.Clone
                            Vy = Ki.MultiplyY(Vx).MultiplyConstY(1.0 / stmp4)
                        Else
                            Vx_ant = Vx.Clone
                            Vx = Vy.DivideY(Ki).MultiplyConstY(1.0 / stmp4)
                        End If

                        marcador2 = 0
                        If marcador = 1 Then
                            If V = 0.0 Then
                                If Vy.SubtractY(Vy_ant).AbsSumY < 0.001 Then
                                    marcador2 = 1
                                End If
                            Else
                                If Vx.SubtractY(Vx_ant).AbsSumY < 0.001 Then
                                    marcador2 = 1
                                End If
                            End If
                        End If

                        cont_int = cont_int + 1

                        IObj3?.Paragraphs.Add(String.Format("Updated x: {0}", Vx.ToMathArrayString))
                        IObj3?.Paragraphs.Add(String.Format("Updated y: {0}", Vy.ToMathArrayString))

                        IObj3?.Close()

                    Loop Until marcador2 = 1 Or Double.IsNaN(stmp4) Or cont_int > maxit_i

                    IObj2?.Paragraphs.Add(String.Format("Updated x: {0}", Vx.ToMathArrayString))
                    IObj2?.Paragraphs.Add(String.Format("Updated y: {0}", Vy.ToMathArrayString))

                    If PP.ImplementsAnalyticalDerivatives Then
                        dKdT = PP.DW_CalcdKdT(Vx, Vy, T, P)
                    Else
                        If Settings.EnableParallelProcessing Then
                            If fpstencil Then
                                Dim task1 = TaskHelper.Run(Sub()
                                                               If PP.ShouldUseKvalueMethod2 Then
                                                                   K1 = PP.DW_CalcKvalue(Vx.MultiplyConstY(L).AddY(Vy.MultiplyConstY(V)), T - 2 * epsilon, P)
                                                               Else
                                                                   K1 = PP.DW_CalcKvalue(Vx, Vy, T - 2 * epsilon, P)
                                                               End If
                                                           End Sub, Settings.TaskCancellationTokenSource.Token)
                                Dim task2 = TaskHelper.Run(Sub()
                                                               If PP.ShouldUseKvalueMethod2 Then
                                                                   K2 = PP.DW_CalcKvalue(Vx.MultiplyConstY(L).AddY(Vy.MultiplyConstY(V)), T - epsilon, P)
                                                               Else
                                                                   K2 = PP.DW_CalcKvalue(Vx, Vy, T - epsilon, P)
                                                               End If
                                                           End Sub, Settings.TaskCancellationTokenSource.Token)
                                Dim task3 = TaskHelper.Run(Sub()
                                                               If PP.ShouldUseKvalueMethod2 Then
                                                                   K3 = PP.DW_CalcKvalue(Vx.MultiplyConstY(L).AddY(Vy.MultiplyConstY(V)), T + epsilon, P)
                                                               Else
                                                                   K3 = PP.DW_CalcKvalue(Vx, Vy, T + epsilon, P)
                                                               End If
                                                           End Sub, Settings.TaskCancellationTokenSource.Token)
                                Dim task4 = TaskHelper.Run(Sub()
                                                               If PP.ShouldUseKvalueMethod2 Then
                                                                   K4 = PP.DW_CalcKvalue(Vx.MultiplyConstY(L).AddY(Vy.MultiplyConstY(V)), T + 2 * epsilon, P)
                                                               Else
                                                                   K4 = PP.DW_CalcKvalue(Vx, Vy, T + 2 * epsilon, P)
                                                               End If
                                                           End Sub, Settings.TaskCancellationTokenSource.Token)
                                Task.WaitAll(task1, task2, task3, task4)
                                dKdT = K1.AddY(K2.MultiplyConstY(-8)).AddY(K3.MultiplyConstY(8).AddY(K4.MultiplyConstY(-1))).MultiplyConstY(1 / (12 * epsilon))
                            Else
                                Dim task1 = TaskHelper.Run(Sub()
                                                               If PP.ShouldUseKvalueMethod2 Then
                                                                   K1 = PP.DW_CalcKvalue(Vx.MultiplyConstY(L).AddY(Vy.MultiplyConstY(V)), T - epsilon, P)
                                                               Else
                                                                   K1 = PP.DW_CalcKvalue(Vx, Vy, T - epsilon, P)
                                                               End If
                                                           End Sub, Settings.TaskCancellationTokenSource.Token)
                                Dim task2 = TaskHelper.Run(Sub()
                                                               If PP.ShouldUseKvalueMethod2 Then
                                                                   K2 = PP.DW_CalcKvalue(Vx.MultiplyConstY(L).AddY(Vy.MultiplyConstY(V)), T + epsilon, P)
                                                               Else
                                                                   K2 = PP.DW_CalcKvalue(Vx, Vy, T + epsilon, P)
                                                               End If
                                                           End Sub, Settings.TaskCancellationTokenSource.Token)
                                Task.WaitAll(task1, task2)
                                dKdT = K2.SubtractY(K1).MultiplyConstY(1 / (2 * epsilon))
                            End If
                        Else
                            IObj?.SetCurrent
                            If PP.ShouldUseKvalueMethod2 Then
                                K1 = PP.DW_CalcKvalue(Vx.MultiplyConstY(L).AddY(Vy.MultiplyConstY(V)), T - epsilon, P)
                            Else
                                K1 = PP.DW_CalcKvalue(Vx, Vy, T - epsilon, P)
                            End If
                            IObj?.SetCurrent
                            If PP.ShouldUseKvalueMethod2 Then
                                K2 = PP.DW_CalcKvalue(Vx.MultiplyConstY(L).AddY(Vy.MultiplyConstY(V)), T + epsilon, P)
                            Else
                                K2 = PP.DW_CalcKvalue(Vx, Vy, T + epsilon, P)
                            End If
                            dKdT = K2.SubtractY(K1).MultiplyConstY(1 / (2 * epsilon))
                        End If
                    End If

                    IObj2?.Paragraphs.Add(String.Format("K: {0}", Ki.ToMathArrayString))

                    IObj2?.Paragraphs.Add(String.Format("dK/dT: {0}", dKdT.ToMathArrayString))

                    fval_ant = fval
                    fval = stmp4 - 1

                    xvals.Add(T)
                    fvals.Add(fval)

                    If Math.Abs(fval) < etol And ecount > 5 Then
                        Exit Do
                    End If

                    ecount += 1

                    If V = 0 Then
                        dFdT = Vx.MultiplyY(dKdT).SumY
                    Else
                        dFdT = -Vy.DivideY(Ki).DivideY(Ki).MultiplyY(dKdT).SumY
                    End If

                    Tant = T
                    deltaT_ant = deltaT

                    ' Adaptive damping: reduce damping factor when oscillation is detected (sign change)
                    Dim currentDf As Double = df
                    If ecount > 1 AndAlso Math.Sign(fval) <> Math.Sign(fval_ant) Then
                        currentDf = df * 0.5
                    End If

                    deltaT = -currentDf * fval / dFdT

                    ' Progressive deltaT limiting: scale max step down with iteration count
                    Dim currentMaxdT As Double = maxdT / (1.0 + Math.Floor(ecount / 10.0))
                    If Math.Abs(deltaT) > currentMaxdT Then
                        deltaT = Math.Sign(deltaT) * currentMaxdT
                    End If

                    IObj2?.Paragraphs.Add(String.Format("Temperature error: {0} K", deltaT))

                    For i = 0 To n
                        dVxy(i) = Math.Abs(Vx(i) - Vy(i))
                    Next

                    If dVxy.Sum < 0.01 * (n + 1) And ecount > 20 And Not CalculatingAzeotrope Then
                        'azeotrope detected
                        If Vx.Length = 2 Then
                            'binary azeotrope - use interpolation method
                            T = Flash_PV_Azeotrope_Temperature(Vz, P, V, Tref, PP, ReuseKI, PrevKi)
                            If V = 0 Then
                                Vy = Vx.Clone()
                            Else
                                Vx = Vy.Clone()
                            End If
                            deltaT = 0
                            Exit Do
                        ElseIf xvals.Count >= 2 Then
                            'multicomponent azeotrope - use Brent bracketing on accumulated data
                            Dim Tmin As Double = xvals.Min
                            Dim Tmax As Double = xvals.Max
                            If Tmin < Tmax Then
                                Dim bmin As New Brent
                                T = bmin.BrentOpt2(Tmin, Tmax, 500, etol, 100,
                                    Function(tval)
                                        Dim Kitmp = PP.DW_CalcKvalue(Vx, Vy, tval, P)
                                        If V = 0 Then
                                            Return Kitmp.MultiplyY(Vx).SumY - 1.0
                                        Else
                                            Return Vy.DivideY(Kitmp).SumY - 1.0
                                        End If
                                    End Function)
                                Ki = PP.DW_CalcKvalue(Vx, Vy, T, P)
                                If V = 0 Then
                                    Vy = Ki.MultiplyY(Vx).NormalizeY()
                                Else
                                    Vx = Vy.DivideY(Ki).NormalizeY()
                                End If
                                deltaT = 0
                                Exit Do
                            End If
                        End If
                    End If

                    If Double.IsNaN(fval) Then
                        IObj?.Close()
                        Return New Object() {-1}
                    End If

                    If ecount > 30 And Math.Sign(fval) <> Math.Sign(fval_ant) Then

                        'oscillating around the solution - use Brent with rigorous K-value evaluation

                        If xvals.Count >= 2 Then
                            Dim Tmin As Double = xvals.Min
                            Dim Tmax As Double = xvals.Max

                            If Tmin < Tmax Then
                                Dim bmin As New Brent
                                T = bmin.BrentOpt2(Tmin, Tmax, 500, etol, 100,
                                    Function(tval)
                                        Dim Kitmp = PP.DW_CalcKvalue(Vx, Vy, tval, P)
                                        If V = 0 Then
                                            Return Kitmp.MultiplyY(Vx).SumY - 1.0
                                        Else
                                            Return Vy.DivideY(Kitmp).SumY - 1.0
                                        End If
                                    End Function)
                            End If
                        End If

                        Ki = PP.DW_CalcKvalue(Vx, Vy, T, P)

                        If V = 0.0 Then
                            Vy = Ki.MultiplyY(Vx).NormalizeY()
                        Else
                            Vx = Vy.DivideY(Ki).NormalizeY()
                        End If

                        deltaT = 0

                        Exit Do

                    Else

                        T = T + deltaT

                    End If

                    IObj2?.Paragraphs.Add(String.Format("Updated Temperature: {0} K", T))

                    WriteDebugInfo("PV Flash [NL]: Iteration #" & ecount & ", T = " & T & ", VF = " & V)

                    IObj2?.Close()

                    ' Clamp temperature to a reasonable minimum instead of aborting
                    If T < 50.0 Then
                        T = 50.0
                    End If

                Loop Until Double.IsNaN(T) = True Or ecount > maxit_e

            Else

                ecount = 0

                IObj?.SetCurrent

                Dim IObj2 As Inspector.InspectorItem = Inspector.Host.GetNewInspectorItem()

                Inspector.Host.CheckAndAdd(IObj2, "", "Flash_PV", "PV Flash Newton Iteration", "Pressure-Vapor Fraction Flash Algorithm Convergence Iteration Step")

                IObj2?.Paragraphs.Add(String.Format("This is the Newton convergence loop iteration #{0}. DWSIM will use the current values of T, y and x to calculate fugacity coefficients and update K using the Property Package rigorous models.", ecount))

                IObj2?.SetCurrent()

                IObj2?.Paragraphs.Add(String.Format("Tentative temperature value: {0} K", T))

                Do

                    IObj2?.SetCurrent

                    Dim IObj3 As Inspector.InspectorItem = Inspector.Host.GetNewInspectorItem()

                    Inspector.Host.CheckAndAdd(IObj3, "", "Flash_PV", "PV Flash Inner Iteration", "Pressure-Vapor Fraction Flash Algorithm Convergence Inner Iteration Step")

                    IObj3?.Paragraphs.Add(String.Format("This is the inner convergence loop iteration #{0}. DWSIM will use the current value of T to converge x and y.", ecount))

                    IObj3?.SetCurrent()

                    IObj3?.Paragraphs.Add(String.Format("Tentative value for K: {0}", Ki.ToMathArrayString))

                    Ki = PP.DW_CalcKvalue(Vx, Vy, T, P)

                    i = 0
                    Do
                        If Vz(i) <> 0 Then
                            Vy_ant(i) = Vy(i)
                            Vx_ant(i) = Vx(i)
                            Vy(i) = Vz(i) * Ki(i) / ((Ki(i) - 1) * V + 1)
                            Vx(i) = Vy(i) / Ki(i)
                        Else
                            Vy(i) = 0
                            Vx(i) = 0
                        End If
                        i += 1
                    Loop Until i = n + 1

                    Vx = Vx.NormalizeY()
                    Vy = Vy.NormalizeY()

                    IObj2?.Paragraphs.Add(String.Format("Updated x: {0}", Vx.ToMathArrayString))
                    IObj2?.Paragraphs.Add(String.Format("Updated y: {0}", Vy.ToMathArrayString))

                    If V <= 0.5 Then

                        stmp4 = Ki.MultiplyY(Vx).SumY

                    Else

                        stmp4 = Vy.DivideY(Ki).SumY

                    End If

                    If PP.ImplementsAnalyticalDerivatives Then
                        dKdT = PP.DW_CalcdKdT(Vx, Vy, T, P)
                    Else
                        If Settings.EnableParallelProcessing Then
                            If fpstencil Then
                                Dim task1 = TaskHelper.Run(Sub()
                                                               If PP.ShouldUseKvalueMethod2 Then
                                                                   K1 = PP.DW_CalcKvalue(Vx.MultiplyConstY(L).AddY(Vy.MultiplyConstY(V)), T - 2 * epsilon, P)
                                                               Else
                                                                   K1 = PP.DW_CalcKvalue(Vx, Vy, T - 2 * epsilon, P)
                                                               End If
                                                           End Sub, Settings.TaskCancellationTokenSource.Token)
                                Dim task2 = TaskHelper.Run(Sub()
                                                               If PP.ShouldUseKvalueMethod2 Then
                                                                   K2 = PP.DW_CalcKvalue(Vx.MultiplyConstY(L).AddY(Vy.MultiplyConstY(V)), T - epsilon, P)
                                                               Else
                                                                   K2 = PP.DW_CalcKvalue(Vx, Vy, T - epsilon, P)
                                                               End If
                                                           End Sub, Settings.TaskCancellationTokenSource.Token)
                                Dim task3 = TaskHelper.Run(Sub()
                                                               If PP.ShouldUseKvalueMethod2 Then
                                                                   K3 = PP.DW_CalcKvalue(Vx.MultiplyConstY(L).AddY(Vy.MultiplyConstY(V)), T + epsilon, P)
                                                               Else
                                                                   K3 = PP.DW_CalcKvalue(Vx, Vy, T + epsilon, P)
                                                               End If
                                                           End Sub, Settings.TaskCancellationTokenSource.Token)
                                Dim task4 = TaskHelper.Run(Sub()
                                                               If PP.ShouldUseKvalueMethod2 Then
                                                                   K4 = PP.DW_CalcKvalue(Vx.MultiplyConstY(L).AddY(Vy.MultiplyConstY(V)), T + 2 * epsilon, P)
                                                               Else
                                                                   K4 = PP.DW_CalcKvalue(Vx, Vy, T + 2 * epsilon, P)
                                                               End If
                                                           End Sub, Settings.TaskCancellationTokenSource.Token)
                                Task.WaitAll(task1, task2, task3, task4)
                                dKdT = K1.AddY(K2.MultiplyConstY(-8)).AddY(K3.MultiplyConstY(8).AddY(K4.MultiplyConstY(-1))).MultiplyConstY(1 / (12 * epsilon))
                            Else
                                Dim task1 = TaskHelper.Run(Sub()
                                                               If PP.ShouldUseKvalueMethod2 Then
                                                                   K1 = PP.DW_CalcKvalue(Vx.MultiplyConstY(L).AddY(Vy.MultiplyConstY(V)), T - epsilon, P)
                                                               Else
                                                                   K1 = PP.DW_CalcKvalue(Vx, Vy, T - epsilon, P)
                                                               End If
                                                           End Sub, Settings.TaskCancellationTokenSource.Token)
                                Dim task2 = TaskHelper.Run(Sub()
                                                               If PP.ShouldUseKvalueMethod2 Then
                                                                   K2 = PP.DW_CalcKvalue(Vx.MultiplyConstY(L).AddY(Vy.MultiplyConstY(V)), T + epsilon, P)
                                                               Else
                                                                   K2 = PP.DW_CalcKvalue(Vx, Vy, T + epsilon, P)
                                                               End If
                                                           End Sub, Settings.TaskCancellationTokenSource.Token)
                                Task.WaitAll(task1, task2)
                                dKdT = K2.SubtractY(K1).MultiplyConstY(1 / (2 * epsilon))
                            End If
                        Else
                            IObj?.SetCurrent
                            If PP.ShouldUseKvalueMethod2 Then
                                K1 = PP.DW_CalcKvalue(Vx.MultiplyConstY(L).AddY(Vy.MultiplyConstY(V)), T - epsilon, P)
                            Else
                                K1 = PP.DW_CalcKvalue(Vx, Vy, T - epsilon, P)
                            End If
                            IObj?.SetCurrent
                            If PP.ShouldUseKvalueMethod2 Then
                                K2 = PP.DW_CalcKvalue(Vx.MultiplyConstY(L).AddY(Vy.MultiplyConstY(V)), T + epsilon, P)
                            Else
                                K2 = PP.DW_CalcKvalue(Vx, Vy, T + epsilon, P)
                            End If
                            dKdT = K2.SubtractY(K1).MultiplyConstY(1 / (2 * epsilon))
                        End If
                    End If

                    If V <= 0.5 Then

                        dFdT = Vx.MultiplyY(dKdT).SumY

                        IObj2?.Paragraphs.Add(String.Format("dK/dT: {0}", dKdT.ToMathArrayString))

                    Else

                        dFdT = -Vy.DivideY(Ki).DivideY(Ki).MultiplyY(dKdT).SumY

                        IObj2?.Paragraphs.Add(String.Format("dK/dT: {0}", dKdT.ToMathArrayString))

                    End If

                    ecount += 1

                    fval_ant = fval
                    fval = stmp4 - 1

                    xvals.Add(T)
                    fvals.Add(fval)

                    Tant = T

                    ' Adaptive damping: reduce when oscillation detected
                    Dim currentDf As Double = df
                    If ecount > 1 AndAlso Math.Sign(fval) <> Math.Sign(fval_ant) Then
                        currentDf = df * 0.5
                    End If

                    deltaT = -currentDf * fval / dFdT

                    IObj2?.Paragraphs.Add(String.Format("Temperature error: {0} K", deltaT))

                    If Abs(deltaT) < etol / 1000 And ecount > 5 Then Exit Do

                    ' Progressive deltaT limiting
                    Dim currentMaxdT As Double = maxdT / (1.0 + Math.Floor(ecount / 10.0))
                    If Abs(deltaT) > currentMaxdT Then
                        T = T + Sign(deltaT) * currentMaxdT
                    Else
                        T = T + deltaT
                    End If

                    IObj2?.Paragraphs.Add(String.Format("Updated Temperature: {0} K", T))

                    e1 = Vx.SubtractY(Vx_ant).AbsSumY + Vy.SubtractY(Vy_ant).AbsSumY

                    WriteDebugInfo("PV Flash [NL]: Iteration #" & ecount & ", T = " & T & ", VF = " & V)

                    If Not PP.CurrentMaterialStream.Flowsheet Is Nothing Then PP.CurrentMaterialStream.Flowsheet.CheckStatus()

                    IObj2?.Close()

                    ' Clamp temperature to a reasonable minimum
                    If T < 50.0 Then
                        T = 50.0
                    End If

                    ' Oscillation fallback: use Brent with rigorous evaluation
                    If ecount > 30 And Math.Sign(fval) <> Math.Sign(fval_ant) And xvals.Count >= 2 Then
                        Dim Tmin As Double = xvals.Min
                        Dim Tmax As Double = xvals.Max
                        If Tmin < Tmax Then
                            Dim bmin As New Brent
                            T = bmin.BrentOpt2(Tmin, Tmax, 500, etol, 100,
                                Function(tval)
                                    Dim Kitmp = PP.DW_CalcKvalue(Vx, Vy, tval, P)
                                    If V <= 0.5 Then
                                        Return Kitmp.MultiplyY(Vx).SumY - 1.0
                                    Else
                                        Return Vy.DivideY(Kitmp).SumY - 1.0
                                    End If
                                End Function)
                            Ki = PP.DW_CalcKvalue(Vx, Vy, T, P)
                            i = 0
                            Do
                                If Vz(i) <> 0 Then
                                    Vy(i) = Vz(i) * Ki(i) / ((Ki(i) - 1) * V + 1)
                                    Vx(i) = Vy(i) / Ki(i)
                                End If
                                i += 1
                            Loop Until i = n + 1
                            Vx = Vx.NormalizeY()
                            Vy = Vy.NormalizeY()
                            Exit Do
                        End If
                    End If

                Loop Until (Math.Abs(fval) < etol And e1 < etol) Or Double.IsNaN(T) = True Or ecount > maxit_e

            End If

            d2 = Date.Now

            dt = d2 - d1

            If ecount > maxit_e Then
                IObj?.Close()
                Return New Object() {-1}
            End If

            If PP.AUX_CheckTrivial(Ki) Then
                IObj?.Close()
                Return New Object() {-1}
            End If

            WriteDebugInfo("PV Flash [NL]: Converged in " & ecount & " iterations. Time taken: " & dt.TotalMilliseconds & " ms.")

            IObj?.Paragraphs.Add("The algorithm converged in " & ecount & " iterations. Time taken: " & dt.TotalMilliseconds & " ms.")

            IObj?.Paragraphs.Add(String.Format("Final converged value for T: {0}", T))

            IObj?.Close()

            If S > 0 Then

                Dim VnL = Vx.MultiplyConstY(L)
                Dim VnV = Vy.MultiplyConstY(V)
                Dim VnS = Vs.MultiplyConstY(S)

                L = VnL.AddY(VnS).SumY

                Vx = VnL.AddY(VnS).MultiplyConstY(1 / (L + 0.0000000001))

                For i = 0 To n
                    If Vs(i) > 0.0 Then Ki(i) = 1.0E-20 / Vx(i)
                Next

            End If

            Return New Object() {L, V, Vx, Vy, T, ecount, Ki, 0.0#, PP.RET_NullVector, 0.0#, PP.RET_NullVector, deltaT}

        End Function

        Public Function Flash_PV_Saturated_Newton(ByVal Vz As Double(), ByVal P As Double, ByVal V As Double, ByVal Tref As Double, ByVal PP As PropertyPackages.PropertyPackage, Optional ByVal ReuseKI As Boolean = False, Optional ByVal PrevKi As Double() = Nothing) As Object

            Dim IObj As Inspector.InspectorItem = Inspector.Host.GetNewInspectorItem()

            Inspector.Host.CheckAndAdd(IObj, "", "Flash_PV", Name & " (PV Flash)", "Pressure/Vapor Fraction Flash Algorithm Routine", True)

            IObj?.Paragraphs.Add("This routine calculates the temperature at which the specified mixture composition finds itself in vapor-liquid equilibrium with a vapor phase mole fraction equal to V at the specified P.")

            IObj?.Paragraphs.Add(String.Format("<h2>Input Parameters</h2>"))

            IObj?.Paragraphs.Add(String.Format("Pressure: {0} Pa", P))
            IObj?.Paragraphs.Add(String.Format("Vapor Mole Fraction: {0} ", V))
            IObj?.Paragraphs.Add(String.Format("Compounds: {0}", PP.RET_VNAMES.ToMathArrayString))
            IObj?.Paragraphs.Add(String.Format("Mole Fractions: {0}", Vz.ToMathArrayString))

            Dim i, n, ecount As Integer
            Dim d1, d2 As Date, dt As TimeSpan
            Dim L, Lf, Vf, T, deltaT, deltaT_ant, epsilon, df, maxdT As Double
            Dim e1 As Double

            d1 = Date.Now

            etol = Me.FlashSettings(Interfaces.Enums.FlashSetting.PTFlash_External_Loop_Tolerance).ToDoubleFromInvariant
            maxit_e = Me.FlashSettings(Interfaces.Enums.FlashSetting.PTFlash_Maximum_Number_Of_External_Iterations)
            itol = Me.FlashSettings(Interfaces.Enums.FlashSetting.PTFlash_Internal_Loop_Tolerance).ToDoubleFromInvariant
            maxit_i = Me.FlashSettings(Interfaces.Enums.FlashSetting.PTFlash_Maximum_Number_Of_Internal_Iterations)

            epsilon = Me.FlashSettings(Interfaces.Enums.FlashSetting.PVFlash_TemperatureDerivativeEpsilon).ToDoubleFromInvariant
            df = Me.FlashSettings(Interfaces.Enums.FlashSetting.PVFlash_FixedDampingFactor).ToDoubleFromInvariant
            maxdT = Me.FlashSettings(Interfaces.Enums.FlashSetting.PVFlash_MaximumTemperatureChange).ToDoubleFromInvariant

            n = Vz.Length - 1

            PP = PP
            Vf = V
            L = 1 - V
            Lf = 1 - Vf

            Dim Vx(n), Vy(n), Vx_ant(n), Vy_ant(n), Vp(n), Ki(n) As Double
            Dim Vt(n), VTc(n), dFdT, Tsat(n) As Double

            VTc = PP.RET_VTC()

            If Tref = 0.0# Then
                i = 0
                Tref = 0.0#
                Do
                    Tref += Vz(i) * PP.AUX_TSATi(P, i)
                    i += 1
                Loop Until i = n + 1
            End If

            T = Tref

            'Calculate Ki`s

            If Not ReuseKI Then
                i = 0
                Do
                    IObj?.SetCurrent
                    Vp(i) = PP.AUX_PVAPi(i, T)
                    Ki(i) = Vp(i) / P
                    If Double.IsNaN(Ki(i)) Or Double.IsInfinity(Ki(i)) Then Ki(i) = 1.0E+20
                    i += 1
                Loop Until i = n + 1
            Else
                If Not PP.AUX_CheckTrivial(PrevKi) And Not Double.IsNaN(PrevKi(0)) Then
                    For i = 0 To n
                        IObj?.SetCurrent
                        Ki(i) = PrevKi(i)
                        If Double.IsNaN(Ki(i)) Or Double.IsInfinity(Ki(i)) Then Ki(i) = 1.0E+20
                    Next
                Else
                    i = 0
                    Do
                        IObj?.SetCurrent
                        Vp(i) = PP.AUX_PVAPi(i, T)
                        Ki(i) = Vp(i) / P
                        If Double.IsNaN(Ki(i)) Or Double.IsInfinity(Ki(i)) Then Ki(i) = 1.0E+20
                        i += 1
                    Loop Until i = n + 1
                End If
            End If

            IObj?.Paragraphs.Add(String.Format("Initial estimates for T: {0} K", T))
            IObj?.Paragraphs.Add(String.Format("Initial estimates for K: {0}", Ki.ToMathArrayString))

            i = 0
            Do
                If Vz(i) <> 0 Then
                    Vy(i) = Vz(i) * Ki(i) / ((Ki(i) - 1) * V + 1)
                    If Double.IsInfinity(Vy(i)) Then Vy(i) = 0.0#
                    Vx(i) = Vy(i) / Ki(i)
                Else
                    Vy(i) = 0
                    Vx(i) = 0
                End If
                i += 1
            Loop Until i = n + 1

            Vx = Vx.NormalizeY()
            Vy = Vy.NormalizeY()

            IObj?.Paragraphs.Add(String.Format("Initial estimates for x: {0}", Vx.ToMathArrayString))
            IObj?.Paragraphs.Add(String.Format("Initial estimates for y: {0}", Vy.ToMathArrayString))

            If PP.AUX_IS_SINGLECOMP(Vz) Then
                WriteDebugInfo("PV Flash [NL]: Converged in 1 iteration.")
                T = 0
                For i = 0 To n
                    IObj?.SetCurrent
                    T += Vz(i) * PP.AUX_TSATi(P, i)
                Next
                IObj?.Close()
                If Vz.Count = 1 Then
                    Vx = New Double() {1.0}
                    Vy = New Double() {1.0}
                    Ki = New Double() {1.0}
                End If
                Return New Object() {L, V, Vx, Vy, T, 0, Ki, 0.0#, PP.RET_NullVector, 0.0#, PP.RET_NullVector}
            End If

            Dim xi0(n + 1), lbo(n + 1), ubo(n + 1), fi(n + 1), fugxi(n), fugzi(n), zi(n) As Double
            Dim jac As Double(,)

            If V > 0.0 And V < 1.0 Then

                Throw New Exception("This procedure is for calculation of saturation points only (V = 0 or V = 1).")

            End If

            zi = Vz.Clone()
            For i = 0 To n
                If zi(i) = 0.0 Then zi(i) = 1.0E-20
            Next

            If V = 0 Then
                For i = 0 To n
                    xi0(i) = Vy(i)
                    If xi0(i) = 0.0 Then xi0(i) = 1.0E-20
                    xi0(i) = Log(xi0(i))
                    lbo(i) = Log(1.0E-20)
                    ubo(i) = Log(1.0)
                Next
            Else
                For i = 0 To n
                    xi0(i) = Vx(i)
                    If xi0(i) = 0.0 Then xi0(i) = 1.0E-20
                    xi0(i) = Log(xi0(i))
                    lbo(i) = Log(1.0E-20)
                    ubo(i) = Log(1.0)
                Next
            End If
            xi0(n + 1) = Log(T)
            lbo(n + 1) = Log(T * 0.2)
            ubo(n + 1) = Log(T * 3.0)

            Dim fmin = Function(xi() As Double)

                           If Double.IsNaN(xi.Sum) Then
                               For i = 0 To n
                                   fi(i) = Double.NaN
                               Next
                               Return fi
                           End If

                           T = Exp(xi.Last())

                           Dim xvar = xi.Take(n + 1).ToArray().ExpY()

                           If V = 0 Then
                               fugxi = PP.DW_CalcFugCoeff(xvar.NormalizeY(), T, P, State.Vapor)
                               fugzi = PP.DW_CalcFugCoeff(zi, T, P, State.Liquid)
                           Else
                               fugxi = PP.DW_CalcFugCoeff(xvar.NormalizeY(), T, P, State.Liquid)
                               fugzi = PP.DW_CalcFugCoeff(zi, T, P, State.Vapor)
                           End If
                           For i = 0 To n
                               fi(i) = Log(xvar(i)) + Log(fugxi(i)) - Log(zi(i)) - Log(fugzi(i))
                           Next
                           fi(n + 1) = Log(xvar.Sum())

                           Return fi.AbsSqrSumY()

                       End Function

            Dim ipopt As New Optimization.IPOPTSolver()
            ipopt.Tolerance = 0.0000000001

            Dim xf = ipopt.Solve(fmin, Nothing, xi0, lbo, ubo).ExpY()

            ecount = ipopt.Iterations

            If V = 0 Then
                Vy = xf.Take(n + 1).ToArray()
            Else
                Vx = xf.Take(n + 1).ToArray()
            End If
            T = xf.Last

            Ki = PP.DW_CalcKvalue(Vx, Vy, T, P)

            'jac = newton.Jacobian

            d2 = Date.Now

            dt = d2 - d1

            If ecount > maxit_e Then
                IObj?.Close()
                Return New Object() {-1}
            End If

            If PP.AUX_CheckTrivial(Ki) Then
                IObj?.Close()
                Return New Object() {-1}
            End If

            WriteDebugInfo("PV Flash [NL]: Converged in " & ecount & " iterations. Time taken: " & dt.TotalMilliseconds & " ms.")

            IObj?.Paragraphs.Add("The algorithm converged in " & ecount & " iterations. Time taken: " & dt.TotalMilliseconds & " ms.")

            IObj?.Paragraphs.Add(String.Format("Final converged value for T: {0}", T))

            IObj?.Close()

            Return New Object() {L, V, Vx, Vy, T, ecount, Ki, 0.0#, PP.RET_NullVector, 0.0#, PP.RET_NullVector, jac}

        End Function


        Public Function Flash_PV_Azeotrope_Temperature(ByVal Vz As Double(), ByVal P As Double, ByVal V As Double, ByVal Tref As Double, ByVal PP As PropertyPackages.PropertyPackage, Optional ByVal ReuseKI As Boolean = False, Optional ByVal PrevKi As Double() = Nothing) As Double

            Dim T, dx, validdx As New List(Of Double)
            Dim T0 As Double = Tref
            Dim xaz As Double = Vz(0)

            For i = 0 To 100 Step 5
                dx.Add(i / 100)
            Next

            CalculatingAzeotrope = True

            For Each item In dx
                If Math.Abs(xaz - item) > 0.05 Then
                    Try
                        T.Add(Flash_PV(New Double() {item, 1 - item}, P, V, T0, PP, ReuseKI, PrevKi)(4))
                        T0 = T.Last
                        validdx.Add(item)
                    Catch ex As Exception
                    End Try
                End If
            Next

            CalculatingAzeotrope = False

            Dim Taz = Interpolate.RationalWithPoles(validdx, T).Interpolate(xaz)

            Return Taz

        End Function

        Public Function Flash_PV_4(ByVal Vz As Double(), ByVal P As Double, ByVal V As Double, ByVal Tref As Double, ByVal PP As PropertyPackages.PropertyPackage, Optional ByVal ReuseKI As Boolean = False, Optional ByVal PrevKi As Double() = Nothing) As Object

            Dim IObj As Inspector.InspectorItem = Inspector.Host.GetNewInspectorItem()

            Inspector.Host.CheckAndAdd(IObj, "", "Flash_PV", Name & " (PV Flash)", "Pressure/Vapor Fraction Flash Algorithm Routine", True)

            IObj?.Paragraphs.Add("This routine calculates the temperature at which the specified mixture composition finds itself in vapor-liquid equilibrium with a vapor phase mole fraction equal to V at the specified P.")

            IObj?.Paragraphs.Add(String.Format("<h2>Input Parameters</h2>"))

            IObj?.Paragraphs.Add(String.Format("Pressure: {0} Pa", P))
            IObj?.Paragraphs.Add(String.Format("Vapor Mole Fraction: {0} ", V))
            IObj?.Paragraphs.Add(String.Format("Compounds: {0}", PP.RET_VNAMES.ToMathArrayString))
            IObj?.Paragraphs.Add(String.Format("Mole Fractions: {0}", Vz.ToMathArrayString))

            Dim i, n, ecount As Integer
            Dim d1, d2 As Date, dt As TimeSpan
            Dim L, Lf, Vf, T, epsilon, df, maxdT As Double

            d1 = Date.Now

            etol = Me.FlashSettings(Interfaces.Enums.FlashSetting.PTFlash_External_Loop_Tolerance).ToDoubleFromInvariant
            maxit_e = Me.FlashSettings(Interfaces.Enums.FlashSetting.PTFlash_Maximum_Number_Of_External_Iterations)
            itol = Me.FlashSettings(Interfaces.Enums.FlashSetting.PTFlash_Internal_Loop_Tolerance).ToDoubleFromInvariant
            maxit_i = Me.FlashSettings(Interfaces.Enums.FlashSetting.PTFlash_Maximum_Number_Of_Internal_Iterations)

            epsilon = Me.FlashSettings(Interfaces.Enums.FlashSetting.PVFlash_TemperatureDerivativeEpsilon).ToDoubleFromInvariant
            df = Me.FlashSettings(Interfaces.Enums.FlashSetting.PVFlash_FixedDampingFactor).ToDoubleFromInvariant
            maxdT = Me.FlashSettings(Interfaces.Enums.FlashSetting.PVFlash_MaximumTemperatureChange).ToDoubleFromInvariant

            n = Vz.Length - 1

            PP = PP
            Vf = V
            L = 1 - V
            Lf = 1 - Vf

            Dim Vx(n), Vy(n), Vx_ant(n), Vy_ant(n), Vp(n), Ki(n), fi(n), dVxy(n) As Double
            Dim Vt(n), VTc(n), Tsat(n) As Double

            VTc = PP.RET_VTC()
            fi = Vz.Clone

            i = 0
            Tref = 0.0#
            Do
                Tref += Vz(i) * PP.AUX_TSATi(P, i)
                i += 1
            Loop Until i = n + 1

            T = Tref

            'Calculate Ki`s

            If Not ReuseKI Then
                i = 0
                Do
                    IObj?.SetCurrent
                    Vp(i) = PP.AUX_PVAPi(i, T)
                    Ki(i) = Vp(i) / P
                    If Double.IsNaN(Ki(i)) Or Double.IsInfinity(Ki(i)) Then Ki(i) = 1.0E+20
                    i += 1
                Loop Until i = n + 1
            Else
                If Not PP.AUX_CheckTrivial(PrevKi) And Not Double.IsNaN(PrevKi(0)) Then
                    For i = 0 To n
                        IObj?.SetCurrent
                        Ki(i) = PrevKi(i)
                        If Double.IsNaN(Ki(i)) Or Double.IsInfinity(Ki(i)) Then Ki(i) = 1.0E+20
                    Next
                Else
                    i = 0
                    Do
                        IObj?.SetCurrent
                        Vp(i) = PP.AUX_PVAPi(i, T)
                        Ki(i) = Vp(i) / P
                        If Double.IsNaN(Ki(i)) Or Double.IsInfinity(Ki(i)) Then Ki(i) = 1.0E+20
                        i += 1
                    Loop Until i = n + 1
                End If
            End If

            IObj?.Paragraphs.Add(String.Format("Initial estimates for T: {0} K", T))
            IObj?.Paragraphs.Add(String.Format("Initial estimates for K: {0}", Ki.ToMathArrayString))

            i = 0
            Do
                If Vz(i) <> 0 Then
                    Vy(i) = Vz(i) * Ki(i) / ((Ki(i) - 1) * V + 1)
                    If Double.IsInfinity(Vy(i)) Then Vy(i) = 0.0#
                    Vx(i) = Vy(i) / Ki(i)
                Else
                    Vy(i) = 0
                    Vx(i) = 0
                End If
                i += 1
            Loop Until i = n + 1

            Vx = Vx.NormalizeY()
            Vy = Vy.NormalizeY()

            IObj?.Paragraphs.Add(String.Format("Initial estimates for x: {0}", Vx.ToMathArrayString))
            IObj?.Paragraphs.Add(String.Format("Initial estimates for y: {0}", Vy.ToMathArrayString))

            If PP.AUX_IS_SINGLECOMP(Vz) Then
                WriteDebugInfo("PV Flash [NL]: Converged in 1 iteration.")
                T = 0
                For i = 0 To n
                    IObj?.SetCurrent
                    T += Vz(i) * PP.AUX_TSATi(P, i)
                Next
                IObj?.Close()
                If Vz.Count = 1 Then
                    Vx = New Double() {1.0}
                    Vy = New Double() {1.0}
                    Ki = New Double() {1.0}
                End If
                Return New Object() {L, V, Vx, Vy, T, 0, Ki, 0.0#, PP.RET_NullVector, 0.0#, PP.RET_NullVector}
            End If

            d2 = Date.Now

            dt = d2 - d1

            WriteDebugInfo("PV Flash [NL]: Converged in " & ecount & " iterations. Time taken: " & dt.TotalMilliseconds & " ms.")

            IObj?.Paragraphs.Add("The algorithm converged in " & ecount & " iterations. Time taken: " & dt.TotalMilliseconds & " ms.")

            IObj?.Paragraphs.Add(String.Format("Final converged value for T: {0}", T))

            IObj?.Close()

            Return New Object() {L, V, Vx, Vy, T, ecount, Ki, 0.0#, PP.RET_NullVector, 0.0#, PP.RET_NullVector}

        End Function


        Function OBJ_FUNC_PH_FLASH(ByVal Type As String, ByVal X As Double, ByVal P As Double, ByVal Vz() As Double, ByVal PP As PropertyPackages.PropertyPackage, ByVal ReuseKi As Boolean, ByVal Ki() As Double) As Object

            Dim IObj As Inspector.InspectorItem = Inspector.Host.GetNewInspectorItem()

            Inspector.Host.CheckAndAdd(IObj, "", "Flash_PH", "PH Flash Objective Function (Error)", "Pressure-Enthalpy Flash Algorithm Objective Function (Error) Calculation")

            IObj?.Paragraphs.Add("This routine calculates the current error between calculated and specified enthalpies.")

            IObj?.SetCurrent()

            Dim n As Integer = Vz.Length - 1
            Dim L1, L2, V, Vx1(), Vx2(), Vy(), Sx, Vs(), T As Double

            If Type = "PT" Then
                If PTFlashFunction IsNot Nothing Then
                    Dim tmp = PTFlashFunction.Invoke(Vz, P, X, PP, ReuseKi, Ki)
                    L1 = tmp(0)
                    V = tmp(1)
                    Vx1 = tmp(2)
                    Vy = tmp(3)
                    L2 = tmp(5)
                    Vx2 = tmp(6)
                    Sx = tmp(7)
                    Vs = tmp(8)
                    T = X
                Else
                    Dim tmp = Me.Flash_PT(Vz, P, X, PP, ReuseKi, Ki)
                    L1 = tmp(0)
                    V = tmp(1)
                    Vx1 = tmp(2)
                    Vy = tmp(3)
                    L2 = tmp(5)
                    Vx2 = tmp(6)
                    Sx = tmp(7)
                    Vs = tmp(8)
                    T = X
                End If
            Else
                Dim tmp As Object() = Me.Flash_PV(Vz, P, X, 0.0#, PP, ReuseKi, Ki)
                T = tmp(4)
                Dim hres = PerformHeuristicsTest(Vz, T, P, PP)
                Dim totalsolids = PP.ForcedSolids.Count
                Dim cprops = PP.DW_GetConstantProperties()
                For i = 0 To cprops.Count - 1
                    totalsolids += Convert.ToInt32(cprops(i).IsSolid)
                Next
                If hres.SolidPhase And Not totalsolids > 0 Then
                    tmp = New NestedLoopsSLE().Flash_PV(Vz, P, X, T, PP, False, Nothing)
                End If
                L1 = tmp(0)
                V = tmp(1)
                Vx1 = tmp(2)
                Vy = tmp(3)
                T = tmp(4)
                L2 = tmp(7)
                Vx2 = tmp(8)
                Sx = tmp(9)
                Vs = tmp(10)
            End If

            Dim _Hv, _Hl1, _Hl2, _Hs As Double

            _Hv = 0.0#
            _Hl1 = 0.0#
            _Hl2 = 0.0#
            _Hs = 0.0

            If Settings.EnableParallelProcessing Then
                Dim t1 = New Task(Sub()
                                      If V > 0 Then _Hv = PP.DW_CalcEnthalpy(Vy, T, P, State.Vapor)
                                  End Sub)
                Dim t2 = New Task(Sub()
                                      If L1 > 0 Then _Hl1 = PP.DW_CalcEnthalpy(Vx1, T, P, State.Liquid)
                                  End Sub)
                Dim t3 = New Task(Sub()
                                      If L2 > 0 Then _Hl2 = PP.DW_CalcEnthalpy(Vx2, T, P, State.Liquid)
                                  End Sub)
                Dim t4 = New Task(Sub()
                                      If Sx > 0 Then _Hs = PP.DW_CalcEnthalpy(Vs, T, P, State.Solid)
                                  End Sub)
                t1.Start()
                t2.Start()
                t3.Start()
                t4.Start()
                Task.WaitAll(t1, t2, t3, t4)
            Else
                If V > 0 Then _Hv = PP.DW_CalcEnthalpy(Vy, T, P, State.Vapor)
                If L1 > 0 Then _Hl1 = PP.DW_CalcEnthalpy(Vx1, T, P, State.Liquid)
                If L2 > 0 Then _Hl2 = PP.DW_CalcEnthalpy(Vx2, T, P, State.Liquid)
                If Sx > 0 Then _Hs = PP.DW_CalcEnthalpy(Vs, T, P, State.Solid)
            End If

            Dim mmg, mml, mml2, mms As Double
            mmg = PP.AUX_MMM(Vy)
            mml = PP.AUX_MMM(Vx1)
            mml2 = PP.AUX_MMM(Vx2)
            mms = PP.AUX_MMM(Vs)

            Dim herr = Hf - (mmg * V / (mmg * V + mml * L1 + mml2 * L2 + mms * Sx)) * _Hv -
                (mml * L1 / (mmg * V + mml * L1 + mml2 * L2 + mms * Sx)) * _Hl1 -
                (mml2 * L2 / (mmg * V + mml * L1 + mml2 * L2 + mms * Sx)) * _Hl2 -
                (mms * Sx / (mmg * V + mml * L1 + mml2 * L2 + mms * Sx)) * _Hs

            OBJ_FUNC_PH_FLASH = {herr, T, V, L1, Vy, Vx1}

            IObj?.Paragraphs.Add(String.Format("Specified Enthalpy: {0} kJ/kg", Hf))

            IObj?.Paragraphs.Add(String.Format("Current Error: {0} kJ/kg", herr))

            IObj?.Close()

            WriteDebugInfo("PH Flash [NL]: Current T = " & T & ", Current H Error = " & herr)

            If Not PP.CurrentMaterialStream.Flowsheet Is Nothing Then PP.CurrentMaterialStream.Flowsheet.CheckStatus()

        End Function

        Function OBJ_FUNC_PS_FLASH(ByVal Type As String, ByVal X As Double, ByVal P As Double, ByVal Vz() As Double, ByVal PP As PropertyPackages.PropertyPackage, ByVal ReuseKi As Boolean, ByVal Ki() As Double) As Object

            Dim IObj As Inspector.InspectorItem = Inspector.Host.GetNewInspectorItem()

            Inspector.Host.CheckAndAdd(IObj, "", "Flash_PS", "PS Flash Objective Function (Error)", "Pressure-Entropy Flash Algorithm Objective Function (Error) Calculation")

            IObj?.Paragraphs.Add("This routine calculates the current error between calculated and specified entropies.")

            IObj?.SetCurrent()

            Dim n = Vz.Length - 1
            Dim L1, L2, V, Vx1(), Vx2(), Vy(), Sx, Vs(), T As Double

            If Type = "PT" Then
                If PTFlashFunction IsNot Nothing Then
                    Dim tmp = PTFlashFunction.Invoke(Vz, P, X, PP, ReuseKi, Ki)
                    L1 = tmp(0)
                    V = tmp(1)
                    Vx1 = tmp(2)
                    Vy = tmp(3)
                    L2 = tmp(5)
                    Vx2 = tmp(6)
                    Sx = tmp(7)
                    Vs = tmp(8)
                    T = X
                Else
                    Dim tmp = Me.Flash_PT(Vz, P, X, PP, ReuseKi, Ki)
                    L1 = tmp(0)
                    V = tmp(1)
                    Vx1 = tmp(2)
                    Vy = tmp(3)
                    L2 = tmp(5)
                    Vx2 = tmp(6)
                    Sx = tmp(7)
                    Vs = tmp(8)
                    T = X
                End If
            Else
                Dim tmp = Me.Flash_PV(Vz, P, X, 0.0#, PP, ReuseKi, Ki)
                T = tmp(4)
                Dim hres = PerformHeuristicsTest(Vz, T, P, PP)
                If hres.SolidPhase And Not PP.ForcedSolids.Count > 0 Then
                    tmp = New NestedLoopsSLE().Flash_PV(Vz, P, X, T, PP, ReuseKi, Ki)
                End If
                L1 = tmp(0)
                V = tmp(1)
                Vx1 = tmp(2)
                Vy = tmp(3)
                T = tmp(4)
                L2 = tmp(7)
                Vx2 = tmp(8)
                Sx = tmp(9)
                Vs = tmp(10)
            End If

            Dim _Sv, _Sl1, _Sl2, _Ss As Double

            _Sv = 0.0#
            _Sl1 = 0.0#
            _Sl2 = 0.0#
            _Ss = 0.0

            If V > 0 Then _Sv = PP.DW_CalcEntropy(Vy, T, P, State.Vapor)
            If L1 > 0 Then _Sl1 = PP.DW_CalcEntropy(Vx1, T, P, State.Liquid)
            If L2 > 0 Then _Sl2 = PP.DW_CalcEntropy(Vx2, T, P, State.Liquid)
            If Sx > 0 Then _Ss = PP.DW_CalcEntropy(Vs, T, P, State.Solid)

            Dim mmg, mml, mml2, mms As Double

            mmg = PP.AUX_MMM(Vy)
            mml = PP.AUX_MMM(Vx1)
            mml2 = PP.AUX_MMM(Vx2)
            mms = PP.AUX_MMM(Vs)

            Dim serr = Sf - (mmg * V / (mmg * V + mml * L1 + mml2 * L2 + mms * Sx)) * _Sv -
                (mml * L1 / (mmg * V + mml * L1 + mml2 * L2 + mms * Sx)) * _Sl1 -
                (mml2 * L2 / (mmg * V + mml * L1 + mml2 * L2 + mms * Sx)) * _Sl2 -
                (mms * Sx / (mmg * V + mml * L1 + mml2 * L2 + mms * Sx)) * _Ss

            OBJ_FUNC_PS_FLASH = {serr, T, V, L1, Vy, Vx1}

            IObj?.Paragraphs.Add(String.Format("Specified Entropy: {0} kJ/[kg.K]", Sf))

            IObj?.Paragraphs.Add(String.Format("Current Error: {0} kJ/[kg.K]", serr))

            IObj?.Close()

            WriteDebugInfo("PS Flash [NL]: Current T = " & T & ", Current S Error = " & serr)

            If Not PP.CurrentMaterialStream.Flowsheet Is Nothing Then PP.CurrentMaterialStream.Flowsheet.CheckStatus()

        End Function

        Function Herror(ByVal type As String, ByVal X As Double, ByVal P As Double, ByVal Vz() As Double, ByVal PP As PropertyPackages.PropertyPackage, ByVal ReuseKi As Boolean, ByVal Ki() As Double) As Object
            Return OBJ_FUNC_PH_FLASH(type, X, P, Vz, PP, ReuseKi, Ki)
        End Function

        Function Serror(ByVal type As String, ByVal X As Double, ByVal P As Double, ByVal Vz() As Double, ByVal PP As PropertyPackages.PropertyPackage, ByVal ReuseKi As Boolean, ByVal Ki() As Double) As Object
            Return OBJ_FUNC_PS_FLASH(type, X, P, Vz, PP, ReuseKi, Ki)
        End Function

    End Class

End Namespace
