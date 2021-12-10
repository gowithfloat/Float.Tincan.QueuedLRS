using System;
using System.Threading;
using System.Threading.Tasks;

namespace Float.TinCan.QueuedLRS
{
    /// <summary>
    /// Extensions on the <see cref="SemaphoreSlim"/> class.
    /// </summary>
    public static class SemaphoreSlimExtensions
    {
        /// <summary>
        /// Create a disposable task for awaiting this semaphore asynchronously.
        /// e.g. `using (await mySemaphore.UseWaitAsync()) { ... }`
        /// The semaphore will be released when code exits the using block.
        /// </summary>
        /// <returns>A task containing a disposable wrapper around a semaphore.</returns>
        /// <param name="semaphoreSlim">This semaphore.</param>
        public static async Task<IDisposable> UseWaitAsync(this SemaphoreSlim semaphoreSlim)
        {
            if (semaphoreSlim == null)
            {
                throw new ArgumentNullException(nameof(semaphoreSlim));
            }

            await semaphoreSlim.WaitAsync().ConfigureAwait(false);
            return new DisposableSemaphoreSlim(semaphoreSlim);
        }

        /// <summary>
        /// Create a disposable object for awaiting this semaphore synchronously.
        /// e.g. `using (mySemaphore.UseWait()) { ... }`
        /// The semaphore will be released when code exits the using block.
        /// </summary>
        /// <returns>A disposable wrapper around a semaphore.</returns>
        /// <param name="semaphoreSlim">This semaphore.</param>
        public static IDisposable UseWait(this SemaphoreSlim semaphoreSlim)
        {
            if (semaphoreSlim == null)
            {
                throw new ArgumentNullException(nameof(semaphoreSlim));
            }

            semaphoreSlim.Wait();
            return new DisposableSemaphoreSlim(semaphoreSlim);
        }

        internal class DisposableSemaphoreSlim : IDisposable
        {
            readonly SemaphoreSlim semaphoreSlim;
            bool isDisposed;

            internal DisposableSemaphoreSlim(SemaphoreSlim semaphoreSlim)
            {
                this.semaphoreSlim = semaphoreSlim;
            }

            public void Dispose()
            {
                if (isDisposed)
                {
                    return;
                }

                semaphoreSlim.Release();
                isDisposed = true;
            }
        }
    }
}
