using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TinCan;
using TinCan.Documents;

namespace Float.TinCan.QueuedLRS
{
    /// <summary>
    /// This class encapsulates a list of state resource documents that have been created by the app. It also contains
    /// all of the helper functions that allow you to add, update, remove, search the saved state resources.
    /// </summary>
    public class StateResourceCache
    {
        //// TODO: Consider making a Dictionary/Hash table; based on the usage it seems like it would be more efficient
        //// I have some questions on this because I couldn't come up with a good key for a given document.
        readonly List<CachedStateDocument> stateCache = new List<CachedStateDocument>();

        /// <summary>
        /// Gets or sets the state queue.
        /// </summary>
        /// <value>The state queue.</value>
        public List<CachedStateDocument> Cache
        {
            get => new List<CachedStateDocument>(stateCache);
            set
            {
                stateCache.Clear();

                if (value != null)
                {
                    stateCache.AddRange(value);
                }
            }
        }

        /// <summary>
        /// Gets the state values that need to be sync'ed from statequeue.
        /// </summary>
        /// <value>The dirty queue.</value>
        public List<CachedStateDocument> DirtyQueue => stateCache.FindAll(t => t.CurrentStatus != CachedStateDocument.Status.Clean);

        /// <summary>
        /// Saves a new state to the saved state queue.
        /// </summary>
        /// <param name="state">This is a state resource coming from the LRS.</param>
        /// <returns>A boolean indicating if Add was successful or not.</returns>
        public bool Add(CachedStateDocument state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            // check to see if version of state is already saved
            var currentIndex = QueueContainsIndex(state.State);
            var addedSuccessfully = true;

            if (currentIndex == -1)
            {
                stateCache.Add(state);
            }
            else
            {
                // To overwrite a state both states need to be json
                // Use StartsWith because content type may include encoding (utf8)
                var contentType = "application/json";
                if (state.State.contentType != null &&
                    state.State.contentType.StartsWith(contentType, StringComparison.OrdinalIgnoreCase) &&
                    stateCache[currentIndex].State.contentType == state.State.contentType)
                {
                    try
                    {
                        var oldJson = JToken.Parse(Encoding.UTF8.GetString(stateCache[currentIndex].State.content));
                        var newJson = JToken.Parse(Encoding.UTF8.GetString(state.State.content));

                        if (oldJson is JObject mergedJson && newJson is JObject json)
                        {
                            foreach (var newObject in json)
                            {
                                var key = newObject.Key;
                                var value = newObject.Value;
                                if (mergedJson.ContainsKey(key))
                                {
                                    mergedJson.Remove(key);
                                }

                                mergedJson.Add(key, value);
                            }

                            state.State.content = Encoding.UTF8.GetBytes(mergedJson.ToString());
                        }

                        stateCache[currentIndex] = state;

                        // We merged the local and the remote
                        // Set the status to dirty to persist those changes
                        stateCache[currentIndex].CurrentStatus = CachedStateDocument.Status.Dirty;
                    }
                    catch (JsonException)
                    {
                        addedSuccessfully = false;
                    }
                }
                else
                {
                    // if the state is not json just replace it.
                    stateCache.RemoveAt(currentIndex);
                    stateCache.Add(state);
                    addedSuccessfully = true;
                }
            }

            return addedSuccessfully;
        }

        /// <summary>
        /// Given details of a certain state, see if it has been saved in our queue.
        /// </summary>
        /// <returns>The state resource if it exists otherwise null.</returns>
        /// <param name="id">State Id you are searching for.</param>
        /// <param name="activity">The originating Activity for this request.</param>
        /// <param name="agent">The originating Agent/User for this request.</param>
        /// <param name="registration">Registration GUID for this state is any.</param>
        public CachedStateDocument RetrieveState(string id, Activity activity, Agent agent, Guid? registration = default)
        {
            return QueueContains(id, activity, agent, registration);
        }

        /// <summary>
        /// Takes the information being looked for and returns the queued state document if it is saved in queue.
        /// </summary>
        /// <returns>The queued state object that matches the query.</returns>
        /// <param name="id">State Identifier.</param>
        /// <param name="activity">Activity information for state.</param>
        /// <param name="agent">Agent information for state.</param>
        /// <param name="registration">Registration GUID of state is any.</param>
        /// <param name="includeDeleted">Include items marked for deletion.</param>
        public CachedStateDocument QueueContains(string id, Activity activity, Agent agent, Guid? registration = default, bool includeDeleted = false)
        {
            return stateCache.FirstOrDefault(t => CreateQueryString(t.State) == CreateQueryString(id, activity, agent, registration) && (IsNotDeleted(t) || includeDeleted));
        }

        /// <summary>
        /// Returns the index of the queued state document that matches the query.
        /// </summary>
        /// <returns>Index of matching document.</returns>
        /// <param name="id">State Identifier.</param>
        /// <param name="activity">Activity information for state.</param>
        /// <param name="agent">Agent information for state.</param>
        /// <param name="registration">Registration GUID of state is any.</param>
        public int QueueContainsIndex(string id, Activity activity, Agent agent, Guid? registration = default)
        {
            return stateCache.FindIndex(t => CreateQueryString(t.State) == CreateQueryString(id, activity, agent, registration) && IsNotDeleted(t));
        }

        /// <summary>
        /// Returns the index of the queued state document that matches the query.
        /// </summary>
        /// <returns>Index of matching document.</returns>
        /// <param name="state">State to match.</param>
        public int QueueContainsIndex(StateDocument state)
        {
            return stateCache.FindIndex(t => CreateQueryString(t.State) == CreateQueryString(state) && IsNotDeleted(t));
        }

        /// <summary>
        /// Remove the specified state from local queue. If object hasn't been synced to remote LRS mark it for delete.
        /// </summary>
        /// <param name="state">State To Remove from List.</param>
        /// <param name="alreadySynced">If set to <c>true</c> already synced.</param>
        public void Remove(StateDocument state, bool alreadySynced)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            var itemToRemove = QueueContains(state.id, state.activity, state.agent, state.registration, true);

            if (itemToRemove != null)
            {
                if (alreadySynced)
                {
                    stateCache.Remove(itemToRemove);
                }
                else
                {
                    // needs to be synced
                    itemToRemove.CurrentStatus = CachedStateDocument.Status.Deleted;
                }
            }
        }

        /// <summary>
        /// Remove all state from local queue that match pattern. If object hasn't been synced to remote LRS mark it for delete.
        /// </summary>
        /// <param name="activity">The Activity to clear from List.</param>
        /// <param name="agent">The agent to clear from List.</param>
        /// <param name="registration">The registration to clear from List.</param>
        /// <param name="alreadySynced">If set to <c>true</c> already synced.</param>
        public void Clear(Activity activity, Agent agent, Guid? registration, bool alreadySynced)
        {
            stateCache.FindAll(t => CreateQueryString(t.State, false) == CreateQueryString(activity, agent, registration) && IsNotDeleted(t))
                      .ForEach(state =>
            {
                if (alreadySynced)
                {
                    stateCache.Remove(state);
                }
                else
                {
                    state.CurrentStatus = CachedStateDocument.Status.Deleted;
                }
            });
        }

        /// <summary>
        /// Gets the state identifiers.
        /// </summary>
        /// <returns>The state identifiers.</returns>
        /// <param name="activity">Activity generating the state.</param>
        /// <param name="agent">Agent associated with the state.</param>
        /// <param name="registration">Registration GUID of state if any.</param>
        public List<string> GetStateIds(Activity activity, Agent agent, Guid? registration = default)
        {
            return stateCache.Where(t => CreateQueryString(t.State, false) == CreateQueryString(activity, agent, registration) && IsNotDeleted(t))
                             .Select(t => t.State.id)
                             .ToList();
        }

        static string CreateQueryString(Activity activity, Agent agent, Guid? registration = default)
        {
            return CreateQueryString(string.Empty, activity, agent, registration);
        }

        static string CreateQueryString(StateDocument state, bool includeId = true)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            return CreateQueryString(includeId ? state.id : string.Empty, state.activity, state.agent, state.registration);
        }

        static string CreateQueryString(string id, Activity activity, Agent agent, Guid? registration = default)
        {
            if (activity == null)
            {
                throw new ArgumentNullException(nameof(activity));
            }

            if (agent == null)
            {
                throw new ArgumentNullException(nameof(agent));
            }

            return id + activity.id + agent.ToJSON() + (registration?.ToString() ?? string.Empty);
        }

        static bool IsNotDeleted(CachedStateDocument stateDocument)
        {
            if (stateDocument == null)
            {
                throw new ArgumentNullException(nameof(stateDocument));
            }

            return stateDocument.CurrentStatus != CachedStateDocument.Status.Deleted;
        }
    }
}
