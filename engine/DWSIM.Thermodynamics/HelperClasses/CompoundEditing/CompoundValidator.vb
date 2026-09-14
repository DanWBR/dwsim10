Imports System.Globalization
Imports System.IO
Imports DWSIM.Interfaces
Imports DWSIM.Thermodynamics.BaseClasses

Namespace CompoundEditing

    Public Enum CompoundIssueSeverity
        Info
        Warning
    End Enum

    ''' <summary>One thing worth telling the user about a compound. Never blocks anything.</summary>
    Public Class CompoundIssue
        Public Property Severity As CompoundIssueSeverity
        ''' <summary>Stable identifier, e.g. MW_FORMULA_MISMATCH, for tests and for jumping to help.</summary>
        Public Property Code As String
        ''' <summary>Descriptor key the UI can jump to; Nothing for the whole compound.</summary>
        Public Property PropertyKey As String
        Public Property Message As String
        Public Overrides Function ToString() As String
            Return Severity.ToString() & ": " & Message
        End Function
    End Class

    ''' <summary>
    ''' Consistency checks with plain-language messages that quote the actual numbers: formula versus
    ''' molecular weight, missing constants, suspicious critical points, equation blocks that the engine
    ''' will ignore or estimate, ranges and coefficients. Informational; nothing here stops a simulation.
    ''' </summary>
    Public Module CompoundValidator

        Private _atomicWeights As Dictionary(Of String, Double)
        Private ReadOnly _lock As New Object

        ''' <summary>Atomic weights from the periodic table shipped inside DWSIM.Thermodynamics (Elements.txt).</summary>
        Public ReadOnly Property AtomicWeights As IReadOnlyDictionary(Of String, Double)
            Get
                SyncLock _lock
                    If _atomicWeights Is Nothing Then
                        Dim d As New Dictionary(Of String, Double)
                        Try
                            Using s = Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("DWSIM.Thermodynamics.Elements.txt")
                                Using r As New StreamReader(s)
                                    r.ReadLine() ' header: ID;Name;Symbol;MW
                                    While Not r.EndOfStream
                                        Dim line = r.ReadLine()
                                        If String.IsNullOrWhiteSpace(line) Then Continue While
                                        Dim parts = line.Split(";"c)
                                        If parts.Length < 4 Then Continue While
                                        Dim mw As Double
                                        If Double.TryParse(parts(3), NumberStyles.Float, CultureInfo.InvariantCulture, mw) Then d(parts(2).Trim()) = mw
                                    End While
                                End Using
                            End Using
                        Catch
                        End Try
                        _atomicWeights = d
                    End If
                    Return _atomicWeights
                End SyncLock
            End Get
        End Property

        ''' <summary>Molecular weight the formula adds up to, using the engine's own formula parser. 0 when it cannot be parsed.</summary>
        Public Function MolarWeightFromFormula(formula As String, ByRef unknownSymbols As List(Of String)) As Double
            unknownSymbols = New List(Of String)
            If String.IsNullOrWhiteSpace(formula) Then Return 0.0
            Try
                Dim tmp As New ConstantProperties()
                tmp.Formula = formula
                Return MolarWeightFromElements(tmp.Elements, unknownSymbols)
            Catch
                Return 0.0
            End Try
        End Function

        Public Function MolarWeightFromElements(elements As SortedList, ByRef unknownSymbols As List(Of String)) As Double
            If unknownSymbols Is Nothing Then unknownSymbols = New List(Of String)
            If elements Is Nothing OrElse elements.Count = 0 Then Return 0.0
            Dim total = 0.0
            For Each k In elements.Keys
                Dim symbol = k.ToString().Trim()
                Dim count = CompoundDiff.ToDouble(elements(k))
                Dim aw As Double
                If AtomicWeights.TryGetValue(symbol, aw) Then
                    total += aw * count
                Else
                    unknownSymbols.Add(symbol)
                End If
            Next
            Return total
        End Function

        Private Function Fmt(x As Double, Optional format As String = "G5") As String
            Return x.ToString(format, CultureInfo.InvariantCulture)
        End Function

        Private Function ElementsText(elements As SortedList) As String
            If elements Is Nothing OrElse elements.Count = 0 Then Return "(none)"
            Dim parts As New List(Of String)
            For Each k In elements.Keys
                parts.Add(k.ToString() & " " & Fmt(CompoundDiff.ToDouble(elements(k))))
            Next
            Return String.Join(", ", parts)
        End Function

        ''' <summary>Runs every check; a check that throws is skipped, so this never fails on odd data.</summary>
        Public Function Validate(cp As ICompoundConstantProperties) As List(Of CompoundIssue)
            Dim issues As New List(Of CompoundIssue)
            If cp Is Nothing Then Return issues
            Dim checks As Action(Of ICompoundConstantProperties, List(Of CompoundIssue))() = {
                AddressOf CheckFormulaAndMolarWeight,
                AddressOf CheckMissingConstants,
                AddressOf CheckCriticalPoint,
                AddressOf CheckTemperatureOrder,
                AddressOf CheckFormationData,
                AddressOf CheckSolid,
                AddressOf CheckBlocks,
                AddressOf CheckDatabases}
            For Each c In checks
                Try
                    c(cp, issues)
                Catch
                End Try
            Next
            Return issues
        End Function

        Private Sub Add(issues As List(Of CompoundIssue), severity As CompoundIssueSeverity, code As String, key As String, message As String)
            issues.Add(New CompoundIssue With {.Severity = severity, .Code = code, .PropertyKey = key, .Message = message})
        End Sub

        Private Sub CheckFormulaAndMolarWeight(cp As ICompoundConstantProperties, issues As List(Of CompoundIssue))
            Dim unknownF As List(Of String) = Nothing, unknownE As List(Of String) = Nothing
            Dim mw = cp.Molar_Weight
            Dim mwF = MolarWeightFromFormula(cp.Formula, unknownF)
            Dim mwE = MolarWeightFromElements(cp.Elements, unknownE)

            For Each s In unknownF.Concat(unknownE).Distinct()
                Add(issues, CompoundIssueSeverity.Warning, "UNKNOWN_ELEMENT", "Formula",
                    "'" & s & "' is not a chemical element symbol DWSIM knows, so the formula cannot be checked against the molecular weight.")
            Next

            If mwF > 0.0 AndAlso mw > 0.0 Then
                Dim rel = (mwF - mw) / mw
                If Math.Abs(rel) > 0.005 Then
                    Add(issues, CompoundIssueSeverity.Warning, "MW_FORMULA_MISMATCH", "Molar_Weight",
                        "The formula " & cp.Formula & " adds up to " & Fmt(mwF, "F2") & " kg/kmol, but the molecular weight is " & Fmt(mw, "F2") &
                        " (" & Fmt(Math.Abs(rel) * 100.0, "F1") & " % " & If(rel > 0, "higher", "lower") & "). One of the two is wrong: DWSIM uses the molecular weight for every mass balance and the formula for elemental balances, so they must agree.")
                End If
            End If

            If mwE > 0.0 AndAlso mwF > 0.0 AndAlso Math.Abs(mwE - mwF) / mwF > 0.005 Then
                Add(issues, CompoundIssueSeverity.Warning, "ELEMENTS_FORMULA_MISMATCH", "Elements",
                    "The element list (" & ElementsText(cp.Elements) & ", " & Fmt(mwE, "F2") & " kg/kmol) does not match the formula " & cp.Formula &
                    " (" & Fmt(mwF, "F2") & " kg/kmol). Elemental balances use the list; re-enter the formula to rebuild it.")
            ElseIf mwE > 0.0 AndAlso mw > 0.0 AndAlso mwF <= 0.0 AndAlso Math.Abs(mwE - mw) / mw > 0.005 Then
                Add(issues, CompoundIssueSeverity.Warning, "MW_ELEMENTS_MISMATCH", "Elements",
                    "The element list (" & ElementsText(cp.Elements) & ") adds up to " & Fmt(mwE, "F2") & " kg/kmol, but the molecular weight is " & Fmt(mw, "F2") & ".")
            End If
        End Sub

        Private Sub CheckMissingConstants(cp As ICompoundConstantProperties, issues As List(Of CompoundIssue))
            If cp.Molar_Weight <= 0.0 Then Add(issues, CompoundIssueSeverity.Warning, "MISSING_CRITICAL", "Molar_Weight",
                "The molecular weight is missing. Nothing works without it: DWSIM cannot convert between mass and moles.")
            If cp.Critical_Temperature <= 0.0 Then Add(issues, CompoundIssueSeverity.Warning, "MISSING_CRITICAL", "Critical_Temperature",
                "The critical temperature is missing. Equations of state and every estimation fallback (vapor pressure, liquid density, viscosity) need it.")
            If cp.Critical_Pressure <= 0.0 Then Add(issues, CompoundIssueSeverity.Warning, "MISSING_CRITICAL", "Critical_Pressure",
                "The critical pressure is missing. Equations of state and the vapor-pressure and liquid-density estimates need it.")
            If cp.Acentric_Factor = 0.0 AndAlso cp.IsIon = False AndAlso cp.IsSalt = False Then Add(issues, CompoundIssueSeverity.Warning, "MISSING_CRITICAL", "Acentric_Factor",
                "The acentric factor is zero. Equations of state use it in their temperature function, and the vapor-pressure and liquid-density estimates depend on it.")
            If cp.Normal_Boiling_Point <= 0.0 AndAlso cp.IsIon = False AndAlso cp.IsSalt = False Then Add(issues, CompoundIssueSeverity.Info, "MISSING_NBP", "Normal_Boiling_Point",
                "The normal boiling point is missing. Some estimation methods (thermal conductivity, Joback) use it.")
        End Sub

        Private Sub CheckCriticalPoint(cp As ICompoundConstantProperties, issues As List(Of CompoundIssue))
            Dim tc = cp.Critical_Temperature, pc = cp.Critical_Pressure
            Dim isWater = String.Equals(If(cp.CAS_Number, "").Trim(), "7732-18-5") OrElse String.Equals(If(cp.Name, "").Trim(), "Water", StringComparison.OrdinalIgnoreCase)
            If Not isWater AndAlso Math.Abs(tc - 647.1) < 0.5 AndAlso pc > 0.0 AndAlso Math.Abs(pc - 22064000.0) / 22064000.0 < 0.01 Then
                Add(issues, CompoundIssueSeverity.Warning, "TC_PC_SUSPICIOUS", "Critical_Temperature",
                    "The critical temperature and pressure are those of water (647.1 K, 220.6 bar). If they were copied as placeholders, every estimate for this compound, including its vapor pressure, will behave like water's, and an inert meant to stay in the liquid may evaporate. Give an inert a high critical temperature instead (1500 K or more).")
            End If
            If pc > 0.0 AndAlso pc < 100000.0 Then
                Add(issues, CompoundIssueSeverity.Warning, "PC_LOOKS_LIKE_BAR", "Critical_Pressure",
                    "The critical pressure is " & Fmt(pc) & " Pa, below 1 bar. It was probably typed in bar or kPa: DWSIM stores it in Pa.")
            End If
            If tc > 0.0 AndAlso pc > 0.0 AndAlso cp.Critical_Volume > 0.0 Then
                Dim zc = pc * cp.Critical_Volume / (8314.0 * tc)
                If zc < 0.15 OrElse zc > 0.4 Then
                    Add(issues, CompoundIssueSeverity.Warning, "ZC_OUT_OF_RANGE", "Critical_Volume",
                        "Pc.Vc/(R.Tc) comes out as " & Fmt(zc, "F3") & "; real compounds lie between about 0.2 and 0.35, so one of the critical constants is probably in the wrong unit.")
                End If
            End If
        End Sub

        Private Sub CheckTemperatureOrder(cp As ICompoundConstantProperties, issues As List(Of CompoundIssue))
            If cp.Normal_Boiling_Point > 0.0 AndAlso cp.Critical_Temperature > 0.0 AndAlso cp.Normal_Boiling_Point >= cp.Critical_Temperature Then
                Add(issues, CompoundIssueSeverity.Warning, "NBP_GT_TC", "Normal_Boiling_Point",
                    "The normal boiling point (" & Fmt(cp.Normal_Boiling_Point) & " K) is not below the critical temperature (" & Fmt(cp.Critical_Temperature) & " K). A liquid boils below its critical point.")
            End If
            If cp.TemperatureOfFusion > 0.0 AndAlso cp.Normal_Boiling_Point > 0.0 AndAlso cp.TemperatureOfFusion >= cp.Normal_Boiling_Point Then
                Add(issues, CompoundIssueSeverity.Warning, "TFUS_GT_NBP", "TemperatureOfFusion",
                    "The melting point (" & Fmt(cp.TemperatureOfFusion) & " K) is not below the normal boiling point (" & Fmt(cp.Normal_Boiling_Point) & " K).")
            End If
            If cp.Acentric_Factor > 0.8 AndAlso CompoundEquationCatalog.IsNotDefined(cp.VaporPressureEquation) AndAlso cp.IsPF = 0 Then
                Add(issues, CompoundIssueSeverity.Warning, "OMEGA_HIGH_WITH_LK_PVAP", "Acentric_Factor",
                    "The acentric factor is " & Fmt(cp.Acentric_Factor, "F2") & " and there is no vapor-pressure equation, so DWSIM estimates the vapor pressure with Lee-Kesler, which is reliable up to about 0.8. Far below the critical temperature the value is tiny anyway (a non-volatile compound); nearer to it, do not trust it.")
            End If
        End Sub

        Private Sub CheckFormationData(cp As ICompoundConstantProperties, issues As List(Of CompoundIssue))
            If cp.IG_Enthalpy_of_Formation_25C = 0.0 Then
                Add(issues, CompoundIssueSeverity.Info, "HF_IG_MISSING", "IG_Enthalpy_of_Formation_25C",
                    "The enthalpy of formation is zero. Fine for separations; in a reactor the heat of any reaction involving this compound will be wrong.")
            End If
            If cp.IG_Gibbs_Energy_of_Formation_25C = 0.0 Then
                Add(issues, CompoundIssueSeverity.Info, "GF_IG_MISSING", "IG_Gibbs_Energy_of_Formation_25C",
                    "The Gibbs energy of formation is zero. Only Gibbs-minimization reactors and thermodynamic equilibrium constants need it.")
            End If
        End Sub

        Private Sub CheckSolid(cp As ICompoundConstantProperties, issues As List(Of CompoundIssue))
            If cp.IsSolid AndAlso String.IsNullOrWhiteSpace(cp.SolidDensityEquation) AndAlso cp.SolidDensityAtTs <= 0.0 Then
                Add(issues, CompoundIssueSeverity.Warning, "SOLID_WITHOUT_DENSITY", "SolidDensityAtTs",
                    "The compound is marked as a solid but has neither a solid-density equation nor a single-point solid density, so DWSIM will use a placeholder density and any solid volume will be meaningless.")
            End If
        End Sub

        Private Sub CheckBlocks(cp As ICompoundConstantProperties, issues As List(Of CompoundIssue))
            Dim db = If(cp.OriginalDB, "")
            For Each blk In CompoundPropertyDescriptors.Blocks
                Try
                    CheckBlock(cp, blk, db, issues)
                Catch
                End Try
            Next
        End Sub

        Private Sub CheckBlock(cp As ICompoundConstantProperties, blk As TemperatureDependentBlock, db As String, issues As List(Of CompoundIssue))
            Dim eq = If(blk.Equation(cp), "").Trim()
            Dim coeffs = blk.Coefficients(cp)
            Dim anyCoeff = coeffs.Any(Function(c) c <> 0.0)
            Dim notDefined = CompoundEquationCatalog.IsNotDefined(eq)
            Dim honoured = blk.EquationIsHonoured(cp)

            If Not notDefined AndAlso Not honoured Then
                Add(issues, CompoundIssueSeverity.Warning, "EQUATION_IGNORED", blk.EquationKey,
                    "This compound comes from the " & If(db = "", "built-in", db) & " database, so DWSIM uses its own fixed form for the " & blk.DisplayName.ToLowerInvariant() &
                    " and ignores equation number " & eq & ". To use your own coefficients, save the compound to a JSON file and import it back as a User compound.")
            End If

            If blk.Key = "IdealGasHeatCapacity" AndAlso (notDefined OrElse Not anyCoeff) Then
                Dim def = If(cp.Molar_Weight > 0, 3.5 * 8.314 / cp.Molar_Weight, 0.0)
                Add(issues, CompoundIssueSeverity.Warning, "CP_IG_MISSING", blk.EquationKey,
                    "There is no ideal gas heat capacity equation. DWSIM will use a constant default of 3.5 R (" & Fmt(def, "F3") & " kJ/(kg.K) for this molecular weight), so enthalpy changes with temperature, and therefore heater and reactor duties, will be rough.")
            ElseIf blk.Key = "EnthalpyOfVaporization" AndAlso notDefined AndAlso coeffs.Skip(1).Any(Function(c) c <> 0.0) Then
                Add(issues, CompoundIssueSeverity.Warning, "HVAP_CONSTANTS_WITHOUT_EQUATION", blk.EquationKey,
                    "Coefficients B to E of the enthalpy of vaporization are set but no equation number is chosen, so only A is used (as the value in kJ/kg at the boiling point) and the others are ignored.")
            ElseIf notDefined Then
                If anyCoeff Then
                    Add(issues, CompoundIssueSeverity.Info, "COEFFS_WITHOUT_EQUATION", blk.EquationKey,
                        "Coefficients are set for the " & blk.DisplayName.ToLowerInvariant() & " but no equation number is chosen, so they are ignored and the property is estimated instead. Pick the equation the coefficients belong to.")
                End If
                Select Case blk.Key
                    Case "VaporPressure"
                        If cp.IsPF = 0 AndAlso Not cp.IsIon AndAlso Not cp.IsSalt Then
                            Add(issues, CompoundIssueSeverity.Info, "PVAP_ESTIMATED", blk.EquationKey, "No vapor-pressure equation: " & blk.NoEquationBehaviour)
                        End If
                    Case "LiquidDensity"
                        Add(issues, CompoundIssueSeverity.Info, "LIQDENS_ESTIMATED", blk.EquationKey, "No liquid-density equation: " & blk.NoEquationBehaviour)
                    Case "LiquidHeatCapacity"
                        Add(issues, CompoundIssueSeverity.Info, "LIQCP_ESTIMATED", blk.EquationKey, "No liquid heat capacity equation: " & blk.NoEquationBehaviour)
                End Select
            End If

            If notDefined Then Return

            Dim info = CompoundEquationCatalog.Describe(eq)
            If info.IsExpression Then
                Dim msg As String = ""
                If Not CompoundEquationCatalog.TryValidateExpression(eq, msg) Then
                    Add(issues, CompoundIssueSeverity.Warning, "EXPRESSION_INVALID", blk.EquationKey,
                        "The " & blk.DisplayName.ToLowerInvariant() & " expression cannot be evaluated: " & msg)
                    Return
                End If
            ElseIf info.Formula = "Not Defined" Then
                Add(issues, CompoundIssueSeverity.Warning, "EQUATION_UNKNOWN_ID", blk.EquationKey,
                    "Equation number " & eq & " for the " & blk.DisplayName.ToLowerInvariant() & " is not one DWSIM knows, so the property will evaluate to zero. Pick a number from the list.")
                Return
            Else
                For i = info.CoefficientCount To 4
                    If coeffs(i) <> 0.0 Then
                        Add(issues, CompoundIssueSeverity.Info, "COEFF_UNUSED", blk.CoefficientKeys(i),
                            "Coefficient " & ChrW(AscW("A"c) + i) & " of the " & blk.DisplayName.ToLowerInvariant() & " is set, but equation " & eq & " (" & info.Formula & ") does not use it.")
                    End If
                Next
                If info.UsesReducedTemperature AndAlso cp.Critical_Temperature <= 0.0 Then
                    Add(issues, CompoundIssueSeverity.Warning, "EQUATION_NEEDS_TC", blk.EquationKey,
                        "Equation " & eq & " uses the reduced temperature T/Tc, but the critical temperature is missing.")
                End If
            End If

            Dim tmin = blk.Tmin(cp), tmax = blk.Tmax(cp)
            If tmax <> 0.0 AndAlso tmin > tmax Then
                Add(issues, CompoundIssueSeverity.Warning, "TRANGE_INVERTED", blk.TminKey,
                    "The " & blk.DisplayName.ToLowerInvariant() & " range runs from " & Fmt(tmin) & " K down to " & Fmt(tmax) & " K; the minimum is above the maximum.")
            End If

            If honoured Then
                ' inside the range, not at its ends: the DIPPR forms in Tr are zero at Tc by construction
                ' and a file's Tmax often sits right on it
                Dim r = blk.DefaultRange(cp)
                Dim span = r.Tmax - r.Tmin
                Dim bad As New List(Of String)
                For Each t In {r.Tmin + 0.05 * span, r.Tmin + 0.5 * span, r.Tmin + 0.95 * span}
                    Dim y As Double
                    Try
                        y = blk.Evaluator(cp, t)
                    Catch
                        y = Double.NaN
                    End Try
                    If Double.IsNaN(y) OrElse Double.IsInfinity(y) OrElse y <= 0.0 Then bad.Add(Fmt(t, "F0") & " K")
                Next
                If bad.Count > 0 Then
                    Add(issues, CompoundIssueSeverity.Warning, "EQUATION_EVAL_FAILS", blk.EquationKey,
                        "The " & blk.DisplayName.ToLowerInvariant() & " equation gives an invalid or non-positive value at " & String.Join(", ", bad) &
                        ". Check the coefficients and the unit they were fitted in (" & blk.RawEquationUnit(cp) & ").")
                End If
            End If
        End Sub

        Private Sub CheckDatabases(cp As ICompoundConstantProperties, issues As List(Of CompoundIssue))
            Dim o = If(cp.OriginalDB, ""), c = If(cp.CurrentDB, "")
            If o <> "" AndAlso c <> "" AndAlso o <> c Then
                Add(issues, CompoundIssueSeverity.Info, "DB_MISMATCH", "OriginalDB",
                    "The compound was created in the " & o & " database and is now listed under " & c & ". The equation coefficients are still read with the " & o & " conventions.")
            End If
        End Sub

    End Module

End Namespace
