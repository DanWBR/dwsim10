# Builders.ReactionSetBuilder

`DWSIM.Automation.FluentAPI.Builders.ReactionSetBuilder`

Fluent builder for a reaction set. Add reactions to it via [`Add`](dwsim-automation-fluentapi-builders-reactionsetbuilder.md).

## Methods

### `Add(DWSIM.Interfaces.IReaction, int, bool)`

Adds an existing reaction to this set.

## Properties

### `Flowsheet`

The underlying DWSIM object / owning flowsheet - escape hatch for advanced use.

### `Id`

The reaction-set ID - used by reactor builders' `WithReactionSet(string)`.
