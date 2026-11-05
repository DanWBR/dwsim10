// Copyright 2026 Daniel Wagner O. de Medeiros
//
// This file is part of DWSIM.
//
// DWSIM is free software: you can redistribute it and/or modify it under the terms of the GNU
// General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version.
//
// DWSIM is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even
// the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General
// Public License for more details.
//
// You should have received a copy of the GNU General Public License along with DWSIM. If not, see
// <http://www.gnu.org/licenses/>.

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DWSIM.Extensions.SolidCraft
{
    /// <summary>
    /// What the import wizards show while the AI Assistant works: a moving bar, the step under
    /// way and the time it has taken so far, so a reading of a few minutes does not look like a
    /// window that stopped answering.
    /// </summary>
    internal sealed class WorkIndicator : IDisposable
    {
        readonly Label status;
        readonly ProgressBar bar;
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer { Interval = 1000 };
        DateTime started;
        string step = "";

        public WorkIndicator(Label status, ProgressBar bar)
        {
            this.status = status;
            this.bar = bar;
            timer.Tick += (s, e) => Show();
        }

        public TimeSpan Elapsed => DateTime.Now - started;

        public void Start(string first)
        {
            started = DateTime.Now;
            step = first;
            bar.Style = ProgressBarStyle.Marquee;
            bar.Visible = true;
            timer.Start();
            Show();
        }

        /// <summary>The step under way; called on the UI thread.</summary>
        public void Step(string text)
        {
            step = text;
            Show();
        }

        public void Stop()
        {
            timer.Stop();
            bar.Visible = false;
        }

        string localAccepted;

        /// <summary>Where the model the assistant reads with is shown (the first page).</summary>
        public Label ModelLabel;

        /// <summary>Shows the model the assistant reads with, starting the assistant when it is not running; the window stays free meanwhile.</summary>
        public async void ShowModel()
        {
            if (ModelLabel == null) return;
            ModelLabel.Text = "Asking the AI Assistant which model it reads with...";
            try
            {
                var m = await Task.Run(() => DrawingImportClient.Model(CancellationToken.None));
                if (!ModelLabel.IsDisposed) ModelLabel.Text = DrawingImportClient.ModelLine(m);
            }
            catch (Exception ex)
            {
                if (!ModelLabel.IsDisposed) ModelLabel.Text = "The AI Assistant's model could not be read: " + ex.GetBaseException().Message;
            }
        }

        /// <summary>
        /// The model the assistant will read with; null when it is a local model met for the first
        /// time, which the status then explains, so that the next read goes ahead with it.
        /// </summary>
        public async Task<DrawingImportClient.AssistantModel> Model(CancellationToken ct, string what)
        {
            var m = await Task.Run(() => DrawingImportClient.Model(ct, s => status.BeginInvoke((Action)(() => Step(s)))));
            if (ModelLabel != null && !ModelLabel.IsDisposed) ModelLabel.Text = DrawingImportClient.ModelLine(m);
            if (!m.Local || localAccepted == m.ToString()) return m;
            localAccepted = m.ToString();
            Stop();
            status.Text = DrawingImportClient.LocalModelNote(m, what);
            return null;
        }

        void Show()
        {
            if (!status.IsDisposed) status.Text = step + " (" + DrawingImportClient.Elapsed(Elapsed) + ")";
        }

        public void Dispose() => timer.Dispose();
    }
}
