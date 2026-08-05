'    CFB Fast Pyrolysis - Ranzi Multi-Step Kinetic Scheme
'    Simplified Ranzi et al. (2008) scheme for lignocellulosic biomass fast pyrolysis,
'    expressed as three pseudo-components (cellulose, hemicellulose, lignin) going
'    through activated intermediates to primary vapors, non-condensable gas and char,
'    plus secondary vapor cracking.
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
        ''' <summary>Activated hemicellulose (solid intermediate).</summary>
        HCEA = 3
        ''' <summary>Native lignin (solid reactant).</summary>
        LIG = 4
        ''' <summary>Activated lignin (solid intermediate).</summary>
        LIGA = 5
        ''' <summary>Char (solid final product).</summary>
        CHAR_S = 6
        ''' <summary>Condensable primary vapors - bio-oil lump (gas phase).</summary>
        BIO_OIL = 7
        ''' <summary>Non-condensable gas lump - CO/CO2/CH4/H2/H2O (gas phase).</summary>
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
    ''' Reduced Ranzi (2008) multi-step kinetic scheme for lignocellulose fast pyrolysis.
    ''' Three pseudo-components (cellulose, hemicellulose, lignin) each follow a two-branch
    ''' activation → {primary vapor vs. char+gas} path; primary vapors are then cracked to
    ''' non-condensable gas at high temperature and vapor residence time.
    ''' Parameters reflect the compact scheme used in Anca-Couce (2016) and Debiagi et al.
    ''' (2018) biomass pyrolysis reviews, consistent with Ranzi et al. (2008).
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

            ' --- CELLULOSE branch ---
            ' 1. CELL -> CELLA  (activation)
            L.Add(New PyroReaction() With {
                .Name = "CELL_activation",
                .Reactant = PyroSpecies.CELL,
                .ProductYields = New Dictionary(Of PyroSpecies, Double)() From {{PyroSpecies.CELLA, 1.0}},
                .A = 0.00000000000004 * 0.0000000000000000000000000000000001 ' placeholder override below
            })
            ' Use explicit values to avoid precision loss in VB long literals
            L(L.Count - 1).A = 40000000000000000.0         ' 4e16 1/s (Ranzi)
            L(L.Count - 1).Ea_JmolK = 198000.0             ' 198 kJ/mol
            L(L.Count - 1).DH_Jkg = 0.0

            ' 2. CELLA -> BIO_OIL (primary vapors, high-T branch)
            L.Add(New PyroReaction() With {
                .Name = "CELLA_to_oil",
                .Reactant = PyroSpecies.CELLA,
                .ProductYields = New Dictionary(Of PyroSpecies, Double)() From {{PyroSpecies.BIO_OIL, 1.0}},
                .A = 3300000000.0,                         ' 3.3e9 1/s
                .Ea_JmolK = 125000.0,
                .DH_Jkg = 255000.0                         ' +255 kJ/kg (endothermic)
            })

            ' 3. CELLA -> 0.35 CHAR + 0.65 GAS (low-T/char branch)
            L.Add(New PyroReaction() With {
                .Name = "CELLA_to_char_gas",
                .Reactant = PyroSpecies.CELLA,
                .ProductYields = New Dictionary(Of PyroSpecies, Double)() From {
                    {PyroSpecies.CHAR_S, 0.35}, {PyroSpecies.GAS, 0.65}},
                .A = 1300000000.0,                         ' 1.3e9 1/s
                .Ea_JmolK = 150000.0,
                .DH_Jkg = -20000.0                         ' -20 kJ/kg (mildly exothermic char formation)
            })

            ' --- HEMICELLULOSE branch ---
            ' 4. HCE -> HCEA
            L.Add(New PyroReaction() With {
                .Name = "HCE_activation",
                .Reactant = PyroSpecies.HCE,
                .ProductYields = New Dictionary(Of PyroSpecies, Double)() From {{PyroSpecies.HCEA, 1.0}},
                .A = 10000000000.0,                        ' 1e10 1/s
                .Ea_JmolK = 129000.0,
                .DH_Jkg = 0.0
            })

            ' 5. HCEA -> BIO_OIL
            L.Add(New PyroReaction() With {
                .Name = "HCEA_to_oil",
                .Reactant = PyroSpecies.HCEA,
                .ProductYields = New Dictionary(Of PyroSpecies, Double)() From {{PyroSpecies.BIO_OIL, 1.0}},
                .A = 3000000000.0,                         ' 3e9 1/s
                .Ea_JmolK = 113000.0,
                .DH_Jkg = 190000.0
            })

            ' 6. HCEA -> 0.40 CHAR + 0.60 GAS
            L.Add(New PyroReaction() With {
                .Name = "HCEA_to_char_gas",
                .Reactant = PyroSpecies.HCEA,
                .ProductYields = New Dictionary(Of PyroSpecies, Double)() From {
                    {PyroSpecies.CHAR_S, 0.4}, {PyroSpecies.GAS, 0.6}},
                .A = 1000000000.0,                         ' 1e9 1/s
                .Ea_JmolK = 130000.0,
                .DH_Jkg = -30000.0
            })

            ' --- LIGNIN branch (aggregated LIG-C/LIG-O/LIG-H) ---
            ' 7. LIG -> LIGA
            L.Add(New PyroReaction() With {
                .Name = "LIG_activation",
                .Reactant = PyroSpecies.LIG,
                .ProductYields = New Dictionary(Of PyroSpecies, Double)() From {{PyroSpecies.LIGA, 1.0}},
                .A = 1000000000.0,                         ' 1e9 1/s
                .Ea_JmolK = 108000.0,
                .DH_Jkg = 0.0
            })

            ' 8. LIGA -> 0.7 BIO_OIL + 0.3 CHAR
            L.Add(New PyroReaction() With {
                .Name = "LIGA_to_oil_char",
                .Reactant = PyroSpecies.LIGA,
                .ProductYields = New Dictionary(Of PyroSpecies, Double)() From {
                    {PyroSpecies.BIO_OIL, 0.7}, {PyroSpecies.CHAR_S, 0.3}},
                .A = 100000000.0,                          ' 1e8 1/s
                .Ea_JmolK = 125000.0,
                .DH_Jkg = 150000.0
            })

            ' 9. LIGA -> GAS (slow alternative channel for high-T)
            L.Add(New PyroReaction() With {
                .Name = "LIGA_to_gas",
                .Reactant = PyroSpecies.LIGA,
                .ProductYields = New Dictionary(Of PyroSpecies, Double)() From {{PyroSpecies.GAS, 1.0}},
                .A = 30000000.0,                           ' 3e7 1/s
                .Ea_JmolK = 125000.0,
                .DH_Jkg = 100000.0
            })

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
                                 ByRef qRxn_Wkg As Double)

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
                Dim r As Double = k * wi    ' mass-fraction/second consumption
                dwdt(CInt(rxn.Reactant)) -= r
                For Each kv In rxn.ProductYields
                    dwdt(CInt(kv.Key)) += r * kv.Value
                Next
                ' Heat release (W/kg of mixture): +DH endothermic  →  consumes heat → negative q
                qRxn_Wkg -= r * rxn.DH_Jkg
            Next

        End Sub

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
