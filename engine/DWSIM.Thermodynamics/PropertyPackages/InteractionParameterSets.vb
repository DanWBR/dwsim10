'    The binary interaction parameter lookup of the property packages.
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

Imports DWSIM.Thermodynamics.PropertyPackages

Namespace ExcelAddIn

    ''' <summary>
    ''' The half of the Excel integration that has no Excel in it: three property packages
    ''' call this to report which binary pairs have no interaction parameters.
    ''' </summary>
    Partial Public Class ExcelIntegrationNoAttr

        Public Shared Function GetInteractionParameterSet(ByVal proppack As PropertyPackage, Model As String,
        ByVal Compound1 As String, ByVal Compound2 As String) As Object(,)

            Dim ipdata(1, 8) As Object

            ipdata(0, 0) = "ID1"
            ipdata(0, 1) = "ID2"
            ipdata(0, 2) = "kij/A12"
            ipdata(0, 3) = "kji/A21"
            ipdata(0, 4) = "B12"
            ipdata(0, 5) = "B21"
            ipdata(0, 6) = "C12"
            ipdata(0, 7) = "C21"
            ipdata(0, 8) = "alpha12"

            ipdata(1, 0) = Compound1
            ipdata(1, 1) = Compound2

            Select Case Model
                Case "Peng-Robinson"
                    Dim pp As PengRobinsonPropertyPackage = proppack
                    If pp.m_pr.InteractionParameters.ContainsKey(Compound1) Then
                        If pp.m_pr.InteractionParameters(Compound1).ContainsKey(Compound2) Then
                            ipdata(1, 2) = pp.m_pr.InteractionParameters(Compound1)(Compound2).kij
                        Else
                            If pp.m_pr.InteractionParameters.ContainsKey(Compound2) Then
                                If pp.m_pr.InteractionParameters(Compound2).ContainsKey(Compound1) Then
                                    ipdata(1, 2) = pp.m_pr.InteractionParameters(Compound2)(Compound1).kij
                                End If
                            End If
                        End If
                    ElseIf pp.m_pr.InteractionParameters.ContainsKey(Compound2) Then
                        If pp.m_pr.InteractionParameters(Compound2).ContainsKey(Compound1) Then
                            ipdata(1, 2) = pp.m_pr.InteractionParameters(Compound2)(Compound1).kij
                        End If
                    End If
                    pp.Dispose()
                    pp = Nothing
                Case "Peng-Robinson-Stryjek-Vera 2 (Van Laar)"
                    Dim pp As PRSV2VLPropertyPackage = proppack
                    If pp.m_pr.InteractionParameters.ContainsKey(Compound1) Then
                        If pp.m_pr.InteractionParameters(Compound1).ContainsKey(Compound2) Then
                            ipdata(1, 2) = pp.m_pr.InteractionParameters(Compound1)(Compound2).kij
                            ipdata(1, 3) = pp.m_pr.InteractionParameters(Compound1)(Compound2).kji
                        Else
                            If pp.m_pr.InteractionParameters.ContainsKey(Compound2) Then
                                If pp.m_pr.InteractionParameters(Compound2).ContainsKey(Compound1) Then
                                    ipdata(1, 2) = pp.m_pr.InteractionParameters(Compound2)(Compound1).kji
                                    ipdata(1, 3) = pp.m_pr.InteractionParameters(Compound2)(Compound1).kij
                                End If
                            End If
                        End If
                    ElseIf pp.m_pr.InteractionParameters.ContainsKey(Compound2) Then
                        If pp.m_pr.InteractionParameters(Compound2).ContainsKey(Compound1) Then
                            ipdata(1, 2) = pp.m_pr.InteractionParameters(Compound2)(Compound1).kji
                            ipdata(1, 3) = pp.m_pr.InteractionParameters(Compound2)(Compound1).kij
                        End If
                    End If
                    pp.Dispose()
                    pp = Nothing
                Case "Peng-Robinson-Stryjek-Vera 2 (Margules)"
                    Dim pp As PRSV2PropertyPackage = proppack
                    If pp.m_pr.InteractionParameters.ContainsKey(Compound1) Then
                        If pp.m_pr.InteractionParameters(Compound1).ContainsKey(Compound2) Then
                            ipdata(1, 2) = pp.m_pr.InteractionParameters(Compound1)(Compound2).kij
                            ipdata(1, 3) = pp.m_pr.InteractionParameters(Compound1)(Compound2).kji
                        Else
                            If pp.m_pr.InteractionParameters.ContainsKey(Compound2) Then
                                If pp.m_pr.InteractionParameters(Compound2).ContainsKey(Compound1) Then
                                    ipdata(1, 2) = pp.m_pr.InteractionParameters(Compound2)(Compound1).kji
                                    ipdata(1, 3) = pp.m_pr.InteractionParameters(Compound2)(Compound1).kij
                                End If
                            End If
                        End If
                    ElseIf pp.m_pr.InteractionParameters.ContainsKey(Compound2) Then
                        If pp.m_pr.InteractionParameters(Compound2).ContainsKey(Compound1) Then
                            ipdata(1, 2) = pp.m_pr.InteractionParameters(Compound2)(Compound1).kji
                            ipdata(1, 3) = pp.m_pr.InteractionParameters(Compound2)(Compound1).kij
                        End If
                    End If
                    pp.Dispose()
                    pp = Nothing
                Case "Soave-Redlich-Kwong"
                    Dim pp As SRKPropertyPackage = proppack
                    If pp.m_pr.InteractionParameters.ContainsKey(Compound1) Then
                        If pp.m_pr.InteractionParameters(Compound1).ContainsKey(Compound2) Then
                            ipdata(1, 2) = pp.m_pr.InteractionParameters(Compound1)(Compound2).kij
                        Else
                            If pp.m_pr.InteractionParameters.ContainsKey(Compound2) Then
                                If pp.m_pr.InteractionParameters(Compound2).ContainsKey(Compound1) Then
                                    ipdata(1, 2) = pp.m_pr.InteractionParameters(Compound2)(Compound1).kij
                                End If
                            End If
                        End If
                    ElseIf pp.m_pr.InteractionParameters.ContainsKey(Compound2) Then
                        If pp.m_pr.InteractionParameters(Compound2).ContainsKey(Compound1) Then
                            ipdata(1, 2) = pp.m_pr.InteractionParameters(Compound2)(Compound1).kij
                        End If
                    End If
                    pp.Dispose()
                    pp = Nothing
                Case "Lee-Kesler-Plöcker"
                    Dim pp As LKPPropertyPackage = proppack
                    If pp.m_pr.InteractionParameters.ContainsKey(Compound1) Then
                        If pp.m_pr.InteractionParameters(Compound1).ContainsKey(Compound2) Then
                            ipdata(1, 2) = pp.m_pr.InteractionParameters(Compound1)(Compound2).kij
                        Else
                            If pp.m_pr.InteractionParameters.ContainsKey(Compound2) Then
                                If pp.m_pr.InteractionParameters(Compound2).ContainsKey(Compound1) Then
                                    ipdata(1, 2) = pp.m_pr.InteractionParameters(Compound2)(Compound1).kij
                                End If
                            End If
                        End If
                    ElseIf pp.m_pr.InteractionParameters.ContainsKey(Compound2) Then
                        If pp.m_pr.InteractionParameters(Compound2).ContainsKey(Compound1) Then
                            ipdata(1, 2) = pp.m_pr.InteractionParameters(Compound2)(Compound1).kij
                        End If
                    End If
                    pp.Dispose()
                    pp = Nothing
                Case "NRTL"
                    Dim pp As NRTLPropertyPackage = proppack
                    If pp.m_uni.InteractionParameters.ContainsKey(Compound1) Then
                        If pp.m_uni.InteractionParameters(Compound1).ContainsKey(Compound2) Then
                            ipdata(1, 2) = pp.m_uni.InteractionParameters(Compound1)(Compound2).A12
                            ipdata(1, 3) = pp.m_uni.InteractionParameters(Compound1)(Compound2).A21
                            ipdata(1, 4) = pp.m_uni.InteractionParameters(Compound1)(Compound2).B12
                            ipdata(1, 5) = pp.m_uni.InteractionParameters(Compound1)(Compound2).B21
                            ipdata(1, 6) = pp.m_uni.InteractionParameters(Compound1)(Compound2).C12
                            ipdata(1, 7) = pp.m_uni.InteractionParameters(Compound1)(Compound2).C21
                            ipdata(1, 8) = pp.m_uni.InteractionParameters(Compound1)(Compound2).alpha12
                        Else
                            If pp.m_uni.InteractionParameters.ContainsKey(Compound2) Then
                                If pp.m_uni.InteractionParameters(Compound2).ContainsKey(Compound1) Then
                                    ipdata(1, 2) = pp.m_uni.InteractionParameters(Compound2)(Compound1).A21
                                    ipdata(1, 3) = pp.m_uni.InteractionParameters(Compound2)(Compound1).A12
                                    ipdata(1, 4) = pp.m_uni.InteractionParameters(Compound2)(Compound1).B21
                                    ipdata(1, 5) = pp.m_uni.InteractionParameters(Compound2)(Compound1).B12
                                    ipdata(1, 6) = pp.m_uni.InteractionParameters(Compound2)(Compound1).C21
                                    ipdata(1, 7) = pp.m_uni.InteractionParameters(Compound2)(Compound1).C12
                                    ipdata(1, 8) = pp.m_uni.InteractionParameters(Compound2)(Compound1).alpha12
                                End If
                            End If
                        End If
                    ElseIf pp.m_uni.InteractionParameters.ContainsKey(Compound2) Then
                        If pp.m_uni.InteractionParameters(Compound2).ContainsKey(Compound1) Then
                            ipdata(1, 2) = pp.m_uni.InteractionParameters(Compound2)(Compound1).A21
                            ipdata(1, 3) = pp.m_uni.InteractionParameters(Compound2)(Compound1).A12
                            ipdata(1, 4) = pp.m_uni.InteractionParameters(Compound2)(Compound1).B21
                            ipdata(1, 5) = pp.m_uni.InteractionParameters(Compound2)(Compound1).B12
                            ipdata(1, 6) = pp.m_uni.InteractionParameters(Compound2)(Compound1).C21
                            ipdata(1, 7) = pp.m_uni.InteractionParameters(Compound2)(Compound1).C12
                            ipdata(1, 8) = pp.m_uni.InteractionParameters(Compound2)(Compound1).alpha12
                        End If
                    End If
                    pp.Dispose()
                    pp = Nothing
                Case "UNIQUAC"
                    Dim pp As UNIQUACPropertyPackage = proppack
                    If pp.m_uni.InteractionParameters.ContainsKey(Compound1) Then
                        If pp.m_uni.InteractionParameters(Compound1).ContainsKey(Compound2) Then
                            ipdata(1, 2) = pp.m_uni.InteractionParameters(Compound1)(Compound2).A12
                            ipdata(1, 3) = pp.m_uni.InteractionParameters(Compound1)(Compound2).A21
                            ipdata(1, 4) = pp.m_uni.InteractionParameters(Compound1)(Compound2).B12
                            ipdata(1, 5) = pp.m_uni.InteractionParameters(Compound1)(Compound2).B21
                            ipdata(1, 6) = pp.m_uni.InteractionParameters(Compound1)(Compound2).C12
                            ipdata(1, 7) = pp.m_uni.InteractionParameters(Compound1)(Compound2).C21
                        Else
                            If pp.m_uni.InteractionParameters.ContainsKey(Compound2) Then
                                If pp.m_uni.InteractionParameters(Compound2).ContainsKey(Compound1) Then
                                    ipdata(1, 2) = pp.m_uni.InteractionParameters(Compound2)(Compound1).A21
                                    ipdata(1, 3) = pp.m_uni.InteractionParameters(Compound2)(Compound1).A12
                                    ipdata(1, 4) = pp.m_uni.InteractionParameters(Compound2)(Compound1).B21
                                    ipdata(1, 5) = pp.m_uni.InteractionParameters(Compound2)(Compound1).B12
                                    ipdata(1, 6) = pp.m_uni.InteractionParameters(Compound2)(Compound1).C21
                                    ipdata(1, 7) = pp.m_uni.InteractionParameters(Compound2)(Compound1).C12
                                End If
                            End If
                        End If
                    ElseIf pp.m_uni.InteractionParameters.ContainsKey(Compound2) Then
                        If pp.m_uni.InteractionParameters(Compound2).ContainsKey(Compound1) Then
                            ipdata(1, 2) = pp.m_uni.InteractionParameters(Compound2)(Compound1).A21
                            ipdata(1, 3) = pp.m_uni.InteractionParameters(Compound2)(Compound1).A12
                            ipdata(1, 4) = pp.m_uni.InteractionParameters(Compound2)(Compound1).B21
                            ipdata(1, 5) = pp.m_uni.InteractionParameters(Compound2)(Compound1).B12
                            ipdata(1, 6) = pp.m_uni.InteractionParameters(Compound2)(Compound1).C21
                            ipdata(1, 7) = pp.m_uni.InteractionParameters(Compound2)(Compound1).C12
                        End If
                    End If
                    pp.Dispose()
                    pp = Nothing
                Case "Wilson"
                    Dim pp As WilsonPropertyPackage = proppack
                    If pp.WilsonM.BIPs.ContainsKey(Compound1) Then
                        If pp.WilsonM.BIPs(Compound1).ContainsKey(Compound2) Then
                            ipdata(1, 2) = pp.WilsonM.BIPs(Compound1)(Compound2)(0)
                            ipdata(1, 3) = pp.WilsonM.BIPs(Compound1)(Compound2)(1)
                        Else
                            If pp.WilsonM.BIPs.ContainsKey(Compound2) Then
                                If pp.WilsonM.BIPs(Compound2).ContainsKey(Compound1) Then
                                    ipdata(1, 2) = pp.WilsonM.BIPs(Compound2)(Compound1)(1)
                                    ipdata(1, 3) = pp.WilsonM.BIPs(Compound2)(Compound1)(0)
                                End If
                            End If
                        End If
                    ElseIf pp.WilsonM.BIPs.ContainsKey(Compound2) Then
                        If pp.WilsonM.BIPs(Compound2).ContainsKey(Compound1) Then
                            ipdata(1, 2) = pp.WilsonM.BIPs(Compound2)(Compound1)(1)
                            ipdata(1, 3) = pp.WilsonM.BIPs(Compound2)(Compound1)(0)
                        End If
                    End If
                    pp.Dispose()
                    pp = Nothing
            End Select

            Return ipdata

        End Function

    End Class

End Namespace
