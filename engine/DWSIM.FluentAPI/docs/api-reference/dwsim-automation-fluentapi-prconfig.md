# PRConfig

`DWSIM.Automation.FluentAPI.PRConfig`

Peng-Robinson (PR) interaction-parameter setter. Also covers PR78 and PRSV2 since they share the same `m_pr.InteractionParameters` shape.

## Methods

### `WithKij(string, string, double)`

Sets `kij` for the (compound1, compound2) binary. Symmetric: also writes the reverse entry.
