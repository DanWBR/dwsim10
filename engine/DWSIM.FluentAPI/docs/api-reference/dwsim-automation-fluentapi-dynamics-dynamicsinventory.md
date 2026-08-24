# Dynamics.DynamicsInventory

`DWSIM.Automation.FluentAPI.Dynamics.DynamicsInventory`

What a flowsheet holds that matters to a dynamic simulation.

## Properties

### `CauseAndEffectMatrices`

Descriptions of the defined cause-and-effect matrices.

### `Controllers`

Every PID controller, with its wiring and tuning.

### `CurrentSchedule`

Description of the current schedule, empty when none is selected.

### `DynamicCapableObjects`

Objects that carry a dynamic model, and so contribute hold-up and lag.

### `DynamicModeEnabled`

Whether the flowsheet is currently in dynamic mode.

### `EventSets`

Descriptions of the defined event sets.

### `Indicators`

Tags of every indicator, which is what a cause-and-effect matrix reacts to.

### `Integrators`

Descriptions of the defined integrators.

### `Objects`

Every simulation object, with its dynamic capabilities.

### `Schedules`

Descriptions of the defined schedules.

### `StoredStates`

Names of the stored flowsheet states a schedule can start from.
