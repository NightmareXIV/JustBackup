using Dalamud.Game;
using Dalamud.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JustBackup
{
    class TickScheduler : IDisposable
    {
        long executeAt;
        Action function;
        Framework framework;
        bool disposed = false;

        public TickScheduler(Action function, Framework framework, long delayMS = 0)
        {
            this.executeAt = Environment.TickCount64 + delayMS;
            this.function = function;
            this.framework = framework;
            framework.Update += Execute;
        }

        public void Dispose()
        {
            if (!disposed)
            {
                framework.Update -= Execute;
            }
            disposed = true;
        }

        void Execute(object _)
        {
            if (Environment.TickCount64 < executeAt) return;
            try
            {
                function();
            }
            catch (Exception e)
            {
                PluginLog.Error(e.Message + "\n" + e.StackTrace ?? "");
            }
            this.Dispose();
        }
    }
}
