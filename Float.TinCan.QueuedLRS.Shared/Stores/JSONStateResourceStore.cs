using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Float.TinCan.QueuedLRS.Stores
{
    /// <summary>
    /// JSON statement store for the state resource queue.
    /// </summary>
    public class JSONStateResourceStore : JSONStore, IStateResourceStore
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="JSONStateResourceStore"/> class.
        /// </summary>
        /// <param name="storeFilePath">Store file path.</param>
        public JSONStateResourceStore(string storeFilePath) : base(storeFilePath)
        {
        }

        /// <inheritdoc />
        public bool WriteStateResources(List<CachedStateDocument> stateResources)
        {
            if (stateResources == null)
            {
                throw new ArgumentNullException(nameof(stateResources));
            }

            return WriteToFile(new JArray(stateResources.Select(arg => arg?.ToJObject(null)).Where(arg => arg != null)));
        }

        /// <inheritdoc />
        public List<CachedStateDocument> RestoreStateResources()
        {
            if (!File.Exists(StoreFilePath))
            {
                return null;
            }

            try
            {
                var array = ReadFile();

                if (array?.Any() == true)
                {
                    return array.Select(arg => new CachedStateDocument((JObject)arg)).ToList();
                }
            }
            catch (FileNotFoundException)
            {
            }
            catch (JsonException)
            {
            }

            return null;
        }
    }
}
