using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Float.TinCan.QueuedLRS.Responses;
using Float.TinCan.QueuedLRS.Stores;
using Float.TinCan.QueuedLRS.Triggers;
using TinCan;
using TinCan.Documents;
using TinCan.LRSResponses;

namespace Float.TinCan.QueuedLRS
{
    /// <summary>
    /// A LRS queue for holding/batching statements before passing onto another LRS.
    /// For example, if statements needed to be stored offline until an Internet connection was available,
    /// the statements could be queued and then the queue would be flushed when the connection was up.
    /// The statement queue will be stored so it can persist across sessions.
    /// The queue will store statements indefinitely until the queue has been flushed. At that point, a batch of statements
    /// will be forwarded to the target LRS. If the statements were successfully received, then those statements are removed
    /// from the local queue. If an error occurs, those statements will be kept in the queue.
    /// The queue is automatically flushed when any of the defined triggers (`IQueueFlushTrigger`) is fired.
    /// Additionally, the queue can be manually flushed using `FlushStatementQueue`.
    /// </summary>
    public class QueuedLRS : ILRS
    {
        readonly IEnumerable<IQueueFlushTrigger> triggers;
        readonly ILRS targetLrs;
        readonly IStatementStore statementStore;
        readonly IStateResourceStore stateStore;
        readonly List<Statement> statementQueue = new List<Statement>();
        readonly StateResourceCache stateCache = new StateResourceCache();
        readonly SemaphoreSlim statementQueueSemaphore = new SemaphoreSlim(1, 1);
        readonly SemaphoreSlim stateCacheSemaphore = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Initializes a new instance of the <see cref="QueuedLRS"/> class.
        /// If no triggers are specified, then the queue will automatically be flushed every 60 seconds,
        /// whenever an Internet connection becomes available, and when a "completed" statement is saved to the LRS.
        /// </summary>
        /// <param name="targetLrs">The LRS to forward queued statements when the queue is flushed.</param>
        /// <param name="statementStore">The persistent store for the statements.</param>
        /// <param name="stateStore"> The persistent store for State resources.</param>
        /// <param name="triggers">Triggers to automatically flush the queue.</param>
        public QueuedLRS(ILRS targetLrs, IStatementStore statementStore, IStateResourceStore stateStore, IEnumerable<IQueueFlushTrigger> triggers = null)
        {
            this.targetLrs = targetLrs ?? throw new ArgumentNullException(nameof(targetLrs));
            this.statementStore = statementStore ?? throw new ArgumentNullException(nameof(statementStore));
            this.stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));

            // Recover queued statements
            var queuedStatements = this.statementStore.RestoreStatements();
            if (queuedStatements != null)
            {
                statementQueue = queuedStatements;
            }

            // Recover queued state resources
            var queuedStateResources = stateStore.RestoreStateResources();
            if (queuedStateResources != null)
            {
                stateCache.Cache = queuedStateResources;
            }

            this.triggers = triggers ?? GetDefaultTriggers();
            this.triggers.ForEach(trigger => trigger.TriggerFired += HandleTriggerFired);
        }

        /// <summary>
        /// Gets or sets the number of maximum statements to send to the LRS when flushing the statement queue.
        /// </summary>
        /// <value>The batch size.</value>
        public int BatchSize { get; set; } = 50;

        /// <summary>
        /// Gets or sets the cache policy.
        /// </summary>
        /// <value>The cache policy.</value>
        public StateCachePolicy CachePolicy { get; set; } = StateCachePolicy.PreferRemote;

        /// <summary>
        /// Gets the size of the queue.
        /// </summary>
        /// <value>The size of the queue.</value>
        public int QueueSize => statementQueue.Count;

        /// <summary>
        /// Gets the queue.
        /// </summary>
        /// <value>The queue.</value>
        public List<Statement> Queue => new List<Statement>(statementQueue);

        /// <summary>
        /// Gets the state queue.
        /// </summary>
        /// <value>The state queue.</value>
        public List<CachedStateDocument> StateCache => stateCache.Cache;

        /// <summary>
        /// Gets the dirty state cache.
        /// </summary>
        /// <value>The dirty state cache.</value>
        public List<CachedStateDocument> DirtyStateCache => stateCache.DirtyQueue;

        /// <summary>
        /// Flushes the statement queue.
        /// If statements could not be persisted, they are kept in the queue.
        /// </summary>
        /// <returns>A list of statements that were persisted on the target LRS.</returns>
        [Obsolete("FlushStatementQueue is deprecated and will be removed in a future release, use FlushStatementQueueWithResponse instead.")]
        public async Task<List<Statement>> FlushStatementQueue()
        {
            return (await FlushStatementQueueWithResponse().ConfigureAwait(false))?.PersistedStatements?.ToList();
        }

        /// <summary>
        /// Flushes the statement queue.
        /// If statements could not be persisted, they are kept in the queue.
        /// </summary>
        /// <returns>An object with the responses from the LRS, and a list of statements that were persisted on the target LRS.</returns>
        public async Task<FlushStatementResult> FlushStatementQueueWithResponse()
        {
            IEnumerable<Statement> persisted = new List<Statement>();
            StatementsResultLRSResponse response;

            // we await here even though we're not modifying the queue until later to avoid sending the same 50 statements twice
            using (await statementQueueSemaphore.UseWaitAsync().ConfigureAwait(false))
            {
                if (statementQueue.Count < 1)
                {
                    return null;
                }

                // create a shallow copy of the statement queue; sending only a few statements at a time
                var statementsToSend = statementQueue.GetRange(0, Math.Min(statementQueue.Count, BatchSize));

                try
                {
                    response = await targetLrs.SaveStatements(statementsToSend).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    response = new StatementsResultQueuedLRSResponse(false, new StatementsResult(new List<Statement>()), e);
                }

                if (response?.success == true || IsUnrecoverableErrorResult(response))
                {
                    persisted = statementsToSend;
                    statementsToSend.ForEach(statement => statementQueue.Remove(statement));
                }
            }

            response.success &= await PersistQueue().ConfigureAwait(false);

            return new FlushStatementResult(response, persisted);
        }

        /// <summary>
        /// Attempts to send all the statments in the statement queue, in batches with a size determined in FlushStatementQueueWithResponse().
        /// </summary>
        /// <returns>The flush.</returns>
        public async Task FlushFullStatementQueue()
        {
            FlushStatementResult flushStatementResult;
            do
            {
                flushStatementResult = await FlushStatementQueueWithResponse().ConfigureAwait(false);
            }
            while (flushStatementResult != null && flushStatementResult.Response.success);
        }

        /// <summary>
        /// Flushes the state resource queue. A copy of the state is stored locally, they are kept in the queue.
        /// </summary>
        /// <returns>Responses for all state operations.</returns>
        public async Task<IEnumerable<LRSResponse>> FlushStateResourceQueue()
        {
            if (stateCache.DirtyQueue.Count == 0)
            {
                return Enumerable.Empty<LRSResponse>();
            }

            var responses = await Task.WhenAll(stateCache.DirtyQueue.Select(FlushSingleDocument)).ConfigureAwait(false);

            // we could return this as an additional LRSResponse, but then you would get n+1 results for n statements
            await PersistStateQueue().ConfigureAwait(false);

            return responses;
        }

        /// <summary>
        /// Clears both statements and state.
        /// </summary>
        public void ClearQueue()
        {
            ClearStatementQueue();
            ClearDocumentQueue();
        }

        /// <summary>
        /// Clear any LRS statements queued in memory.
        /// </summary>
        public void ClearStatementQueue()
        {
            statementQueue.Clear();
            statementStore.Empty();
        }

        /// <summary>
        /// Clears the file containing the queued state.
        /// </summary>
        public void ClearDocumentQueue()
        {
            stateStore.Empty();
        }

        /// <inheritdoc />
        public async Task<StatementLRSResponse> SaveStatement(Statement statement)
        {
            if (statement == null)
            {
                throw new ArgumentNullException(nameof(statement));
            }

            return new StatementQueuedLRSResponse(await PersistQueue().ConfigureAwait(false), await AddStatementToQueue(statement).ConfigureAwait(false));
        }

        /// <inheritdoc />
        public async Task<StatementsResultLRSResponse> SaveStatements(List<Statement> statements)
        {
            if (statements == null)
            {
                throw new ArgumentNullException(nameof(statements));
            }

            if (!statements.Any())
            {
                return new StatementsResultQueuedLRSResponse(true, new StatementsResult(new List<Statement>()));
            }

            var queuedStatements = new List<Statement>(await Task.WhenAll(statements.Select(AddStatementToQueue)).ConfigureAwait(false));
            var success = await PersistQueue().ConfigureAwait(false);
            return new StatementsResultQueuedLRSResponse(success, new StatementsResult(queuedStatements));
        }

        /// <inheritdoc />
        public Task<StatementLRSResponse> VoidStatement(Guid id, Agent agent)
        {
            if (agent == null)
            {
                throw new ArgumentNullException(nameof(agent));
            }

            return SaveStatement(new Statement
            {
                actor = agent,
                verb = new Verb(new Uri("http://adlnet.gov/expapi/verbs/voided"), "en-US", "voided"),
                target = new StatementRef { id = id },
            });
        }

        /// <inheritdoc />
        public Task<StatementsResultLRSResponse> MoreStatements(StatementsResult result) => targetLrs.MoreStatements(result);

        /// <inheritdoc />
        public async Task<StatementsResultLRSResponse> QueryStatements(StatementsQuery query)
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            await FlushStatementQueueWithResponse().ConfigureAwait(false);
            return await targetLrs.QueryStatements(query).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public Task<AboutLRSResponse> About() => targetLrs.About();

        /// <inheritdoc />
        public async Task<LRSResponse> ClearState(Activity activity, Agent agent, Guid? registration = default)
        {
            // Make sure incoming data is good
            try
            {
                CheckInputValid(activity, agent);
            }
            catch (QueuedLRSException e)
            {
                return new QueuedLRSResponse(false, e);
            }

            using (await stateCacheSemaphore.UseWaitAsync().ConfigureAwait(false))
            {
                stateCache.Clear(activity, agent, registration, false);
            }

            return new QueuedLRSResponse(true, null);
        }

        /// <inheritdoc />
        public Task<LRSResponse> DeleteActivityProfile(ActivityProfileDocument profile) => targetLrs.DeleteActivityProfile(profile);

        /// <inheritdoc />
        public Task<LRSResponse> DeleteAgentProfile(AgentProfileDocument profile) => targetLrs.DeleteAgentProfile(profile);

        /// <inheritdoc />
        public async Task<LRSResponse> DeleteState(StateDocument state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            // Make sure incoming data is good
            try
            {
                CheckInputValid(state.activity, state.agent, state.id);
            }
            catch (QueuedLRSException e)
            {
                return new QueuedLRSResponse(false, e);
            }

            using (await stateCacheSemaphore.UseWaitAsync().ConfigureAwait(false))
            {
                stateCache.Remove(state, false);
            }

            return new QueuedLRSResponse(true, null);
        }

        /// <inheritdoc />
        public Task<ActivityProfileLRSResponse> RetrieveActivityProfile(string id, Activity activity) => targetLrs.RetrieveActivityProfile(id, activity);

        /// <inheritdoc />
        public Task<ProfileKeysLRSResponse> RetrieveActivityProfileIds(Activity activity) => targetLrs.RetrieveActivityProfileIds(activity);

        /// <inheritdoc />
        public Task<AgentProfileLRSResponse> RetrieveAgentProfile(string id, Agent agent) => targetLrs.RetrieveAgentProfile(id, agent);

        /// <inheritdoc />
        public Task<ProfileKeysLRSResponse> RetrieveAgentProfileIds(Agent agent) => targetLrs.RetrieveAgentProfileIds(agent);

        /// <inheritdoc />
        public async Task<StateLRSResponse> RetrieveState(string id, Activity activity, Agent agent, Guid? registration = default)
        {
            // Make sure incoming data is good
            try
            {
                CheckInputValid(activity, agent, id);
            }
            catch (QueuedLRSException e)
            {
                return new StateQueuedLRSResponse(false, e);
            }

            // retrieve the local copy of the state first
            CachedStateDocument localCopy;

            using (await stateCacheSemaphore.UseWaitAsync().ConfigureAwait(false))
            {
                localCopy = stateCache.RetrieveState(id, activity, agent, registration);
            }

            // based on client's desired policy, return state
            switch (CachePolicy)
            {
                case StateCachePolicy.PreferLocal:
                    if (localCopy == null)
                    {
                        goto case StateCachePolicy.PreferRemote;
                    }

                    return new StateLRSResponse
                    {
                        success = true,
                        content = localCopy?.State,
                    };
                case StateCachePolicy.KeepLocalUpdated:
                    var ignored = Task.Run(async () =>
                    {
                        var response = await RetrieveStateHandleFailure(id, activity, agent, registration).ConfigureAwait(false);
                        await UpdateStateFromResponse(localCopy, response).ConfigureAwait(false);
                    }).ConfigureAwait(false);

                    goto case StateCachePolicy.PreferLocal;
                case StateCachePolicy.PreferRemote:
                default: // Immediate
                    var response2 = await RetrieveStateHandleFailure(id, activity, agent, registration).ConfigureAwait(false);
                    await UpdateStateFromResponse(localCopy, response2).ConfigureAwait(false);
                    return response2;
            }
        }

        /// <inheritdoc />
        public async Task<ProfileKeysLRSResponse> RetrieveStateIds(Activity activity, Agent agent, Guid? registration = default)
        {
            // Make sure incoming data is good
            try
            {
                CheckInputValid(activity, agent);
            }
            catch (QueuedLRSException e)
            {
                return new ProfileKeysQueuedLRSResponse(false, e);
            }

            List<string> localResponse;

            using (await stateCacheSemaphore.UseWaitAsync().ConfigureAwait(false))
            {
                localResponse = stateCache.GetStateIds(activity, agent, registration);
            }

            ProfileKeysLRSResponse response;

            try
            {
                response = await targetLrs.RetrieveStateIds(activity, agent, registration).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                response = new ProfileKeysQueuedLRSResponse(false, e);
            }

            if (response?.success == true)
            {
                response.content.Union(localResponse);
            }
            else if (localResponse != null)
            {
                // remote failed so check local response
                response.success = true;
                response.content = localResponse;
            }

            return response;
        }

        /// <inheritdoc />
        public Task<StatementLRSResponse> RetrieveStatement(Guid id) => targetLrs.RetrieveStatement(id);

        /// <inheritdoc />
        public Task<StatementLRSResponse> RetrieveVoidedStatement(Guid id) => targetLrs.RetrieveVoidedStatement(id);

        /// <inheritdoc />
        public Task<LRSResponse> SaveActivityProfile(ActivityProfileDocument profile) => targetLrs.SaveActivityProfile(profile);

        /// <inheritdoc />
        public Task<LRSResponse> SaveAgentProfile(AgentProfileDocument profile) => targetLrs.SaveAgentProfile(profile);

        /// <inheritdoc />
        public Task<LRSResponse> ForceSaveAgentProfile(AgentProfileDocument profile) => targetLrs.ForceSaveAgentProfile(profile);

        /// <inheritdoc />
        public async Task<LRSResponse> SaveState(StateDocument state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            // Make sure incoming data is good
            try
            {
                CheckInputValid(state.activity, state.agent, state.id);
            }
            catch (QueuedLRSException e)
            {
                return new QueuedLRSResponse(false, e);
            }

            // add state to queue
            var stateToQueue = new CachedStateDocument(state);

            if (state.timestamp == default)
            {
                stateToQueue.State.timestamp = DateTime.Now;
            }

            QueuedLRSResponse response = new QueuedLRSResponse(true);

            using (await stateCacheSemaphore.UseWaitAsync().ConfigureAwait(false))
            {
                response.success &= stateCache.Add(stateToQueue);
            }

            response.success &= await PersistStateQueue().ConfigureAwait(false);

            if (CachePolicy == StateCachePolicy.PreferRemote)
            {
                _ = FlushSingleDocument(stateToQueue).ContinueWith(
                    (task) =>
                    {
                        if (task.Exception != null)
                        {
                            System.Diagnostics.Debug.WriteLine(task.Exception.Message);
                        }
                    }, TaskScheduler.Current);
            }

            return response;
        }

        /// <summary>
        /// Saves multiple states at once. Currently only used for testing.
        /// </summary>
        /// <returns>The LRS response for each state save operation.</returns>
        /// <param name="states">States to save.</param>
        public async Task<IEnumerable<LRSResponse>> SaveStates(IEnumerable<StateDocument> states)
        {
            if (states == null)
            {
                throw new ArgumentNullException(nameof(states));
            }

            return await Task.WhenAll(states.Select(SaveState)).ConfigureAwait(false);
        }

        static IEnumerable<IQueueFlushTrigger> GetDefaultTriggers()
        {
            return new List<IQueueFlushTrigger>
            {
                new InternetConnectionTrigger(),
                new CompletedStatementTrigger(),
                new PeriodicTrigger(),
            };
        }

        /// <summary>
        /// It is possible that the LRS might return an "unrecoverable error" when attempting to persist statements that were previously queued.
        /// In this case, there is no method of recourse for the QueuedLRS and those statements must be discarded to prevent problems persisting
        /// statements in the future.
        /// An unrecoverable error is where the LRS returns a 4xx error (such as 400, 409, or 413).
        /// The local statement validator attempts to prevent most of these occurrences.
        /// </summary>
        /// <returns><c>true</c>, if an unrecoverable error result encountered, <c>false</c> otherwise.</returns>
        /// <param name="result">Response from the LRS.</param>
        static bool IsUnrecoverableErrorResult(LRSResponse result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            // A WebException indicates that there was a problem making the request
            // (e.g. there was no Internet conncetion)
            if (result.httpException != null || result.Error == null)
            {
                return false;
            }

            switch (result.Error?.Code)
            {
                case (int)System.Net.HttpStatusCode.BadRequest:
                case (int)System.Net.HttpStatusCode.Conflict:
                case (int)System.Net.HttpStatusCode.RequestEntityTooLarge:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Checks to see if information passed to State methods is complete and valid.
        /// Throws an exception if checking fails.
        /// </summary>
        /// <param name="activity">The activity object to check.</param>
        /// <param name="agent">The agent object to check.</param>
        static void CheckInputValid(Activity activity, Agent agent)
        {
            // check activity
            if (activity == null || activity.id == null)
            {
                throw new QueuedLRSException("Invalid activity when validating input");
            }

            // check agent
            if (agent == null || (string.IsNullOrWhiteSpace(agent.mbox) && string.IsNullOrWhiteSpace(agent.name) && string.IsNullOrWhiteSpace(agent.openid) && (agent.account == null || (agent.account.homePage == null || string.IsNullOrEmpty(agent.account.name)))))
            {
                throw new QueuedLRSException("Invalid agent when validating input");
            }
        }

        /// <summary>
        /// Checks to see if information passed to State methods is complete and valid.
        /// Throws an exception if checking fails.
        /// </summary>
        /// <param name="activity">The activity object to check.</param>
        /// <param name="agent">The agent object to check.</param>
        /// <param name="stateId">State identifier to check.</param>
        static void CheckInputValid(Activity activity, Agent agent, string stateId)
        {
            CheckInputValid(activity, agent);

            // check stateID if not null
            if (string.IsNullOrWhiteSpace(stateId))
            {
                throw new QueuedLRSException("Invalid state ID when validating input");
            }
        }

        async Task<Statement> AddStatementToQueue(Statement originalStatement)
        {
            if (originalStatement == null)
            {
                throw new ArgumentNullException(nameof(originalStatement));
            }

            // Create a copy of the statement to prevent caller from changing it
            var statement = new Statement(originalStatement.ToJObject(null));

            if (statement.id == null)
            {
                statement.id = Guid.NewGuid();
            }

            if (statement.timestamp == null)
            {
                statement.timestamp = DateTime.Now;
            }

            StatementValidator.ValidateStatement(statement);

            using (await statementQueueSemaphore.UseWaitAsync().ConfigureAwait(false))
            {
                statementQueue.Add(statement);
            }

            SendStatementToTriggers(statement);

            return statement;
        }

        async Task<bool> PersistQueue()
        {
            using (await statementQueueSemaphore.UseWaitAsync().ConfigureAwait(false))
            {
                return statementStore.WriteStatements(statementQueue);
            }
        }

        async Task<bool> PersistStateQueue()
        {
            using (await stateCacheSemaphore.UseWaitAsync().ConfigureAwait(false))
            {
                return stateStore.WriteStateResources(stateCache.Cache);
            }
        }

        void SendStatementToTriggers(Statement statement)
        {
            if (statement == null)
            {
                throw new ArgumentNullException(nameof(statement));
            }

            triggers.ForEach(trigger => trigger.OnStatementQueued(statement));
        }

        async Task<LRSResponse> AsyncStateFlush(CachedStateDocument dirtystate)
        {
            if (dirtystate == null)
            {
                throw new ArgumentNullException(nameof(dirtystate));
            }

            LRSResponse response;

            if (dirtystate.CurrentStatus == CachedStateDocument.Status.Deleted)
            {
                response = await targetLrs.DeleteState(dirtystate.State).ConfigureAwait(false);
            }
            else
            {
                response = await targetLrs.SaveState(dirtystate.State).ConfigureAwait(false);
            }

            if (response?.success == true)
            {
                if (dirtystate.CurrentStatus == CachedStateDocument.Status.Deleted)
                {
                    using (await stateCacheSemaphore.UseWaitAsync().ConfigureAwait(false))
                    {
                        stateCache.Remove(dirtystate.State, true);
                    }
                }
                else
                {
                    dirtystate.CurrentStatus = CachedStateDocument.Status.Clean;
                }
            }

            return response;
        }

        async Task<LRSResponse> FlushSingleDocument(CachedStateDocument doc)
        {
            try
            {
                return await AsyncStateFlush(doc).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                return new QueuedLRSResponse(false, e);
            }
        }

        async Task<StateLRSResponse> RetrieveStateHandleFailure(string id, Activity activity, Agent agent, Guid? registration)
        {
            StateLRSResponse response;

            try
            {
                response = await targetLrs.RetrieveState(id, activity, agent, registration).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                response = new StateQueuedLRSResponse(false, e);
            }

            return response;
        }

        async Task UpdateStateFromResponse(CachedStateDocument localCopy, StateLRSResponse response)
        {
            if (response?.success == true)
            {
                if (response?.content is StateDocument remoteDoc)
                {
                    // timestamp could be set in remoteDoc in the last modified header
                    // it also could not be set in which case we could assume its new
                    if (localCopy?.State is StateDocument localDoc && remoteDoc.timestamp != DateTime.MinValue && localDoc.timestamp > remoteDoc.timestamp)
                    {
                        // local copy is newer
                        response.content = localCopy.State;
                        localCopy.CurrentStatus = CachedStateDocument.Status.Dirty;
                    }
                    else
                    {
                        // remote copy was more up to date so we should up date local copy
                        using (await stateCacheSemaphore.UseWaitAsync().ConfigureAwait(false))
                        {
                            var newResponse = stateCache.Add(new CachedStateDocument(response.content, CachedStateDocument.Status.Clean));
                            response.success = newResponse;
                        }
                    }
                }
            }
            else if (localCopy != null)
            {
                response.success = true;
                response.content = localCopy.State;

                // todo: Find out what else needs to be changed by local copy here
            }
        }

        async void HandleTriggerFired(object sender, EventArgs args)
        {
            await FlushStateResourceQueue().ConfigureAwait(false);
            await FlushStatementQueueWithResponse().ConfigureAwait(false);
        }
    }
}
