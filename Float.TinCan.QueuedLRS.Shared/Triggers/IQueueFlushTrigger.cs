using System;
using TinCan;

namespace Float.TinCan.QueuedLRS.Triggers
{
    /// <summary>
    /// A trigger for flushing the statement queue.
    /// </summary>
    public interface IQueueFlushTrigger
    {
        /// <summary>
        /// Occurs when this trigger has fired.
        /// </summary>
        event EventHandler TriggerFired;

        /// <summary>
        /// Invoked when a new statement has been queued in the queued LRS.
        /// </summary>
        /// <param name="statement">The statement that has been queued.</param>
        void OnStatementQueued(Statement statement);
    }
}
