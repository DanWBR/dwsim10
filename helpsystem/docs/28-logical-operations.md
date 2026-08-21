# Logical Operations

#### Recycle

Principle of Operation

At each iteration k, the Recycle block reads the inlet stream properties (coming from downstream) and compares them against the outlet stream properties (going upstream). The convergence errors are defined as:



\[
\varepsilon_{T}^{(k)}=T_{\mathrm{in}}^{(k)}-T_{\mathrm{out}}^{(k)}
\]




\[
\varepsilon_{P}^{(k)}=P_{\mathrm{in}}^{(k)}-P_{\mathrm{out}}^{(k)}
\]




\[
\varepsilon_{W}^{(k)}=\sum_{i=1}^{N_{c}}\left|\dot{m}_{i,\mathrm{in}}^{(k)}-\dot{m}_{i,\mathrm{out}}^{(k)}\right|
\]


where $T$ is temperature, $P$ is pressure, $m_{i}$ is the mass flow rate of component i, and $N_{c}$ is the number of components.

Convergence is achieved when all errors fall below their respective tolerances simultaneously:



\[
\left|\varepsilon_{T}^{(k)}\right|\leq\delta_{T}\quad\wedge\quad\left|\varepsilon_{P}^{(k)}\right|\leq\delta_{P}\quad\wedge\quad\left|\varepsilon_{W}^{(k)}\right|\leq\delta_{W}
\]


##### Smoothing (Non-Legacy Mode)

When Legacy Mode is disabled, the Recycle block applies a smoothing factor alpha to dampen oscillations:



\[
T_{\mathrm{out}}^{(k+1)}=\alpha\,T_{\mathrm{in}}^{(k)}+(1-\alpha)\,T_{\mathrm{in}}^{(k-1)}
\]




\[
P_{\mathrm{out}}^{(k+1)}=\alpha\,P_{\mathrm{in}}^{(k)}+(1-\alpha)\,P_{\mathrm{in}}^{(k-1)}
\]




\[
\dot{m}_{i,\mathrm{out}}^{(k+1)}=\alpha\,\dot{m}_{i,\mathrm{in}}^{(k)}+(1-\alpha)\,\dot{m}_{i,\mathrm{out}}^{(k)}
\]


where alpha is in (0, 1\] (default alpha = 1.0, equivalent to direct substitution).

##### Parameters

| **Parameter**         | **Symbol**     | **Default** | **Unit** |
|:----------------------|:---------------|:------------|:---------|
| Temperature Tolerance | $\delta_{T}$ | 0.1         | K        |
| Pressure Tolerance    | $\delta_{P}$ | 0.1         | Pa       |
| Mass Flow Tolerance   | $\delta_{W}$ | 0.01        | kg/s     |
| Maximum Iterations    | $N_{\max}$   | 50          | –        |
| Smoothing Factor      | $\alpha$     | 1.0         | –        |

Recycle Block Parameters

| **Parameter** | **Default** | **Description** |
|:---|:---|:---|
| Acceleration Frequency | 4 | Apply acceleration every $n$ iterations |
| Acceleration Delay | 2 | Initial iterations before acceleration begins |
| $q_{\max}$ | 0 | Upper bound for the Wegstein $q$ factor |
| $q_{\min}$ | $-20$ | Lower bound for the Wegstein $q$ factor |

Wegstein Acceleration Parameters

| **Property**      | **Description**                                 |
|:------------------|:------------------------------------------------|
| Temperature Error | $\left|\varepsilon_{T}\right|$ at convergence |
| Pressure Error    | $\left|\varepsilon_{P}\right|$ at convergence |
| Mass Flow Error   | $\left|\varepsilon_{W}\right|$ at convergence |
| Iterations Taken  | Number of iterations to converge                |

Recycle Block Output Properties

#### Energy Recycle

##### Overview {#overview-44}

The Energy Recycle logical block is analogous to the Recycle block but operates on energy streams instead of material streams. It is used when an energy stream from a downstream unit must feed back to an upstream unit.

##### Principle of Operation

The block compares the energy flow of the inlet and outlet energy streams. The convergence error is:



\[
\varepsilon_{E}^{(k)}=\dot{E}_{\mathrm{in}}^{(k)}-\dot{E}_{\mathrm{out}}^{(k-1)}
\]


Convergence is achieved when:



\[
\left|\varepsilon_{E}^{(k)}\right|\leq\delta_{E}
\]


##### Wegstein Acceleration

The Wegstein acceleration method is available and applied identically to the material Recycle case, but operating on the single energy flow variable:



\[
s_{E}^{(k)}=\frac{\varepsilon_{E}^{(k)}-\varepsilon_{E}^{(k-1)}}{\dot{E}^{(k)}-\dot{E}^{(k-1)}}
\]




\[
q_{E}^{(k)}=\frac{s_{E}^{(k)}}{s_{E}^{(k)}-1}
\]




\[
\dot{E}_{\mathrm{out}}^{(k+1)}=\varepsilon_{E}^{(k)}\left(1-q_{E}^{(k)}\right)+\dot{E}^{(k)}\,q_{E}^{(k)}
\]


The same bounding conditions on $q_{E}$ and the delay/frequency parameters apply as in the material Recycle block.

##### Parameters

| **Parameter**      | **Symbol**     | **Default** | **Unit** |
|:-------------------|:---------------|:------------|:---------|
| Energy Tolerance   | $\delta_{E}$ | 0.1         | kW       |
| Maximum Iterations | $N_{\max}$   | 100         | –        |

Energy Recycle Block Parameters

#### Adjust

##### Overview {#overview-45}

The Adjust logical block implements a feedback controller that manipulates a variable in one object to drive a controlled variable in another object to a desired set point. It is conceptually equivalent to a single-loop controller and can be used, for example, to adjust a heater duty until a stream reaches a target temperature.

Definitions

The Adjust block involves three objects:

| **Role** | **Description** |
|:---|:---|
| Manipulated Variable (MV) | The variable that the solver modifies (e.g., heat duty) |
| Controlled Variable (CV) | The variable driven toward the target (e.g., outlet temperature) |
| Reference Variable (RV) | Optional. When referenced, the target becomes $\mathrm{RV}+\Delta$ |

Adjust Block Object Roles

##### Objective

The solver seeks to satisfy CV = Set Point, where the set point is defined as:



\[
\mathrm{Set\;Point}=\begin{cases}
V_{\mathrm{adj}} & \text{if no reference object is used}\\
\mathrm{RV}+V_{\mathrm{adj}} & \text{if a reference object is used}
\end{cases}
\]


and $V_{a}dj$ is the user-specified adjust value.

Parameters

| **Parameter** | **Default** | **Description** |
|:---|:---|:---|
| Adjust Value ($V_{\mathrm{adj}}$) | 1.0 | Target value or offset from reference |
| Step Size | 0.1 | Perturbation step for numerical derivatives |
| Tolerance | 0.0001 | Convergence tolerance for $\left|\mathrm{CV}-\mathrm{Set\;Point}\right|$ |
| Maximum Iterations | 10 | Maximum solver iterations |
| Minimum Value | – | Optional lower bound for the manipulated variable |
| Maximum Value | – | Optional upper bound for the manipulated variable |

Adjust Block Parameters

##### Simultaneous Adjust Mode

When enabled, multiple Adjust blocks are solved simultaneously as a system of equations rather than sequentially. This is recommended when the manipulated variables of different Adjust blocks interact with each other (e.g., two controllers affecting the same unit operation).

#### Specification (Spec)

##### Overview {#overview-46}

The Specification (Spec) logical block establishes an algebraic relationship between a source variable and a target variable using a user-defined mathematical expression. Unlike the Adjust block (which iterates), the Spec block directly computes and assigns the target variable value from the expression.

##### Expression Evaluation

The user defines an expression f(X, Y) where X is the current value of the source variable (read-only) and Y is the current value of the target variable (before assignment). The target variable is then set to:



\[
Y_{\mathrm{new}}=f(X,Y)
\]


The expression supports all standard mathematical functions from System.Math, including Abs, Sqrt, Log, Log10, Exp, Sin, Cos, Tan, Pow, Min, Max, among others.

##### Example Expressions {#example-expressions}

| **Expression** | **Meaning** |
|:---|:---|
| `X` | Target equals source directly |
| `X ``*`` 1.05` | Target is 5% higher than source |
| `X + 10` | Target is source plus 10 (in display units) |
| `Sqrt(X ``*`` Y)` | Target is the geometric mean of source and previous target |
| `Max(X, 300)` | Target is at least 300 |

Example Spec Expressions

##### Value Clamping

Optional minimum and maximum bounds can be specified. When bounds are active, the assigned value is clamped:



\[
Y_{\mathrm{new}}=\begin{cases}
Y_{\min} & \text{if }f(X,Y)<Y_{\min}\\
Y_{\max} & \text{if }f(X,Y)>Y_{\max}\\
f(X,Y) & \text{otherwise}
\end{cases}
\]


##### Parameters

| **Parameter**                | **Description**                           |
|:-----------------------------|:------------------------------------------|
| Source Object / Property     | The object and property to read as $X$  |
| Target Object / Property     | The object and property to write as $Y$ |
| Expression                   | Mathematical expression $f(X,Y)$        |
| Minimum Value ($Y_{\min}$) | Optional lower bound for the target       |
| Maximum Value ($Y_{\max}$) | Optional upper bound for the target       |

Spec Block Parameters

#### Information Carrier

##### Overview {#overview-47}

The Information Carrier logical block transfers a property value from a source object to up to three target objects. It is used to propagate information across the flowsheet without requiring a physical stream connection, enabling non-standard data flows between unit operations.

##### Configuration

| **Parameter**              | **Description**                                |
|:---------------------------|:-----------------------------------------------|
| Source Object / Property   | The object and property to read                |
| Target Object 1 / Property | First target: the object and property to write |
| Target Object 2 / Property | Second target (optional)                       |
| Target Object 3 / Property | Third target (optional)                        |

Information Carrier Block Configuration

##### Behavior

The Information Carrier reads the specified property from the source object and writes it directly to the corresponding property of each configured target object. No mathematical transformation is applied. This block supports both steady-state and dynamic simulation modes.

##### Summary

| **Block** | **Stream Type** | **Purpose** | **Dynamic Support** |
|:---|:---|:---|:---|
| Recycle | Material | Converge material recycle loops | Yes |
| Energy Recycle | Energy | Converge energy recycle loops | Yes |
| Adjust | – | Feedback control (MV $\to$ CV) | No |
| Spec | – | Algebraic variable assignment | Yes |
| Information Carrier | – | Property propagation to multiple targets | Yes |

DWSIM Logical Blocks Summary

