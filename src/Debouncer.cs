using System;
using System.Threading;

namespace CodexPeek
{
    internal sealed class Debouncer
    {
        private readonly int delayMs;
        private readonly Action<string> action;
        private readonly object gate = new object();
        private Timer timer;
        private string latestPath = "";

        public Debouncer(int delayMs, Action<string> action)
        {
            this.delayMs = delayMs;
            this.action = action;
        }

        public void Signal(string path)
        {
            lock (gate)
            {
                latestPath = path;
                if (timer == null)
                {
                    timer = new Timer(OnTimer, null, delayMs, Timeout.Infinite);
                }
                else
                {
                    timer.Change(delayMs, Timeout.Infinite);
                }
            }
        }

        private void OnTimer(object state)
        {
            string path;
            lock (gate)
            {
                path = latestPath;
            }
            action(path);
        }
    }
}
