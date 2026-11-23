Imports DWSIM.Interfaces

Namespace GraphicObjects

    ''' <summary>
    ''' The placeholders of the text blocks that show the Watch panel items: {n:N} the property name,
    ''' {n:V} the value in the flowsheet number format and {n:U} the units, n being the item's position
    ''' (from 1) in the Watch panel.
    ''' </summary>
    Public Module TextVariables

        ''' <summary>Replaces the Watch panel placeholders of <paramref name="text"/>.</summary>
        Public Function Replace(flowsheet As IFlowsheet, text As String) As String

            If flowsheet Is Nothing OrElse String.IsNullOrEmpty(text) Then Return text
            If Not (text.Contains("{") AndAlso text.Contains("}")) Then Return text

            Dim newtext As String = text

            For i = 1 To flowsheet.WatchItems.Count
                Dim objID = flowsheet.WatchItems(i - 1).ObjID
                Dim propID = flowsheet.WatchItems(i - 1).PropID
                If flowsheet.SimulationObjects.ContainsKey(objID) Then
                    Dim units = flowsheet.SimulationObjects(objID).GetPropertyUnit(propID)
                    Dim name = flowsheet.GetTranslatedString(propID)
                    newtext = newtext.Replace("{" + i.ToString() + ":N}", name)
                    newtext = newtext.Replace("{" + i.ToString() + ":U}", units)
                    Dim value = flowsheet.SimulationObjects(objID).GetPropertyValue(propID)?.ToString()
                    Dim dval As Double
                    If Double.TryParse(value, dval) Then
                        newtext = newtext.Replace("{" + i.ToString() + ":V}", dval.ToString(flowsheet.FlowsheetOptions.NumberFormat))
                    Else
                        newtext = newtext.Replace("{" + i.ToString() + ":V}", value)
                    End If
                End If
            Next

            Return newtext

        End Function

    End Module

End Namespace
