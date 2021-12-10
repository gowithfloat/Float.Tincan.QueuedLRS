using System.Collections.Generic;
using TinCan;

namespace Float.TinCan.QueuedLRS.Stores
{
    /// <summary>
    /// Represents a statement store for queued statements.
    /// </summary>
    public interface IStatementStore
    {
        /// <summary>
        /// Writes the statements to the persistent store.
        /// Depending on implementation, this may replace any previously written statements in the store.
        /// JSON stores included in this package currently do overwrite previously written statements.
        /// </summary>
        /// <returns><c>true</c>, if the file was written, <c>false</c> otherwise.</returns>
        /// <param name="statements">The statements to persist.</param>
        bool WriteStatements(List<Statement> statements);

        /// <summary>
        /// Restores statements from the persistent store that were previously written with WriteStatements.
        /// </summary>
        /// <returns>Statements retrieved from the persistent store, or null if no statements have been persisted.</returns>
        List<Statement> RestoreStatements();

        /// <summary>
        /// Empties the persistent store.
        /// </summary>
        void Empty();
    }
}
