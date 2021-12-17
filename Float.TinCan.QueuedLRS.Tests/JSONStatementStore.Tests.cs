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
