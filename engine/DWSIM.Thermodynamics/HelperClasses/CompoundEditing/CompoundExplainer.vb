Imports System.Globalization
Imports DWSIM.Interfaces
Imports DWSIM.SharedClasses.SystemsOfUnits

Namespace CompoundEditing

    ''' <summary>
    ''' The plain-language text both compound editors show for a temperature-dependent property:
    ''' which formula the equation number stands for, with the current coefficients substituted, which
    ''' coefficients it reads, that T is in K, which unit the raw equation must return for this
    ''' compound's database, the validity range, and what DWSIM does when no equation is set.
    ''' </summary>
    Public Module CompoundExplainer

        Public Function ExplainBlock(b As TemperatureDependentBlock, cp As ICompoundConstantProperties, su As IUnitsOfMeasure, Optional nf As String = "G6") As String

            If b Is Nothing OrElse cp Is Nothing Then Return ""

            Dim eq = b.Equation(cp)
            Dim info = CompoundEquationCatalog.Describe(eq)
            Dim c = b.Coefficients(cp)
            Dim raw = b.RawEquationUnit(cp)
            Dim display = b.DisplayUnit(su)
            Dim lines As New List(Of String)

            lines.Add(b.DisplayName & ": " & b.Help)
            lines.Add("")

            If info.IsNotDefined Then
                lines.Add("Equation: none. " & b.NoEquationBehaviour)
                If c.Any(Function(x) x <> 0.0) AndAlso b.Key <> "EnthalpyOfVaporization" Then
                    lines.Add("The coefficients that are set are ignored until an equation number is chosen.")
                End If
            Else
                lines.Add("Equation " & If(info.IsExpression, "(custom expression)", "number " & info.Id) & ": " & info.Formula)
                lines.Add(info.PlainDescription)
                If Not info.IsExpression Then
                    lines.Add("With the current coefficients: " & CompoundEquationCatalog.FormulaWithValues(info, c(0), c(1), c(2), c(3), c(4)))
                    Dim used As New List(Of String), unused As New List(Of String)
                    For i = 0 To 4
                        Dim letter = ChrW(AscW("A"c) + i)
                        If info.UsesCoefficient(letter) Then used.Add(letter) Else unused.Add(letter)
                    Next
                    lines.Add("Coefficients used: " & String.Join(", ", used) & If(unused.Count > 0, ". Not used: " & String.Join(", ", unused) & ".", "."))
                End If
                lines.Add("")
                lines.Add("Units: T must be in K. The formula must give the " & b.DisplayName.ToLowerInvariant() & " in " & raw & ". DWSIM then shows it in " & display & ".")
                If info.UsesReducedTemperature Then
                    lines.Add("Tr is the reduced temperature T/Tc, so this formula needs the critical temperature (" & cp.Critical_Temperature.ToString(nf, CultureInfo.CurrentCulture) & " K).")
                End If
            End If

            Dim tmin = b.Tmin(cp), tmax = b.Tmax(cp)
            If b.TminKey Is Nothing Then
                lines.Add("This property has no validity range fields; the equation is used at every temperature.")
            ElseIf tmax > tmin AndAlso tmin > 0.0 Then
                lines.Add("Valid range: " & TempText(su, tmin, nf) & " to " & TempText(su, tmax, nf) & " " & su.temperature & ". Outside it DWSIM still uses the equation, as an extrapolation.")
            Else
                lines.Add("No validity range is set. DWSIM uses the equation at any temperature, so check that it behaves where your process runs.")
            End If

            If Not b.EquationIsHonoured(cp) AndAlso Not info.IsNotDefined Then
                lines.Add("Important: this compound comes from the " & If(String.IsNullOrEmpty(cp.OriginalDB), "built-in", cp.OriginalDB) &
                          " database, and for that database DWSIM uses its own fixed form for this property and IGNORES the equation number. To use your own coefficients, save the compound to a JSON file and import it back as a User compound.")
            End If

            If b.HasTabularData(cp) Then
                lines.Add("Experimental data points are stored behind this equation (" & CompoundDiff.FormatValue(b.TabularDataKey, CompoundPropertyDescriptors.ByKey(b.TabularDataKey).GetValue(cp), su, nf) & "). DWSIM evaluates the equation, not the points.")
            End If

            Try
                Dim r = b.DefaultRange(cp)
                lines.Add("Source right now: " & b.EvaluatorMessage(cp, 0.5 * (r.Tmin + r.Tmax)))
            Catch ex As Exception
                lines.Add("Source right now: could not evaluate (" & ex.Message & ")")
            End Try

            Return String.Join(Environment.NewLine, lines)

        End Function

        ''' <summary>"At 100 C (373.15 K): 101.3 kPa (101325 Pa). The raw formula gives 101325 Pa."</summary>
        Public Function TryIt(b As TemperatureDependentBlock, cp As ICompoundConstantProperties, su As IUnitsOfMeasure, tDisplay As Double, Optional nf As String = "G6") As String
            If b Is Nothing OrElse cp Is Nothing Then Return ""
            Dim tK = Converter.ConvertToSI(su.temperature, tDisplay)
            Try
                Dim eq = b.Equation(cp)
                Dim info = CompoundEquationCatalog.Describe(eq)
                Dim c = b.Coefficients(cp)
                Dim engine = b.Evaluator(cp, tK)
                Dim shown = Converter.ConvertFromSI(b.DisplayUnit(su), engine)
                Dim text = "At " & tDisplay.ToString(nf, CultureInfo.CurrentCulture) & " " & su.temperature & " (" & tK.ToString("F2", CultureInfo.CurrentCulture) & " K): " &
                           shown.ToString("G6", CultureInfo.CurrentCulture) & " " & b.DisplayUnit(su) & " (" & engine.ToString("G6", CultureInfo.CurrentCulture) & " " & b.OutputUnit & ")."
                If Not info.IsNotDefined AndAlso b.EquationIsHonoured(cp) Then
                    Dim rawValue = CompoundEquationCatalog.Evaluate(eq, c(0), c(1), c(2), c(3), c(4), tK, cp.Critical_Temperature)
                    text &= " The raw formula gives " & rawValue.ToString("G6", CultureInfo.CurrentCulture) & " " & b.RawEquationUnit(cp) & "."
                End If
                Return text
            Catch ex As Exception
                Return "Could not evaluate: " & ex.Message
            End Try
        End Function

        ''' <summary>A temperature that sits in the middle of the block's range, in the display unit.</summary>
        Public Function SuggestedTemperature(b As TemperatureDependentBlock, cp As ICompoundConstantProperties, su As IUnitsOfMeasure) As Double
            Dim r = b.DefaultRange(cp)
            Return Converter.ConvertFromSI(su.temperature, 0.5 * (r.Tmin + r.Tmax))
        End Function

        Private Function TempText(su As IUnitsOfMeasure, kelvin As Double, nf As String) As String
            Return Converter.ConvertFromSI(su.temperature, kelvin).ToString(nf, CultureInfo.CurrentCulture)
        End Function

    End Module

End Namespace
