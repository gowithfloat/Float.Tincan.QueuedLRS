using System;
using TinCan.LRSResponses;

namespace Float.TinCan.QueuedLRS.Responses
{
    /// <summary>
    /// A profile keys LRS response that is generated locally.
    /// </summary>
    public class ProfileKeysQueuedLRSResponse : ProfileKeysLRSResponse, IQueuedLRSResponse
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ProfileKeysQueuedLRSResponse"/> class.
        /// </summary>
        /// <param name="success">If set to <c>true</c>, the operation was a success.</param>
        /// <param name="exception">An optional exception if the operation failed.</param>
        public ProfileKeysQueuedLRSResponse(bool success, Exception exception = null)
        {
            this.success = success;
            this.httpException = exception;
        }
    }
}
