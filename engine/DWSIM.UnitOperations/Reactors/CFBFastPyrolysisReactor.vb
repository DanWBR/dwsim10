'    CFB Fast Pyrolysis Reactor - 1-D axial PFR with Ranzi multi-step kinetics,
'    sand-circulation energy balance, Geldart-A solids hold-up, and an optional
'    coupled char combustor that closes the energy balance autothermally.
'
'    Copyright 2026 Daniel Wagner O. de Medeiros
'
'    This file is part of DWSIM.
'
'    DWSIM is free software: you can redistribute it and/or modify it under the
'    terms of the GNU General Public License as published by the Free Software
'    Foundation, either version 3 of the License, or (at your option) any later
'    version.

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
Imports DWSIM.UnitOperations.Reactors.CFBPyrolysis
Imports System.Collections.Generic
Imports DWSIM.UI.Shared.Avalonia

Namespace Reactors

    ''' <summary>Sand-circulation / heat-supply mode for the CFB fast-pyrolysis reactor.</summary>
    Public Enum CFBSandMode
        ''' <summary>User supplies SandInletTemperature_K and SandToBiomassRatio;
        ''' the reactor only solves the riser energy balance.</summary>
        External = 0
        ''' <summary>Coupled char-combustor loop: char from the riser is burned in a
        ''' second fluidized-bed combustor, sand is regenerated and recirculated. The
        ''' reactor iterates the sand circulation rate so that the energy closure is
        ''' autothermal (combustor duty = pyrolysis duty + losses).</summary>
        InternalCharCombustor = 1
    End Enum

    ''' <summary>
    ''' Circulating Fluidized Bed (CFB) reactor for fast pyrolysis of lignocellulosic
    ''' biomass. The riser is modelled as a 1-D axial plug-flow reactor discretised
    ''' into N cells; each cell integrates the reduced Ranzi multi-step scheme for
    ''' cellulose/hemicellulose/lignin on mass-fraction basis, with vapor residence
    ''' time computed from the local gas superficial velocity and Geldart-A solids
    ''' hold-up. An optional second block (char combustor) closes the energy balance.
    ''' </summary>
    <System.Serializable()> Public Partial Class Reactor_CFBFastPyrolysis

        Inherits Reactor

        Implements IExternalUnitOperation
        Public ReadOnly Property IsBio As Boolean = True

        Public Overrides Property ObjectClass As SimulationObjectClass
            Get
                Return SimulationObjectClass.Reactors
            End Get
            Set(value As SimulationObjectClass)
                MyBase.ObjectClass = value
            End Set
        End Property

        ''' <summary>Gets or sets the display name for this unit operation.</summary>
        Public Overrides Property ComponentName As String = GetDisplayName()

        ''' <summary>Gets or sets the display description for this unit operation.</summary>
        Public Overrides Property ComponentDescription As String = GetDisplayDescription()

        ' -------- GEOMETRY / HYDRODYNAMICS --------

        ''' <summary>Riser height (m). Typical industrial CFB fast-pyrolysis risers: 5â€“15 m.</summary>
        Public Property RiserHeight_m As Double = 8.0

        ''' <summary>Riser internal diameter (m). Typical 0.2â€“1.5 m for 1â€“100 t/day plants.</summary>
        Public Property RiserDiameter_m As Double = 0.5

        ''' <summary>Number of axial discretisation cells for the 1-D PFR (>= 5, &lt;= 500).</summary>
        Public Property NumAxialCells As Integer = 50

        ''' <summary>Average solids hold-up fraction in the riser (m3_solid / m3_total).
        ''' Default 0.05 is typical for Geldart-A sand at u_g &gt;&gt; u_mf in a dilute riser.</summary>
        Public Property SolidsHoldup As Double = 0.05

        ''' <summary>Bed material (sand/olivine) density (kg/m3). Default 2600 (silica sand).</summary>
        Public Property BedMaterialDensity_kgm3 As Double = 2600.0

        ''' <summary>Bed material specific heat (J/kg/K). Default 830 (silica sand at 500 Â°C).</summary>
        Public Property BedMaterialCp_JkgK As Double = 830.0

        ''' <summary>Carrier gas superficial velocity at inlet (m/s). Default 5 m/s for fast-fluidised CFB.</summary>
        Public Property CarrierGasVelocity_ms As Double = 5.0

        ' -------- OPERATING CONDITIONS --------

        ''' <summary>Sand-supply mode (external constant T, or coupled char combustor).</summary>
        Public Property SandMode As CFBSandMode = CFBSandMode.External

        ''' <summary>Sand inlet temperature (K) when SandMode = External. Typical 700â€“850 K.</summary>
        Public Property SandInletTemperature_K As Double = 820.0

        ''' <summary>Sand mass-flow / biomass-feed mass-flow ratio (kg/kg). Default 15.
        ''' When SandMode = InternalCharCombustor this is only the initial guess.</summary>
        Public Property SandToBiomassRatio As Double = 15.0

        ''' <summary>Heat-loss fraction of riser duty (0..0.2). Default 0.02 (2 %).</summary>
        Public Property HeatLossFraction As Double = 0.02

        ' -------- BIOMASS COMPOSITION (dry basis) --------

        ''' <summary>Cellulose mass fraction of dry biomass (0â€“1). Typical woody biomass 0.40â€“0.50.</summary>
        Public Property CelluloseMassFrac As Double = 0.45

        ''' <summary>Hemicellulose mass fraction of dry biomass (0â€“1). Typical 0.25â€“0.35.</summary>
        Public Property HemicelluloseMassFrac As Double = 0.3

        ''' <summary>Lignin mass fraction of dry biomass (0â€“1). Typical 0.20â€“0.30.</summary>
        Public Property LigninMassFrac As Double = 0.25

        ''' <summary>Enthalpy of pyrolysis per kg of dry biomass feed (J/kg).
        ''' Positive = endothermic. Default 250 kJ/kg (Bridgwater 2012).</summary>
        Public Property HeatOfPyrolysis_Jkg As Double = 250000.0

        ' -------- COMPOUND ROLES --------

        ''' <summary>Biomass compound name in the flowsheet (dry-basis pseudo-solid). Required.</summary>
        Public Property BiomassCompound As String = ""

        ''' <summary>Char compound name (pseudo-solid) produced by pyrolysis.</summary>
        Public Property CharCompound As String = ""

        ''' <summary>Bio-oil compound name (condensable primary vapors, lump).</summary>
        Public Property BioOilCompound As String = ""

        ''' <summary>Non-condensable gas compound name (CO/CO2/CH4/H2/H2O lump).</summary>
        Public Property GasLumpCompound As String = ""

        ''' <summary>Water compound name (optional; for moisture flash and combustor).</summary>
        Public Property WaterCompound As String = "Water"

        ''' <summary>Oxygen compound name (only used by the internal char combustor).</summary>
        Public Property OxygenCompound As String = "Oxygen"

        ''' <summary>Carbon-dioxide compound name (char combustor product).</summary>
        Public Property CO2Compound As String = "Carbon dioxide"

        ''' <summary>Nitrogen compound name (char combustor inert).</summary>
        Public Property NitrogenCompound As String = "Nitrogen"

        ' -------- CHAR COMBUSTOR PARAMETERS --------

        ''' <summary>Char heating value (J/kg) used by the internal combustor. Default 30 MJ/kg.</summary>
        Public Property CharLHV_Jkg As Double = 30000000.0

        ''' <summary>Excess air fraction over stoichiometric for the char combustor (0..1). Default 0.2 = 20 %.</summary>
        Public Property CharCombustorExcessAir As Double = 0.2

        ''' <summary>Heat loss fraction of the char combustor (0..0.2). Default 0.03.</summary>
        Public Property CharCombustorHeatLoss As Double = 0.03

        ' -------- RESULT PROPERTIES --------

        Public Property Result_OilYield_wfrac As Double = 0.0
        Public Property Result_GasYield_wfrac As Double = 0.0
        Public Property Result_CharYield_wfrac As Double = 0.0
        Public Property Result_UnreactedSolid_wfrac As Double = 0.0
        Public Property Result_OutletTemperature_K As Double = 0.0
        Public Property Result_VaporResidenceTime_s As Double = 0.0
        Public Property Result_SandCirculation_kgps As Double = 0.0
        Public Property Result_SandOutletTemperature_K As Double = 0.0
        Public Property Result_PyrolysisDuty_kW As Double = 0.0
        Public Property Result_CombustorDuty_kW As Double = 0.0
        Public Property Result_CombustorAirFlow_kgps As Double = 0.0
        Public Property Result_CombustorFlueT_K As Double = 0.0

        ''' <summary>Last axial trajectory (not persisted - recomputed on each Calculate).</summary>
        <Xml.Serialization.XmlIgnore> <Newtonsoft.Json.JsonIgnore>
        Public Property LastTrajectory As CFBPyrolysisTrajectoryResult

        <NonSerialized> <Xml.Serialization.XmlIgnore> Public f As Object

        Public Overrides ReadOnly Property SupportsDynamicMode As Boolean = False

        Public Overrides ReadOnly Property MobileCompatible As Boolean
            Get
                Return False
            End Get
        End Property

        Public Sub New()
            MyBase.New()
        End Sub

        Public Sub New(ByVal name As String, ByVal description As String)
            MyBase.New()
            Me.ComponentName = name
            Me.ComponentDescription = description
        End Sub

        Public Overrides Function CloneXML() As Object
            Dim obj As ICustomXMLSerialization = New Reactor_CFBFastPyrolysis()
            obj.LoadData(Me.SaveData)
            Return obj
        End Function

        Public Overrides Function CloneJSON() As Object
            Return Newtonsoft.Json.JsonConvert.DeserializeObject(Of Reactor_CFBFastPyrolysis)(Newtonsoft.Json.JsonConvert.SerializeObject(Me))
        End Function

        ' ------------------------------------------------------------
        '                         CALCULATE
        ' ------------------------------------------------------------

        Public Overrides Sub Calculate(Optional ByVal args As Object = Nothing)

            If Not Me.GraphicObject.InputConnectors(0).IsAttached Then _
                Throw New Exception("CFB Fast Pyrolysis: biomass inlet not connected.")
            If Not Me.GraphicObject.OutputConnectors(0).IsAttached Then _
                Throw New Exception("CFB Fast Pyrolysis: product outlet not connected.")

            If String.IsNullOrEmpty(BiomassCompound) Then _
                Throw New Exception("CFB Fast Pyrolysis: biomass compound role not set.")
            If RiserHeight_m <= 0.0 OrElse RiserDiameter_m <= 0.0 Then _
                Throw New Exception("CFB Fast Pyrolysis: riser height/diameter must be positive.")
            If NumAxialCells < 5 Then NumAxialCells = 5
            If NumAxialCells > 500 Then NumAxialCells = 500

            ' ----- Clone inlet, compute feed mass flows -----
            Dim ims As MaterialStream =
                DirectCast(FlowSheet.SimulationObjects(Me.GraphicObject.InputConnectors(0).AttachedConnector.AttachedFrom.Name), MaterialStream).Clone
            ims.SetFlowsheet(Me.FlowSheet)
            ims.SetPropertyPackage(PropertyPackage)
            PropertyPackage.CurrentMaterialStream = ims
            ims.DefinedFlow = FlowSpec.Mass

            Dim T_in As Double = ims.Phases(0).Properties.temperature.GetValueOrDefault
            Dim P0 As Double = ims.Phases(0).Properties.pressure.GetValueOrDefault
            Dim P As Double = P0 - DeltaP.GetValueOrDefault
            ims.Phases(0).Properties.pressure = P

            Dim compounds = ims.Phases(0).Compounds
            If Not compounds.ContainsKey(BiomassCompound) Then _
                Throw New Exception("CFB Fast Pyrolysis: biomass compound '" & BiomassCompound & "' not in stream.")

            Dim m_biomass As Double = compounds(BiomassCompound).MassFlow.GetValueOrDefault  ' kg/s
            If m_biomass <= 0.0 Then _
                Throw New Exception("CFB Fast Pyrolysis: biomass mass flow at inlet is zero.")

            ' ----- Normalise composition fractions -----
            Dim sumFrac As Double = CelluloseMassFrac + HemicelluloseMassFrac + LigninMassFrac
            If sumFrac <= 0.0 Then sumFrac = 1.0
            Dim wCell = CelluloseMassFrac / sumFrac
            Dim wHemi = HemicelluloseMassFrac / sumFrac
            Dim wLig = LigninMassFrac / sumFrac

            Dim reactions = RanziKinetics.GetDefaultReactions()

            ' ----- Sand-circulation: iterate if InternalCharCombustor -----
            Dim sandRatio As Double = Max(1.0, SandToBiomassRatio)
            Dim T_sand As Double = Max(500.0, SandInletTemperature_K)

            Dim traj As CFBPyrolysisTrajectoryResult = Nothing
            Dim wOut(RanziKinetics.NSpecies - 1) As Double

            If SandMode = CFBSandMode.InternalCharCombustor Then
                ' Iterate sand ratio so combustor_duty * (1-loss) == pyrolysis_duty + riser_losses
                Dim maxIter As Integer = 25
                Dim tol As Double = 0.005   ' 0.5 % closure
                Dim iter As Integer = 0
                Dim ratioLo As Double = 3.0
                Dim ratioHi As Double = 80.0
                sandRatio = Max(ratioLo, Min(ratioHi, sandRatio))
                Do
                    traj = SolveRiser(m_biomass, T_in, wCell, wHemi, wLig,
                                      reactions, sandRatio, T_sand, wOut)
                    ' Char combustor closure
                    Dim mChar = m_biomass * traj.OutletYield_Char
                    Dim Qcomb_available = mChar * CharLHV_Jkg * (1.0 - CharCombustorHeatLoss)  ' W
                    Dim Qneed = traj.NetPyrolysisDuty_kW * 1000.0 / (1.0 - HeatLossFraction)
                    If Qneed <= 0.0 Then Exit Do
                    Dim ratio_closure = Qcomb_available / Qneed
                    If Abs(ratio_closure - 1.0) < tol Then Exit Do
                    ' Adjust sand ratio: more sand â†’ higher duty delivered â†’ less needed per kg char
                    ' Simpler: adjust sand ratio to reach required sand_dT for given char supply.
                    Dim dT_sand_target = Qcomb_available / (sandRatio * m_biomass * BedMaterialCp_JkgK)
                    If dT_sand_target > 200.0 Then dT_sand_target = 200.0
                    If dT_sand_target < 30.0 Then dT_sand_target = 30.0
                    ' New guess: scale sandRatio by closure error
                    If ratio_closure > 1.0 Then
                        sandRatio = sandRatio / (1.0 + 0.5 * (ratio_closure - 1.0))
                    Else
                        sandRatio = sandRatio * (1.0 + 0.5 * (1.0 - ratio_closure))
                    End If
                    sandRatio = Max(ratioLo, Min(ratioHi, sandRatio))
                    iter += 1
                    If iter >= maxIter Then Exit Do
                Loop
            Else
                traj = SolveRiser(m_biomass, T_in, wCell, wHemi, wLig,
                                  reactions, sandRatio, T_sand, wOut)
            End If

            ' ----- Char combustor sizing (if active) -----
            Dim mChar_out = m_biomass * traj.OutletYield_Char
            If SandMode = CFBSandMode.InternalCharCombustor Then
                traj.InternalCharCombustor = True
                ' Stoichiometric O2 for CH (approximation): char â‰ˆ CH0.5O0.2 â†’ 1.13 kg O2 / kg char
                Dim O2stoich_kg = mChar_out * 1.13
                Dim airStoich = O2stoich_kg / 0.232      ' 23.2 wt% O2 in air
                Dim airActual = airStoich * (1.0 + CharCombustorExcessAir)
                traj.CharCombustorAirFlow_kgps = airActual
                Dim Qcomb = mChar_out * CharLHV_Jkg * (1.0 - CharCombustorHeatLoss)
                traj.CharCombustorDuty_kW = Qcomb / 1000.0
                ' Adiabatic flame T estimate: Qcomb = (airActual + mChar) * cp_flue * (T_flue - T_in_air)
                Dim cp_flue = 1100.0     ' J/kg/K
                Dim T_flue = 298.15 + Qcomb / Max(1.0, (airActual + mChar_out) * cp_flue)
                If T_flue > 2200.0 Then T_flue = 2200.0
                traj.CharCombustorFlueT_K = T_flue
            End If

            LastTrajectory = traj

            ' ----- Push Results -----
            Result_OilYield_wfrac = traj.OutletYield_Oil
            Result_GasYield_wfrac = traj.OutletYield_Gas
            Result_CharYield_wfrac = traj.OutletYield_Char
            Result_UnreactedSolid_wfrac = traj.OutletYield_UnreactedSolid
            Result_OutletTemperature_K = traj.OutletTemperature_K
            Result_VaporResidenceTime_s = traj.OutletVaporResidenceTime_s
            Result_SandCirculation_kgps = traj.RequiredSandCirculation_kgps
            Result_SandOutletTemperature_K = traj.SandOutletTemperature_K
            Result_PyrolysisDuty_kW = traj.NetPyrolysisDuty_kW
            Result_CombustorDuty_kW = traj.CharCombustorDuty_kW
            Result_CombustorAirFlow_kgps = traj.CharCombustorAirFlow_kgps
            Result_CombustorFlueT_K = traj.CharCombustorFlueT_K

            ' ----- Outlet stream: map species yields onto the assigned compounds -----
            Dim newMass As New Dictionary(Of String, Double)
            For Each kvp In compounds
                newMass(kvp.Key) = kvp.Value.MassFlow.GetValueOrDefault
            Next
            ' Zero biomass consumed
            newMass(BiomassCompound) = Max(0.0, newMass(BiomassCompound) * traj.OutletYield_UnreactedSolid)
            Dim mYield = m_biomass * (1.0 - traj.OutletYield_UnreactedSolid)
            ' m_biomass was already counted in the inlet totals - we only rebalance that mass
            ' across {unreacted biomass, char, oil, gas}. The delta from biomass to products is:
            Dim biomassConsumed = m_biomass * (1.0 - traj.OutletYield_UnreactedSolid)
            newMass(BiomassCompound) = m_biomass * traj.OutletYield_UnreactedSolid
            If Not String.IsNullOrEmpty(CharCompound) AndAlso newMass.ContainsKey(CharCompound) Then _
                newMass(CharCompound) += m_biomass * traj.OutletYield_Char
            If Not String.IsNullOrEmpty(BioOilCompound) AndAlso newMass.ContainsKey(BioOilCompound) Then _
                newMass(BioOilCompound) += m_biomass * traj.OutletYield_Oil
            If Not String.IsNullOrEmpty(GasLumpCompound) AndAlso newMass.ContainsKey(GasLumpCompound) Then _
                newMass(GasLumpCompound) += m_biomass * traj.OutletYield_Gas

            Dim totalNewMass As Double = 0.0
            For Each v In newMass.Values : totalNewMass += v : Next
            If totalNewMass <= 0 Then totalNewMass = ims.Phases(0).Properties.massflow.GetValueOrDefault

            For Each comp In compounds.Values
                comp.MassFraction = newMass(comp.Name) / totalNewMass
            Next
            Dim invMWsum As Double = 0.0
            For Each comp In compounds.Values
                invMWsum += comp.MassFraction.GetValueOrDefault / comp.ConstantProperties.Molar_Weight
            Next
            If invMWsum > 0 Then
                For Each comp In compounds.Values
                    comp.MoleFraction = (comp.MassFraction.GetValueOrDefault / comp.ConstantProperties.Molar_Weight) / invMWsum
                Next
            End If
            ims.Phases(0).Properties.massflow = totalNewMass
            ims.Phases(0).Properties.temperature = traj.OutletTemperature_K
            ims.Phases(0).Properties.pressure = P
            ims.DefinedFlow = FlowSpec.Mass
            ims.SpecType = StreamSpec.Temperature_and_Pressure

            ' Push to outlet
            Dim cp = Me.GraphicObject.OutputConnectors(0)
            If cp.IsAttached Then
                Dim ms_out As MaterialStream = FlowSheet.SimulationObjects(cp.AttachedConnector.AttachedTo.Name)
                With ms_out
                    .ClearAllProps()
                    .Phases(0).Properties.temperature = traj.OutletTemperature_K
                    .Phases(0).Properties.pressure = P
                    For Each c In .Phases(0).Compounds.Values
                        If ims.Phases(0).Compounds.ContainsKey(c.Name) Then
                            c.MassFraction = ims.Phases(0).Compounds(c.Name).MassFraction
                            c.MoleFraction = ims.Phases(0).Compounds(c.Name).MoleFraction
                        End If
                    Next
                    .Phases(0).Properties.massflow = totalNewMass
                    .DefinedFlow = FlowSpec.Mass
                    .SpecType = StreamSpec.Temperature_and_Pressure
                End With
            End If

            ' Energy connector: report net external duty required
            ' If InternalCharCombustor, nominally zero (autothermal); else = pyrolysis duty
            Dim ec = Me.GraphicObject.EnergyConnector
            If ec.IsAttached Then
                Dim es As EnergyStream = FlowSheet.SimulationObjects(ec.AttachedConnector.AttachedTo.Name)
                If SandMode = CFBSandMode.InternalCharCombustor Then
                    es.EnergyFlow = 0.0
                Else
                    es.EnergyFlow = traj.NetPyrolysisDuty_kW
                End If
                es.GraphicObject.Calculated = True
            End If

        End Sub

        ''' <summary>
        ''' Solve the 1-D axial riser PFR: march mass fractions and temperature from
        ''' inlet to outlet over NumAxialCells cells, using the Ranzi kinetic scheme.
        ''' Sand is treated as an isothermal co-current hot stream that releases heat
        ''' into the reacting mixture via a lumped UA proxy (realised as a local
        ''' energy balance per cell). Returns a fully populated trajectory.
        ''' </summary>
        Private Function SolveRiser(m_biomass As Double, T_in As Double,
                                     wCell As Double, wHemi As Double, wLig As Double,
                                     reactions As List(Of PyroReaction),
                                     sandRatio As Double, T_sand_in As Double,
                                     ByRef wOut() As Double) As CFBPyrolysisTrajectoryResult

            Dim traj As New CFBPyrolysisTrajectoryResult()
            Dim N As Integer = NumAxialCells
            Dim dz = RiserHeight_m / N
            Dim A_riser = PI * (RiserDiameter_m / 2.0) ^ 2

            ' Composition vector (mass fractions over reacting mixture: solid + vapors)
            Dim w = RanziKinetics.InitialComposition(wCell, wHemi, wLig)
            Dim T = Max(T_in, 450.0)  ' Â°K, biomass preheats quickly in contact with sand

            Dim m_sand = sandRatio * m_biomass
            Dim T_sand = T_sand_in
            Dim cpSand = BedMaterialCp_JkgK
            Dim cpMix = 1500.0  ' J/kg/K effective (solid biomass cp ~1500, vapors ~1800)

            ' Approximate gas superficial velocity - increases along bed as vapors evolve
            Dim u_g0 = Max(0.5, CarrierGasVelocity_ms)
            Dim vaporTau As Double = 0.0

            ' Add inlet sample (z=0)
            AppendSample(traj, 0.0, T, w, vaporTau, u_g0, u_g0, SolidsHoldup)
            For i = 0 To RanziKinetics.NSpecies - 1
                Dim key = RanziKinetics.SpeciesName(CType(i, PyroSpecies))
                If Not traj.Species.ContainsKey(key) Then traj.Species.Add(key, New List(Of Double))
            Next
            ' Record inlet species snapshot
            RecordSpeciesRow(traj, w)

            Dim Q_total As Double = 0.0

            For iCell As Integer = 1 To N
                ' Local gas fraction: evolving vapors + carrier - approx from sum of gas species mass
                Dim wGas = w(CInt(PyroSpecies.BIO_OIL)) + w(CInt(PyroSpecies.GAS))
                Dim u_g = u_g0 * (1.0 + 2.0 * wGas)     ' heuristic expansion factor
                Dim dt_cell = dz / Max(0.1, u_g)
                vaporTau += dt_cell

                ' --- Sub-step Ranzi ODEs with explicit RK2 over dt_cell ---
                Dim nSub = Max(5, CInt(dt_cell / 0.02) + 1)
                Dim h = dt_cell / nSub
                Dim dwdt() As Double = Nothing
                Dim qRxn As Double = 0.0
                Dim QcellTotal As Double = 0.0

                For j = 1 To nSub
                    RanziKinetics.EvaluateRates(w, T, reactions, dwdt, qRxn)
                    ' RK2 (midpoint)
                    Dim wMid(w.Length - 1) As Double
                    For k = 0 To w.Length - 1 : wMid(k) = w(k) + 0.5 * h * dwdt(k) : Next
                    Dim dwdt_mid() As Double = Nothing
                    Dim qRxn_mid As Double = 0.0
                    RanziKinetics.EvaluateRates(wMid, T, reactions, dwdt_mid, qRxn_mid)
                    For k = 0 To w.Length - 1 : w(k) = Max(0.0, w(k) + h * dwdt_mid(k)) : Next

                    ' Energy balance for the sub-step (per kg of reacting mixture, rate W/kg)
                    ' Mixture gets heat from sand, loses heat to reactions
                    ' Hot-sand â†’ mixture: dT_mix/dt = (m_sand*cpSand*(T_sand-T) * UA_frac - qRxn_abs) / (m_biomass*cpMix)
                    ' Simplified: assume complete thermal contact per cell â†’ Î”T approach with approach=0.3
                    Dim Thermal_approach = 0.3   ' 30 % of driving force closed per cell
                    Dim dT_from_sand = (T_sand - T) * Thermal_approach / nSub
                    ' Convert qRxn (W/kg of mixture) to Î”T per kg: divide by cp
                    Dim dT_from_rxn = qRxn_mid * h / cpMix
                    Dim dT = dT_from_sand + dT_from_rxn
                    T += dT
                    ' Sand loses matching heat (biomass/sand exchange)
                    Dim Qexch = m_biomass * cpMix * dT_from_sand   ' W
                    T_sand -= Qexch / Max(1.0, m_sand * cpSand)
                    QcellTotal += Qexch
                Next
                Q_total += QcellTotal

                Dim wGas2 = w(CInt(PyroSpecies.BIO_OIL)) + w(CInt(PyroSpecies.GAS))
                Dim u_g_new = u_g0 * (1.0 + 2.0 * wGas2)
                AppendSample(traj, iCell * dz, T, w, vaporTau, u_g_new, u_g_new, SolidsHoldup)
                RecordSpeciesRow(traj, w)
            Next

            ' Normalise: if the sum of species &gt; 1 from numerical drift, rescale
            Dim wSum As Double = 0.0
            For i = 0 To w.Length - 1 : wSum += w(i) : Next
            If wSum > 0.0 Then
                For i = 0 To w.Length - 1 : w(i) /= wSum : Next
            End If

            ' ----- Summary yields -----
            traj.OutletYield_Oil = w(CInt(PyroSpecies.BIO_OIL))
            traj.OutletYield_Gas = w(CInt(PyroSpecies.GAS))
            traj.OutletYield_Char = w(CInt(PyroSpecies.CHAR_S))
            traj.OutletYield_UnreactedSolid = w(CInt(PyroSpecies.CELL)) + w(CInt(PyroSpecies.HCE)) +
                                              w(CInt(PyroSpecies.LIG)) +
                                              w(CInt(PyroSpecies.CELLA)) + w(CInt(PyroSpecies.HCEA)) +
                                              w(CInt(PyroSpecies.LIGA))
            traj.OutletTemperature_K = T
            traj.OutletVaporResidenceTime_s = vaporTau
            traj.RequiredSandCirculation_kgps = m_sand
            traj.SandInletTemperature_K = T_sand_in
            traj.SandOutletTemperature_K = T_sand
            traj.NetPyrolysisDuty_kW = (m_biomass * HeatOfPyrolysis_Jkg) / 1000.0   ' kW

            ReDim wOut(w.Length - 1)
            Array.Copy(w, wOut, w.Length)

            Return traj

        End Function

        Private Sub AppendSample(t As CFBPyrolysisTrajectoryResult, z As Double, T_K As Double,
                                 w() As Double, tauVapor As Double, u_s As Double, u_g As Double, eps_s As Double)
            t.Z_m.Add(z)
            t.T_K.Add(T_K)
            t.VaporResidenceTime_s.Add(tauVapor)
            t.SolidVelocity_ms.Add(u_s)
            t.GasVelocity_ms.Add(u_g)
            t.SolidsHoldup.Add(eps_s)
        End Sub

        Private Sub RecordSpeciesRow(t As CFBPyrolysisTrajectoryResult, w() As Double)
            For i = 0 To w.Length - 1
                Dim key = RanziKinetics.SpeciesName(CType(i, PyroSpecies))
                If Not t.Species.ContainsKey(key) Then t.Species.Add(key, New List(Of Double))
                t.Species(key).Add(w(i))
            Next
        End Sub

        Public Overrides Sub DeCalculate()
            Dim cp = Me.GraphicObject.OutputConnectors(0)
            If cp.IsAttached Then
                Dim ms As MaterialStream = FlowSheet.SimulationObjects(cp.AttachedConnector.AttachedTo.Name)
                With ms
                    .Phases(0).Properties.temperature = Nothing
                    .Phases(0).Properties.pressure = Nothing
                    .Phases(0).Properties.enthalpy = Nothing
                    For Each c In .Phases(0).Compounds.Values
                        c.MoleFraction = 0
                        c.MassFraction = 0
                    Next
                    .Phases(0).Properties.massflow = Nothing
                    .GraphicObject.Calculated = False
                End With
            End If
            LastTrajectory = Nothing
        End Sub

        ' ------------------------------------------------------------
        '                 Identity / Drawing / Edit
        ' ------------------------------------------------------------

        Public Overrides Function GetIconBitmapBytes() As Byte()
            Return UnitOperations.BioOpsDrawHelper.RenderIconToPngBytes(64, 64, AddressOf DrawIcon)
        End Function

        Public Overrides Function GetDisplayDescription() As String
            Return "CFB fast pyrolysis reactor (1-D axial PFR, Ranzi multi-step kinetics, optional char combustor)"
        End Function

        Public Overrides Function GetDisplayName() As String
            Return "CFB Fast Pyrolysis"
        End Function

        Public Overrides Function GetReport(su As IUnitsOfMeasure, ci As Globalization.CultureInfo, numberformat As String) As String
            Dim s As New Text.StringBuilder
            s.AppendLine("CFB Fast Pyrolysis Reactor: " & Me.GraphicObject.Tag)
            s.AppendLine()
            s.AppendLine("Geometry")
            s.AppendLine("  Riser height:   " & RiserHeight_m.ToString(numberformat, ci) & " m")
            s.AppendLine("  Riser diameter: " & RiserDiameter_m.ToString(numberformat, ci) & " m")
            s.AppendLine("  Axial cells:    " & NumAxialCells.ToString())
            s.AppendLine()
            s.AppendLine("Biomass composition (dry)")
            s.AppendLine("  Cellulose:     " & (CelluloseMassFrac * 100).ToString(numberformat, ci) & " %")
            s.AppendLine("  Hemicellulose: " & (HemicelluloseMassFrac * 100).ToString(numberformat, ci) & " %")
            s.AppendLine("  Lignin:        " & (LigninMassFrac * 100).ToString(numberformat, ci) & " %")
            s.AppendLine()
            s.AppendLine("Sand loop")
            s.AppendLine("  Mode:          " & SandMode.ToString())
            s.AppendLine("  Sand/biomass:  " & SandToBiomassRatio.ToString(numberformat, ci) & " kg/kg")
            s.AppendLine("  Sand inlet T:  " & SandInletTemperature_K.ToString(numberformat, ci) & " K")
            s.AppendLine()
            s.AppendLine("Outlet yields (mass fraction of dry biomass)")
            s.AppendLine("  Bio-oil: " & (Result_OilYield_wfrac * 100).ToString(numberformat, ci) & " %")
            s.AppendLine("  Gas:     " & (Result_GasYield_wfrac * 100).ToString(numberformat, ci) & " %")
            s.AppendLine("  Char:    " & (Result_CharYield_wfrac * 100).ToString(numberformat, ci) & " %")
            s.AppendLine("  Unreacted: " & (Result_UnreactedSolid_wfrac * 100).ToString(numberformat, ci) & " %")
            s.AppendLine()
            s.AppendLine("  Outlet T:          " & Result_OutletTemperature_K.ToString(numberformat, ci) & " K")
            s.AppendLine("  Vapor residence:   " & Result_VaporResidenceTime_s.ToString(numberformat, ci) & " s")
            s.AppendLine("  Sand circulation:  " & Result_SandCirculation_kgps.ToString(numberformat, ci) & " kg/s")
            s.AppendLine("  Sand outlet T:     " & Result_SandOutletTemperature_K.ToString(numberformat, ci) & " K")
            s.AppendLine("  Pyrolysis duty:    " & Result_PyrolysisDuty_kW.ToString(numberformat, ci) & " kW")
            If SandMode = CFBSandMode.InternalCharCombustor Then
                s.AppendLine()
                s.AppendLine("Char combustor")
                s.AppendLine("  Duty:       " & Result_CombustorDuty_kW.ToString(numberformat, ci) & " kW")
                s.AppendLine("  Air flow:   " & Result_CombustorAirFlow_kgps.ToString(numberformat, ci) & " kg/s")
                s.AppendLine("  Flue T:     " & Result_CombustorFlueT_K.ToString(numberformat, ci) & " K")
            End If
            Return s.ToString()
        End Function

        ' ------------------------------------------------------------
        '                    Property bridge (RO/WR)
        ' ------------------------------------------------------------

        Private Shared ReadOnly _inputProps As String() = {
            "Riser Height", "Riser Diameter", "Num Axial Cells",
            "Solids Holdup", "Bed Material Density", "Bed Material Cp",
            "Carrier Gas Velocity",
            "Sand Mode", "Sand Inlet Temperature", "Sand To Biomass Ratio", "Heat Loss Fraction",
            "Cellulose Mass Fraction", "Hemicellulose Mass Fraction", "Lignin Mass Fraction",
            "Heat Of Pyrolysis",
            "Biomass Compound", "Char Compound", "BioOil Compound", "Gas Compound",
            "Water Compound", "Oxygen Compound", "CO2 Compound", "Nitrogen Compound",
            "Char LHV", "Char Combustor Excess Air", "Char Combustor Heat Loss"
        }

        Private Shared ReadOnly _outputProps As String() = {
            "Oil Yield", "Gas Yield", "Char Yield", "Unreacted Solid",
            "Outlet Temperature", "Vapor Residence Time",
            "Sand Circulation", "Sand Outlet Temperature", "Pyrolysis Duty",
            "Combustor Duty", "Combustor Air Flow", "Combustor Flue Temperature"
        }

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
                Case "Riser Height" : Return RiserHeight_m
                Case "Riser Diameter" : Return RiserDiameter_m
                Case "Num Axial Cells" : Return NumAxialCells
                Case "Solids Holdup" : Return SolidsHoldup
                Case "Bed Material Density" : Return BedMaterialDensity_kgm3
                Case "Bed Material Cp" : Return BedMaterialCp_JkgK
                Case "Carrier Gas Velocity" : Return CarrierGasVelocity_ms
                Case "Sand Mode" : Return SandMode.ToString()
                Case "Sand Inlet Temperature" : Return SandInletTemperature_K
                Case "Sand To Biomass Ratio" : Return SandToBiomassRatio
                Case "Heat Loss Fraction" : Return HeatLossFraction
                Case "Cellulose Mass Fraction" : Return CelluloseMassFrac
                Case "Hemicellulose Mass Fraction" : Return HemicelluloseMassFrac
                Case "Lignin Mass Fraction" : Return LigninMassFrac
                Case "Heat Of Pyrolysis" : Return HeatOfPyrolysis_Jkg
                Case "Biomass Compound" : Return BiomassCompound
                Case "Char Compound" : Return CharCompound
                Case "BioOil Compound" : Return BioOilCompound
                Case "Gas Compound" : Return GasLumpCompound
                Case "Water Compound" : Return WaterCompound
                Case "Oxygen Compound" : Return OxygenCompound
                Case "CO2 Compound" : Return CO2Compound
                Case "Nitrogen Compound" : Return NitrogenCompound
                Case "Char LHV" : Return CharLHV_Jkg
                Case "Char Combustor Excess Air" : Return CharCombustorExcessAir
                Case "Char Combustor Heat Loss" : Return CharCombustorHeatLoss
                Case "Oil Yield" : Return Result_OilYield_wfrac
                Case "Gas Yield" : Return Result_GasYield_wfrac
                Case "Char Yield" : Return Result_CharYield_wfrac
                Case "Unreacted Solid" : Return Result_UnreactedSolid_wfrac
                Case "Outlet Temperature" : Return Result_OutletTemperature_K
                Case "Vapor Residence Time" : Return Result_VaporResidenceTime_s
                Case "Sand Circulation" : Return Result_SandCirculation_kgps
                Case "Sand Outlet Temperature" : Return Result_SandOutletTemperature_K
                Case "Pyrolysis Duty" : Return Result_PyrolysisDuty_kW
                Case "Combustor Duty" : Return Result_CombustorDuty_kW
                Case "Combustor Air Flow" : Return Result_CombustorAirFlow_kgps
                Case "Combustor Flue Temperature" : Return Result_CombustorFlueT_K
                Case Else : Return MyBase.GetPropertyValue(prop, su)
            End Select
        End Function

        Public Overrides Function GetPropertyUnit(prop As String, Optional su As IUnitsOfMeasure = Nothing) As String
            Select Case prop
                Case "Riser Height", "Riser Diameter" : Return "m"
                Case "Bed Material Density" : Return "kg/m3"
                Case "Bed Material Cp", "Char LHV", "Heat Of Pyrolysis" : Return "J/kg"
                Case "Carrier Gas Velocity" : Return "m/s"
                Case "Sand Inlet Temperature", "Sand Outlet Temperature", "Outlet Temperature",
                     "Combustor Flue Temperature" : Return "K"
                Case "Vapor Residence Time" : Return "s"
                Case "Sand Circulation", "Combustor Air Flow" : Return "kg/s"
                Case "Pyrolysis Duty", "Combustor Duty" : Return "kW"
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
                Case "Riser Height" : RiserHeight_m = d : Return True
                Case "Riser Diameter" : RiserDiameter_m = d : Return True
                Case "Num Axial Cells" : NumAxialCells = CInt(d) : Return True
                Case "Solids Holdup" : SolidsHoldup = d : Return True
                Case "Bed Material Density" : BedMaterialDensity_kgm3 = d : Return True
                Case "Bed Material Cp" : BedMaterialCp_JkgK = d : Return True
                Case "Carrier Gas Velocity" : CarrierGasVelocity_ms = d : Return True
                Case "Sand Mode"
                    Dim m As CFBSandMode
                    If [Enum].TryParse(Of CFBSandMode)(propval?.ToString(), m) Then SandMode = m
                    Return True
                Case "Sand Inlet Temperature" : SandInletTemperature_K = d : Return True
                Case "Sand To Biomass Ratio" : SandToBiomassRatio = d : Return True
                Case "Heat Loss Fraction" : HeatLossFraction = d : Return True
                Case "Cellulose Mass Fraction" : CelluloseMassFrac = d : Return True
                Case "Hemicellulose Mass Fraction" : HemicelluloseMassFrac = d : Return True
                Case "Lignin Mass Fraction" : LigninMassFrac = d : Return True
                Case "Heat Of Pyrolysis" : HeatOfPyrolysis_Jkg = d : Return True
                Case "Biomass Compound" : BiomassCompound = propval?.ToString() : Return True
                Case "Char Compound" : CharCompound = propval?.ToString() : Return True
                Case "BioOil Compound" : BioOilCompound = propval?.ToString() : Return True
                Case "Gas Compound" : GasLumpCompound = propval?.ToString() : Return True
                Case "Water Compound" : WaterCompound = propval?.ToString() : Return True
                Case "Oxygen Compound" : OxygenCompound = propval?.ToString() : Return True
                Case "CO2 Compound" : CO2Compound = propval?.ToString() : Return True
                Case "Nitrogen Compound" : NitrogenCompound = propval?.ToString() : Return True
                Case "Char LHV" : CharLHV_Jkg = d : Return True
                Case "Char Combustor Excess Air" : CharCombustorExcessAir = d : Return True
                Case "Char Combustor Heat Loss" : CharCombustorHeatLoss = d : Return True
                Case Else : Return MyBase.SetPropertyValue(prop, propval, su)
            End Select
        End Function

        ' ------------------------------------------------------------
        '                 IExternalUnitOperation
        ' ------------------------------------------------------------

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
                Return "CFB-"
            End Get
        End Property
        Public Function ReturnInstance(typename As String) As Object Implements IExternalUnitOperation.ReturnInstance
            Return New Reactor_CFBFastPyrolysis()
        End Function
        Public Sub PopulateEditorPanel(ctner As Object) Implements IExternalUnitOperation.PopulateEditorPanel

            If TypeOf ctner Is AvaloniaEditorPanel Then PopulateEditorPanelAvalonia(DirectCast(ctner, AvaloniaEditorPanel)) : Return
        End Sub

        Private Sub PopulateEditorPanelAvalonia(container As AvaloniaEditorPanel)

            Dim nf = FlowSheet.FlowsheetOptions.NumberFormat
            Dim compIds = FlowSheet.SelectedCompounds.Values.Select(Function(c) c.Name).ToList()

            container.CreateAndAddLabelRow("Riser Geometry")

            container.CreateAndAddTextBoxRow(nf, "Riser Height (m)", RiserHeight_m,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     RiserHeight_m = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Riser Diameter (m)", RiserDiameter_m,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     RiserDiameter_m = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Number of Axial Cells", NumAxialCells,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     NumAxialCells = CInt(tb.Text.ParseExpressionToDouble())
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Carrier Gas Superficial Velocity (m/s)", CarrierGasVelocity_ms,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     CarrierGasVelocity_ms = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddLabelRow("Bed Material (Sand)")

            container.CreateAndAddTextBoxRow(nf, "Solids Holdup (-)", SolidsHoldup,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     SolidsHoldup = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Bed Material Density (kg/m3)", BedMaterialDensity_kgm3,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     BedMaterialDensity_kgm3 = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Bed Material Cp (J/kgÂ·K)", BedMaterialCp_JkgK,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     BedMaterialCp_JkgK = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddLabelRow("Sand Circulation Mode")

            container.CreateAndAddDropDownRow("Mode",
                                              New List(Of String)({"External (user-specified)", "Char Combustor (coupled, autothermal)"}),
                                              CInt(SandMode),
                                              Sub(dd, e)
                                                  SandMode = CType(dd.SelectedIndex, CFBSandMode)
                                                  FlowSheet.RequestCalculation()
                                              End Sub)

            container.CreateAndAddTextBoxRow(nf, "Sand Inlet Temperature (K)", SandInletTemperature_K,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     SandInletTemperature_K = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Sand / Biomass Ratio", SandToBiomassRatio,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     SandToBiomassRatio = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Heat Loss Fraction (riser)", HeatLossFraction,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     HeatLossFraction = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddLabelRow("Biomass Composition (mass frac, dry ash-free)")

            container.CreateAndAddTextBoxRow(nf, "Cellulose", CelluloseMassFrac,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     CelluloseMassFrac = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Hemicellulose", HemicelluloseMassFrac,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     HemicelluloseMassFrac = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Lignin", LigninMassFrac,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     LigninMassFrac = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Heat of Pyrolysis (J/kg)", HeatOfPyrolysis_Jkg,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     HeatOfPyrolysis_Jkg = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddLabelRow("Char Combustor (autothermal mode only)")

            container.CreateAndAddTextBoxRow(nf, "Char LHV (J/kg)", CharLHV_Jkg,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     CharLHV_Jkg = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Excess Air Fraction", CharCombustorExcessAir,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     CharCombustorExcessAir = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Combustor Heat Loss Fraction", CharCombustorHeatLoss,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     CharCombustorHeatLoss = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddLabelRow("Compound Mapping")

            Dim addCompoundDropdown =
                Sub(label As String, currentValue As String, setter As Action(Of String))
                    Dim idx = compIds.IndexOf(currentValue)
                    container.CreateAndAddDropDownRow(label,
                                                      New List(Of String)(New String() {"(none)"}.Concat(compIds)),
                                                      If(idx < 0, 0, idx + 1),
                                                      Sub(dd, e)
                                                          setter(If(dd.SelectedIndex > 0, compIds(dd.SelectedIndex - 1), ""))
                                                          FlowSheet.RequestCalculation()
                                                      End Sub)
                End Sub

            addCompoundDropdown("Biomass", BiomassCompound, Sub(v) BiomassCompound = v)
            addCompoundDropdown("Char", CharCompound, Sub(v) CharCompound = v)
            addCompoundDropdown("Bio-oil", BioOilCompound, Sub(v) BioOilCompound = v)
            addCompoundDropdown("Gas Lump", GasLumpCompound, Sub(v) GasLumpCompound = v)
            addCompoundDropdown("Water", WaterCompound, Sub(v) WaterCompound = v)
            addCompoundDropdown("Oxygen", OxygenCompound, Sub(v) OxygenCompound = v)
            addCompoundDropdown("Carbon Dioxide", CO2Compound, Sub(v) CO2Compound = v)
            addCompoundDropdown("Nitrogen", NitrogenCompound, Sub(v) NitrogenCompound = v)

        End Sub

        Public Sub CreateConnectors() Implements IExternalUnitOperation.CreateConnectors
            If GraphicObject Is Nothing Then Return
            Dim w = GraphicObject.Width, h = GraphicObject.Height
            Dim gx = GraphicObject.X, gy = GraphicObject.Y
            If GraphicObject.InputConnectors.Count = 1 AndAlso GraphicObject.OutputConnectors.Count = 1 Then
                GraphicObject.InputConnectors(0).Position = New Point(gx, gy + 0.75 * h)
                GraphicObject.InputConnectors(0).ConnectorName = "Biomass Feed"
                GraphicObject.OutputConnectors(0).Position = New Point(gx + w, gy + 0.2 * h)
                GraphicObject.OutputConnectors(0).ConnectorName = "Products (char + bio-oil vapors + gas)"
            Else
                GraphicObject.InputConnectors.Clear()
                GraphicObject.OutputConnectors.Clear()
                GraphicObject.InputConnectors.Add(New ConnectionPoint With {
                    .Position = New Point(gx, gy + 0.75 * h), .Type = ConType.ConIn,
                    .Direction = ConDir.Right, .ConnectorName = "Biomass Feed"})
                GraphicObject.OutputConnectors.Add(New ConnectionPoint With {
                    .Position = New Point(gx + w, gy + 0.2 * h), .Type = ConType.ConOut,
                    .Direction = ConDir.Right, .ConnectorName = "Products"})
            End If
            GraphicObject.EnergyConnector.Position = New Point(gx + 0.5 * w, gy + h)
            GraphicObject.EnergyConnector.Direction = ConDir.Up
            GraphicObject.EnergyConnector.Active = True
        End Sub

        <NonSerialized> <Xml.Serialization.XmlIgnore> Private _photoImage As SKImage

        Public Sub Draw(g As Object) Implements IExternalUnitOperation.Draw
            If GraphicObject Is Nothing Then Return
            Dim canvas As SKCanvas = DirectCast(g, SKCanvas)
            If GraphicObject.DrawMode = 2 Then
                If UnitOperations.BioOpsDrawHelper.TryDrawPhotorealistic(canvas,
                    GraphicObject.X, GraphicObject.Y, GraphicObject.Width, GraphicObject.Height,
                    "cfb_fast_pyrolysis_photo", _photoImage) Then Return
            End If
            DrawIcon(canvas, CSng(GraphicObject.X), CSng(GraphicObject.Y),
                     CSng(GraphicObject.Width), CSng(GraphicObject.Height),
                     GraphicObject.DrawMode = 1)
        End Sub

        ''' <summary>
        ''' Vector icon: a tall riser column with cyclone at top, a downcomer returning sand
        ''' to a smaller regenerator / char combustor vessel at the left, a biomass inlet at
        ''' the riser base and a product vapors/char outlet at the cyclone exit.
        ''' </summary>
        Private Shared Sub DrawIcon(canvas As SKCanvas, gx As Single, gy As Single, w As Single, h As Single, Optional mono As Boolean = False)

            ' Riser (tall column, right side)
            Dim riser As New SKRect(gx + 0.58F * w, gy + 0.15F * h, gx + 0.75F * w, gy + 0.95F * h)
            UnitOperations.BioOpsDrawHelper.DrawVerticalTank(canvas, riser, mono)

            ' Cyclone at top of riser
            Dim cyclone As New SKRect(gx + 0.5F * w, gy + 0.02F * h, gx + 0.82F * w, gy + 0.22F * h)
            Using body As New SKPaint With {.Color = UnitOperations.BioOpsDrawHelper.ClrMetalLight(mono), .IsAntialias = True}
                Dim path As New SKPath()
                path.MoveTo(cyclone.Left, cyclone.Top)
                path.LineTo(cyclone.Right, cyclone.Top)
                path.LineTo(cyclone.Right, gy + 0.14F * h)
                path.LineTo(gx + 0.66F * w, cyclone.Bottom)
                path.LineTo(cyclone.Left, gy + 0.14F * h)
                path.Close()
                canvas.DrawPath(path, body)
            End Using
            Using stroke As New SKPaint With {.Color = UnitOperations.BioOpsDrawHelper.ClrStroke(mono), .Style = SKPaintStyle.Stroke, .StrokeWidth = 1.0F, .IsAntialias = True}
                Dim path As New SKPath()
                path.MoveTo(cyclone.Left, cyclone.Top)
                path.LineTo(cyclone.Right, cyclone.Top)
                path.LineTo(cyclone.Right, gy + 0.14F * h)
                path.LineTo(gx + 0.66F * w, cyclone.Bottom)
                path.LineTo(cyclone.Left, gy + 0.14F * h)
                path.Close()
                canvas.DrawPath(path, stroke)
            End Using

            ' Char combustor / regenerator (smaller column, left side)
            Dim regen As New SKRect(gx + 0.15F * w, gy + 0.30F * h, gx + 0.35F * w, gy + 0.88F * h)
            UnitOperations.BioOpsDrawHelper.DrawVerticalTank(canvas, regen, mono)

            ' Sand transfer line: cyclone â†’ downcomer â†’ regen
            UnitOperations.BioOpsDrawHelper.DrawPipe(canvas, New SKPoint(cyclone.Left + 2, gy + 0.18F * h),
                                                    New SKPoint(gx + 0.25F * w, gy + 0.30F * h), 0.02F * w, mono)
            ' Hot-sand return line: regen top â†’ riser base
            UnitOperations.BioOpsDrawHelper.DrawPipe(canvas, New SKPoint(gx + 0.35F * w, gy + 0.35F * h),
                                                    New SKPoint(gx + 0.58F * w, gy + 0.85F * h), 0.02F * w, mono)

            ' Biomass feed nozzle at riser base
            UnitOperations.BioOpsDrawHelper.DrawPipe(canvas, New SKPoint(gx, gy + 0.75F * h),
                                                    New SKPoint(gx + 0.58F * w, gy + 0.75F * h), 0.03F * w, mono)

            ' Product vapors/char outlet from cyclone top-right
            UnitOperations.BioOpsDrawHelper.DrawPipe(canvas, New SKPoint(gx + 0.82F * w, gy + 0.05F * h),
                                                    New SKPoint(gx + w, gy + 0.2F * h), 0.03F * w, mono)

            ' Air inlet to char combustor (bottom of regen)
            UnitOperations.BioOpsDrawHelper.DrawPipe(canvas, New SKPoint(gx + 0.10F * w, gy + 0.92F * h),
                                                    New SKPoint(gx + 0.25F * w, gy + 0.88F * h), 0.02F * w, mono)

            ' Flue gas outlet from regen top
            UnitOperations.BioOpsDrawHelper.DrawPipe(canvas, New SKPoint(gx + 0.25F * w, gy + 0.30F * h),
                                                    New SKPoint(gx + 0.25F * w, gy + 0.15F * h), 0.02F * w, mono)

            ' Flange accents
            UnitOperations.BioOpsDrawHelper.DrawFlange(canvas, gx + 0.58F * w, gy + 0.75F * h, 0.08F * w, mono)
            UnitOperations.BioOpsDrawHelper.DrawFlange(canvas, gx + 0.25F * w, gy + 0.88F * h, 0.06F * w, mono)

        End Sub

    End Class

End Namespace
