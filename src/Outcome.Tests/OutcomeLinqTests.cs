// Tests for LINQ extension methods (Select, SelectMany, Where) on Outcome<T>.

using Outcome;
using System.Collections.Generic;
using NUnit.Framework;

namespace Outcome.Tests
{
    /// <summary>
    /// Tests for LINQ extension methods (Select, SelectMany, Where) on Outcome<T>.
    /// </summary>
    [TestFixture]
    public class OutcomeLinqTests
    {
        [Test]
        public void Select_SuccessOutcome_ShouldTransformValue()
        {
            // Arrange
            var outcome = Outcome<int>.From(5);

            // Act
            var result = outcome.Select(x => x * 2);

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(10));
        }

        [Test]
        public void Select_ErrorOutcome_ShouldPropagateError()
        {
            // Arrange
            var errors = new List<object> { "Error1" };
            var outcome = Outcome<int>.FromErrors(errors);

            // Act
            var result = outcome.Select(x => x * 2);

            // Assert
            Assert.That(result.IsError, Is.True);
        }

        [Test]
        public void SelectMany_SuccessOutcome_ShouldChainOutcomes()
        {
            // Arrange
            var outcome1 = Outcome<int>.From(5);
            var outcome2 = Outcome<int>.From(3);

            // Act
            var result = outcome1.SelectMany(
                x => outcome2,
                (x, y) => x + y
            );

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(8));
        }

        [Test]
        public void SelectMany_ErrorInFirstOutcome_ShouldPropagateError()
        {
            // Arrange
            var outcome1 = Outcome<int>.FromErrors(new List<object> { "Error1" });
            var outcome2 = Outcome<int>.From(3);

            // Act
            var result = outcome1.SelectMany(
                x => outcome2,
                (x, y) => x + y
            );

            // Assert
            Assert.That(result.IsError, Is.True);
        }

        [Test]
        public void Where_SuccessOutcomePredicateTrue_ShouldReturnOutcome()
        {
            // Arrange
            var outcome = Outcome<int>.From(42);

            // Act
            var result = outcome.Where(x => x > 0);

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(42));
        }

        [Test]
        public void Where_SuccessOutcomePredicateFalse_ShouldReturnValidationError()
        {
            // Arrange
            var outcome = Outcome<int>.From(-5);

            // Act
            var result = outcome.Where(x => x > 0);

            // Assert
            Assert.That(result.IsError, Is.True);
        }

        [Test]
        public void Where_ErrorOutcome_ShouldPropagateError()
        {
            // Arrange
            var outcome = Outcome<int>.FromErrors(new List<object> { "Error1" });

            // Act
            var result = outcome.Where(x => x > 0);

            // Assert
            Assert.That(result.IsError, Is.True);
        }

        [Test]
        public void LinqQuerySyntax_SuccessOutcomes_ShouldWork()
        {
            // Arrange & Act
            var result = from x in Outcome<int>.From(10)
                          from y in Outcome<int>.From(5)
                          select x + y;

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(15));
        }

        [Test]
        public void LinqQuerySyntax_WithWhere_ShouldFilterCorrectly()
        {
            // Arrange & Act
            var result = from x in Outcome<int>.From(10)
                          where x > 0
                          select x * 2;

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(20));
        }
    }
}
