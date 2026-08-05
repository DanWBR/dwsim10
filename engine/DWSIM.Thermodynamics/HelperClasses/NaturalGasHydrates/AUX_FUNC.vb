'    Hydrate Calculation Routines
'    Copyright 2008 Daniel Wagner O. de Medeiros
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

Namespace NaturalGasHydrates

    Public Class AuxMethods

        Public MAT_UNIFAC(60, 35) As Double
        Public MAT_UNIFAC_ELL(60, 35) As Double
        Public MAT_PROPS(47, 26) As Double
        Public MAT_PROPS2(40, 27) As Double
        Public MAT_KIJ(38, 38) As Double
        Public MAT_KIJ_PRSV(38, 38) As Double
        Public MAT_KAPPA1(37, 1) As Double
        Public MAT_DIEL(37, 2) As Double
        Public MAT_SLOAN1(93, 15) As Double
        Public MAT_SLOAN2(81, 9) As Double
        Public MAT_SLOAN3(53, 7) As Double
        Public MAT_CHENGUO(35, 6) As Double
        Public MAT_KLAUDASANDLER(53, 7) As Double
        Public MAT_VDWP_PP(53, 7) As Double
        Public MAT_MOD_LIFAC(73, 47) As Double
        Public MAT_MOD_LIFAC_BIJ(22, 22) As Double
        Public MAT_MOD_LIFAC_CIJ(22, 22) As Double
        Public MAT_INHIB(46, 2) As Double

        Sub New()

            READ_DIEL()
            READ_INHIB()
            READ_CHENGUO()
            READ_KLAUDASANDLER()
            READ_VDWP_PP()

        End Sub

        Function GetInhibitorIndex(ByVal name As String) As Integer

            GetInhibitorIndex = -1
            Dim i = 0
            Do
                If MAT_INHIB(i, 0) = name Then GetInhibitorIndex = i + 1
                i = i + 1
            Loop Until i = 47

        End Function

        Function GetInhibitorStructureType(ByVal name As String) As Integer

            GetInhibitorStructureType = 0
            Dim i = 0
            Do
                If MAT_INHIB(i, 0) = name Then GetInhibitorStructureType = Convert.ToInt32(MAT_INHIB(i, 2))
                i = i + 1
            Loop Until i = 47

        End Function

        Function GetInhibitorType(ByVal name As String) As Integer

            GetInhibitorType = 0
            Dim i = 0
            Do
                If MAT_INHIB(i, 0) = name Then GetInhibitorType = Convert.ToInt32(MAT_INHIB(i, 1))
                i = i + 1
            Loop Until i = 47

        End Function

        Function CHARGE(ByVal id As Integer)

            CHARGE = 0
            If id = 39 Then CHARGE = 1
            If id = 40 Then CHARGE = 1
            If id = 41 Then CHARGE = 2
            If id = 42 Then CHARGE = -1
            If id = 43 Then CHARGE = -1
            If id = 44 Then CHARGE = -1

        End Function

        Function POS(ByVal Vids, ByVal i)

            POS = 0
            Dim j = 0
            Do
                If Vids(j) = i Then POS = j
                j = j + 1
            Loop Until j = UBound(Vids) + 1

        End Function

        Function READ_CHENGUO()


            Dim l, k As Integer
            Dim currentLine As String() = New String() {}

            Dim calculatorassembly = AppDomain.CurrentDomain.GetAssemblies().Where(Function(x) x.FullName.Contains("DWSIM.Thermodynamics,")).FirstOrDefault
            Using filestr As IO.Stream = calculatorassembly.GetManifestResourceStream("DWSIM.Thermodynamics.hid_chenguo.dat")
                Using MyReader As New Microsoft.VisualBasic.FileIO.TextFieldParser(filestr)
                    MyReader.TextFieldType = FileIO.FieldType.Delimited
                    MyReader.SetDelimiters(";")
                    l = 0
                    While Not MyReader.EndOfData
                        currentLine = MyReader.ReadFields()
                        k = 0
                        Do
                            MAT_CHENGUO(l, k) = Val(currentLine(k))
                            k = k + 1
                        Loop Until k = 7
                        l = l + 1
                    End While
                End Using
            End Using


            READ_CHENGUO = 1

        End Function

        Function READ_KLAUDASANDLER()

            Dim l, k As Integer
            Dim currentLine As String() = New String() {}
            Dim calculatorassembly = AppDomain.CurrentDomain.GetAssemblies().Where(Function(x) x.FullName.Contains("DWSIM.Thermodynamics,")).FirstOrDefault
            Using filestr As IO.Stream = calculatorassembly.GetManifestResourceStream("DWSIM.Thermodynamics.hid_klaudasandler.dat")
                Using MyReader As New Microsoft.VisualBasic.FileIO.TextFieldParser(filestr)
                    MyReader.TextFieldType = FileIO.FieldType.Delimited
                    MyReader.SetDelimiters(";")
                    l = 0
                    While Not MyReader.EndOfData
                        currentLine = MyReader.ReadFields()
                        k = 0
                        Do
                            MAT_KLAUDASANDLER(l, k) = Val(currentLine(k))
                            k = k + 1
                        Loop Until k = 8
                        l = l + 1
                    End While
                End Using
            End Using

            READ_KLAUDASANDLER = 1

        End Function

        Function READ_VDWP_PP()

            Dim l, k As Integer
            Dim currentLine As String() = New String() {}
            Dim calculatorassembly = AppDomain.CurrentDomain.GetAssemblies().Where(Function(x) x.FullName.Contains("DWSIM.Thermodynamics,")).FirstOrDefault
            Using filestr As IO.Stream = calculatorassembly.GetManifestResourceStream("DWSIM.Thermodynamics.hid_vdwp_pp.dat")
                Using MyReader As New Microsoft.VisualBasic.FileIO.TextFieldParser(filestr)
                    MyReader.TextFieldType = FileIO.FieldType.Delimited
                    MyReader.SetDelimiters(";")
                    l = 0
                    While Not MyReader.EndOfData
                        currentLine = MyReader.ReadFields()
                        k = 0
                        Do
                            MAT_VDWP_PP(l, k) = Val(currentLine(k))
                            k = k + 1
                        Loop Until k = 8
                        l = l + 1
                    End While
                End Using
            End Using

            READ_VDWP_PP = 1

        End Function

        Function READ_DIEL()

            Dim l, j As Integer
            Dim currentLine As String() = New String() {}

            Dim calculatorassembly = AppDomain.CurrentDomain.GetAssemblies().Where(Function(x) x.FullName.Contains("DWSIM.Thermodynamics,")).FirstOrDefault
            Using filestr As IO.Stream = calculatorassembly.GetManifestResourceStream("DWSIM.Thermodynamics.diel.dat")
                Using MyReader As New Microsoft.VisualBasic.FileIO.TextFieldParser(filestr)
                    MyReader.TextFieldType = FileIO.FieldType.Delimited
                    MyReader.SetDelimiters(";")
                    l = 0
                    While Not MyReader.EndOfData
                        currentLine = MyReader.ReadFields()
                        j = 0
                        Do
                            MAT_DIEL(l, j) = Val(currentLine(j))
                            j = j + 1
                        Loop Until j = 3
                        l = l + 1
                    End While
                End Using
            End Using

            READ_DIEL = 1

        End Function

        Function READ_INHIB()

            Dim l, j As Integer
            Dim currentLine As String() = New String() {}

            Dim calculatorassembly = AppDomain.CurrentDomain.GetAssemblies().Where(Function(x) x.FullName.Contains("DWSIM.Thermodynamics,")).FirstOrDefault
            Using filestr As IO.Stream = calculatorassembly.GetManifestResourceStream("DWSIM.Thermodynamics.inib.dat")
                Using MyReader2 As New Microsoft.VisualBasic.FileIO.TextFieldParser(filestr)
                    MyReader2.TextFieldType = FileIO.FieldType.Delimited
                    MyReader2.SetDelimiters(";")
                    l = 0
                    While Not MyReader2.EndOfData
                        currentLine = MyReader2.ReadFields()
                        j = 0
                        Do
                            MAT_INHIB(l, j) = Val(currentLine(j))
                            j = j + 1
                        Loop Until j = 3
                        l = l + 1
                    End While
                End Using
            End Using

            READ_INHIB = 1

        End Function

        Function GET_HS_KS(ByVal id) As Double()

            Dim i As Integer, tmp(3) As Double
            i = 0

            If id = 1 Then i = 29
            If id = 2 Then i = 30
            If id = 3 Then i = 31
            If id = 4 Then i = 32
            If id = 14 Then i = 33
            If id = 16 Then i = 34
            If id = 15 Then i = 35
            If id = 38 Then i = 36
            Try
                tmp(0) = Convert.ToDouble(MAT_KLAUDASANDLER(i, 1))
                tmp(1) = Convert.ToDouble(MAT_KLAUDASANDLER(i, 2))
                tmp(2) = Convert.ToDouble(MAT_KLAUDASANDLER(i, 3))
                tmp(3) = Convert.ToDouble(MAT_KLAUDASANDLER(i, 4))
            Catch
                tmp(0) = -1.0E+32
                tmp(1) = -1.0E+32
                tmp(2) = -1.0E+32
                tmp(3) = -1.0E+32
            End Try
            GET_HS_KS = tmp

        End Function

        ''' <summary>
        ''' Checks if a compound name corresponds to a common hydrate inhibitor
        ''' (thermodynamic inhibitors: alcohols, glycols, and salts).
        ''' Returns True for: methanol, ethanol, ethylene/diethylene/triethylene glycol,
        ''' propylene glycol, sodium chloride, calcium chloride, potassium chloride.
        ''' </summary>
        Function IsHydrateInhibitor(ByVal compoundName As String) As Boolean

            If String.IsNullOrEmpty(compoundName) Then Return False

            Dim n As String = compoundName.ToLowerInvariant().Replace(" ", "").Replace("-", "")

            ' Alcohols
            If n = "methanol" Or n = "metanol" Then Return True
            If n = "ethanol" Or n = "etanol" Then Return True

            ' Glycols (include both English/Portuguese names and common abbreviations)
            If n.Contains("ethyleneglycol") Or n.Contains("etilenoglicol") Or n.Contains("etilenglicol") Then Return True
            If n.Contains("propyleneglycol") Or n.Contains("propilenoglicol") Then Return True
            If n = "meg" Or n = "deg" Or n = "teg" Or n = "peg" Then Return True

            ' Salts (thermodynamic inhibitors via ionic strength / water activity reduction)
            If n.Contains("sodiumchloride") Or n.Contains("cloretodesodio") Or n = "nacl" Then Return True
            If n.Contains("calciumchloride") Or n.Contains("cloretodecalcio") Or n = "cacl2" Then Return True
            If n.Contains("potassiumchloride") Or n.Contains("cloretodepotassio") Or n = "kcl" Then Return True

            Return False

        End Function

        Function GetIDsForHydrateCalculation(ByVal names As String())

            Dim res As ArrayList = New ArrayList()

            Dim str As String
            For Each str In names
                If str = "Metano" Or str = "Methane" Then
                    res.Add(1)
                ElseIf str = "Etano" Or str = "Ethane" Then
                    res.Add(2)
                ElseIf str = "Propano" Or str = "Propane" Then
                    res.Add(3)
                ElseIf str = "nButano" Or str = "N-butane" Then
                    res.Add(5)
                ElseIf str = "iButano" Or str = "Isobutane" Then
                    res.Add(4)
                ElseIf str = "nPentano" Or str = "N-pentane" Then
                    res.Add(7)
                ElseIf str = "iPentano" Or str = "Isopentane" Then
                    res.Add(6)
                ElseIf str = "nHexano" Or str = "N-hexane" Then
                    res.Add(8)
                ElseIf str = "nHeptano" Or str = "N-heptane" Then
                    res.Add(9)
                ElseIf str = "nOctano" Or str = "N-octane" Then
                    res.Add(10)
                ElseIf str = "nNonano" Or str = "N-nonane" Then
                    res.Add(11)
                ElseIf str = "nDecano" Or str = "N-decane" Then
                    res.Add(12)
                ElseIf str = "Oxigenio" Or str = "Oxygen" Then
                    res.Add(17)
                ElseIf str = "Nitrogenio" Or str = "Nitrogen" Then
                    res.Add(16)
                ElseIf str = "Agua" Or str = "Water" Then
                    res.Add(13)
                ElseIf str = "DioxidoDeCarbono" Or str = "Carbon dioxide" Then
                    res.Add(15)
                ElseIf str = "SulfetoDeHidrogenio" Or str = "Hydrogen sulfide" Then
                    res.Add(14)
                ElseIf IsHydrateInhibitor(str) Then
                    res.Add(100)
                Else
                    res.Add(0)
                End If
            Next

            Return res.ToArray()

        End Function

    End Class

End Namespace
