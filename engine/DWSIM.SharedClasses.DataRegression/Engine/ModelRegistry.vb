Imports DWSIM.SharedClasses.DataRegression.Models

Namespace Global.DWSIM.SharedClasses.DataRegression.Engine

    Public Module ModelRegistry

        Private ReadOnly _models As Dictionary(Of String, ModelDefinition) = BuildRegistry()

        Public Function GetDefinition(modelName As String) As ModelDefinition
            Dim def As ModelDefinition = Nothing
            _models.TryGetValue(modelName, def)
            Return def
        End Function

        Public Function Contains(modelName As String) As Boolean
            Return _models.ContainsKey(modelName)
        End Function

        Public Function AllModels() As IEnumerable(Of ModelDefinition)
            Return _models.Values
        End Function

        Private Function BuildRegistry() As Dictionary(Of String, ModelDefinition)
            Dim r As New Dictionary(Of String, ModelDefinition)

            r("Peng-Robinson") = New ModelDefinition With {
                .Name = "Peng-Robinson",
                .PropertyPackageName = "Peng-Robinson (PR)",
                .DefaultRows = {New ParameterRow("kij", -0.5, 0.0, 0.5, False)},
                .AllowEstimators = False,
                .AllowIdealVaporOption = False,
                .AllowTDepRegression = False,
                .ResetTDepCheckedOnSelect = True
            }

            r("Soave-Redlich-Kwong") = New ModelDefinition With {
                .Name = "Soave-Redlich-Kwong",
                .PropertyPackageName = "Soave-Redlich-Kwong (SRK)",
                .DefaultRows = {New ParameterRow("kij", -0.5, 0.0, 0.5, False)},
                .AllowEstimators = False,
                .AllowIdealVaporOption = False,
                .AllowTDepRegression = False,
                .ResetTDepCheckedOnSelect = True
            }

            r("Lee-Kesler-Plöcker") = New ModelDefinition With {
                .Name = "Lee-Kesler-Plöcker",
                .PropertyPackageName = "Lee-Kesler-Plöcker",
                .DefaultRows = {New ParameterRow("kij", 0.9, 1.0, 1.1, False)},
                .AllowEstimators = False,
                .AllowIdealVaporOption = False,
                .AllowTDepRegression = False,
                .ResetTDepCheckedOnSelect = True
            }

            Dim prsv2Rows = {
                New ParameterRow("kij", -0.5, 0.0, 0.5, False),
                New ParameterRow("kji", -0.5, 0.0, 0.5, False)
            }

            r("PRSV2-M") = New ModelDefinition With {
                .Name = "PRSV2-M",
                .PropertyPackageName = "Peng-Robinson-Stryjek-Vera 2 (PRSV2-M)",
                .DefaultRows = prsv2Rows,
                .AllowEstimators = False,
                .AllowIdealVaporOption = False,
                .AllowTDepRegression = False,
                .ResetTDepCheckedOnSelect = True
            }

            r("PRSV2-VL") = New ModelDefinition With {
                .Name = "PRSV2-VL",
                .PropertyPackageName = "Peng-Robinson-Stryjek-Vera 2 (PRSV2-VL)",
                .DefaultRows = prsv2Rows,
                .AllowEstimators = False,
                .AllowIdealVaporOption = False,
                .AllowTDepRegression = False,
                .ResetTDepCheckedOnSelect = True
            }

            Dim activityRows2 = {
                New ParameterRow("A12 (cal/mol)", -5000.0, 0.0, 5000.0, False),
                New ParameterRow("A21 (cal/mol)", -5000.0, 0.0, 5000.0, False)
            }

            r("UNIQUAC") = New ModelDefinition With {
                .Name = "UNIQUAC",
                .PropertyPackageName = "UNIQUAC",
                .DefaultRows = activityRows2,
                .AllowEstimators = True,
                .AllowIdealVaporOption = True,
                .AllowTDepRegression = True,
                .ResetTDepCheckedOnSelect = False
            }

            r("Wilson") = New ModelDefinition With {
                .Name = "Wilson",
                .PropertyPackageName = "Wilson",
                .DefaultRows = activityRows2,
                .AllowEstimators = True,
                .AllowIdealVaporOption = True,
                .AllowTDepRegression = False,
                .ResetTDepCheckedOnSelect = False
            }

            r("NRTL") = New ModelDefinition With {
                .Name = "NRTL",
                .PropertyPackageName = "NRTL",
                .DefaultRows = {
                    New ParameterRow("A12 (cal/mol)", -5000.0, 0.0, 5000.0, False),
                    New ParameterRow("A21 (cal/mol)", -5000.0, 0.0, 5000.0, False),
                    New ParameterRow("alpha12", 0.0, 0.3, 0.8, False)
                },
                .AllowEstimators = True,
                .AllowIdealVaporOption = True,
                .AllowTDepRegression = True,
                .ResetTDepCheckedOnSelect = False
            }

            Return r
        End Function

    End Module

End Namespace
