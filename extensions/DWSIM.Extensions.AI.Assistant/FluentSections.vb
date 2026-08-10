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

Imports System.Reflection
Imports DWSIM.Interfaces
Imports DWSIM.Automation.FluentAPI
Imports FAPI = DWSIM.Automation.FluentAPI

''' <summary>
''' FluentAPI-backed factory for the assistant <c>/api/add-section</c> endpoint.
''' Exposes ~50 unit operations (typed free + Plus-gated refining, electrolyte,
''' advanced HX, ExtensionPack and clean-energy) on top of the legacy section
''' templates already implemented in <see cref="Server.CreateSection"/>.
'''
''' <para>
''' For each known <c>section_type</c>, the factory looks up the matching
''' <c>Flowsheet.AddXxx(tag)</c> method on <see cref="FAPI.Flowsheet"/> via
''' reflection, invokes it, casts the resulting builder's underlying object to
''' <see cref="ISimulationObject"/>, and creates the standard side streams
''' (<c>feed_in_N</c> / <c>product_out_N</c>) so the section behaves like the
''' legacy templates from the LLM's point of view.
''' </para>
''' </summary>
Public Class FluentSections

    Public Class SectionSpec
        ''' <summary>FluentAPI method name on <see cref="FAPI.Flowsheet"/> (e.g. "AddValve").</summary>
        Public AddMethod As String
        Public NumFeeds As Integer = 1
        Public NumProducts As Integer = 1
        Public NumEnergyFeeds As Integer = 0
        Public NumEnergyProducts As Integer = 0
        ''' <summary>True when activation via <see cref="License.IsActivated"/> is required.</summary>
        Public RequiresPlus As Boolean = False
    End Class

    ' ── Dispatch table ─────────────────────────────────────────────────────────
    ' Each entry's port spec is the most common contract for that UO. The
    ' factory still creates streams even when the underlying solver later
    ' rejects them - letting the LLM fix mismatches via dwsim_modify_unit /
    ' dwsim_remove_section instead of failing section creation outright.
    Public Shared ReadOnly Specs As Dictionary(Of String, SectionSpec) = BuildSpecs()

    Private Shared Function BuildSpecs() As Dictionary(Of String, SectionSpec)
        Dim d As New Dictionary(Of String, SectionSpec)(StringComparer.OrdinalIgnoreCase)
        Add(d, "valve",                       "AddValve",                       1, 1, 0, 0, False)
        Add(d, "pipe",                        "AddPipe",                        1, 1, 0, 1, False)
        Add(d, "tank",                        "AddTank",                        1, 1, 0, 0, False)
        Add(d, "orifice_plate",               "AddOrificePlate",                1, 1, 0, 0, False)
        Add(d, "filter",                      "AddFilter",                      1, 2, 0, 0, False)
        Add(d, "solids_separator",            "AddSolidsSeparator",             1, 2, 0, 0, False)
        Add(d, "shortcut_column",             "AddShortcutColumn",              1, 2, 1, 1, False)
        Add(d, "gibbs_reactor",               "AddGibbsReactor",                1, 1, 1, 0, False)
        Add(d, "reaktoro_gibbs",              "AddReaktoroGibbsReactor",        1, 1, 1, 0, False)
        ' Clean energy (free, typed)
        Add(d, "wind_turbine",                "AddWindTurbine",                 0, 0, 0, 1, False)
        Add(d, "hydroelectric_turbine",       "AddHydroelectricTurbine",        1, 1, 0, 1, False)
        Add(d, "solar_panel",                 "AddSolarPanel",                  0, 0, 0, 1, False)
        Add(d, "water_electrolyzer",          "AddWaterElectrolyzer",           1, 2, 1, 0, False)
        Add(d, "pem_fuel_cell",               "AddPEMFuelCell",                 2, 1, 0, 1, False)
        ' Bioprocess (free external)
        Add(d, "bio_reactor",                 "AddBioReactor",                  1, 1, 1, 0, False)
        Add(d, "anaerobic_digester",          "AddAnaerobicDigester",           1, 2, 1, 0, False)
        Add(d, "cfb_pyrolysis",               "AddCFBFastPyrolysisReactor",     1, 2, 1, 0, False)
        Add(d, "pretreatment_reactor",        "AddPretreatmentReactor",         1, 1, 1, 0, False)
        Add(d, "biogas_upgrader",             "AddBiogasUpgrader",              1, 2, 0, 0, False)
        Add(d, "cell_lysis",                  "AddCellLysis",                   1, 1, 0, 0, False)
        Add(d, "centrifuge",                  "AddCentrifuge",                  1, 2, 0, 0, False)
        Add(d, "chromatography_column",       "AddChromatographyColumn",        1, 2, 0, 0, False)
        Add(d, "crossflow_uf",                "AddCrossflowUF",                 1, 2, 0, 0, False)
        Add(d, "crystallizer",                "AddCrystallizer",                1, 2, 1, 0, False)
        ' Refining (Plus, gated)
        Add(d, "alkylation",                  "AddAlkylation",                  2, 2, 0, 0, True)
        Add(d, "amine_treater",               "AddAmineTreater",                2, 2, 1, 0, True)
        Add(d, "blender",                     "AddBlender",                     4, 1, 0, 0, True)
        Add(d, "claus_sru",                   "AddClausSRU",                    2, 2, 0, 0, True)
        Add(d, "coker",                       "AddCoker",                       1, 4, 1, 0, True)
        Add(d, "fcc",                         "AddFCC",                         1, 4, 0, 0, True)
        Add(d, "hydrocracker",                "AddHydrocracker",                2, 3, 1, 0, True)
        Add(d, "hds",                         "AddHDS",                         2, 2, 1, 0, True)
        Add(d, "isomerization",               "AddIsomerization",               1, 1, 1, 0, True)
        Add(d, "reformer",                    "AddReformer",                    1, 1, 1, 0, True)
        Add(d, "shortcut_cdu",                "AddShortcutCDU",                 1, 5, 1, 1, True)
        ' Electrolyte (Plus, gated)
        Add(d, "ion_exchange",                "AddIonExchange",                 1, 1, 0, 0, True)
        Add(d, "neutralization_reactor",      "AddNeutralizationReactor",       2, 1, 0, 0, True)
        Add(d, "precipitation_reactor",       "AddPrecipitationReactor",        2, 2, 0, 0, True)
        Add(d, "reverse_osmosis",             "AddReverseOsmosis",              1, 2, 0, 0, True)
        ' Advanced Plus UOs (gated)
        Add(d, "advanced_heat_exchanger",     "AddAdvancedHeatExchanger",       2, 2, 0, 0, True)
        Add(d, "fired_heater",                "AddFiredHeater",                 2, 2, 0, 1, True)
        Add(d, "restriction_orifice",         "AddRestrictionOrifice",          1, 1, 0, 0, True)
        Add(d, "pipe_network",                "AddPipeNetwork",                 1, 1, 0, 0, True)
        Add(d, "vapor_compression_chiller",   "AddVaporCompressionChiller",     1, 2, 1, 0, True)
        Add(d, "zeolite_adsorber",            "AddZeoliteAdsorber",             1, 2, 0, 0, True)
        Add(d, "copper_bed_mercury_adsorber", "AddCopperBedMercuryAdsorber",    1, 1, 0, 0, True)
        ' ExtensionPack Plus UOs (gated)
        Add(d, "air_cooler2",                 "AddAirCooler2",                  1, 1, 0, 0, True)
        Add(d, "energy_mixer",                "AddEnergyMixer",                 0, 0, 4, 1, True)
        Add(d, "energy_splitter",             "AddEnergySplitter",              0, 0, 1, 4, True)
        Add(d, "energy_stream_switch",        "AddEnergyStreamSwitch",          0, 0, 2, 2, True)
        Add(d, "material_stream_switch",      "AddMaterialStreamSwitch",        2, 2, 0, 0, True)
        Add(d, "material_stream_mapper",      "AddMaterialStreamMapper",        1, 1, 0, 0, True)
        Add(d, "falling_film_evaporator",     "AddFallingFilmEvaporator",       1, 2, 1, 0, True)
        Add(d, "thermo_property_editor",      "AddThermoPropertyEditor",        1, 1, 0, 0, False)
        Return d
    End Function

    Private Shared Sub Add(d As Dictionary(Of String, SectionSpec),
                           sectionType As String, addMethod As String,
                           feeds As Integer, products As Integer,
                           energyFeeds As Integer, energyProducts As Integer,
                           requiresPlus As Boolean)
        Dim s As New SectionSpec()
        s.AddMethod = addMethod
        s.NumFeeds = feeds
        s.NumProducts = products
        s.NumEnergyFeeds = energyFeeds
        s.NumEnergyProducts = energyProducts
        s.RequiresPlus = requiresPlus
        d(sectionType) = s
    End Sub

    ''' <summary>List of section types this dispatcher handles. Used by the LLM tool catalog.</summary>
    Public Shared ReadOnly Property KnownSectionTypes As IReadOnlyList(Of String)
        Get
            Return Specs.Keys.OrderBy(Function(k) k).ToList()
        End Get
    End Property

    ''' <summary>Returns True when the section type is handled by this dispatcher.</summary>
    Public Shared Function IsHandled(sectionType As String) As Boolean
        Return sectionType IsNot Nothing AndAlso Specs.ContainsKey(sectionType)
    End Function

    ''' <summary>
    ''' Builds the section on the live flowsheet. Returns the same shape as
    ''' <see cref="Server.CreateSection"/>: a dictionary with keys "type",
    ''' "objects" (List(Of String)) and "ports" (Dict(Of String, String)).
    ''' Throws when <see cref="License.IsActivated"/> is false for a Plus type.
    ''' </summary>
    Public Shared Function BuildSection(sim As IFlowsheet, sectionType As String, sectionId As String, paramsJson As String) As Dictionary(Of String, Object)

        Dim spec As SectionSpec = Nothing
        If Not Specs.TryGetValue(sectionType, spec) Then
            Throw New InvalidOperationException("Unknown FluentAPI section type '" & sectionType & "'.")
        End If

        If spec.RequiresPlus AndAlso Not License.IsActivated Then
            Throw New InvalidOperationException(
                "Section type '" & sectionType & "' requires an active DWSIM Patron / Plus license. " &
                "Activate via the Settings panel or use a free section type.")
        End If

        Dim fs = FAPI.Flowsheet.Wrap(sim)

        ' Reflectively invoke fs.AddXxx(sectionId)
        Dim mi = GetType(FAPI.Flowsheet).GetMethod(spec.AddMethod, BindingFlags.Public Or BindingFlags.Instance, Nothing, New Type() {GetType(String)}, Nothing)
        If mi Is Nothing Then
            Throw New MissingMethodException(
                "FluentAPI method '" & spec.AddMethod & "(string)' not found on Flowsheet - " &
                "FluentAPI version mismatch.")
        End If

        Dim builder = mi.Invoke(fs, New Object() {sectionId})
        If builder Is Nothing Then
            Throw New InvalidOperationException("FluentAPI builder for '" & sectionType & "' returned null.")
        End If

        ' Extract underlying ISimulationObject from builder.Object
        Dim objProp = builder.GetType().GetProperty("Object", BindingFlags.Public Or BindingFlags.Instance)
        If objProp Is Nothing Then
            Throw New InvalidOperationException(
                "FluentAPI builder " & builder.GetType().Name & " has no 'Object' property.")
        End If
        Dim uoObj = TryCast(objProp.GetValue(builder), ISimulationObject)
        If uoObj Is Nothing Then
            Throw New InvalidOperationException(
                "FluentAPI builder " & builder.GetType().Name & ".Object did not implement ISimulationObject.")
        End If

        ' Assemble the section info dict (mirrors legacy CreateSection shape)
        Dim objects As New List(Of String) From {sectionId}
        Dim ports As New Dictionary(Of String, String)
        Dim info As New Dictionary(Of String, Object)
        info("type") = sectionType
        info("objects") = objects
        info("ports") = ports

        ' Wire feeds, products, energy streams using the standard naming so the
        ' LLM and the connect-sections endpoint can find them.
        For i = 0 To spec.NumFeeds - 1
            Dim sName = sectionId & "_feed_in_" & i
            Dim s = sim.AddObject(Enums.GraphicObjects.ObjectType.MaterialStream, 80, 80 + i * 60, sName)
            objects.Add(sName)
            uoObj.ConnectFeedMaterialStream(s, i)
            ports("feed_in_" & i) = sName
            If spec.NumFeeds = 1 Then ports("feed_in") = sName
        Next
        For i = 0 To spec.NumProducts - 1
            Dim sName = sectionId & "_product_out_" & i
            Dim s = sim.AddObject(Enums.GraphicObjects.ObjectType.MaterialStream, 320, 80 + i * 60, sName)
            objects.Add(sName)
            uoObj.ConnectProductMaterialStream(s, i)
            ports("product_out_" & i) = sName
            If spec.NumProducts = 1 Then ports("product_out") = sName
        Next
        For i = 0 To spec.NumEnergyFeeds - 1
            Dim sName = sectionId & "_energy_in_" & i
            Dim s = sim.AddObject(Enums.GraphicObjects.ObjectType.EnergyStream, 200, 200 + i * 60, sName)
            objects.Add(sName)
            uoObj.ConnectFeedEnergyStream(s, i)
            ports("energy_in_" & i) = sName
            If spec.NumEnergyFeeds = 1 Then ports("energy_in") = sName
        Next
        For i = 0 To spec.NumEnergyProducts - 1
            Dim sName = sectionId & "_energy_out_" & i
            Dim s = sim.AddObject(Enums.GraphicObjects.ObjectType.EnergyStream, 400, 200 + i * 60, sName)
            objects.Add(sName)
            uoObj.ConnectProductEnergyStream(s, i)
            ports("energy_out_" & i) = sName
            If spec.NumEnergyProducts = 1 Then ports("energy_out") = sName
        Next

        Return info
    End Function

End Class
