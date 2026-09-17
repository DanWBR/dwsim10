'    DWSIM AI Assistant Extension
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

Imports System.Collections.Generic
Imports System.Linq
Imports DWSIM.Automation.FluentAPI.Diagnostics
Imports DWSIM.Interfaces
Imports Newtonsoft.Json.Linq

''' <summary>
''' Renders the flowsheet diagnostics for the assistant's HTTP surface.
''' </summary>
''' <remarks>
''' The rules live in the engine, shared with the MCP server, so both surfaces report the same
''' codes and the same fixes. This is only the rendering.
''' </remarks>
Public Class FlowsheetChecks

    ''' <summary>Findings past this many are counted rather than listed.</summary>
    Public Const MaxItems As Integer = 25

    ''' <summary>What is wrong with the flowsheet as it stands, without solving it.</summary>
    Public Shared Function Check(fs As IFlowsheet) As JObject

        Dim findings = FlowsheetDiagnostics.Check(fs)

        Dim result = Report(findings)
        result("object_count") = fs.SimulationObjects.Count
        result("compound_count") = fs.SelectedCompounds.Count
        Return result

    End Function

    ''' <summary>Why a solve failed, or left objects unconverged.</summary>
    Public Shared Function Diagnose(fs As IFlowsheet, errors As IEnumerable(Of Exception)) As JObject

        Return Report(FlowsheetDiagnostics.Diagnose(fs, errors))

    End Function

    ''' <summary>The findings alone, worst first, for embedding in another response.</summary>
    Public Shared Function FindingsArray(findings As IEnumerable(Of Finding)) As JArray

        Dim array As New JArray()

        For Each finding In findings.Take(MaxItems)
            array.Add(New JObject() From {
                {"code", finding.Code},
                {"severity", finding.Severity.ToString().ToLowerInvariant()},
                {"object", finding.ObjectTag},
                {"message", finding.Message},
                {"fix", finding.Fix},
                {"learn_more", FindingExplanations.LearnMoreUrl(finding.Code)}
            })
        Next

        Return array

    End Function

    ''' <summary>The written explanation of one code: meaning, why, how to fix, where to read.</summary>
    Public Shared Function Explanation(code As String) As JObject

        Dim e = FindingExplanations.For(code)
        Return New JObject() From {
            {"code", e.Code},
            {"title", e.Title},
            {"meaning", e.Meaning},
            {"why", e.Why},
            {"how_to_fix", e.HowToFix},
            {"learn_more", FindingExplanations.LearnMoreUrl(e.Code)}
        }

    End Function

    ''' <summary>The explanations of every distinct code among the findings.</summary>
    Public Shared Function Explanations(findings As IEnumerable(Of Finding)) As JArray

        Dim array As New JArray()
        For Each code In findings.Select(Function(f) f.Code).Distinct()
            array.Add(Explanation(code))
        Next
        Return array

    End Function

    ''' <summary>The degrees of freedom of the flowsheet, objects with holes first.</summary>
    Public Shared Function DegreesOfFreedom(fs As IFlowsheet) As JObject

        Dim dof = DegreesOfFreedomAnalysis.Analyze(fs)
        Dim objects As New JArray()
        For Each o In dof.Objects
            objects.Add(DegreesOfFreedom(o))
        Next
        Return New JObject() From {
            {"fully_specified", dof.IsFullySpecified},
            {"remaining", dof.Remaining},
            {"unsupported", dof.Unsupported},
            {"objects", objects}
        }

    End Function

    ''' <summary>The degrees of freedom of one object.</summary>
    Public Shared Function DegreesOfFreedom(dof As ObjectDegreesOfFreedom) As JObject

        Dim slots As New JArray()
        For Each slot In dof.Slots
            Dim item As New JObject() From {
                {"name", slot.Name},
                {"property", slot.Property},
                {"required", slot.Required},
                {"set", slot.IsSet}
            }
            If slot.Value.HasValue Then item("value") = slot.Value.Value
            If Not String.IsNullOrEmpty(slot.Units) Then item("units") = slot.Units
            If Not String.IsNullOrEmpty(slot.Note) Then item("note") = slot.Note
            slots.Add(item)
        Next

        Dim result As New JObject() From {
            {"object", dof.ObjectTag},
            {"type", dof.ObjectType},
            {"mode", dof.Mode},
            {"supported", dof.Supported},
            {"required", dof.Required},
            {"specified", dof.Specified},
            {"remaining", dof.Remaining},
            {"missing", New JArray(dof.Missing.Select(Function(s) s.Name).ToArray())},
            {"slots", slots}
        }
        If Not String.IsNullOrEmpty(dof.Note) Then result("note") = dof.Note
        Return result

    End Function

    Private Shared Function Report(findings As IReadOnlyList(Of Finding)) As JObject

        Dim blockers = findings.Where(Function(f) f.Severity = DiagnosticSeverity.Blocker).Count()
        Dim warnings = findings.Where(Function(f) f.Severity = DiagnosticSeverity.Warning).Count()

        Dim result As New JObject() From {
            {"ready", blockers = 0},
            {"blockers", blockers},
            {"warnings", warnings},
            {"findings", FindingsArray(findings)},
            {"explanations", Explanations(findings.Take(MaxItems))}
        }

        If findings.Count > MaxItems Then
            result("truncated") = True
            result("total") = findings.Count
        End If

        Return result

    End Function

End Class
