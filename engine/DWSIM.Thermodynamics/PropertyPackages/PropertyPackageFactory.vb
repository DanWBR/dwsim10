'    Builds a property package from its display name.
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
Imports DWSIM.Thermodynamics.PropertyPackages.Auxiliary.FlashAlgorithms

Namespace PropertyPackages

    ''' <summary>
    ''' Builds a property package from the name it shows in the interface. The CAPE-OPEN manager
    ''' and the data regression engine both pick a package this way.
    ''' </summary>
    Public Class PropertyPackageFactory

        Public Shared Function Create(ByVal PackageName As String) As PropertyPackage
            Dim pp As PropertyPackage = Nothing
            Select Case PackageName
                Case "CoolProp"
                    pp = New CoolPropPropertyPackage(True)
                    pp.ComponentDescription = Calculator.GetLocalString("DescCPPP")
                Case "Peng-Robinson (PR)"
                    pp = New PengRobinsonPropertyPackage(True)
                    pp.ComponentDescription = Calculator.GetLocalString("DescPengRobinsonPP")
                Case "Peng-Robinson 1978 (PR78)"
                    pp = New PengRobinson1978PropertyPackage(True)
                Case "Peng-Robinson-Stryjek-Vera 2 (PRSV2-M)", "Peng-Robinson-Stryjek-Vera 2 (PRSV2)"
                    pp = New PRSV2PropertyPackage(True)
                    pp.ComponentDescription = Calculator.GetLocalString("DescPRSV2PP")
                Case "Peng-Robinson-Stryjek-Vera 2 (PRSV2-VL)"
                    pp = New PRSV2VLPropertyPackage(True)
                    pp.ComponentDescription = Calculator.GetLocalString("DescPRSV2VLPP")
                Case "Soave-Redlich-Kwong (SRK)"
                    pp = New SRKPropertyPackage(True)
                    pp.ComponentDescription = Calculator.GetLocalString("DescSoaveRedlichKwongSRK")
                Case "UNIFAC"
                    pp = New UNIFACPropertyPackage(True)
                    pp.ComponentDescription = Calculator.GetLocalString("DescUPP")
                Case "UNIFAC-LL"
                    pp = New UNIFACLLPropertyPackage(True)
                    pp.ComponentDescription = Calculator.GetLocalString("DescUPP")
                Case "NRTL"
                    pp = New NRTLPropertyPackage(True)
                    pp.ComponentDescription = Calculator.GetLocalString("DescNRTLPP")
                Case "UNIQUAC"
                    pp = New UNIQUACPropertyPackage(True)
                    pp.ComponentDescription = Calculator.GetLocalString("DescUNIQUACPP")
                Case "Wilson"
                    pp = New WilsonPropertyPackage()
                    pp.ComponentDescription = Calculator.GetLocalString("Wilson Property Package")
                Case "Modified UNIFAC (Dortmund)"
                    pp = New MODFACPropertyPackage(True)
                    pp.ComponentDescription = Calculator.GetLocalString("DescMUPP")
                Case "Modified UNIFAC (NIST)"
                    pp = New NISTMFACPropertyPackage(True)
                    pp.ComponentDescription = Calculator.GetLocalString("DescNUPP")
                Case "Chao-Seader"
                    pp = New ChaoSeaderPropertyPackage(True)
                    pp.ComponentDescription = Calculator.GetLocalString("DescCSLKPP")
                Case "Grayson-Streed"
                    pp = New GraysonStreedPropertyPackage(True)
                    pp.ComponentDescription = Calculator.GetLocalString("DescGSLKPP")
                Case "Lee-Kesler-Plöcker"
                    pp = New LKPPropertyPackage(True)
                    pp.ComponentDescription = Calculator.GetLocalString("DescLKPPP")
                Case "Raoult's Law"
                    pp = New RaoultPropertyPackage(True)
                    pp.ComponentDescription = Calculator.GetLocalString("DescRPP")
                Case "IAPWS-IF97 Steam Tables"
                    pp = New SteamTablesPropertyPackage(True)
                    pp.ComponentDescription = Calculator.GetLocalString("DescSteamTablesPP")
                Case "IAPWS-08 Seawater"
                    pp = New SeawaterPropertyPackage(True)
                    pp.ComponentDescription = Calculator.GetLocalString("DescSEAPP")
                Case Else
                    Dim otherpps = SharedClasses.Utility.LoadAdditionalPropertyPackages()
                    Dim p0 = otherpps.Where(Function(x) DirectCast(x, ICapeIdentification).ComponentName = PackageName)
                    If p0.Count > 0 Then
                        pp = DirectCast(p0(0), PropertyPackage)
                        Settings.CAPEOPENMode = True
                        pp.InitCO()
                        pp.Initialize()
                    Else
                        Throw New CapeBadArgumentException("Property Package not found.")
                    End If
            End Select
            If Not pp Is Nothing Then pp.ComponentName = PackageName
            Return pp
        End Function

        ''' <summary>The display names this factory knows.</summary>
        Public Shared Function Names() As Object
            Dim l As New List(Of String)({"CoolProp", "Peng-Robinson (PR)", "Peng-Robinson 1978 (PR78)", "Peng-Robinson-Stryjek-Vera 2 (PRSV2-M)", "Peng-Robinson-Stryjek-Vera 2 (PRSV2-VL)", "Soave-Redlich-Kwong (SRK)",
                                 "UNIFAC", "UNIFAC-LL", "Modified UNIFAC (Dortmund)", "Modified UNIFAC (NIST)", "NRTL", "UNIQUAC",
                                "Chao-Seader", "Grayson-Streed", "Lee-Kesler-Plöcker", "Raoult's Law", "IAPWS-IF97 Steam Tables", "IAPWS-08 Seawater"})
            Try
                Dim otherpps = SharedClasses.Utility.LoadAdditionalPropertyPackages()
                For Each pp In otherpps
                    l.Add(DirectCast(pp, CapeOpen.ICapeIdentification).ComponentName)
                Next
            Catch ex As Exception
                MsgBox(ex.ToString)
            End Try
            Return l.ToArray
        End Function

    End Class

End Namespace
