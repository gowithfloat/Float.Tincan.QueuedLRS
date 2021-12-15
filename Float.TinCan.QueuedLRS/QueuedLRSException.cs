using System;

namespace Float.TinCan.QueuedLRS
{
    /// <summary>
    /// Exceptions raised by the Queued LRS.
    /// </summary>
    public class QueuedLRSException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="QueuedLRSException"/> class.
        /// </summary>
        public QueuedLRSException() : base()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="QueuedLRSException"/> class.
        /// </summary>
        /// <param name="message">Message associated with the exception.</param>
        public QueuedLRSException(string message) : base(message)
        {
        }
    }
}
