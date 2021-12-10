using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Float.TinCan.QueuedLRS.Stores;
using Float.TinCan.QueuedLRS.Triggers;
using Moq;
using TinCan;
using TinCan.Documents;
using TinCan.LRSResponses;
using Xunit;

namespace Float.TinCan.QueuedLRS.Tests
{
    public class QueuedLRSTests
    {
        readonly Mock<ILRS> spyLRS = new Mock<ILRS>();
        readonly Mock<IStatementStore> spyStatementStore = new Mock<IStatementStore>();
        readonly Mock<IStateResourceStore> spyStateResourceStore = new Mock<IStateResourceStore>();
        readonly Mock<IQueueFlushTrigger> spyTrigger = new Mock<IQueueFlushTrigger>();
        readonly MockQueueFlushTrigger mockTrigger = new MockQueueFlushTrigger();
        readonly QueuedLRS queuedLRS;

        public QueuedLRSTests()
        {
            var errorResponse = new StatementsResultLRSResponse { success = false };
            var successResponse = new StatementsResultLRSResponse { success = true };
            spyLRS.SetupSequence(lrs => lrs.SaveStatements(It.IsAny<List<Statement>>()))
                  .Returns(Task.FromResult(errorResponse)) // Return error the first time
                  .Returns(Task.FromResult(successResponse)); // Return success the second time

            spyStatementStore.Setup(store => store.WriteStatements(It.IsAny<List<Statement>>()))
                  .Returns(true);

            spyLRS.Invocations.Clear();
            spyStatementStore.Invocations.Clear();
            spyTrigger.Invocations.Clear();

            var triggers = new IQueueFlushTrigger[] { spyTrigger.Object, mockTrigger };
            queuedLRS = new QueuedLRS(spyLRS.Object, spyStatementStore.Object, spyStateResourceStore.Object, triggers);
        }

        /// <summary>
        /// A new statement queue should attempt to restore statements from the store.
        /// </summary>
        [Fact]
        public void TestQueuedLRS()
        {
            Assert.Equal(0, queuedLRS.QueueSize);
            spyStatementStore.Verify(store => store.RestoreStatements(), Times.Once);
        }

        /// <summary>
        /// Tests populating queue with single statement.
        /// </summary>
        [Fact]
        public async Task TestPopulatingQueueWithSingleStatement()
        {
            var statement = StatementGenerator.GenerateStatement();

            await queuedLRS.SaveStatement(statement);

            var expectedQueue = new List<Statement> { statement };
            AssertEquivalentStatements(expectedQueue, queuedLRS.Queue);
            var queuedStatement = queuedLRS.Queue[0];

            spyStatementStore.Verify(store => store.WriteStatements(queuedLRS.Queue), Times.Once);
            spyTrigger.Verify(trigger => trigger.OnStatementQueued(queuedStatement), Times.Once);
        }

        /// <summary>
        /// Tests populating queue with multiple statements.
        /// </summary>
        [Fact]
        public async Task TestPopulatingQueueWithMultipleStatements()
        {
            var statements = StatementGenerator.GenerateStatements(2);

            await queuedLRS.SaveStatements(statements);
            AssertEquivalentStatements(statements, queuedLRS.Queue);

            spyStatementStore.Verify(store => store.WriteStatements(queuedLRS.Queue), Times.Once);
            spyTrigger.Verify(trigger => trigger.OnStatementQueued(It.IsIn<Statement>(queuedLRS.Queue)), Times.Exactly(statements.Count));
        }

        /// <summary>
        /// The statement queue should be able to accept additional statements.
        /// </summary>
        [Fact]
        public async Task TestAppendingToExistingQueue()
        {
            var statementsBatch1 = StatementGenerator.GenerateStatements(2);
            var statementsBatch2 = StatementGenerator.GenerateStatements(2);

            await queuedLRS.SaveStatements(statementsBatch1);
            Assert.Equal(2, queuedLRS.QueueSize);

            await queuedLRS.SaveStatements(statementsBatch2);

            var expectedQueue = new List<Statement>();
            expectedQueue.AddRange(statementsBatch1);
            expectedQueue.AddRange(statementsBatch2);

            AssertEquivalentStatements(expectedQueue, queuedLRS.Queue);
            spyTrigger.Verify(trigger => trigger.OnStatementQueued(It.IsIn<Statement>(queuedLRS.Queue)), Times.Exactly(expectedQueue.Count));
        }

        /// <summary>
        /// When a trigger fires, the queue should be flushed.
        /// </summary>
        [Fact]
        public async Task TestTriggerFire()
        {
            var statements = StatementGenerator.GenerateStatements(5);
            await queuedLRS.SaveStatements(statements);

            var expectedStatements = queuedLRS.Queue;
            mockTrigger.Fire();
            spyLRS.Verify(lrs => lrs.SaveStatements(expectedStatements), Times.Once);
        }

        [Fact]
        public async Task TestFlushEmptyQueue()
        {
            await queuedLRS.FlushStatementQueueWithResponse();
            spyLRS.Verify(lrs => lrs.SaveStatements(It.IsAny<List<Statement>>()), Times.Never);
        }

        [Fact]
        public async Task TestFlushQueueOfOne()
        {
            // set up fake success responses from the LRS
            spyLRS.Setup(lrs => lrs.SaveStatements(It.IsAny<List<Statement>>()))
                  .Returns(Task.FromResult(new StatementsResultLRSResponse { success = true }));

            await queuedLRS.SaveStatements(StatementGenerator.GenerateStatements(1));
            await queuedLRS.FlushStatementQueueWithResponse();
            spyLRS.Verify(lrs => lrs.SaveStatements(It.IsAny<List<Statement>>()), Times.Once);
            Assert.Equal(0, queuedLRS.QueueSize);
        }

        [Fact]
        public async Task TestFlushAlmostFullQueue()
        {
            // set up fake success responses from the LRS
            spyLRS.Setup(lrs => lrs.SaveStatements(It.IsAny<List<Statement>>()))
                  .Returns(Task.FromResult(new StatementsResultLRSResponse { success = true }));

            await queuedLRS.SaveStatements(StatementGenerator.GenerateStatements(queuedLRS.BatchSize - 1));
            await queuedLRS.FlushStatementQueueWithResponse();
            spyLRS.Verify(lrs => lrs.SaveStatements(It.IsAny<List<Statement>>()), Times.Once);
            Assert.Equal(0, queuedLRS.QueueSize);
        }

        [Fact]
        public async Task TestFlushFullQueue()
        {
            // set up fake success responses from the LRS
            spyLRS.Setup(lrs => lrs.SaveStatements(It.IsAny<List<Statement>>()))
                  .Returns(Task.FromResult(new StatementsResultLRSResponse { success = true }));

            await queuedLRS.SaveStatements(StatementGenerator.GenerateStatements(queuedLRS.BatchSize));
            await queuedLRS.FlushStatementQueueWithResponse();
            spyLRS.Verify(lrs => lrs.SaveStatements(It.IsAny<List<Statement>>()), Times.Once);
            Assert.Equal(0, queuedLRS.QueueSize);
        }

        [Fact]
        public async Task TestFlushOverfullQueue()
        {
            // set up fake success responses from the LRS
            spyLRS.Setup(lrs => lrs.SaveStatements(It.IsAny<List<Statement>>()))
                  .Returns(Task.FromResult(new StatementsResultLRSResponse { success = true }));

            await queuedLRS.SaveStatements(StatementGenerator.GenerateStatements(queuedLRS.BatchSize + 1));
            await queuedLRS.FlushStatementQueueWithResponse();
            spyLRS.Verify(lrs => lrs.SaveStatements(It.IsAny<List<Statement>>()), Times.Once);
            Assert.Equal(1, queuedLRS.QueueSize);
        }

        [Fact]
        public async Task TestFullFlushEmptyQueue()
        {
            await queuedLRS.FlushFullStatementQueue();
            spyLRS.Verify(lrs => lrs.SaveStatements(It.IsAny<List<Statement>>()), Times.Never);
        }

        [Fact]
        public async Task TestFullFlushSingleBatch()
        {
            // set up fake success responses from the LRS
            spyLRS.Setup(lrs => lrs.SaveStatements(It.IsAny<List<Statement>>()))
                  .Returns(Task.FromResult(new StatementsResultLRSResponse { success = true }));

            await queuedLRS.SaveStatements(StatementGenerator.GenerateStatements(queuedLRS.BatchSize));
            await queuedLRS.FlushFullStatementQueue();
            spyLRS.Verify(lrs => lrs.SaveStatements(It.IsAny<List<Statement>>()), Times.Once);
            Assert.Equal(0, queuedLRS.QueueSize);
        }

        [Fact]
        public async Task TestFullFlushMultipleBatches()
        {
            // set up fake success responses from the LRS
            spyLRS.Setup(lrs => lrs.SaveStatements(It.IsAny<List<Statement>>()))
                  .Returns(Task.FromResult(new StatementsResultLRSResponse { success = true }));

            await queuedLRS.SaveStatements(StatementGenerator.GenerateStatements(queuedLRS.BatchSize + 1));
            await queuedLRS.FlushFullStatementQueue();
            spyLRS.Verify(lrs => lrs.SaveStatements(It.IsAny<List<Statement>>()), Times.Exactly(2));
            Assert.Equal(0, queuedLRS.QueueSize);
        }

        [Fact]
        public async Task TestFlushStateResourceQueueWithOneFailure()
        {
            spyLRS.SetupSequence(lrs => lrs.SaveState(It.IsAny<StateDocument>()))

                  // failing during savestate calls
                  .Returns(Task.FromResult(new LRSResponse { success = false }))
                  .Returns(Task.FromResult(new LRSResponse { success = false }))
                  .Returns(Task.FromResult(new LRSResponse { success = false }))
                  .Returns(Task.FromResult(new LRSResponse { success = false }))
                  .Returns(Task.FromResult(new LRSResponse { success = false }))

                  // success during flushing
                  .Returns(Task.FromResult(new LRSResponse { success = true }))
                  .Returns(Task.FromResult(new LRSResponse { success = true }))
                  .Returns(Task.FromResult(new LRSResponse { success = true }))
                  .Returns(Task.FromResult(new LRSResponse { success = true }))

                  // but one exception right at the end
                  .Returns(CreateException<LRSResponse>());

            var states = StateResourceGenerator.GenerateSingleUserStateDocuments(5);

            Assert.Equal(5, states.Count());
            Assert.Empty(queuedLRS.StateCache);
            Assert.Empty(queuedLRS.DirtyStateCache);

            var saveResponses = await queuedLRS.SaveStates(states);

            Assert.Equal(5, saveResponses.Count());
            Assert.Equal(5, queuedLRS.StateCache.Count);
            Assert.Equal(5, queuedLRS.DirtyStateCache.Count);

            var responses = await queuedLRS.FlushStateResourceQueue();

            Assert.Equal(5, responses.Count());
            Assert.Single(responses.Where(resp => resp.success == false));
            Assert.Equal(5, queuedLRS.StateCache.Count);
            Assert.Single(queuedLRS.DirtyStateCache);
        }

        /// <summary>
        /// Flushing the queue should send a single batch of statements to the LRS.
        /// If an error occurs, those statements should be kept in the queue.
        /// If it was successful, those statements should be removed from the queue.
        /// </summary>
        [Fact]
        public async Task TestFlushQueue()
        {
            var statements = StatementGenerator.GenerateStatements(queuedLRS.BatchSize + 1);

            // Intially, flushing the statement queue should do nothing
            await queuedLRS.FlushStatementQueueWithResponse();
            spyLRS.Verify(lrs => lrs.SaveStatements(It.IsAny<List<Statement>>()), Times.Never);
            spyLRS.Invocations.Clear();

            // Until some statements are added
            await queuedLRS.SaveStatements(statements);
            spyStatementStore.Invocations.Clear();

            // Verify that if the LRS returns an error response, the queue is unchanged
            await queuedLRS.FlushStatementQueueWithResponse();
            spyLRS.Verify(lrs => lrs.SaveStatements(It.IsAny<List<Statement>>()), Times.Once);
            spyLRS.Invocations.Clear();
            AssertEquivalentStatements(statements, queuedLRS.Queue);

            // But if the LRS returns a success response, those statements are dropped from the queue
            var expectedStatements = queuedLRS.Queue.GetRange(0, queuedLRS.BatchSize);
            await queuedLRS.FlushStatementQueueWithResponse();
            Assert.Equal(1, queuedLRS.QueueSize);

            spyLRS.Verify(lrs => lrs.SaveStatements(expectedStatements), Times.Once);
            spyLRS.Invocations.Clear();

            spyStatementStore.Verify(store => store.WriteStatements(It.IsAny<List<Statement>>()), Times.Exactly(2));
            spyStatementStore.Invocations.Clear();
        }

        /// <summary>
        /// LRS queue should be emptied if an unrecoverable error occurs.
        /// </summary>
        [Fact]
        public async Task TestUnrecoverableErrorDetection()
        {
            // Set up a fake error response fro the LRS
            var errorResponse = new StatementsResultLRSResponse { success = false };
            errorResponse.SetErrMsgFromBytes(null, (int)System.Net.HttpStatusCode.BadRequest);

            spyLRS.SetupSequence(lrs => lrs.SaveStatements(It.IsAny<List<Statement>>()))
                  .Returns(Task.FromResult(errorResponse));

            // Attempt to queue statements
            var statements = StatementGenerator.GenerateStatements(2);
            await queuedLRS.SaveStatements(statements);

            Assert.Equal(statements.Count, queuedLRS.QueueSize);

            await queuedLRS.FlushStatementQueueWithResponse();

            Assert.Equal(0, queuedLRS.QueueSize);
        }

        /// <summary>
        /// LRS queue should support voiding statements.
        /// </summary>
        [Fact]
        public void TestVoidingStatement()
        {
            var statement = StatementGenerator.GenerateStatement();
            var guid = Guid.NewGuid();
            var voidedVerb = "http://adlnet.gov/expapi/verbs/voided";

            queuedLRS.VoidStatement(guid, statement.actor);
            Assert.Equal(1, queuedLRS.QueueSize);

            var voidedStatement = queuedLRS.Queue[0];
            Assert.Equal(voidedVerb, voidedStatement.verb.id.ToString());
            Assert.Equal(guid, (voidedStatement.target as StatementRef).id);
        }

        /// <summary>
        /// Statements that are queued should have their ID and timestamp set.
        /// </summary>
        [Fact]
        public async Task TestStatementPreparation()
        {
            await queuedLRS.SaveStatement(StatementGenerator.GenerateStatement());
            await queuedLRS.SaveStatements(StatementGenerator.GenerateStatements(2));

            queuedLRS.Queue.ForEach((statement) =>
            {
                Assert.NotNull(statement.id);
                Assert.NotNull(statement.timestamp);
            });
        }

        /// <summary>
        /// The statement queue should be flushed prior to querying statements from the LRS.
        /// </summary>
        [Fact]
        public async Task TestFlushOnQueryStatements()
        {
            await queuedLRS.SaveStatement(StatementGenerator.GenerateStatement());
            var queue = queuedLRS.Queue;
            await queuedLRS.QueryStatements(new StatementsQuery());

            spyLRS.Verify(lrs => lrs.SaveStatements(queue), Times.Once);
        }

        /// <summary>
        /// The QueuedLRS should safely handle saving a statement during a flush (or defer either).
        /// </summary>
        [Fact]
        public async Task TestSaveStatementsDuringFlush()
        {
            // set up fake success responses from the LRS
            spyLRS.Setup(lrs => lrs.SaveStatements(It.IsAny<List<Statement>>()))
                  .Returns(Task.FromResult(new StatementsResultLRSResponse { success = true }));

            var statements1 = StatementGenerator.GenerateStatements(100);
            var statements2 = StatementGenerator.GenerateStatements(100);

            var task1 = FlushStatements();
            var task2 = SaveStatementsSlow(statements1);
            var task3 = FlushStatements();
            var task4 = SaveStatementsFast(statements2);

            await Task.WhenAll(task1, task2, task3, task4);
        }

        /// <summary>
        /// The QueuedLRS should safely handle saving a statement during a flush (or defer either).
        /// </summary>
        [Fact]
        public async Task TestSaveStateDuringFlush()
        {
            // set up fake success responses from the LRS
            spyLRS.Setup(lrs => lrs.SaveState(It.IsAny<StateDocument>()))
                  .Returns(Task.FromResult(new LRSResponse { success = true }));

            var state1 = StateResourceGenerator.GenerateSingleUserStateDocuments(100);
            var state2 = StateResourceGenerator.GenerateSingleUserStateDocuments(100);

            var task1 = FlushStatements();
            var task2 = SaveStateSlow(state1);
            var task3 = FlushState();
            var task4 = SaveStateFast(state2);

            await Task.WhenAll(task1, task2, task3, task4);
        }

        /// <summary>
        /// Just mess me up fam
        /// </summary>
        [Fact]
        public async Task TestSaveStateAndStatementsAndFlushBoth()
        {
            // set up fake success responses from the LRS
            spyLRS.Setup(lrs => lrs.SaveStatements(It.IsAny<List<Statement>>()))
                  .Returns(Task.FromResult(new StatementsResultLRSResponse { success = true }));

            spyLRS.Setup(lrs => lrs.SaveState(It.IsAny<StateDocument>()))
                  .Returns(Task.FromResult(new LRSResponse { success = true }));

            var state1 = StateResourceGenerator.GenerateSingleUserStateDocuments(1000);
            var state2 = StateResourceGenerator.GenerateSingleUserStateDocuments(1000);
            var statements1 = StatementGenerator.GenerateStatements(1000);
            var statements2 = StatementGenerator.GenerateStatements(1000);

            var task1 = FlushStatements();
            var task2 = SaveStateSlow(state1);
            var task3 = FlushState();
            var task4 = SaveStateFast(state2);
            var task5 = FlushStatements();
            var task6 = SaveStatementsSlow(statements1);
            var task7 = FlushStatements();
            var task8 = SaveStatementsFast(statements2);

            await Task.WhenAll(task1, task2, task3, task4, task5, task6, task7, task8);
        }

        /// <summary>
        /// When flushing statements, we should be able to read the response from the LRS.
        /// </summary>
        [Fact]
        public async Task TestFlushStatementsAndReadResult()
        {
            spyLRS.Setup(lrs => lrs.SaveStatements(It.IsAny<List<Statement>>()))
                  .Returns(Task.FromResult(new StatementsResultLRSResponse { success = true }));

            var statements = StatementGenerator.GenerateStatements(10);
            await queuedLRS.SaveStatements(statements);
            var result = await queuedLRS.FlushStatementQueueWithResponse();

            Assert.Equal(result.PersistedStatements.Count(), statements.Count);
            Assert.True(result.Response.success);
            Assert.Null(result.Response.httpException);
        }

        /// <summary>
        /// When flushing statements, if the LRS returns with a `false` success, nothing should be removed from the queue.
        /// </summary>
        [Fact]
        public async Task TestFlushStatementsWithFailure()
        {
            spyLRS.Setup(lrs => lrs.SaveStatements(It.IsAny<List<Statement>>()))
                  .Returns(Task.FromResult(new StatementsResultLRSResponse { success = false }));

            var statements = StatementGenerator.GenerateStatements(10);
            await queuedLRS.SaveStatements(statements);
            var result = await queuedLRS.FlushStatementQueueWithResponse();

            Assert.Empty(result.PersistedStatements);
            Assert.False(result.Response.success);
            Assert.Equal(statements.Count(), queuedLRS.QueueSize);
            Assert.Null(result.Response.httpException);
        }

        /// <summary>
        /// When flushing statements, if the LRS throws an exception, nothing should be removed from the queue, and execution should continue.
        /// </summary>
        [Fact]
        public async Task TestFlushStatementsWithException()
        {
            spyLRS.Setup(lrs => lrs.SaveStatements(It.IsAny<List<Statement>>()))
                  .Returns(CreateException<StatementsResultLRSResponse>());

            var statements = StatementGenerator.GenerateStatements(10);
            await queuedLRS.SaveStatements(statements);
            var result = await queuedLRS.FlushStatementQueueWithResponse();

            Assert.Empty(result.PersistedStatements);
            Assert.False(result.Response.success);
            Assert.Equal(statements.Count(), queuedLRS.QueueSize);
            Assert.NotNull(result.Response.httpException);
        }

        /// <summary>
        /// Saving a null or invalid statement should cause an exception.
        /// </summary>
        [Fact]
        public async Task TestSaveInvalidStatement()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(async () => await queuedLRS.SaveStatement(null));
            await Assert.ThrowsAsync<StatementValidationException>(async () => await queuedLRS.SaveStatement(new Statement()));
        }

        [Fact]
        public async Task TestWriteLockedFile()
        {
            var statementPath = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());
            var remoteLRS = new Mock<ILRS>();

            remoteLRS.Setup(arg => arg.SaveStatement(It.IsAny<Statement>()))
                     .Returns(Task.FromResult(new StatementLRSResponse { success = false }));

            remoteLRS.Setup(arg => arg.SaveStatements(It.IsAny<List<Statement>>()))
                  .Returns(Task.FromResult(new StatementsResultLRSResponse { success = false }));

            var statementStore = new JSONStatementStore(statementPath);
            var stateStore = new Mock<IStateResourceStore>();
            var lrs = new QueuedLRS(remoteLRS.Object, statementStore, stateStore.Object);

            await lrs.SaveStatement(StatementGenerator.GenerateStatement());
            Assert.Equal(1, lrs.QueueSize);

            // prevent other threads from accessing the file
            using (var unused = File.Open(statementPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                // then, try to access
                await lrs.SaveStatement(StatementGenerator.GenerateStatement());
                Assert.Equal(2, lrs.QueueSize);
            }
        }

        [Fact]
        public async Task TestWriteReadonlyFile()
        {
            var statementPath = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());
            var remoteLRS = new Mock<ILRS>();

            remoteLRS.Setup(arg => arg.SaveStatement(It.IsAny<Statement>()))
                     .Returns(Task.FromResult(new StatementLRSResponse { success = false }));

            remoteLRS.Setup(arg => arg.SaveStatements(It.IsAny<List<Statement>>()))
                  .Returns(Task.FromResult(new StatementsResultLRSResponse { success = false }));

            var statementStore = new JSONStatementStore(statementPath);
            var stateStore = new Mock<IStateResourceStore>();
            var lrs = new QueuedLRS(remoteLRS.Object, statementStore, stateStore.Object);

            await lrs.SaveStatement(StatementGenerator.GenerateStatement());
            Assert.Equal(1, lrs.QueueSize);

            // make file readonly
            File.SetAttributes(statementPath, FileAttributes.ReadOnly);

            // then, try to access
            await lrs.SaveStatement(StatementGenerator.GenerateStatement());
            Assert.Equal(2, lrs.QueueSize);
        }

        [Fact]
        public async Task TestLocalCachePolicy()
        {
            var remoteAccessCounter = 0;
            var responseContent = new StateDocument { id = "test1", activity = GetActivity("id"), agent = GetAgent("test") };
            var responseContent2 = new StateDocument { id = "test2", activity = GetActivity("id"), agent = GetAgent("test") };
            var remoteLRS = new Mock<ILRS>();
            remoteLRS.Setup(arg => arg.RetrieveState("test1", It.IsAny<Activity>(), It.IsAny<Agent>(), It.IsAny<Guid?>()))
                .Callback(() => { remoteAccessCounter += 1; })
                .Returns(Task.FromResult(new StateLRSResponse { success = true, content = responseContent }));
            remoteLRS.Setup(arg => arg.RetrieveState("test2", It.IsAny<Activity>(), It.IsAny<Agent>(), It.IsAny<Guid?>()))
                .Callback(() => { remoteAccessCounter += 1; })
                .Returns(Task.FromResult(new StateLRSResponse { success = true, content = responseContent2 }));

            remoteLRS.Setup(arg => arg.SaveState(It.IsAny<StateDocument>()))
                .Returns(Task.FromResult(new LRSResponse { success = true }));

            var statementPath = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());
            var statementStore = new JSONStatementStore(statementPath);
            var stateStore = new Mock<IStateResourceStore>();
            var lrs = new QueuedLRS(remoteLRS.Object, statementStore, stateStore.Object);

            lrs.CachePolicy = StateCachePolicy.PreferLocal;

            // if we have the document locally, don't bother the remote server if the policy is local
            await lrs.SaveState(responseContent);
            await lrs.RetrieveState("test1", GetActivity("id"), GetAgent("test"));
            Assert.Equal(0, remoteAccessCounter);

            // if we don't have the document locally, we have to reach out for it
            await lrs.RetrieveState("test2", GetActivity("id"), GetAgent("test"));
            Assert.Equal(1, remoteAccessCounter);

            lrs.CachePolicy = StateCachePolicy.PreferRemote;

            // if the policy is immediate, always contact the server, even if we already have it locally
            await lrs.RetrieveState("test2", GetActivity("id"), GetAgent("test"));
            Assert.Equal(2, remoteAccessCounter);

            lrs.CachePolicy = StateCachePolicy.KeepLocalUpdated;

            // if the policy is eventual, we will contact the server, but not before we return
            await lrs.RetrieveState("test2", GetActivity("id"), GetAgent("test"));
            Assert.Equal(2, remoteAccessCounter);

            await Task.Delay(500);
            Assert.Equal(3, remoteAccessCounter);
        }

        [Fact]
        public async Task TestLocalCachePersistence()
        {
            var remoteLRS = new Mock<ILRS>();
            remoteLRS.Setup(arg => arg.RetrieveState(It.IsAny<string>(), It.IsAny<Activity>(), It.IsAny<Agent>(), It.IsAny<Guid?>()))
                .Returns(Task.FromResult(new StateLRSResponse { success = true }));
            remoteLRS.Setup(arg => arg.SaveState(It.IsAny<StateDocument>()))
                .Returns(Task.FromResult(new LRSResponse { success = true }));

            var statePath = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());
            var statementStore = new Mock<IStatementStore>();
            var stateStore = new JSONStateResourceStore(statePath);
            var lrs1 = new QueuedLRS(remoteLRS.Object, statementStore.Object, stateStore);

            await lrs1.SaveState(new StateDocument { id = "test1", activity = GetActivity("id"), agent = GetAgent("name"), content = Encode("arbitrary content") });

            var lrs2 = new QueuedLRS(remoteLRS.Object, statementStore.Object, stateStore);
            lrs2.CachePolicy = StateCachePolicy.PreferLocal;

            var stat2 = await lrs2.RetrieveState("test1", GetActivity("id"), GetAgent("name"));
            Assert.NotNull(stat2);
            Assert.Equal("arbitrary content", Decode(stat2.content?.content));

            var lrs3 = new QueuedLRS(remoteLRS.Object, statementStore.Object, stateStore);
            lrs3.CachePolicy = StateCachePolicy.KeepLocalUpdated;

            var stat3 = await lrs2.RetrieveState("test1", GetActivity("id"), GetAgent("name"));
            Assert.NotNull(stat3);
            Assert.Equal("arbitrary content", Decode(stat3.content?.content));

            var lrs4 = new QueuedLRS(remoteLRS.Object, statementStore.Object, stateStore);
            lrs4.CachePolicy = StateCachePolicy.PreferRemote;

            var stat4 = await lrs2.RetrieveState("test1", GetActivity("id"), GetAgent("name"));
            Assert.NotNull(stat4);
            Assert.Equal("arbitrary content", Decode(stat4.content?.content));
        }

        byte[] Encode(string str)
        {
            if (string.IsNullOrEmpty(str))
            {
                return new byte[0];
            }

            return System.Text.Encoding.UTF8.GetBytes(str);
        }

        string Decode(byte[] byt)
        {
            if (byt == null)
            {
                return null;
            }

            return System.Text.Encoding.UTF8.GetString(byt);
        }

        Activity GetActivity(string id)
        {
            return new Activity
            {
                id = $"http://example.com/{id}"
            };
        }

        Agent GetAgent(string name)
        {
            return new Agent
            {
                name = name,
                mbox = $"{name}@example.com"
            };
        }

        async Task FlushStatements()
        {
            await Task.Delay(new Random().Next(10, 500));
            await queuedLRS.FlushStatementQueueWithResponse();
        }

        async Task SaveStatementsSlow(IEnumerable<Statement> statements)
        {
            foreach (var statement in statements)
            {
                await Task.Delay(new Random().Next(5, 10));
                await queuedLRS.SaveStatement(statement);
            }
        }

        async Task SaveStatementsFast(IEnumerable<Statement> statements)
        {
            await Task.Delay(new Random().Next(10, 100));
            await queuedLRS.SaveStatements(statements.ToList());
        }

        async Task FlushState()
        {
            await Task.Delay(new Random().Next(10, 500));
            await queuedLRS.FlushStateResourceQueue();
        }

        async Task SaveStateSlow(IEnumerable<StateDocument> states)
        {
            foreach (var state in states)
            {
                await Task.Delay(new Random().Next(5, 10));
                await queuedLRS.SaveState(state);
            }
        }

        async Task SaveStateFast(IEnumerable<StateDocument> states)
        {
            await Task.Delay(new Random().Next(10, 100));
            await queuedLRS.SaveStates(states);
        }

        async Task<T> CreateException<T>()
        {
            await Task.Delay(new Random().Next(5, 10));
            throw new Exception();
        }

        /// <summary>
        /// Asserts that two collections contain equivalent statements.
        /// This specificlly checks that the actor, verb, and object have matching ids.
        /// </summary>
        /// <param name="expectedStatements">Expected statements.</param>
        /// <param name="actualStatements">Actual statements.</param>
        void AssertEquivalentStatements(IEnumerable<Statement> expectedStatements, IEnumerable<Statement> actualStatements)
        {
            Assert.True(expectedStatements.Count() == actualStatements.Count(), "Expected and actual should have same number of statements");

            for (var i = 0; i < expectedStatements.Count(); i++)
            {
                var expected = expectedStatements.ElementAt(i);
                var actual = actualStatements.ElementAt(i);

                // Consider statements equivalent if they have the same actor mbox,
                // the same verb, and the same activity id
                Assert.True(expected.actor.mbox == actual.actor.mbox, "Statements at index [" + i + "] have different actors");
                Assert.True(expected.verb.id == actual.verb.id, "Statements at index [" + i + "] have different verbs");

                var expectedTarget = expected.target as Activity;
                var actualTarget = actual.target as Activity;
                Assert.True(expectedTarget.id == actualTarget.id, "Statements at index [" + i + "] have different activities");
            }
        }
    }
}
