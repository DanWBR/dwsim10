'    Centrifuge (disk-stack / decanter / tubular) - Calculation Routines
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

    Public Enum CentrifugeType
        DiskStack = 0
        Decanter = 1
        Tubular = 2
    End Enum

    ''' <summary>How the centrifuge decides the fraction of each compound that leaves through the Heavy outlet.</summary>
    Public Enum CentrifugeRecoveryModel
        ''' <summary>Every compound goes to the Heavy outlet in the fraction the user gives (<see cref="UnitOp_Centrifuge.RecoveryToHeavy"/>, or <see cref="UnitOp_Centrifuge.DefaultRecoveryToHeavy"/>).</summary>
        UserFractions = 0
        ''' <summary>The particulate compounds follow the Sigma theory (Ambler 1952): Stokes settling against the throughput over an
        ''' effective settling area, integrated over a log-normal particle size distribution. The other compounds keep the user fractions.</summary>
        SigmaTheory = 1
    End Enum

    ''' <summary>
    ''' Solids / liquid centrifuge (disk-stack / decanter / tubular). Splits the inlet between a
    ''' Heavy (concentrate / cake) outlet and a Light (clarified) outlet with a recovery-to-heavy
    ''' fraction r_i (0 to 1) per compound. With <see cref="CentrifugeRecoveryModel.UserFractions"/> (default) every
    ''' r_i is a user input. With <see cref="CentrifugeRecoveryModel.SigmaTheory"/> the particulate compounds
    ''' (cells, debris, solids) get r_i from the Sigma theory: Stokes settling velocity
    ''' v_g(d) = d^2 (rho_p - rho_L) g / (18 mu), grade efficiency G(d) = min(1, v_g(d) Sigma_ef / Q),
    ''' with Sigma_ef = eta Sigma and the cut size d50 at Q = 2 v_g(d50) Sigma_ef, integrated over a
    ''' log-normal mass distribution of particle size (Ambler 1952; Svarovsky 2000; Maybury et al. 2000;
    ''' Harrison et al. 2015). The liquid carried with the solids keeps the user fractions.
    ''' </summary>
    <System.Serializable()> Public Partial Class UnitOp_Centrifuge

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

        ''' <summary>Gets or sets the centrifuge type: <c>DiskStack</c> (0, default), <c>Decanter</c> (1) or <c>Tubular</c> (2).
        ''' With <see cref="CentrifugeRecoveryModel.UserFractions"/> it is informational. With <see cref="CentrifugeRecoveryModel.SigmaTheory"/>
        ''' it sets the default Sigma efficiency (see <see cref="SigmaEfficiency"/>) and the geometry formula for Sigma.</summary>
        Public Property Technology As CentrifugeType = CentrifugeType.DiskStack
        ''' <summary>Gets or sets the bowl rotational speed, in rpm. Default 6000 rpm. Informational with <see cref="CentrifugeRecoveryModel.UserFractions"/>.
        ''' With <see cref="CentrifugeRecoveryModel.SigmaTheory"/> Sigma scales with the square of the speed: through the geometry when it is given,
        ''' or relative to <see cref="ReferenceBowlSpeed_rpm"/> otherwise. Setting this property directly does not move the reference speed;
        ''' <see cref="ChangeBowlSpeed"/> (used by the editors and by <c>SetPropertyValue("Bowl Speed")</c>) does.</summary>
        Public Property BowlSpeed_rpm As Double = 6000.0
        ''' <summary>Gets or sets the sigma factor (equivalent settling area), in m2, at <see cref="ReferenceBowlSpeed_rpm"/> (at the bowl speed when the reference is 0).
        ''' Default 1000 m2. Informational with <see cref="CentrifugeRecoveryModel.UserFractions"/>; not used when the geometry gives Sigma.</summary>
        Public Property SigmaFactor_m2 As Double = 1000.0
        ''' <summary>Gets or sets how the recovery to the Heavy outlet is found: <c>UserFractions</c> (0, default, every fraction is an input)
        ''' or <c>SigmaTheory</c> (1, the particulate compounds follow the Sigma theory).</summary>
        Public Property RecoveryModel As CentrifugeRecoveryModel = CentrifugeRecoveryModel.UserFractions
        ''' <summary>Gets or sets the bowl speed, in rpm, at which <see cref="SigmaFactor_m2"/> was given. Default 0: Sigma applies at the current bowl speed.
        ''' <see cref="ChangeBowlSpeed"/> sets it to the old speed the first time the speed changes, so that Sigma then scales with (N / N_ref)^2.</summary>
        Public Property ReferenceBowlSpeed_rpm As Double = 0.0
        ''' <summary>Gets or sets the Sigma efficiency eta (0 to 1) that turns the theoretical Sigma into the effective one, Sigma_ef = eta Sigma.
        ''' Default 0: the value for the technology (tubular 0.90, decanter 0.60, disk stack 0.55; Svarovsky 2000).</summary>
        Public Property SigmaEfficiency As Double = 0.0
        ''' <summary>Gets or sets the number of discs of a disk-stack bowl, for Sigma from the geometry. Default 0 (Sigma from <see cref="SigmaFactor_m2"/>).</summary>
        Public Property NumberOfDiscs As Integer = 0
        ''' <summary>Gets or sets the disc half-cone angle, in degrees, measured from the axis of rotation. Default 40 degrees. Used only with the disk-stack geometry.</summary>
        Public Property DiscAngle_deg As Double = 40.0
        ''' <summary>Gets or sets the outer radius, in m: the disc outer radius of a disk stack, or the bowl wall radius of a tubular or decanter bowl. Default 0 (no geometry).</summary>
        Public Property OuterRadius_m As Double = 0.0
        ''' <summary>Gets or sets the inner radius, in m: the disc inner radius of a disk stack, or the liquid surface (weir) radius of a tubular or decanter bowl. Default 0.</summary>
        Public Property InnerRadius_m As Double = 0.0
        ''' <summary>Gets or sets the length of the cylindrical bowl, in m, for Sigma from the geometry of a tubular or decanter bowl. Default 0 (no geometry).</summary>
        Public Property BowlLength_m As Double = 0.0
        ''' <summary>Gets or sets the mass median particle diameter d50, in micrometres, per compound name, for the Sigma theory.
        ''' A compound with no entry takes the default for its kind (see <see cref="ParticleDefaults"/>); an entry of 0 marks the compound as not particulate.</summary>
        Public Property ParticleMedianDiameter_um As Dictionary(Of String, Double)
        ''' <summary>Gets or sets the geometric standard deviation sigma_g (1 or more) of the log-normal particle size distribution, per compound name.
        ''' 1 is a monodisperse population. A compound with no entry takes the default for its kind.</summary>
        Public Property ParticleSizeSpread As Dictionary(Of String, Double)
        ''' <summary>Gets or sets the particle density, in kg/m3, per compound name. A compound with no entry takes the solid density of the compound,
        ''' or 1100 kg/m3 (a wet cell) when the compound has none.</summary>
        Public Property ParticleDensity_kgm3 As Dictionary(Of String, Double)
        ''' <summary>Gets or sets the fraction (0 to 1) of each compound sent to the Heavy outlet when the compound has no entry in <see cref="RecoveryToHeavy"/>. Default 0.05.</summary>
        Public Property DefaultRecoveryToHeavy As Double = 0.05
        ''' <summary>Gets or sets the per-compound fraction (0 to 1) of the feed mass sent to the Heavy (concentrate) outlet, keyed by compound name. The remainder goes to the Light (clarified) outlet.</summary>
        Public Property RecoveryToHeavy As Dictionary(Of String, Double)

        ''' <summary>Gets or sets the calculated feed mass flow, in kg/s.</summary>
        Public Property Result_FeedMass_kgs As Double = 0.0
        ''' <summary>Gets or sets the calculated Heavy (concentrate) outlet mass flow, in kg/s.</summary>
        Public Property Result_HeavyMass_kgs As Double = 0.0
        ''' <summary>Gets or sets the calculated Light (clarified) outlet mass flow, in kg/s.</summary>
        Public Property Result_LightMass_kgs As Double = 0.0
        ''' <summary>Gets or sets the calculated solids recovery (0 to 1). With <see cref="CentrifugeRecoveryModel.UserFractions"/>: the fraction of the compounds
        ''' with molecular weight above 10000 g/mol that goes to the Heavy outlet. With <see cref="CentrifugeRecoveryModel.SigmaTheory"/>: the fraction of the
        ''' particulate compounds that goes to the Heavy outlet. Zero when no such compound is present.</summary>
        Public Property Result_SolidsRecovery As Double = 0.0
        ''' <summary>Gets or sets the Sigma at the bowl speed, in m2, before the efficiency (Sigma theory only).</summary>
        Public Property Result_Sigma_m2 As Double = 0.0
        ''' <summary>Gets or sets whether the Sigma came from the bowl geometry (Sigma theory only).</summary>
        Public Property Result_SigmaFromGeometry As Boolean = False
        ''' <summary>Gets or sets the Sigma efficiency eta used in the calculation (Sigma theory only).</summary>
        Public Property Result_SigmaEfficiency As Double = 0.0
        ''' <summary>Gets or sets the effective Sigma, eta Sigma, in m2 (Sigma theory only).</summary>
        Public Property Result_SigmaEffective_m2 As Double = 0.0
        ''' <summary>Gets or sets the volumetric flow of the feed liquid phase, the throughput Q, in m3/s (Sigma theory only).</summary>
        Public Property Result_LiquidFlow_m3s As Double = 0.0
        ''' <summary>Gets or sets the density of the feed liquid phase, in kg/m3 (Sigma theory only).</summary>
        Public Property Result_LiquidDensity_kgm3 As Double = 0.0
        ''' <summary>Gets or sets the viscosity of the feed liquid phase, in Pa.s (Sigma theory only).</summary>
        Public Property Result_LiquidViscosity_Pas As Double = 0.0
        ''' <summary>Gets or sets the cut size d50 of the grade efficiency curve, in micrometres, per particulate compound (Sigma theory only):
        ''' the diameter recovered at 50 %, Q = 2 v_g(d50) Sigma_ef. A compound whose particles are not denser than the liquid has no entry (it does not settle, recovery 0).</summary>
        Public Property Result_CutSize_um As Dictionary(Of String, Double)
        ''' <summary>Gets or sets the fraction (0 to 1) of each compound sent to the Heavy outlet in the last calculation, keyed by compound name.</summary>
        Public Property Result_Recovery As Dictionary(Of String, Double)

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

        ''' <summary>Initializes a new default instance of the <see cref="UnitOp_Centrifuge"/> class.</summary>
        Public Sub New()
            MyBase.New()
            InitializeDictionaries()
        End Sub

        ''' <summary>Initializes a new instance of the <see cref="UnitOp_Centrifuge"/> class with a name and description.</summary>
        ''' <param name="name">The name of this unit operation.</param>
        ''' <param name="description">A brief description of this unit operation.</param>
        Public Sub New(ByVal name As String, ByVal description As String)
            MyBase.New()
            Me.ComponentName = name
            Me.ComponentDescription = description
            InitializeDictionaries()
        End Sub

        Private Sub InitializeDictionaries()
            RecoveryToHeavy = New Dictionary(Of String, Double)()
            ParticleMedianDiameter_um = New Dictionary(Of String, Double)()
            ParticleSizeSpread = New Dictionary(Of String, Double)()
            ParticleDensity_kgm3 = New Dictionary(Of String, Double)()
            Result_CutSize_um = New Dictionary(Of String, Double)()
            Result_Recovery = New Dictionary(Of String, Double)()
        End Sub

        ''' <summary>Creates a deep copy of this object by round-tripping through XML serialization.</summary>
        ''' <returns>A new <see cref="UnitOp_Centrifuge"/> instance with the same property values.</returns>
        Public Overrides Function CloneXML() As Object
            Dim obj As ICustomXMLSerialization = New UnitOp_Centrifuge()
            obj.LoadData(Me.SaveData)
            Return obj
        End Function

        ''' <summary>Returns the fraction of a compound sent to the Heavy outlet: its entry in <see cref="RecoveryToHeavy"/>, or <see cref="DefaultRecoveryToHeavy"/> when it has none, clamped to 0 to 1.</summary>
        ''' <param name="compName">The compound name.</param>
        ''' <returns>The recovery-to-heavy fraction, between 0 and 1.</returns>
        Public Function RecoveryFor(compName As String) As Double
            If RecoveryToHeavy IsNot Nothing AndAlso RecoveryToHeavy.ContainsKey(compName) Then
                Return Max(0.0, Min(1.0, RecoveryToHeavy(compName)))
            End If
            Return Max(0.0, Min(1.0, DefaultRecoveryToHeavy))
        End Function

        ''' <summary>Standard gravity, in m/s2.</summary>
        Private Const StandardGravity As Double = 9.80665

        ''' <summary>Returns the Sigma efficiency eta of a centrifuge type (Svarovsky 2000): tubular 0.90, decanter 0.60, disk stack 0.55.</summary>
        ''' <param name="type">The centrifuge type.</param>
        ''' <returns>The ratio of the effective to the theoretical Sigma, between 0 and 1.</returns>
        Public Shared Function TechnologySigmaEfficiency(type As CentrifugeType) As Double
            Select Case type
                Case CentrifugeType.Tubular : Return 0.9
                Case CentrifugeType.Decanter : Return 0.6
                Case Else : Return 0.55
            End Select
        End Function

        ''' <summary>Returns the Sigma efficiency the calculation uses: <see cref="SigmaEfficiency"/> when it is positive (at most 1), otherwise the value for the <see cref="Technology"/>.</summary>
        ''' <returns>The Sigma efficiency eta, between 0 and 1.</returns>
        Public Function EffectiveSigmaEfficiency() As Double
            If SigmaEfficiency > 0.0 Then Return Min(1.0, SigmaEfficiency)
            Return TechnologySigmaEfficiency(Technology)
        End Function

        ''' <summary>Returns the theoretical Sigma, in m2, from the bowl geometry at the bowl speed, or 0 when the geometry is incomplete.
        ''' Disk stack (Ambler 1952): Sigma = 2 pi n omega^2 (r2^3 - r1^3) / (3 g tan(theta)), with n <see cref="NumberOfDiscs"/>, r2 <see cref="OuterRadius_m"/>,
        ''' r1 <see cref="InnerRadius_m"/> and theta <see cref="DiscAngle_deg"/>. Tubular and decanter: Sigma = pi L omega^2 (3 r2^2 + r1^2) / (2 g), with L
        ''' <see cref="BowlLength_m"/>, r2 the bowl wall radius and r1 the liquid surface radius; for a decanter this is the cylindrical section only (the cone is left out).</summary>
        ''' <returns>The Sigma, in m2, or 0.</returns>
        Public Function GeometrySigma() As Double
            Dim omega = BowlSpeed_rpm * 2.0 * PI / 60.0
            If omega <= 0.0 OrElse OuterRadius_m <= 0.0 OrElse InnerRadius_m < 0.0 OrElse InnerRadius_m >= OuterRadius_m Then Return 0.0
            If Technology = CentrifugeType.DiskStack Then
                If NumberOfDiscs <= 0 OrElse DiscAngle_deg <= 0.0 OrElse DiscAngle_deg >= 90.0 Then Return 0.0
                Dim theta = DiscAngle_deg * PI / 180.0
                Return 2.0 * PI * NumberOfDiscs * omega ^ 2 * (OuterRadius_m ^ 3 - InnerRadius_m ^ 3) / (3.0 * StandardGravity * Tan(theta))
            Else
                If BowlLength_m <= 0.0 Then Return 0.0
                Return PI * BowlLength_m * omega ^ 2 * (3.0 * OuterRadius_m ^ 2 + InnerRadius_m ^ 2) / (2.0 * StandardGravity)
            End If
        End Function

        ''' <summary>Returns the theoretical Sigma at the bowl speed, in m2: from the geometry when it is complete (<see cref="GeometrySigma"/>), otherwise
        ''' <see cref="SigmaFactor_m2"/> times (<see cref="BowlSpeed_rpm"/> / <see cref="ReferenceBowlSpeed_rpm"/>)^2, with no scaling when the reference speed is 0.</summary>
        ''' <returns>The Sigma, in m2, before the efficiency.</returns>
        Public Function SigmaAtBowlSpeed() As Double
            Dim fromGeometry = GeometrySigma()
            If fromGeometry > 0.0 Then Return fromGeometry
            If ReferenceBowlSpeed_rpm > 0.0 Then Return SigmaFactor_m2 * (BowlSpeed_rpm / ReferenceBowlSpeed_rpm) ^ 2
            Return SigmaFactor_m2
        End Function

        ''' <summary>Changes the bowl speed. When <see cref="ReferenceBowlSpeed_rpm"/> is 0 the old speed becomes the reference first, so that
        ''' <see cref="SigmaFactor_m2"/> stays the Sigma at the speed it was given and the Sigma at the new speed scales with (N / N_ref)^2.</summary>
        ''' <param name="rpm">The new bowl speed, in rpm.</param>
        Public Sub ChangeBowlSpeed(rpm As Double)
            If ReferenceBowlSpeed_rpm <= 0.0 AndAlso BowlSpeed_rpm > 0.0 AndAlso rpm <> BowlSpeed_rpm Then ReferenceBowlSpeed_rpm = BowlSpeed_rpm
            BowlSpeed_rpm = rpm
        End Sub

        ''' <summary>Returns the default particle size of a compound for the Sigma theory: the mass median diameter d50 (micrometres), the geometric
        ''' standard deviation sigma_g and where the values come from. Cell debris (name contains "debris") 1 um, 1.8. Biomass by kind (the compound
        ''' BiomassType, or its name): yeast 5 um, 1.5; bacteria 1 um, 1.4; mammalian cells 15 um, 1.3; microalgae 5 um, 1.6; activated sludge flocs
        ''' 50 um, 2.0; other biomass 3 um, 1.6. Compounds flagged as solids 10 um, 2.0. Any other compound gets d50 = 0: it is not particulate and
        ''' keeps its user fraction.</summary>
        ''' <param name="compound">The compound.</param>
        ''' <returns>The default d50 (um), sigma_g and the source note.</returns>
        Public Shared Function ParticleDefaults(compound As ICompoundConstantProperties) As (Diameter_um As Double, Spread As Double, Source As String)
            If compound Is Nothing Then Return (0.0, 1.0, "not particulate")
            Dim name = If(compound.Name, "").ToLowerInvariant()
            If name.Contains("debris") Then Return (1.0, 1.8, "cell debris default")
            Dim isBiomass = String.Equals(compound.Tag, "Biomass", StringComparison.OrdinalIgnoreCase) OrElse name.StartsWith("biomass")
            Dim kind As String = ""
            Try
                Dim extra = TryCast(compound.ExtraProperties, IDictionary(Of String, Object))
                If extra IsNot Nothing Then
                    If extra.ContainsKey("IsBiomass") AndAlso extra("IsBiomass") IsNot Nothing AndAlso CBool(extra("IsBiomass")) Then isBiomass = True
                    If extra.ContainsKey("BiomassType") AndAlso extra("BiomassType") IsNot Nothing Then kind = extra("BiomassType").ToString()
                End If
            Catch
            End Try
            If isBiomass Then
                If kind = "" OrElse kind = "Generic" Then
                    If name.Contains("yeast") OrElse name.Contains("cerevisiae") OrElse name.Contains("pichia") Then
                        kind = "Yeast"
                    ElseIf name.Contains("coli") OrElse name.Contains("bacter") Then
                        kind = "Bacterial"
                    ElseIf name.Contains("cho") OrElse name.Contains("mammal") Then
                        kind = "Mammalian"
                    ElseIf name.Contains("alga") Then
                        kind = "Algal"
                    ElseIf name.Contains("sludge") Then
                        kind = "MixedCulture"
                    End If
                End If
                Select Case kind
                    Case "Yeast" : Return (5.0, 1.5, "yeast cell default")
                    Case "Bacterial" : Return (1.0, 1.4, "bacterial cell default")
                    Case "Mammalian" : Return (15.0, 1.3, "mammalian cell default")
                    Case "Algal" : Return (5.0, 1.6, "microalgae default")
                    Case "MixedCulture" : Return (50.0, 2.0, "sludge floc default")
                    Case Else : Return (3.0, 1.6, "biomass default")
                End Select
            End If
            If compound.IsSolid Then Return (10.0, 2.0, "solid default")
            Return (0.0, 1.0, "not particulate")
        End Function

        ''' <summary>Returns the particle data the Sigma theory uses for a compound: the entries in <see cref="ParticleMedianDiameter_um"/>,
        ''' <see cref="ParticleSizeSpread"/> and <see cref="ParticleDensity_kgm3"/>, or the defaults (<see cref="ParticleDefaults"/>; the solid density
        ''' of the compound, or 1100 kg/m3).</summary>
        ''' <param name="compound">The compound.</param>
        ''' <returns>The mass median diameter (um, 0 = not particulate), sigma_g (1 or more) and the particle density (kg/m3).</returns>
        Public Function ParticleFor(compound As ICompoundConstantProperties) As (Diameter_um As Double, Spread As Double, Density_kgm3 As Double)
            Dim def = ParticleDefaults(compound)
            Dim d = def.Diameter_um, s = def.Spread
            Dim rho = 1100.0
            If compound IsNot Nothing Then
                Dim name = compound.Name
                If ParticleMedianDiameter_um IsNot Nothing AndAlso ParticleMedianDiameter_um.ContainsKey(name) Then d = ParticleMedianDiameter_um(name)
                If ParticleSizeSpread IsNot Nothing AndAlso ParticleSizeSpread.ContainsKey(name) Then s = ParticleSizeSpread(name)
                If compound.SolidDensityAtTs > 0.0 Then rho = compound.SolidDensityAtTs
                If ParticleDensity_kgm3 IsNot Nothing AndAlso ParticleDensity_kgm3.ContainsKey(name) AndAlso ParticleDensity_kgm3(name) > 0.0 Then rho = ParticleDensity_kgm3(name)
            End If
            Return (Max(0.0, d), Max(1.0, s), rho)
        End Function

        ''' <summary>Returns the smallest particle diameter recovered completely, d_c, in m, from v_g(d_c) Sigma_ef = Q with the Stokes settling velocity
        ''' v_g(d) = d^2 delta_rho g / (18 mu): d_c = sqrt(18 mu Q / (delta_rho g Sigma_ef)). The cut size d50 is d_c / sqrt(2).</summary>
        ''' <param name="flow_m3s">The throughput Q, in m3/s.</param>
        ''' <param name="viscosity_Pas">The liquid viscosity, in Pa.s.</param>
        ''' <param name="densityDifference_kgm3">The particle density minus the liquid density, in kg/m3.</param>
        ''' <param name="effectiveSigma_m2">The effective Sigma, in m2.</param>
        ''' <returns>The diameter, in m; infinite when the particle does not settle.</returns>
        Public Shared Function CriticalDiameter_m(flow_m3s As Double, viscosity_Pas As Double, densityDifference_kgm3 As Double, effectiveSigma_m2 As Double) As Double
            If densityDifference_kgm3 <= 0.0 OrElse effectiveSigma_m2 <= 0.0 Then Return Double.PositiveInfinity
            Return Sqrt(18.0 * viscosity_Pas * Max(0.0, flow_m3s) / (densityDifference_kgm3 * StandardGravity * effectiveSigma_m2))
        End Function

        ''' <summary>Returns the recovery (0 to 1) of a log-normal particle population under the Sigma grade efficiency G(d) = min(1, (d / d_c)^2).
        ''' Closed form of the integral of G over the mass distribution, with s = ln(sigma_g):
        ''' R = (d_m / d_c)^2 exp(2 s^2) Phi((ln(d_c / d_m) - 2 s^2) / s) + 1 - Phi(ln(d_c / d_m) / s). A monodisperse population (sigma_g = 1) gives G(d_m).</summary>
        ''' <param name="medianDiameter">The mass median diameter d_m of the population.</param>
        ''' <param name="spread">The geometric standard deviation sigma_g (1 or more).</param>
        ''' <param name="criticalDiameter">The diameter recovered completely, d_c, in the same unit as <paramref name="medianDiameter"/>.</param>
        ''' <returns>The mass fraction of the population sent to the Heavy outlet.</returns>
        Public Shared Function LogNormalRecovery(medianDiameter As Double, spread As Double, criticalDiameter As Double) As Double
            If medianDiameter <= 0.0 OrElse Double.IsInfinity(criticalDiameter) OrElse Double.IsNaN(criticalDiameter) Then Return 0.0
            If criticalDiameter <= 0.0 Then Return 1.0
            Dim s = Log(Max(1.0, spread))
            If s < 0.000001 Then Return Min(1.0, (medianDiameter / criticalDiameter) ^ 2)
            Dim z = Log(criticalDiameter / medianDiameter)
            Dim below = (medianDiameter / criticalDiameter) ^ 2 * Exp(2.0 * s * s) * NormalCdf((z - 2.0 * s * s) / s)
            Dim above = 1.0 - NormalCdf(z / s)
            Return Max(0.0, Min(1.0, below + above))
        End Function

        Private Shared Function NormalCdf(x As Double) As Double
            Return 0.5 * MathNet.Numerics.SpecialFunctions.Erfc(-x / Sqrt(2.0))
        End Function

        Private Sub ApplySigmaTheory(feed As MaterialStream, props As Dictionary(Of String, ICompoundConstantProperties),
                                     recovery As Dictionary(Of String, Double), particulate As HashSet(Of String))

            ' the throughput, density and viscosity come from the feed liquid phase
            Dim liquid = feed.Phases(1).Properties
            Dim rhoL = liquid.density.GetValueOrDefault
            Dim muL = liquid.viscosity.GetValueOrDefault
            Dim Q = liquid.volumetric_flow.GetValueOrDefault
            If Not Q > 0.0 AndAlso rhoL > 0.0 Then Q = liquid.massflow.GetValueOrDefault / rhoL
            If Not (rhoL > 0.0 AndAlso muL > 0.0 AndAlso Q > 0.0) Then
                Throw New Exception("Centrifuge (Sigma theory): the feed has no liquid phase to take the throughput, density and viscosity from.")
            End If

            Dim sigma = SigmaAtBowlSpeed()
            Dim eta = EffectiveSigmaEfficiency()
            Dim sigmaEf = eta * sigma
            If Not sigmaEf > 0.0 Then
                Throw New Exception("Centrifuge (Sigma theory): Sigma must be positive; set the Sigma factor or the bowl geometry.")
            End If

            Result_Sigma_m2 = sigma
            Result_SigmaFromGeometry = GeometrySigma() > 0.0
            Result_SigmaEfficiency = eta
            Result_SigmaEffective_m2 = sigmaEf
            Result_LiquidFlow_m3s = Q
            Result_LiquidDensity_kgm3 = rhoL
            Result_LiquidViscosity_Pas = muL

            For Each kv In props
                If kv.Value Is Nothing Then Continue For
                Dim particle = ParticleFor(kv.Value)
                If particle.Diameter_um <= 0.0 Then Continue For
                particulate.Add(kv.Key)
                Dim dc = CriticalDiameter_m(Q, muL, particle.Density_kgm3 - rhoL, sigmaEf)
                If Double.IsInfinity(dc) Then
                    ' not denser than the liquid: the particles do not settle
                    recovery(kv.Key) = 0.0
                Else
                    Result_CutSize_um(kv.Key) = dc / Sqrt(2.0) * 1000000.0
                    recovery(kv.Key) = LogNormalRecovery(particle.Diameter_um * 0.000001, particle.Spread, dc)
                End If
            Next

        End Sub

        Public Overrides Sub Calculate(Optional ByVal args As Object = Nothing)

            If Not Me.GraphicObject.InputConnectors(0).IsAttached Then _
                Throw New Exception("Centrifuge: Feed not connected.")
            If Me.GraphicObject.OutputConnectors.Count < 2 OrElse
               Not Me.GraphicObject.OutputConnectors(0).IsAttached OrElse
               Not Me.GraphicObject.OutputConnectors(1).IsAttached Then
                Throw New Exception("Centrifuge: Both Heavy and Light outlets must be connected.")
            End If

            Dim feed As MaterialStream =
                DirectCast(FlowSheet.SimulationObjects(Me.GraphicObject.InputConnectors(0).AttachedConnector.AttachedFrom.Name), MaterialStream)

            Dim T = feed.Phases(0).Properties.temperature.GetValueOrDefault
            Dim P = feed.Phases(0).Properties.pressure.GetValueOrDefault
            Dim m_total = feed.Phases(0).Properties.massflow.GetValueOrDefault

            Dim feedComp As New Dictionary(Of String, Double)
            Dim props As New Dictionary(Of String, ICompoundConstantProperties)
            For Each c In feed.Phases(0).Compounds.Values
                feedComp(c.Name) = c.MassFraction.GetValueOrDefault * m_total
                props(c.Name) = c.ConstantProperties
            Next

            ' the user fractions, replaced by the Sigma theory for the particulate compounds when it is selected
            Dim recovery As New Dictionary(Of String, Double)
            For Each kv In feedComp
                recovery(kv.Key) = RecoveryFor(kv.Key)
            Next
            Result_Sigma_m2 = 0.0 : Result_SigmaFromGeometry = False : Result_SigmaEfficiency = 0.0 : Result_SigmaEffective_m2 = 0.0
            Result_LiquidFlow_m3s = 0.0 : Result_LiquidDensity_kgm3 = 0.0 : Result_LiquidViscosity_Pas = 0.0
            Result_CutSize_um = New Dictionary(Of String, Double)
            Dim particulate As New HashSet(Of String)
            If RecoveryModel = CentrifugeRecoveryModel.SigmaTheory Then ApplySigmaTheory(feed, props, recovery, particulate)
            Result_Recovery = New Dictionary(Of String, Double)(recovery)

            Dim heavy As New Dictionary(Of String, Double)
            Dim light As New Dictionary(Of String, Double)
            Dim m_h As Double = 0.0, m_l As Double = 0.0
            For Each kv In feedComp
                Dim r = recovery(kv.Key)
                heavy(kv.Key) = kv.Value * r
                light(kv.Key) = kv.Value * (1.0 - r)
                m_h += heavy(kv.Key) : m_l += light(kv.Key)
            Next

            Result_FeedMass_kgs = m_total
            Result_HeavyMass_kgs = m_h
            Result_LightMass_kgs = m_l
            ' Solids recovery: if a "biomass-like" compound (MW > 10000) is present, report its recovery;
            ' with the Sigma theory, the recovery of the particulate compounds
            Dim macro_in As Double = 0.0, macro_h As Double = 0.0
            For Each c In feed.Phases(0).Compounds.Values
                If RecoveryModel = CentrifugeRecoveryModel.SigmaTheory Then
                    If particulate.Contains(c.Name) Then
                        macro_in += feedComp(c.Name) : macro_h += heavy(c.Name)
                    End If
                ElseIf c.ConstantProperties IsNot Nothing AndAlso c.ConstantProperties.Molar_Weight > 10000.0 Then
                    macro_in += feedComp(c.Name) : macro_h += heavy(c.Name)
                End If
            Next
            If macro_in > 0 Then Result_SolidsRecovery = macro_h / macro_in Else Result_SolidsRecovery = 0.0

            WriteStream(FlowSheet.SimulationObjects(Me.GraphicObject.OutputConnectors(0).AttachedConnector.AttachedTo.Name),
                        heavy, m_h, T, P)
            WriteStream(FlowSheet.SimulationObjects(Me.GraphicObject.OutputConnectors(1).AttachedConnector.AttachedTo.Name),
                        light, m_l, T, P)

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
            Return "Centrifuge (disk-stack / decanter / tubular)"
        End Function
        ''' <summary>Returns the display name for this unit operation type.</summary>
        ''' <returns>The name string for this unit operation type.</returns>
        Public Overrides Function GetDisplayName() As String
            Return "Centrifuge"
        End Function

        ''' <summary>Generates a plain-text results report for this unit operation.</summary>
        ''' <param name="su">The unit system used for formatting output values (not used; values are reported in fixed units).</param>
        ''' <param name="ci">The culture info used for number formatting.</param>
        ''' <param name="numberformat">A .NET numeric format string (e.g. "G6") applied to output values.</param>
        ''' <returns>A formatted multi-line string report.</returns>
        Public Overrides Function GetReport(su As IUnitsOfMeasure, ci As Globalization.CultureInfo, numberformat As String) As String
            Dim s As New Text.StringBuilder
            s.AppendLine("Centrifuge: " & Me.GraphicObject.Tag)
            s.AppendLine("Technology:    " & Technology.ToString())
            s.AppendLine("Bowl Speed:    " & BowlSpeed_rpm.ToString(numberformat, ci) & " rpm")
            s.AppendLine("Sigma factor:  " & SigmaFactor_m2.ToString(numberformat, ci) & " m2")
            s.AppendLine("Recovery model: " & RecoveryModel.ToString())
            s.AppendLine()
            s.AppendLine("Feed:     " & Result_FeedMass_kgs.ToString(numberformat, ci) & " kg/s")
            s.AppendLine("Heavy:    " & Result_HeavyMass_kgs.ToString(numberformat, ci) & " kg/s")
            s.AppendLine("Light:    " & Result_LightMass_kgs.ToString(numberformat, ci) & " kg/s")
            If RecoveryModel = CentrifugeRecoveryModel.SigmaTheory Then
                s.AppendLine("Solids recovery (particulate compounds): " & (Result_SolidsRecovery * 100).ToString(numberformat, ci) & " %")
                s.AppendLine()
                s.AppendLine("Sigma at bowl speed: " & Result_Sigma_m2.ToString(numberformat, ci) & " m2" & If(Result_SigmaFromGeometry, " (from the geometry)", ""))
                s.AppendLine("Sigma efficiency:    " & Result_SigmaEfficiency.ToString(numberformat, ci))
                s.AppendLine("Effective Sigma:     " & Result_SigmaEffective_m2.ToString(numberformat, ci) & " m2")
                s.AppendLine("Liquid flow (Q):     " & Result_LiquidFlow_m3s.ToString(numberformat, ci) & " m3/s")
                s.AppendLine("Liquid density:      " & Result_LiquidDensity_kgm3.ToString(numberformat, ci) & " kg/m3")
                s.AppendLine("Liquid viscosity:    " & Result_LiquidViscosity_Pas.ToString(numberformat, ci) & " Pa.s")
                If Result_Recovery IsNot Nothing AndAlso Result_CutSize_um IsNot Nothing Then
                    For Each kv In Result_CutSize_um
                        Dim r = If(Result_Recovery.ContainsKey(kv.Key), Result_Recovery(kv.Key), 0.0)
                        s.AppendLine(kv.Key & ": cut size d50 " & kv.Value.ToString(numberformat, ci) & " um, recovery " & (r * 100).ToString(numberformat, ci) & " %")
                    Next
                End If
            Else
                s.AppendLine("Solids recovery (macro MW>10 kDa): " & (Result_SolidsRecovery * 100).ToString(numberformat, ci) & " %")
            End If
            Return s.ToString()
        End Function

        Private Shared ReadOnly _inputProps As String() = {"Technology", "Bowl Speed", "Sigma Factor", "Default Recovery To Heavy", "Recovery Model"}
        Private Shared ReadOnly _sigmaInputProps As String() = {"Reference Bowl Speed", "Sigma Efficiency", "Number of Discs", "Disc Angle", "Outer Radius", "Inner Radius", "Bowl Length"}
        Private Shared ReadOnly _outputProps As String() = {"Feed Mass Flow", "Heavy Mass Flow", "Light Mass Flow", "Solids Recovery",
            "Sigma at Bowl Speed", "Effective Sigma", "Liquid Volumetric Flow", "Liquid Density", "Liquid Viscosity"}

        Private Const RecoveryPrefix As String = "Recovery to Heavy: "
        Private Const CutSizePrefix As String = "Cut Size d50: "

        Private Function InputProps() As String()
            If RecoveryModel = CentrifugeRecoveryModel.SigmaTheory Then Return _inputProps.Concat(_sigmaInputProps).ToArray()
            Return _inputProps
        End Function

        Private Function OutputProps() As String()
            Dim list As New List(Of String)(_outputProps)
            If Result_Recovery IsNot Nothing Then list.AddRange(Result_Recovery.Keys.Select(Function(k) RecoveryPrefix & k))
            If Result_CutSize_um IsNot Nothing Then list.AddRange(Result_CutSize_um.Keys.Select(Function(k) CutSizePrefix & k))
            Return list.ToArray()
        End Function

        Public Overrides Function GetProperties(proptype As PropertyType) As String()
            Dim baseprops = MyBase.GetProperties(proptype)
            Select Case proptype
                Case PropertyType.WR : Return InputProps()
                Case PropertyType.RO : Return OutputProps()
                Case Else : Return _inputProps.Concat(_sigmaInputProps).Concat(OutputProps()).Concat(baseprops).ToArray()
            End Select
        End Function

        Public Overrides Function GetPropertyValue(prop As String, Optional su As IUnitsOfMeasure = Nothing) As Object
            If prop IsNot Nothing AndAlso prop.StartsWith(RecoveryPrefix) Then
                Dim key = prop.Substring(RecoveryPrefix.Length)
                Return If(Result_Recovery IsNot Nothing AndAlso Result_Recovery.ContainsKey(key), Result_Recovery(key), 0.0)
            End If
            If prop IsNot Nothing AndAlso prop.StartsWith(CutSizePrefix) Then
                Dim key = prop.Substring(CutSizePrefix.Length)
                Return If(Result_CutSize_um IsNot Nothing AndAlso Result_CutSize_um.ContainsKey(key), Result_CutSize_um(key), 0.0)
            End If
            Select Case prop
                Case "Technology" : Return Technology.ToString()
                Case "Bowl Speed" : Return BowlSpeed_rpm
                Case "Sigma Factor" : Return SigmaFactor_m2
                Case "Default Recovery To Heavy" : Return DefaultRecoveryToHeavy
                Case "Recovery Model" : Return RecoveryModel.ToString()
                Case "Reference Bowl Speed" : Return ReferenceBowlSpeed_rpm
                Case "Sigma Efficiency" : Return SigmaEfficiency
                Case "Number of Discs" : Return NumberOfDiscs
                Case "Disc Angle" : Return DiscAngle_deg
                Case "Outer Radius" : Return OuterRadius_m
                Case "Inner Radius" : Return InnerRadius_m
                Case "Bowl Length" : Return BowlLength_m
                Case "Feed Mass Flow" : Return Result_FeedMass_kgs
                Case "Heavy Mass Flow" : Return Result_HeavyMass_kgs
                Case "Light Mass Flow" : Return Result_LightMass_kgs
                Case "Solids Recovery" : Return Result_SolidsRecovery
                Case "Sigma at Bowl Speed" : Return Result_Sigma_m2
                Case "Effective Sigma" : Return Result_SigmaEffective_m2
                Case "Liquid Volumetric Flow" : Return Result_LiquidFlow_m3s
                Case "Liquid Density" : Return Result_LiquidDensity_kgm3
                Case "Liquid Viscosity" : Return Result_LiquidViscosity_Pas
                Case Else : Return MyBase.GetPropertyValue(prop, su)
            End Select
        End Function

        Public Overrides Function GetPropertyUnit(prop As String, Optional su As IUnitsOfMeasure = Nothing) As String
            If prop IsNot Nothing AndAlso prop.StartsWith(CutSizePrefix) Then Return "um"
            Select Case prop
                Case "Bowl Speed", "Reference Bowl Speed" : Return "rpm"
                Case "Sigma Factor", "Sigma at Bowl Speed", "Effective Sigma" : Return "m2"
                Case "Feed Mass Flow", "Heavy Mass Flow", "Light Mass Flow" : Return "kg/s"
                Case "Disc Angle" : Return "deg"
                Case "Outer Radius", "Inner Radius", "Bowl Length" : Return "m"
                Case "Liquid Volumetric Flow" : Return "m3/s"
                Case "Liquid Density" : Return "kg/m3"
                Case "Liquid Viscosity" : Return "Pa.s"
                Case Else : Return "-"
            End Select
        End Function

        Public Overrides Function SetPropertyValue(prop As String, propval As Object, Optional su As IUnitsOfMeasure = Nothing) As Boolean
            Dim d As Double = 0.0
            If TypeOf propval Is Double Then
                d = CDbl(propval)
            ElseIf TypeOf propval Is String Then
                Double.TryParse(CStr(propval), Globalization.NumberStyles.Any, Globalization.CultureInfo.CurrentCulture, d)
            ElseIf propval IsNot Nothing AndAlso TypeOf propval Is IConvertible AndAlso Not TypeOf propval Is Boolean Then
                Try
                    d = Convert.ToDouble(propval, Globalization.CultureInfo.InvariantCulture)
                Catch
                End Try
            End If
            Select Case prop
                Case "Technology"
                    Dim t As CentrifugeType
                    If [Enum].TryParse(Of CentrifugeType)(propval?.ToString(), t) Then Technology = t
                    Return True
                Case "Recovery Model"
                    Dim m As CentrifugeRecoveryModel
                    If [Enum].TryParse(Of CentrifugeRecoveryModel)(propval?.ToString(), m) Then RecoveryModel = m
                    Return True
                Case "Bowl Speed" : ChangeBowlSpeed(d) : Return True
                Case "Sigma Factor" : SigmaFactor_m2 = d : Return True
                Case "Default Recovery To Heavy" : DefaultRecoveryToHeavy = d : Return True
                Case "Reference Bowl Speed" : ReferenceBowlSpeed_rpm = d : Return True
                Case "Sigma Efficiency" : SigmaEfficiency = d : Return True
                Case "Number of Discs" : NumberOfDiscs = CInt(Math.Round(d)) : Return True
                Case "Disc Angle" : DiscAngle_deg = d : Return True
                Case "Outer Radius" : OuterRadius_m = d : Return True
                Case "Inner Radius" : InnerRadius_m = d : Return True
                Case "Bowl Length" : BowlLength_m = d : Return True
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
                Return "CF-"
            End Get
        End Property
        Public Function ReturnInstance(typename As String) As Object Implements IExternalUnitOperation.ReturnInstance
            Return New UnitOp_Centrifuge()
        End Function
        Public Sub PopulateEditorPanel(ctner As Object) Implements IExternalUnitOperation.PopulateEditorPanel

            If TypeOf ctner Is AvaloniaEditorPanel Then
                PopulateEditorPanelAvalonia(DirectCast(ctner, AvaloniaEditorPanel))
                Return
            End If
        End Sub

        Private Sub PopulateEditorPanelAvalonia(container As AvaloniaEditorPanel)

            Dim nf = FlowSheet.FlowsheetOptions.NumberFormat

            container.CreateAndAddLabelRow("Centrifuge Configuration")

            container.CreateAndAddDropDownRow("Centrifuge Type",
                                              New List(Of String)({"Disk-Stack", "Decanter", "Tubular"}),
                                              Technology,
                                              Sub(dd, e)
                                                  Technology = CType(dd.SelectedIndex, CentrifugeType)
                                                  FlowSheet.RequestCalculation()
                                              End Sub)

            container.CreateAndAddTextBoxRow(nf, "Bowl Speed (rpm)", BowlSpeed_rpm,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     ChangeBowlSpeed(tb.Text.ParseExpressionToDouble())
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Sigma Factor (m2)", SigmaFactor_m2,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     SigmaFactor_m2 = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddLabelRow("Separation")

            container.CreateAndAddDropDownRow("Recovery Model",
                                              New List(Of String)({"User Fractions", "Sigma Theory"}),
                                              RecoveryModel,
                                              Sub(dd, e)
                                                  RecoveryModel = CType(dd.SelectedIndex, CentrifugeRecoveryModel)
                                                  FlowSheet.RequestCalculation()
                                              End Sub)

            container.CreateAndAddTextBoxRow(nf, "Default Recovery to Heavy (0-1)", DefaultRecoveryToHeavy,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     DefaultRecoveryToHeavy = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddDescriptionRow("Per-compound recovery-to-heavy fractions are set to the default above. Override individual compounds via the Windows editing form. With the Sigma theory, the particulate compounds (cells, debris, solids) take their recovery from the Sigma factor, the feed liquid and their particle size.")

        End Sub

        Public Sub CreateConnectors() Implements IExternalUnitOperation.CreateConnectors
            If GraphicObject Is Nothing Then Return
            Dim w = GraphicObject.Width, h = GraphicObject.Height
            Dim gx = GraphicObject.X, gy = GraphicObject.Y
            If GraphicObject.InputConnectors.Count = 1 AndAlso GraphicObject.OutputConnectors.Count = 2 Then
                GraphicObject.InputConnectors(0).Position = New Point(gx, gy + 0.5 * h)
                GraphicObject.InputConnectors(0).ConnectorName = "Feed"
                GraphicObject.OutputConnectors(0).Position = New Point(gx + w, gy + 0.8 * h)
                GraphicObject.OutputConnectors(0).ConnectorName = "Heavy (Concentrate)"
                GraphicObject.OutputConnectors(1).Position = New Point(gx + w, gy + 0.2 * h)
                GraphicObject.OutputConnectors(1).ConnectorName = "Light (Clarified)"
            Else
                GraphicObject.InputConnectors.Clear() : GraphicObject.OutputConnectors.Clear()
                GraphicObject.InputConnectors.Add(New ConnectionPoint With {
                    .Position = New Point(gx, gy + 0.5 * h), .Type = ConType.ConIn,
                    .Direction = ConDir.Right, .ConnectorName = "Feed"})
                GraphicObject.OutputConnectors.Add(New ConnectionPoint With {
                    .Position = New Point(gx + w, gy + 0.8 * h), .Type = ConType.ConOut,
                    .Direction = ConDir.Right, .ConnectorName = "Heavy (Concentrate)"})
                GraphicObject.OutputConnectors.Add(New ConnectionPoint With {
                    .Position = New Point(gx + w, gy + 0.2 * h), .Type = ConType.ConOut,
                    .Direction = ConDir.Right, .ConnectorName = "Light (Clarified)"})
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
                    "centrifuge_photo", _photoImage) Then Return
            End If
            DrawIcon(canvas, CSng(GraphicObject.X), CSng(GraphicObject.Y),
                     CSng(GraphicObject.Width), CSng(GraphicObject.Height),
                     GraphicObject.DrawMode = 1)
        End Sub

        Private Shared Sub DrawIcon(canvas As SKCanvas, gx As Single, gy As Single, w As Single, h As Single, Optional mono As Boolean = False)
            ' Disc-stack centrifuge: motor on top, rounded body on a skid, feed on top-left, two discharges on right.
            Dim skid As New SKRect(gx + 0.05F * w, gy + 0.8F * h, gx + 0.95F * w, gy + h)
            BioOpsDrawHelper.DrawSkid(canvas, skid, mono)
            Dim bodyRect As New SKRect(gx + 0.2F * w, gy + 0.3F * h, gx + 0.78F * w, gy + 0.82F * h)
            BioOpsDrawHelper.DrawVerticalTank(canvas, bodyRect, mono)
            ' disc-stack hint: three horizontal stripes inside the bowl
            Using band As New SKPaint With {.Color = BioOpsDrawHelper.ClrStrokeLight(mono), .Style = SKPaintStyle.Stroke, .StrokeWidth = 0.7F, .IsAntialias = True}
                For i = 0 To 2
                    Dim yy = bodyRect.Top + bodyRect.Height * (0.25F + i * 0.18F)
                    canvas.DrawLine(bodyRect.Left + 3, yy, bodyRect.Right - 3, yy, band)
                Next
            End Using
            ' mounting flange between motor and bowl
            Dim cxB = (bodyRect.Left + bodyRect.Right) * 0.5F
            BioOpsDrawHelper.DrawFlange(canvas, cxB, gy + 0.3F * h, 0.32F * w, mono)
            ' motor on top centered
            Dim motor As New SKRect(gx + 0.4F * w, gy + 0.08F * h, gx + 0.58F * w, gy + 0.28F * h)
            BioOpsDrawHelper.DrawMotor(canvas, motor, mono)
            ' small pressure gauge on the bowl
            BioOpsDrawHelper.DrawGauge(canvas, gx + 0.7F * w, gy + 0.36F * h, 0.045F * w, mono)
            ' feed pipe from top-left with flange at motor
            BioOpsDrawHelper.DrawPipe(canvas, New SKPoint(gx + 0.05F * w, gy + 0.2F * h), New SKPoint(gx + 0.4F * w, gy + 0.2F * h), 0.05F * h, mono)
            BioOpsDrawHelper.DrawFlange(canvas, gx + 0.4F * w, gy + 0.2F * h, 0.09F * w, mono)
            ' two discharge nozzles on right with flanges
            BioOpsDrawHelper.DrawPipe(canvas, New SKPoint(gx + 0.78F * w, gy + 0.5F * h), New SKPoint(gx + w, gy + 0.5F * h), 0.045F * h, mono)
            BioOpsDrawHelper.DrawFlange(canvas, gx + 0.78F * w, gy + 0.5F * h, 0.08F * w, mono)
            BioOpsDrawHelper.DrawPipe(canvas, New SKPoint(gx + 0.78F * w, gy + 0.72F * h), New SKPoint(gx + w, gy + 0.72F * h), 0.045F * h, mono)
            BioOpsDrawHelper.DrawFlange(canvas, gx + 0.78F * w, gy + 0.72F * h, 0.08F * w, mono)
        End Sub

    End Class

End Namespace
