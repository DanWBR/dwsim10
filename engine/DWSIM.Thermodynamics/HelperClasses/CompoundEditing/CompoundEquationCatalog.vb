Imports System.Globalization
Imports System.Text.RegularExpressions
Imports DWSIM.Thermodynamics.PropertyPackages

Namespace CompoundEditing

    ''' <summary>What the editors show about one equation number (or free-form expression).</summary>
    Public Class CompoundEquationInfo
        ''' <summary>"101", "" (not defined) or the free-form expression text.</summary>
        Public Property Id As String
        ''' <summary>The formula as the engine evaluates it, from PropertyPackage.GetEquationString.</summary>
        Public Property Formula As String
        ''' <summary>Highest coefficient letter the formula uses (0..5).</summary>
        Public Property CoefficientCount As Integer
        ''' <summary>True when the formula uses Tr = T/Tc and therefore needs the critical temperature.</summary>
        Public Property UsesReducedTemperature As Boolean
        Public Property IsExpression As Boolean
        Public Property IsNotDefined As Boolean
        ''' <summary>Easy-to-read explanation of the form and where it is typically used.</summary>
        Public Property PlainDescription As String

        ''' <summary>Text for a drop-down list: "101: Exp(A + B / T + ...)".</summary>
        Public ReadOnly Property ListText As String
            Get
                If IsNotDefined Then Return "(none) - DWSIM estimates it"
                If IsExpression Then Return "Expression: " & Id
                Return Id & ": " & Formula
            End Get
        End Property

        ''' <summary>Which of A..E the formula reads.</summary>
        Public Function UsesCoefficient(letter As Char) As Boolean
            Return Regex.IsMatch(Formula, "\b" & letter & "\b")
        End Function
    End Class

    ''' <summary>
    ''' The list of equation numbers DWSIM understands, with their formulas and plain-language notes.
    ''' The formulas come from PropertyPackage.GetEquationString, so nothing is duplicated; the id
    ''' list is kept honest by a test that probes the engine for ids missing here.
    ''' </summary>
    Public Module CompoundEquationCatalog

        ''' <summary>Every numeric id PropertyPackage.CalcCSTDepProp handles.</summary>
        Public ReadOnly KnownIds As String() = {"1", "2", "3", "4", "5", "6", "10", "11", "12", "13", "14", "15", "16", "17", "45", "75",
                                               "100", "101", "102", "103", "104", "105", "106", "107", "114", "115", "116", "117", "119",
                                               "207", "208", "209", "210", "211", "212", "213", "221", "230", "231"}

        Private ReadOnly _plain As New Dictionary(Of String, String) From {
            {"1", "A constant value, the same at every temperature."},
            {"2", "A straight line in temperature: A + B T."},
            {"3", "A second-degree polynomial in temperature."},
            {"4", "A third-degree polynomial in temperature: A + B T + C T^2 + D T^3. Uses A to D; E is ignored. A common form for ideal gas heat capacity."},
            {"5", "A fourth-degree polynomial in temperature using all five coefficients. Common for heat capacities."},
            {"6", "A third-degree polynomial plus an E/T^2 term (the Shomate-like form used for heat capacities)."},
            {"10", "Antoine equation written as exp(A - B/(T + C)): the classic three-coefficient vapor-pressure form. T in K; the result is in the unit the coefficients were fitted in, which must match the unit this property expects."},
            {"11", "exp(A): a constant written in exponential form."},
            {"12", "exp(A + B T): an exponential of a straight line."},
            {"13", "exp(A + B T + C T^2)."},
            {"14", "exp(A + B T + C T^2 + D T^3)."},
            {"15", "exp of a fourth-degree polynomial, using all five coefficients."},
            {"16", "A + exp(B/T + C + D T + E T^2): a constant plus an exponential term."},
            {"17", "A + exp(B + C T + D T^2 + E T^3)."},
            {"45", "The integral of a fourth-degree polynomial (A T + B T^2/2 + ...). Used for enthalpy-type integrals of a heat-capacity polynomial."},
            {"75", "The derivative of a fourth-degree polynomial (B + 2 C T + ...). Coefficient A is not used."},
            {"100", "DIPPR equation 100: a fourth-degree polynomial A + B T + C T^2 + D T^3 + E T^4. The standard ChemSep form for heat capacities, thermal conductivities and liquid densities (when it is a polynomial)."},
            {"101", "DIPPR equation 101: exp(A + B/T + C ln T + D T^E). The standard form for vapor pressure and liquid viscosity in ChemSep and DIPPR. Uses all five coefficients (E is an exponent, usually 1, 2 or 6). T in K; the result unit must be the one this property expects (for vapor pressure, Pa)."},
            {"102", "DIPPR equation 102: A T^B / (1 + C/T + D/T^2). The standard form for vapor viscosity and vapor thermal conductivity. Uses A to D."},
            {"103", "DIPPR equation 103: A + B exp(-C / T^D). Uses A to D."},
            {"104", "DIPPR equation 104: A + B/T + C/T^2 + D/T^8 + E/T^9, the second virial coefficient form."},
            {"105", "DIPPR equation 105: A / B^(1 + (1 - T/C)^D), the Rackett-type form used for saturated liquid density. Uses A to D; C is usually the critical temperature. For a User compound the result must be in kg/m3 (in ChemSep files it is kmol/m3)."},
            {"106", "DIPPR equation 106: A (1 - Tr)^(B + C Tr + D Tr^2 + E Tr^3) with Tr = T/Tc. The standard form for enthalpy of vaporization and surface tension. It needs the critical temperature; a wrong Tc distorts the whole curve."},
            {"107", "DIPPR equation 107 (Aly-Lee): A + B (C/T / sinh(C/T))^2 + D (E/T / cosh(E/T))^2. The standard ChemSep form for ideal gas heat capacity, valid over a very wide temperature range. Uses all five coefficients."},
            {"114", "The integral of a third-degree polynomial (A T + B T^2/2 + C T^3/3 + D T^4/4). Uses A to D."},
            {"115", "exp(A + B/T + C ln T + D T^2 + E/T^2): an extended vapor-pressure form."},
            {"116", "DIPPR equation 116: A + B (1-Tr)^0.35 + C (1-Tr)^(2/3) + D (1-Tr) + E (1-Tr)^(4/3), used for saturated liquid density near the critical point. Needs the critical temperature."},
            {"117", "DIPPR equation 117: A T + B (C/T)/tanh(C/T) - D (E/T)/tanh(E/T), the integrated Aly-Lee form (enthalpy of an ideal gas)."},
            {"119", "exp(A/T + B + C T + D T^2 + E ln T): another extended vapor-pressure form."},
            {"207", "Antoine equation exp(A - B/(T + C)), the same form as 10."},
            {"208", "Antoine equation in base 10: 10^(A - B/(T + C)). Check the pressure unit the coefficients were fitted in."},
            {"209", "10^(A (1/T - 1/B)), a two-coefficient Clausius-Clapeyron-like form."},
            {"210", "10^(A + B/T + C T + D T^2)."},
            {"211", "A ((B - T)/(B - C))^D, a Watson-type form."},
            {"212", "Wagner vapor-pressure equation exp((E/T)(A tau + B tau^1.5 + C tau^3 + D tau^6)) with tau = 1 - T/E, where E is the critical temperature. Very accurate up to the critical point; all five coefficients are used and E must be Tc."},
            {"213", "The Wagner form without the exponential: (E/T)(A tau + B tau^1.5 + C tau^3 + D tau^6), tau = 1 - T/E."},
            {"221", "-B/T^2 + C/T + D E T^(E-1): the temperature derivative of the DIPPR 101 exponent. Coefficient A is not used."},
            {"230", "-B/T^2 + C/T + D - 2 E/T^3: the temperature derivative of the extended form 115."},
            {"231", "B - C/(T - D)^2: the temperature derivative of an Antoine-type exponent."}
        }

        Private ReadOnly _cache As New Dictionary(Of String, CompoundEquationInfo)

        ''' <summary>True when the text is not a number and will be evaluated as a free-form expression.</summary>
        Public Function IsExpression(equation As String) As Boolean
            If String.IsNullOrWhiteSpace(equation) Then Return False
            Dim n As Integer
            Return Not Integer.TryParse(equation.Trim(), n)
        End Function

        ''' <summary>"" and "0" both mean "no equation set".</summary>
        Public Function IsNotDefined(equation As String) As Boolean
            Return String.IsNullOrWhiteSpace(equation) OrElse equation.Trim() = "0"
        End Function

        Public Function Describe(equation As String) As CompoundEquationInfo
            Dim key = If(equation, "").Trim()
            SyncLock _cache
                Dim info As CompoundEquationInfo = Nothing
                If _cache.TryGetValue(key, info) Then Return info
                info = New CompoundEquationInfo With {.Id = key}
                If IsNotDefined(key) Then
                    info.IsNotDefined = True
                    info.Formula = "Not defined"
                    info.PlainDescription = "No equation is set for this property, so DWSIM estimates it from the basic constants (see the note on the property block for which estimate it uses). That keeps the simulation running but the values are approximate."
                ElseIf IsExpression(key) Then
                    info.IsExpression = True
                    info.Formula = key
                    info.CoefficientCount = CountCoefficients(key)
                    info.PlainDescription = "A free-form expression evaluated by DWSIM's expression parser. Write it as y = f(T) using A to E as the coefficients, for example ""y = A + B * T"". To use other units add a where clause: ""ln(P) = A - B / (T + C) where T in C and P in bar"". Without a where clause T is in K and the result must be in the unit this property expects."
                Else
                    info.Formula = PropertyPackage.GetEquationString(key)
                    info.CoefficientCount = CountCoefficients(info.Formula)
                    info.UsesReducedTemperature = info.Formula.Contains("Tr")
                    Dim plain As String = Nothing
                    If _plain.TryGetValue(key, plain) Then
                        info.PlainDescription = plain
                    ElseIf info.Formula = "Not Defined" Then
                        info.PlainDescription = "DWSIM does not know equation number " & key & ": the property will evaluate to zero. Pick a number from the list or write an expression."
                    Else
                        info.PlainDescription = "Equation " & key & "."
                    End If
                End If
                _cache(key) = info
                Return info
            End SyncLock
        End Function

        ''' <summary>All choices for a drop-down: "not defined" first, then the numeric ids in order.</summary>
        Public Function All() As List(Of CompoundEquationInfo)
            Dim l As New List(Of CompoundEquationInfo) From {Describe("")}
            For Each id In KnownIds
                l.Add(Describe(id))
            Next
            Return l
        End Function

        ''' <summary>Highest coefficient letter used, as a count: "A + B * T" gives 2, the DIPPR 101 form gives 5.</summary>
        Public Function CountCoefficients(formula As String) As Integer
            Dim n = 0
            For i = 0 To 4
                Dim letter = ChrW(AscW("A"c) + i)
                If Regex.IsMatch(If(formula, ""), "\b" & letter & "\b") Then n = i + 1
            Next
            Return n
        End Function

        ''' <summary>Evaluates a numeric id through CalcCSTDepProp or an expression through ParseEquation. "" gives 0.</summary>
        Public Function Evaluate(equation As String, A As Double, B As Double, C As Double, D As Double, E As Double, T As Double, Tc As Double) As Double
            Dim key = If(equation, "").Trim()
            If key = "" Then Return 0.0
            If IsExpression(key) Then Return PropertyPackage.ParseEquation(key, A, B, C, D, E, T)
            Return PropertyPackage.CalcCSTDepProp(key, A, B, C, D, E, T, Tc)
        End Function

        ''' <summary>Tries the expression parser once so the editor can flag a bad expression before it is saved.</summary>
        Public Function TryValidateExpression(expression As String, ByRef message As String) As Boolean
            Try
                Dim v = PropertyPackage.ParseEquation(expression, 1.0, 1.0, 1.0, 1.0, 1.0, 300.0)
                If Double.IsNaN(v) OrElse Double.IsInfinity(v) Then
                    message = "The expression evaluates to an invalid number at T = 300 K."
                    Return False
                End If
                message = ""
                Return True
            Catch ex As Exception
                message = ex.Message
                Return False
            End Try
        End Function

        ''' <summary>The formula with the coefficient values substituted, for the explainer: "Exp(73.649 + (-7258.2) / T + ...)".</summary>
        Public Function FormulaWithValues(info As CompoundEquationInfo, A As Double, B As Double, C As Double, D As Double, E As Double, Optional format As String = "G6") As String
            If info Is Nothing OrElse info.IsNotDefined Then Return ""
            Dim values() As Double = {A, B, C, D, E}
            Dim text = info.Formula
            For i = 4 To 0 Step -1
                Dim letter = ChrW(AscW("A"c) + i)
                Dim s = values(i).ToString(format, CultureInfo.InvariantCulture)
                If values(i) < 0 Then s = "(" & s & ")"
                text = Regex.Replace(text, "\b" & letter & "\b", s)
            Next
            Return text
        End Function

    End Module

End Namespace
