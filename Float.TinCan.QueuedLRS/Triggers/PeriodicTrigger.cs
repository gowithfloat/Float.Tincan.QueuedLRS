using System;
using System.Threading;
using TinCan;

namespace Float.TinCan.QueuedLRS.Triggers
{
    /// <summary>
    /// A periodic trigger (e.g. every 1 minute) for the queued LRS.
    /// </summary>
    public class PeriodicTrigger : IQueueFlushTrigger
    {
        readonly Timer timer;

        /// <summary>
        /// Initializes a new instance of the <see cref="PeriodicTrigger"/> class.
        /// </summary>
        /// <param name="interval">Interval (in seconds) that the queue should be flushed.</param>
        public PeriodicTrigger(int interval = 60)
        {
            if (interval < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(interval));
            }

            // When running within Xamarin, this timer will not "stack up" while the app is in the background.
            // For example, if the interval is set to 60 seconds, but the app has been in the background for 5 minutes,
            // upon returning to the foreground, the trigger will only be fired once--not five times.
            timer = new Timer(e => TriggerFired?.Invoke(this, EventArgs.Empty), null, TimeSpan.Zero, TimeSpan.FromSeconds(interval));
        }

        /// <inheritdoc />
        public event EventHandler TriggerFired;

        /// <inheritdoc />
        public void OnStatementQueued(Statement statement)
        {
        }
    }
}
