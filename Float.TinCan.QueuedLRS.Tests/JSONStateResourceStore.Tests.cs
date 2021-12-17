using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Float.TinCan.QueuedLRS.Stores;
using Newtonsoft.Json;
using TinCan.Documents;
using Xunit;

namespace Float.TinCan.QueuedLRS.Tests
{
    public class JSONStateResourceStoreTests : IDisposable
    {
        readonly JSONStateResourceStore store;
        readonly string storeFile;

        public JSONStateResourceStoreTests()
        {
            storeFile = Path.Combine(Path.GetTempPath(), "state-resource-store-test.json");
            store = new JSONStateResourceStore(storeFile);
        }

        public void Dispose()
        {
            File.Delete(storeFile);
        }

        /// <summary>
        /// The file store property should matched the value passed to the constructor.
        /// </summary>
        [Fact]
        public void TestStoreFileProperty()
        {
            Assert.Equal(storeFile, store.StoreFilePath);
        }

        /// <summary>
        /// Restoring from an non-existent store should return null.
        /// </summary>
        [Fact]
        public void TestRestoringFromEmptyStore()
        {
            var restoredStatements = store.RestoreStateResources();

            Assert.Null(restoredStatements);
        }

        /// <summary>
        /// The JSON state resource store should allow writing from two async tasks without causing a file access exception.
        /// </summary>
        [Fact]
        public void TestSimultaneousWrite()
        {
            var stateResources1 = StateResourceGenerator.GenerateStateDocuments(40);
            var stateResources2 = StateResourceGenerator.GenerateStateDocuments(40);
            var writeTask1 = WriteResource(stateResources1);
            var writeTask2 = WriteResource(stateResources2);
            Task.WhenAll(writeTask1, writeTask2).Wait();

            // we expect that one of the sets of resources will get written, but it's not certain which one
            // the implementation doesn't append data, and we have no way of knowing which task runs first
            Assert.Equal(40, store.RestoreStateResources().Count);
        }

        /// <summary>
        /// Any valid resources in a list should be persisted. Null references should not.
        /// </summary>
        [Fact]
        public void TestWritePartialInvalid()
        {
            store.WriteStateResources(new List<CachedStateDocument> { null, new CachedStateDocument(new StateDocument { id = "example_id" }), null });
            var resources2 = store.RestoreStateResources();
            Assert.Single(resources2);
            Assert.Equal("example_id", resources2[0].State.id);
        }

        async Task WriteResource(IEnumerable<CachedStateDocument> statements)
        {
            await Task.Delay(100);
            store.WriteStateResources(statements.ToList());
            await Task.Delay(100);
        }

        async Task ReadResource()
        {
            await Task.Delay(100);

            for (int i = 0; i < 100; i++)
            {
                var unused = store.RestoreStateResources();
            }

            await Task.Delay(100);
        }
    }
}
