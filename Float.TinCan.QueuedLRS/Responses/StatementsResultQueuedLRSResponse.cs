using System;
using TinCan;
using TinCan.LRSResponses;

namespace Float.TinCan.QueuedLRS.Responses
{
    /// <summary>
    /// A statements result LRS response that is generated locally.
    /// </summary>
    public class StatementsResultQueuedLRSResponse : StatementsResultLRSResponse, IQueuedLRSResponse
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="StatementsResultQueuedLRSResponse"/> class.
        /// </summary>
        /// <param name="success">If set to <c>true</c>, the operation was a success.</param>
        /// <param name="content">The statements result associated with the response.</param>
        /// <param name="exception">An optional exception if the operation failed.</param>
        public StatementsResultQueuedLRSResponse(bool success, StatementsResult content, Exception exception = null)
        {
            this.success = success;
            this.content = content ?? throw new ArgumentNullException(nameof(content));
            this.httpException = exception;
        }
    }
}
