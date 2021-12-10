using System;
using TinCan;
using TinCan.LRSResponses;

namespace Float.TinCan.QueuedLRS.Responses
{
    /// <summary>
    /// A statement LRS response that is generated locally.
    /// </summary>
    public class StatementQueuedLRSResponse : StatementLRSResponse, IQueuedLRSResponse
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="StatementQueuedLRSResponse"/> class.
        /// </summary>
        /// <param name="success">If set to <c>true</c>, the operation was a success.</param>
        /// <param name="content">The statement associated with the response.</param>
        public StatementQueuedLRSResponse(bool success, Statement content)
        {
            this.success = success;
            this.content = content ?? throw new ArgumentNullException(nameof(content));
        }
    }
}
