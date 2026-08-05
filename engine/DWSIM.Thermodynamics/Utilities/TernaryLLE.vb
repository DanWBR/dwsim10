'    Ternary Liquid-Liquid Equilibrium Diagram
'    Copyright 2025 Daniel Wagner O. de Medeiros
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

Imports DWSIM.Thermodynamics.BaseClasses
Imports DWSIM.Thermodynamics.PropertyPackages
Imports DWSIM.Thermodynamics.Streams

Namespace Utilities.LLE

    ''' <summary>
    ''' One tie line (konode) of a ternary LLE diagram. The coordinates are the mole fractions of
    ''' compounds 1 and 2 in each of the two liquid phases.
    ''' </summary>
    Public Class TieLine

        Public Property X11 As Double
        Public Property X12 As Double
        Public Property X21 As Double
        Public Property X22 As Double

        Public ReadOnly Property Length As Double
            Get
                Return Math.Sqrt((X21 - X11) ^ 2 + (X22 - X12) ^ 2)
            End Get
        End Property

        Public Function Copy() As TieLine
            Return New TieLine With {.X11 = X11, .X12 = X12, .X21 = X21, .X22 = X22}
        End Function

        ''' <summary>Exchanges the two ends, used when the flash reorders the liquid phases.</summary>
        Public Sub Swap()
            Dim t1 = X11, t2 = X12
            X11 = X21 : X12 = X22
            X21 = t1 : X22 = t2
        End Sub

    End Class

    ''' <summary>
    ''' Traces the binodal curve of a ternary system at fixed temperature and pressure by walking
    ''' from tie line to tie line, each new feed composition being taken perpendicular to the
    ''' previous tie line from its midpoint. Shared by the WinForms and the Avalonia LLE utilities.
    ''' </summary>
    Public Class TernaryLLETracer

        ''' <summary>Feed compositions scanned for a first two-liquid point.</summary>
        Private Shared ReadOnly SeedPoints As Double()() = {
            New Double() {0.5, 0.0}, New Double() {0.0, 0.5}, New Double() {0.5, 0.5},
            New Double() {0.25, 0.0}, New Double() {0.75, 0.0},
            New Double() {0.0, 0.25}, New Double() {0.0, 0.75},
            New Double() {0.25, 0.75}, New Double() {0.75, 0.25},
            New Double() {0.125, 0.0}, New Double() {0.375, 0.0},
            New Double() {0.625, 0.0}, New Double() {0.875, 0.0},
            New Double() {0.0, 0.125}, New Double() {0.0, 0.375},
            New Double() {0.0, 0.625}, New Double() {0.0, 0.875},
            New Double() {0.125, 0.875}, New Double() {0.375, 0.625},
            New Double() {0.625, 0.375}, New Double() {0.875, 0.125}}

        Private ReadOnly _ms As MaterialStream
        Private ReadOnly _names As String()

        Public Sub New(flowsheet As Interfaces.IFlowsheet, pp As PropertyPackage,
                       comp1 As String, comp2 As String, comp3 As String, T As Double, P As Double)

            If comp1 = comp2 OrElse comp1 = comp3 OrElse comp2 = comp3 Then
                Throw New Exception("Select three different compounds.")
            End If

            _names = {comp1, comp2, comp3}

            _ms = New MaterialStream("", "")
            _ms.SetFlowsheet(flowsheet)

            For Each phase As BaseClasses.Phase In _ms.Phases.Values
                phase.Compounds.Clear()
                For Each n In _names
                    phase.Compounds.Add(n, New Compound(n, ""))
                    phase.Compounds(n).ConstantProperties = flowsheet.SelectedCompounds(n)
                Next
            Next

            _ms.PropertyPackage = pp
            pp.CurrentMaterialStream = _ms

            _ms.Phases(0).Properties.temperature = T
            _ms.Phases(0).Properties.pressure = P

        End Sub

        ''' <summary>Flashes one feed composition and returns the resulting tie line.</summary>
        Public Function CalculateTieLine(x1 As Double, x2 As Double) As TieLine

            _ms.Phases(0).Compounds(_names(0)).MoleFraction = x1
            _ms.Phases(0).Compounds(_names(1)).MoleFraction = x2
            _ms.Phases(0).Compounds(_names(2)).MoleFraction = 1 - x1 - x2

            _ms.CalcEquilibrium("tp", Nothing)

            Dim ko As New TieLine

            ko.X11 = _ms.Phases(3).Compounds(_names(0)).MoleFraction.GetValueOrDefault
            ko.X12 = _ms.Phases(3).Compounds(_names(1)).MoleFraction.GetValueOrDefault

            If _ms.Phases(4).Properties.molarfraction.GetValueOrDefault > 0 Then
                ko.X21 = _ms.Phases(4).Compounds(_names(0)).MoleFraction.GetValueOrDefault
                ko.X22 = _ms.Phases(4).Compounds(_names(1)).MoleFraction.GetValueOrDefault
            Else
                ko.X21 = ko.X11
                ko.X22 = ko.X12
            End If

            Return ko

        End Function

        ''' <summary>
        ''' Traces the miscibility gap. Returns an empty list when no seed composition splits into
        ''' two liquid phases, which means the system is fully miscible at these conditions.
        ''' </summary>
        Public Function Trace(Optional onError As Action(Of String) = Nothing) As List(Of TieLine)

            Dim curve As New List(Of TieLine)

            Dim ko As New TieLine, lastKo As New TieLine
            Dim ptx, pty As Double
            Dim dir = New Double(1) {}
            Dim first As Boolean = False

            'find a starting composition inside the miscibility gap

            For Each seed In SeedPoints
                Try
                    ko = CalculateTieLine(seed(0), seed(1))
                Catch ex As Exception
                    Continue For
                End Try
                If ko.Length > 0.01 Then
                    first = True
                    ptx = seed(0)
                    pty = seed(1)
                    If ptx > 0 AndAlso pty = 0 Then dir = {0, 1}
                    If ptx = 0 AndAlso pty > 0 Then dir = {1, 0}
                    If ptx > 0 AndAlso pty > 0 Then dir = {-1, -1}
                    Exit For
                End If
            Next

            If Not first Then Return curve

            Dim final As Boolean = False, searchmode As Boolean = False
            Dim stepsize As Double = 0.03
            Dim counter As Integer = 0

            Do
                Dim w As Double
                Try
                    ko = CalculateTieLine(ptx, pty)
                    w = ko.Length

                    If (ko.X21 + ko.X22 > 0) AndAlso w > 0.001 Then
                        If first Then
                            lastKo = ko.Copy
                            first = False
                        Else
                            'the flash may return the phases in the opposite order: keep the
                            'ends on the same branch of the binodal as the previous tie line
                            Dim d1 = (lastKo.X11 - ko.X11) ^ 2 + (lastKo.X12 - ko.X12) ^ 2
                            Dim d2 = (lastKo.X11 - ko.X21) ^ 2 + (lastKo.X12 - ko.X22) ^ 2
                            If d2 < d1 Then ko.Swap()
                            lastKo = ko.Copy
                        End If

                        curve.Add(ko.Copy)
                    Else
                        'outside the miscibility gap
                        searchmode = True
                    End If

                    If (w < 0.001 And Not searchmode) Or stepsize < 0.001 Or counter > 30 Then Exit Do

                Catch ex As Exception
                    onError?.Invoke(ex.Message)
                    Exit Do
                End Try

                If final Then Exit Do

                If searchmode Then
                    stepsize /= 3
                    NextPoint(lastKo, stepsize, dir, ptx, pty)
                    searchmode = False
                Else
                    NextPoint(ko, stepsize, dir, ptx, pty)
                End If

                If Not IsValidComposition(ptx, pty) Then
                    'project the point back onto the composition simplex and stop after it
                    Dim c = New Double() {ptx, pty, 1 - ptx - pty}
                    Dim sum As Double = 0
                    For k = 0 To 2
                        If c(k) < 0 Then c(k) = 0
                        If c(k) > 1 Then c(k) = 1
                        sum += c(k)
                    Next
                    ptx = c(0) / sum
                    pty = c(1) / sum
                    final = True
                End If

                counter += 1

            Loop

            Return curve

        End Function

        ''' <summary>
        ''' Next feed composition: from the midpoint of the tie line, one step along its normal,
        ''' keeping the same side as the previous step.
        ''' </summary>
        Private Shared Sub NextPoint(ko As TieLine, length As Double, dir As Double(),
                                     ByRef x As Double, ByRef y As Double)

            Dim mx = (ko.X21 + ko.X11) / 2
            Dim my = (ko.X22 + ko.X12) / 2
            Dim vx = ko.X21 - ko.X11
            Dim vy = ko.X22 - ko.X12
            Dim l = ko.Length

            If l = 0 Then Return

            Dim nx = -vy / l
            Dim ny = vx / l

            'a negative scalar product means the normal flipped to the other side of the tie line
            If nx * dir(0) + ny * dir(1) < 0 Then length = -length

            dir(0) = nx * length
            dir(1) = ny * length

            x = mx + length * nx
            y = my + length * ny

        End Sub

        Private Shared Function IsValidComposition(x1 As Double, x2 As Double) As Boolean
            Dim x3 = 1 - x1 - x2
            Return x1 >= 0 And x2 >= 0 And x3 >= 0 And x1 <= 1 And x2 <= 1 And x3 <= 1
        End Function

    End Class

End Namespace
