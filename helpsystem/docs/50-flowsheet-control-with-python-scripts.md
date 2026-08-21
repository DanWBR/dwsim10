# Flowsheet Control with Python Scripts

#### Introduction

Let us study the effect of the pressure on the temperature profile of the Acetone Column created on the previous tutorial. We will use the **IronPython Scripting**, **Spreadsheet** and **Charts** features available in DWSIM to generate, organize and analyze the results.

#### DWSIM Model (Classic UI)

1.  Save the previous simulation with a different file name and remove everything from the flowsheet except the objects depicted on the following picture:

    


![Process Flowsheet Diagram](images/screens58/tut3/tut3-3.png)

*Process Flowsheet Diagram*



2.  Go to the **Script Manager** and enter the following script:

    


![Python Script](images/screens58/tut3/tut3-5.png)

*Python Script*



3.  Run the script asynchronously (this prevents DWSIM from freezing until the calculation finishes):

    


![Run Python Script (Async)](images/screens58/tut3/tut3-4.png)

*Run Python Script (Async)*



4.  Go to the **Spreadsheet**, select the entire data range, click with the right mouse button and select **Create 2D XY Chart from Selection**.

    


![Create new chart from selected spreadsheet data range](images/screens58/tut3/tut3-2.png)

*Create new chart from selected spreadsheet data range*



5.  View and configure the newly created chart as in the following picture:

    


![Pressure-Temperature dependence of the Acetone Column](images/screens58/tut3/tut3-6.png)

*Pressure-Temperature dependence of the Acetone Column*



6.  Analyze the results obtained and discuss them with your colleagues.

#### DWSIM Model (Cross-Platform UI)

1.  Save the previous simulation with a different file name and remove everything from the flowsheet except the objects depicted on the following picture:

    


![Process Flowsheet Diagram](images/screens58/tut3cp/tut3cp-1.png)

*Process Flowsheet Diagram*



2.  Go to the **Script Manager,** create a New Script, select it on the list and enter the following code:

    


![Python Script](images/screens58/tut3/tut3-5.png)

*Python Script*



3.  Run the script asynchronously (this prevents DWSIM from freezing until the calculation finishes):

    


![Run Python Script (Async)](images/screens58/tut3cp/tut3cp-6.png)

*Run Python Script (Async)*



4.  Go to the **Spreadsheet**, select the entire data range, click with the right mouse button and select **Create Chart from Selected Range**.

    


![Create new chart from selected spreadsheet data range](images/screens58/tut3cp/tut3cp-2.png)

*Create new chart from selected spreadsheet data range*



5.  View and configure the newly created chart as in the following picture:

    


![Pressure-Temperature dependence of the Acetone Column](images/screens58/tut3cp/tut3cp-3.png)

*Pressure-Temperature dependence of the Acetone Column*



6.  Analyze the results obtained and discuss them with your colleagues.

