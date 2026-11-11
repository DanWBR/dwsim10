# Advanced Equation-of-State Packages

The advanced equation-of-state packages (Patel-Teja, Schmidt-Wenzel, PC-SAFT, Simplified PC-SAFT, SAFT-VR Mie, SAFT-VRQ Mie, and the Cubic-Plus-Association variants PR-CPA and SRK-CPA) expose analytical temperature and mole-number derivatives of the logarithmic fugacity coefficient directly from their residual Helmholtz-energy formulation, differentiated at constant pressure and, for the composition derivative, at constant temperature and pressure. These derivatives are wired into the same K-value derivative interface as the native packages, so the flash and column solvers use them automatically. The multiparameter reference equations are pure-component models, so composition derivatives do not apply to them.

