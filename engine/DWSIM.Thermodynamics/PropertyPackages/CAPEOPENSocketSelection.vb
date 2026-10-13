'    CAPE-OPEN Property Package Wrapper: server and package selection
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

Imports CapeOpen
Imports Microsoft.Win32
Imports System.Linq
Imports DWSIM.Interfaces
Imports DWSIM.SharedClasses

Namespace PropertyPackages

    Partial Public Class CAPEOPENPropertyPackage

        ''' <summary>COM component category of the CAPE-OPEN 1.0 Thermo Systems.</summary>
        Public Const ThermoSystemCategory10 As String = "{678c09a3-7d66-11d2-a67d-00105a42887f}"

        ''' <summary>COM component category of the CAPE-OPEN 1.1 Property Package Managers.</summary>
        Public Const PropertyPackageManagerCategory11 As String = "{cf51e383-0110-4ed8-acb7-b50cfde6908e}"

        ''' <summary>The message raised when the package is used before one has been selected.</summary>
        Public Const NoPackageSelectedMessage As String =
            "Select a CAPE-OPEN property package in its configuration (Configure) before solving."

        ''' <summary>
        ''' The CAPE-OPEN property package object, or a clear error when none has been selected.
        ''' </summary>
        Private Function RequirePackage() As Object
            Dim copp = _copp
            If copp Is Nothing Then
                Dim who = If(String.IsNullOrEmpty(Me.Tag), Me.ComponentName, Me.Tag)
                Throw New InvalidOperationException(who & ": no CAPE-OPEN property package has been selected. " & NoPackageSelectedMessage)
            End If
            Return copp
        End Function

        ''' <summary>True when a CAPE-OPEN property package object has been created for this package.</summary>
        Public ReadOnly Property HasPropertyPackage As Boolean
            Get
                Return _copp IsNot Nothing
            End Get
        End Property

        ''' <summary>
        ''' Scans HKCR and HKCU for the CAPE-OPEN 1.1 Property Package Managers and the CAPE-OPEN 1.0
        ''' Thermo Systems registered on this machine. Touches nothing but the registry, so it can run
        ''' on any thread. Returns an empty list on platforms other than Windows.
        ''' </summary>
        ''' <param name="version">"1.1" for the managers, "1.0" for the thermo systems, empty for both.</param>
        Public Shared Function SearchRegisteredServers(Optional version As String = "") As List(Of CapeOpenObjInfo)

            Dim result As New List(Of CapeOpenObjInfo)

            If Environment.OSVersion.Platform <> PlatformID.Win32NT Then Return result

            Dim categories As New List(Of String)
            If version = "" OrElse version = "1.1" Then categories.Add(PropertyPackageManagerCategory11)
            If version = "" OrElse version = "1.0" Then categories.Add(ThermoSystemCategory10)

            Dim seen As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

            Dim roots As New List(Of RegistryKey)
            Try
                roots.Add(Registry.ClassesRoot.OpenSubKey("CLSID", False))
            Catch ex As Exception
            End Try
            Try
                roots.Add(Registry.CurrentUser.OpenSubKey("SOFTWARE\Classes\CLSID", False))
            Catch ex As Exception
            End Try

            For Each root In roots
                If root Is Nothing Then Continue For
                Using root
                    For Each clsid In root.GetSubKeyNames()
                        Try
                            Using key = root.OpenSubKey(clsid, False)
                                If key Is Nothing Then Continue For
                                Using cats = key.OpenSubKey("Implemented Categories", False)
                                    If cats Is Nothing Then Continue For
                                    Dim implemented = cats.GetSubKeyNames().Select(Function(s) s.ToLowerInvariant()).ToList()
                                    For Each category In categories
                                        If Not implemented.Contains(category) Then Continue For
                                        If Not seen.Add(clsid & category) Then Continue For
                                        Dim info = ReadObjectInfo(key, category)
                                        If info IsNot Nothing Then result.Add(info)
                                    Next
                                End Using
                            End Using
                        Catch ex As Exception
                            ' a key we may not read, or one that went away while scanning
                        End Try
                    Next
                End Using
            Next

            Return result.OrderBy(Function(x) If(x.ImplementedCategory = ThermoSystemCategory10, 1, 0)).ThenBy(Function(x) x.Name).ToList()

        End Function

        Private Shared Function ReadObjectInfo(key As RegistryKey, category As String) As CapeOpenObjInfo

            Dim info As New CapeOpenObjInfo With {.ImplementedCategory = category}

            Using desc = key.OpenSubKey("CapeDescription", False)
                If desc IsNot Nothing Then
                    info.AboutInfo = CStr(desc.GetValue("About", ""))
                    info.CapeVersion = CStr(desc.GetValue("CapeVersion", ""))
                    info.Description = CStr(desc.GetValue("Description", ""))
                    info.HelpURL = CStr(desc.GetValue("HelpURL", ""))
                    info.Name = CStr(desc.GetValue("Name", ""))
                    info.VendorURL = CStr(desc.GetValue("VendorURL", ""))
                    info.Version = CStr(desc.GetValue("ComponentVersion", ""))
                End If
            End Using

            Using progid = key.OpenSubKey("ProgID", False)
                If progid IsNot Nothing Then info.TypeName = CStr(progid.GetValue("", ""))
            End Using

            ' without a ProgID the object cannot be created (nor re-created when the file is loaded)
            If String.IsNullOrEmpty(info.TypeName) Then Return Nothing

            If String.IsNullOrEmpty(info.Name) Then info.Name = info.TypeName

            Using server = key.OpenSubKey("InProcServer32", False)
                If server IsNot Nothing Then
                    info.Location = CStr(server.GetValue("", ""))
                Else
                    Using local = key.OpenSubKey("LocalServer32", False)
                        If local IsNot Nothing Then info.Location = CStr(local.GetValue("", ""))
                    End Using
                End If
            End Using

            Return info

        End Function

        ''' <summary>
        ''' Creates the Property Package Manager (1.1) or Thermo System (1.0) described by
        ''' <paramref name="server"/>, initializes it and makes it this package's server. The package
        ''' selected before, if any, is dropped: pick one with <see cref="SelectPropertyPackage"/>.
        ''' CAPE-OPEN objects are COM objects: call this from an STA thread.
        ''' </summary>
        Public Sub SelectServer(server As CapeOpenObjInfo)

            If server Is Nothing Then Throw New ArgumentNullException(NameOf(server))

            Dim t As Type = Nothing
            Try
                t = Type.GetTypeFromProgID(server.TypeName)
            Catch ex As Exception
                Throw New Exception("Error creating CAPE-OPEN Thermo Server / Property Package Manager instance '" & server.TypeName & "'.", ex)
            End Try
            If t Is Nothing Then
                Throw New Exception("The CAPE-OPEN Thermo Server / Property Package Manager '" & server.TypeName & "' is not registered on this system.")
            End If

            Dim manager As Object
            Try
                manager = Activator.CreateInstance(t)
            Catch ex As Exception
                Throw New Exception("Error creating CAPE-OPEN Thermo Server / Property Package Manager instance '" & server.TypeName & "'.", ex)
            End Try

            Dim init = TryCast(manager, Interfaces2.IPersistStreamInit)
            If init IsNot Nothing Then init.InitNew()
            Dim utilities = TryCast(manager, ICapeUtilities)
            If utilities IsNot Nothing Then
                Try
                    utilities.Initialize()
                Catch ex As Exception
                    Throw New Exception("Error initializing the CAPE-OPEN Thermo Server / Property Package Manager '" & server.Name & "': " & DescribeCapeError(ex, manager), ex)
                End Try
            End If

            _coversion = If(String.Equals(server.ImplementedCategory, ThermoSystemCategory10, StringComparison.OrdinalIgnoreCase), "1.0", "1.1")
            _selts = server
            _pptpl = manager
            _copp = Nothing
            _ppname = ""
            _istrts = Nothing
            _istrpp = Nothing

        End Sub

        ''' <summary>The names of the property packages the selected server offers.</summary>
        Public Function GetAvailablePropertyPackages() As List(Of String)

            Dim list As New List(Of String)
            If _pptpl Is Nothing Then Return list

            Dim names As Object
            If _coversion = "1.0" Then
                names = CType(_pptpl, ICapeThermoSystem).GetPropertyPackages()
            Else
                names = CType(_pptpl, ICapeThermoPropertyPackageManager).GetPropertyPackageList()
            End If

            If names IsNot Nothing Then
                For Each n In CType(names, IEnumerable)
                    If n IsNot Nothing Then list.Add(n.ToString())
                Next
            End If

            Return list

        End Function

        ''' <summary>
        ''' Asks the selected server for the property package <paramref name="name"/>, initializes it
        ''' and maps the flowsheet compounds and phases onto the ones it offers (compounds by CAS
        ''' number). CAPE-OPEN objects are COM objects: call this from an STA thread.
        ''' </summary>
        Public Sub SelectPropertyPackage(name As String, compounds As IEnumerable(Of ICompoundConstantProperties))

            If _pptpl Is Nothing Then Throw New InvalidOperationException("Select a CAPE-OPEN Property Package Manager or Thermo System first.")

            Dim package As Object
            Try
                If _coversion = "1.0" Then
                    package = CType(_pptpl, ICapeThermoSystem).ResolvePropertyPackage(name)
                Else
                    package = CType(_pptpl, ICapeThermoPropertyPackageManager).GetPropertyPackage(name)
                End If
            Catch ex As Exception
                Throw New Exception("The CAPE-OPEN Property Package Manager '" & If(_selts IsNot Nothing, _selts.Name, "") &
                                    "' could not deliver the '" & name & "' Property Package. " & DescribeCapeError(ex, _pptpl), ex)
            End Try

            If package Is Nothing Then
                Throw New Exception("The CAPE-OPEN Property Package Manager '" & If(_selts IsNot Nothing, _selts.Name, "") &
                                    "' does not know a Property Package named '" & name & "'.")
            End If

            Dim init = TryCast(package, Interfaces2.IPersistStreamInit)
            If init IsNot Nothing Then init.InitNew()
            Dim utilities = TryCast(package, ICapeUtilities)
            If utilities IsNot Nothing Then
                Try
                    utilities.Initialize()
                Catch ex As Exception
                    Throw New Exception("Error initializing the CAPE-OPEN Property Package '" & name & "': " & DescribeCapeError(ex, package), ex)
                End Try
            End If

            _copp = package
            _ppname = name
            _istrpp = Nothing

            If compounds IsNot Nothing Then UpdateMappings(compounds)

        End Sub

        ''' <summary>The compound IDs the selected property package knows.</summary>
        Public Function GetPackageCompoundIds() As List(Of String)
            Dim ids As New List(Of String)
            If _copp Is Nothing Then Return ids
            Dim complist As Object = Nothing, casids As Object = Nothing
            ReadPackageCompounds(complist, casids)
            If complist IsNot Nothing Then
                For Each c In CType(complist, IEnumerable)
                    ids.Add(If(c Is Nothing, "", c.ToString()))
                Next
            End If
            Return ids
        End Function

        ''' <summary>The phase labels the selected property package knows.</summary>
        Public Function GetPackagePhaseLabels() As List(Of String)
            Dim labels As New List(Of String)
            If _copp Is Nothing Then Return labels
            Dim plist As Object = Nothing, staggr As Object = Nothing
            ReadPackagePhases(plist, staggr)
            If plist IsNot Nothing Then
                For Each p In CType(plist, IEnumerable)
                    labels.Add(If(p Is Nothing, "", p.ToString()))
                Next
            End If
            Return labels
        End Function

        Private Sub ReadPackageCompounds(ByRef complist As Object, ByRef casids As Object)
            Dim formulae As Object = Nothing, names As Object = Nothing, boiltemps As Object = Nothing, molwts As Object = Nothing
            If _coversion = "1.0" Then
                CType(_copp, ICapeThermoPropertyPackage).GetComponentList(complist, formulae, names, boiltemps, molwts, casids)
            Else
                CType(_copp, ICapeThermoCompounds).GetCompoundList(complist, formulae, names, boiltemps, molwts, casids)
            End If
        End Sub

        Private Sub ReadPackagePhases(ByRef plist As Object, ByRef staggr As Object)
            If _coversion = "1.0" Then
                plist = CType(_copp, ICapeThermoPropertyPackage).GetPhaseList()
            Else
                Dim kci As Object = Nothing
                CType(_copp, ICapeThermoPhases).GetPhaseList(plist, staggr, kci)
            End If
        End Sub

        ''' <summary>
        ''' Maps the flowsheet compounds onto the selected package's compounds by CAS number, and the
        ''' DWSIM phases that have no label yet onto the package's phases by state of aggregation, as
        ''' the WinForms configuration window does for CAPE-OPEN 1.1. A 1.0 package reports no state of
        ''' aggregation, so its phases are matched by label (Vapor, Liquid, Solid).
        ''' </summary>
        Public Sub UpdateMappings(compounds As IEnumerable(Of ICompoundConstantProperties))

            If _mappings Is Nothing Then _mappings = New Dictionary(Of String, String)
            If _phasemappings Is Nothing Then CreatePhaseMappings()

            Dim comps = compounds.ToList()

            Dim remap As Boolean = comps.Count <> _mappings.Count OrElse comps.Any(Function(c) Not _mappings.ContainsKey(c.Name))
            If remap Then
                _mappings.Clear()
                For Each c In comps
                    _mappings.Add(c.Name, "")
                Next
            End If

            If _copp Is Nothing Then Exit Sub

            Dim complist As Object = Nothing, casids As Object = Nothing
            ReadPackageCompounds(complist, casids)

            If complist IsNot Nothing AndAlso casids IsNot Nothing Then
                Dim ids = CType(complist, IEnumerable).Cast(Of Object).Select(Function(o) If(o Is Nothing, "", o.ToString())).ToList()
                Dim cas = CType(casids, IEnumerable).Cast(Of Object).Select(Function(o) If(o Is Nothing, "", o.ToString())).ToList()
                For Each c In comps
                    For i = 0 To Math.Min(ids.Count, cas.Count) - 1
                        If c.CAS_Number = cas(i) Then _mappings(c.Name) = ids(i)
                    Next
                Next
            End If

            Dim plist As Object = Nothing, staggr As Object = Nothing
            ReadPackagePhases(plist, staggr)
            If plist Is Nothing Then Exit Sub

            Dim labels = CType(plist, IEnumerable).Cast(Of Object).Select(Function(o) If(o Is Nothing, "", o.ToString())).ToList()
            Dim states As List(Of String)
            If staggr IsNot Nothing Then
                states = CType(staggr, IEnumerable).Cast(Of Object).Select(Function(o) If(o Is Nothing, "", o.ToString())).ToList()
            Else
                ' CAPE-OPEN 1.0 gives the labels only: read the state of aggregation off the label
                states = labels.Select(Function(l)
                                           Dim low = l.ToLowerInvariant()
                                           ' a label naming two phases (ChemSep's LiquidVapor) is neither
                                           If low = "vapor" OrElse low = "vapour" OrElse low = "gas" Then Return "Vapor"
                                           If low.StartsWith("liquid") AndAlso Not low.Contains("vap") Then Return "Liquid"
                                           If low.StartsWith("solid") Then Return "Solid"
                                           Return ""
                                       End Function).ToList()
            End If
            Dim used(labels.Count - 1) As Boolean

            Dim assign = Sub(dwphase As String, state As String)
                             If Not _phasemappings.ContainsKey(dwphase) Then Exit Sub
                             If _phasemappings(dwphase).PhaseLabel <> "" Then Exit Sub
                             For i = 0 To Math.Min(labels.Count, states.Count) - 1
                                 If states(i) = state AndAlso Not used(i) Then
                                     _phasemappings(dwphase).PhaseLabel = labels(i)
                                     used(i) = True
                                     Exit For
                                 End If
                             Next
                             If _phasemappings(dwphase).PhaseLabel = "" Then _phasemappings(dwphase).PhaseLabel = "Disabled"
                         End Sub

            assign("Vapor", "Vapor")
            assign("Liquid1", "Liquid")
            assign("Liquid2", "Liquid")
            assign("Liquid3", "Liquid")
            assign("Aqueous", "Liquid")
            assign("Solid", "Solid")

        End Sub

        ''' <summary>Opens the selected server's own editor (ICapeUtilities.Edit). False when it has none.</summary>
        Public Function EditServer() As Boolean
            Dim utilities = TryCast(_pptpl, ICapeUtilities)
            If utilities Is Nothing Then Return False
            utilities.Edit()
            Return True
        End Function

        ''' <summary>Opens the selected package's own editor (ICapeUtilities.Edit). False when it has none.</summary>
        Public Function EditPropertyPackage() As Boolean
            Dim utilities = TryCast(_copp, ICapeUtilities)
            If utilities Is Nothing Then Return False
            utilities.Edit()
            Return True
        End Function

    End Class

End Namespace
