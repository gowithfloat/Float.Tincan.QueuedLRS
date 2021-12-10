using System.Collections.Generic;

namespace Float.TinCan.QueuedLRS.Stores
{
    /// <summary>
    /// Represents a statement store for queued statements.
    /// </summary>
    public interface IStateResourceStore
    {
        /// <summary>
        /// Writes the state resource documents to the persistent store.
        /// Depending on implementation, this may replace any previously written state resource documents in the store.
        /// JSON stores included in this package currently do overwrite previously written state resource documents.
        /// </summary>
        /// <returns><c>true</c>, if the file was written, <c>false</c> otherwise.</returns>
        /// <param name="stateResources">The state docucment to persist.</param>
        bool WriteStateResources(List<CachedStateDocument> stateResources);

        /// <summary>
        /// Restores state resources from the persistent store that were previously written with WriteStatements.
        /// </summary>
        /// <returns>State Documents retrieved from the persistent store, or null if no statements have been persisted.</returns>
        List<CachedStateDocument> RestoreStateResources();

        /// <summary>
        /// Removes state from the persistent store.
        /// </summary>
        void Empty();
    }
}
