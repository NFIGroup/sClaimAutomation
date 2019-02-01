using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace sClaim_Automation
{
    class BackgroundServiceUtil
    {
        /// <summary>
        /// Runs the specified action asynchronously.
        /// </summary>
        /// <param name="actionToRun">The action to run.</param>
        /// <exception cref="ArgumentNullException"><paramref name="actionToRun"/> is null.</exception>
        public void RunAsync(Action actionToRun)
        {
            if (actionToRun == null)
                throw new ArgumentNullException("actionToRun");

            var worker = new BackgroundWorker();

            DoWorkEventHandler doWorkHandler = null;
            doWorkHandler = (sender, e) =>
            {
                actionToRun();
                worker.DoWork -= doWorkHandler;
            };
            worker.DoWork += doWorkHandler;

            worker.RunWorkerCompleted += BackgroundWorkerCompleted;
            worker.RunWorkerAsync();
        }

        private void BackgroundWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            // Todo: add logging message once thread is completed.
            MessageBox.Show("Thread is completed");
        }
    }
}
