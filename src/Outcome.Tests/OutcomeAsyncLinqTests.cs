// Tests for async LINQ extension methods on Task<Outcome<T>>.

using Outcome;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Outcome.Tests
{
    /// <summary>
    /// Tests for async LINQ extension methods on Task<Outcome<T>>.
    /// </summary>
    [TestFixture]
    public class OutcomeAsyncLinqTests
    {
        [Test]
        public async Task SelectAsync_SuccessOutcome_ShouldTransformValue()
        {
            // Arrange
            var outcome = Task.FromResult(Outcome<int>.From(5));

            // Act
            var result = await outcome.Select(x => x * 2);

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(10));
        }

        [Test]
        public async Task SelectAsync_ErrorOutcome_ShouldPropagateError()
        {
            // Arrange
            var outcome = Task.FromResult(Outcome<int>.FromErrors(new List<object> { "Error1" }));

            // Act
            var result = await outcome.Select(x => x * 2);

            // Assert
            Assert.That(result.IsError, Is.True);
        }

        [Test]
        public async Task SelectManyAsync_SuccessOutcomes_ShouldChain()
        {
            // Arrange
            var outcome = Task.FromResult(Outcome<int>.From(5));

            // Act
            var result = await outcome.SelectMany(
                x => Outcome<int>.From(x * 2),
                (x, y) => x + y
            );

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(15));
        }

        [Test]
        public async Task SelectManyAsync_ErrorOutcome_ShouldPropagateError()
        {
            // Arrange
            var outcome = Task.FromResult(Outcome<int>.FromErrors(new List<object> { "Error1" }));

            // Act
            var result = await outcome.SelectMany(
                x => Outcome<int>.From(x * 2),
                (x, y) => x + y
            );

            // Assert
            Assert.That(result.IsError, Is.True);
        }

        [Test]
        public async Task WhereAsync_SuccessOutcomePredicateTrue_ShouldReturnOutcome()
        {
            // Arrange
            var outcome = Task.FromResult(Outcome<int>.From(42));

            // Act
            var result = await outcome.Where(x => x > 0);

            // Assert
            Assert.That(result.IsSuccess, Is.True);
        }

        [Test]
        public async Task WhereAsync_SuccessOutcomePredicateFalse_ShouldReturnValidationError()
        {
            // Arrange
            var outcome = Task.FromResult(Outcome<int>.From(-5));

            // Act
            var result = await outcome.Where(x => x > 0);

            // Assert
            Assert.That(result.IsError, Is.True);
        }

        [Test]
        public async Task WhereAsync_ErrorOutcome_ShouldPropagateError()
        {
            // Arrange
            var outcome = Task.FromResult(Outcome<int>.FromErrors(new List<object> { "Error1" }));

            // Act
            var result = await outcome.Where(x => x > 0);

            // Assert
            Assert.That(result.IsError, Is.True);
        }
    }
}
