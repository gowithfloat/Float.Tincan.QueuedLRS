using System;
using Float.TinCan.QueuedLRS.Triggers;
using Xunit;

namespace Float.TinCan.QueuedLRS.Tests
{
    public class CompletedStatementTriggerTests
    {
        /// <summary>
        /// Attempting to trigger on a valid statement should trigger the event.
        /// </summary>
        [Fact]
        public void TestCompletedStatementTrigger()
        {
            var eventRaised = false;
            var trigger = new CompletedStatementTrigger();
            trigger.TriggerFired += (sender, e) => eventRaised = true;

            var statement = StatementGenerator.GenerateCompletedStatement();
            trigger.OnStatementQueued(statement);

            Assert.True(eventRaised, "TriggerFired event was not raised");
        }

        /// <summary>
        /// Attempting to trigger on a null statement should cause an exception.
        /// </summary>
        [Fact]
        public void TestInvalidStatementTrigger()
        {
            var trigger = new CompletedStatementTrigger();
            Assert.Throws<ArgumentNullException>(() => trigger.OnStatementQueued(null));
        }
    }
}
