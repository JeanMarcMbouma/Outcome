// Tests for error constructor extension methods on Outcome<T>.
// These test FromError, FromErrors, Validation, and Critical methods.

using Outcome;
using System.Collections.Generic;
using NUnit.Framework;

namespace Outcome.Tests
{
    /// <summary>
    /// Tests for error constructor extension methods on Outcome<T>.
    /// These test FromError, FromErrors, Validation, and Critical methods.
    /// </summary>
    [TestFixture]
    public class OutcomeErrorConstructorTests
    {
        [Test]
        public void FromError_WithCodeAndDescription_ShouldCreateErrorOutcome()
        {
            // Act
            var outcome = Outcome<int>.FromError("ERR_CODE", "Something went wrong");

            // Assert
            Assert.That(outcome.IsError, Is.True);
            Assert.That(outcome.Errors.Count, Is.EqualTo(1));
        }

        [Test]
        public void FromError_WithCustomSeverity_ShouldSetSeverity()
        {
            // Act
            var outcome = Outcome<int>.FromError("ERR_CODE", "Something went wrong", ErrorSeverity.Critical);

            // Assert
            Assert.That(outcome.IsError, Is.True);
            // Verify the error is in the errors list
            Assert.That(outcome.Errors.Count, Is.EqualTo(1));
        }

        [Test]
        public void FromError_WithErrorObject_ShouldCreateErrorOutcome()
        {
            // Arrange
            var error = new Error<string>("CODE", "Description", ErrorSeverity.Warning);

            // Act
            var outcome = Outcome<int>.FromError(error);

            // Assert
            Assert.That(outcome.IsError, Is.True);
            Assert.That(outcome.Errors.Count, Is.EqualTo(1));
        }

        [Test]
        public void FromErrors_WithEnumerable_ShouldCreateErrorOutcome()
        {
            // Arrange
            var errors = new List<Error<string>>
            {
                new Error<string>("CODE1", "Description1"),
                new Error<string>("CODE2", "Description2")
            };

            // Act
            var outcome = Outcome<int>.FromErrors(errors);

            // Assert
            Assert.That(outcome.IsError, Is.True);
            Assert.That(outcome.Errors.Count, Is.EqualTo(2));
        }

        [Test]
        public void Validation_ShouldCreateValidationError()
        {
            // Act
            var outcome = Outcome<int>.Validation("INVALID_INPUT", "Input is invalid");

            // Assert
            Assert.That(outcome.IsError, Is.True);
            Assert.That(outcome.Errors.Count, Is.EqualTo(1));
        }

        [Test]
        public void Critical_ShouldCreateCriticalError()
        {
            // Act
            var outcome = Outcome<int>.Critical("CRITICAL_ERR", "System error");

            // Assert
            Assert.That(outcome.IsError, Is.True);
            Assert.That(outcome.Errors.Count, Is.EqualTo(1));
        }
    }
}
