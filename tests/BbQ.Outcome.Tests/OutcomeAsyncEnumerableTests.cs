// Tests for IAsyncEnumerable extension methods on Outcome<T>.
// These test Select, SelectMany, Where, Bind, Map, Values, and Errors
// over async streams of outcomes.

using NUnit.Framework;
using System.Runtime.CompilerServices;

namespace BbQ.Outcome.Tests
{
    /// <summary>
    /// Tests for IAsyncEnumerable extension methods on streams of Outcome&lt;T&gt;.
    /// </summary>
    [TestFixture]
    public class OutcomeAsyncEnumerableTests
    {
        // ============ Select Tests ============

        [Test]
        public async Task Select_SuccessOutcomes_ShouldTransformValues()
        {
            // Arrange
            var stream = CreateStream(
                Outcome<int>.From(1),
                Outcome<int>.From(2),
                Outcome<int>.From(3));

            // Act
            var results = await CollectAsync(stream.Select(x => x * 10));

            // Assert
            Assert.That(results, Has.Count.EqualTo(3));
            Assert.That(results[0].IsSuccess, Is.True);
            Assert.That(results[0].Value, Is.EqualTo(10));
            Assert.That(results[1].Value, Is.EqualTo(20));
            Assert.That(results[2].Value, Is.EqualTo(30));
        }

        [Test]
        public async Task Select_ErrorOutcomes_ShouldPropagateErrors()
        {
            // Arrange
            var stream = CreateStream(
                Outcome<int>.FromErrors(["Error1"]),
                Outcome<int>.FromErrors(["Error2"]));

            // Act
            var results = await CollectAsync(stream.Select(x => x * 10));

            // Assert
            Assert.That(results, Has.Count.EqualTo(2));
            Assert.That(results[0].IsError, Is.True);
            Assert.That(results[0].Errors[0], Is.EqualTo("Error1"));
            Assert.That(results[1].IsError, Is.True);
            Assert.That(results[1].Errors[0], Is.EqualTo("Error2"));
        }

        [Test]
        public async Task Select_MixedOutcomes_ShouldTransformSuccessesAndPropagateErrors()
        {
            // Arrange
            var stream = CreateStream(
                Outcome<int>.From(5),
                Outcome<int>.FromErrors(["Error1"]),
                Outcome<int>.From(10));

            // Act
            var results = await CollectAsync(stream.Select(x => x * 2));

            // Assert
            Assert.That(results, Has.Count.EqualTo(3));
            Assert.That(results[0].IsSuccess, Is.True);
            Assert.That(results[0].Value, Is.EqualTo(10));
            Assert.That(results[1].IsError, Is.True);
            Assert.That(results[2].IsSuccess, Is.True);
            Assert.That(results[2].Value, Is.EqualTo(20));
        }

        [Test]
        public async Task Select_EmptyStream_ShouldReturnEmpty()
        {
            // Arrange
            var stream = CreateStream<int>();

            // Act
            var results = await CollectAsync(stream.Select(x => x * 2));

            // Assert
            Assert.That(results, Is.Empty);
        }

        // ============ Map Tests ============

        [Test]
        public async Task Map_SuccessOutcomes_ShouldTransformValues()
        {
            // Arrange
            var stream = CreateStream(
                Outcome<int>.From(1),
                Outcome<int>.From(2));

            // Act
            var results = await CollectAsync(stream.Map(x => x.ToString()));

            // Assert
            Assert.That(results, Has.Count.EqualTo(2));
            Assert.That(results[0].IsSuccess, Is.True);
            Assert.That(results[0].Value, Is.EqualTo("1"));
            Assert.That(results[1].Value, Is.EqualTo("2"));
        }

        [Test]
        public async Task Map_ErrorOutcomes_ShouldPropagateErrors()
        {
            // Arrange
            var stream = CreateStream(
                Outcome<int>.FromErrors(["Error1"]));

            // Act
            var results = await CollectAsync(stream.Map(x => x.ToString()));

            // Assert
            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(results[0].IsError, Is.True);
        }

        // ============ Bind Tests ============

        [Test]
        public async Task Bind_SuccessOutcomes_ShouldApplyBinder()
        {
            // Arrange
            var stream = CreateStream(
                Outcome<int>.From(5),
                Outcome<int>.From(10));

            // Act
            var results = await CollectAsync(stream.Bind(x =>
                x > 0 ? Outcome<string>.From($"Value:{x}") : Outcome<string>.FromErrors(["Negative"])));

            // Assert
            Assert.That(results, Has.Count.EqualTo(2));
            Assert.That(results[0].IsSuccess, Is.True);
            Assert.That(results[0].Value, Is.EqualTo("Value:5"));
            Assert.That(results[1].Value, Is.EqualTo("Value:10"));
        }

        [Test]
        public async Task Bind_SuccessOutcome_BinderReturnsError_ShouldReturnError()
        {
            // Arrange
            var stream = CreateStream(
                Outcome<int>.From(-1));

            // Act
            var results = await CollectAsync(stream.Bind(x =>
                x > 0 ? Outcome<string>.From($"Value:{x}") : Outcome<string>.FromErrors(["Negative"])));

            // Assert
            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(results[0].IsError, Is.True);
            Assert.That(results[0].Errors[0], Is.EqualTo("Negative"));
        }

        [Test]
        public async Task Bind_ErrorOutcomes_ShouldPropagateErrors()
        {
            // Arrange
            var stream = CreateStream(
                Outcome<int>.FromErrors(["OriginalError"]));

            // Act
            var results = await CollectAsync(stream.Bind(x => Outcome<string>.From($"Value:{x}")));

            // Assert
            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(results[0].IsError, Is.True);
            Assert.That(results[0].Errors[0], Is.EqualTo("OriginalError"));
        }

        // ============ SelectMany Tests ============

        [Test]
        public async Task SelectMany_SuccessOutcomes_ShouldBindAndProject()
        {
            // Arrange
            var stream = CreateStream(
                Outcome<int>.From(5),
                Outcome<int>.From(10));

            // Act
            var results = await CollectAsync(stream.SelectMany(
                x => Outcome<int>.From(x * 2),
                (original, doubled) => original + doubled));

            // Assert
            Assert.That(results, Has.Count.EqualTo(2));
            Assert.That(results[0].IsSuccess, Is.True);
            Assert.That(results[0].Value, Is.EqualTo(15)); // 5 + 10
            Assert.That(results[1].Value, Is.EqualTo(30)); // 10 + 20
        }

        [Test]
        public async Task SelectMany_BinderReturnsError_ShouldReturnError()
        {
            // Arrange
            var stream = CreateStream(
                Outcome<int>.From(5));

            // Act
            var results = await CollectAsync(stream.SelectMany(
                x => Outcome<int>.FromErrors(["BindError"]),
                (original, intermediate) => original + intermediate));

            // Assert
            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(results[0].IsError, Is.True);
        }

        [Test]
        public async Task SelectMany_ErrorOutcomes_ShouldPropagateErrors()
        {
            // Arrange
            var stream = CreateStream(
                Outcome<int>.FromErrors(["OriginalError"]));

            // Act
            var results = await CollectAsync(stream.SelectMany(
                x => Outcome<int>.From(x * 2),
                (x, y) => x + y));

            // Assert
            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(results[0].IsError, Is.True);
            Assert.That(results[0].Errors[0], Is.EqualTo("OriginalError"));
        }

        // ============ Where Tests ============

        [Test]
        public async Task Where_PredicateTrue_ShouldPassThrough()
        {
            // Arrange
            var stream = CreateStream(
                Outcome<int>.From(42),
                Outcome<int>.From(10));

            // Act
            var results = await CollectAsync(stream.Where(x => x > 0));

            // Assert
            Assert.That(results, Has.Count.EqualTo(2));
            Assert.That(results[0].IsSuccess, Is.True);
            Assert.That(results[0].Value, Is.EqualTo(42));
            Assert.That(results[1].IsSuccess, Is.True);
            Assert.That(results[1].Value, Is.EqualTo(10));
        }

        [Test]
        public async Task Where_PredicateFalse_ShouldReturnValidationError()
        {
            // Arrange
            var stream = CreateStream(
                Outcome<int>.From(-5));

            // Act
            var results = await CollectAsync(stream.Where(x => x > 0));

            // Assert
            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(results[0].IsError, Is.True);
        }

        [Test]
        public async Task Where_MixedPredicateResults_ShouldFilterCorrectly()
        {
            // Arrange
            var stream = CreateStream(
                Outcome<int>.From(10),
                Outcome<int>.From(-1),
                Outcome<int>.From(20));

            // Act
            var results = await CollectAsync(stream.Where(x => x > 0));

            // Assert
            Assert.That(results, Has.Count.EqualTo(3));
            Assert.That(results[0].IsSuccess, Is.True);
            Assert.That(results[0].Value, Is.EqualTo(10));
            Assert.That(results[1].IsError, Is.True); // -1 fails predicate
            Assert.That(results[2].IsSuccess, Is.True);
            Assert.That(results[2].Value, Is.EqualTo(20));
        }

        [Test]
        public async Task Where_ErrorOutcomes_ShouldPropagateWithoutCheckingPredicate()
        {
            // Arrange
            var stream = CreateStream(
                Outcome<int>.FromErrors(["Error1"]));

            // Act
            var results = await CollectAsync(stream.Where(x => x > 0));

            // Assert
            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(results[0].IsError, Is.True);
            Assert.That(results[0].Errors[0], Is.EqualTo("Error1"));
        }

        // ============ Values Tests ============

        [Test]
        public async Task Values_AllSuccesses_ShouldReturnAllValues()
        {
            // Arrange
            var stream = CreateStream(
                Outcome<int>.From(1),
                Outcome<int>.From(2),
                Outcome<int>.From(3));

            // Act
            var results = await CollectAsync(stream.Values());

            // Assert
            Assert.That(results, Is.EqualTo(new[] { 1, 2, 3 }));
        }

        [Test]
        public async Task Values_AllErrors_ShouldReturnEmpty()
        {
            // Arrange
            var stream = CreateStream(
                Outcome<int>.FromErrors(["Error1"]),
                Outcome<int>.FromErrors(["Error2"]));

            // Act
            var results = await CollectAsync(stream.Values());

            // Assert
            Assert.That(results, Is.Empty);
        }

        [Test]
        public async Task Values_MixedOutcomes_ShouldReturnOnlySuccessValues()
        {
            // Arrange
            var stream = CreateStream(
                Outcome<int>.From(1),
                Outcome<int>.FromErrors(["Error1"]),
                Outcome<int>.From(3),
                Outcome<int>.FromErrors(["Error2"]),
                Outcome<int>.From(5));

            // Act
            var results = await CollectAsync(stream.Values());

            // Assert
            Assert.That(results, Is.EqualTo(new[] { 1, 3, 5 }));
        }

        [Test]
        public async Task Values_EmptyStream_ShouldReturnEmpty()
        {
            // Arrange
            var stream = CreateStream<int>();

            // Act
            var results = await CollectAsync(stream.Values());

            // Assert
            Assert.That(results, Is.Empty);
        }

        // ============ Errors Tests ============

        [Test]
        public async Task Errors_AllErrors_ShouldReturnAllErrorLists()
        {
            // Arrange
            var stream = CreateStream(
                Outcome<int>.FromErrors(["Error1"]),
                Outcome<int>.FromErrors(["Error2", "Error3"]));

            // Act
            var results = await CollectAsync(stream.Errors());

            // Assert
            Assert.That(results, Has.Count.EqualTo(2));
            Assert.That(results[0][0], Is.EqualTo("Error1"));
            Assert.That(results[1][0], Is.EqualTo("Error2"));
            Assert.That(results[1][1], Is.EqualTo("Error3"));
        }

        [Test]
        public async Task Errors_AllSuccesses_ShouldReturnEmpty()
        {
            // Arrange
            var stream = CreateStream(
                Outcome<int>.From(1),
                Outcome<int>.From(2));

            // Act
            var results = await CollectAsync(stream.Errors());

            // Assert
            Assert.That(results, Is.Empty);
        }

        [Test]
        public async Task Errors_MixedOutcomes_ShouldReturnOnlyErrorLists()
        {
            // Arrange
            var stream = CreateStream(
                Outcome<int>.From(1),
                Outcome<int>.FromErrors(["Error1"]),
                Outcome<int>.From(3),
                Outcome<int>.FromErrors(["Error2"]));

            // Act
            var results = await CollectAsync(stream.Errors());

            // Assert
            Assert.That(results, Has.Count.EqualTo(2));
            Assert.That(results[0][0], Is.EqualTo("Error1"));
            Assert.That(results[1][0], Is.EqualTo("Error2"));
        }

        [Test]
        public async Task Errors_EmptyStream_ShouldReturnEmpty()
        {
            // Arrange
            var stream = CreateStream<int>();

            // Act
            var results = await CollectAsync(stream.Errors());

            // Assert
            Assert.That(results, Is.Empty);
        }

        // ============ Cancellation Tests ============

        [Test]
        public async Task Select_CancellationRequested_ShouldStopEnumeration()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            var stream = CreateInfiniteStream();
            var count = 0;

            // Act & Assert
            await foreach (var item in stream.Select(x => x * 2).WithCancellation(cts.Token))
            {
                count++;
                if (count >= 3)
                    cts.Cancel();
            }

            Assert.That(count, Is.GreaterThanOrEqualTo(3));
        }

        [Test]
        public async Task Values_CancellationRequested_ShouldStopEnumeration()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            var stream = CreateInfiniteStream();
            var count = 0;

            // Act & Assert
            await foreach (var value in stream.Values().WithCancellation(cts.Token))
            {
                count++;
                if (count >= 3)
                    cts.Cancel();
            }

            Assert.That(count, Is.GreaterThanOrEqualTo(3));
        }

        // ============ Chaining Tests ============

        [Test]
        public async Task Chaining_SelectThenWhere_ShouldComposeCorrectly()
        {
            // Arrange
            var stream = CreateStream(
                Outcome<int>.From(1),
                Outcome<int>.From(2),
                Outcome<int>.From(3),
                Outcome<int>.From(4));

            // Act: double each value, then filter for values > 5
            var results = await CollectAsync(
                stream.Select(x => x * 2).Where(x => x > 5));

            // Assert
            Assert.That(results, Has.Count.EqualTo(4));
            Assert.That(results[0].IsError, Is.True);  // 2 fails predicate
            Assert.That(results[1].IsError, Is.True);  // 4 fails predicate
            Assert.That(results[2].IsSuccess, Is.True); // 6 passes
            Assert.That(results[2].Value, Is.EqualTo(6));
            Assert.That(results[3].IsSuccess, Is.True); // 8 passes
            Assert.That(results[3].Value, Is.EqualTo(8));
        }

        [Test]
        public async Task Chaining_BindThenValues_ShouldComposeCorrectly()
        {
            // Arrange
            var stream = CreateStream(
                Outcome<int>.From(5),
                Outcome<int>.From(-1),
                Outcome<int>.From(10));

            // Act: bind (only positive), then extract values
            var results = await CollectAsync(
                stream.Bind(x => x > 0
                    ? Outcome<string>.From($"ok:{x}")
                    : Outcome<string>.FromErrors(["Negative"]))
                .Values());

            // Assert
            Assert.That(results, Is.EqualTo(new[] { "ok:5", "ok:10" }));
        }

        // ============ Helpers ============

        /// <summary>
        /// Creates an async stream from an array of outcomes.
        /// </summary>
        private static async IAsyncEnumerable<Outcome<T>> CreateStream<T>(
            params Outcome<T>[] items)
        {
            foreach (var item in items)
            {
                await Task.Yield();
                yield return item;
            }
        }

        /// <summary>
        /// Creates an infinite async stream of successful integer outcomes.
        /// Consumers must cancel to stop enumeration.
        /// </summary>
        private static async IAsyncEnumerable<Outcome<int>> CreateInfiniteStream(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var i = 0;
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Yield();
                yield return Outcome<int>.From(i++);
            }
        }

        /// <summary>
        /// Collects all items from an async enumerable into a list.
        /// </summary>
        private static async Task<List<T>> CollectAsync<T>(IAsyncEnumerable<T> source)
        {
            var list = new List<T>();
            await foreach (var item in source)
            {
                list.Add(item);
            }
            return list;
        }
    }
}
