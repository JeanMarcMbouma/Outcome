// Tests for Combine extension method on Outcome<T>.
// These test aggregating multiple outcomes.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using BbQ.Outcome;

namespace BbQ.Outcome.Tests
{
    /// <summary>
    /// Tests for Combine extension method on Outcome<T>.
    /// These test aggregating multiple outcomes.
    /// </summary>
    [TestFixture]
    public class OutcomeCombineTests
    {
        [Test]
        public void Combine_AllSuccesses_ShouldReturnAggregatedValues()
        {
            // Arrange
            var outcome1 = Outcome<int>.From(1);
            var outcome2 = Outcome<int>.From(2);
            var outcome3 = Outcome<int>.From(3);

            // Act
            var result = Outcome<int>.Combine(outcome1, outcome2, outcome3);

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.ToList(), Is.EqualTo(new List<int> { 1, 2, 3 }));
        }

        [Test]
        public void Combine_WithErrors_ShouldAggregateAllErrors()
        {
            // Arrange
            var outcome1 = Outcome<int>.From(1);
            var outcome2 = Outcome<int>.FromErrors(new List<object> { "Error1", "Error2" });
            var outcome3 = Outcome<int>.FromErrors(new List<object> { "Error3" });

            // Act
            var result = Outcome<int>.Combine(outcome1, outcome2, outcome3);

            // Assert
            Assert.That(result.IsError, Is.True);
            Assert.That(result.Errors.Count, Is.EqualTo(3));
        }

        [Test]
        public void Combine_Empty_ShouldReturnEmpty()
        {
            // Arrange & Act
            var result = Outcome<int>.Combine();

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Count(), Is.EqualTo(0));
        }

        [Test]
        public void Combine_SingleSuccess_ShouldReturnSingleValue()
        {
            // Arrange
            var outcome = Outcome<string>.From("test");

            // Act
            var result = Outcome<string>.Combine(outcome);

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Single(), Is.EqualTo("test"));
        }
    }
}
