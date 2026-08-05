# lib

Managed assemblies that are not on NuGet.

## CapeOpen.dll, Interop.CAPEOPEN110.dll

The CAPE-OPEN interfaces, from CO-LaN. Every property package and the material stream
implement them, so they are not optional at compile time. They are a Windows and COM standard:
the types load and the interfaces compile anywhere, but activating a CAPE-OPEN component only
works on Windows. `CapeOpen.dll` also carries a few WinForms parameter editors, which is why it
names System.Windows.Forms; nothing DWSIM touches reaches them, and the assembly loads on
net10.0 without it.
