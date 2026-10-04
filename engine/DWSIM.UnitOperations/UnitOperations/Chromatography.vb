'    Chromatography Column (simplified equilibrium / Langmuir binding capacity model)
'    Copyright 2026 Daniel Wagner O. de Medeiros
'
'    This file is part of DWSIM.

Imports DWSIM.Thermodynamics.BaseClasses
Imports System.Math
Imports System.Linq
Imports DWSIM.Interfaces
Imports DWSIM.Interfaces.Enums
Imports DWSIM.Interfaces.Enums.GraphicObjects
Imports DWSIM.DrawingTools.Point
Imports DWSIM.Drawing.SkiaSharp.GraphicObjects
Imports SkiaSharp
Imports DWSIM.SharedClasses
Imports DWSIM.Thermodynamics.Streams
Imports DWSIM.Thermodynamics
Imports DWSIM.UnitOperations.Streams
Imports System.Collections.Generic
Imports DWSIM.UI.Shared.Avalonia

Namespace UnitOperations

    Public Enum ChromatographyMode
        ''' <summary>Bind-and-elute: target compounds bind to the resin and come off in the Product.</summary>
        BindElute = 0
        ''' <summary>Flow-through: contaminants bind; product flows through.</summary>
        FlowThrough = 1
        ''' <summary>Bind-elute with a dynamic Thomas-model breakthrough curve for the loading step.</summary>
        BindElute_Dynamic = 2
    End Enum

    ''' <summary>Resin chemistry of a chromatography column. <see cref="UnitOp_Chromatography.ApplyChemistryDefaults"/> fills typical platform values for each one.</summary>
    Public Enum ChromatographyChemistry
        ''' <summary>Ion exchange (cation or anion). Typical DBC 60 g/L, target yield 0.90, host cell protein LRV 1.</summary>
        IonExchange = 0
        ''' <summary>Affinity (Protein A for antibodies). Typical DBC 35 g/L, target yield 0.95, host cell protein LRV 2.</summary>
        Affinity = 1
        ''' <summary>Hydrophobic interaction. Typical DBC 25 g/L, target yield 0.85, host cell protein LRV 1.</summary>
        HIC = 2
        ''' <summary>Size exclusion (gel filtration). Nothing binds; each compound splits by its partition coefficient Kav, which falls with log MW. Load 2 to 5 % of the column volume per run.</summary>
        SizeExclusion = 3
        ''' <summary>Mixed mode (multimodal). Typical DBC 50 g/L, target yield 0.85, host cell protein LRV 1.5.</summary>
        MixedMode = 4
    End Enum

    ''' <summary>Typical platform values for one resin chemistry, as <see cref="UnitOp_Chromatography.GetChemistryDefaults"/> returns them.</summary>
    ''' <remarks>
    ''' Sources: Shukla et al. (2007), J. Chromatogr. B 848, 28-39 (platform capacities, step yields and host cell protein clearance);
    ''' Hahn et al. (2003), J. Chromatogr. B 790, 35-51 (Protein A sorbents: capacity and pore diffusivity);
    ''' Carta and Jungbauer (2010), Protein Chromatography: Process Development and Scale-Up, Wiley-VCH (ion exchange, HIC and SEC, Kav calibration, rate models).
    ''' The Thomas rate constants are estimates from the pore-diffusion (shrinking core) limit, k_Th = 15 De / (rp^2 q0),
    ''' with the typical effective pore diffusivity De and bead radius rp of each resin class and q0 = the dynamic binding capacity.
    ''' </remarks>
    Public Class ChromatographyChemistryDefaults
        ''' <summary>Gets the dynamic binding capacity, in g per L of column volume. 0 for size exclusion (nothing binds).</summary>
        Public ReadOnly Property DynamicBindingCapacity_gL As Double
        ''' <summary>Gets the Thomas rate constant, in L/(g.s). 0 for size exclusion (no breakthrough model).</summary>
        Public ReadOnly Property ThomasRateConstant_Lgs As Double
        ''' <summary>Gets the step yield of the target compound (fraction of its feed sent to the Product outlet). Not used for size exclusion.</summary>
        Public ReadOnly Property TargetRecovery As Double
        ''' <summary>Gets the log reduction value of host cell proteins and other macromolecular impurities. Their recovery to the Product outlet is 10^-LRV. Not used for size exclusion.</summary>
        Public ReadOnly Property ImpurityLRV As Double
        ''' <summary>Gets a short note with the resin these values represent.</summary>
        Public ReadOnly Property Note As String

        ''' <summary>Initializes a new set of chemistry defaults.</summary>
        Public Sub New(dbc_gL As Double, kTh_Lgs As Double, targetRecovery As Double, impurityLRV As Double, note As String)
            DynamicBindingCapacity_gL = dbc_gL
            ThomasRateConstant_Lgs = kTh_Lgs
            Me.TargetRecovery = targetRecovery
            Me.ImpurityLRV = impurityLRV
            Me.Note = note
        End Sub
    End Class

    ''' <summary>
    ''' Chromatography column (simplified Langmuir-binding + user-specified resolution model).
    ''' For each compound, the user specifies a "RecoveryToProduct" fraction (0 to 1) - for BindElute
    ''' mode, this is the elution yield; for FlowThrough, it's the pass-through fraction.
    ''' <see cref="ApplyChemistryDefaults"/> fills the binding capacity, the Thomas rate constant and the recoveries
    ''' with typical values for the selected chemistry; the editors and the FluentAPI call it when the chemistry changes.
    ''' The column's dynamic binding capacity is checked against the target load of one cycle (feed rate times <see cref="CycleLoadTime_s"/>).
    ''' </summary>
    <System.Serializable()> Public Partial Class UnitOp_Chromatography

        Inherits UnitOperations.UnitOpBaseClass

        Implements IExternalUnitOperation
        ''' <summary>Gets a value that marks this unit operation as a bioprocess unit. The object palettes read this flag by reflection to list it in the Biochemical group.</summary>
        Public ReadOnly Property IsBio As Boolean = True

        ''' <summary>Gets or sets the simulation object class category (always <c>Separators</c>).</summary>
        Public Overrides Property ObjectClass As SimulationObjectClass
            Get
                Return SimulationObjectClass.Separators
            End Get
            Set(value As SimulationObjectClass)
                MyBase.ObjectClass = value
            End Set
        End Property

        ''' <summary>Gets or sets the display name for this unit operation.</summary>
        Public Overrides Property ComponentName As String = GetDisplayName()

        ''' <summary>Gets or sets the display description for this unit operation.</summary>
        Public Overrides Property ComponentDescription As String = GetDisplayDescription()

        ''' <summary>Gets or sets the operating mode: <c>BindElute</c> (0, default), <c>FlowThrough</c> (1) or <c>BindElute_Dynamic</c> (2).
        ''' The compound split always uses the per-compound recoveries; <c>BindElute_Dynamic</c> also builds a Thomas-model breakthrough curve for the loading step.</summary>
        Public Property Mode As ChromatographyMode = ChromatographyMode.BindElute
        ''' <summary>Gets or sets the resin chemistry: <c>IonExchange</c> (default), <c>Affinity</c>, <c>HIC</c> (hydrophobic interaction), <c>SizeExclusion</c> or <c>MixedMode</c>.
        ''' The calculation does not read it; the editors and the FluentAPI <c>WithChemistry</c> call <see cref="ApplyChemistryDefaults"/> when it changes,
        ''' which fills the binding capacity, the Thomas rate constant and the recoveries with typical values for the chemistry. Setting the property directly keeps the current values.</summary>
        Public Property Chemistry As ChromatographyChemistry = ChromatographyChemistry.IonExchange
        ''' <summary>Gets or sets the packed column (resin bed) volume, in L. Default 10 L.</summary>
        Public Property ColumnVolume_L As Double = 10.0
        ''' <summary>Gets or sets the dynamic binding capacity of the resin, in g per L of column volume. Default 40 g/L.
        ''' Used for the load ratio check and as q_max in the Thomas breakthrough model.</summary>
        Public Property DynamicBindingCapacity_gL As Double = 40.0
        ''' <summary>Gets or sets the fraction (0 to 1) of each compound sent to the Product outlet when the compound has no entry in <see cref="RecoveryToProduct"/>. Default 0.05.</summary>
        Public Property DefaultRecoveryToProduct As Double = 0.05
        ''' <summary>Gets or sets the per-compound fraction (0 to 1) of the feed mass sent to the Product outlet, keyed by compound name.
        ''' The remainder goes to the Waste outlet. In bind-elute mode this is the elution yield; in flow-through mode it is the pass-through fraction.</summary>
        Public Property RecoveryToProduct As Dictionary(Of String, Double)

        ''' <summary>Gets or sets the names of the compounds whose <see cref="RecoveryToProduct"/> entry was filled by <see cref="ApplyChemistryDefaults"/>.
        ''' The editors label these rows as chemistry defaults; <see cref="SetRecoveryToProduct"/> takes a compound off the list when the user sets its recovery.
        ''' The calculation does not read it. Empty by default, so files saved before it existed show every entry as user-set.</summary>
        Public Property RecoveryFromChemistryDefaults As List(Of String) = New List(Of String)()

        ''' <summary>Gets or sets the name of the target compound (the product the column captures or polishes).
        ''' Empty (default): every compound above 5000 g/mol counts as a target for the recovery, the load ratio and the Thomas breakthrough.
        ''' <see cref="ApplyChemistryDefaults"/> gives the target the chemistry yield and treats the other macromolecules as impurities.</summary>
        Public Property TargetCompound As String = ""

        ''' <summary>Gets or sets the duration of the load step of one cycle, in s. The target load per cycle is the target feed mass flow times this time,
        ''' and <see cref="Result_LoadRatio"/> divides it by the binding capacity. Default 1 s, which reproduces the load ratio of earlier versions
        ''' (target feed rate in kg/s over capacity in kg); set the real load time of the cycle. <see cref="Result_TimeToCapacity_s"/> gives the time that fills the capacity.</summary>
        Public Property CycleLoadTime_s As Double = 1.0

        ''' <summary>Gets or sets the exclusion limit of the size exclusion resin, in g/mol: compounds at or above it elute in the void volume (Kav = 0).
        ''' Default 600 000 g/mol (Superdex 200 class, globular proteins). Read only by <see cref="ApplyChemistryDefaults"/> for <c>SizeExclusion</c>.</summary>
        Public Property SEC_ExclusionLimit_gmol As Double = 600000.0

        ''' <summary>Gets or sets the total permeation limit of the size exclusion resin, in g/mol: compounds at or below it reach the whole pore volume (Kav = 1).
        ''' Default 10 000 g/mol (Superdex 200 class). Read only by <see cref="ApplyChemistryDefaults"/> for <c>SizeExclusion</c>.</summary>
        Public Property SEC_PermeationLimit_gmol As Double = 10000.0

        ''' <summary>Gets or sets the calculated feed mass flow, in kg/s.</summary>
        Public Property Result_FeedMass_kgs As Double = 0.0
        ''' <summary>Gets or sets the calculated Product outlet mass flow, in kg/s.</summary>
        Public Property Result_ProductMass_kgs As Double = 0.0
        ''' <summary>Gets or sets the calculated Waste outlet mass flow, in kg/s.</summary>
        Public Property Result_WasteMass_kgs As Double = 0.0
        ''' <summary>Gets or sets the calculated recovery (0 to 1) of the target compounds in the Product outlet. The target is <see cref="TargetCompound"/>, or every compound above 5000 g/mol when it is empty.</summary>
        Public Property Result_TargetRecovery As Double = 0.0
        ''' <summary>Gets or sets the calculated load ratio (dimensionless): target load per cycle (target feed mass flow times <see cref="CycleLoadTime_s"/>, in kg)
        ''' divided by the column binding capacity (dynamic binding capacity times column volume, in kg). Values above 1 mean the column is saturated.</summary>
        Public Property Result_LoadRatio As Double = 0.0
        ''' <summary>Gets or sets a value indicating whether the last calculation exceeded the binding capacity (load ratio above 1).</summary>
        Public Property Result_Saturated As Boolean = False
        ''' <summary>Gets or sets the calculated target load per cycle, in kg: target feed mass flow times <see cref="CycleLoadTime_s"/>.</summary>
        Public Property Result_LoadPerCycle_kg As Double = 0.0
        ''' <summary>Gets or sets the calculated binding capacity of the column, in kg: dynamic binding capacity (g/L) times column volume (L) / 1000.</summary>
        Public Property Result_BindingCapacity_kg As Double = 0.0
        ''' <summary>Gets or sets the calculated load time that fills the binding capacity, in s: binding capacity over target feed mass flow. 0 when the feed has no target.</summary>
        Public Property Result_TimeToCapacity_s As Double = 0.0
        ''' <summary>Gets or sets the calculated feed volume loaded per cycle as a fraction of the column volume: feed volumetric flow times <see cref="CycleLoadTime_s"/> over the column volume.
        ''' Size exclusion runs take 0.02 to 0.05 (2 to 5 % of CV).</summary>
        Public Property Result_LoadVolumeFraction As Double = 0.0

        ''' <summary>Thomas rate constant k_Th, in L/(g.s). Typical: 1e-4 to 1e-2. Used by <c>BindElute_Dynamic</c> only.</summary>
        Public Property ThomasRateConstant_Lgs As Double = 0.001

        ''' <summary>Loading time (s) of the dynamic breakthrough simulation (<c>BindElute_Dynamic</c>). 0 = auto (to about 99 % saturation).</summary>
        Public Property LoadingTime_s As Double = 0.0

        ''' <summary>Resin bulk density, in g/L of column. Not used by the calculation: the dynamic binding capacity is per litre of column,
        ''' so the Thomas capacity term is q_max (g/L) times the column volume (L) and the resin density cancels. Kept so older files load.</summary>
        Public Property ResinDensity_gL As Double = 1000.0

        ''' <summary>Last breakthrough trajectory (populated by Calculate when in BindElute_Dynamic mode). Not persisted.</summary>
        <Xml.Serialization.XmlIgnore> <Newtonsoft.Json.JsonIgnore>
        Public Property LastTrajectory As ChromatographyTrajectoryResult

        ' set by Calculate when the feed has no target and the breakthrough curve falls back to C0 = 0.001 g/L; read by the report
        <NonSerialized> <Xml.Serialization.XmlIgnore> <Newtonsoft.Json.JsonIgnore> Private _thomasNominalC0 As Boolean = False

        ''' <summary>The classic (WinForms) editor window open for this unit operation, if any. Not saved with the flowsheet.</summary>
        <NonSerialized> <Xml.Serialization.XmlIgnore> Public f As Object

        ''' <summary>Gets a value indicating whether this unit operation supports dynamic simulation mode. Always <c>False</c>.</summary>
        Public Overrides ReadOnly Property SupportsDynamicMode As Boolean = False
        ''' <summary>Gets a value indicating whether this unit operation is compatible with mobile interfaces. Always <c>False</c>.</summary>
        Public Overrides ReadOnly Property MobileCompatible As Boolean
            Get
                Return False
            End Get
        End Property

        ''' <summary>Initializes a new default instance of the <see cref="UnitOp_Chromatography"/> class.</summary>
        Public Sub New()
            MyBase.New()
            RecoveryToProduct = New Dictionary(Of String, Double)()
        End Sub

        ''' <summary>Initializes a new instance of the <see cref="UnitOp_Chromatography"/> class with a name and description.</summary>
        ''' <param name="name">The name of this unit operation.</param>
        ''' <param name="description">A brief description of this unit operation.</param>
        Public Sub New(ByVal name As String, ByVal description As String)
            MyBase.New()
            Me.ComponentName = name
            Me.ComponentDescription = description
            RecoveryToProduct = New Dictionary(Of String, Double)()
        End Sub

        ''' <summary>Creates a deep copy of this object by round-tripping through XML serialization.</summary>
        ''' <returns>A new <see cref="UnitOp_Chromatography"/> instance with the same property values.</returns>
        Public Overrides Function CloneXML() As Object
            Dim obj As ICustomXMLSerialization = New UnitOp_Chromatography()
            obj.LoadData(Me.SaveData)
            Return obj
        End Function

        ''' <summary>Returns the fraction of a compound sent to the Product outlet: its entry in <see cref="RecoveryToProduct"/>, or <see cref="DefaultRecoveryToProduct"/> when it has none, clamped to 0 to 1.
        ''' <see cref="Calculate"/> splits the feed with it and the editors show it in the recovery grids, so both always agree.</summary>
        ''' <param name="compName">The compound name.</param>
        ''' <returns>The recovery-to-product fraction, between 0 and 1.</returns>
        Public Function RecoveryFor(compName As String) As Double
            If RecoveryToProduct IsNot Nothing AndAlso RecoveryToProduct.ContainsKey(compName) Then
                Return Max(0.0, Min(1.0, RecoveryToProduct(compName)))
            End If
            Return Max(0.0, Min(1.0, DefaultRecoveryToProduct))
        End Function

        ''' <summary>Returns where the recovery of a compound comes from, as the editors label it: "user-set" (an entry the user typed),
        ''' "chemistry default" (an entry filled by <see cref="ApplyChemistryDefaults"/>) or "default recovery" (no entry; <see cref="DefaultRecoveryToProduct"/> applies).</summary>
        ''' <param name="compName">The compound name.</param>
        ''' <returns>A short label for the source of the value <see cref="RecoveryFor"/> returns.</returns>
        Public Function RecoverySource(compName As String) As String
            If RecoveryToProduct Is Nothing OrElse Not RecoveryToProduct.ContainsKey(compName) Then Return "default recovery"
            If RecoveryFromChemistryDefaults IsNot Nothing AndAlso RecoveryFromChemistryDefaults.Contains(compName) Then Return "chemistry default"
            Return "user-set"
        End Function

        ''' <summary>Sets the recovery to product of one compound as a user value: writes its <see cref="RecoveryToProduct"/> entry
        ''' and takes it off <see cref="RecoveryFromChemistryDefaults"/>. The editors and the FluentAPI <c>WithRecoveryToProduct</c> call it.</summary>
        ''' <param name="compName">The compound name.</param>
        ''' <param name="value">The fraction (0 to 1) of the compound feed mass sent to the Product outlet.</param>
        Public Sub SetRecoveryToProduct(compName As String, value As Double)
            If RecoveryToProduct Is Nothing Then RecoveryToProduct = New Dictionary(Of String, Double)()
            RecoveryToProduct(compName) = value
            If RecoveryFromChemistryDefaults IsNot Nothing Then RecoveryFromChemistryDefaults.Remove(compName)
        End Sub

        Private Function HasTargetCompound() As Boolean
            Return Not String.IsNullOrWhiteSpace(TargetCompound)
        End Function

        ''' <summary>Returns the typical platform values of a resin chemistry: dynamic binding capacity, Thomas rate constant, target step yield and impurity log reduction.</summary>
        ''' <param name="chemistry">The resin chemistry.</param>
        ''' <returns>The values <see cref="ApplyChemistryDefaults"/> applies for this chemistry.</returns>
        ''' <remarks>
        ''' Capacities, yields and host cell protein clearance: Shukla et al. (2007), J. Chromatogr. B 848, 28-39; Hahn et al. (2003), J. Chromatogr. B 790, 35-51 (Protein A);
        ''' Carta and Jungbauer (2010), Protein Chromatography, Wiley-VCH. The Thomas rate constants are pore-diffusion estimates, k_Th = 15 De / (rp^2 q0):
        ''' Protein A De 5e-12 m2/s, 85 um beads; ion exchange De 3e-12 m2/s, 65 um; HIC De 5e-12 m2/s, 90 um; mixed mode De 3e-12 m2/s, 75 um.
        ''' </remarks>
        Public Shared Function GetChemistryDefaults(chemistry As ChromatographyChemistry) As ChromatographyChemistryDefaults
            Select Case chemistry
                Case ChromatographyChemistry.Affinity
                    Return New ChromatographyChemistryDefaults(35.0, 0.0012, 0.95, 2.0, "Protein A affinity capture")
                Case ChromatographyChemistry.IonExchange
                    Return New ChromatographyChemistryDefaults(60.0, 0.0007, 0.9, 1.0, "ion exchange")
                Case ChromatographyChemistry.HIC
                    Return New ChromatographyChemistryDefaults(25.0, 0.0015, 0.85, 1.0, "hydrophobic interaction")
                Case ChromatographyChemistry.MixedMode
                    Return New ChromatographyChemistryDefaults(50.0, 0.0006, 0.85, 1.5, "mixed mode")
                Case Else
                    Return New ChromatographyChemistryDefaults(0.0, 0.0, 0.0, 0.0, "size exclusion: no binding, split by Kav, load 2 to 5 % of CV")
            End Select
        End Function

        ''' <summary>Returns the size exclusion partition coefficient Kav for a molecular weight: linear in log10(MW) from 1 at
        ''' <see cref="SEC_PermeationLimit_gmol"/> to 0 at <see cref="SEC_ExclusionLimit_gmol"/>, clamped to 0 to 1 (Carta and Jungbauer 2010).</summary>
        ''' <param name="molarWeight">The molecular weight, in g/mol.</param>
        ''' <returns>Kav, between 0 (excluded, elutes in the void volume) and 1 (reaches the whole pore volume).</returns>
        Public Function SizeExclusionKav(molarWeight As Double) As Double
            Dim mwExcl = SEC_ExclusionLimit_gmol, mwPerm = SEC_PermeationLimit_gmol
            If molarWeight <= 0.0 OrElse mwPerm <= 0.0 OrElse mwExcl <= mwPerm Then
                Return If(molarWeight >= mwExcl AndAlso mwExcl > 0.0, 0.0, 1.0)
            End If
            Dim kav = (Log10(mwExcl) - Log10(molarWeight)) / (Log10(mwExcl) - Log10(mwPerm))
            Return Max(0.0, Min(1.0, kav))
        End Function

        Private Shared Function IsWater(compound As ICompoundConstantProperties) As Boolean
            Return String.Equals(compound.Name, "Water", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(compound.CAS_Number, "7732-18-5", StringComparison.OrdinalIgnoreCase)
        End Function

        Private Shared Function IsHostCellProtein(name As String) As Boolean
            If String.IsNullOrEmpty(name) Then Return False
            Dim lower = name.ToLowerInvariant()
            If lower.Replace(" ", "").Replace("_", "").Replace("-", "").Contains("hostcell") Then Return True
            Return lower.Split({" "c, "_"c, "-"c, "("c, ")"c, "."c}, StringSplitOptions.RemoveEmptyEntries).Any(Function(t) t = "hcp" OrElse t = "hcps")
        End Function

        ''' <summary>Returns the recovery to product that <see cref="ApplyChemistryDefaults"/> gives a compound under the current <see cref="Chemistry"/>,
        ''' or <c>Nothing</c> when the defaults leave the compound alone (water, and small solutes outside size exclusion).</summary>
        ''' <param name="compound">The compound.</param>
        ''' <returns>The default recovery to product, between 0 and 1, or <c>Nothing</c>.</returns>
        Public Function ChemistryDefaultRecovery(compound As ICompoundConstantProperties) As Double?
            If compound Is Nothing OrElse IsWater(compound) Then Return Nothing
            If Chemistry = ChromatographyChemistry.SizeExclusion Then Return 1.0 - SizeExclusionKav(compound.Molar_Weight)
            Dim d = GetChemistryDefaults(Chemistry)
            Dim impurity = Pow(10.0, -d.ImpurityLRV)
            If HasTargetCompound() Then
                If compound.Name = TargetCompound Then Return d.TargetRecovery
                If IsHostCellProtein(compound.Name) OrElse compound.Molar_Weight > 5000.0 Then Return impurity
                Return Nothing
            End If
            If IsHostCellProtein(compound.Name) Then Return impurity
            If compound.Molar_Weight > 5000.0 Then Return d.TargetRecovery
            Return Nothing
        End Function

        ''' <summary>
        ''' Fills <see cref="DynamicBindingCapacity_gL"/>, <see cref="ThomasRateConstant_Lgs"/> and the <see cref="RecoveryToProduct"/> entries with typical
        ''' platform values for <see cref="Chemistry"/> (see <see cref="GetChemistryDefaults"/>). The editors and the FluentAPI <c>WithChemistry</c> call it when
        ''' the chemistry changes; <see cref="Calculate"/> never does, so a saved flowsheet keeps the values it was saved with.
        ''' Recoveries: the target (<see cref="TargetCompound"/>, or every compound above 5000 g/mol when it is empty) gets the chemistry step yield;
        ''' host cell proteins (a compound named HCP or host cell protein) and, with a named target, the other compounds above 5000 g/mol get 10^-LRV.
        ''' Size exclusion sets the binding capacity to 0 and gives every compound 1 - Kav (<see cref="SizeExclusionKav"/>).
        ''' Water, and the small solutes outside size exclusion, keep their entries or <see cref="DefaultRecoveryToProduct"/>.
        ''' Entries a previous call filled (those on <see cref="RecoveryFromChemistryDefaults"/>) are removed first, so a compound the new
        ''' chemistry does not cover returns to <see cref="DefaultRecoveryToProduct"/>; entries the user typed stay.
        ''' Each compound it fills goes on <see cref="RecoveryFromChemistryDefaults"/>.
        ''' </summary>
        Public Sub ApplyChemistryDefaults()
            Dim d = GetChemistryDefaults(Chemistry)
            DynamicBindingCapacity_gL = d.DynamicBindingCapacity_gL
            If d.ThomasRateConstant_Lgs > 0.0 Then ThomasRateConstant_Lgs = d.ThomasRateConstant_Lgs
            If RecoveryToProduct Is Nothing Then RecoveryToProduct = New Dictionary(Of String, Double)()
            If RecoveryFromChemistryDefaults Is Nothing Then RecoveryFromChemistryDefaults = New List(Of String)()
            If FlowSheet Is Nothing Then Return
            'entries filled by the previous chemistry go back to the default recovery; entries the user typed stay
            For Each compName In RecoveryFromChemistryDefaults
                RecoveryToProduct.Remove(compName)
            Next
            RecoveryFromChemistryDefaults.Clear()
            For Each c In FlowSheet.SelectedCompounds.Values
                Dim r = ChemistryDefaultRecovery(c)
                If r.HasValue Then
                    RecoveryToProduct(c.Name) = r.Value
                    If Not RecoveryFromChemistryDefaults.Contains(c.Name) Then RecoveryFromChemistryDefaults.Add(c.Name)
                End If
            Next
        End Sub

        Public Overrides Sub Calculate(Optional ByVal args As Object = Nothing)

            If Not Me.GraphicObject.InputConnectors(0).IsAttached Then _
                Throw New Exception("Chromatography: Feed not connected.")
            If Me.GraphicObject.OutputConnectors.Count < 2 OrElse
               Not Me.GraphicObject.OutputConnectors(0).IsAttached OrElse
               Not Me.GraphicObject.OutputConnectors(1).IsAttached Then
                Throw New Exception("Chromatography: Both Product and Waste outlets must be connected.")
            End If

            Dim feed As MaterialStream =
                DirectCast(FlowSheet.SimulationObjects(Me.GraphicObject.InputConnectors(0).AttachedConnector.AttachedFrom.Name), MaterialStream)

            Dim T = feed.Phases(0).Properties.temperature.GetValueOrDefault
            Dim P = feed.Phases(0).Properties.pressure.GetValueOrDefault
            Dim m_total = feed.Phases(0).Properties.massflow.GetValueOrDefault

            Dim feedComp As New Dictionary(Of String, Double)
            For Each c In feed.Phases(0).Compounds.Values
                feedComp(c.Name) = c.MassFraction.GetValueOrDefault * m_total
            Next

            Dim prod As New Dictionary(Of String, Double)
            Dim waste As New Dictionary(Of String, Double)
            Dim m_p As Double = 0.0, m_w As Double = 0.0
            Dim target_in As Double = 0.0, target_out As Double = 0.0

            ' the named target when it is in the feed; otherwise every macromolecule (MW > 5000)
            Dim namedTarget = HasTargetCompound() AndAlso feedComp.ContainsKey(TargetCompound)

            For Each kv In feedComp
                Dim r = RecoveryFor(kv.Key)
                prod(kv.Key) = kv.Value * r
                waste(kv.Key) = kv.Value * (1.0 - r)
                m_p += prod(kv.Key) : m_w += waste(kv.Key)
                Dim c = feed.Phases(0).Compounds(kv.Key)
                Dim isTarget As Boolean
                If namedTarget Then
                    isTarget = (kv.Key = TargetCompound)
                Else
                    isTarget = c.ConstantProperties IsNot Nothing AndAlso c.ConstantProperties.Molar_Weight > 5000.0
                End If
                If isTarget Then
                    target_in += kv.Value
                    target_out += prod(kv.Key)
                End If
            Next

            Dim Q_vol = feed.Phases(1).Properties.volumetric_flow.GetValueOrDefault
            If Q_vol <= 0.0 Then Q_vol = feed.Phases(0).Properties.volumetric_flow.GetValueOrDefault

            ' load check: target load of one cycle (kg/s x s = kg) against the binding capacity (g/L x L / 1000 = kg)
            Dim capacity_kg As Double = DynamicBindingCapacity_gL / 1000.0 * ColumnVolume_L
            Dim loadTime_s As Double = Max(CycleLoadTime_s, 0.0)
            Result_BindingCapacity_kg = capacity_kg
            Result_LoadPerCycle_kg = target_in * loadTime_s
            If capacity_kg > 0 Then
                Result_LoadRatio = Result_LoadPerCycle_kg / capacity_kg
            Else
                Result_LoadRatio = 0.0
            End If
            Result_Saturated = (Result_LoadRatio > 1.0)
            If target_in > 0.0 AndAlso capacity_kg > 0.0 Then
                Result_TimeToCapacity_s = capacity_kg / target_in
            Else
                Result_TimeToCapacity_s = 0.0
            End If
            If ColumnVolume_L > 0.0 Then
                Result_LoadVolumeFraction = Max(Q_vol, 0.0) * loadTime_s / (ColumnVolume_L / 1000.0)
            Else
                Result_LoadVolumeFraction = 0.0
            End If

            ' Dynamic Thomas breakthrough for the loading step, if in dynamic mode
            _thomasNominalC0 = False
            If Mode = ChromatographyMode.BindElute_Dynamic Then
                If Q_vol <= 0.0 Then Q_vol = 0.000000000001
                Dim Q_Ls = Q_vol * 1000.0          ' L/s
                Dim C0_gL = If(target_in > 0.0 AndAlso Q_vol > 0.0, target_in / Q_vol, 0.001) ' kg/s / (m3/s) = g/L
                If target_in <= 0.0 Then
                    _thomasNominalC0 = True
                    FlowSheet.ShowMessage(GraphicObject.Tag & ": no target compound in the feed, so the Thomas breakthrough curve uses a nominal feed concentration of 0.001 g/L.", IFlowsheet.MessageType.Warning)
                End If
                BuildThomasBreakthrough(C0_gL, Q_Ls)
            End If

            Result_FeedMass_kgs = m_total
            Result_ProductMass_kgs = m_p
            Result_WasteMass_kgs = m_w
            If target_in > 0 Then Result_TargetRecovery = target_out / target_in Else Result_TargetRecovery = 0.0

            WriteStream(FlowSheet.SimulationObjects(Me.GraphicObject.OutputConnectors(0).AttachedConnector.AttachedTo.Name),
                        prod, m_p, T, P)
            WriteStream(FlowSheet.SimulationObjects(Me.GraphicObject.OutputConnectors(1).AttachedConnector.AttachedTo.Name),
                        waste, m_w, T, P)

        End Sub

        ''' <summary>
        ''' Build a Thomas-model breakthrough curve C/C0 vs time for the loading step.
        '''   C/C0 = 1 / (1 + exp((k_Th / Q) (q_max V_col - C0 Q t)))
        ''' C0 in g/L, Q in L/s, k_Th in L/(g.s), q_max = DynamicBindingCapacity_gL (g per L of column),
        ''' V_col = ColumnVolume_L (L), so q_max V_col is the binding capacity in g and the exponent is dimensionless.
        ''' The classic form q0 M (g/g resin times g resin) gives the same capacity; the resin density cancels.
        ''' </summary>
        Private Sub BuildThomasBreakthrough(C0_gL As Double, Q_Ls As Double)

            Dim traj As New ChromatographyTrajectoryResult() With {.Mode = "BindElute_Dynamic"}
            LastTrajectory = traj
            If C0_gL <= 0.0 OrElse Q_Ls <= 0.0 OrElse ColumnVolume_L <= 0.0 Then Return

            Dim kTh = Max(ThomasRateConstant_Lgs, 0.000000000001)
            Dim qmax = Max(DynamicBindingCapacity_gL, 0.000000000001) ' g / L column
            Dim capacity_g = qmax * ColumnVolume_L                     ' g

            ' Time horizon: auto-compute if LoadingTime_s <= 0
            Dim t_end As Double = LoadingTime_s
            If t_end <= 0.0 Then
                ' Time to reach ~99% saturation: C/C0 = 0.99 => exp(...) = 1/99
                '   kTh/Q * (capacity - C0*Q*t) = -ln(99)
                '   t = (capacity + ln(99)*Q/kTh) / (C0*Q)
                t_end = (capacity_g + Math.Log(99.0) * Q_Ls / kTh) / (Math.Max(C0_gL * Q_Ls, 0.000000000001))
                t_end = Max(t_end, 1.0)
            End If

            Dim N As Integer = 500
            Dim dt As Double = t_end / N
            Dim qLoaded As Double = 0.0
            For i = 0 To N
                Dim t = i * dt
                Dim expArg = (kTh / Q_Ls) * (capacity_g - C0_gL * Q_Ls * t)
                ' clamp for numerical safety
                If expArg > 700 Then expArg = 700
                If expArg < -700 Then expArg = -700
                Dim CoverC0 = 1.0 / (1.0 + Math.Exp(expArg))
                Dim bv = Q_Ls * t / ColumnVolume_L
                ' Cumulative mass adsorbed (trap integration of (1-C/C0) * C0 * Q)
                If i > 0 Then
                    Dim tPrev = (i - 1) * dt
                    Dim eaPrev = (kTh / Q_Ls) * (capacity_g - C0_gL * Q_Ls * tPrev)
                    If eaPrev > 700 Then eaPrev = 700
                    If eaPrev < -700 Then eaPrev = -700
                    Dim CoverPrev = 1.0 / (1.0 + Math.Exp(eaPrev))
                    Dim absRate = C0_gL * Q_Ls * 0.5 * ((1.0 - CoverPrev) + (1.0 - CoverC0))  ' g/s
                    qLoaded += absRate * dt / Max(ColumnVolume_L, 0.000000000001)             ' g/L column
                End If
                traj.Times.Add(t)
                traj.BedVolumes.Add(bv)
                traj.C_over_C0.Add(CoverC0)
                traj.QLoaded.Add(qLoaded)
                traj.Breakthrough.Add(1.0 - CoverC0)
            Next

        End Sub

        Private Shared Sub WriteStream(ms As MaterialStream, m As Dictionary(Of String, Double), total As Double, T As Double, P As Double)
            With ms
                .ClearAllProps()
                .Phases(0).Properties.temperature = T
                .Phases(0).Properties.pressure = P
                If total > 0 Then
                    For Each c In .Phases(0).Compounds.Values
                        c.MassFraction = If(m.ContainsKey(c.Name), m(c.Name), 0.0) / total
                    Next
                    Dim invMW As Double = 0.0
                    For Each c In .Phases(0).Compounds.Values
                        invMW += c.MassFraction.GetValueOrDefault / c.ConstantProperties.Molar_Weight
                    Next
                    If invMW > 0 Then
                        For Each c In .Phases(0).Compounds.Values
                            c.MoleFraction = (c.MassFraction.GetValueOrDefault / c.ConstantProperties.Molar_Weight) / invMW
                        Next
                    End If
                End If
                .Phases(0).Properties.massflow = total
                .DefinedFlow = FlowSpec.Mass
                .SpecType = StreamSpec.Temperature_and_Pressure
                'a single-compound outlet would otherwise be re-flashed at PH with the H cleared above
                .OverrideSingleCompoundFlashBehavior = True
            End With
        End Sub

        Public Overrides Sub DeCalculate()
            For i = 0 To Math.Min(1, Me.GraphicObject.OutputConnectors.Count - 1)
                Dim cp = Me.GraphicObject.OutputConnectors(i)
                If cp.IsAttached Then
                    Dim ms As MaterialStream = FlowSheet.SimulationObjects(cp.AttachedConnector.AttachedTo.Name)
                    With ms
                        .Phases(0).Properties.temperature = Nothing
                        .Phases(0).Properties.pressure = Nothing
                        For Each c In .Phases(0).Compounds.Values
                            c.MoleFraction = 0 : c.MassFraction = 0
                        Next
                        .Phases(0).Properties.massflow = Nothing
                        .GraphicObject.Calculated = False
                    End With
                End If
            Next
        End Sub

        ''' <summary>Returns the raw bytes of the icon image for this unit operation.</summary>
        ''' <returns>A byte array containing the PNG image data for the icon.</returns>
        Public Overrides Function GetIconBitmapBytes() As Byte()
            Return BioOpsDrawHelper.RenderIconToPngBytes(64, 64, AddressOf DrawIcon)
        End Function
        ''' <summary>Returns the description string for this unit operation type.</summary>
        ''' <returns>A description string identifying this unit operation type.</returns>
        Public Overrides Function GetDisplayDescription() As String
            Return "Chromatography column (bind-elute or flow-through)"
        End Function
        ''' <summary>Returns the display name for this unit operation type.</summary>
        ''' <returns>The name string for this unit operation type.</returns>
        Public Overrides Function GetDisplayName() As String
            Return "Chromatography Column"
        End Function

        ''' <summary>Generates a plain-text results report for this unit operation.</summary>
        ''' <param name="su">The unit system used for formatting output values (not used; values are reported in fixed units).</param>
        ''' <param name="ci">The culture info used for number formatting.</param>
        ''' <param name="numberformat">A .NET numeric format string (e.g. "G6") applied to output values.</param>
        ''' <returns>A formatted multi-line string report.</returns>
        Public Overrides Function GetReport(su As IUnitsOfMeasure, ci As Globalization.CultureInfo, numberformat As String) As String
            Dim s As New Text.StringBuilder
            s.AppendLine("Chromatography: " & Me.GraphicObject.Tag)
            s.AppendLine("Mode:        " & Mode.ToString())
            s.AppendLine("Chemistry:   " & Chemistry.ToString())
            s.AppendLine("CV:          " & ColumnVolume_L.ToString(numberformat, ci) & " L")
            s.AppendLine("DBC:         " & DynamicBindingCapacity_gL.ToString(numberformat, ci) & " g/L")
            s.AppendLine("Target:      " & If(HasTargetCompound(), TargetCompound, "compounds above 5 kDa"))
            s.AppendLine("Load time per cycle: " & CycleLoadTime_s.ToString(numberformat, ci) & " s")
            s.AppendLine()
            s.AppendLine("Feed:           " & Result_FeedMass_kgs.ToString(numberformat, ci) & " kg/s")
            s.AppendLine("Product:        " & Result_ProductMass_kgs.ToString(numberformat, ci) & " kg/s")
            s.AppendLine("Waste:          " & Result_WasteMass_kgs.ToString(numberformat, ci) & " kg/s")
            s.AppendLine("Target recovery:              " & (Result_TargetRecovery * 100).ToString(numberformat, ci) & " %")
            s.AppendLine("Target load per cycle:        " & Result_LoadPerCycle_kg.ToString(numberformat, ci) & " kg")
            s.AppendLine("Binding capacity (DBC x CV):  " & Result_BindingCapacity_kg.ToString(numberformat, ci) & " kg")
            s.AppendLine("Load ratio (load/capacity):   " & Result_LoadRatio.ToString(numberformat, ci))
            s.AppendLine("Load time to fill capacity:   " & Result_TimeToCapacity_s.ToString(numberformat, ci) & " s")
            s.AppendLine("Load volume per cycle:        " & (Result_LoadVolumeFraction * 100).ToString(numberformat, ci) & " % of CV")
            If Result_Saturated Then s.AppendLine("  Column is SATURATED - binding capacity exceeded.")
            If _thomasNominalC0 Then
                s.AppendLine("  No target compound in the feed: the Thomas breakthrough curve uses a nominal feed concentration of 0.001 g/L.")
            End If
            If Chemistry = ChromatographyChemistry.SizeExclusion AndAlso Result_LoadVolumeFraction > 0.05 Then
                s.AppendLine("  Size exclusion load above 5 % of CV: resolution drops (typical 2 to 5 %).")
            End If
            Return s.ToString()
        End Function

        Private Shared ReadOnly _inputProps As String() = {"Mode", "Chemistry", "Target Compound", "Column Volume", "Dynamic Binding Capacity", "Default Recovery To Product", "Cycle Load Time", "Thomas Rate Constant", "Loading Time"}
        Private Shared ReadOnly _outputProps As String() = {"Feed Mass", "Product Mass", "Waste Mass", "Target Recovery", "Load Per Cycle", "Binding Capacity", "Load Ratio", "Time To Capacity", "Load Volume Fraction", "Saturated"}

        Public Overrides Function GetProperties(proptype As PropertyType) As String()
            Dim baseprops = MyBase.GetProperties(proptype)
            Select Case proptype
                Case PropertyType.WR : Return _inputProps
                Case PropertyType.RO : Return _outputProps
                Case Else : Return _inputProps.Concat(_outputProps).Concat(baseprops).ToArray()
            End Select
        End Function

        Public Overrides Function GetPropertyValue(prop As String, Optional su As IUnitsOfMeasure = Nothing) As Object
            Select Case prop
                Case "Mode" : Return Mode.ToString()
                Case "Chemistry" : Return Chemistry.ToString()
                Case "Target Compound" : Return TargetCompound
                Case "Cycle Load Time" : Return CycleLoadTime_s
                Case "Thomas Rate Constant" : Return ThomasRateConstant_Lgs
                Case "Loading Time" : Return LoadingTime_s
                Case "Load Per Cycle" : Return Result_LoadPerCycle_kg
                Case "Binding Capacity" : Return Result_BindingCapacity_kg
                Case "Time To Capacity" : Return Result_TimeToCapacity_s
                Case "Load Volume Fraction" : Return Result_LoadVolumeFraction
                Case "Column Volume" : Return ColumnVolume_L
                Case "Dynamic Binding Capacity" : Return DynamicBindingCapacity_gL
                Case "Default Recovery To Product" : Return DefaultRecoveryToProduct
                Case "Feed Mass" : Return Result_FeedMass_kgs
                Case "Product Mass" : Return Result_ProductMass_kgs
                Case "Waste Mass" : Return Result_WasteMass_kgs
                Case "Target Recovery" : Return Result_TargetRecovery
                Case "Load Ratio" : Return Result_LoadRatio
                Case "Saturated" : Return Result_Saturated
                Case Else : Return MyBase.GetPropertyValue(prop, su)
            End Select
        End Function

        Public Overrides Function GetPropertyUnit(prop As String, Optional su As IUnitsOfMeasure = Nothing) As String
            Select Case prop
                Case "Column Volume" : Return "L"
                Case "Dynamic Binding Capacity" : Return "g/L"
                Case "Feed Mass", "Product Mass", "Waste Mass" : Return "kg/s"
                Case "Cycle Load Time", "Loading Time", "Time To Capacity" : Return "s"
                Case "Thomas Rate Constant" : Return "L/(g.s)"
                Case "Load Per Cycle", "Binding Capacity" : Return "kg"
                Case Else : Return "-"
            End Select
        End Function

        Public Overrides Function SetPropertyValue(prop As String, propval As Object, Optional su As IUnitsOfMeasure = Nothing) As Boolean
            Dim d As Double = 0.0
            If TypeOf propval Is Double Then
                d = CDbl(propval)
            ElseIf TypeOf propval Is String Then
                Double.TryParse(CStr(propval), Globalization.NumberStyles.Any, Globalization.CultureInfo.CurrentCulture, d)
            End If
            Select Case prop
                Case "Mode"
                    Dim m As ChromatographyMode
                    If [Enum].TryParse(Of ChromatographyMode)(propval?.ToString(), m) Then Me.Mode = m
                    Return True
                Case "Chemistry"
                    Dim c As ChromatographyChemistry
                    If [Enum].TryParse(Of ChromatographyChemistry)(propval?.ToString(), c) Then Me.Chemistry = c
                    Return True
                Case "Target Compound" : TargetCompound = If(propval?.ToString(), "") : Return True
                Case "Cycle Load Time" : CycleLoadTime_s = d : Return True
                Case "Thomas Rate Constant" : ThomasRateConstant_Lgs = d : Return True
                Case "Loading Time" : LoadingTime_s = d : Return True
                Case "Column Volume" : ColumnVolume_L = d : Return True
                Case "Dynamic Binding Capacity" : DynamicBindingCapacity_gL = d : Return True
                Case "Default Recovery To Product" : DefaultRecoveryToProduct = d : Return True
                Case Else : Return MyBase.SetPropertyValue(prop, propval, su)
            End Select
        End Function

        ' IExternalUnitOperation
        Private ReadOnly Property IEUO_Name As String Implements IExternalUnitOperation.Name
            Get
                Return GetDisplayName()
            End Get
        End Property
        Private ReadOnly Property IEUO_Description As String Implements IExternalUnitOperation.Description
            Get
                Return GetDisplayDescription()
            End Get
        End Property
        Public ReadOnly Property Prefix As String Implements IExternalUnitOperation.Prefix
            Get
                Return "CHR-"
            End Get
        End Property
        Public Function ReturnInstance(typename As String) As Object Implements IExternalUnitOperation.ReturnInstance
            Return New UnitOp_Chromatography()
        End Function
        Public Sub PopulateEditorPanel(ctner As Object) Implements IExternalUnitOperation.PopulateEditorPanel

            If TypeOf ctner Is AvaloniaEditorPanel Then PopulateEditorPanelAvalonia(DirectCast(ctner, AvaloniaEditorPanel)) : Return
        End Sub

        Private Sub PopulateEditorPanelAvalonia(container As AvaloniaEditorPanel)

            Dim nf = FlowSheet.FlowsheetOptions.NumberFormat

            container.CreateAndAddLabelRow("Mode & Chemistry")

            container.CreateAndAddDropDownRow("Operating Mode",
                                              New List(Of String)({"Bind-Elute", "Flow-Through", "Bind-Elute (Dynamic, Thomas)"}),
                                              CInt(Mode),
                                              Sub(dd, e)
                                                  Mode = CType(dd.SelectedIndex, ChromatographyMode)
                                                  FlowSheet.RequestCalculation()
                                              End Sub)

            Dim compIds = FlowSheet.SelectedCompounds.Values.Select(Function(c) c.Name).ToList()
            Dim targetIdx = compIds.IndexOf(If(TargetCompound, ""))
            container.CreateAndAddDropDownRow("Target Compound",
                                              New List(Of String)(New String() {"(MW > 5 kDa)"}.Concat(compIds)),
                                              If(targetIdx < 0, 0, targetIdx + 1),
                                              Sub(dd, e)
                                                  TargetCompound = If(dd.SelectedIndex > 0, compIds(dd.SelectedIndex - 1), "")
                                                  FlowSheet.RequestCalculation()
                                              End Sub)

            ' a new chemistry brings its typical capacity, rate constant and recoveries
            container.CreateAndAddDropDownRow("Chemistry",
                                              New List(Of String)({"Ion Exchange", "Affinity", "HIC", "Size Exclusion", "Mixed Mode"}),
                                              CInt(Chemistry),
                                              Sub(dd, e)
                                                  Chemistry = CType(dd.SelectedIndex, ChromatographyChemistry)
                                                  ApplyChemistryDefaults()
                                                  FlowSheet.UpdateOpenEditForms()
                                                  FlowSheet.RequestCalculation()
                                              End Sub)

            container.CreateAndAddLabelRow("Column")

            container.CreateAndAddTextBoxRow(nf, "Column Volume (L)", ColumnVolume_L,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     ColumnVolume_L = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Dynamic Binding Capacity (g/L)", DynamicBindingCapacity_gL,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     DynamicBindingCapacity_gL = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Load Time per Cycle (s)", CycleLoadTime_s,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     CycleLoadTime_s = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddLabelRow("Separation")

            container.CreateAndAddTextBoxRow(nf, "Default Recovery to Product (0-1)", DefaultRecoveryToProduct,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     DefaultRecoveryToProduct = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddLabelRow("Thomas Dynamic Model (Bind-Elute Dynamic only)")

            container.CreateAndAddTextBoxRow(nf, "Thomas Rate Constant (L/(g.s))", ThomasRateConstant_Lgs,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     ThomasRateConstant_Lgs = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Loading Time (s, 0 = auto to 99%)", LoadingTime_s,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     LoadingTime_s = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

        End Sub

        Public Sub CreateConnectors() Implements IExternalUnitOperation.CreateConnectors
            If GraphicObject Is Nothing Then Return
            Dim w = GraphicObject.Width, h = GraphicObject.Height
            Dim gx = GraphicObject.X, gy = GraphicObject.Y
            If GraphicObject.InputConnectors.Count = 1 AndAlso GraphicObject.OutputConnectors.Count = 2 Then
                GraphicObject.InputConnectors(0).Position = New Point(gx + 0.5 * w, gy)
                GraphicObject.InputConnectors(0).ConnectorName = "Feed"
                GraphicObject.InputConnectors(0).Direction = ConDir.Down
                GraphicObject.OutputConnectors(0).Position = New Point(gx + w, gy + 0.7 * h)
                GraphicObject.OutputConnectors(0).ConnectorName = "Product"
                GraphicObject.OutputConnectors(1).Position = New Point(gx + 0.5 * w, gy + h)
                GraphicObject.OutputConnectors(1).ConnectorName = "Waste"
                GraphicObject.OutputConnectors(1).Direction = ConDir.Up
            Else
                GraphicObject.InputConnectors.Clear() : GraphicObject.OutputConnectors.Clear()
                GraphicObject.InputConnectors.Add(New ConnectionPoint With {
                    .Position = New Point(gx + 0.5 * w, gy), .Type = ConType.ConIn,
                    .Direction = ConDir.Down, .ConnectorName = "Feed"})
                GraphicObject.OutputConnectors.Add(New ConnectionPoint With {
                    .Position = New Point(gx + w, gy + 0.7 * h), .Type = ConType.ConOut,
                    .Direction = ConDir.Right, .ConnectorName = "Product"})
                GraphicObject.OutputConnectors.Add(New ConnectionPoint With {
                    .Position = New Point(gx + 0.5 * w, gy + h), .Type = ConType.ConOut,
                    .Direction = ConDir.Up, .ConnectorName = "Waste"})
            End If
            GraphicObject.EnergyConnector.Active = False
        End Sub

        <NonSerialized> <Xml.Serialization.XmlIgnore> Private _photoImage As SKImage

        Public Sub Draw(g As Object) Implements IExternalUnitOperation.Draw
            If GraphicObject Is Nothing Then Return
            Dim canvas As SKCanvas = DirectCast(g, SKCanvas)
            If GraphicObject.DrawMode = 2 Then
                If BioOpsDrawHelper.TryDrawPhotorealistic(canvas,
                    GraphicObject.X, GraphicObject.Y, GraphicObject.Width, GraphicObject.Height,
                    "chromatography_photo", _photoImage) Then Return
            End If
            DrawIcon(canvas, CSng(GraphicObject.X), CSng(GraphicObject.Y),
                     CSng(GraphicObject.Width), CSng(GraphicObject.Height),
                     GraphicObject.DrawMode = 1)
        End Sub

        Private Shared Sub DrawIcon(canvas As SKCanvas, gx As Single, gy As Single, w As Single, h As Single, Optional mono As Boolean = False)
            ' Packed bed chromatography column: tall cylinder + top/bottom flanges + resin bed + inlet/outlet pipes.
            Dim skid As New SKRect(gx + 0.15F * w, gy + 0.9F * h, gx + 0.85F * w, gy + h)
            BioOpsDrawHelper.DrawSkid(canvas, skid, mono)
            Dim col As New SKRect(gx + 0.35F * w, gy + 0.12F * h, gx + 0.65F * w, gy + 0.9F * h)
            BioOpsDrawHelper.DrawVerticalTank(canvas, col, mono)
            ' flanges at top and bottom
            Dim cx = (col.Left + col.Right) * 0.5F
            BioOpsDrawHelper.DrawFlange(canvas, cx, col.Top + 0.01F * h, col.Width * 1.3F, mono)
            BioOpsDrawHelper.DrawFlange(canvas, cx, col.Bottom - 0.01F * h, col.Width * 1.3F, mono)
            ' resin bed (amber band)
            Dim bedTop = col.Top + col.Height * 0.22F
            Dim bedBot = col.Bottom - col.Height * 0.08F
            Using bed As New SKPaint With {.Color = If(mono, New SKColor(180, 180, 180, 230), New SKColor(210, 175, 105, 230)), .IsAntialias = True}
                canvas.DrawRect(New SKRect(col.Left + 2.5F, bedTop, col.Right - 2.5F, bedBot), bed)
            End Using
            ' top & bottom distributor plates (thin dark bands)
            Using plate As New SKPaint With {.Color = BioOpsDrawHelper.ClrStroke(mono), .IsAntialias = True}
                canvas.DrawRect(New SKRect(col.Left + 2.5F, bedTop - 2, col.Right - 2.5F, bedTop), plate)
                canvas.DrawRect(New SKRect(col.Left + 2.5F, bedBot, col.Right - 2.5F, bedBot + 2), plate)
            End Using
            ' denser beads hint
            Dim r = 0.009F * w
            Using bead As New SKPaint With {.Color = If(mono, New SKColor(130, 130, 130), New SKColor(140, 100, 50, 255)), .IsAntialias = True}
                Dim y = bedTop + 2 * r
                Dim row = 0
                While y < bedBot - r
                    Dim xoff = If(row Mod 2 = 0, 0.0F, 1.0F * r)
                    Dim x = col.Left + 4 * r + xoff
                    While x < col.Right - 2 * r
                        canvas.DrawCircle(x, y, r, bead)
                        x += 2.0F * r
                    End While
                    y += 1.8F * r
                    row += 1
                End While
            End Using
            ' inlet & outlet pipes with flanges
            BioOpsDrawHelper.DrawPipe(canvas, New SKPoint(gx + 0.08F * w, gy + 0.08F * h), New SKPoint(cx, gy + 0.08F * h), 0.04F * h, mono)
            BioOpsDrawHelper.DrawFlange(canvas, gx + 0.08F * w, gy + 0.08F * h, 0.07F * w, mono)
            BioOpsDrawHelper.DrawPipe(canvas, New SKPoint(cx, gy + 0.95F * h), New SKPoint(gx + 0.92F * w, gy + 0.95F * h), 0.04F * h, mono)
            BioOpsDrawHelper.DrawFlange(canvas, gx + 0.92F * w, gy + 0.95F * h, 0.07F * w, mono)
        End Sub


        ''' <summary>Chart names the PFD chart object can embed: the curves of the last dynamic bind-elute run.</summary>
        Public Overrides Function GetChartModelNames() As List(Of String)
            If Mode <> ChromatographyMode.BindElute_Dynamic Then Return New List(Of String)()
            Return New List(Of String)({"Breakthrough Curve", "Cumulative Load"})
        End Function

        ''' <summary>Builds an OxyPlot model of the breakthrough curve (against bed volumes) or of the cumulative load (against time).</summary>
        Public Overrides Function GetChartModel(name As String) As Object

            Dim traj = LastTrajectory
            If traj Is Nothing OrElse traj.Times Is Nothing OrElse traj.Times.Count = 0 Then Return Nothing

            Dim series As New List(Of Tuple(Of String, String))()
            Dim x() As Double
            Dim xTitle As String
            Dim yTitle As String
            Select Case name
                Case "Breakthrough Curve"
                    series.Add(Tuple.Create("C_over_C0", "C / C0"))
                    series.Add(Tuple.Create("Breakthrough", "1 - C/C0"))
                    x = traj.GetSeries("BedVolumes")
                    xTitle = "Bed volumes"
                    yTitle = "Fraction"
                Case "Cumulative Load"
                    series.Add(Tuple.Create("QLoaded", "q loaded (g/L resin)"))
                    x = traj.GetTimes()
                    xTitle = "Time (s)"
                    yTitle = "Load (g/L resin)"
                Case Else
                    Return Nothing
            End Select

            Dim model = New OxyPlot.PlotModel() With {.Subtitle = name, .Title = GraphicObject.Tag}
            model.TitleFontSize = 11
            model.SubtitleFontSize = 10
            model.LegendFontSize = 9
            model.LegendPlacement = OxyPlot.LegendPlacement.Outside
            model.LegendOrientation = OxyPlot.LegendOrientation.Horizontal
            model.LegendPosition = OxyPlot.LegendPosition.BottomCenter
            model.TitleHorizontalAlignment = OxyPlot.TitleHorizontalAlignment.CenteredWithinView
            model.Axes.Add(New OxyPlot.Axes.LinearAxis() With {
                .MajorGridlineStyle = OxyPlot.LineStyle.Dash,
                .MinorGridlineStyle = OxyPlot.LineStyle.Dot,
                .Position = OxyPlot.Axes.AxisPosition.Bottom,
                .FontSize = 10,
                .Title = xTitle
            })
            model.Axes.Add(New OxyPlot.Axes.LinearAxis() With {
                .MajorGridlineStyle = OxyPlot.LineStyle.Dash,
                .MinorGridlineStyle = OxyPlot.LineStyle.Dot,
                .Position = OxyPlot.Axes.AxisPosition.Left,
                .FontSize = 10,
                .Title = yTitle
            })

            Dim colors = {OxyPlot.OxyColors.Red, OxyPlot.OxyColors.Blue}
            For i = 0 To series.Count - 1
                Dim y = traj.GetSeries(series(i).Item1)
                Dim ls As New OxyPlot.Series.LineSeries() With {.Title = series(i).Item2, .StrokeThickness = 1.5, .Color = colors(i Mod colors.Length)}
                For j = 0 To Math.Min(x.Length, y.Length) - 1
                    If Not Double.IsNaN(y(j)) AndAlso Not Double.IsInfinity(y(j)) Then ls.Points.Add(New OxyPlot.DataPoint(x(j), y(j)))
                Next
                model.Series.Add(ls)
            Next

            Return model

        End Function

    End Class

End Namespace
