// Tests for async extension methods on Outcome<T>.
// These test MapAsync, BindAsync, and CombineAsync.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using BbQ.Outcome;

namespace BbQ.Outcome.Tests
{
    /// <summary>
    /// Tests for async extension methods on Outcome<T>.
    /// These test MapAsync, BindAsync, and CombineAsync.
    /// </summary>
    [TestFixture]
    public class OutcomeAsyncTests
    {
        [Test]
        public async Task MapAsync_SuccessOutcome_ShouldTransformValueAsync()
        {
            // Arrange
            var outcome = Outcome<int>.From(5);

            // Act
            var result = await outcome.MapAsync(async x =>
            {
                await Task.Delay(10);
                return x * 2;
            });

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(10));
        }

        [Test]
        public async Task MapAsync_ErrorOutcome_ShouldPropagateErrorWithoutInvokingMapper()
        {
            // Arrange
            var errors = new List<object> { "Error1" };
            var outcome = Outcome<int>.FromErrors(errors);
            var mapperInvoked = false;

            // Act
            var result = await outcome.MapAsync(async x =>
            {
                mapperInvoked = true;
                await Task.Delay(10);
                return x * 2;
            });

            // Assert
            Assert.That(result.IsError, Is.True);
            Assert.That(mapperInvoked, Is.False);
        }

        [Test]
        public async Task BindAsync_SuccessOutcome_ShouldChainOutcomesAsync()
        {
            // Arrange
            var outcome = Outcome<int>.From(5);

            // Act
            var result = await outcome.BindAsync(async x =>
            {
                await Task.Delay(10);
                return Outcome<int>.From(x * 2);
            });

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(10));
        }

        [Test]
        public async Task BindAsync_ErrorOutcome_ShouldPropagateError()
        {
            // Arrange
            var errors = new List<object> { "Error1" };
            var outcome = Outcome<int>.FromErrors(errors);

            // Act
            var result = await outcome.BindAsync(async x =>
            {
                await Task.Delay(10);
                return Outcome<int>.From(x * 2);
            });

            // Assert
            Assert.That(result.IsError, Is.True);
        }

        [Test]
        public async Task CombineAsync_AllSuccesses_ShouldReturnAggregatedValues()
        {
            // Arrange
            var task1 = Task.FromResult(Outcome<int>.From(1));
            var task2 = Task.FromResult(Outcome<int>.From(2));
            var task3 = Task.FromResult(Outcome<int>.From(3));

            // Act
            var result = await Outcome<int>.CombineAsync(task1, task2, task3);

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.ToList(), Is.EqualTo(new List<int> { 1, 2, 3 }));
        }

        [Test]
        public async Task CombineAsync_WithErrors_ShouldAggregateAllErrors()
        {
            // Arrange
            var task1 = Task.FromResult(Outcome<int>.From(1));
            var task2 = Task.FromResult(Outcome<int>.FromErrors(new List<object> { "Error1" }));
            var task3 = Task.FromResult(Outcome<int>.FromErrors(new List<object> { "Error2" }));

            // Act
            var result = await Outcome<int>.CombineAsync(task1, task2, task3);

            // Assert
            Assert.That(result.IsError, Is.True);
            Assert.That(result.Errors.Count, Is.EqualTo(2));
        }
    }
}
