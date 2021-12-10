using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Float.TinCan.QueuedLRS.Stores;
using Float.TinCan.QueuedLRS.Triggers;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TinCan;
using TinCan.Documents;
using TinCan.LRSResponses;
using Xunit;

namespace Float.TinCan.QueuedLRS.Tests
{
    public class QueuedStateResourceTests
    {
        readonly Mock<ILRS> spyLRS = new Mock<ILRS>();
        readonly Mock<IStatementStore> spyStatementStore = new Mock<IStatementStore>();
        readonly Mock<IStateResourceStore> spyStateResourceStore = new Mock<IStateResourceStore>();
        readonly Mock<IQueueFlushTrigger> spyTrigger = new Mock<IQueueFlushTrigger>();
        readonly MockQueueFlushTrigger mockTrigger = new MockQueueFlushTrigger();
        readonly QueuedLRS queuedLRS;

        public QueuedStateResourceTests()
        {
            spyLRS.Invocations.Clear();
            spyStatementStore.Invocations.Clear();
            spyStateResourceStore.Invocations.Clear();
            spyTrigger.Invocations.Clear();
            var triggers = new IQueueFlushTrigger[] { spyTrigger.Object, mockTrigger };
            queuedLRS = new QueuedLRS(spyLRS.Object, spyStatementStore.Object, spyStateResourceStore.Object, triggers);
        }

        /// <summary>
        /// On Startup the QueuedLRS should attempt to restore state Resources from store
        /// </summary>
        [Fact]
        public void TestReadStoredStatesAtStartup()
        {
            Assert.Equal(0, queuedLRS.QueueSize);
            spyStateResourceStore.Verify(store => store.RestoreStateResources(), Times.Once);
        }

        /// <summary>
        /// Tests that state gets saved to local queue and an attempt is made to send it to remote.
        /// </summary>
        [Fact]
        public async Task TestWritingState()
        {
            spyLRS.Invocations.Clear();
            var state = StateResourceGenerator.GenerateState();

            var successResponse = new LRSResponse { success = true };
            spyLRS.SetupSequence(lrs => lrs.SaveState(It.IsAny<StateDocument>()))
                  .Returns(Task.FromResult(successResponse));    // Return success the second time

            Assert.Empty(queuedLRS.StateCache);
            await queuedLRS.SaveState(state);
            Assert.Single(queuedLRS.StateCache);
            mockTrigger.Fire();

            // System should attempt to write when a new state comes in
            spyLRS.Verify(lrs => lrs.SaveState(state), Times.Once);
            spyLRS.Invocations.Clear();
        }

        /// <summary>
        /// When new data is added to the state queue it should also be flushed to the persistent copy in memory
        /// </summary>
        [Fact]
        public async Task TestPersistToStore()
        {
            spyLRS.Invocations.Clear();
            var state = StateResourceGenerator.GenerateState();
            var successResponse = new LRSResponse { success = true };
            spyLRS.SetupSequence(lrs => lrs.SaveState(It.IsAny<StateDocument>()))
                  .Returns(Task.FromResult(successResponse));    // Return success

            spyStateResourceStore.Invocations.Clear();

            await queuedLRS.SaveState(state);

            // System should attempt to write when a new state comes in
            spyStateResourceStore.Verify(store => store.WriteStateResources(queuedLRS.StateCache), Times.Once);
            spyStateResourceStore.Invocations.Clear();
        }

        /// <summary>
        /// When new data is added to the state queue it should also be flushed to the persistent copy in memory
        /// </summary>
        [Fact]
        public async Task TestPersistNonJsonToStore()
        {
            spyLRS.Invocations.Clear();
            var state = StateResourceGenerator.GenerateNonJsonState();
            var successResponse = new LRSResponse { success = true };
            spyLRS.SetupSequence(lrs => lrs.SaveState(It.IsAny<StateDocument>()))
                  .Returns(Task.FromResult(successResponse));    // Return success

            spyStateResourceStore.Invocations.Clear();

            await queuedLRS.SaveState(state);

            // System should attempt to write when a new state comes in
            spyStateResourceStore.Verify(store => store.WriteStateResources(queuedLRS.StateCache), Times.Once);
            spyStateResourceStore.Invocations.Clear();
        }

        /// <summary>
        /// If the queuedLRS has no connection to remoteLRS a copy of any stored state values
        /// should be return when data is requested
        /// </summary>
        [Fact]
        public async Task TestRestorefromLocalState()
        {
            spyLRS.Invocations.Clear();
            var state = StateResourceGenerator.GenerateState();

            var stateErrorResponse = new StateLRSResponse { success = false };
            spyLRS.SetupSequence(lrs => lrs.RetrieveState(It.IsAny<string>(), It.IsAny<Activity>(), It.IsAny<Agent>(), It.IsAny<Guid>()))
                  .Returns(Task.FromResult(stateErrorResponse))
                  .Returns(Task.FromResult(stateErrorResponse));

            var errorResponse = new LRSResponse { success = false };
            spyLRS.SetupSequence(lrs => lrs.SaveState(It.IsAny<StateDocument>()))
                  .Returns(Task.FromResult(errorResponse));      // Return error the first time

            // if no states are saved locally call should fail
            var response = await queuedLRS.RetrieveState(state.id, state.activity, state.agent, state.registration);

            Assert.False(response.success);

            // now save a state
            await queuedLRS.SaveState(state);

            response = await queuedLRS.RetrieveState(state.id, state.activity, state.agent, state.registration);

            // We should get a successful response
            Assert.True(response.success);

            // Check that we get correct state back again
            var expected = JsonConvert.SerializeObject(state);
            var actual = JsonConvert.SerializeObject(response.content);
            Assert.Equal(expected, actual);

            spyLRS.Invocations.Clear();
        }

        /// <summary>
        /// If the queuedLRS has no connection to remoteLRS a copy of any stored state values
        /// should be return when data is requested
        /// </summary>
        [Fact]
        public async Task TestRestoreNonJsonfromLocalState()
        {
            spyLRS.Invocations.Clear();
            var state = StateResourceGenerator.GenerateNonJsonState();

            var stateErrorResponse = new StateLRSResponse() { success = false };
            spyLRS.SetupSequence(lrs => lrs.RetrieveState(It.IsAny<string>(), It.IsAny<Activity>(), It.IsAny<Agent>(), It.IsAny<Guid>()))
                  .Returns(Task.FromResult(stateErrorResponse))
                  .Returns(Task.FromResult(stateErrorResponse));

            var errorResponse = new LRSResponse { success = false };
            spyLRS.SetupSequence(lrs => lrs.SaveState(It.IsAny<StateDocument>()))
                  .Returns(Task.FromResult(errorResponse));      // Return error the first time

            // if no states are saved locally call should fail
            var response = await queuedLRS.RetrieveState(state.id, state.activity, state.agent, state.registration);

            Assert.False(response.success);

            // now save a state
            await queuedLRS.SaveState(state);

            response = await queuedLRS.RetrieveState(state.id, state.activity, state.agent, state.registration);

            // We should get a successful response
            Assert.True(response.success);

            // Check that we get correct state back again
            var expected = JsonConvert.SerializeObject(state);
            var actual = JsonConvert.SerializeObject(response.content);
            Assert.Equal(expected, actual);

            spyLRS.Invocations.Clear();
        }

        /// <summary>
        /// When a trigger fires, the queue should be flushed.
        /// </summary>
        [Fact]
        public async Task TestTriggerFire()
        {
            var state = StateResourceGenerator.GenerateState();

            spyLRS.SetupSequence(lrs => lrs.SaveState(It.IsAny<StateDocument>()))
                  .Returns(Task.FromResult(new LRSResponse { success = false }))
                  .Returns(Task.FromResult(new LRSResponse { success = true }));

            await queuedLRS.SaveState(state);
            Assert.Single(queuedLRS.StateCache);

            spyLRS.Invocations.Clear();

            mockTrigger.Fire();
            spyLRS.Verify(lrs => lrs.SaveState(state), Times.Once);
        }

        /// <summary>
        /// If initial write to LRS was successful trigger should not send anything new
        /// </summary>
        [Fact]
        public async Task TestTriggerFireWithCleanQueue()
        {
            var state = StateResourceGenerator.GenerateState();

            var stateSuccessResponse = new LRSResponse { success = true };
            spyLRS.Invocations.Clear();
            spyLRS.SetupSequence(lrs => lrs.SaveState(It.IsAny<StateDocument>()))
                  .Returns(Task.FromResult(stateSuccessResponse));

            await queuedLRS.SaveState(state);

            mockTrigger.Fire();
            spyLRS.Verify(lrs => lrs.SaveState(state), Times.Once);
        }

        /// <summary>
        /// Only dirty data should be written to remote LRS
        /// </summary>
        [Fact]
        public async Task TestTriggerFireWriteOnlyDirtyStates()
        {
            var state1 = StateResourceGenerator.GenerateState();
            var state2 = StateResourceGenerator.GenerateState();

            var stateSuccessResponse = new LRSResponse { success = true };
            var stateErrorResponse = new LRSResponse { success = false };
            spyLRS.SetupSequence(lrs => lrs.SaveState(It.IsAny<StateDocument>()))
                  .Returns(Task.FromResult(stateErrorResponse))
                  .Returns(Task.FromResult(stateSuccessResponse))
                  .Returns(Task.FromResult(stateSuccessResponse));

            await queuedLRS.SaveState(state1);
            await queuedLRS.SaveState(state2);

            // Should have been called twice here (one fail and one pass)
            spyLRS.Verify(lrs => lrs.SaveState(It.IsAny<StateDocument>()), Times.Exactly(2));
            spyLRS.Invocations.Clear();

            mockTrigger.Fire();

            spyLRS.Verify(lrs => lrs.SaveState(state1), Times.Once());
            spyLRS.Verify(lrs => lrs.SaveState(state2), Times.Never());

            // further triggers should not write anything
            spyLRS.Invocations.Clear();
            mockTrigger.Fire();

            spyLRS.Verify(lrs => lrs.SaveState(state1), Times.Never());
            spyLRS.Verify(lrs => lrs.SaveState(state2), Times.Never());
        }

        /// <summary>
        /// Mutliple states may need to be written when a trigger occurs
        /// </summary>
        [Fact]
        public async Task TestTriggerFireWriteMultipleStates()
        {
            var state1 = StateResourceGenerator.GenerateState();
            var state2 = StateResourceGenerator.GenerateState();

            var stateSuccessResponse = new LRSResponse { success = true };
            var stateErrorResponse = new LRSResponse { success = false };
            spyLRS.SetupSequence(lrs => lrs.SaveState(It.IsAny<StateDocument>()))
                  .Returns(Task.FromResult(stateErrorResponse))
                  .Returns(Task.FromResult(stateErrorResponse))
                  .Returns(Task.FromResult(stateSuccessResponse))
                  .Returns(Task.FromResult(stateSuccessResponse));

            await queuedLRS.SaveState(state1);
            await queuedLRS.SaveState(state2);

            // Should have been called twice here (both fails)
            spyLRS.Verify(lrs => lrs.SaveState(It.IsAny<StateDocument>()), Times.Exactly(2));
            spyLRS.Invocations.Clear();

            mockTrigger.Fire();

            // Since both failed we should see both written
            spyLRS.Verify(lrs => lrs.SaveState(state1), Times.Once());
            spyLRS.Verify(lrs => lrs.SaveState(state2), Times.Once());

            // further triggers should not write anything
            spyLRS.Invocations.Clear();
            mockTrigger.Fire();

            spyLRS.Verify(lrs => lrs.SaveState(state1), Times.Never());
            spyLRS.Verify(lrs => lrs.SaveState(state2), Times.Never());
        }

        /// <summary>
        /// Test to ensure that state value in queue is overwritten if state id, activity agent and registration are the same
        /// </summary>
        [Fact]
        public async Task TestStateOverwrite()
        {
            spyLRS.Invocations.Clear();

            var state1 = StateResourceGenerator.GenerateSingleUserStateDocument(0);
            var state2 = StateResourceGenerator.GenerateSingleUserStateDocument(1);
            state2.id = state1.id;
            var data = new JObject();
            data.Add("data0", "MyData");
            data.Add("data1", "MyData");

            // Need the connect to be down to get local copy
            var stateErrorResponse = new StateLRSResponse { success = false };
            spyLRS.SetupSequence(lrs => lrs.RetrieveState(It.IsAny<string>(), It.IsAny<Activity>(), It.IsAny<Agent>(), It.IsAny<Guid>()))
                  .Returns(Task.FromResult(stateErrorResponse));

            var errorResponse = new LRSResponse { success = false };
            spyLRS.SetupSequence(lrs => lrs.SaveState(It.IsAny<StateDocument>()))
                  .Returns(Task.FromResult(errorResponse)) // Return error the first time
                  .Returns(Task.FromResult(errorResponse))
                  .Returns(Task.FromResult(errorResponse));

            await queuedLRS.SaveState(state1);
            await queuedLRS.SaveState(state2);

            var response = await queuedLRS.RetrieveState(state1.id, state1.activity, state1.agent, state1.registration);

            Assert.Single(queuedLRS.StateCache);
            Assert.Equal(data.ToString(), Encoding.UTF8.GetString(response.content.content));
        }

        /// <summary>
        /// Test to ensure that state value in queue is overwritten if state id, activity agent and registration are the same
        /// </summary>
        [Fact]
        public async Task TestNonJsonStateOverwrite()
        {
            spyLRS.Invocations.Clear();
            var state1 = StateResourceGenerator.GenerateSingleUserNonJsonStateDocument(0);
            var state2 = StateResourceGenerator.GenerateSingleUserNonJsonStateDocument(1);
            state2.id = state1.id;

            // Need the connect to be down to get local copy
            var stateErrorResponse = new StateLRSResponse() { success = false };
            spyLRS.SetupSequence(lrs => lrs.RetrieveState(It.IsAny<string>(), It.IsAny<Activity>(), It.IsAny<Agent>(), It.IsAny<Guid>()))
                  .Returns(Task.FromResult(stateErrorResponse));

            var errorResponse = new LRSResponse { success = false };
            spyLRS.SetupSequence(lrs => lrs.SaveState(It.IsAny<StateDocument>()))
                  .Returns(Task.FromResult(errorResponse)) // Return error the first time
                  .Returns(Task.FromResult(errorResponse))
                  .Returns(Task.FromResult(errorResponse));

            await queuedLRS.SaveState(state1);
            await queuedLRS.SaveState(state2);

            var response = await queuedLRS.RetrieveState(state1.id, state1.activity, state1.agent, state1.registration);

            Assert.Single(queuedLRS.StateCache);
            Assert.Equal("632283d7c51c4aa8ba548813b74453f1", Encoding.UTF8.GetString(response.content.content));
        }

        /// <summary>
        /// Given a remote and local copy of a state ensure that newest version is returned
        /// </summary>
        [Fact]
        public async Task TestRetrieveGetsNewestState()
        {
            spyLRS.Invocations.Clear();
            var state1 = StateResourceGenerator.GenerateState();
            var state2 = new StateDocument
            {
                id = state1.id,
                activity = state1.activity,
                agent = state1.agent,
                registration = state1.registration,
                content = Encoding.UTF8.GetBytes("Test Message"),
                contentType = "application/json; charset=utf-8"
            };

            // Need the connect to be down to get local copy
            var stateSuccessResponse = new StateLRSResponse { success = true, content = state2 };
            spyLRS.SetupSequence(lrs => lrs.RetrieveState(It.IsAny<string>(), It.IsAny<Activity>(), It.IsAny<Agent>(), It.IsAny<Guid>()))
                  .Returns(Task.FromResult(stateSuccessResponse));

            var stateErrorResponse = new LRSResponse { success = false };
            spyLRS.SetupSequence(lrs => lrs.SaveState(It.IsAny<StateDocument>()))
                  .Returns(Task.FromResult(stateErrorResponse));

            state1.timestamp = new DateTime(2017, 11, 10, 14, 00, 00);
            state2.timestamp = new DateTime(2017, 11, 09, 12, 00, 00);

            // Save a state to the LRS this will be marked as needing sync
            await queuedLRS.SaveState(state1);

            var response = await queuedLRS.RetrieveState(state1.id, state1.activity, state1.agent, state1.registration);

            // in first test response should return state 1 because it is newer
            Assert.Equal(state1, response.content);

            // now set timestamp in state 2 to be newer
            state2.timestamp = new DateTime(2017, 11, 10, 16, 00, 00);
            stateSuccessResponse = new StateLRSResponse() { success = true, content = state2 };
            spyLRS.SetupSequence(lrs => lrs.RetrieveState(It.IsAny<string>(), It.IsAny<Activity>(), It.IsAny<Agent>(), It.IsAny<Guid>()))
                  .Returns(Task.FromResult(stateSuccessResponse));
            response = await queuedLRS.RetrieveState(state1.id, state1.activity, state1.agent, state1.registration);
            // since state 2 now has newer timestamp it should be returned
            Assert.Equal(state2, response.content);
        }

        /// <summary>
        /// Given a remote and local copy of a state ensure that they can be merged
        /// </summary>
        [Fact]
        public async Task TestRetrieveAndMergeState()
        {
            spyLRS.Invocations.Clear();
            var state1 = StateResourceGenerator.GenerateState();
            var state2Payload = new JObject(
                new JProperty("merge", "this")
            );
            var state2 = new StateDocument
            {
                id = state1.id,
                activity = state1.activity,
                agent = state1.agent,
                registration = state1.registration,
                content = Encoding.UTF8.GetBytes(state2Payload.ToString()),
                contentType = "application/json; charset=utf-8"
            };

            // Need the connect to be down to get local copy
            var stateSuccessResponse = new StateLRSResponse { success = true, content = state2 };
            spyLRS.SetupSequence(lrs => lrs.RetrieveState(It.IsAny<string>(), It.IsAny<Activity>(), It.IsAny<Agent>(), It.IsAny<Guid>()))
                  .Returns(Task.FromResult(stateSuccessResponse));

            var stateErrorResponse = new LRSResponse { success = false };
            spyLRS.SetupSequence(lrs => lrs.SaveState(It.IsAny<StateDocument>()))
                  .Returns(Task.FromResult(stateErrorResponse));

            // Save a state to the LRS this will be marked as needing sync
            await queuedLRS.SaveState(state1);

            var response = await queuedLRS.RetrieveState(state1.id, state1.activity, state1.agent, state1.registration);
            var finalPayload = new JObject(
                new JProperty("data0", "MyData"),
                new JProperty("merge", "this")
            );
            // since state 2 now has newer timestamp it should be returned
            Assert.Equal(Encoding.UTF8.GetString(response.content.content), finalPayload.ToString());
        }

        /// <summary>
        /// Tests the retrieve state warms cache.
        /// If the Queued LRS receives state from the remote LRS that it has never seen before,
        /// it should be cached locally.
        /// </summary>
        [Fact]
        public async Task TestRetrieveStateWarmsCache()
        {
            spyLRS.Invocations.Clear();

            // Set up our test state
            var state = StateResourceGenerator.GenerateSingleUserStateDocument();
            var stateSuccessResponse = new StateLRSResponse { success = true, content = state };
            spyLRS.SetupSequence(lrs => lrs.RetrieveState(It.IsAny<string>(), It.IsAny<Activity>(), It.IsAny<Agent>(), It.IsAny<Guid>()))
                  .Throws(new HttpRequestException())
                  .Returns(Task.FromResult(stateSuccessResponse))
                  .Throws(new HttpRequestException());

            // Verify local state is empty when retrieving state when offline
            var result1 = await queuedLRS.RetrieveState(state.id, state.activity, state.agent, state.registration);
            Assert.False(result1.success);

            // Verify local cache is empty
            Assert.Empty(queuedLRS.StateCache);

            // Now online, retrieve state and verify remote state was retrieved
            var result2 = await queuedLRS.RetrieveState(state.id, state.activity, state.agent, state.registration);
            Assert.True(result2.success);
            Assert.Equal(state, result2.content);

            // Verify that the local cache contains the remote state
            Assert.NotEmpty(queuedLRS.StateCache);
            Assert.Equal(state, queuedLRS.StateCache.FirstOrDefault().State);

            // Verify that calling the LRS again while offline now retrieves this state
            var result3 = await queuedLRS.RetrieveState(state.id, state.activity, state.agent, state.registration);
            Assert.True(result3.success);
            Assert.Equal(state, result3.content);
        }

        /// <summary>
        /// Ensures that delete state works when both save and delete calls happen
        /// online
        /// </summary>
        [Fact]
        public async Task TestDeleteStateOnline()
        {
            var stateList = StateResourceGenerator.GenerateStateDocuments(1).ToList();
            var state1 = stateList[0].State;

            // online test
            var stateSuccessResponse = new LRSResponse() { success = true };
            spyLRS.SetupSequence(lrs => lrs.DeleteState(It.IsAny<StateDocument>()))
                  .Returns(Task.FromResult(stateSuccessResponse));
            spyLRS.SetupSequence(lrs => lrs.SaveState(It.IsAny<StateDocument>()))
                  .Returns(Task.FromResult(stateSuccessResponse));

            await queuedLRS.SaveState(state1);
            Assert.Single(queuedLRS.StateCache);
            await queuedLRS.FlushStateResourceQueue();
            await queuedLRS.DeleteState(state1);
            await queuedLRS.FlushStateResourceQueue();
            Assert.Empty(queuedLRS.StateCache);
        }

        /// <summary>
        /// Ensures that delete state works when both save and delete calls happen
        /// offline
        /// </summary>
        [Fact]
        public async Task TestDeleteStateOffline()
        {
            spyLRS.Invocations.Clear();
            var state1 = StateResourceGenerator.GenerateState();

            // offline test
            var stateErrorResponse = new LRSResponse { success = false };
            var stateErrorResponse1 = new LRSResponse { success = false };

            spyLRS.SetupSequence(lrs => lrs.SaveState(state1))
                  .Returns(Task.FromResult(stateErrorResponse1));

            await queuedLRS.SaveState(state1);
            Assert.Single(queuedLRS.StateCache);

            spyLRS.SetupSequence(lrs => lrs.DeleteState(state1))
                .Returns(Task.FromResult(stateErrorResponse));

            await queuedLRS.DeleteState(state1);
            Assert.Single(queuedLRS.StateCache);
        }

        /// <summary>
        /// Ensures that delete state works when both save and delete calls happen
        /// offline
        /// </summary>
        [Fact]
        public async Task TestDeleteNonJsonStateOffline()
        {
            spyLRS.Invocations.Clear();
            var state1 = StateResourceGenerator.GenerateNonJsonState();

            // online test
            var stateErrorResponse = new LRSResponse { success = false };
            var stateErrorResponse1 = new LRSResponse { success = false };

            spyLRS.SetupSequence(lrs => lrs.SaveState(state1))
                  .Returns(Task.FromResult(stateErrorResponse1));

            await queuedLRS.SaveState(state1);
            Assert.Single(queuedLRS.StateCache);

            spyLRS.SetupSequence(lrs => lrs.DeleteState(state1))
                .Returns(Task.FromResult(stateErrorResponse));

            await queuedLRS.DeleteState(state1);
            Assert.Single(queuedLRS.StateCache);
        }

        /// <summary>
        /// Ensures that delete state works when both save and delete calls happen
        /// offline
        /// </summary>
        [Fact]
        public async Task TestDeleteStateSaveOnLineDeleteOffline()
        {
            var state1 = StateResourceGenerator.GenerateState();

            // online test
            var stateErrorResponse = new LRSResponse { success = false };
            var stateSuccessResponse = new LRSResponse { success = true };
            spyLRS.SetupSequence(lrs => lrs.SaveState(It.IsAny<StateDocument>()))
                  .Returns(Task.FromResult(stateSuccessResponse))
                  .Returns(Task.FromResult(stateSuccessResponse));
            spyLRS.SetupSequence(lrs => lrs.DeleteState(It.IsAny<StateDocument>()))
                  .Returns(Task.FromResult(stateErrorResponse))
                  .Returns(Task.FromResult(stateSuccessResponse));

            await queuedLRS.SaveState(state1);
            Assert.Single(queuedLRS.StateCache);
            await queuedLRS.FlushStateResourceQueue();
            await queuedLRS.DeleteState(state1);
            Assert.Single(queuedLRS.StateCache);

            // ontrigger should write out
            mockTrigger.Fire();
            Assert.Single(queuedLRS.StateCache);
            spyLRS.Verify(lrs => lrs.DeleteState(state1), Times.Once());
        }

        /// <summary>
        /// Ensures that delete state works when both save and delete calls happen
        /// offline
        /// </summary>
        [Fact]
        public async Task TestDeleteStateSaveOffLineDeleteOnline()
        {
            var state1 = StateResourceGenerator.GenerateState();

            // online test
            var stateErrorResponse = new LRSResponse { success = false };
            var stateSuccessResponse = new LRSResponse { success = true };
            spyLRS.SetupSequence(lrs => lrs.SaveState(It.IsAny<StateDocument>()))
                  .Returns(Task.FromResult(stateErrorResponse));
            spyLRS.SetupSequence(lrs => lrs.DeleteState(It.IsAny<StateDocument>()))
                  .Returns(Task.FromResult(stateSuccessResponse));

            await queuedLRS.SaveState(state1);
            Assert.Single(queuedLRS.StateCache);
            await queuedLRS.DeleteState(state1);
            await queuedLRS.FlushStateResourceQueue();
            Assert.Empty(queuedLRS.StateCache);
        }

        [Fact]
        public async Task TestGetStateIds()
        {
            var stateList = StateResourceGenerator.GenerateSingleUserStateDocuments(3).ToList();

            var list1 = new List<string>
            {
                stateList[0].id,
                stateList[1].id
            };

            var stateErrorResponse = new LRSResponse { success = false };
            var stateSuccessResponse = new LRSResponse { success = true };
            spyLRS.SetupSequence(lrs => lrs.SaveState(It.IsAny<StateDocument>()))
                  .Returns(Task.FromResult(stateSuccessResponse))
                  .Returns(Task.FromResult(stateSuccessResponse))
                  .Returns(Task.FromResult(stateErrorResponse));

            // build state id request response
            var stateIdResponseSuccess = new ProfileKeysLRSResponse { success = true, content = list1 };
            var stateIdResponseError = new ProfileKeysLRSResponse { success = false, content = null };
            spyLRS.SetupSequence(lrs => lrs.RetrieveStateIds(It.IsAny<Activity>(), It.IsAny<Agent>(), It.IsAny<Guid>()))
                  .Returns(Task.FromResult(stateIdResponseSuccess))
                  .Returns(Task.FromResult(stateIdResponseSuccess))
                  .Returns(Task.FromResult(stateIdResponseError));

            // get state ids from remote before any states saved
            var returnedList = await queuedLRS.RetrieveStateIds(stateList[0].activity, stateList[0].agent, stateList[0].registration);

            Assert.Equal(list1, returnedList.content);

            // now save the values so local and remote copies exist
            await queuedLRS.SaveState(stateList[0]);
            await queuedLRS.SaveState(stateList[1]);

            returnedList = await queuedLRS.RetrieveStateIds(stateList[0].activity, stateList[0].agent, stateList[0].registration);

            Assert.Equal(list1, returnedList.content);

            // the last saved value does not go to remote so this test makes sure lists are combined
            await queuedLRS.SaveState(stateList[2]);
            var expectedList = new List<string>
            {
                stateList[0].id,
                stateList[1].id,
                stateList[2].id
            };

            returnedList = await queuedLRS.RetrieveStateIds(stateList[0].activity, stateList[0].agent, stateList[0].registration);

            Assert.Equal(expectedList, returnedList.content);
        }

        [Fact]
        public async Task TestStandardCompliance()
        {
            var stateList = StateResourceGenerator.GenerateStateDocuments(3).ToList();
            var stateErrorResponse = new LRSResponse { success = false };
            var stateSuccessResponse = new LRSResponse { success = true };
            var successStateResponse = new StateLRSResponse { success = true };

            spyLRS.SetupSequence(lrs => lrs.DeleteState(It.IsAny<StateDocument>()))
                  .Returns(Task.FromResult(stateSuccessResponse));
            spyLRS.SetupSequence(lrsResponse => lrsResponse.SaveState(It.IsAny<StateDocument>()))
                  .Returns(Task.FromResult(stateSuccessResponse));
            spyLRS.SetupSequence(lrsResponse => lrsResponse.RetrieveState(It.IsAny<string>(), It.IsAny<Activity>(), It.IsAny<Agent>(), It.IsAny<Guid>()))
                  .Returns(Task.FromResult(successStateResponse));

            // Test to ensure State Id check in place
            stateList[0].State.id = string.Empty;
            var returnValue = await queuedLRS.SaveState(stateList[0].State);
            Assert.False(returnValue.success);

            // now test activity
            stateList[1].State.activity = new Activity();
            returnValue = await queuedLRS.SaveState(stateList[1].State);
            Assert.False(returnValue.success);
            returnValue = await queuedLRS.DeleteState(stateList[1].State);
            Assert.False(returnValue.success);
            returnValue = await queuedLRS.RetrieveState(stateList[1].State.id, stateList[1].State.activity, stateList[1].State.agent, stateList[1].State.registration);
            Assert.False(returnValue.success);

            // test agent
            stateList[2].State.agent = new Agent();
            returnValue = await queuedLRS.SaveState(stateList[2].State);
            Assert.False(returnValue.success);
            returnValue = await queuedLRS.DeleteState(stateList[2].State);
            Assert.False(returnValue.success);
            returnValue = await queuedLRS.RetrieveState(stateList[2].State.id, stateList[2].State.activity, stateList[2].State.agent, stateList[2].State.registration);
            Assert.False(returnValue.success);
        }
    }
}
