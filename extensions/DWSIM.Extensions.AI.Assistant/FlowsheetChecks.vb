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
                {"fix", finding.Fix}
            })
        Next

        Return array

    End Function

    Private Shared Function Report(findings As IReadOnlyList(Of Finding)) As JObject

        Dim blockers = findings.Where(Function(f) f.Severity = DiagnosticSeverity.Blocker).Count()
        Dim warnings = findings.Where(Function(f) f.Severity = DiagnosticSeverity.Warning).Count()

        Dim result As New JObject() From {
            {"ready", blockers = 0},
            {"blockers", blockers},
            {"warnings", warnings},
            {"findings", FindingsArray(findings)}
        }

        If findings.Count > MaxItems Then
            result("truncated") = True
            result("total") = findings.Count
        End If

        Return result

    End Function

End Class
