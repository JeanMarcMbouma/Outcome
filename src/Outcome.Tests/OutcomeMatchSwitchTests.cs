// Tests for Match and Switch extension methods on Outcome<T>.
// These test pattern matching and side-effect execution.

using System.Collections.Generic;
using NUnit.Framework;
using BbQ.Outcome;

namespace BbQ.Outcome.Tests
{
    /// <summary>
    /// Tests for Match and Switch extension methods on Outcome<T>.
    /// These test pattern matching and side-effect execution.
    /// </summary>
    [TestFixture]
    public class OutcomeMatchSwitchTests
    {
        [Test]
        public void Match_SuccessOutcome_ShouldInvokeOnSuccess()
        {
            // Arrange
            var outcome = Outcome<int>.From(42);

            // Act
            var result = outcome.Match(
                onSuccess: x => $"Value: {x}",
                onError: errors => "Failed"
            );

            // Assert
            Assert.That(result, Is.EqualTo("Value: 42"));
        }

        [Test]
        public void Match_ErrorOutcome_ShouldInvokeOnError()
        {
            // Arrange
            var errors = new List<object> { "Error1", "Error2" };
            var outcome = Outcome<int>.FromErrors(errors);

            // Act
            var result = outcome.Match(
                onSuccess: x => "Success",
                onError: errs => $"Errors: {errs.Count}"
            );

            // Assert
            Assert.That(result, Is.EqualTo("Errors: 2"));
        }

        [Test]
        public void Switch_SuccessOutcome_ShouldInvokeOnSuccess()
        {
            // Arrange
            var outcome = Outcome<int>.From(42);
            var invoked = false;

            // Act
            outcome.Switch(
                onSuccess: x => { invoked = true; Assert.That(x, Is.EqualTo(42)); },
                onError: errors => { }
            );

            // Assert
            Assert.That(invoked, Is.True);
        }

        [Test]
        public void Switch_ErrorOutcome_ShouldInvokeOnError()
        {
            // Arrange
            var errors = new List<object> { "Error1" };
            var outcome = Outcome<int>.FromErrors(errors);
            var invoked = false;

            // Act
            outcome.Switch(
                onSuccess: x => { },
                onError: errs => { invoked = true; Assert.That(errs.Count, Is.EqualTo(1)); }
            );

            // Assert
            Assert.That(invoked, Is.True);
        }
    }
}
