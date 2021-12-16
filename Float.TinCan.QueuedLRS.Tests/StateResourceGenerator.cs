using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using TinCan;
using TinCan.Documents;

namespace Float.TinCan.QueuedLRS.Tests
{
    public static class StateResourceGenerator
    {
        /// <summary>
        /// A Method to generate test Queue state documents
        /// </summary>
        /// <returns>The List of Queued state documents.</returns>
        /// <param name="numberToCreate">Number to create.</param>
        public static IEnumerable<CachedStateDocument> GenerateStateDocuments(int numberToCreate)
        {
            return Enumerable.Range(0, numberToCreate).Select(GenerateStateDocument);
        }

        public static CachedStateDocument GenerateStateDocument(int index = 0)
        {
            return new CachedStateDocument
            {
                CurrentStatus = (index % 2 == 0) ? CachedStateDocument.Status.Dirty : CachedStateDocument.Status.Clean,
                State = GenerateState(index)
            };
        }

        public static StateDocument GenerateState(int index = 0)
        {
            return new StateDocument
            {
                id = $"www.example.com/activities/state{index}",
                activity = new Activity
                {
                    id = new Uri($"http://example.com/activities/example-activity{index}")
                },
                agent = new Agent
                {
                    mbox = $"mailto:jdoe@example.com{index}"
                },
                registration = Guid.NewGuid(),
                timestamp = DateTime.UtcNow,
                contentType = "application/json; charset=utf-8",
                content = Encoding.UTF8.GetBytes(new JObject
                {
                    {
                        $"data{index}", "MyData"
                    }
                }.ToString()),
                etag = $"TestTag_{index}",
            };
        }

        public static IEnumerable<StateDocument> GenerateSingleUserStateDocuments(int numberToCreate)
        {
            return Enumerable.Range(0, numberToCreate).Select(GenerateSingleUserStateDocument);
        }

        public static StateDocument GenerateSingleUserStateDocument(int index = 0)
        {
            return new StateDocument
            {
                id = $"www.example.com/activities/state{index}",
                activity = new Activity
                {
                    id = new Uri("http://example.com/activities/example-activity")
                },
                agent = new Agent
                {
                    mbox = "mailto:jdoe@example.com",
                    name = "Test",
                    openid = "test"
                },
                registration = new Guid("632283d7c51c4aa8ba548813b74453f4"),
                timestamp = DateTime.UtcNow,
                contentType = "application/json",
                content = Encoding.UTF8.GetBytes(new JObject
                {
                    {
                        $"data{index}", "MyData"
                    }
                }.ToString())
            };
        }

        public static IEnumerable<StateDocument> GenerateSingleUserNonJsonStateDocuments(int numberToCreate)
        {
            return Enumerable.Range(0, numberToCreate).Select(GenerateSingleUserNonJsonStateDocument);
        }

        public static StateDocument GenerateSingleUserNonJsonStateDocument(int index = 0)
        {
            return new StateDocument
            {
                id = $"www.example.com/activities/state{index}",
                activity = new Activity
                {
                    id = new Uri("http://example.com/activities/example-activity")
                },
                agent = new Agent
                {
                    mbox = "mailto:jdoe@example.com",
                    name = "Test",
                    openid = "test"
                },
                registration = new Guid("632283d7c51c4aa8ba548813b74453f4"),
                timestamp = DateTime.UtcNow,
                contentType = "application/octet-stream",
                content = Encoding.UTF8.GetBytes($"632283d7c51c4aa8ba548813b74453f{index}")
            };
        }

        public static IEnumerable<CachedStateDocument> GenerateNonJsonStateDocuments(int numberToCreate)
        {
            return Enumerable.Range(0, numberToCreate).Select(GenerateNonJsonStateDocument);
        }

        public static CachedStateDocument GenerateNonJsonStateDocument(int index = 0)
        {
            return new CachedStateDocument
            {
                CurrentStatus = (index % 2 == 0) ? CachedStateDocument.Status.Dirty : CachedStateDocument.Status.Clean,
                State = GenerateNonJsonState(index)
            };
        }

        public static StateDocument GenerateNonJsonState(int index = 0)
        {
            return new StateDocument
            {
                id = $"www.example.com/activities/state{index}",
                activity = new Activity
                {
                    id = new Uri($"http://example.com/activities/example-activity{index}")
                },
                agent = new Agent
                {
                    mbox = $"mailto:jdoe@example.com{index}"
                },
                registration = Guid.NewGuid(),
                timestamp = DateTime.UtcNow,
                contentType = "application/octet-stream",
                content = Encoding.UTF8.GetBytes($"632283d7c51c4aa8ba548813b74453f{index}"),
                etag = $"TestTag_{index}"
            };
        }
    }
}
