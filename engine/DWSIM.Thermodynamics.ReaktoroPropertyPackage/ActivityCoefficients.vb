Imports System.IO
Imports DWSIM.Thermodynamics.PropertyPackages
Imports DWSIM.ExtensionMethods
Imports DWSIM.GlobalSettings

Public Class ActivityCoefficients

    Public Overloads Function Calculate(Vx As Double(), T As Double, P As Double, pp As PropertyPackage) As Double()

        If Vx.SumY = 0.0 Then Return pp.RET_UnitaryVector()

        Dim i As Integer

        Dim CompProps = pp.DW_GetConstantProperties()
        Dim saltonly As Boolean = False
        For i = 0 To Vx.Length - 1
            If Vx(i) > 0 And CompProps(i).IsSalt Then
                saltonly = True
            ElseIf Vx(i) > 0 And Not CompProps(i).IsSalt Then
                saltonly = False
                Exit For
            End If
        Next

        If saltonly Then Return pp.RET_UnitaryVector()

        Dim CompoundMaps = New CompoundMapper()
        Dim Setschenow As New SetschenowCoefficients()

        Dim n As Integer = Vx.Length - 1
        Dim activcoeff(n) As Double

        Dim names = pp.RET_VNAMES().ToList
        Dim formulas As New List(Of String)

        For Each na In names
            If Not CompoundMaps.Maps.ContainsKey(na) Then
                'Throw New Exception(String.Format("Compound {0} is not supported by this Property Package [{1}].", na, pp.ComponentName))
            End If
        Next

        Dim speciesPhases As New Dictionary(Of String, String)
        Dim speciesAmounts As New Dictionary(Of String, Double)
        Dim speciesAmountsFinal As New Dictionary(Of String, Double)
        Dim compoundAmountsFinal As New Dictionary(Of String, Double)
        Dim inverseMaps As New Dictionary(Of String, String)

        Dim aqueous As String = ""

        i = 0
        For Each na In names
            formulas.Add(CompoundMaps.Maps(na).Formula)
            speciesAmounts.Add(CompoundMaps.Maps(na).Formula, Vx(i))
            If CompoundMaps.Maps(na).AqueousName <> "" Then
                aqueous += CompoundMaps.Maps(na).AqueousName + " "
                speciesPhases.Add(CompoundMaps.Maps(na).AqueousName, "L")
                inverseMaps.Add(CompoundMaps.Maps(na).AqueousName, CompoundMaps.Maps(na).Formula)
            Else
                speciesPhases.Add(CompoundMaps.Maps(na).AqueousName, "")
            End If
            i += 1
        Next
        aqueous = aqueous.TrimEnd()

        Try

            ' No gaseous phase: this is the activity coefficient of the aqueous solution, asked for
            ' at a composition DWSIM already has, so there is nothing to equilibrate.
            Using system = Reaktoro.CreateSystem(aqueous, "")

                ' One amount per species, in the order the system holds them. The old code passed
                ' the compound amounts straight through, which lined up only as long as every
                ' compound had an aqueous species and the two lists happened to agree.
                Dim amounts = New Double(system.SpeciesCount - 1) {}

                For j = 0 To system.SpeciesCount - 1
                    Dim species = system.SpeciesNames(j)
                    If inverseMaps.ContainsKey(species) Then
                        amounts(j) = speciesAmounts(inverseMaps(species))
                    End If
                Next

                Dim ac = system.LnActivityCoefficients(T, P, amounts)

                ' The logarithm goes in and ExpY below takes it out, once. What was here stored
                ' Exp(ac) and then let ExpY exponentiate that as well, so every coefficient this
                ' path produced was Exp(Exp(ln gamma)); the flash alongside it, which does the same
                ' work, exponentiates once. A species that never gets a value keeps its zero, which
                ' is what ExpY is for: it comes out as one.
                For j = 0 To system.SpeciesCount - 1
                    Dim species = system.SpeciesNames(j)
                    If inverseMaps.ContainsKey(species) AndAlso speciesPhases(species) = "L" Then
                        activcoeff(formulas.IndexOf(inverseMaps(species))) = ac(j)
                    End If
                Next

            End Using

        Catch ex As Exception

            pp.Flowsheet?.ShowMessage("Reaktoro error: " + ex.Message, DWSIM.Interfaces.IFlowsheet.MessageType.GeneralError)
            Throw

        End Try

        Return activcoeff.ExpY()

    End Function


End Class
