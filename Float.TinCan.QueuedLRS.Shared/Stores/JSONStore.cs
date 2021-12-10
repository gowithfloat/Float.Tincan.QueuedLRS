using System;
using System.IO;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Float.TinCan.QueuedLRS.Stores
{
    /// <summary>
    /// Base class for both <see cref="JSONStatementStore"/> and <see cref="JSONStateResourceStore"/>.
    /// </summary>
    public abstract class JSONStore
    {
        // since it's possible two threads could try to write to the store file at once, use a semaphore to prevent file sharing exceptions
        readonly SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Initializes a new instance of the <see cref="JSONStore"/> class.
        /// Subclasses can override this to provide a public constructor.
        /// This initializer will verify that the file path is not null or whitespace.
        /// </summary>
        /// <param name="storeFilePath">Path to the store file.</param>
        internal JSONStore(string storeFilePath)
        {
            if (string.IsNullOrWhiteSpace(storeFilePath))
            {
                throw new ArgumentException("File path is required.", nameof(storeFilePath));
            }

            StoreFilePath = storeFilePath;
        }

        /// <summary>
        /// Gets the store file path.
        /// </summary>
        /// <value>The store file path.</value>
        public string StoreFilePath { get; }

        /// <summary>
        /// Removes data from file by writing an empty array.
        /// </summary>
        public void Empty()
        {
            this.WriteToFile(new JArray());
        }

        /// <summary>
        /// Internal method to write to a file.
        /// Internally, this uses a semaphore to avoid file sharing exceptions.
        /// </summary>
        /// <returns><c>true</c>, if the file was written, <c>false</c> otherwise.</returns>
        /// <param name="array">Array of data to write.</param>
        internal bool WriteToFile(JArray array)
        {
            if (array == null)
            {
                throw new ArgumentNullException(nameof(array));
            }

            using (semaphore.UseWait())
            {
                try
                {
                    using (var file = File.CreateText(StoreFilePath))
                    {
                        new JsonSerializer().Serialize(file, array);
                    }

                    return true;
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                    // if the file is locked, we get an IOException; if readonly, we get unauthorized access
                    // in either case, we just can't write to the file currently
                }
            }

            return false;
        }

        /// <summary>
        /// Internal method to read from the store file.
        /// Internally, this uses a semaphore to avoid file sharing exceptions.
        /// </summary>
        /// <returns>An array of data read from the file.</returns>
        internal JArray ReadFile()
        {
            using (semaphore.UseWait())
            using (var file = File.OpenText(StoreFilePath))
            {
                var serializer = new JsonSerializer();
                object deserialized = null;

                try
                {
                    deserialized = serializer.Deserialize(file, typeof(JArray));
                }
                catch (JsonReaderException)
                {
                    // if we can't parse the file as JSON, just return null
                }

                return deserialized as JArray;
            }
        }
    }
}
