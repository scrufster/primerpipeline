using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PrimerPipeline
{
    public class Progress
    {
        #region Variables

        private bool isIndeterminate = false;

        private int numberOfSteps = 0, currentStep = 0;

        private string currentTask = "";
        private double progressFraction = 0;

        private bool userRequestedMoveToNextTask = false;

        #endregion

        public Progress(int numberOfSteps)
        {
            this.numberOfSteps = numberOfSteps;
        }

        public void ApplyProgressFraction(double fraction)
        {
            progressFraction = fraction;
        }

        public void AssignCurrentTask(string taskName, bool isIndeterminate = false)
        {
            currentTask = taskName;
            this.isIndeterminate = isIndeterminate;

            progressFraction = 0;

            //update the current step:
            currentStep++;
        }

        public void CurrentTaskComplete()
        {
            progressFraction = 1;
        }

        public string GetProgress()
        {
            if (isIndeterminate)
            {
                return string.Format("Step {0}/{1} - {2}...", currentStep, numberOfSteps, currentTask);
            }
            else
            {
                return string.Format("Step {0}/{1} - {2} ({3}%)...", currentStep, numberOfSteps, currentTask, Math.Round(progressFraction * 100));
            }
        }

        #region Accessor methods

        public bool MoveToNextTaskRequested
        {
            get { return userRequestedMoveToNextTask; }
            set { userRequestedMoveToNextTask = value; }
        }

        #endregion
    }
}
