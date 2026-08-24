# AI-Assisted Convergence Enhancer

#### Overview

The **AI-Assisted Convergence Enhancer** (ACE) is a machine-learning extension that improves the robustness and speed of thermodynamic calculations throughout the DWSIM flowsheet solver. The underlying technology is a set of fully connected feedforward artificial neural networks (ANNs) trained on convergence data collected during ordinary simulations. The trained networks are used to provide either improved initial estimates or complete fallback solutions for flash calculations and reactor equilibria that are difficult to converge by conventional iterative methods.

The system operates transparently alongside the standard solver: data are collected silently during each successful calculation, models are retrained automatically when enough data have accumulated, and improved estimates are injected into the solver (or used directly as solutions) with no action required from the user beyond selecting an assistance level. An optional anonymised data-sharing mechanism uploads local training data to a cloud server, from which globally improved models may be retrieved.

#### Extension Architecture

ACE is implemented as a DWSIM *extender* plug-in. Two components are registered at start-up:









The manager is initialised once per session; the status bar is attached to every flowsheet window and updates when a provider is invoked.

#### Assistance Levels

The degree of AI involvement is governed by a single integer parameter called the *assistance level*, which the user sets in the ACE settings form:









Levels 1–3 are recommended for most applications: the ANN accelerates convergence without sacrificing physical accuracy, because the iterative solver still enforces the rigorous thermodynamic model when possible. Levels 4 and 5 trade accuracy for speed and are intended for preliminary screening studies.

#### Solution Provider Cascade

When ACE is asked for an estimate or solution it queries a chain of *solution providers* in priority order. The first provider that returns a non-null result is accepted; subsequent providers are not called. The default priority order is:

1.  **Online Server** – retrieves a prediction from the cloud model server (requires internet access and authentication).

2.  **Local ANN** – evaluates a locally stored TensorFlow model trained on data collected on the current machine.

3.  **NeqSim** – delegates the calculation to the NeqSim  thermodynamics library.

4.  **ThermoPack** – delegates to the ThermoPack  equation-of-state library.

Each provider can be independently enabled or disabled in the settings form. If all enabled providers return null, the standard solver proceeds without an initial estimate.

Before querying any provider, ACE validates the input: requests containing any `NaN` value or compositions whose sum deviates from unity by more than 5 % are rejected and control is returned to the normal solver immediately.

#### Neural Network Architecture

Each local ANN is a fully connected feedforward network  with the following structure:

- **Input layer**: normalised scalar variables and per-component molar flows (see Section [3.9](#sec:convenhancer_operations)).

- **Hidden layers**: $L$ layers (configurable, $2 \le L \le 6$) where the number of neurons in layer $\ell$ is


<a id="eq:ace_neurons"></a>

\[
n_\ell = \left\lfloor \frac{n_1}{2^{\ell-1}} \right\rfloor,
                \qquad \ell = 1, \ldots, L
\]


  and $n_1$ is the user-specified neuron count on the first hidden layer (default 100). ReLU activation 


<a id="eq:ace_relu"></a>

\[
\sigma(z) = \max(0, z)
\]


  is applied after each hidden layer.

- **Output layer**: one neuron per output variable with a linear activation. All weights are initialised with a variance scaling initialiser (mode $=\,$`FAN_AVG`, uniform, factor $=\,$<!-- -->1).

#### Data Normalisation

All inputs and outputs are rescaled to the range $[-1, 1]$ before training and inference using min-max normalisation:


<a id="eq:ace_scale"></a>

\[
\tilde{x} = \frac{x - x_{\min}}{x_{\max} - x_{\min}} \times 2 - 1
\]


The per-feature extrema $x_{\min}$ and $x_{\max}$ are computed from the training set and stored as model metadata. At inference time, predictions $\hat{y}$ in $[-1, 1]$ are mapped back to physical units by the inverse transformation:


<a id="eq:ace_unscale"></a>

\[
\hat{y}_{\mathrm{phys}} = \frac{(\hat{y} + 1)}{2}
        \,(y_{\max} - y_{\min}) + y_{\min}
\]


#### Training Algorithm

The network is trained by minimising the mean squared error (MSE) loss over the training set using the Adam optimiser :


<a id="eq:ace_loss"></a>

\[
\mathcal{L} = \frac{1}{N_{\mathrm{train}}}\sum_{i=1}^{N_{\mathrm{train}}}
        \bigl\|\hat{\mathbf{y}}_i - \mathbf{y}_i\bigr\|^2
\]


where $\hat{\mathbf{y}}_i$ is the network prediction and $\mathbf{y}_i$ is the target vector for sample $i$. The dataset is partitioned into a training split (70 %) and a hold-out test split (30 %) prior to training. The mini-batch size is


<a id="eq:ace_batch"></a>

\[
B = \min\!\bigl(5000,\; \lfloor 0.5\,N_{\mathrm{train}} \rfloor\bigr)
\]


Training runs for up to $E_{\max}$ epochs (default 10 000). An early stopping criterion halts training if the relative improvement in MSE falls below a threshold $\varepsilon_{\mathrm{stop}}$ for ten consecutive evaluation epochs:


<a id="eq:ace_earlystop"></a>

\[
\frac{|\mathcal{L}_e - \mathcal{L}_{e-1}|}{\mathcal{L}_{e-1}}
        < \varepsilon_{\mathrm{stop}}
\]


where $\varepsilon_{\mathrm{stop}} = 10^{-3}$ by default and the loss is evaluated every 100 epochs.

#### Post-Prediction Constraints {#sec:ace_constraints}

Raw network outputs are post-processed to enforce physical consistency before being returned to the solver:

1.  **Non-negativity**: any predicted molar flow $\hat{n}_k < 0$ is clamped to zero.

2.  **Absence constraint**: if component $k$ is absent from the inlet ($n_k^{\mathrm{in}} = 0$), then $\hat{n}_k = 0$ for all outlet phases regardless of the network output.

3.  **Material balance**: the predicted flows are normalised so that the total number of moles is conserved:


<a id="eq:ace_balance"></a>

\[
\hat{n}_k^{\mathrm{corr}} = \hat{n}_k
                      \,\frac{\displaystyle\sum_{j} n_j^{\mathrm{in}}}
                             {\displaystyle\sum_{j} \hat{n}_j}
\]


#### Supported Thermodynamic Operations {#sec:convenhancer_operations}

ACE provides estimates and/or solutions for the following operation types. Each type corresponds to a separate trained model.

##### Flash calculations







| **Type** | **Inputs** | **Outputs** |  |
|:---|:---|:---|:---|
| PT-Flash | $P,\,T,\,\mathbf{z}$ | $V,\,L_1,\,L_2$ component flows |  |
| PV-Flash | $P,\,\psi_V,\,\mathbf{z}$ | $T,\,V,\,L_1,\,L_2$ component flows |  |
| TV-Flash | $T,\,\psi_V,\,\mathbf{z}$ | $P,\,V,\,L_1,\,L_2$ component flows |  |
| PH-Flash | $P,\,H,\,\mathbf{z}$ | $T,\,V,\,L_1,\,L_2$ component flows |  |
| PS-Flash | $P,\,S,\,\mathbf{z}$ | $T,\,V,\,L_1,\,L_2$ component flows |  |



Here $P$ is pressure (Pa), $T$ temperature (K), $\psi_V$ vapour mole fraction, $H$ molar enthalpy (J mol$^{-1}$), $S$ molar entropy (J mol$^{-1}$ K$^{-1}$), $\mathbf{z}$ the inlet molar flow vector (mol s$^{-1}$), and $V$, $L_1$, $L_2$ the vapour, first-liquid, and second-liquid outlet molar flow vectors respectively.

##### Reactor models







| **Type** | **Inputs** | **Outputs** |
|:---|:---|:---|
| Gibbs isothermal | $T,\,P,\,\mathbf{z}$ | outlet $\mathbf{z}'$ |
| Gibbs adiabatic | $T_{\mathrm{in}},\,P,\,\mathbf{z}$ | outlet $\mathbf{z}',\,T_{\mathrm{out}}$ |
| Equilibrium isothermal | $T,\,P,\,\mathbf{z}$ | outlet $\mathbf{z}'$ |
| Equilibrium adiabatic | $T_{\mathrm{in}},\,P,\,\mathbf{z}$ | outlet $\mathbf{z}',\,T_{\mathrm{out}}$ |



#### Data Collection and Storage

Every successful thermodynamic calculation performed by the DWSIM solver is intercepted and stored as a training sample. To avoid storing duplicate records, each sample is reduced to a SHA-256 hash of its serialised JSON representation; samples whose hash already exists in the local database are silently discarded.

Training samples are persisted in a LiteDB embedded database located at







`{DWSIM_CONFIG}/ACE/data/data.db`



A backup copy (`data.db.backup`) is written each time the database is saved.

If the *Upload to Server* option is enabled, samples are further compressed with GZip, URL-encoded, and dispatched in batches of ten records per HTTP POST request to the ACE cloud server. Only thermodynamic input/output pairs are transmitted; no user identity, flowsheet topology, or process description is included. Each installation is identified by an auto-generated anonymous UUID.

#### Batch Data Generation

Users may explicitly generate a synthetic training dataset from an existing flowsheet configuration using the *Batch Data Generator*. Starting from the inlet conditions of a selected material stream, the generator systematically perturbs each independent variable within a user-specified percentage range $\delta$ to produce a Cartesian grid of scenarios:


<a id="eq:ace_batch_grid"></a>

\[
x_j^{(k)} = x_j \left(1 + \delta \, \frac{2k - (M+1)}{M-1}\right),
    \qquad k = 1, \ldots, M
\]


where $x_j$ is the nominal value of variable $j$, $\delta$ is the delta fraction (default 10 %), and $M$ is the number of grid points per variable (default 5). Each scenario is evaluated by a clone of the flowsheet property package running in a parallel thread, giving a total of up to $M^{N_{\mathrm{var}}}$ new training samples per run.

#### Automatic Model Retraining

A background timer checks the local database every $\Delta t_{\mathrm{upd}}$ seconds (default 60 s). When the number of new samples since the last training run exceeds the configured threshold $N_{\mathrm{thr}}$, the model updater is triggered. For each of the nine supported operation types the updater:

1.  Queries the database for all records matching the operation type.

2.  Groups records by compound set and property package; only groups with at least $N_{\mathrm{thr}}$ samples are processed.

3.  Calls the appropriate model trainer, which constructs the input/output matrices, scales the data (Eq. [\[eq:ace_scale\]](#eq:ace_scale)), and trains a new ANN as described above.

4.  Saves the trained model as a ZIP archive containing the serialised TensorFlow graph and a JSON metadata file with the model MSE, compound list, property package name, sample count, and scaling boundaries.

Default training thresholds:







| **Operation type**                 | $N_{\mathrm{thr}}$ |
|:-----------------------------------|:--------------------:|
| PT, PV, TV, PH, PS flash           |         1000         |
| Gibbs isothermal / adiabatic       |         100          |
| Equilibrium isothermal / adiabatic |         100          |



#### Model Selection

When ACE needs to run inference it queries the local model store and selects the model that satisfies all of the following criteria simultaneously:

- Compound set matches the inlet stream (same components, same alphabetical ordering used at training time).

- Property package name matches the active package on the flowsheet.

- Operation type (flash type or reactor type) matches the request.

- Among all qualifying models, the one with the lowest test-set MSE is selected.

If no qualifying model exists, the provider returns null and the next provider in the cascade is queried (Section ).

Compound names are sorted alphabetically before constructing both the training feature vector and the inference input vector, ensuring that a model is agnostic to the component ordering used in the flowsheet.

#### File and Directory Layout







| **Path**                   | **Contents**             |
|:---------------------------|:-------------------------|
| `ACE/config/settings.json` | User configuration       |
| `ACE/data/data.db`         | LiteDB training database |
| `ACE/data/data.db.backup`  | Backup copy              |
| `ACE/models/*.zip`         | Trained model archives   |
| `ACE/models/summary.json`  | Model index and metadata |



Model file names follow the convention







`<type>_<package>_Nc<N>_LU_<timestamp>.zip`



where `<type>` is the operation type (e.g. `PT`), `<package>` is the property package name, `N` is the number of components, and `<timestamp>` encodes the training date and time.

#### Configuration Parameters







| **Parameter** | **Key** | **Unit** | **Default** |
|:---|:---|:---|:---|
| Assistance level | `AssistanceLevel` | — | 0 |
| Auto update enabled | `AutoUpdateEnabled` | — | true |
| Update timer interval | `UpdateTimerInterval` | s | 60 |
| Database save threshold | `DatabaseSaveThreshold` | — | 1000 |
| Data delta percentage | `ProcessDataDeltaPercentage` | % | 10 |
| Data grid points per variable | `ProcessDataNumberOfPoints` | — | 5 |
| Training epochs | `ModelTrainingIterations` | — | 10 000 |
| First hidden layer size | `FirstLayerSize` | — | 100 |
| Number of hidden layers | `NumberOfLayers` | — | 2 |
| Adam learning rate | `LearningRate` | — | 0.01 |
| Early stopping tolerance | `EarlyStopTolerance` | — | $10^{-3}$ |
| PT/PV/TV/PH/PS train threshold | `*TrainThreshold` | — | 1000 |
| Gibbs/Equil. train threshold | `*TrainThreshold` | — | 100 |
| Upload to server | `UploadToServer` | — | true |
| Store data locally | `StoreDataInLocalDatabase` | — | true |
| Enable online provider | `EnableSolutionProvider4` | — | false |
| Enable local ANN provider | `EnableSolutionProvider1` | — | true |
| Enable NeqSim provider | `EnableSolutionProvider2` | — | false |
| Enable ThermoPack provider | `EnableSolutionProvider3` | — | false |



#### Assumptions and Limitations

1.  **Accuracy depends on training coverage**: ANN predictions are reliable only within the region of the input space covered by the training data. Extrapolation to conditions far outside the training range may produce unphysical results; the non-negativity and material-balance constraints (Section [3.8](#sec:ace_constraints)) reduce but do not eliminate this risk.

2.  **Compound set and property package specificity**: each model is strictly tied to the compound set and thermodynamic property package used during training. Changing either requires a new model to be trained from scratch.

3.  **Minimum data requirement**: models will not be trained until the configured threshold $N_{\mathrm{thr}}$ is reached. The extension is therefore less effective in its early use in a new project or with a new property package.

4.  **Single-phase outputs not modelled**: the networks predict per-component flows in up to three phases (V, L1, L2). Single-phase limiting cases (pure vapour or pure liquid) are handled by the material-balance correction, but the network is not explicitly trained to recognise phase boundaries.

5.  **Three-phase systems**: the current trainer generates a second liquid phase output, but convergence of three-phase equilibria by the iterative solver – even with an improved estimate – remains challenging for near-critical or highly non-ideal mixtures.

6.  **TensorFlow session overhead**: the first inference call after start-up incurs an additional latency while the TensorFlow computational graph is deserialised and loaded; subsequent calls use a cached session.

7.  **Premium requirement**: the AI-Assisted Convergence Enhancer requires an active DWSIM Premium Supporter subscription. Data collection is disabled and no providers are invoked when the subscription check fails.

8.  **Data privacy**: although only thermodynamic input/output pairs are uploaded, users operating under data governance policies that prohibit external data transfer should disable the *Upload to Server* option before use.

