# Dynamics.SeriesDecimator

`DWSIM.Automation.FluentAPI.Dynamics.SeriesDecimator`

Reduces a time series to a handful of points that still look like the original.

## Remarks

A dynamic run easily produces thousands of steps, which no chat interface — and no language model's context — wants in full. Averaging would flatten exactly what matters in a transient: the overshoot peak and any oscillation. This uses largest-triangle-three-buckets, which selects real samples by visual significance, and then forces the extremes back in.

## Methods

### `Decimate(Collections.Generic.IReadOnlyList{double}, Collections.Generic.IReadOnlyList{double}, int)`

Decimates a series and returns the selected (time, value) pairs.

### `Format(double)`

Formats a number for transport: six significant digits, invariant culture. Enough precision to reason about, short enough not to waste a context window.

### `Preview(DynamicsSeries, int, Nullable{double}, Nullable{double})`

Decimates a series to a preview, honouring an optional time window.

**Parameters**

- `series` — The series to sample.
- `maxPoints` — Point budget; clamped to at least 3 and at most 400.
- `startSeconds` — Lower time bound, inclusive; null for the start of the run.
- `endSeconds` — Upper time bound, inclusive; null for the end of the run.

### `SelectIndices(Collections.Generic.IReadOnlyList{double}, Collections.Generic.IReadOnlyList{double}, int)`

Picks at most `maxPoints` samples from the series, preserving its shape, its first and last points and its minimum and maximum.

**Returns**: The indices of the selected samples, in ascending order.
