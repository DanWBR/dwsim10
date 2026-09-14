Imports System.Globalization
Imports System.Reflection
Imports DWSIM.Interfaces
Imports DWSIM.SharedClasses.SystemsOfUnits

Namespace CompoundEditing

    ''' <summary>Where a property is shown in the compound editor.</summary>
    Public Enum CompoundPropertyGroup
        Identity
        Constants
        Formation
        Solid
        ModelParameters
        Composition
        Electrolyte
        BlackOil
        PetroleumFraction
        Metadata
    End Enum

    ''' <summary>How a property is stored and therefore how it is edited and compared.</summary>
    Public Enum CompoundPropertyKind
        Text
        [Double]
        NullableDouble
        [Integer]
        [Boolean]
        ''' <summary>SortedList of element symbol to atom count.</summary>
        Elements
        ''' <summary>SortedList of group id to count (UNIFAC, modified UNIFAC, NIST modified UNIFAC).</summary>
        Groups
        ''' <summary>ITabularData (experimental points behind a fitted correlation).</summary>
        Tabular
    End Enum

    ''' <summary>A group of the compound editor with the text shown at the top of it.</summary>
    Public Class CompoundPropertyGroupInfo
        Public Property Group As CompoundPropertyGroup
        Public Property DisplayName As String
        Public Property Intro As String
        Public Property IsAdvanced As Boolean
    End Class

    ''' <summary>
    ''' One editable property of a compound: where it lives on ICompoundConstantProperties, how it is
    ''' shown, in which unit, and the plain-language help that both user interfaces display.
    ''' The table in CompoundPropertyDescriptors is the single source of truth for the editors,
    ''' the JSON diff and the validator, so a property is added in one place only.
    ''' </summary>
    Public NotInheritable Class CompoundPropertyDescriptor

        Private Shared ReadOnly InterfaceType As Type = GetType(ICompoundConstantProperties)

        Private ReadOnly _info As PropertyInfo

        ''' <summary>Name of the property on ICompoundConstantProperties (also the JSON key).</summary>
        Public ReadOnly Property Key As String
        Public ReadOnly Property DisplayName As String
        Public ReadOnly Property Group As CompoundPropertyGroup
        Public ReadOnly Property Kind As CompoundPropertyKind
        ''' <summary>Unit of the stored value; "" when dimensionless. Used as the label when there is no UnitSelector.</summary>
        Public ReadOnly Property StoredUnit As String
        ''' <summary>Picks the display unit from the simulation's units system; Nothing means no conversion.</summary>
        Public ReadOnly Property UnitSelector As Func(Of IUnitsOfMeasure, String)
        Public ReadOnly Property IsReadOnly As Boolean
        Public ReadOnly Property IsAdvanced As Boolean
        ''' <summary>Plain-language explanation shown as tooltip and help row.</summary>
        Public ReadOnly Property Help As String

        Friend Sub New(key As String, displayName As String, group As CompoundPropertyGroup, kind As CompoundPropertyKind,
                       storedUnit As String, unitSelector As Func(Of IUnitsOfMeasure, String),
                       isReadOnly As Boolean, isAdvanced As Boolean, help As String)
            _info = InterfaceType.GetProperty(key)
            If _info Is Nothing Then Throw New ArgumentException("ICompoundConstantProperties has no property named " & key)
            Me.Key = key
            Me.DisplayName = displayName
            Me.Group = group
            Me.Kind = kind
            Me.StoredUnit = If(storedUnit, "")
            Me.UnitSelector = unitSelector
            Me.IsReadOnly = isReadOnly
            Me.IsAdvanced = isAdvanced
            Me.Help = If(help, "")
        End Sub

        Public ReadOnly Property IsNumeric As Boolean
            Get
                Return Kind = CompoundPropertyKind.Double OrElse Kind = CompoundPropertyKind.NullableDouble OrElse Kind = CompoundPropertyKind.Integer
            End Get
        End Property

        Public Function GetValue(cp As ICompoundConstantProperties) As Object
            Return _info.GetValue(cp)
        End Function

        ''' <summary>
        ''' Writes a value in the STORED unit. Strings are parsed with the invariant culture first and the
        ''' current culture second, so both decimal separators are accepted.
        ''' </summary>
        Public Sub SetValue(cp As ICompoundConstantProperties, value As Object)
            Select Case Kind
                Case CompoundPropertyKind.Double
                    _info.SetValue(cp, ParseDouble(value).GetValueOrDefault())
                Case CompoundPropertyKind.NullableDouble
                    _info.SetValue(cp, ParseDouble(value))
                Case CompoundPropertyKind.Integer
                    _info.SetValue(cp, CInt(Math.Round(ParseDouble(value).GetValueOrDefault())))
                Case CompoundPropertyKind.Boolean
                    _info.SetValue(cp, ParseBoolean(value))
                Case CompoundPropertyKind.Text
                    _info.SetValue(cp, If(value Is Nothing, "", value.ToString()))
                Case CompoundPropertyKind.Elements, CompoundPropertyKind.Groups
                    Dim list = TryCast(value, SortedList)
                    _info.SetValue(cp, If(list Is Nothing, New SortedList(), DirectCast(list.Clone(), SortedList)))
                Case CompoundPropertyKind.Tabular
                    _info.SetValue(cp, CloneIfPossible(value))
            End Select
            ' the historical NBP field mirrors the normal boiling point, as the classic editor always did
            If Key = "Normal_Boiling_Point" Then cp.NBP = cp.Normal_Boiling_Point
        End Sub

        ''' <summary>Unit the value is shown in for the given units system.</summary>
        Public Function DisplayUnit(su As IUnitsOfMeasure) As String
            If UnitSelector Is Nothing OrElse su Is Nothing Then Return StoredUnit
            Dim u = UnitSelector(su)
            Return If(String.IsNullOrEmpty(u), StoredUnit, u)
        End Function

        ''' <summary>Value converted to the display unit (numeric kinds with a UnitSelector only).</summary>
        Public Function GetDisplayValue(cp As ICompoundConstantProperties, su As IUnitsOfMeasure) As Object
            Dim raw = GetValue(cp)
            If UnitSelector Is Nothing OrElse su Is Nothing OrElse raw Is Nothing Then Return raw
            Select Case Kind
                Case CompoundPropertyKind.Double
                    Return Converter.ConvertFromSI(DisplayUnit(su), CDbl(raw))
                Case CompoundPropertyKind.NullableDouble
                    Return Converter.ConvertFromSI(DisplayUnit(su), CDbl(raw))
                Case Else
                    Return raw
            End Select
        End Function

        ''' <summary>Parses a value typed in the display unit and stores it in the stored unit.</summary>
        Public Sub SetFromDisplay(cp As ICompoundConstantProperties, su As IUnitsOfMeasure, value As Object)
            If IsNumeric AndAlso UnitSelector IsNot Nothing AndAlso su IsNot Nothing Then
                Dim parsed = ParseDouble(value)
                If Kind = CompoundPropertyKind.NullableDouble AndAlso Not parsed.HasValue Then
                    SetValue(cp, Nothing)
                Else
                    SetValue(cp, Converter.ConvertToSI(DisplayUnit(su), parsed.GetValueOrDefault()))
                End If
            Else
                SetValue(cp, value)
            End If
        End Sub

        ''' <summary>Accepts numbers and strings with either decimal separator. Empty means Nothing.</summary>
        Public Shared Function ParseDouble(value As Object) As Double?
            If value Is Nothing Then Return Nothing
            If TypeOf value Is Double Then Return CDbl(value)
            If TypeOf value Is Single OrElse TypeOf value Is Integer OrElse TypeOf value Is Long OrElse TypeOf value Is Decimal Then Return Convert.ToDouble(value, CultureInfo.InvariantCulture)
            If TypeOf value Is Boolean Then Return If(CBool(value), 1.0, 0.0)
            Dim s = value.ToString().Trim()
            If s = "" Then Return Nothing
            Dim d As Double
            If Double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, d) Then Return d
            If Double.TryParse(s, NumberStyles.Float, CultureInfo.CurrentCulture, d) Then Return d
            If Double.TryParse(s.Replace(","c, "."c), NumberStyles.Float, CultureInfo.InvariantCulture, d) Then Return d
            Throw New FormatException("'" & s & "' is not a number.")
        End Function

        Private Shared Function ParseBoolean(value As Object) As Boolean
            If value Is Nothing Then Return False
            If TypeOf value Is Boolean Then Return CBool(value)
            Dim s = value.ToString().Trim().ToLowerInvariant()
            Return s = "true" OrElse s = "1" OrElse s = "yes"
        End Function

        Private Shared Function CloneIfPossible(value As Object) As Object
            If value Is Nothing Then Return Nothing
            Dim m = value.GetType().GetMethod("Clone", Type.EmptyTypes)
            If m IsNot Nothing Then Return m.Invoke(value, Nothing)
            Return value
        End Function

    End Class

    ''' <summary>A temperature range in K.</summary>
    Public Class TemperatureRange
        Public Property Tmin As Double
        Public Property Tmax As Double
        Public Sub New(tmin As Double, tmax As Double)
            Me.Tmin = tmin
            Me.Tmax = tmax
        End Sub
    End Class

    ''' <summary>One sampled point of a temperature-dependent property, in SI.</summary>
    Public Class CurvePoint
        Public Property T As Double
        Public Property Y As Double
        Public Sub New(t As Double, y As Double)
            Me.T = t
            Me.Y = y
        End Sub
    End Class

    ''' <summary>
    ''' A temperature-dependent property block: the equation number, its five coefficients, the validity
    ''' range, the regression data behind it, and everything the editors need to EXPLAIN it: which unit
    ''' the raw equation must return for this compound's database, whether the engine even honours the
    ''' equation number, and an evaluator that follows the engine's own fallbacks.
    ''' </summary>
    Public NotInheritable Class TemperatureDependentBlock

        Public ReadOnly Property Key As String
        Public ReadOnly Property DisplayName As String
        Public ReadOnly Property Help As String
        Public ReadOnly Property EquationKey As String
        Public ReadOnly Property CoefficientKeys As String()
        ''' <summary>Nothing when the block has no range properties (ideal gas Cp, liquid viscosity).</summary>
        Public ReadOnly Property TminKey As String
        Public ReadOnly Property TmaxKey As String
        Public ReadOnly Property RegressionFitKey As String
        Public ReadOnly Property TabularDataKey As String
        ''' <summary>Unit the Evaluator returns (SI of the property's category).</summary>
        Public ReadOnly Property OutputUnit As String
        ''' <summary>Picks the display unit for the evaluated value from the simulation's units system.</summary>
        Public ReadOnly Property UnitSelector As Func(Of IUnitsOfMeasure, String)
        ''' <summary>Unit the raw equation (coefficients A..E, T in K) must produce for this compound's database.</summary>
        Public ReadOnly Property RawEquationUnit As Func(Of ICompoundConstantProperties, String)
        ''' <summary>False when the engine uses a fixed built-in form for this database and ignores the equation number.</summary>
        Public ReadOnly Property EquationIsHonoured As Func(Of ICompoundConstantProperties, Boolean)
        ''' <summary>The property in OutputUnit at T (K), with the engine's estimation fallbacks.</summary>
        Public ReadOnly Property Evaluator As Func(Of ICompoundConstantProperties, Double, Double)
        ''' <summary>Where the value came from ("regressed data", "estimated with ...").</summary>
        Public ReadOnly Property EvaluatorMessage As Func(Of ICompoundConstantProperties, Double, String)
        ''' <summary>What the engine does when no equation is set.</summary>
        Public ReadOnly Property NoEquationBehaviour As String

        Friend Sub New(key As String, displayName As String, help As String, equationKey As String, coefficientKeys As String(),
                       tminKey As String, tmaxKey As String, regressionFitKey As String, tabularDataKey As String,
                       outputUnit As String, unitSelector As Func(Of IUnitsOfMeasure, String),
                       rawEquationUnit As Func(Of ICompoundConstantProperties, String),
                       equationIsHonoured As Func(Of ICompoundConstantProperties, Boolean),
                       evaluator As Func(Of ICompoundConstantProperties, Double, Double),
                       evaluatorMessage As Func(Of ICompoundConstantProperties, Double, String),
                       noEquationBehaviour As String)
            Me.Key = key
            Me.DisplayName = displayName
            Me.Help = help
            Me.EquationKey = equationKey
            Me.CoefficientKeys = coefficientKeys
            Me.TminKey = tminKey
            Me.TmaxKey = tmaxKey
            Me.RegressionFitKey = regressionFitKey
            Me.TabularDataKey = tabularDataKey
            Me.OutputUnit = outputUnit
            Me.UnitSelector = unitSelector
            Me.RawEquationUnit = rawEquationUnit
            Me.EquationIsHonoured = equationIsHonoured
            Me.Evaluator = evaluator
            Me.EvaluatorMessage = evaluatorMessage
            Me.NoEquationBehaviour = noEquationBehaviour
        End Sub

        ''' <summary>Every property key this block owns.</summary>
        Public Function Keys() As List(Of String)
            Dim l As New List(Of String) From {EquationKey}
            l.AddRange(CoefficientKeys)
            If TminKey IsNot Nothing Then l.Add(TminKey)
            If TmaxKey IsNot Nothing Then l.Add(TmaxKey)
            If RegressionFitKey IsNot Nothing Then l.Add(RegressionFitKey)
            If TabularDataKey IsNot Nothing Then l.Add(TabularDataKey)
            Return l
        End Function

        Public Function Equation(cp As ICompoundConstantProperties) As String
            Return If(CStr(CompoundPropertyDescriptors.ByKey(EquationKey).GetValue(cp)), "")
        End Function

        Public Function Coefficients(cp As ICompoundConstantProperties) As Double()
            Return CoefficientKeys.Select(Function(k) CDbl(CompoundPropertyDescriptors.ByKey(k).GetValue(cp))).ToArray()
        End Function

        Public Function Tmin(cp As ICompoundConstantProperties) As Double
            If TminKey Is Nothing Then Return 0.0
            Return CDbl(CompoundPropertyDescriptors.ByKey(TminKey).GetValue(cp))
        End Function

        Public Function Tmax(cp As ICompoundConstantProperties) As Double
            If TmaxKey Is Nothing Then Return 0.0
            Return CDbl(CompoundPropertyDescriptors.ByKey(TmaxKey).GetValue(cp))
        End Function

        Public Function HasTabularData(cp As ICompoundConstantProperties) As Boolean
            If TabularDataKey Is Nothing Then Return False
            Dim td = TryCast(CompoundPropertyDescriptors.ByKey(TabularDataKey).GetValue(cp), ITabularData)
            Return td IsNot Nothing AndAlso td.XData IsNot Nothing AndAlso td.XData.Count > 0
        End Function

        ''' <summary>Display unit for the evaluated value under the given units system.</summary>
        Public Function DisplayUnit(su As IUnitsOfMeasure) As String
            If UnitSelector Is Nothing OrElse su Is Nothing Then Return OutputUnit
            Dim u = UnitSelector(su)
            Return If(String.IsNullOrEmpty(u), OutputUnit, u)
        End Function

        ''' <summary>
        ''' The block's own range when it is set, otherwise a sensible span for the phase the property
        ''' belongs to, derived from the melting point, boiling point and critical temperature.
        ''' </summary>
        Public Function DefaultRange(cp As ICompoundConstantProperties) As TemperatureRange
            Dim lo = Tmin(cp), hi = Tmax(cp)
            If hi > lo AndAlso lo > 0.0 Then Return New TemperatureRange(lo, hi)
            Dim tc = cp.Critical_Temperature, tb = cp.Normal_Boiling_Point, tf = cp.TemperatureOfFusion
            Select Case Key
                Case "IdealGasHeatCapacity", "VaporViscosity", "VaporThermalConductivity"
                    lo = If(tb > 0.0, tb, If(tc > 0.0, 0.5 * tc, 200.0))
                    hi = If(tc > 0.0, Math.Max(lo + 100.0, 1.5 * tc), lo + 800.0)
                Case "SolidDensity", "SolidHeatCapacity"
                    hi = If(tf > 0.0, tf, 300.0)
                    lo = Math.Max(50.0, 0.3 * hi)
                Case Else
                    ' liquid properties: stop short of the critical point, where the estimation
                    ' correlations either blow up or cut off (Latini returns 0 above 0.98 Tc)
                    lo = If(tf > 0.0, tf, If(tc > 0.0, 0.4 * tc, 250.0))
                    hi = If(tc > 0.0, 0.95 * tc, lo + 300.0)
                    If hi <= lo Then hi = lo + 100.0
            End Select
            Return New TemperatureRange(lo, hi)
        End Function

        ''' <summary>Samples the property over a range through the Evaluator, skipping points it cannot compute.</summary>
        Public Function Sample(cp As ICompoundConstantProperties, tmin As Double, tmax As Double, Optional n As Integer = 51) As List(Of CurvePoint)
            Dim pts As New List(Of CurvePoint)
            If n < 2 OrElse tmax <= tmin Then Return pts
            For i = 0 To n - 1
                Dim t = tmin + (tmax - tmin) * i / (n - 1)
                Try
                    Dim y = Evaluator(cp, t)
                    If Not Double.IsNaN(y) AndAlso Not Double.IsInfinity(y) Then pts.Add(New CurvePoint(t, y))
                Catch
                End Try
            Next
            Return pts
        End Function

    End Class

    ''' <summary>
    ''' The table of everything a compound editor can show or edit. Both user interfaces, the JSON diff
    ''' and the validator read it; nothing else lists compound properties by hand.
    ''' </summary>
    Public Module CompoundPropertyDescriptors

        Public ReadOnly Scalars As New List(Of CompoundPropertyDescriptor)
        Public ReadOnly Blocks As New List(Of TemperatureDependentBlock)
        Public ReadOnly Groups As New List(Of CompoundPropertyGroupInfo)
        ''' <summary>Interface members deliberately outside the editor (metadata, deprecated duplicates).</summary>
        Public ReadOnly ExcludedKeys As New List(Of String)

        Private ReadOnly _byKey As New Dictionary(Of String, CompoundPropertyDescriptor)
        Private ReadOnly _blockByKey As New Dictionary(Of String, TemperatureDependentBlock)

        Private Sub D(key As String, displayName As String, group As CompoundPropertyGroup, kind As CompoundPropertyKind, help As String,
                      Optional storedUnit As String = "", Optional unitSelector As Func(Of IUnitsOfMeasure, String) = Nothing,
                      Optional isReadOnly As Boolean = False, Optional isAdvanced As Boolean = False)
            Dim d1 As New CompoundPropertyDescriptor(key, displayName, group, kind, storedUnit, unitSelector, isReadOnly, isAdvanced, help)
            Scalars.Add(d1)
            _byKey(key) = d1
        End Sub

        Private Sub G(group As CompoundPropertyGroup, displayName As String, intro As String, Optional isAdvanced As Boolean = False)
            Groups.Add(New CompoundPropertyGroupInfo With {.Group = group, .DisplayName = displayName, .Intro = intro, .IsAdvanced = isAdvanced})
        End Sub

        Private Function Db(cp As ICompoundConstantProperties) As String
            Return If(cp.OriginalDB, "")
        End Function

        ''' <summary>DWSIM, blank and CheResources compounds use fixed built-in forms for most blocks.</summary>
        Private Function UsesBuiltInForms(cp As ICompoundConstantProperties) As Boolean
            Dim db1 = Db(cp)
            Return db1 = "DWSIM" OrElse db1 = "" OrElse db1 = "CheResources"
        End Function

        Sub New()

            ' ---------------------------------------------------------------- groups
            G(CompoundPropertyGroup.Identity, "Identification",
              "Who this compound is. The name is the key the simulation uses everywhere, so it cannot be changed here; the CAS number, formula and structure identify the substance and feed the elemental balance.")
            G(CompoundPropertyGroup.Constants, "Basic constants",
              "The handful of numbers every thermodynamic model needs. Molecular weight converts between mass and moles; the critical point and the acentric factor drive equations of state and most of the estimation methods DWSIM falls back on when a property is missing.")
            G(CompoundPropertyGroup.Formation, "Formation and combustion",
              "Reference-state energies used for reaction heat effects, Gibbs reactors and heating values. A pure separation model works without them; any reactor or combustion calculation needs them.")
            G(CompoundPropertyGroup.Solid, "Solid phase",
              "Melting point, heat of fusion and a single-point solid density. Needed only when the compound can precipitate or is handled as a solid.")
            G(CompoundPropertyGroup.ModelParameters, "Model-specific parameters",
              "Extra constants that particular property packages read: volume translation for cubic equations of state, UNIQUAC size parameters, Chao-Seader, COSTALD, PC-SAFT and transport-property helpers. Leave them at zero unless the package you use asks for them.")
            G(CompoundPropertyGroup.Composition, "Structure and groups",
              "The elemental composition (derived from the formula) and the functional-group breakdown used by UNIFAC-type activity models and by the Joback estimation method.")
            G(CompoundPropertyGroup.Electrolyte, "Electrolyte and ion data",
              "Only for ions and salts in electrolyte packages. Charge, dissociation stoichiometry and aqueous reference-state properties.", True)
            G(CompoundPropertyGroup.BlackOil, "Black oil",
              "Only for black-oil pseudo-compounds: the field data (gravities, GOR, water cut, viscosity points) that the black-oil correlations use instead of a molecular description.", True)
            G(CompoundPropertyGroup.PetroleumFraction, "Petroleum fraction",
              "Only for pseudo-components created by petroleum characterization: assay-derived properties such as the Watson K factor, specific gravity, viscosities and PNA composition.", True)
            G(CompoundPropertyGroup.Metadata, "Other",
              "Flags that tell DWSIM which external property engines know this compound.", True)

            ' ---------------------------------------------------------------- identity
            D("Name", "Name", CompoundPropertyGroup.Identity, CompoundPropertyKind.Text,
              "The name DWSIM uses as the key for this compound in every stream, reaction and property package of the simulation. It cannot be changed here: to rename a compound, create a copy with the new name and replace it.", isReadOnly:=True)
            D("ID", "ID", CompoundPropertyGroup.Identity, CompoundPropertyKind.Integer,
              "An internal number that identifies the compound in the property packages' caches. It is assigned by the database and must stay unique in a simulation, so it is read-only.", isReadOnly:=True)
            D("OriginalDB", "Source database", CompoundPropertyGroup.Identity, CompoundPropertyKind.Text,
              "Where the compound came from (DWSIM, ChemSep, CoolProp, User, ...). This matters more than it looks: it selects how the temperature-dependent equations are evaluated and in which unit their coefficients are read. A compound loaded from a JSON file is a User compound.", isReadOnly:=True)
            D("CurrentDB", "Current database", CompoundPropertyGroup.Identity, CompoundPropertyKind.Text,
              "The database the compound is currently listed under. It is set automatically when a compound is imported or saved to the user database.", isReadOnly:=True)
            D("CAS_Number", "CAS number", CompoundPropertyGroup.Identity, CompoundPropertyKind.Text,
              "The Chemical Abstracts registry number, the unambiguous identifier of a substance. Used for searching and for matching the compound in external databases. Pseudo-compounds can leave it blank or use a made-up value.")
            D("Formula", "Formula", CompoundPropertyGroup.Identity, CompoundPropertyKind.Text,
              "The molecular formula, for example C2H6O. Fractional subscripts are allowed for lumped pseudo-compounds (C14.08H27.75O11.67N1S0.33). Changing the formula rebuilds the element list, which is what elemental balances, Gibbs reactors and the formula check use. Keep it consistent with the molecular weight.")
            D("SMILES", "SMILES", CompoundPropertyGroup.Identity, CompoundPropertyKind.Text,
              "A text description of the molecular structure. Optional; it lets DWSIM fragment the molecule into functional groups for UNIFAC-type models and the Joback estimation method.")
            D("InChI", "InChI", CompoundPropertyGroup.Identity, CompoundPropertyKind.Text,
              "The IUPAC International Chemical Identifier. Optional, used for identification only.")
            D("ChemicalStructure", "Structure (Mol file)", CompoundPropertyGroup.Identity, CompoundPropertyKind.Text,
              "The molecular structure as a Mol-file text block, when available. Optional.")
            D("Comments", "Comments", CompoundPropertyGroup.Identity, CompoundPropertyKind.Text,
              "Free text saved with the compound: where the data came from, what was estimated, what to double-check. Import tools write their provenance notes here.")
            D("ChemSepFamily", "ChemSep family", CompoundPropertyGroup.Identity, CompoundPropertyKind.Integer,
              "The chemical family code from the ChemSep database (alkanes, alcohols, ...). Used to sort the compound list. 1000 means unclassified.", isAdvanced:=True)

            ' ---------------------------------------------------------------- constants
            D("Molar_Weight", "Molecular weight", CompoundPropertyGroup.Constants, CompoundPropertyKind.Double,
              "Mass of one kmol of the compound. DWSIM uses it to convert between mass and molar flows and compositions, and to convert per-mole property equations into per-mass values. A wrong value here silently distorts every mass balance, so keep it consistent with the formula.",
              "kg/kmol", Function(su) su.molecularWeight)
            D("Critical_Temperature", "Critical temperature", CompoundPropertyGroup.Constants, CompoundPropertyKind.Double,
              "Temperature above which the compound cannot be liquefied by pressure alone. Cubic equations of state (Peng-Robinson, SRK) are built from it, and so are the estimates DWSIM uses when vapor pressure, liquid density or viscosity are missing. For a non-volatile pseudo-compound a high value keeps it in the liquid.",
              "K", Function(su) su.temperature)
            D("Critical_Pressure", "Critical pressure", CompoundPropertyGroup.Constants, CompoundPropertyKind.Double,
              "Pressure at the critical point, stored in Pa. Needed by every equation of state and by the vapor-pressure and liquid-density estimates. A value below 100000 usually means it was typed in bar or kPa by mistake.",
              "Pa", Function(su) su.pressure)
            D("Critical_Volume", "Critical volume", CompoundPropertyGroup.Constants, CompoundPropertyKind.Double,
              "Molar volume at the critical point. Used by mixing rules for critical properties and by some transport-property correlations. If unknown, DWSIM can work without it for most models.",
              "m3/kmol", Function(su) su.molar_volume)
            D("Critical_Compressibility", "Critical compressibility (Zc)", CompoundPropertyGroup.Constants, CompoundPropertyKind.Double,
              "Zc = Pc.Vc/(R.Tc), dimensionless. Typical values lie between 0.2 and 0.35; a value far outside that range means one of the three critical constants is off.")
            D("Acentric_Factor", "Acentric factor", CompoundPropertyGroup.Constants, CompoundPropertyKind.Double,
              "Pitzer's acentric factor, dimensionless, a measure of how non-spherical the molecule is. Equations of state use it in their temperature function, and the Lee-Kesler vapor-pressure estimate and the Rackett liquid-density estimate depend on it when the dedicated equations are missing. Values above about 0.8 are outside where those estimates are reliable.")
            D("Normal_Boiling_Point", "Normal boiling point", CompoundPropertyGroup.Constants, CompoundPropertyKind.Double,
              "Boiling temperature at 1 atm. Used by thermal-conductivity and other estimation methods and as a sanity anchor for the vapor-pressure curve. It must be below the critical temperature.",
              "K", Function(su) su.temperature)
            D("Z_Rackett", "Rackett parameter (ZRA)", CompoundPropertyGroup.Constants, CompoundPropertyKind.Double,
              "The Rackett compressibility used to estimate saturated liquid density when no liquid-density equation is set. Leave it at zero to let DWSIM derive it from the acentric factor (ZRA = 0.29056 - 0.08775 w).")
            D("Dipole_Moment", "Dipole moment", CompoundPropertyGroup.Constants, CompoundPropertyKind.Double,
              "Electric dipole moment in C.m. Read by a few transport-property correlations; zero is fine for most purposes.", "C.m")

            ' ---------------------------------------------------------------- formation
            D("IG_Enthalpy_of_Formation_25C", "Ideal gas enthalpy of formation (25 C)", CompoundPropertyGroup.Formation, CompoundPropertyKind.Double,
              "Enthalpy of forming the compound from its elements at 25 C in the ideal gas state, per kg. It sets the enthalpy reference of the compound, so reaction heat effects and heating values come out right only when every reacting compound has it. With it at zero a reactor reports no heat of reaction.",
              "kJ/kg", Function(su) su.enthalpy)
            D("IG_Gibbs_Energy_of_Formation_25C", "Ideal gas Gibbs energy of formation (25 C)", CompoundPropertyGroup.Formation, CompoundPropertyKind.Double,
              "Gibbs energy of formation at 25 C, per kg. Used by Gibbs-minimization reactors and to compute equilibrium constants from thermodynamics. Not needed for kinetic or conversion reactors.",
              "kJ/kg", Function(su) su.enthalpy)
            D("IG_Entropy_of_Formation_25C", "Ideal gas entropy of formation (25 C)", CompoundPropertyGroup.Formation, CompoundPropertyKind.Double,
              "Entropy of formation at 25 C, per kg.K. Mostly derived from the enthalpy and Gibbs energy; used by the entropy reference of reacting systems.",
              "kJ/(kg.K)", Function(su) su.entropy)
            D("StandardHeatOfCombustion_LHV", "Lower heating value", CompoundPropertyGroup.Formation, CompoundPropertyKind.Double,
              "Standard net heat of combustion (water as vapor), per kg. Used for fuel heating values and combustion energy reports. Zero means unknown.",
              "kJ/kg", Function(su) su.enthalpy)

            ' ---------------------------------------------------------------- solid
            D("IsSolid", "Handle as solid", CompoundPropertyGroup.Solid, CompoundPropertyKind.Boolean,
              "Marks the compound as a solid for property packages that track a solid phase. It does not by itself remove the compound from vapor-liquid equilibrium: to keep an inert from evaporating, give it a high critical temperature as well.")
            D("TemperatureOfFusion", "Melting point", CompoundPropertyGroup.Solid, CompoundPropertyKind.Double,
              "Normal melting (fusion) temperature. Used by solid-liquid equilibrium and as the lower end of liquid-property ranges. Must be below the boiling point.",
              "K", Function(su) su.temperature)
            D("EnthalpyOfFusionAtTf", "Enthalpy of fusion", CompoundPropertyGroup.Solid, CompoundPropertyKind.Double,
              "Heat absorbed on melting at the melting point, in kJ/mol (note: per mol, not per kg). Needed for solid-liquid equilibrium and for the enthalpy of a solid phase.", "kJ/mol")
            D("SolidTs", "Solid density reference temperature", CompoundPropertyGroup.Solid, CompoundPropertyKind.Double,
              "Temperature at which the single-point solid density below was measured. Used only when no solid-density equation is set.",
              "K", Function(su) su.temperature)
            D("SolidDensityAtTs", "Single-point solid density", CompoundPropertyGroup.Solid, CompoundPropertyKind.Double,
              "Density of the solid at the reference temperature above. DWSIM uses it when the solid-density equation is empty; if both are missing, the solid gets a very large placeholder density and any solid volume is meaningless.",
              "kg/m3", Function(su) su.density)

            ' ---------------------------------------------------------------- model parameters
            D("PR_Volume_Translation_Coefficient", "PR volume translation", CompoundPropertyGroup.ModelParameters, CompoundPropertyKind.Double,
              "Peneloux-type volume shift for the Peng-Robinson equation of state, dimensionless. Improves liquid densities from the EOS; zero disables it.")
            D("SRK_Volume_Translation_Coefficient", "SRK volume translation", CompoundPropertyGroup.ModelParameters, CompoundPropertyKind.Double,
              "Volume shift for the Soave-Redlich-Kwong equation of state, dimensionless. Zero disables it.")
            D("UNIQUAC_R", "UNIQUAC r (volume)", CompoundPropertyGroup.ModelParameters, CompoundPropertyKind.Double,
              "Relative van der Waals volume of the molecule, used by UNIQUAC and the combinatorial part of UNIFAC. Required when the compound is used with those models.")
            D("UNIQUAC_Q", "UNIQUAC q (area)", CompoundPropertyGroup.ModelParameters, CompoundPropertyKind.Double,
              "Relative van der Waals surface area of the molecule, the second UNIQUAC size parameter.")
            D("Chao_Seader_Acentricity", "Chao-Seader acentric factor", CompoundPropertyGroup.ModelParameters, CompoundPropertyKind.Double,
              "Acentric factor as tabulated for the Chao-Seader and Grayson-Streed models. Only those two packages read it.")
            D("Chao_Seader_Solubility_Parameter", "Chao-Seader solubility parameter", CompoundPropertyGroup.ModelParameters, CompoundPropertyKind.Double,
              "Hildebrand solubility parameter in (cal/mL)^0.5 for the Chao-Seader and Grayson-Streed models.", "(cal/mL)^0.5")
            D("Chao_Seader_Liquid_Molar_Volume", "Chao-Seader liquid molar volume", CompoundPropertyGroup.ModelParameters, CompoundPropertyKind.Double,
              "Liquid molar volume in mL/mol for the Chao-Seader and Grayson-Streed models.", "mL/mol")
            D("PC_SAFT_m", "PC-SAFT segment number (m)", CompoundPropertyGroup.ModelParameters, CompoundPropertyKind.Double,
              "Number of segments per molecule in the PC-SAFT equation of state. Zero means the PC-SAFT parameters are not available for this compound.")
            D("PC_SAFT_sigma", "PC-SAFT segment diameter (sigma)", CompoundPropertyGroup.ModelParameters, CompoundPropertyKind.Double,
              "Segment diameter for PC-SAFT, in Angstrom.", "Angstrom")
            D("PC_SAFT_epsilon_k", "PC-SAFT energy (epsilon/k)", CompoundPropertyGroup.ModelParameters, CompoundPropertyKind.Double,
              "Segment dispersion energy divided by Boltzmann's constant for PC-SAFT, in K.", "K")
            D("COSTALD_SRK_Acentric_Factor", "COSTALD acentric factor", CompoundPropertyGroup.ModelParameters, CompoundPropertyKind.Double,
              "SRK-fitted acentric factor used by the COSTALD liquid-density correlation.", isAdvanced:=True)
            D("COSTALD_Characteristic_Volume", "COSTALD characteristic volume", CompoundPropertyGroup.ModelParameters, CompoundPropertyKind.Double,
              "Characteristic volume of the COSTALD liquid-density correlation, in m3/kmol.", "m3/kmol", isAdvanced:=True)
            D("LennardJonesDiameter", "Lennard-Jones diameter", CompoundPropertyGroup.ModelParameters, CompoundPropertyKind.Double,
              "Collision diameter of the Lennard-Jones potential, in m. Used by gas diffusivity estimates.", "m", isAdvanced:=True)
            D("LennardJonesEnergy", "Lennard-Jones energy (epsilon/k)", CompoundPropertyGroup.ModelParameters, CompoundPropertyKind.Double,
              "Well depth of the Lennard-Jones potential divided by Boltzmann's constant, in K. Used by gas diffusivity estimates.", "K", isAdvanced:=True)
            D("Parachor", "Parachor", CompoundPropertyGroup.ModelParameters, CompoundPropertyKind.Double,
              "Parachor for the Macleod-Sugden surface-tension mixing rule. Optional.", isAdvanced:=True)
            D("FullerDiffusionVolume", "Fuller diffusion volume", CompoundPropertyGroup.ModelParameters, CompoundPropertyKind.Double,
              "Atomic diffusion volume for the Fuller gas-diffusivity correlation. Optional.", isAdvanced:=True)

            ' ---------------------------------------------------------------- structure
            D("Elements", "Elements", CompoundPropertyGroup.Composition, CompoundPropertyKind.Elements,
              "Atoms of each element in one molecule, rebuilt automatically whenever the formula changes. Elemental balances, Gibbs reactors and combustion use this list, so it must agree with the formula and with the molecular weight.")
            D("UNIFACGroups", "UNIFAC groups", CompoundPropertyGroup.Composition, CompoundPropertyKind.Groups,
              "Functional-group breakdown (group id and count) for the original UNIFAC model and for Joback estimation. Empty means the compound cannot be used with UNIFAC.")
            D("MODFACGroups", "Modified UNIFAC (Dortmund) groups", CompoundPropertyGroup.Composition, CompoundPropertyKind.Groups,
              "Group breakdown for the Dortmund modified UNIFAC model.")
            D("NISTMODFACGroups", "NIST modified UNIFAC groups", CompoundPropertyGroup.Composition, CompoundPropertyKind.Groups,
              "Group breakdown for the NIST modified UNIFAC model.")

            ' ---------------------------------------------------------------- electrolyte
            D("IsIon", "Is an ion", CompoundPropertyGroup.Electrolyte, CompoundPropertyKind.Boolean,
              "Marks a charged species. Ions have no vapor pressure and are only handled by electrolyte property packages.", isAdvanced:=True)
            D("IsSalt", "Is a salt", CompoundPropertyGroup.Electrolyte, CompoundPropertyKind.Boolean,
              "Marks a salt that dissociates into the ions named below.", isAdvanced:=True)
            D("IsHydratedSalt", "Is a hydrated salt", CompoundPropertyGroup.Electrolyte, CompoundPropertyKind.Boolean,
              "Marks a salt that carries water of hydration (see hydration number).", isAdvanced:=True)
            D("HydrationNumber", "Hydration number", CompoundPropertyGroup.Electrolyte, CompoundPropertyKind.Double,
              "Number of water molecules per formula unit of a hydrated salt.", isAdvanced:=True)
            D("Charge", "Charge", CompoundPropertyGroup.Electrolyte, CompoundPropertyKind.Integer,
              "Electric charge of the ion (for example -1 for Cl-, +2 for Ca2+). Zero for neutral species.", isAdvanced:=True)
            D("PositiveIon", "Positive ion", CompoundPropertyGroup.Electrolyte, CompoundPropertyKind.Text,
              "Name of the cation the salt dissociates into.", isAdvanced:=True)
            D("NegativeIon", "Negative ion", CompoundPropertyGroup.Electrolyte, CompoundPropertyKind.Text,
              "Name of the anion the salt dissociates into.", isAdvanced:=True)
            D("PositiveIonStoichCoeff", "Positive ion coefficient", CompoundPropertyGroup.Electrolyte, CompoundPropertyKind.Integer,
              "Moles of cation released per mole of salt.", isAdvanced:=True)
            D("NegativeIonStoichCoeff", "Negative ion coefficient", CompoundPropertyGroup.Electrolyte, CompoundPropertyKind.Integer,
              "Moles of anion released per mole of salt.", isAdvanced:=True)
            D("StoichSum", "Ions per formula unit", CompoundPropertyGroup.Electrolyte, CompoundPropertyKind.Integer,
              "Total moles of ions released per mole of salt (the two coefficients added).", isAdvanced:=True)
            D("Electrolyte_DelGF", "Aqueous Gibbs energy of formation", CompoundPropertyGroup.Electrolyte, CompoundPropertyKind.Double,
              "Standard Gibbs energy of formation in aqueous solution, in kJ/mol. Drives the dissociation and speciation equilibria of electrolyte packages.", "kJ/mol", isAdvanced:=True)
            D("Electrolyte_DelHF", "Aqueous enthalpy of formation", CompoundPropertyGroup.Electrolyte, CompoundPropertyKind.Double,
              "Standard enthalpy of formation in aqueous solution, in kJ/mol.", "kJ/mol", isAdvanced:=True)
            D("Electrolyte_Cp0", "Aqueous heat capacity", CompoundPropertyGroup.Electrolyte, CompoundPropertyKind.Double,
              "Standard-state heat capacity in aqueous solution, in kJ/(mol.K).", "kJ/(mol.K)", isAdvanced:=True)
            D("StandardStateMolarVolume", "Standard-state molar volume", CompoundPropertyGroup.Electrolyte, CompoundPropertyKind.Double,
              "Partial molar volume of the species in solution at standard state, in cm3/mol.", "cm3/mol", isAdvanced:=True)
            D("MolarVolume_v2i", "Molar volume parameter v2", CompoundPropertyGroup.Electrolyte, CompoundPropertyKind.Double,
              "Coefficient of the apparent molar volume correlation used by electrolyte packages.", isAdvanced:=True)
            D("MolarVolume_v3i", "Molar volume parameter v3", CompoundPropertyGroup.Electrolyte, CompoundPropertyKind.Double,
              "Coefficient of the apparent molar volume correlation used by electrolyte packages.", isAdvanced:=True)
            D("MolarVolume_k1i", "Molar volume parameter k1", CompoundPropertyGroup.Electrolyte, CompoundPropertyKind.Double,
              "Coefficient of the apparent molar volume correlation used by electrolyte packages.", isAdvanced:=True)
            D("MolarVolume_k2i", "Molar volume parameter k2", CompoundPropertyGroup.Electrolyte, CompoundPropertyKind.Double,
              "Coefficient of the apparent molar volume correlation used by electrolyte packages.", isAdvanced:=True)
            D("MolarVolume_k3i", "Molar volume parameter k3", CompoundPropertyGroup.Electrolyte, CompoundPropertyKind.Double,
              "Coefficient of the apparent molar volume correlation used by electrolyte packages.", isAdvanced:=True)
            D("Ion_CpAq_a", "Aqueous Cp coefficient a", CompoundPropertyGroup.Electrolyte, CompoundPropertyKind.Double,
              "Coefficient of the temperature-dependent aqueous heat capacity of the ion.", isAdvanced:=True)
            D("Ion_CpAq_b", "Aqueous Cp coefficient b", CompoundPropertyGroup.Electrolyte, CompoundPropertyKind.Double,
              "Coefficient of the temperature-dependent aqueous heat capacity of the ion.", isAdvanced:=True)
            D("Ion_CpAq_c", "Aqueous Cp coefficient c", CompoundPropertyGroup.Electrolyte, CompoundPropertyKind.Double,
              "Coefficient of the temperature-dependent aqueous heat capacity of the ion.", isAdvanced:=True)

            ' ---------------------------------------------------------------- black oil
            D("IsBlackOil", "Is a black-oil pseudo-compound", CompoundPropertyGroup.BlackOil, CompoundPropertyKind.Boolean,
              "Marks a black-oil fluid described by field data instead of a molecular formula. Only the Black Oil property package uses these compounds.", isAdvanced:=True)
            D("BO_SGG", "Gas specific gravity", CompoundPropertyGroup.BlackOil, CompoundPropertyKind.Double,
              "Specific gravity of the associated gas relative to air.", isAdvanced:=True)
            D("BO_SGO", "Oil specific gravity", CompoundPropertyGroup.BlackOil, CompoundPropertyKind.Double,
              "Specific gravity of the stock-tank oil relative to water.", isAdvanced:=True)
            D("BO_GOR", "Gas-oil ratio", CompoundPropertyGroup.BlackOil, CompoundPropertyKind.Double,
              "Solution gas-oil ratio at standard conditions.", "m3/m3", Function(su) su.gor, isAdvanced:=True)
            D("BO_BSW", "Basic sediments and water", CompoundPropertyGroup.BlackOil, CompoundPropertyKind.Double,
              "Water cut of the produced liquid, as a fraction.", isAdvanced:=True)
            D("BO_OilVisc1", "Oil viscosity at T1", CompoundPropertyGroup.BlackOil, CompoundPropertyKind.Double,
              "Kinematic viscosity of the dead oil at the first reference temperature.", "m2/s", Function(su) su.cinematic_viscosity, isAdvanced:=True)
            D("BO_OilViscTemp1", "Viscosity reference temperature T1", CompoundPropertyGroup.BlackOil, CompoundPropertyKind.Double,
              "First reference temperature of the oil viscosity data.", "K", Function(su) su.temperature, isAdvanced:=True)
            D("BO_OilVisc2", "Oil viscosity at T2", CompoundPropertyGroup.BlackOil, CompoundPropertyKind.Double,
              "Kinematic viscosity of the dead oil at the second reference temperature.", "m2/s", Function(su) su.cinematic_viscosity, isAdvanced:=True)
            D("BO_OilViscTemp2", "Viscosity reference temperature T2", CompoundPropertyGroup.BlackOil, CompoundPropertyKind.Double,
              "Second reference temperature of the oil viscosity data.", "K", Function(su) su.temperature, isAdvanced:=True)
            D("BO_PNA_P", "Paraffins fraction", CompoundPropertyGroup.BlackOil, CompoundPropertyKind.Double,
              "Paraffin content of the oil (PNA analysis), as a fraction.", isAdvanced:=True)
            D("BO_PNA_N", "Naphthenes fraction", CompoundPropertyGroup.BlackOil, CompoundPropertyKind.Double,
              "Naphthene content of the oil (PNA analysis), as a fraction.", isAdvanced:=True)
            D("BO_PNA_A", "Aromatics fraction", CompoundPropertyGroup.BlackOil, CompoundPropertyKind.Double,
              "Aromatic content of the oil (PNA analysis), as a fraction.", isAdvanced:=True)
            D("BO_RsMult", "Solution GOR multiplier", CompoundPropertyGroup.BlackOil, CompoundPropertyKind.Double,
              "Tuning multiplier applied to the correlated solution gas-oil ratio. 1 leaves the correlation as is.", isAdvanced:=True)
            D("BO_BoMult", "Oil formation volume factor multiplier", CompoundPropertyGroup.BlackOil, CompoundPropertyKind.Double,
              "Tuning multiplier applied to the correlated oil formation volume factor.", isAdvanced:=True)
            D("BO_PbMult", "Bubble point pressure multiplier", CompoundPropertyGroup.BlackOil, CompoundPropertyKind.Double,
              "Tuning multiplier applied to the correlated bubble point pressure.", isAdvanced:=True)
            D("BO_OilViscMult", "Oil viscosity multiplier", CompoundPropertyGroup.BlackOil, CompoundPropertyKind.Double,
              "Tuning multiplier applied to the correlated oil viscosity.", isAdvanced:=True)

            ' ---------------------------------------------------------------- petroleum fraction
            D("IsPF", "Is a petroleum fraction", CompoundPropertyGroup.PetroleumFraction, CompoundPropertyKind.Integer,
              "1 when the compound is a pseudo-component from petroleum characterization, 0 otherwise. For petroleum fractions DWSIM estimates vapor pressure and heat capacity from the Watson K factor and the acentric factor instead of the equation blocks.", isAdvanced:=True)
            D("IsHYPO", "Is hypothetical", CompoundPropertyGroup.PetroleumFraction, CompoundPropertyKind.Integer,
              "1 when the compound was created by the hypothetical-component estimator, 0 otherwise. Informational.", isAdvanced:=True)
            D("PF_Watson_K", "Watson K factor", CompoundPropertyGroup.PetroleumFraction, CompoundPropertyKind.NullableDouble,
              "Watson characterization factor of the fraction (about 10 for aromatic, 13 for paraffinic cuts). Drives the petroleum-fraction property estimates. Blank when not a petroleum fraction.", isAdvanced:=True)
            D("PF_SG", "Specific gravity", CompoundPropertyGroup.PetroleumFraction, CompoundPropertyKind.NullableDouble,
              "Specific gravity of the fraction at 60 F relative to water.", isAdvanced:=True)
            D("PF_vA", "Viscosity parameter A", CompoundPropertyGroup.PetroleumFraction, CompoundPropertyKind.NullableDouble,
              "First parameter of the fraction's viscosity-temperature correlation.", isAdvanced:=True)
            D("PF_vB", "Viscosity parameter B", CompoundPropertyGroup.PetroleumFraction, CompoundPropertyKind.NullableDouble,
              "Second parameter of the fraction's viscosity-temperature correlation.", isAdvanced:=True)
            D("PF_v1", "Viscosity at T1", CompoundPropertyGroup.PetroleumFraction, CompoundPropertyKind.NullableDouble,
              "Kinematic viscosity of the fraction at the first reference temperature.", isAdvanced:=True)
            D("PF_Tv1", "Viscosity reference temperature T1", CompoundPropertyGroup.PetroleumFraction, CompoundPropertyKind.NullableDouble,
              "First reference temperature of the fraction's viscosity data, in K.", "K", isAdvanced:=True)
            D("PF_v2", "Viscosity at T2", CompoundPropertyGroup.PetroleumFraction, CompoundPropertyKind.NullableDouble,
              "Kinematic viscosity of the fraction at the second reference temperature.", isAdvanced:=True)
            D("PF_Tv2", "Viscosity reference temperature T2", CompoundPropertyGroup.PetroleumFraction, CompoundPropertyKind.NullableDouble,
              "Second reference temperature of the fraction's viscosity data, in K.", "K", isAdvanced:=True)
            D("PF_xP", "Paraffins fraction", CompoundPropertyGroup.PetroleumFraction, CompoundPropertyKind.NullableDouble,
              "Paraffin content of the fraction (PNA analysis).", isAdvanced:=True)
            D("PF_xN", "Naphthenes fraction", CompoundPropertyGroup.PetroleumFraction, CompoundPropertyKind.NullableDouble,
              "Naphthene content of the fraction (PNA analysis).", isAdvanced:=True)
            D("PF_xA", "Aromatics fraction", CompoundPropertyGroup.PetroleumFraction, CompoundPropertyKind.NullableDouble,
              "Aromatic content of the fraction (PNA analysis).", isAdvanced:=True)
            D("PF_n20", "Refractive index at 20 C", CompoundPropertyGroup.PetroleumFraction, CompoundPropertyKind.NullableDouble,
              "Refractive index of the fraction at 20 C, used by PNA estimation.", isAdvanced:=True)
            D("PF_Ri", "Refractivity intercept", CompoundPropertyGroup.PetroleumFraction, CompoundPropertyKind.NullableDouble,
              "Refractivity intercept of the fraction, used by PNA estimation.", isAdvanced:=True)

            ' ---------------------------------------------------------------- metadata
            D("IsCOOLPROPSupported", "Supported by CoolProp", CompoundPropertyGroup.Metadata, CompoundPropertyKind.Boolean,
              "True when the CoolProp library has a reference equation of state for this compound, so the CoolProp property package can use it directly.", isAdvanced:=True)

            ' interface members outside the editor
            ExcludedKeys.AddRange({"ExtraProperties", "IsModified", "IsFPROPSSupported", "CompCreatorStudyFile", "LinkedJsonFile",
                                   "COSMODBName", "Tag", "NBP", "PF_MM"})

            ' ---------------------------------------------------------------- temperature-dependent blocks
            Dim honoured As Func(Of ICompoundConstantProperties, Boolean) = Function(cp) Not UsesBuiltInForms(cp)
            Dim always As Func(Of ICompoundConstantProperties, Boolean) = Function(cp) True

            Blocks.Add(New TemperatureDependentBlock("VaporPressure", "Vapor pressure",
                "Saturation pressure of the pure compound as a function of temperature: the property that decides how volatile the compound is in every flash. T is in K and the equation must return Pa. When no equation is set, DWSIM estimates it with the Lee-Kesler correlation from Tc, Pc and the acentric factor, which is fine for a non-volatile pseudo-compound but rough for anything that actually boils in the process.",
                "VaporPressureEquation", {"Vapor_Pressure_Constant_A", "Vapor_Pressure_Constant_B", "Vapor_Pressure_Constant_C", "Vapor_Pressure_Constant_D", "Vapor_Pressure_Constant_E"},
                "Vapor_Pressure_TMIN", "Vapor_Pressure_TMAX", "Vapor_Pressure_Regression_Fit", "Vapor_Pressure_Tabular_Data",
                "Pa", Function(su) su.pressure,
                Function(cp)
                    Select Case Db(cp)
                        Case "CheResources" : Return "mmHg"
                        Case "Biodiesel" : Return "kPa"
                        Case "DWSIM", "" : Return "Pa, from the built-in form exp(A + B/T + C ln T + D T^E)"
                        Case Else : Return "Pa"
                    End Select
                End Function,
                honoured,
                Function(cp, T)
                    Dim m As String = ""
                    Return cp.GetVaporPressure(T, m)
                End Function,
                Function(cp, T)
                    Dim m As String = ""
                    cp.GetVaporPressure(T, m)
                    Return m
                End Function,
                "Estimated with the Lee-Kesler correlation from the critical temperature, critical pressure and acentric factor."))

            Blocks.Add(New TemperatureDependentBlock("IdealGasHeatCapacity", "Ideal gas heat capacity",
                "Heat capacity of the compound as an ideal gas. It is the backbone of every enthalpy and entropy DWSIM computes: liquid and real-gas values are built from it plus departure functions. T is in K; for a User or ChemSep compound the equation must return J/(kmol.K), which DWSIM then divides by 1000 and by the molecular weight. If the block is empty the compound gets a constant default of 3.5 R, which keeps the simulation running but makes heat duties wrong.",
                "IdealgasCpEquation", {"Ideal_Gas_Heat_Capacity_Const_A", "Ideal_Gas_Heat_Capacity_Const_B", "Ideal_Gas_Heat_Capacity_Const_C", "Ideal_Gas_Heat_Capacity_Const_D", "Ideal_Gas_Heat_Capacity_Const_E"},
                Nothing, Nothing, "Ideal_Gas_Heat_Capacity_Regression_Fit", "Ideal_Gas_Heat_Capacity_Tabular_Data",
                "kJ/(kg.K)", Function(su) su.heatCapacityCp,
                Function(cp)
                    Select Case Db(cp)
                        Case "CheResources" : Return "cal/(mol.K)"
                        Case "CoolProp", "ChEDL Thermo", "Biodiesel" : Return "kJ/(kg.K)"
                        Case "DWSIM", "" : Return "kJ/(kmol.K), from the built-in polynomial A + B T + C T^2 + D T^3 + E T^4"
                        Case Else : Return "J/(kmol.K)"
                    End Select
                End Function,
                honoured,
                Function(cp, T)
                    Dim m As String = ""
                    Return cp.GetIdealGasHeatCapacity(T, m)
                End Function,
                Function(cp, T)
                    Dim m As String = ""
                    cp.GetIdealGasHeatCapacity(T, m)
                    Return m
                End Function,
                "A constant default of 3.5 R per mole (about 29 J/(mol.K)) is used, so enthalpy changes with temperature are only roughly right."))

            Blocks.Add(New TemperatureDependentBlock("EnthalpyOfVaporization", "Enthalpy of vaporization",
                "Heat needed to evaporate the compound at a given temperature. Used for the enthalpy of the liquid when the property package builds it from the ideal gas. T is in K; for a User or ChemSep compound the equation returns J/kmol. With the equation empty, only coefficient A is used, read as the value in kJ/kg at the boiling point and scaled to other temperatures with the Watson rule; with A also zero DWSIM estimates it with the Vetere method.",
                "VaporizationEnthalpyEquation", {"HVap_A", "HVap_B", "HVap_C", "HVap_D", "HVap_E"},
                "HVap_TMIN", "HVap_TMAX", "Enthalpy_Of_Vaporization_Regression_Fit", "Enthalpy_Of_Vaporization_Tabular_Data",
                "kJ/kg", Function(su) su.enthalpy,
                Function(cp)
                    Select Case Db(cp)
                        Case "CheResources", "CoolProp", "KDB" : Return "kJ/kg"
                        Case "DWSIM", "" : Return "kJ/kg, from the built-in Watson form using A only"
                        Case Else : Return "J/kmol"
                    End Select
                End Function,
                honoured,
                Function(cp, T)
                    Dim m As String = ""
                    Return cp.GetEnthalpyOfVaporization(T, m)
                End Function,
                Function(cp, T)
                    Dim m As String = ""
                    cp.GetEnthalpyOfVaporization(T, m)
                    Return m
                End Function,
                "Coefficient A alone is used as the value at the boiling point (kJ/kg) and scaled with the Watson rule; if A is zero, the Vetere estimate is used."))

            Blocks.Add(New TemperatureDependentBlock("LiquidViscosity", "Liquid viscosity",
                "Dynamic viscosity of the liquid. Only transport calculations use it (pipes, pumps, heat exchangers, mixing), never the phase equilibrium. T is in K and the equation returns Pa.s. When empty DWSIM uses the Letsou-Stiel estimate.",
                "LiquidViscosityEquation", {"Liquid_Viscosity_Const_A", "Liquid_Viscosity_Const_B", "Liquid_Viscosity_Const_C", "Liquid_Viscosity_Const_D", "Liquid_Viscosity_Const_E"},
                Nothing, Nothing, "Liquid_Viscosity_Regression_Fit", "Liquid_Viscosity_Tabular_Data",
                "Pa.s", Function(su) su.viscosityOfLiquid,
                Function(cp)
                    Select Case Db(cp)
                        Case "DWSIM", "", "CheResources" : Return "Pa.s, from a built-in form"
                        Case Else : Return "Pa.s"
                    End Select
                End Function,
                honoured,
                Function(cp, T)
                    Dim m As String = ""
                    Return cp.GetLiquidViscosity(T, m)
                End Function,
                Function(cp, T)
                    Dim m As String = ""
                    cp.GetLiquidViscosity(T, m)
                    Return m
                End Function,
                "Estimated with the Letsou-Stiel correlation."))

            Blocks.Add(New TemperatureDependentBlock("VaporViscosity", "Vapor viscosity",
                "Dynamic viscosity of the vapor, used by pipe and equipment pressure-drop calculations. T is in K and the equation returns Pa.s. When empty DWSIM uses the Lucas estimate.",
                "VaporViscosityEquation", {"Vapor_Viscosity_Const_A", "Vapor_Viscosity_Const_B", "Vapor_Viscosity_Const_C", "Vapor_Viscosity_Const_D", "Vapor_Viscosity_Const_E"},
                "Vapor_Viscosity_Tmin", "Vapor_Viscosity_Tmax", "Vapor_Viscosity_Regression_Fit", "Vapor_Viscosity_Tabular_Data",
                "Pa.s", Function(su) su.viscosityOfVapor,
                Function(cp) "Pa.s",
                always,
                Function(cp, T)
                    Dim m As String = ""
                    Return cp.GetVaporViscosity(T, m)
                End Function,
                Function(cp, T)
                    Dim m As String = ""
                    cp.GetVaporViscosity(T, m)
                    Return m
                End Function,
                "Estimated with the Lucas correlation."))

            Blocks.Add(New TemperatureDependentBlock("LiquidDensity", "Liquid density",
                "Density of the saturated liquid. Used for volumes, volumetric flows, vessel sizing and pipe hydraulics, and by some property packages for the liquid molar volume. T is in K. Careful with the unit: for a User (JSON), CoolProp or ChEDL compound the equation must return kg/m3, but for a ChemSep compound it returns kmol/m3 and DWSIM multiplies by the molecular weight. When empty DWSIM uses the Rackett estimate.",
                "LiquidDensityEquation", {"Liquid_Density_Const_A", "Liquid_Density_Const_B", "Liquid_Density_Const_C", "Liquid_Density_Const_D", "Liquid_Density_Const_E"},
                "Liquid_Density_Tmin", "Liquid_Density_Tmax", "Liquid_Density_Regression_Fit", "Liquid_Density_Tabular_Data",
                "kg/m3", Function(su) su.density,
                Function(cp)
                    Select Case Db(cp)
                        Case "User", "CoolProp", "ChEDL Thermo" : Return "kg/m3"
                        Case Else : Return "kmol/m3 (DWSIM multiplies it by the molecular weight)"
                    End Select
                End Function,
                always,
                Function(cp, T)
                    Dim m As String = ""
                    Return cp.GetLiquidDensity(T, m)
                End Function,
                Function(cp, T)
                    Dim m As String = ""
                    cp.GetLiquidDensity(T, m)
                    Return m
                End Function,
                "Estimated with the Rackett equation from Tc, Pc and the Rackett parameter (or the acentric factor when that is zero)."))

            Blocks.Add(New TemperatureDependentBlock("LiquidHeatCapacity", "Liquid heat capacity",
                "Heat capacity of the liquid, used for liquid enthalpies by the packages that do not derive them from the equation of state. T is in K; for a User or ChemSep compound the equation returns J/(kmol.K). When empty DWSIM uses the Rowlinson-Bondi estimate built on the ideal gas heat capacity.",
                "LiquidHeatCapacityEquation", {"Liquid_Heat_Capacity_Const_A", "Liquid_Heat_Capacity_Const_B", "Liquid_Heat_Capacity_Const_C", "Liquid_Heat_Capacity_Const_D", "Liquid_Heat_Capacity_Const_E"},
                "Liquid_Heat_Capacity_Tmin", "Liquid_Heat_Capacity_Tmax", "Liquid_Heat_Capacity_Regression_Fit", "Liquid_Heat_Capacity_Tabular_Data",
                "kJ/(kg.K)", Function(su) su.heatCapacityCp,
                Function(cp)
                    Select Case Db(cp)
                        Case "CoolProp", "ChEDL Thermo" : Return "kJ/(kg.K)"
                        Case Else : Return "J/(kmol.K)"
                    End Select
                End Function,
                always,
                Function(cp, T)
                    Dim m As String = ""
                    Return cp.GetLiquidHeatCapacity(T, m)
                End Function,
                Function(cp, T)
                    Dim m As String = ""
                    cp.GetLiquidHeatCapacity(T, m)
                    Return m
                End Function,
                "Estimated with the Rowlinson-Bondi correlation from the ideal gas heat capacity."))

            Blocks.Add(New TemperatureDependentBlock("LiquidThermalConductivity", "Liquid thermal conductivity",
                "Thermal conductivity of the liquid, used by heat-exchanger and pipe heat-transfer calculations. T is in K and the equation returns W/(m.K). When empty DWSIM uses the Latini estimate.",
                "LiquidThermalConductivityEquation", {"Liquid_Thermal_Conductivity_Const_A", "Liquid_Thermal_Conductivity_Const_B", "Liquid_Thermal_Conductivity_Const_C", "Liquid_Thermal_Conductivity_Const_D", "Liquid_Thermal_Conductivity_Const_E"},
                "Liquid_Thermal_Conductivity_Tmin", "Liquid_Thermal_Conductivity_Tmax", "Liquid_Thermal_Conductivity_Regression_Fit", "Liquid_Thermal_Conductivity_Tabular_Data",
                "W/(m.K)", Function(su) su.thermalConductivityOfLiquid,
                Function(cp) "W/(m.K)",
                always,
                Function(cp, T)
                    Dim m As String = ""
                    Return cp.GetLiquidThermalConductivity(T, m)
                End Function,
                Function(cp, T)
                    Dim m As String = ""
                    cp.GetLiquidThermalConductivity(T, m)
                    Return m
                End Function,
                "Estimated with the Latini correlation."))

            Blocks.Add(New TemperatureDependentBlock("VaporThermalConductivity", "Vapor thermal conductivity",
                "Thermal conductivity of the vapor, used by heat-transfer calculations. T is in K and the equation returns W/(m.K). When empty DWSIM uses the Ely-Hanley estimate.",
                "VaporThermalConductivityEquation", {"Vapor_Thermal_Conductivity_Const_A", "Vapor_Thermal_Conductivity_Const_B", "Vapor_Thermal_Conductivity_Const_C", "Vapor_Thermal_Conductivity_Const_D", "Vapor_Thermal_Conductivity_Const_E"},
                "Vapor_Thermal_Conductivity_Tmin", "Vapor_Thermal_Conductivity_Tmax", "Vapor_Thermal_Conductivity_Regression_Fit", "Vapor_Thermal_Conductivity_Tabular_Data",
                "W/(m.K)", Function(su) su.thermalConductivityOfVapor,
                Function(cp) "W/(m.K)",
                always,
                Function(cp, T)
                    Dim m As String = ""
                    Return cp.GetVaporThermalConductivity(T, m)
                End Function,
                Function(cp, T)
                    Dim m As String = ""
                    cp.GetVaporThermalConductivity(T, m)
                    Return m
                End Function,
                "Estimated with the Ely-Hanley correlation."))

            Blocks.Add(New TemperatureDependentBlock("SurfaceTension", "Surface tension",
                "Surface tension of the liquid against its vapor, used by separator sizing and some two-phase flow correlations. T is in K and the equation returns N/m. When empty DWSIM uses the Brock-Bird estimate.",
                "SurfaceTensionEquation", {"Surface_Tension_Const_A", "Surface_Tension_Const_B", "Surface_Tension_Const_C", "Surface_Tension_Const_D", "Surface_Tension_Const_E"},
                "Surface_Tension_Tmin", "Surface_Tension_Tmax", "Surface_Tension_Regression_Fit", "Surface_Tension_Tabular_Data",
                "N/m", Function(su) su.surfaceTension,
                Function(cp) "N/m",
                always,
                Function(cp, T)
                    Dim m As String = ""
                    Return cp.GetLiquidSurfaceTension(T, m)
                End Function,
                Function(cp, T)
                    Dim m As String = ""
                    cp.GetLiquidSurfaceTension(T, m)
                    Return m
                End Function,
                "Estimated with the Brock-Bird correlation."))

            Blocks.Add(New TemperatureDependentBlock("SolidDensity", "Solid density",
                "Density of the solid phase. T is in K. Careful with the unit: unlike the liquid density, for a User (JSON) or ChemSep compound the equation must return kmol/m3 and DWSIM multiplies by the molecular weight; only ChEDL compounds return kg/m3. When empty DWSIM uses the single-point solid density from the Solid phase group, and with that missing too a placeholder density so large that solid volumes are meaningless.",
                "SolidDensityEquation", {"Solid_Density_Const_A", "Solid_Density_Const_B", "Solid_Density_Const_C", "Solid_Density_Const_D", "Solid_Density_Const_E"},
                "Solid_Density_Tmin", "Solid_Density_Tmax", "Solid_Density_Regression_Fit", "Solid_Density_Tabular_Data",
                "kg/m3", Function(su) su.density,
                Function(cp)
                    Select Case Db(cp)
                        Case "ChEDL Thermo" : Return "kg/m3"
                        Case Else : Return "kmol/m3 (DWSIM multiplies it by the molecular weight)"
                    End Select
                End Function,
                always,
                Function(cp, T)
                    Dim m As String = ""
                    Return cp.GetSolidDensity(T, m)
                End Function,
                Function(cp, T)
                    Dim m As String = ""
                    cp.GetSolidDensity(T, m)
                    Return m
                End Function,
                "The single-point solid density is used; if that is zero as well, a placeholder value is used and solid volumes are meaningless."))

            Blocks.Add(New TemperatureDependentBlock("SolidHeatCapacity", "Solid heat capacity",
                "Heat capacity of the solid phase, used for solid enthalpies. T is in K; for a User or ChemSep compound the equation returns J/(kmol.K), for ChEDL compounds kJ/(kg.K). When empty DWSIM falls back to the liquid heat capacity.",
                "SolidHeatCapacityEquation", {"Solid_Heat_Capacity_Const_A", "Solid_Heat_Capacity_Const_B", "Solid_Heat_Capacity_Const_C", "Solid_Heat_Capacity_Const_D", "Solid_Heat_Capacity_Const_E"},
                "Solid_Heat_Capacity_Tmin", "Solid_Heat_Capacity_Tmax", "Solid_Heat_Capacity_Regression_Fit", "Solid_Heat_Capacity_Tabular_Data",
                "kJ/(kg.K)", Function(su) su.heatCapacityCp,
                Function(cp)
                    Select Case Db(cp)
                        Case "ChEDL Thermo" : Return "kJ/(kg.K)"
                        Case Else : Return "J/(kmol.K)"
                    End Select
                End Function,
                always,
                Function(cp, T)
                    Dim m As String = ""
                    Return cp.GetSolidHeatCapacity(T, m)
                End Function,
                Function(cp, T)
                    Dim m As String = ""
                    cp.GetSolidHeatCapacity(T, m)
                    Return m
                End Function,
                "The liquid heat capacity is used in its place."))

            For Each b In Blocks
                _blockByKey(b.Key) = b
            Next

        End Sub

        Public Function ByKey(key As String) As CompoundPropertyDescriptor
            Dim d1 As CompoundPropertyDescriptor = Nothing
            If _byKey.TryGetValue(key, d1) Then Return d1
            ' block-owned keys are addressable too, with sensible defaults
            Return BlockOwnedDescriptor(key)
        End Function

        Private ReadOnly _blockOwned As New Dictionary(Of String, CompoundPropertyDescriptor)

        ''' <summary>Descriptors for the equation, coefficient, range, fit and tabular keys, created on demand.</summary>
        Private Function BlockOwnedDescriptor(key As String) As CompoundPropertyDescriptor
            Dim d1 As CompoundPropertyDescriptor = Nothing
            SyncLock _blockOwned
                If _blockOwned.TryGetValue(key, d1) Then Return d1
                For Each b In Blocks
                    If b.EquationKey = key Then
                        d1 = New CompoundPropertyDescriptor(key, b.DisplayName & " equation", CompoundPropertyGroup.Constants, CompoundPropertyKind.Text, "", Nothing, False, False,
                                                            "The equation number (or free-form expression) DWSIM evaluates for this property. Pick it from the list to see its formula.")
                    ElseIf b.CoefficientKeys.Contains(key) Then
                        Dim letter = key.Substring(key.Length - 1)
                        d1 = New CompoundPropertyDescriptor(key, b.DisplayName & " coefficient " & letter, CompoundPropertyGroup.Constants, CompoundPropertyKind.Double, "", Nothing, False, False,
                                                            "Coefficient " & letter & " of the " & b.DisplayName.ToLowerInvariant() & " equation. Its meaning and unit depend on the equation number; T is always in K.")
                    ElseIf b.TminKey = key Then
                        d1 = New CompoundPropertyDescriptor(key, b.DisplayName & " minimum temperature", CompoundPropertyGroup.Constants, CompoundPropertyKind.Double, "K", Function(su) su.temperature, False, False,
                                                            "Lower end of the temperature range the equation was fitted for. DWSIM does not stop at it, so values outside the range are extrapolations.")
                    ElseIf b.TmaxKey = key Then
                        d1 = New CompoundPropertyDescriptor(key, b.DisplayName & " maximum temperature", CompoundPropertyGroup.Constants, CompoundPropertyKind.Double, "K", Function(su) su.temperature, False, False,
                                                            "Upper end of the temperature range the equation was fitted for.")
                    ElseIf b.RegressionFitKey = key Then
                        d1 = New CompoundPropertyDescriptor(key, b.DisplayName & " regression fit", CompoundPropertyGroup.Constants, CompoundPropertyKind.Double, "", Nothing, True, False,
                                                            "Goodness of fit left by the regression tool when the coefficients were fitted to data. Informational.")
                    ElseIf b.TabularDataKey = key Then
                        d1 = New CompoundPropertyDescriptor(key, b.DisplayName & " experimental data", CompoundPropertyGroup.Constants, CompoundPropertyKind.Tabular, "", Nothing, False, False,
                                                            "The data points the coefficients were fitted to, when they were imported or regressed. DWSIM evaluates the equation, not the points; they are kept for reference and re-fitting.")
                    End If
                    If d1 IsNot Nothing Then Exit For
                Next
                If d1 Is Nothing Then Throw New KeyNotFoundException("No compound property descriptor for '" & key & "'.")
                _blockOwned(key) = d1
            End SyncLock
            Return d1
        End Function

        Public Function BlockByKey(key As String) As TemperatureDependentBlock
            Return _blockByKey(key)
        End Function

        ''' <summary>The block that owns a given property key, or Nothing.</summary>
        Public Function BlockOwning(propertyKey As String) As TemperatureDependentBlock
            For Each b In Blocks
                If b.Keys().Contains(propertyKey) Then Return b
            Next
            Return Nothing
        End Function

        Public Function ScalarsInGroup(group As CompoundPropertyGroup) As List(Of CompoundPropertyDescriptor)
            Return Scalars.Where(Function(d1) d1.Group = group).ToList()
        End Function

        ''' <summary>Scalars, block-owned keys and excluded keys: must equal the interface's property list.</summary>
        Public Function AllKeys() As List(Of String)
            Dim l As New List(Of String)
            l.AddRange(Scalars.Select(Function(d1) d1.Key))
            For Each b In Blocks
                l.AddRange(b.Keys())
            Next
            l.AddRange(ExcludedKeys)
            Return l
        End Function

    End Module

End Namespace
