using System;

namespace FullTrustProcess
{
    public abstract class STATaskData
    {
        public Delegate Executer { get; }

        protected STATaskData(Delegate Executer)
        {
            this.Executer = Executer;
        }
    }
}
