using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TinCan.Documents;
using TinCan.Json;
using Xunit;

namespace Float.TinCan.QueuedLRS.Tests
{
    public class CachedStateDocumentTests
    {
        /// <summary>
        /// It should be possible to build a cached state document with valid inputs.
        /// </summary>
        [Fact]
        public void TestValidInit()
        {
            var state1 = new CachedStateDocument();
            var state2 = new CachedStateDocument(new StateDocument());
            var state3 = new CachedStateDocument(new StateDocument(), CachedStateDocument.Status.Clean);
        }

        /// <summary>
        /// It should be not possible to build a cached state document with invalid inputs.
        /// </summary>
        [Fact]
        public void TestInvalidInit()
        {
            StateDocument nullStateDocument = null;
            Assert.Throws<ArgumentNullException>(() => new CachedStateDocument(nullStateDocument));

            JObject nullJObject = null;
            Assert.Throws<ArgumentNullException>(() => new CachedStateDocument(nullJObject));

            StringOfJSON nullStringOfJson = null;
            Assert.Throws<ArgumentNullException>(() => new CachedStateDocument(nullStringOfJson));

            Assert.Throws<ArgumentNullException>(() => new CachedStateDocument(new StringOfJSON(null)));
            Assert.Throws<JsonReaderException>(() => new CachedStateDocument(new StringOfJSON(string.Empty)));
            Assert.Throws<JsonReaderException>(() => new CachedStateDocument(new StringOfJSON(" ")));
            Assert.Throws<JsonReaderException>(() => new CachedStateDocument(new StringOfJSON("a")));
        }
    }
}
