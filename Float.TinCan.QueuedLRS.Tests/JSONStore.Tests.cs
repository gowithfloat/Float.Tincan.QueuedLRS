using System;
using System.Collections.Generic;
using System.IO;
using Float.TinCan.QueuedLRS.Stores;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Float.TinCan.QueuedLRS.Tests
{
    public class JSONStoreTests
    {
        /// <summary>
        /// Initializing with any valid string should succeed.
        /// </summary>
        [Fact]
        public void TestInit()
        {
            var store = new SimpleJsonStore("a");
        }

        /// <summary>
        /// Initializing with any invalid string should fail.
        /// </summary>
        [Fact]
        public void TestInvalidInit()
        {
            Assert.Throws<ArgumentException>(() => new SimpleJsonStore(null));
            Assert.Throws<ArgumentException>(() => new SimpleJsonStore(string.Empty));
            Assert.Throws<ArgumentException>(() => new SimpleJsonStore(" "));
        }

        /// <summary>
        /// The store should be able to write to the path provided.
        /// </summary>
        [Fact]
        public void TestWrite()
        {
            var store = new SimpleJsonStore(TempPath());
            var array = new JArray(GenerateList());
            store.WriteToFile(array);
        }

        /// <summary>
        /// Reading nothing is fine.
        /// </summary>
        [Fact]
        public void TestReadNothing()
        {
            var store = new SimpleJsonStore(TempPath());
            Assert.Null(store.ReadFile());
        }

        /// <summary>
        /// The store should be able to write to the path provided.
        /// </summary>
        [Fact]
        public void TestWriteInvalid()
        {
            var store = new SimpleJsonStore(TempPath());
            Assert.Throws<ArgumentNullException>(() => store.WriteToFile(null));
        }

        /// <summary>
        /// Reading from the file should return the same contents that were written.
        /// </summary>
        [Fact]
        public void TestRead()
        {
            var store = new SimpleJsonStore(TempPath());
            store.WriteToFile(new JArray(GenerateList()));

            var result = store.ReadFile();
            Assert.Equal(result, GenerateList());
        }

        /// <summary>
        /// Reading from an invalid file should return null.
        /// </summary>
        [Fact]
        public void TestInvalidReadFile()
        {
            var path = TempPath();
            var store = new SimpleJsonStore(path);
            store.WriteToFile(new JArray(GenerateList()));

            File.WriteAllText(path, "ldksafjlasdjkf");
            Assert.Null(store.ReadFile());
        }

        /// <summary>
        /// If the store file is locked, writing should fail but not throw.
        /// </summary>
        [Fact]
        public void TestWriteLockedFile()
        {
            var path = TempPath();
            var store = new SimpleJsonStore(path);
            store.WriteToFile(new JArray(GenerateList()));

            // prevent other threads from accessing the file
            using (var unused = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                // then, try to access
                var success = store.WriteToFile(new JArray(GenerateList()));
                Assert.False(success);
            }
        }

        /// <summary>
        /// If the store file is read only, writing should fail but not throw.
        /// </summary>
        [Fact]
        public void TestWriteReadonlyFile()
        {
            var path = TempPath();
            var store = new SimpleJsonStore(path);
            store.WriteToFile(new JArray(GenerateList()));

            File.SetAttributes(path, FileAttributes.ReadOnly);

            var success = store.WriteToFile(new JArray(GenerateList()));
            Assert.False(success);

            File.SetAttributes(path, FileAttributes.Normal);
        }

        /// <summary>
        /// If file data is removed it should return an empty array
        /// </summary>
        [Fact]
        public void TestRemoveFileData()
        {
            var path = TempPath();
            var store = new SimpleJsonStore(path);
            var array = new JArray(GenerateList());
            store.WriteToFile(array);
            store.Empty();
            Assert.True(store.ReadFile().Count == 0);
        }

        List<object> GenerateList()
        {
            return new List<object>
            {
                "hello",
                22
            };
        }

        string TempPath()
        {
            return Path.Combine(Path.GetTempPath(), Path.GetTempFileName());
        }

        class SimpleJsonStore : JSONStore
        {
            internal SimpleJsonStore(string path) : base(path)
            {
            }
        }
    }
}
