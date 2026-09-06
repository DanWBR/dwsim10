'    Free-Radical Polymerization Reactor (CSTR) - unit operation
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

Imports System.Linq
Imports DWSIM.Thermodynamics
Imports DWSIM.Thermodynamics.BaseClasses
Imports DWSIM.Interfaces.Enums
Imports DWSIM.Thermodynamics.Streams
Imports DWSIM.Thermodynamics.Polymers
Imports DWSIM.SharedClasses

Namespace Reactors

    ''' <summary>
    ''' Free-radical polymerization reactor operated as a homogeneous, isothermal, well-mixed vessel. It reads
    ''' the monomer and initiator (and optional solvent / chain-transfer agent) from its feed, solves the
    ''' steady-state method-of-moments model at the reactor residence time, and writes the unreacted feed plus
    ''' the polymer to its product stream, reporting conversion and the number- and weight-average molar masses.
    ''' The polymer product compound's molar mass is set to the computed Mn so the mass balance closes.
    ''' </summary>
    <System.Serializable()> Public Partial Class Reactor_Polymerization

        Inherits Reactor

        <NonSerialized> <Xml.Serialization.XmlIgnore> Public f As Object

        ' --- configuration ---

        ''' <summary>Name of the monomer compound in the feed.</summary>
        Public Property MonomerID As String = ""

        ''' <summary>Name of the initiator compound in the feed.</summary>
        Public Property InitiatorID As String = ""

        ''' <summary>Name of the solvent or chain-transfer-agent compound (empty for a bulk polymerization).</summary>
        Public Property SolventID As String = ""

        ''' <summary>Name of the polymer product compound (a PC-SAFT pseudo-compound already in the flowsheet).</summary>
        Public Property PolymerID As String = ""

        ''' <summary>Reactor vessel volume (m3).</summary>
        Public Property Volume As Double = 1.0

        ''' <summary>Isothermal operating temperature (K); when zero the feed temperature is used.</summary>
        Public Property IsothermalTemperature As Double = 0.0

        ''' <summary>Heat of polymerization per mole of monomer added to a chain (J/mol, negative = exothermic).</summary>
        Public Property HeatOfPolymerization As Double = -70000.0

        ' --- Arrhenius kinetics (k = A*exp(-E/RT); A in 1/s or L/mol/s, E in J/mol), styrene/AIBN defaults ---
        Public Property Kd_A As Double = 1.58E+15
        Public Property Kd_E As Double = 128000.0
        Public Property Efficiency As Double = 0.6
        Public Property Kp_A As Double = 4.266E+7
        Public Property Kp_E As Double = 32510.0
        Public Property Ktc_A As Double = 1.255E+9
        Public Property Ktc_E As Double = 8000.0
        Public Property Ktd_A As Double = 0.0
        Public Property Ktd_E As Double = 0.0
        Public Property KtrM_A As Double = 4.266E+7 * 6.0E-5
        Public Property KtrM_E As Double = 32510.0
        Public Property KtrS_A As Double = 0.0
        Public Property KtrS_E As Double = 0.0
        Public Property MonomerMolarMass As Double = 104.15

        ' --- copolymer (binary) configuration; setting a second monomer switches the reactor to the terminal
        '     copolymerization model (reactivity ratios rA/rB), otherwise it homopolymerizes monomer A ---
        ''' <summary>Name of the second monomer compound in the feed (empty = homopolymerization).</summary>
        Public Property MonomerBID As String = ""

        ''' <summary>Reactivity ratio of monomer A, rA = kpAA/kpAB.</summary>
        Public Property ReactivityRatioA As Double = 0.52

        ''' <summary>Reactivity ratio of monomer B, rB = kpBB/kpBA.</summary>
        Public Property ReactivityRatioB As Double = 0.46

        ''' <summary>Arrhenius pre-exponential for the homo-propagation of monomer B (L/mol/s).</summary>
        Public Property KpB_A As Double = 2.673E+6

        ''' <summary>Arrhenius activation energy for the homo-propagation of monomer B (J/mol).</summary>
        Public Property KpB_E As Double = 22360.0

        ''' <summary>Molar mass of the second monomer (g/mol).</summary>
        Public Property MonomerBMolarMass As Double = 100.12

        ' --- molecular-weight distribution emission ---
        ''' <summary>When true, the polymer leaves the reactor as a set of molar-mass pseudo-component cuts
        ''' (a real distribution) rather than a single lumped polymer compound. The cut compounds must first be
        ''' generated onto the flowsheet with <see cref="GenerateDistributionCompounds"/>.</summary>
        Public Property EmitDistribution As Boolean = False

        ''' <summary>Number of pseudo-component cuts the distribution is discretized into.</summary>
        Public Property NumberOfCuts As Integer = 7

        ''' <summary>Shape of the molar-mass distribution the cuts are generated from.</summary>
        Public Property DistributionType As PolymerDistribution = PolymerDistribution.SchulzZimm

        ''' <summary>Names of the generated cut compounds on the flowsheet (the distribution the reactor fills).</summary>
        Public Property CutCompoundNames As New List(Of String)

        ''' <summary>Relative mole fractions of the cuts (from the distribution generator); reproduce the Mn/Mw.</summary>
        Public Property CutMoleFractions As New List(Of Double)

        ' --- results (read-only outputs) ---
        Public Property Conversion As Double = 0.0
        Public Property Mn As Double = 0.0
        Public Property Mw As Double = 0.0
        Public Property PDI As Double = 0.0
        Public Property RateOfPolymerization As Double = 0.0
        Public Property ResidenceTime As Double = 0.0

        ''' <summary>Instantaneous mole fraction of monomer A in the copolymer (copolymer mode only).</summary>
        Public Property CopolymerCompositionA As Double = 0.0

        Public Sub New()
            MyBase.New()
        End Sub

        Public Sub New(ByVal name As String, ByVal description As String)
            MyBase.New()
            Me.ComponentName = name
            Me.ComponentDescription = description
            Me.ReactorOperationMode = OperationMode.Isothermic
        End Sub

        Public Overrides Function CloneXML() As Object
            Dim obj As ICustomXMLSerialization = New Reactor_Polymerization()
            obj.LoadData(Me.SaveData)
            Return obj
        End Function

        Public Overrides Function CloneJSON() As Object
            Return Newtonsoft.Json.JsonConvert.DeserializeObject(Of Reactor_Polymerization)(Newtonsoft.Json.JsonConvert.SerializeObject(Me))
        End Function

        Private Function BuildKinetics() As FreeRadicalKinetics
            Return New FreeRadicalKinetics With {
                .Ad = Kd_A, .Ed = Kd_E, .Efficiency = Efficiency,
                .Ap = Kp_A, .Ep = Kp_E,
                .Atc = Ktc_A, .Etc = Ktc_E, .Atd = Ktd_A, .Etd = Ktd_E,
                .AtrM = KtrM_A, .EtrM = KtrM_E, .AtrS = KtrS_A, .EtrS = KtrS_E,
                .MonomerMW = MonomerMolarMass}
        End Function

        ''' <summary>Loads the AIBN-initiated bulk styrene benchmark kinetics into this reactor.</summary>
        Public Sub LoadStyrenePreset()
            Dim k = FreeRadicalKinetics.StyreneAIBN()
            Kd_A = k.Ad : Kd_E = k.Ed : Efficiency = k.Efficiency
            Kp_A = k.Ap : Kp_E = k.Ep
            Ktc_A = k.Atc : Ktc_E = k.Etc : Ktd_A = k.Atd : Ktd_E = k.Etd
            KtrM_A = k.AtrM : KtrM_E = k.EtrM : KtrS_A = k.AtrS : KtrS_E = k.EtrS
            MonomerMolarMass = k.MonomerMW
        End Sub

        ''' <summary>True when a second monomer is configured, selecting the binary copolymerization model.</summary>
        Public Function IsCopolymer() As Boolean
            Return Not String.IsNullOrEmpty(MonomerBID)
        End Function

        Private Function BuildCopolymerKinetics() As CopolymerKinetics
            Return New CopolymerKinetics With {
                .Ad = Kd_A, .Ed = Kd_E, .Efficiency = Efficiency,
                .ApAA = Kp_A, .EpAA = Kp_E,
                .ApBB = KpB_A, .EpBB = KpB_E,
                .ReactivityA = ReactivityRatioA, .ReactivityB = ReactivityRatioB,
                .Atc = Ktc_A, .Etc = Ktc_E, .Atd = Ktd_A, .Etd = Ktd_E,
                .MonomerAMW = MonomerMolarMass, .MonomerBMW = MonomerBMolarMass}
        End Function

        ''' <summary>Loads the AIBN-initiated styrene(A)/methyl-methacrylate(B) copolymer benchmark kinetics.</summary>
        Public Sub LoadStyreneMMAPreset()
            Dim k = CopolymerKinetics.StyreneMMA()
            Kd_A = k.Ad : Kd_E = k.Ed : Efficiency = k.Efficiency
            Kp_A = k.ApAA : Kp_E = k.EpAA
            KpB_A = k.ApBB : KpB_E = k.EpBB
            ReactivityRatioA = k.ReactivityA : ReactivityRatioB = k.ReactivityB
            Ktc_A = k.Atc : Ktc_E = k.Etc : Ktd_A = k.Atd : Ktd_E = k.Etd
            MonomerMolarMass = k.MonomerAMW : MonomerBMolarMass = k.MonomerBMW
        End Sub

        ''' <summary>Common result of the kinetics solve, whichever polymerization model is active.</summary>
        Private Class KineticsSolution
            Public Converged As Boolean
            Public Conversion As Double        ' overall monomer conversion (molar)
            Public Mn As Double, Mw As Double, PDI As Double, Rp As Double
            Public CompositionA As Double      ' copolymer F_A (0 in homopolymer mode)
            Public ConvA As Double, ConvB As Double   ' per-monomer conversions
            Public IniRatio As Double          ' [I]out/[I]in
        End Class

        Private Function SolveKineticsAt(Tr As Double, theta As Double, Cmon As Double, CmonB As Double,
                                         Cini As Double, Csol As Double) As KineticsSolution
            Dim s As New KineticsSolution
            If IsCopolymer() Then
                Dim r = CopolymerCSTR.Solve(BuildCopolymerKinetics(), Tr, theta, Cmon, CmonB, Cini)
                s.Converged = r.Converged
                s.Conversion = r.OverallConversion
                s.Mn = r.Mn : s.Mw = r.Mw : s.PDI = r.PDI : s.Rp = r.Rp
                s.CompositionA = r.CopolymerCompositionA
                s.ConvA = r.ConversionA : s.ConvB = r.ConversionB
                s.IniRatio = If(Cini > 0.0, r.InitiatorConc / Cini, 1.0)
            Else
                Dim r = FreeRadicalCSTR.Solve(BuildKinetics(), Tr, theta, Cmon, Cini, Csol)
                s.Converged = r.Converged
                s.Conversion = r.Conversion
                s.Mn = r.Mn : s.Mw = r.Mw : s.PDI = r.PDI : s.Rp = r.Rp
                s.CompositionA = 0.0
                s.ConvA = r.Conversion : s.ConvB = 0.0
                s.IniRatio = If(Cini > 0.0, r.InitiatorConc / Cini, 1.0)
            End If
            Return s
        End Function

        ''' <summary>
        ''' Generates the molar-mass pseudo-component cuts for the polymer distribution and registers them on
        ''' the flowsheet - in the compound list and in every material stream - so the reactor can emit a real
        ''' distribution instead of a single lumped polymer. The cuts clone the polymer product compound and
        ''' share its CAS (the equation of state reuses its parameters at each cut's own molar mass); the grid
        ''' is placed from the current Mn and PDI when the reactor has a solution, otherwise from the polymer
        ''' compound's molar mass and a most-probable spread. Call this once, then set <see cref="EmitDistribution"/>.
        ''' </summary>
        Public Sub GenerateDistributionCompounds()

            If FlowSheet Is Nothing Then Throw New Exception("The reactor is not attached to a flowsheet.")
            If String.IsNullOrEmpty(PolymerID) OrElse Not FlowSheet.SelectedCompounds.ContainsKey(PolymerID) Then
                Throw New Exception("Select a polymer product compound first.")
            End If
            Dim baseCP = TryCast(FlowSheet.SelectedCompounds(PolymerID), ConstantProperties)
            If baseCP Is Nothing Then Throw New Exception("The polymer product is not a standard compound.")

            Dim MnGen = If(Mn > 0.0, Mn, baseCP.Molar_Weight)
            Dim PDIGen = If(PDI > 1.0, PDI, 2.0)
            Dim N = Math.Max(2, NumberOfCuts)

            Dim zrel As Double() = Nothing
            Dim cuts = PolymerCharacterization.BuildCuts(baseCP, MnGen, PDIGen, N, DistributionType, zrel)

            CutCompoundNames = New List(Of String)
            CutMoleFractions = New List(Of Double)(zrel)
            For Each cut In cuts
                cut.CurrentDB = baseCP.CurrentDB
                cut.OriginalDB = baseCP.OriginalDB
                If Not FlowSheet.SelectedCompounds.ContainsKey(cut.Name) Then
                    FlowSheet.SelectedCompounds.Add(cut.Name, cut)
                    For Each so In FlowSheet.SimulationObjects.Values
                        If so.GraphicObject IsNot Nothing AndAlso so.GraphicObject.ObjectType = Interfaces.Enums.GraphicObjects.ObjectType.MaterialStream Then
                            Dim ms = TryCast(so, Interfaces.IMaterialStream)
                            If ms IsNot Nothing Then
                                For Each ph In ms.Phases.Values
                                    If Not ph.Compounds.ContainsKey(cut.Name) Then
                                        ph.Compounds.Add(cut.Name, New Compound(cut.Name, "") With {.ConstantProperties = cut, .MoleFraction = 0.0, .MassFraction = 0.0})
                                    End If
                                Next
                            End If
                        End If
                    Next
                End If
                CutCompoundNames.Add(cut.Name)
            Next

        End Sub

        Public Overrides Sub Calculate(Optional ByVal args As Object = Nothing)

            If Not Me.GraphicObject.InputConnectors(0).IsAttached Then Throw New Exception("No feed material stream connected.")
            If Not Me.GraphicObject.OutputConnectors(0).IsAttached Then Throw New Exception("No product material stream connected.")
            If String.IsNullOrEmpty(MonomerID) Then Throw New Exception("No monomer compound selected.")
            If String.IsNullOrEmpty(InitiatorID) Then Throw New Exception("No initiator compound selected.")
            If String.IsNullOrEmpty(PolymerID) Then Throw New Exception("No polymer product compound selected.")
            If Volume <= 0.0 Then Throw New Exception("Reactor volume must be greater than zero.")

            Dim ims As MaterialStream = GetInletMaterialStream(0)
            Dim comps = ims.Phases(0).Compounds
            For Each id In {MonomerID, InitiatorID, PolymerID}
                If Not comps.ContainsKey(id) Then Throw New Exception("Compound '" & id & "' is not present in the feed.")
            Next
            If IsCopolymer() AndAlso Not comps.ContainsKey(MonomerBID) Then Throw New Exception("Second monomer '" & MonomerBID & "' is not present in the feed.")
            Dim hasSolvent = Not String.IsNullOrEmpty(SolventID) AndAlso comps.ContainsKey(SolventID)

            Dim Q As Double = ims.Phases(0).Properties.volumetric_flow.GetValueOrDefault()
            If Q <= 0.0 Then Q = ims.Phases(1).Properties.volumetric_flow.GetValueOrDefault()
            If Q <= 0.0 Then Throw New Exception("Feed volumetric flow is zero; cannot define a residence time.")

            Dim Pout As Double = ims.Phases(0).Properties.pressure.GetValueOrDefault() - Me.DeltaP.GetValueOrDefault()

            Dim monFlow = comps(MonomerID).MolarFlow.GetValueOrDefault()     ' mol/s
            Dim iniFlow = comps(InitiatorID).MolarFlow.GetValueOrDefault()
            Dim solFlow = If(hasSolvent, comps(SolventID).MolarFlow.GetValueOrDefault(), 0.0)
            Dim monBFlow = If(IsCopolymer(), comps(MonomerBID).MolarFlow.GetValueOrDefault(), 0.0)

            ' Concentrations in mol/L (volumetric flow is m3/s).
            Dim Cmon = monFlow / (Q * 1000.0)
            Dim Cini = iniFlow / (Q * 1000.0)
            Dim Csol = solFlow / (Q * 1000.0)
            Dim CmonB = monBFlow / (Q * 1000.0)
            Dim theta = Volume / Q

            Dim Tin = ims.Phases(0).Properties.temperature.GetValueOrDefault()
            Dim W = ims.Phases(0).Properties.massflow.GetValueOrDefault()          ' kg/s
            Dim Cp = ims.Phases(0).Properties.heatCapacityCp.GetValueOrDefault()   ' kJ/kg.K

            ' Reaction (outlet) temperature. A well-mixed reactor reacts at its outlet temperature; for
            ' adiabatic operation that temperature and the conversion are coupled through the heat of
            ' polymerization and the Arrhenius rate constants, so they are iterated to a fixed point.
            Dim Tr As Double
            Dim r As KineticsSolution
            Select Case Me.ReactorOperationMode
                Case OperationMode.OutletTemperature
                    Tr = Me.OutletTemperature
                    r = SolveKineticsAt(Tr, theta, Cmon, CmonB, Cini, Csol)
                Case OperationMode.Adiabatic
                    Tr = If(IsothermalTemperature > 0.0, IsothermalTemperature, Tin)
                    r = SolveKineticsAt(Tr, theta, Cmon, CmonB, Cini, Csol)
                    If W > 0.0 AndAlso Cp > 0.0 Then
                        For it As Integer = 1 To 50
                            Dim Qg = r.Conversion * (monFlow + monBFlow) * (-HeatOfPolymerization) / 1000.0 ' kW
                            Dim Tnew = Tin + Qg / (W * Cp)
                            Dim converged = Math.Abs(Tnew - Tr) < 0.01
                            Tr = 0.5 * Tnew + 0.5 * Tr
                            r = SolveKineticsAt(Tr, theta, Cmon, CmonB, Cini, Csol)
                            If converged Then Exit For
                        Next
                    End If
                Case Else ' Isothermic
                    Tr = If(IsothermalTemperature > 0.0, IsothermalTemperature, Tin)
                    r = SolveKineticsAt(Tr, theta, Cmon, CmonB, Cini, Csol)
            End Select
            If Not r.Converged Then Throw New Exception("The polymerization solver did not converge at these conditions.")

            Conversion = r.Conversion
            Mn = r.Mn : Mw = r.Mw : PDI = r.PDI
            RateOfPolymerization = r.Rp
            CopolymerCompositionA = r.CompositionA
            ResidenceTime = theta

            ' Energy balance: the heat released by polymerization is W*Cp*(Tr-Tin) minus the duty. Adiabatic
            ' operation removes no heat (the temperature rise carries it); otherwise the duty holds the reactor
            ' at Tr (negative duty = heat removed, the usual case for an exothermic polymerization).
            Dim QgenkW = r.Conversion * (monFlow + monBFlow) * (-HeatOfPolymerization) / 1000.0 ' kW
            Dim duty As Double = 0.0
            If Me.ReactorOperationMode <> OperationMode.Adiabatic Then duty = W * Cp * (Tr - Tin) - QgenkW
            Me.DeltaQ = duty
            Me.DeltaT = Tr - Tin

            ' Reacted monomer mass (both monomers in copolymer mode) and the polymer chain molar flow.
            Dim convAmol = monFlow * r.ConvA
            Dim convBmol = monBFlow * r.ConvB
            Dim polymerMass = convAmol * MonomerMolarMass + convBmol * MonomerBMolarMass  ' g/s
            Dim chainFlow = If(r.Mn > 0.0, polymerMass / r.Mn, 0.0)
            Dim iniRatio = r.IniRatio

            ' Emit the polymer as a real molar-mass distribution over the generated cuts, or as a single
            ' lumped polymer compound whose molar mass is set to Mn (so the reacted monomer mass is conserved).
            Dim distributing = EmitDistribution AndAlso CutCompoundNames.Count > 0 AndAlso
                               CutMoleFractions.Count = CutCompoundNames.Count AndAlso
                               CutCompoundNames.All(Function(nm) comps.ContainsKey(nm))
            If Not distributing Then
                comps(PolymerID).ConstantProperties.Molar_Weight = If(r.Mn > 0.0, r.Mn, comps(PolymerID).ConstantProperties.Molar_Weight)
            End If

            ' Outlet molar flows: monomer depleted, initiator partly consumed, polymer produced, rest inert.
            Dim outFlow As New Dictionary(Of String, Double)
            For Each c In comps.Values
                Dim fl = c.MolarFlow.GetValueOrDefault()
                If c.Name = MonomerID Then
                    fl = monFlow * (1.0 - r.ConvA)
                ElseIf IsCopolymer() AndAlso c.Name = MonomerBID Then
                    fl = monBFlow * (1.0 - r.ConvB)
                ElseIf c.Name = InitiatorID Then
                    fl = iniFlow * iniRatio
                ElseIf c.Name = PolymerID AndAlso Not distributing Then
                    fl = c.MolarFlow.GetValueOrDefault() + chainFlow
                End If
                outFlow(c.Name) = Math.Max(fl, 0.0)
            Next

            If distributing Then
                ' Distribute the reacted polymer mass over the cuts using the generator's relative mole
                ' fractions z (which reproduce the distribution's Mn and Mw). The cut mole flow is
                ' polymerMass * z_j / sum(z_k * M_k), so the total mass equals the reacted monomer mass and the
                ' number-average molar mass of the cuts is preserved.
                Dim denomZM As Double = 0.0
                For j As Integer = 0 To CutCompoundNames.Count - 1
                    denomZM += CutMoleFractions(j) * comps(CutCompoundNames(j)).ConstantProperties.Molar_Weight
                Next
                If denomZM > 0.0 Then
                    For j As Integer = 0 To CutCompoundNames.Count - 1
                        outFlow(CutCompoundNames(j)) += polymerMass * CutMoleFractions(j) / denomZM
                    Next
                End If
            End If

            Dim total As Double = outFlow.Values.Sum()

            Dim cpt = Me.GraphicObject.OutputConnectors(0)
            Dim oms As MaterialStream = FlowSheet.SimulationObjects(cpt.AttachedConnector.AttachedTo.Name)
            With oms
                .SpecType = StreamSpec.Temperature_and_Pressure
                .Phases(0).Properties.temperature = Tr
                .Phases(0).Properties.pressure = Pout
                For Each c In .Phases(0).Compounds.Values
                    c.MoleFraction = If(total > 0.0, outFlow(c.Name) / total, 0.0)
                Next
                .Phases(0).Properties.massflow = W
                .DefinedFlow = FlowSpec.Mass
            End With

            Dim es = GetInletEnergyStream(1)
            If es IsNot Nothing Then
                es.EnergyFlow = duty
                If es.GraphicObject IsNot Nothing Then es.GraphicObject.Calculated = True
            End If

        End Sub

        Public Overrides Sub DeCalculate()
            Dim cp = Me.GraphicObject.OutputConnectors(0)
            If cp.IsAttached Then
                Dim oms As MaterialStream = FlowSheet.SimulationObjects(cp.AttachedConnector.AttachedTo.Name)
                oms.Clear()
            End If
        End Sub

        Public Overrides Function GetProperties(ByVal proptype As Interfaces.Enums.PropertyType) As String()
            Dim proplist As New List(Of String)
            Select Case proptype
                Case PropertyType.RW, PropertyType.WR
                    proplist.AddRange({"Volume", "Isothermal Temperature"})
                Case Else
                    proplist.AddRange({"Volume", "Isothermal Temperature", "Residence Time", "Conversion",
                                       "Number-Average Molar Mass (Mn)", "Weight-Average Molar Mass (Mw)",
                                       "Polydispersity Index", "Rate of Polymerization", "Heat Duty",
                                       "Temperature Rise"})
                    If IsCopolymer() Then proplist.Add("Copolymer Composition (monomer A)")
            End Select
            Return proplist.ToArray()
        End Function

        Public Overrides Function GetPropertyValue(ByVal prop As String, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As Object
            If su Is Nothing Then su = New SystemsOfUnits.SI
            Select Case prop
                Case "Volume" : Return SystemsOfUnits.Converter.ConvertFromSI(su.volume, Volume)
                Case "Isothermal Temperature" : Return SystemsOfUnits.Converter.ConvertFromSI(su.temperature, IsothermalTemperature)
                Case "Residence Time" : Return SystemsOfUnits.Converter.ConvertFromSI(su.time, ResidenceTime)
                Case "Conversion" : Return Conversion * 100.0
                Case "Number-Average Molar Mass (Mn)" : Return Mn
                Case "Weight-Average Molar Mass (Mw)" : Return Mw
                Case "Polydispersity Index" : Return PDI
                Case "Rate of Polymerization" : Return RateOfPolymerization
                Case "Copolymer Composition (monomer A)" : Return CopolymerCompositionA
                Case "Heat Duty" : Return SystemsOfUnits.Converter.ConvertFromSI(su.heatflow, Me.DeltaQ.GetValueOrDefault())
                Case "Temperature Rise" : Return SystemsOfUnits.Converter.ConvertFromSI(su.deltaT, Me.DeltaT.GetValueOrDefault())
                Case Else : Return Nothing
            End Select
        End Function

        Public Overrides Function SetPropertyValue(ByVal prop As String, ByVal propval As Object, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As Boolean
            If su Is Nothing Then su = New SystemsOfUnits.SI
            Select Case prop
                Case "Volume" : Volume = SystemsOfUnits.Converter.ConvertToSI(su.volume, propval) : Return True
                Case "Isothermal Temperature" : IsothermalTemperature = SystemsOfUnits.Converter.ConvertToSI(su.temperature, propval) : Return True
            End Select
            Return False
        End Function

        Public Overrides Function GetPropertyUnit(ByVal prop As String, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As String
            If su Is Nothing Then su = New SystemsOfUnits.SI
            Select Case prop
                Case "Volume" : Return su.volume
                Case "Isothermal Temperature" : Return su.temperature
                Case "Residence Time" : Return su.time
                Case "Conversion" : Return "%"
                Case "Number-Average Molar Mass (Mn)", "Weight-Average Molar Mass (Mw)" : Return "g/mol"
                Case "Rate of Polymerization" : Return "mol/[L.s]"
                Case "Copolymer Composition (monomer A)" : Return "mole fraction"
                Case "Heat Duty" : Return su.heatflow
                Case "Temperature Rise" : Return su.deltaT
                Case Else : Return ""
            End Select
        End Function

        Public Overrides Function GetIconBitmapBytes() As Byte()
            Return GetBytesFromResource("DWSIM.UnitOperations.cstr.png")
        End Function

        Public Overrides Function GetDisplayDescription() As String
            Return "Free-radical polymerization reactor (isothermal CSTR, method of moments)"
        End Function

        Public Overrides Function GetDisplayName() As String
            Return "Polymerization Reactor"
        End Function

        Public Overrides ReadOnly Property MobileCompatible As Boolean
            Get
                Return False
            End Get
        End Property

        Public Overrides Function GetReport(su As IUnitsOfMeasure, ci As Globalization.CultureInfo, numberformat As String) As String
            Dim str As New Text.StringBuilder
            str.AppendLine("Polymerization Reactor: " & Me.GraphicObject.Tag)
            str.AppendLine()
            str.AppendLine("Monomer: " & MonomerID & "   Initiator: " & InitiatorID &
                           If(String.IsNullOrEmpty(SolventID), "", "   Solvent/CTA: " & SolventID))
            str.AppendLine("Polymer product: " & PolymerID)
            str.AppendLine()
            str.AppendLine("Operation mode: " & ReactorOperationMode.ToString())
            str.AppendLine("Residence time: " & SystemsOfUnits.Converter.ConvertFromSI(su.time, ResidenceTime).ToString(numberformat, ci) & " " & su.time)
            str.AppendLine("Conversion: " & (Conversion * 100.0).ToString(numberformat, ci) & " %")
            str.AppendLine("Temperature rise: " & SystemsOfUnits.Converter.ConvertFromSI(su.deltaT, DeltaT.GetValueOrDefault()).ToString(numberformat, ci) & " " & su.deltaT)
            str.AppendLine("Heat duty: " & SystemsOfUnits.Converter.ConvertFromSI(su.heatflow, DeltaQ.GetValueOrDefault()).ToString(numberformat, ci) & " " & su.heatflow)
            str.AppendLine("Number-average molar mass (Mn): " & Mn.ToString(numberformat, ci) & " g/mol")
            str.AppendLine("Weight-average molar mass (Mw): " & Mw.ToString(numberformat, ci) & " g/mol")
            str.AppendLine("Polydispersity index (Mw/Mn): " & PDI.ToString(numberformat, ci))
            Return str.ToString()
        End Function

    End Class

End Namespace
