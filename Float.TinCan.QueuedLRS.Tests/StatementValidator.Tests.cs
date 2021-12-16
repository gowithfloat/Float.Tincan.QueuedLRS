using System;
using TinCan;
using Xunit;

namespace Float.TinCan.QueuedLRS.Tests
{
    public class StatementValidatorTests
    {
        readonly Statement statement = StatementGenerator.GenerateStatement();

        [Fact]
        public void TestNullStatement()
        {
            Assert.Throws<ArgumentNullException>(() => StatementValidator.ValidateStatement(null));
        }

        [Fact]
        public void TestMissingActor()
        {
            statement.actor = null;

            RunValidation();
        }

        [Fact]
        public void TestInvalidActor()
        {
            statement.actor.mbox = null;
            RunValidation();
        }

        [Fact]
        public void TestInvalidActorMbox()
        {
            statement.actor.mbox = "invalid mbox";
            RunValidation();
        }

        [Fact]
        public void TestMissingVerb()
        {
            statement.verb = null;
            RunValidation();
        }

        [Fact]
        public void TestInvalidVerb()
        {
            statement.verb.id = null;
            RunValidation();
        }

        [Fact]
        public void TestMissingTarget()
        {
            statement.target = null;
            RunValidation();
        }

        [Fact]
        public void TestMissingTargetId()
        {
            statement.target = new Activity();
            RunValidation();
        }

        void RunValidation()
        {
            Assert.Throws<StatementValidationException>(() => StatementValidator.ValidateStatement(statement));
        }
    }
}
