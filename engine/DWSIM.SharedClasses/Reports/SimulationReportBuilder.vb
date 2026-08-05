'    Copyright 2008-2026 Daniel Wagner O. de Medeiros
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

Imports System.Data
Imports System.Text
Imports DWSIM.Interfaces
Imports DWSIM.Interfaces.Enums.GraphicObjects

Namespace Reports

    ''' <summary>
    ''' Options that drive a simulation report. Mirrors the selection UI of the classic
    ''' report configuration form (objects to include + per-phase toggles). When passed as
    ''' Nothing, the builder defaults to "everything": all simulation objects and all phases.
    ''' </summary>
    Public Class ReportOptions

        ''' <summary>Ordered list of object names (ISimulationObject.Name) to include. Nothing/empty = all objects.</summary>
        Public Property ObjectNames As List(Of String) = Nothing

        Public Property IncludeConditions As Boolean = True
        Public Property IncludeCompositions As Boolean = True
        Public Property IncludeMixtureProps As Boolean = True
        Public Property IncludeVaporProps As Boolean = True
        Public Property IncludeLiquidMixtureProps As Boolean = True
        Public Property IncludeLiquid1Props As Boolean = True
        Public Property IncludeLiquid2Props As Boolean = True
        Public Property IncludeAqueousProps As Boolean = True
        Public Property IncludeSolidProps As Boolean = True

        ''' <summary>
        ''' Resolver that turns a property key (e.g. "PROP_MS_0") into a display name.
        ''' When Nothing, the flowsheet's GetTranslatedString is used. The classic WinForms
        ''' form passes DWSIM.App.GetPropertyName here so exported names stay identical.
        ''' </summary>
        Public Property PropertyNameResolver As Func(Of String, String) = Nothing

        ''' <summary>Optional product version string shown in the HTML header (e.g. "DWSIM 10.0").</summary>
        Public Property ProductVersion As String = Nothing

    End Class

    ''' <summary>
    ''' Cross-platform, UI-agnostic builder for the flowsheet results report. Produces a flat
    ''' DataTable (Name/Type/Property/Value/Unit) and a self-contained, print-friendly HTML
    ''' document. Lives in DWSIM.SharedClasses so both the classic WinForms UI and the
    ''' cross-platform (Eto/Avalonia) UIs can reuse it.
    ''' </summary>
    Public Class SimulationReportBuilder

        Private ReadOnly frm As IFlowsheet

        ' Material-stream property index ranges (match the classic report form layout).
        Private Const r1 As Integer = 5
        Private Const r2 As Integer = 12
        Private Const r3 As Integer = 30
        Private Const r4 As Integer = 48
        Private Const r5 As Integer = 66
        Private Const r6 As Integer = 84
        Private Const r7 As Integer = 131

        Public Sub New(flowsheet As IFlowsheet)
            frm = flowsheet
        End Sub

        ''' <summary>
        ''' Builds the report DataTable with columns Name/Type/Property/Value/Unit.
        ''' </summary>
        Public Function BuildDataTable(Optional options As ReportOptions = Nothing) As DataTable

            If options Is Nothing Then options = New ReportOptions()

            Dim su = CType(frm.FlowsheetOptions.SelectedUnitSystem, SystemsOfUnits.Units)
            Dim nf As String = frm.FlowsheetOptions.NumberFormat

            Dim DT As New DataTable()
            DT.Columns.Add("Nome", GetType(String))
            DT.Columns.Add("Tipo", GetType(String))
            DT.Columns.Add("Propriedade", GetType(String))
            DT.Columns.Add("Valor", GetType(String))
            DT.Columns.Add("Unidade", GetType(String))

            ' Resolve the ordered set of objects to include.
            Dim objects As New List(Of ISimulationObject)
            If options.ObjectNames Is Nothing OrElse options.ObjectNames.Count = 0 Then
                objects.AddRange(frm.SimulationObjects.Values)
            Else
                For Each nm In options.ObjectNames
                    If frm.SimulationObjects.ContainsKey(nm) Then objects.Add(frm.SimulationObjects(nm))
                Next
            End If

            For Each baseobj As ISimulationObject In objects

                Dim properties() As String = baseobj.GetProperties(Interfaces.Enums.PropertyType.ALL)
                Dim objtype As ObjectType = baseobj.GraphicObject.ObjectType
                Dim name As String = baseobj.GraphicObject.Tag
                Dim description As String = frm.GetTranslatedString(baseobj.GraphicObject.Description)

                If objtype = ObjectType.MaterialStream Then

                    If options.IncludeConditions Then AddPropRange(DT, baseobj, su, nf, name, description, properties, options, 0, r1 - 1)
                    If options.IncludeCompositions Then AddComposition(DT, baseobj, name, description, nf, 0, "FraomolarnaMistura", False, options)

                    If options.IncludeMixtureProps Then AddPropRange(DT, baseobj, su, nf, name, description, properties, options, r1, r2 - 1)
                    If options.IncludeCompositions Then AddComposition(DT, baseobj, name, description, nf, 2, "PROP_MS_106", True, options)

                    If options.IncludeVaporProps Then AddPropRange(DT, baseobj, su, nf, name, description, properties, options, r2, r3 - 1)
                    If options.IncludeCompositions Then AddComposition(DT, baseobj, name, description, nf, 1, "PROP_MS_107", True, options)

                    If options.IncludeLiquidMixtureProps Then AddPropRange(DT, baseobj, su, nf, name, description, properties, options, r3, r4 - 1)
                    If options.IncludeCompositions Then AddComposition(DT, baseobj, name, description, nf, 3, "PROP_MS_108", True, options)

                    If options.IncludeLiquid1Props Then AddPropRange(DT, baseobj, su, nf, name, description, properties, options, r4, r5 - 1)
                    If options.IncludeCompositions Then AddComposition(DT, baseobj, name, description, nf, 4, "PROP_MS_109", True, options)

                    If options.IncludeLiquid2Props Then AddPropRange(DT, baseobj, su, nf, name, description, properties, options, r5, r6 - 1)
                    If options.IncludeCompositions Then AddComposition(DT, baseobj, name, description, nf, 6, "PROP_MS_110", True, options)

                    If options.IncludeAqueousProps Then AddPropRange(DT, baseobj, su, nf, name, description, properties, options, r6, 101)
                    If options.IncludeCompositions Then AddComposition(DT, baseobj, name, description, nf, 7, "PROP_MS_146", True, options)

                    If options.IncludeSolidProps Then AddPropRange(DT, baseobj, su, nf, name, description, properties, options, r7, 148)

                Else

                    For Each prop As String In properties
                        Dim val = baseobj.GetPropertyValue(prop, su)
                        Dim valStr As String
                        Dim dblVal As Double
                        If val IsNot Nothing AndAlso Double.TryParse(val.ToString(), Globalization.NumberStyles.Any, Globalization.CultureInfo.InvariantCulture, dblVal) Then
                            valStr = Format(dblVal, nf)
                        Else
                            valStr = If(val IsNot Nothing, val.ToString(), "")
                        End If
                        DT.Rows.Add(New String() {name, description, ResolveName(options, prop), valStr, baseobj.GetPropertyUnit(prop, su)})
                    Next

                End If

            Next

            Return DT

        End Function

        ''' <summary>
        ''' Generates a self-contained, print-friendly HTML report document.
        ''' </summary>
        Public Function GenerateHTML(Optional options As ReportOptions = Nothing) As String

            If options Is Nothing Then options = New ReportOptions()

            Dim DT As DataTable = BuildDataTable(options)

            Dim simName As String = If(frm.FlowsheetOptions.SimulationName, "")
            Dim simComments As String = If(frm.FlowsheetOptions.SimulationComments, "")
            Dim simPath As String = If(frm.FilePath, "")
            Dim version As String = If(options.ProductVersion, "DWSIM " & Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString())

            Dim sb As New StringBuilder()
            sb.AppendLine("<!DOCTYPE html>")
            sb.AppendLine("<html lang=""en""><head><meta charset=""utf-8"">")
            sb.AppendLine("<meta name=""viewport"" content=""width=device-width, initial-scale=1"">")
            sb.AppendLine("<title>" & Enc(If(simName <> "", simName, "Simulation Results Report")) & "</title>")
            sb.AppendLine("<style>")
            sb.AppendLine("  :root { color-scheme: light; }")
            sb.AppendLine("  body { font-family: 'Segoe UI', Arial, sans-serif; font-size: 12px; color: #222; margin: 24px; background: #fff; }")
            sb.AppendLine("  header.report-head { border-bottom: 2px solid #2c6fb5; padding-bottom: 10px; margin-bottom: 18px; }")
            sb.AppendLine("  header.report-head h1 { font-size: 20px; margin: 0 0 4px 0; color: #2c6fb5; }")
            sb.AppendLine("  header.report-head .meta { color: #555; font-size: 11px; line-height: 1.5; }")
            sb.AppendLine("  section.obj { margin-bottom: 22px; page-break-inside: avoid; }")
            sb.AppendLine("  section.obj h2 { font-size: 14px; margin: 0 0 6px 0; color: #1f4e79; border-left: 4px solid #2c6fb5; padding-left: 8px; }")
            sb.AppendLine("  table { border-collapse: collapse; width: 100%; }")
            sb.AppendLine("  th, td { text-align: left; padding: 4px 8px; border-bottom: 1px solid #e2e2e2; vertical-align: top; }")
            sb.AppendLine("  th { background: #f2f6fb; color: #1f4e79; font-weight: 600; }")
            sb.AppendLine("  td.value { text-align: right; white-space: nowrap; }")
            sb.AppendLine("  td.unit { color: #666; white-space: nowrap; }")
            sb.AppendLine("  tr.subhead td { background: #fafafa; font-weight: 600; color: #444; }")
            sb.AppendLine("  @media print {")
            sb.AppendLine("    body { margin: 0; font-size: 11px; }")
            sb.AppendLine("    section.obj { page-break-inside: avoid; }")
            sb.AppendLine("    th { -webkit-print-color-adjust: exact; print-color-adjust: exact; }")
            sb.AppendLine("  }")
            sb.AppendLine("</style></head><body>")

            sb.AppendLine("<header class=""report-head"">")
            sb.AppendLine("  <h1>" & Enc(If(simName <> "", simName, "Simulation Results Report")) & "</h1>")
            sb.AppendLine("  <div class=""meta"">")
            If simComments <> "" Then sb.AppendLine("    <div>" & Enc(simComments) & "</div>")
            If simPath <> "" Then sb.AppendLine("    <div>Simulation File: " & Enc(simPath) & "</div>")
            sb.AppendLine("    <div>" & Enc(version) & " &middot; " & Enc(Date.Now.ToString()) & "</div>")
            sb.AppendLine("  </div>")
            sb.AppendLine("</header>")

            ' Group consecutive rows by object name (column 0).
            Dim i As Integer = 0
            Dim rows As DataRowCollection = DT.Rows
            While i < rows.Count
                Dim objName As String = rows(i).Item(0).ToString()
                Dim objDesc As String = rows(i).Item(1).ToString()

                sb.AppendLine("<section class=""obj"">")
                sb.AppendLine("  <h2>" & Enc(objName) & If(objDesc <> "", " <span style=""font-weight:400;color:#777"">(" & Enc(objDesc) & ")</span>", "") & "</h2>")
                sb.AppendLine("  <table><thead><tr>")
                sb.AppendLine("    <th>Property</th>")
                sb.AppendLine("    <th style=""text-align:right"">Value</th>")
                sb.AppendLine("    <th>Unit</th>")
                sb.AppendLine("  </tr></thead><tbody>")

                While i < rows.Count AndAlso rows(i).Item(0).ToString() = objName
                    Dim prop As String = rows(i).Item(2).ToString()
                    Dim val As String = rows(i).Item(3).ToString()
                    Dim unit As String = rows(i).Item(4).ToString()
                    If val = "" AndAlso unit = "" Then
                        ' Section / composition sub-header row.
                        sb.AppendLine("    <tr class=""subhead""><td colspan=""3"">" & Enc(prop) & "</td></tr>")
                    Else
                        sb.AppendLine("    <tr><td>" & Enc(prop) & "</td><td class=""value"">" & Enc(val) & "</td><td class=""unit"">" & Enc(unit) & "</td></tr>")
                    End If
                    i += 1
                End While

                sb.AppendLine("  </tbody></table>")
                sb.AppendLine("</section>")
            End While

            sb.AppendLine("</body></html>")

            Return sb.ToString()

        End Function

        ' ── helpers ───────────────────────────────────────────────────────────

        Private Function ResolveName(options As ReportOptions, key As String) As String
            If options.PropertyNameResolver IsNot Nothing Then Return options.PropertyNameResolver(key)
            Return frm.GetTranslatedString(key)
        End Function

        Private Sub AddPropRange(DT As DataTable, baseobj As ISimulationObject, su As SystemsOfUnits.Units, nf As String,
                                 name As String, description As String, properties() As String,
                                 options As ReportOptions, fromIdx As Integer, toIdx As Integer)
            For propidx = fromIdx To toIdx
                If propidx < 0 OrElse propidx > properties.Length - 1 Then Continue For
                Dim value As String = baseobj.GetPropertyValue(properties(propidx), su)
                If Double.TryParse(value, New Double) Then
                    DT.Rows.Add(New String() {name, description, ResolveName(options, properties(propidx)), Format(Double.Parse(value), nf), baseobj.GetPropertyUnit(properties(propidx), su)})
                Else
                    DT.Rows.Add(New String() {name, description, ResolveName(options, properties(propidx)), value, baseobj.GetPropertyUnit(properties(propidx), su)})
                End If
            Next
        End Sub

        Private Sub AddComposition(DT As DataTable, baseobj As ISimulationObject, name As String, description As String,
                                   nf As String, phaseIdx As Integer, headerKey As String,
                                   headerIsPropertyKey As Boolean, options As ReportOptions)
            Dim header As String = If(headerIsPropertyKey, ResolveName(options, headerKey), frm.GetTranslatedString(headerKey))
            DT.Rows.Add(New String() {name, description, header, "", ""})
            For Each subst As ICompound In DirectCast(baseobj, IMaterialStream).Phases(phaseIdx).Compounds.Values
                DT.Rows.Add(New String() {name, description, subst.Name, Format(subst.MoleFraction.GetValueOrDefault, nf), ""})
            Next
        End Sub

        Private Shared Function Enc(s As String) As String
            If s Is Nothing Then Return ""
            Return Net.WebUtility.HtmlEncode(s)
        End Function

    End Class

End Namespace
