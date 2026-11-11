# Python Scripting

DWSIM’s scripting subsystem allows the user to automate tasks, extend calculation logic, and interact programmatically with the simulation through Python scripts. Scripts have full access to all flowsheet objects, the solver, and the simulation environment.

Typical uses include manipulating flowsheet objects, performing property-package calculations outside the normal solver sequence, reading data from external sources, running parametric studies by varying properties between solver calls, and post-processing results.

The script blocks can be associated with events in the flowsheet, i.e. they can run when a specific object is calculated or when an error occurs in the calculation. A script can also run when the simulation is opened, closed and saved.




![Python Script Manager (Classic UI)](images/screens67/pythonscripting.png)

*Python Script Manager (Classic UI)*



#### Python Interpreters

DWSIM uses two Python interpreters: **IronPython** and **Python.NET**.

IronPython is a .NET implementation of the Python language embedded directly in DWSIM. It ships with a standard library corresponding to Python 2.7, but scripts can also access the full .NET Framework class library (e.g., System.IO for file operations) directly, which is often preferable.

Python.NET links DWSIM to a Python installation on your machine. It runs your scripts using that external Python environment.

Unless a specific external Python package (e.g., NumPy, SciPy, TensorFlow, Matplotlib) is required—which is the primary reason the Python.NET interpreter exists—IronPython is the recommended choice because it offers significantly faster execution and tighter integration with DWSIM’s internal object model.

#### IronPython Interactive Console (Classic UI)

The IronPython Interactive Console provides a REPL (Read-Eval-Print Loop) for real-time interaction with the flowsheet. Users can modify stream or unit-operation parameters, trigger solver calculations, and inspect results interactively. This console is particularly useful during dynamic simulations, as it allows variables to be changed while the integrator is running and the effect on other flowsheet variables to be observed immediately.

**[Intellisense](https://en.wikipedia.org/wiki/Intelligent_code_completion#Visual_Studio)** is available in the IronPython Interactive Console.




![IronPython Interactive Console (Classic UI)](images/screens67/interactiveconsole.png)

*IronPython Interactive Console (Classic UI)*



##### Available Functions







solve()



Requests a Flowsheet calculation.







save()



Saves the current flowsheet to its currently associated file.







apihelp()



Opens the API Help page in a new browser window.

##### Changing object properties

When accessing flowsheet objects from the console, remove white spaces and underlines from the object name, i.e., to change the value of the temperature of a Material Stream named *’MSTR-01*’, do the following:

    MSTR01.SetTemperature(400)

The above will set the temperature of MSTR-01 to 400 K.

You can also set a stream variable with specific units, i.e.

    MSTR01.SetPressure('10 bar')

The above will set the pressure of MSTR-01 to 10 bar or 1000000 Pa.

