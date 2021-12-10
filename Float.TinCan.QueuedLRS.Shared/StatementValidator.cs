using System;
using TinCan;

namespace Float.TinCan.QueuedLRS
{
    /// <summary>
    /// A (very) rudimentary statement validator.
    /// Intended to help prevent invalid statements from being queued.
    /// </summary>
    public static class StatementValidator
    {
        /// <summary>
        /// Validates a statement against a very rudimentary set of rules.
        /// It validates that the required actor, verb, and object (target) properties are set and appear to be valid.
        /// Throws StatementValidationException if a statement is not valid.
        /// </summary>
        /// <returns><c>true</c>, if statement was validated.</returns>
        /// <param name="statement">The statement to validate.</param>
        public static bool ValidateStatement(Statement statement)
        {
            if (statement == null)
            {
                throw new ArgumentNullException(nameof(statement));
            }

            if (statement.actor == null)
            {
                throw new StatementValidationException("Statement is missing actor");
            }

            if (statement.actor.mbox == null
                && statement.actor.mbox_sha1sum == null
                && statement.actor.openid == null
                && statement.actor.account == null)
            {
                throw new StatementValidationException("An agent must have at least one inverse functional identifier");
            }

            if (statement.actor.mbox != null && !statement.actor.mbox.StartsWith("mailto:", System.StringComparison.CurrentCulture))
            {
                throw new StatementValidationException("Missing \"mailto\" scheme for the statement's actor");
            }

            if (statement.verb == null)
            {
                throw new StatementValidationException("Statement is missing a verb");
            }

            if (statement.verb.id == null)
            {
                throw new StatementValidationException("A verb must have a URI");
            }

            if (statement.target == null)
            {
                throw new StatementValidationException("Statement is missing an object (target)");
            }

            if (statement.target is Activity activity)
            {
                if (activity.id == null)
                {
                    throw new StatementValidationException("A statement object must have an id");
                }

                // Validate the id references an absolute URI
                if (Uri.IsWellFormedUriString(activity.id, UriKind.Absolute) == false)
                {
                    throw new StatementValidationException("The activity ID must be a valid absolute URI");
                }
            }

            if (statement.target is StatementRef statementRef && statementRef.id == null)
            {
                throw new StatementValidationException("A statement object must have an id");
            }

            return true;
        }
    }
}
