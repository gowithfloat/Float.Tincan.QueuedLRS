using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Float.TinCan.QueuedLRS.Stores;
using Newtonsoft.Json;
using TinCan;
using Xunit;

namespace Float.TinCan.QueuedLRS.Tests
{
    public class JSONStatementStoreTests : IDisposable
    {
        readonly JSONStatementStore store;
        readonly string storeFile;

        public JSONStatementStoreTests()
        {
            storeFile = Path.Combine(Path.GetTempPath(), "statement-store-test.json");
            store = new JSONStatementStore(storeFile);
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
        /// The store should be able to restore statements that were previously written
        /// and those statements should be unchanged after the round-trip.
        /// </summary>
        [Fact]
        public void TestWritingAndRestoringStatements()
        {
            var statements = StatementGenerator.GenerateStatements(2);
            store.WriteStatements(statements);

            // Verify that a file was created
            Assert.True(File.Exists(storeFile));

            var restoredStatements = store.RestoreStatements();

            // Verify that the statements that come back are logically the same as the statements that went in
            var expected = JsonConvert.SerializeObject(statements);
            var actual = JsonConvert.SerializeObject(restoredStatements);
            Assert.Equal(expected, actual);
        }

        /// <summary>
        /// Restoring from an non-existent store should return null.
        /// </summary>
        [Fact]
        public void TestRestoringFromEmptyStore()
        {
            var restoredStatements = store.RestoreStatements();

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
                var unused1 = new JSONStatementStore(null);
            });

            Assert.Throws<ArgumentException>(() =>
            {
                var unused2 = new JSONStatementStore(string.Empty);
            });

            Assert.Throws<ArgumentException>(() =>
            {
                var unused3 = new JSONStatementStore(" ");
            });

            var unused4 = new JSONStatementStore("test");
        }

        /// <summary>
        /// The JSON statement store should allow writing from two async tasks without causing a file access exception.
        /// </summary>
        [Fact]
        public void TestSimultaneousWrite()
        {
            var statements1 = StatementGenerator.GenerateStatements(40);
            var statements2 = StatementGenerator.GenerateStatements(40);
            var writeTask1 = WriteStatement(statements1);
            var writeTask2 = WriteStatement(statements2);
            Task.WhenAll(writeTask1, writeTask2).Wait();

            // we expect that one of the sets of statements will get written, but it's not certain which one
            // the implementation doesn't append data, and we have no way of knowing which task runs first
            Assert.Equal(40, store.RestoreStatements().Count);
        }

        /// <summary>
        /// The store should allow reading from multiple tasks.
        /// </summary>
        [Fact]
        public void TestSimultaneousRead()
        {
            store.WriteStatements(StatementGenerator.GenerateStatements(100));
            var readTask1 = ReadStatement();
            var readTask2 = ReadStatement();
            var readTask3 = ReadStatement();
            Task.WhenAll(readTask1, readTask2, readTask3).Wait();
        }

        /// <summary>
        /// The store should allow reading and writing from multiple tasks.
        /// </summary>
        [Fact]
        public void TestSimultaneousReadWrite()
        {
            var statements1 = StatementGenerator.GenerateStatements(50);
            var statements2 = StatementGenerator.GenerateStatements(50);
            var task1 = WriteStatement(statements1);
            var task2 = ReadStatement();
            var task3 = WriteStatement(statements2);
            var task4 = ReadStatement();
            Task.WhenAll(task1, task2, task3, task4).Wait();
        }

        /// <summary>
        /// Attempting to write a null statement list causes an exception.
        /// </summary>
        [Fact]
        public void TestWriteNull()
        {
            Assert.Throws<ArgumentNullException>(() => store.WriteStatements(null));
        }

        /// <summary>
        /// Writing an empty or null-only list should be allowed.
        /// </summary>
        [Fact]
        public void TestWriteInvalid()
        {
            store.WriteStatements(new List<Statement>());
            store.WriteStatements(new List<Statement> { null, null });
        }

        /// <summary>
        /// Any valid statements in a list should be persisted. Null references should not.
        /// </summary>
        [Fact]
        public void TestWritePartialInvalid()
        {
            var id = Guid.NewGuid();
            store.WriteStatements(new List<Statement> { null, new Statement { id = id }, null });
            var statements = store.RestoreStatements();
            Assert.Single(statements);
            Assert.Equal(id, statements[0].id);
        }

        /// <summary>
        /// If the JSON store file is broken, the app should not crash.
        /// </summary>
        [Fact]
        public void TestRestoreFromInvalidFile()
        {
            var path = Path.Combine(Path.GetTempPath(), "statement-store-test.json");
            var statementStore = new JSONStatementStore(path);
            statementStore.WriteStatements(StatementGenerator.GenerateStatements(10));

            Assert.Equal(10, statementStore.RestoreStatements().Count);
            File.WriteAllText(path, "aaaaaaa");

            Assert.Null(statementStore.RestoreStatements());
        }

        async Task WriteStatement(List<Statement> statements)
        {
            await Task.Delay(100);
            store.WriteStatements(statements);
            await Task.Delay(100);
        }

        async Task ReadStatement()
        {
            await Task.Delay(100);

            for (int i = 0; i < 100; i++)
            {
                var unused = store.RestoreStatements();
            }

            await Task.Delay(100);
        }
    }
}
