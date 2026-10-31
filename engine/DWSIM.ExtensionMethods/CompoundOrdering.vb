Imports DWSIM.Interfaces
Imports DWSIM.Interfaces.Enums

Public Module CompoundOrderingExtensions

    ''' <summary>
    ''' Sorts compounds as the flowsheet displays them (<see cref="IFlowsheetOptions.CompoundOrderingMode"/>).
    ''' As Added keeps the input order; Custom follows <see cref="IFlowsheetOptions.CustomCompoundOrder"/>,
    ''' with the compounds missing from that list at the end, in input order.
    ''' </summary>
    ''' <param name="items">The compounds, in any representation.</param>
    ''' <param name="constprops">Returns the constant properties of an item.</param>
    ''' <param name="options">The flowsheet options; Nothing keeps the input order.</param>
    <System.Runtime.CompilerServices.Extension()>
    Public Function InFlowsheetOrder(Of T)(items As IEnumerable(Of T), constprops As Func(Of T, ICompoundConstantProperties), options As IFlowsheetOptions) As IEnumerable(Of T)

        If options Is Nothing Then Return items

        Select Case options.CompoundOrderingMode
            Case CompoundOrdering.CAS_ASC
                Return items.OrderBy(Function(c) constprops(c).CAS_Number)
            Case CompoundOrdering.CAS_DESC
                Return items.OrderByDescending(Function(c) constprops(c).CAS_Number)
            Case CompoundOrdering.MW_ASC
                Return items.OrderBy(Function(c) constprops(c).Molar_Weight)
            Case CompoundOrdering.MW_DESC
                Return items.OrderByDescending(Function(c) constprops(c).Molar_Weight)
            Case CompoundOrdering.Name_ASC
                Return items.OrderBy(Function(c) constprops(c).Name)
            Case CompoundOrdering.Name_DESC
                Return items.OrderByDescending(Function(c) constprops(c).Name)
            Case CompoundOrdering.NBP_ASC
                Return items.OrderBy(Function(c) constprops(c).NBP.GetValueOrDefault)
            Case CompoundOrdering.NBP_DESC
                Return items.OrderByDescending(Function(c) constprops(c).NBP.GetValueOrDefault)
            Case CompoundOrdering.TAG_ASC
                Return items.OrderBy(Function(c) constprops(c).Tag)
            Case CompoundOrdering.TAG_DESC
                Return items.OrderByDescending(Function(c) constprops(c).Tag)
            Case CompoundOrdering.Custom
                Dim position As New Dictionary(Of String, Integer)
                If options.CustomCompoundOrder IsNot Nothing Then
                    For Each name In options.CustomCompoundOrder
                        If name IsNot Nothing AndAlso Not position.ContainsKey(name) Then position.Add(name, position.Count)
                    Next
                End If
                ' OrderBy is stable: compounds not in the list keep their relative order
                Return items.OrderBy(Function(c)
                                         Dim i As Integer
                                         Return If(position.TryGetValue(constprops(c).Name, i), i, Integer.MaxValue)
                                     End Function)
            Case Else
                Return items
        End Select

    End Function

End Module
