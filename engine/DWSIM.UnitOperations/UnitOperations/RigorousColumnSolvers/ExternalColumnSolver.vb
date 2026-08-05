Imports DWSIM.UnitOperations.UnitOperations
Imports DWSIM.UnitOperations.UnitOperations.Auxiliary.SepOps

''' <summary>
''' Defines a provider that supplies initial estimates (temperatures, flows, compositions)
''' for a rigorous column solver from an external source.
''' </summary>
Public Interface IExternalColumnInitialEstimatesProvider

    ''' <summary>
    ''' Computes and returns initial estimates for the given column.
    ''' </summary>
    ''' <param name="column">The column for which initial estimates are needed.</param>
    ''' <returns>A <see cref="ColumnSolverInputData"/> containing initial temperature, flow, and composition profiles.</returns>
    Function GetInitialEstimates(column As Column) As ColumnSolverInputData

End Interface

''' <summary>
''' Defines an external solver that can solve a rigorous distillation or absorption column
''' given a set of initial estimates, returning converged stage-by-stage results.
''' </summary>
Public Interface IExternalColumnSolver

    ''' <summary>
    ''' Solves the column to convergence using the supplied initial estimates.
    ''' </summary>
    ''' <param name="column">The column to solve.</param>
    ''' <param name="initialestimates">Initial temperature, flow, and composition profiles.</param>
    ''' <returns>A <see cref="ColumnSolverOutputData"/> containing the converged results.</returns>
    Function SolveColumn(column As Column, initialestimates As ColumnSolverInputData) As ColumnSolverOutputData

End Interface
