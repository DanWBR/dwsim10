This document provides a comprehensive guide to configuring, executing, and analyzing steady-state and dynamic process simulations in DWSIM. The material is organized to follow the typical simulation workflow: selecting thermodynamic models, defining the flowsheet topology, specifying operating conditions, solving the system of equations, and interpreting the results. Each step is illustrated with annotated screenshots and detailed descriptions of the relevant interface elements.

DWSIM has two Graphical User Interfaces: Classic UI and Cross-Platform UI.

- The Classic UI is built on the **Windows Forms** graphical class library ([link](https://en.wikipedia.org/wiki/Windows_Forms)), the original interface used since DWSIM’s earliest releases. Because Windows Forms is a native Windows toolkit, this interface is available only on Microsoft Windows.




![Classic UI on Windows 10 + .NET Framework 4.8.](images/screens100/15.png)

*Classic UI on Windows 10 + .NET Framework 4.8.*



- The Cross-Platform UI is built on **Avalonia** ([link](https://avaloniaui.net/)), a cross-platform UI framework for .NET which runs on Windows, Linux and macOS, on both x64 and ARM64 processors. Unlike a toolkit wrapper, Avalonia does not delegate to the operating system’s own widgets: every control is drawn by Avalonia itself on a *Skia* ([link](https://skia.org/)) canvas, which is the same graphics library DWSIM already uses to draw the flowsheet. The interface therefore looks and behaves the same way on the three operating systems, and this manual describes it only once.

Up to DWSIM 9 the Cross-Platform UI was based on Eto.Forms and rendered through WPF on Windows, GTK on Linux and Cocoa on macOS. DWSIM 10 replaces that layer with Avalonia. The switch changed the way the windows are laid out - simulations are now tabbed documents inside a single dockable shell rather than separate top-level windows - and closed the remaining feature gaps against the Classic UI.

On Windows both interfaces are installed and either one may be used; the Classic UI opens by default. On Linux and macOS the Cross-Platform UI is the only interface available. It is distributed as a self-contained application: the .NET runtime travels with it, so neither Mono nor a separate runtime installation is required.

