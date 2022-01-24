using System;
using System.Collections.Generic;
using System.Linq;
using TinCan;

namespace Float.TinCan.QueuedLRS.Tests
{
    /// <summary>
    /// Generates fake statements for testing the QueuedLRS.
    /// </summary>
    public static class StatementGenerator
    {
        /// <summary>
        /// Generates multiple statements.
        /// </summary>
        /// <returns>The generated statements.</returns>
        /// <param name="count">The number of statements to generate.</param>
        public static List<Statement> GenerateStatements(int count)
        {
            return Enumerable.Repeat(GenerateStatement(), count).ToList();
        }

        /// <summary>
        /// Generates a single "attempted" statement.
        /// </summary>
        /// <returns>The statement.</returns>
        public static Statement GenerateStatement()
        {
            return PrepareStatement("http://adlnet.gov/expapi/verbs/attempted");
        }

        /// <summary>
        /// Generates a single "completed" statement.
        /// </summary>
        /// <returns>The completed statement.</returns>
        public static Statement GenerateCompletedStatement()
        {
            return PrepareStatement("http://adlnet.gov/expapi/verbs/completed");
        }

        static Statement PrepareStatement(string verbId)
        {
            return new Statement
            {
                actor = new Agent
                {
                    mbox = "mailto:jdoe@example.com"
                },
                verb = new Verb(new Uri(verbId)),
                target = new Activity
                {
                    id = new System.Uri("http://example.com/activities/example-activity")
                }
            };
        }
    }
}
