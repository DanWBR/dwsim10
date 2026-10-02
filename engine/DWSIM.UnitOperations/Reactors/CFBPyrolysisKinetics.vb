'    CFB Fast Pyrolysis - Ranzi Multi-Step Kinetic Scheme
'    The CRECK-2014 scheme of the Ranzi group (Corbetta et al. 2013/2014) for lignocellulosic
'    biomass fast pyrolysis, reduced to three pseudo-components (cellulose, hemicellulose,
'    lignin) going through activated intermediates to primary vapors, non-condensable gas
'    and char, plus secondary vapor cracking.
'    Copyright 2026 Daniel Wagner O. de Medeiros
'
'    This file is part of DWSIM.
'
'    DWSIM is free software: you can redistribute it and/or modify it under the terms
'    of the GNU General Public License as published by the Free Software Foundation,
'    either version 3 of the License, or (at your option) any later version.

Imports System
Imports System.Math

Namespace Reactors.CFBPyrolysis

    ''' <summary>
    ''' Species index for the reduced Ranzi pyrolysis scheme. The reactor marches
    ''' mass-fraction arrays indexed by these enum values.
    ''' </summary>
    Public Enum PyroSpecies As Integer
        ''' <summary>Native cellulose (solid reactant).</summary>
        CELL = 0
        ''' <summary>Activated cellulose (solid intermediate).</summary>
        CELLA = 1
        ''' <summary>Native hemicellulose (solid reactant).</summary>
        HCE = 2
        ''' <summary>Activated hemicellulose, HCE1 of the CRECK scheme (solid intermediate).</summary>
        HCEA = 3
        ''' <summary>Native lignin (solid reactant).</summary>
        LIG = 4
        ''' <summary>Activated lignin, LIGOH of the CRECK scheme (solid residue that leaves with the char).</summary>
        LIGA = 5
        ''' <summary>Char (solid final product).</summary>
        CHAR_S = 6
        ''' <summary>Condensable primary vapors and reaction water - bio-oil (total liquid) lump (gas phase).</summary>
        BIO_OIL = 7
        ''' <summary>Non-condensable gas lump - CO/CO2/CH4/H2/C2H4 (gas phase).</summary>
        GAS = 8
    End Enum

    ''' <summary>
    ''' A single Arrhenius first-order reaction in the reduced Ranzi scheme.
    ''' Rate r = A * exp(-E/(R*T)) * mass_fraction_of_reactant.
    ''' Enthalpy DH &lt; 0 for exothermic, &gt; 0 for endothermic. Units are SI
    ''' (A in 1/s, E in J/mol, DH in J/kg of reactant consumed).
    ''' </summary>
    Public Class PyroReaction
        Public Property Name As String
        Public Property Reactant As PyroSpecies
        Public Property ProductYields As Dictionary(Of PyroSpecies, Double)
        Public Property A As Double                 ' 1/s
        Public Property Ea_JmolK As Double          ' J/mol
        Public Property DH_Jkg As Double = 0.0      ' J per kg of reactant consumed (+ endothermic)
    End Class

    ''' <summary>
    ''' Multi-step kinetic scheme for lignocellulose fast pyrolysis: the CRECK-2014 scheme
    ''' (Corbetta, Pierucci, Ranzi et al., Energy Fuels 28 (2014) 3884) reduced to nine species.
    ''' Cellulose, hemicellulose and lignin go through activated intermediates to primary vapors,
    ''' gas and char; primary vapors are then cracked to non-condensable gas at high temperature
    ''' and vapor residence time.
    ''' </summary>
    Public Module RanziKinetics

        Public Const R_JmolK As Double = 8.314462618

        ''' <summary>Total number of species tracked.</summary>
        Public ReadOnly NSpecies As Integer = [Enum].GetValues(GetType(PyroSpecies)).Length

        ''' <summary>
        ''' Return the canonical list of reactions in the reduced Ranzi scheme.
        ''' Kinetic parameters are first-order on the reactant's mass fraction within the
        ''' reacting mixture (solid biomass + vapors co-flowing with sand).
        ''' </summary>
        Public Function GetDefaultReactions() As List(Of PyroReaction)

            Dim L As New List(Of PyroReaction)

            ' Reduced CRECK-2014 primary pyrolysis scheme: Corbetta, Pierucci, Ranzi, Bennadji, Fisher (2013),
            ' XXXVI Meeting of the Italian Section of the Combustion Institute, Table 1 (published as Corbetta
            ' et al., Energy Fuels 28 (2014) 3884). Lumps: char, the trapped G{} species and LIGCC -> CHAR_S;
            ' organics and reaction water -> BIO_OIL; CO, CO2, H2, CH4, C2H4 -> GAS. HCEA = HCE1, LIGA = LIGOH.
            ' A*T pre-exponentials are folded at 773 K. Lignin split LIG-C/H/O = 6/7/9 (hardwood, Anca-Couce 2017).
            ' DH is the H0R of the same table, J per kg of reactant (+ endothermic).

            ' --- CELLULOSE ---
            L.Add(New PyroReaction() With {.Name = "CELL_activation", .Reactant = PyroSpecies.CELL,
                .ProductYields = New Dictionary(Of PyroSpecies, Double)() From {{PyroSpecies.CELLA, 1.0}},
                .A = 40000000000000.0, .Ea_JmolK = 188280.0, .DH_Jkg = 0.0})
            L.Add(New PyroReaction() With {.Name = "CELLA_fragmentation", .Reactant = PyroSpecies.CELLA,
                .ProductYields = New Dictionary(Of PyroSpecies, Double)() From {
                    {PyroSpecies.BIO_OIL, 0.8605}, {PyroSpecies.GAS, 0.0948}, {PyroSpecies.CHAR_S, 0.0447}},
                .A = 500000000.0, .Ea_JmolK = 121336.0, .DH_Jkg = 620000.0})
            L.Add(New PyroReaction() With {.Name = "CELLA_to_levoglucosan", .Reactant = PyroSpecies.CELLA,
                .ProductYields = New Dictionary(Of PyroSpecies, Double)() From {{PyroSpecies.BIO_OIL, 1.0}},
                .A = 3783.0, .Ea_JmolK = 48268.3, .DH_Jkg = 364000.0})          ' 1.8*T exp(-10 kcal/RT)
            L.Add(New PyroReaction() With {.Name = "CELL_to_char_water", .Reactant = PyroSpecies.CELL,
                .ProductYields = New Dictionary(Of PyroSpecies, Double)() From {
                    {PyroSpecies.CHAR_S, 0.4445}, {PyroSpecies.BIO_OIL, 0.5555}},
                .A = 40000000.0, .Ea_JmolK = 129704.0, .DH_Jkg = -1913000.0})

            ' --- HEMICELLULOSE ---
            L.Add(New PyroReaction() With {.Name = "HCE_activation", .Reactant = PyroSpecies.HCE,
                .ProductYields = New Dictionary(Of PyroSpecies, Double)() From {
                    {PyroSpecies.HCEA, 0.4}, {PyroSpecies.CHAR_S, 0.3576}, {PyroSpecies.BIO_OIL, 0.1652}, {PyroSpecies.GAS, 0.0772}},
                .A = 3300000000.0, .Ea_JmolK = 129704.0, .DH_Jkg = 227200.0})   ' R5 + 0.6 x R9 (HCE2 decomposes with HCE)
            L.Add(New PyroReaction() With {.Name = "HCEA_to_gas_char", .Reactant = PyroSpecies.HCEA,
                .ProductYields = New Dictionary(Of PyroSpecies, Double)() From {
                    {PyroSpecies.GAS, 0.3256}, {PyroSpecies.CHAR_S, 0.4126}, {PyroSpecies.BIO_OIL, 0.2618}},
                .A = 1000000000.0, .Ea_JmolK = 133888.0, .DH_Jkg = -92000.0})
            L.Add(New PyroReaction() With {.Name = "HCEA_to_char", .Reactant = PyroSpecies.HCEA,
                .ProductYields = New Dictionary(Of PyroSpecies, Double)() From {
                    {PyroSpecies.CHAR_S, 0.7183}, {PyroSpecies.GAS, 0.2302}, {PyroSpecies.BIO_OIL, 0.0515}},
                .A = 105.08, .Ea_JmolK = 39900.3, .DH_Jkg = -1860000.0})        ' 0.05*T exp(-8 kcal/RT)
            L.Add(New PyroReaction() With {.Name = "HCEA_to_xylan", .Reactant = PyroSpecies.HCEA,
                .ProductYields = New Dictionary(Of PyroSpecies, Double)() From {{PyroSpecies.BIO_OIL, 1.0}},
                .A = 1891.5, .Ea_JmolK = 52452.3, .DH_Jkg = 588000.0})          ' 0.9*T exp(-11 kcal/RT)

            ' --- LIGNIN (LIG-C, LIG-H and LIG-O lumped) ---
            L.Add(New PyroReaction() With {.Name = "LIG_activation", .Reactant = PyroSpecies.LIG,
                .ProductYields = New Dictionary(Of PyroSpecies, Double)() From {
                    {PyroSpecies.LIGA, 0.6423}, {PyroSpecies.CHAR_S, 0.2414}, {PyroSpecies.BIO_OIL, 0.0947}, {PyroSpecies.GAS, 0.0216}},
                .A = 138880000000.0, .Ea_JmolK = 143390.1, .DH_Jkg = 80636.0})
            L.Add(New PyroReaction() With {.Name = "LIGA_to_oil", .Reactant = PyroSpecies.LIGA,
                .ProductYields = New Dictionary(Of PyroSpecies, Double)() From {
                    {PyroSpecies.BIO_OIL, 0.6415}, {PyroSpecies.CHAR_S, 0.3262}, {PyroSpecies.GAS, 0.0323}},
                .A = 76317.0, .Ea_JmolK = 91713.2, .DH_Jkg = 257492.0})         ' LIGOH -> LIG -> FE2MACR
            L.Add(New PyroReaction() With {.Name = "LIGA_to_gas_oil_char", .Reactant = PyroSpecies.LIGA,
                .ProductYields = New Dictionary(Of PyroSpecies, Double)() From {
                    {PyroSpecies.CHAR_S, 0.6389}, {PyroSpecies.BIO_OIL, 0.2463}, {PyroSpecies.GAS, 0.1148}},
                .A = 6060000000.0, .Ea_JmolK = 160615.9, .DH_Jkg = -378631.0})
            L.Add(New PyroReaction() With {.Name = "LIGA_to_char_gas", .Reactant = PyroSpecies.LIGA,
                .ProductYields = New Dictionary(Of PyroSpecies, Double)() From {
                    {PyroSpecies.CHAR_S, 0.778}, {PyroSpecies.BIO_OIL, 0.1516}, {PyroSpecies.GAS, 0.0704}},
                .A = 2639.3, .Ea_JmolK = 74977.2, .DH_Jkg = -1035114.0})
            L.Add(New PyroReaction() With {.Name = "LIGA_to_char", .Reactant = PyroSpecies.LIGA,
                .ProductYields = New Dictionary(Of PyroSpecies, Double)() From {
                    {PyroSpecies.CHAR_S, 0.8873}, {PyroSpecies.BIO_OIL, 0.0714}, {PyroSpecies.GAS, 0.0413}},
                .A = 33.0, .Ea_JmolK = 62760.0, .DH_Jkg = -1604000.0})

            ' --- SECONDARY VAPOR CRACKING (gas-phase) ---
            ' 10. BIO_OIL -> GAS
            L.Add(New PyroReaction() With {
                .Name = "oil_cracking",
                .Reactant = PyroSpecies.BIO_OIL,
                .ProductYields = New Dictionary(Of PyroSpecies, Double)() From {{PyroSpecies.GAS, 1.0}},
                .A = 43000.0,                              ' 4.3e4 1/s (Di Blasi 1996)
                .Ea_JmolK = 108000.0,
                .DH_Jkg = 50000.0
            })

            Return L

        End Function

        ''' <summary>
        ''' Compute the instantaneous rate of change of each species mass fraction (1/s)
        ''' given the current mass-fraction vector w(0..NSpecies-1) and absolute temperature T (K).
        ''' Reactions are first-order on the reactant's mass fraction. Caller supplies the
        ''' cached reaction list (so it is built once per run).
        ''' </summary>
        Public Sub EvaluateRates(w() As Double, T As Double,
                                 reactions As List(Of PyroReaction),
                                 ByRef dwdt() As Double,
                                 ByRef qRxn_Wkg As Double,
                                 Optional solidTimeFactor As Double = 1.0)

            Dim n As Integer = w.Length
            If dwdt Is Nothing OrElse dwdt.Length <> n Then ReDim dwdt(n - 1)
            For i = 0 To n - 1 : dwdt(i) = 0.0 : Next
            qRxn_Wkg = 0.0

            If T <= 273.15 Then Return

            Dim invRT As Double = 1.0 / (R_JmolK * T)

            For Each rxn In reactions
                Dim wi As Double = w(CInt(rxn.Reactant))
                If wi <= 0.0 Then Continue For
                Dim k As Double = rxn.A * Exp(-rxn.Ea_JmolK * invRT)
                ' the solids stay solidTimeFactor times longer than the vapors in each cell
                If IsSolid(rxn.Reactant) Then k *= solidTimeFactor
                Dim r As Double = k * wi    ' mass-fraction/second consumption
                dwdt(CInt(rxn.Reactant)) -= r
                For Each kv In rxn.ProductYields
                    dwdt(CInt(kv.Key)) += r * kv.Value
                Next
                ' Heat release (W/kg of mixture): +DH endothermic  →  consumes heat → negative q
                qRxn_Wkg -= r * rxn.DH_Jkg
            Next

        End Sub

        ''' <summary>True for the solid species (native and activated biomass, char).</summary>
        Public Function IsSolid(s As PyroSpecies) As Boolean
            Return s <> PyroSpecies.BIO_OIL AndAlso s <> PyroSpecies.GAS
        End Function

        ''' <summary>
        ''' Map the three bulk mass-fraction inputs (cellulose, hemicellulose, lignin, dry basis)
        ''' into the full 9-species initial-composition vector. Moisture is assumed to have
        ''' already been removed by a drying block upstream. Char, intermediates and vapors
        ''' start at zero.
        ''' </summary>
        Public Function InitialComposition(wCell As Double, wHemi As Double, wLig As Double) As Double()

            Dim w(NSpecies - 1) As Double
            Dim total = wCell + wHemi + wLig
            If total <= 0.0 Then total = 1.0
            w(CInt(PyroSpecies.CELL)) = wCell / total
            w(CInt(PyroSpecies.HCE)) = wHemi / total
            w(CInt(PyroSpecies.LIG)) = wLig / total
            Return w

        End Function

        ''' <summary>
        ''' Return the human-readable name of a species (used by the results CSV header and
        ''' chart legends).
        ''' </summary>
        Public Function SpeciesName(s As PyroSpecies) As String
            Select Case s
                Case PyroSpecies.CELL : Return "Cellulose"
                Case PyroSpecies.CELLA : Return "CelluloseActive"
                Case PyroSpecies.HCE : Return "Hemicellulose"
                Case PyroSpecies.HCEA : Return "HemicelluloseActive"
                Case PyroSpecies.LIG : Return "Lignin"
                Case PyroSpecies.LIGA : Return "LigninActive"
                Case PyroSpecies.CHAR_S : Return "Char"
                Case PyroSpecies.BIO_OIL : Return "BioOil"
                Case PyroSpecies.GAS : Return "Gas"
                Case Else : Return s.ToString()
            End Select
        End Function

    End Module

End Namespace
