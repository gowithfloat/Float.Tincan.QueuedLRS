using System;
using System.Collections.Generic;
using TinCan;
using TinCan.LRSResponses;

namespace Float.TinCan.QueuedLRS
{
    /// <summary>
    /// An object to hold data related to the process of flushing statements from the queue.
    /// </summary>
    public class FlushStatementResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FlushStatementResult"/> class.
        /// </summary>
        /// <param name="response">Response from the LRS.</param>
        /// <param name="statements">Statements that were persisted.</param>
        public FlushStatementResult(StatementsResultLRSResponse response, IEnumerable<Statement> statements)
        {
            Response = response ?? throw new ArgumentNullException(nameof(response));
            PersistedStatements = statements ?? throw new ArgumentNullException(nameof(statements));
        }

        /// <summary>
        /// Gets the response from the LRS.
        /// </summary>
        /// <value>The response.</value>
        public StatementsResultLRSResponse Response { get; }

        /// <summary>
        /// Gets the persisted statements.
        /// </summary>
        /// <value>The persisted statements.</value>
        public IEnumerable<Statement> PersistedStatements { get; }
    }
}
