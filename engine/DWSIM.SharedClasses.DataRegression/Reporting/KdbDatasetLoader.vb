Namespace Global.DWSIM.SharedClasses.DataRegression.Reporting

    ''' <summary>
    ''' Converts a KDB binary-VLE dataset (KDBVLEDataSet) into a list of
    ''' RegressionDataPoint rows in the dataset's own units. Caller is
    ''' responsible for setting cbTunit/cbPunit to ds.Tunits/ds.Punits after
    ''' loading. The "swap" flag mirrors composition values when the user's
    ''' selected compound order is reversed relative to the dataset's.
    ''' </summary>
    Public Module KdbDatasetLoader

        Public Function ToRegressionPoints(ds As Global.DWSIM.Thermodynamics.Databases.KDBLink.KDBVLEDataSet,
                                           swap As Boolean) As List(Of RegressionDataPoint)
            Dim rows As New List(Of RegressionDataPoint)
            If ds Is Nothing Then Return rows

            For Each rec In ds.Data
                If swap Then
                    rows.Add(New RegressionDataPoint With {
                        .Use = True,
                        .X1 = 1.0 - rec.X,
                        .Y1 = 1.0 - rec.Y,
                        .T = rec.T,
                        .P = rec.P
                    })
                Else
                    rows.Add(New RegressionDataPoint With {
                        .Use = True,
                        .X1 = rec.X,
                        .Y1 = rec.Y,
                        .T = rec.T,
                        .P = rec.P
                    })
                End If
            Next

            Return rows
        End Function

    End Module

End Namespace
