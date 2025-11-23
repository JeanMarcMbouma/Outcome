// Tests for Map and Bind extension methods on Outcome<T>.
// These test functor and monadic operations.

using System.Collections.Generic;
using NUnit.Framework;
using BbQ.Outcome;

namespace BbQ.Outcome.Tests
{
    /// <summary>
    /// Tests for Map and Bind extension methods on Outcome<T>.
    /// These test functor and monadic operations.
    /// </summary>
    [TestFixture]
    public class OutcomeMapBindTests
    {
        [Test]
        public void Map_SuccessOutcome_ShouldTransformValue()
        {
            // Arrange
            var outcome = Outcome<int>.From(5);

            // Act
            var result = outcome.Map(x => x * 2);

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(10));
        }

        [Test]
        public void Map_ErrorOutcome_ShouldPropagateError()
        {
            // Arrange
            var errors = new List<object> { "Error1" };
            var outcome = Outcome<int>.FromErrors(errors);

            // Act
            var result = outcome.Map(x => x * 2);

            // Assert
            Assert.That(result.IsError, Is.True);
            Assert.That(result.Errors, Is.EqualTo(errors));
        }

        [Test]
        public void Bind_SuccessOutcome_ShouldChainOutcomes()
        {
            // Arrange
            var outcome = Outcome<int>.From(5);

            // Act
            var result = outcome.Bind(x => Outcome<int>.From(x * 2));

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(10));
        }

        [Test]
        public void Bind_SuccessOutcomeToError_ShouldReturnError()
        {
            // Arrange
            var outcome = Outcome<int>.From(5);

            // Act
            var result = outcome.Bind(x => 
                x > 10 
                    ? Outcome<int>.From(x) 
                    : Outcome<int>.FromErrors(new List<object> { "Too small" })
            );

            // Assert
            Assert.That(result.IsError, Is.True);
            Assert.That(result.Errors.Count, Is.EqualTo(1));
        }

        [Test]
        public void Bind_ErrorOutcome_ShouldPropagateError()
        {
            // Arrange
            var errors = new List<object> { "Error1" };
            var outcome = Outcome<int>.FromErrors(errors);

            // Act
            var result = outcome.Bind(x => Outcome<int>.From(x * 2));

            // Assert
            Assert.That(result.IsError, Is.True);
            Assert.That(result.Errors, Is.EqualTo(errors));
        }

        [Test]
        public void Map_ChainedOperations_ShouldComposeCorrectly()
        {
            // Arrange
            var outcome = Outcome<int>.From(2);

            // Act
            var result = outcome
                .Map(x => x * 3)
                .Map(x => x + 4);

            // Assert
            Assert.That(result.Value, Is.EqualTo(10)); // (2 * 3) + 4 = 10
        }

        [Test]
        public void Bind_ChainedOperations_ShouldComposeCorrectly()
        {
            // Arrange
            var outcome = Outcome<int>.From(2);

            // Act
            var result = outcome
                .Bind(x => Outcome<int>.From(x * 3))
                .Bind(x => Outcome<int>.From(x + 4));

            // Assert
            Assert.That(result.Value, Is.EqualTo(10)); // (2 * 3) + 4 = 10
        }
    }
}
