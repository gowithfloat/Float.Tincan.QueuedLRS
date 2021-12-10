using System;
using Float.TinCan.QueuedLRS.Triggers;
using TinCan;

namespace Float.TinCan.QueuedLRS.Tests
{
    public class MockQueueFlushTrigger : IQueueFlushTrigger
    {
        public event EventHandler TriggerFired;

        public void Fire()
        {
            TriggerFired?.Invoke(this, null);
        }

        public void OnStatementQueued(Statement statement)
        {
        }
    }
}
