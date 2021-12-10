using System;
using TinCan;

namespace Float.TinCan.QueuedLRS.Triggers
{
    /// <summary>
    /// Trigger to flush the statement queue when a "completed" statement is sent to the LRS.
    /// </summary>
    public class CompletedStatementTrigger : IQueueFlushTrigger
    {
        const string CompletedVerbId = "http://adlnet.gov/expapi/verbs/completed";

        /// <inheritdoc />
        public event EventHandler TriggerFired;

        /// <inheritdoc />
        public void OnStatementQueued(Statement statement)
        {
            if (statement == null)
            {
                throw new ArgumentNullException(nameof(statement));
            }

            if (statement.verb.id.ToString() == CompletedVerbId)
            {
                TriggerFired?.Invoke(this, null);
            }
        }
    }
}
