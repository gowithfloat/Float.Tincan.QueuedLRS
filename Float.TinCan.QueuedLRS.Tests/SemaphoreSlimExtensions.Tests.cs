using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Float.TinCan.QueuedLRS.Tests
{
    public class SemaphoreSlimExtensionsTests
    {
        [Fact]
        public async Task UsingTest()
        {
            var semaphoreSlim = new SemaphoreSlim(1, 1);
            Assert.Equal(1, semaphoreSlim.CurrentCount);

            using (await semaphoreSlim.UseWaitAsync())
            {
                Assert.Equal(0, semaphoreSlim.CurrentCount);
            }

            Assert.Equal(1, semaphoreSlim.CurrentCount);
        }
    }
}
