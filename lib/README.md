# lib

Managed assemblies that are not on NuGet.

## CapeOpen.dll, Interop.CAPEOPEN110.dll

The CAPE-OPEN interfaces, from CO-LaN. Every property package and the material stream
implement them, so they are not optional at compile time. They are a Windows and COM standard:
the types load and the interfaces compile anywhere, but activating a CAPE-OPEN component only
works on Windows. `CapeOpen.dll` also carries a few WinForms parameter editors, which is why it
names System.Windows.Forms; nothing DWSIM touches reaches them, and the assembly loads on
net10.0 without it.

## Microsoft.Research.Oslo.dll

The Open Solving Library for ODEs, from Microsoft Research. The plug flow reactor integrates
its concentration profile with its RK45. Pure managed, references only mscorlib and System.Core.

## AODL.dll

An OpenDocument writer, used to save the flowsheet report as an .odt. It names System.Drawing
for a picture type the report never builds; writing a text document with tables works on
net10.0, which is what the report does.
