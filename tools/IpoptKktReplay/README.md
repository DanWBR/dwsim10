# ipopt-kkt-replay

Replays KKT systems captured from a native Ipopt run through the managed sparse solver in
`engine/DWSIM.Numerics.Ipopt.Sparse`, and generates synthetic ones. It exists so the managed
linear algebra can be checked against the systems Ipopt actually produces, rather than only
against textbook matrices.

```
dotnet run --project tools/IpoptKktReplay -- --help
```

`native/` holds the C++ `KktDumpSolverInterface` that captures those systems from native Ipopt:
it is a linear-solver interface that writes each system to disk before delegating. It is shelved,
not wired into any build here, and is kept because writing it again would be the expensive part.
