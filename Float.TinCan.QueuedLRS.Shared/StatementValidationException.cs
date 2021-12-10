using System;

namespace Float.TinCan.QueuedLRS
{
    /// <summary>
    /// Statement validation exception.
    /// </summary>
    public class StatementValidationException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="StatementValidationException"/> class.
        /// </summary>
        public StatementValidationException() : base()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="StatementValidationException"/> class.
        /// </summary>
        /// <param name="message">The validation error.</param>
        public StatementValidationException(string message) : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="StatementValidationException"/> class.
        /// </summary>
        /// <param name="message">The exception message.</param>
        /// <param name="innerException">The inner exception.</param>
        public StatementValidationException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
