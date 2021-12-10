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
        /// The store should be able to restore state resources that were previously written
        /// and those state resources should be unchanged after the round-trip.
        /// </summary>
        [Fact]
        public void TestWritingAndRestoringStateResources()
        {
            var stateResources = StateResourceGenerator.GenerateStateDocuments(2).ToList();
            store.WriteStateResources(stateResources);

            // Verify that a file was created
            Assert.True(File.Exists(storeFile));

            var restoredStateResources = store.RestoreStateResources();

            // Verify that the resources that come back are logically the same as the statements that went in
            var expected = JsonConvert.SerializeObject(stateResources);
            var actual = JsonConvert.SerializeObject(restoredStateResources);
            Assert.Equal(expected, actual);
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
        /// Attempting to create a store with an invalid file path should throw an exception.
        /// </summary>
        [Fact]
        public void TestInvalidFilePath()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                var unused1 = new JSONStateResourceStore(null);
            });

            Assert.Throws<ArgumentException>(() =>
            {
                var unused2 = new JSONStateResourceStore(string.Empty);
            });

            Assert.Throws<ArgumentException>(() =>
            {
                var unused3 = new JSONStateResourceStore(" ");
            });

            var unused4 = new JSONStateResourceStore("test");
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
        /// The store should allow reading from multiple tasks.
        /// </summary>
        [Fact]
        public void TestSimultaneousRead()
        {
            store.WriteStateResources(StateResourceGenerator.GenerateStateDocuments(100).ToList());
            var readTask1 = ReadResource();
            var readTask2 = ReadResource();
            var readTask3 = ReadResource();
            Task.WhenAll(readTask1, readTask2, readTask3).Wait();
        }

        /// <summary>
        /// The store should allow reading and writing from multiple tasks.
        /// </summary>
        [Fact]
        public void TestSimultaneousReadWrite()
        {
            var statements1 = StateResourceGenerator.GenerateStateDocuments(50);
            var statements2 = StateResourceGenerator.GenerateStateDocuments(50);
            var task1 = WriteResource(statements1);
            var task2 = ReadResource();
            var task3 = WriteResource(statements2);
            var task4 = ReadResource();
            Task.WhenAll(task1, task2, task3, task4).Wait();
        }

        /// <summary>
        /// Attempting to write a null state resource list should cause an exception.
        /// </summary>
        [Fact]
        public void TestWriteNull()
        {
            Assert.Throws<ArgumentNullException>(() => store.WriteStateResources(null));
        }

        /// <summary>
        /// Individually null state resources should be ignored and not persisted. Writing a list of nulls is equivalent to an empty list.
        /// </summary>
        [Fact]
        public void TestWriteInvalid()
        {
            store.WriteStateResources(new List<CachedStateDocument>());
            var resources1 = store.RestoreStateResources();
            Assert.Null(resources1);

            store.WriteStateResources(new List<CachedStateDocument> { null, null });
            var resources2 = store.RestoreStateResources();
            Assert.Null(resources2);
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

        /// <summary>
        /// If the JSON store file is broken, the app should not crash.
        /// </summary>
        [Fact]
        public void TestRestoreFromInvalidFile()
        {
            var path = Path.Combine(Path.GetTempPath(), "statement-store-test.json");
            var stateResourceStore = new JSONStateResourceStore(path);
            stateResourceStore.WriteStateResources(StateResourceGenerator.GenerateStateDocuments(10).ToList());

            Assert.Equal(10, stateResourceStore.RestoreStateResources().Count);
            File.WriteAllText(path, "aaaaaaa");

            Assert.Null(stateResourceStore.RestoreStateResources());
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
