using DWSIM.GlobalSettings;
using DWSIM.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static DWSIM.Interfaces.Enums.Scripts;
using System.Xml.Linq;

namespace DWSIM.DynamicRunner
{
    /// <summary>
    /// A headless DWSIM flowsheet implementation for use in automation and dynamic simulation scenarios.
    /// </summary>
    public class Flowsheet : FlowsheetBase.FlowsheetBase
    {
        private Action<string, IFlowsheet.MessageType> listeningaction;

        private Action updateUIaction;

        /// <inheritdoc/>
        public override bool SupressMessages { get; set; } = false;

        /// <summary>
        /// Initializes a new instance of <see cref="Flowsheet"/> with optional message and UI update callbacks.
        /// </summary>
        /// <param name="messageListener">
        /// A callback invoked when the flowsheet emits a message. Pass <c>null</c> to ignore messages.
        /// </param>
        /// <param name="updateUIhandler">
        /// A callback invoked when the flowsheet requests a UI refresh. Pass <c>null</c> to skip UI updates.
        /// </param>
        public Flowsheet(Action<string, IFlowsheet.MessageType> messageListener, Action updateUIhandler)
        {

            SaveSpreadsheetData = new Action<XDocument>((xdoc) =>
            {
            });

            RetrieveSpreadsheetData = new Func<string, List<string[]>>((range) =>
            {
                return null;
                //return Spreadsheet.GetDataFromRange(range);
            });

            RetrieveSpreadsheetFormat = new Func<string, List<string[]>>((range) =>
            {
                return null;
                //return Spreadsheet.GetDataFromRange(range);
            });

            DynamicsManager.RunSchedule = (schname) =>
            {
                DynamicsManager.CurrentSchedule = DynamicsManager.GetSchedule(schname).ID;
                return null;
            };

            listeningaction = messageListener;

            updateUIaction = updateUIhandler;

        }

        /// <summary>
        /// Performs base flowsheet initialization. Must be called after construction before loading or solving.
        /// </summary>
        public void Init()
        {

            Initialize();

        }

        /// <inheritdoc/>
        public override IFlowsheet GetNewInstance()
        {
            var fs = new Flowsheet(null, null);
            return fs;
        }

        /// <inheritdoc/>
        public override void UpdateInformation()
        {
            UpdateInterface();
        }

        /// <inheritdoc/>
        public override void UpdateInterface()
        {
            updateUIaction?.Invoke();
        }

        /// <inheritdoc/>
        public override void ShowDebugInfo(string text, int level)
        {
            Console.WriteLine(text);
        }

        /// <inheritdoc/>
        public override void ShowMessage(string text, IFlowsheet.MessageType mtype, string exceptionid = "")
        {
            if (listeningaction != null) listeningaction(text, mtype);
        }

        /// <summary>
        /// Sends an informational message to the registered message listener.
        /// </summary>
        /// <param name="text">The message text to send.</param>
        public override void WriteMessage(string text)
        {
            listeningaction?.Invoke(text, IFlowsheet.MessageType.Information);
        }

        /// <inheritdoc/>
        public override void UpdateOpenEditForms()
        {

        }

        /// <inheritdoc/>
        public override object GetApplicationObject()
        {
            return null;
        }

        /// <summary>
        /// Solves the flowsheet synchronously. Throws an exception if no property package or compounds are selected,
        /// or if the solver encounters an error.
        /// </summary>
        /// <exception cref="Exception">
        /// Thrown when no property package is configured or no compounds are selected.
        /// </exception>
        public void SolveFlowsheet()
        {

            if (PropertyPackages.Count == 0)
            {
                throw new Exception("Please select a Property Package before solving the flowsheet.");
            }

            if (SelectedCompounds.Count == 0)
            {
                throw new Exception("Please select a Compound before solving the flowsheet.");
            }

            Settings.CalculatorActivated = true;
            Settings.SolverMode = 1;
            Settings.SolverBreakOnException = true;

            RequestCalculation();

        }

        /// <summary>
        /// Solves the flowsheet asynchronously on the default task scheduler and waits for completion.
        /// Unlike <see cref="SolveFlowsheet"/>, this method returns solver exceptions as a list rather than throwing.
        /// </summary>
        /// <returns>
        /// A list of <see cref="Exception"/> objects encountered during solving, or an empty list on success.
        /// </returns>
        public List<Exception> SolveFlowsheet2()
        {
            if (PropertyPackages.Count == 0)
            {
                ShowMessage("Please select a Property Package before solving the flowsheet.", IFlowsheet.MessageType.GeneralError);
                return new List<Exception>();
            }

            if (SelectedCompounds.Count == 0)
            {
                ShowMessage("Please select a Compound before solving the flowsheet.", IFlowsheet.MessageType.GeneralError);
                return new List<Exception>();
            }

            Settings.CalculatorActivated = true;

            Task<List<Exception>> st = new Task<List<Exception>>(() =>
            {
                return FlowsheetSolver.FlowsheetSolver.SolveFlowsheet(this, GlobalSettings.Settings.SolverMode);
            });

            st.ContinueWith((t) =>
            {
                Settings.CalculatorStopRequested = false;
                Settings.CalculatorBusy = false;
                Settings.TaskCancellationTokenSource = new System.Threading.CancellationTokenSource();
            });

            try
            {
                st.Start(TaskScheduler.Default);
                st.Wait();
                return st.Result;
            }
            catch (AggregateException aex)
            {
                foreach (Exception ex2 in aex.InnerExceptions)
                {
                    ShowMessage(ex2.ToString(), IFlowsheet.MessageType.GeneralError);
                }
                Settings.CalculatorBusy = false;
                Settings.TaskCancellationTokenSource = new System.Threading.CancellationTokenSource();
                return new List<Exception>(aex.InnerExceptions);
            }
            catch (Exception ex)
            {
                ShowMessage(ex.ToString(), IFlowsheet.MessageType.GeneralError);
                Settings.CalculatorBusy = false;
                Settings.TaskCancellationTokenSource = new System.Threading.CancellationTokenSource();
                return new List<Exception> { ex };
            }

        }

        /// <inheritdoc/>
        public override void SetMessageListener(Action<string, IFlowsheet.MessageType> act)
        {
            listeningaction = act;
        }

        /// <inheritdoc/>
        public override void RunCodeOnUIThread(Action act)
        {
            act.Invoke();
        }

        /// <inheritdoc/>
        /// <exception cref="NotImplementedException">Always thrown; displaying forms is not supported in headless mode.</exception>
        public override void DisplayForm(object form)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override void CloseOpenEditForms()
        {

        }
    }
}
