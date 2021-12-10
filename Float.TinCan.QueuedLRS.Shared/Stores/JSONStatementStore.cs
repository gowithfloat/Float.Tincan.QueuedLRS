using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using TinCan;

namespace Float.TinCan.QueuedLRS.Stores
{
    /// <summary>
    /// JSON statement store for the statement queue.
    /// </summary>
    public class JSONStatementStore : JSONStore, IStatementStore
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="JSONStatementStore"/> class.
        /// </summary>
        /// <param name="storeFilePath">Store file path.</param>
        public JSONStatementStore(string storeFilePath) : base(storeFilePath)
        {
        }

        /// <inheritdoc />
        public bool WriteStatements(List<Statement> statements)
        {
            if (statements == null)
            {
                throw new ArgumentNullException(nameof(statements));
            }

            return WriteToFile(new JArray(statements.Select(arg => arg?.ToJObject(null)).Where(arg => arg != null)));
        }

        /// <inheritdoc />
        public List<Statement> RestoreStatements()
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
                    return array.OfType<JObject>()
                                .Where(jobj => jobj != null)
                                .Select(jobj => new Statement(jobj))
                                .Where(statement => statement != null)
                                .ToList();
                }
            }
            catch (FileNotFoundException)
            {
            }

            return null;
        }
    }
}
