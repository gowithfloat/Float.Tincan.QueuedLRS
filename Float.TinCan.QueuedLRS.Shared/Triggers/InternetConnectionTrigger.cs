using System;
using System.Net.NetworkInformation;
using TinCan;

namespace Float.TinCan.QueuedLRS.Triggers
{
    /// <summary>
    /// Trigger to flush the statement queue when a network connection becomes available after previously being unavailable.
    /// </summary>
    /// <remarks>
    /// Class emptied due to issues with 64-bit android support. NetworkChange would cause the app to crash on startup.
    /// Using Mono Framework MDK 6.0.0.296, this class was able to work with 64-bit android. When the next stable
    /// update for Mono comes out, we can re-implement this class.
    /// </remarks>
    public class InternetConnectionTrigger : IQueueFlushTrigger
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InternetConnectionTrigger"/> class.
        /// </summary>
        public InternetConnectionTrigger()
        {
            NetworkChange.NetworkAvailabilityChanged += HandleNetworkChanged;
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="InternetConnectionTrigger"/> class.
        /// </summary>
        ~InternetConnectionTrigger()
        {
            NetworkChange.NetworkAvailabilityChanged -= HandleNetworkChanged;
        }

        /// <inheritdoc />
        public event EventHandler TriggerFired;

        /// <inheritdoc />
        public void OnStatementQueued(Statement statement)
        {
        }

        /// <summary>
        /// Invoked when the status on a network interface changes.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">Additional information about hte change in network status.</param>
        void HandleNetworkChanged(object sender, NetworkAvailabilityEventArgs e)
        {
            if (e.IsAvailable)
            {
                TriggerFired?.Invoke(this, e);
            }
        }
    }
}
