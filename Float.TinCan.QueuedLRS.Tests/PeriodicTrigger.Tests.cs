using System;
using System.Threading.Tasks;
using Float.TinCan.QueuedLRS.Triggers;
using Xunit;

namespace Float.TinCan.QueuedLRS.Tests
{
    public class PeriodicTriggerTests
    {
        /// <summary>
        /// Initializing a periodic trigger requires a positive non-negative integer.
        /// </summary>
        [Fact]
        public void TestInit()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PeriodicTrigger(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PeriodicTrigger(-1));
            var trigger = new PeriodicTrigger(1);
        }

        /// <summary>
        /// A periodic trigger should trigger after its expected duration.
        /// This test is EXTREMELY FLAKY.
        /// </summary>
        [Fact]
        public async Task TestPeriodicTrigger()
        {
            var eventRaised = false;
            var trigger = new PeriodicTrigger(1);
            trigger.TriggerFired += (sender, e) => eventRaised = true;

            Assert.False(eventRaised, "TriggerFired should not have been raised yet");
            await Task.Delay(1001);
            Assert.True(eventRaised, "TriggerFired event was not raised");
        }
    }
}
