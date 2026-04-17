// Tests for the Outcome<T, TError> struct covering basic functionality,
// error handling, implicit conversions, and deconstruction.

using NUnit.Framework;

namespace BbQ.Outcome.Tests
{
    /// <summary>
    /// Tests for the strongly-typed Outcome&lt;T, TError&gt; struct.
    /// </summary>
    [TestFixture]
    public class OutcomeTypedTests
    {
        private enum TestError
        {
            NotFound,
            Unauthorized,
            Conflict
        }

        [Test]
        public void From_ShouldCreateSuccessOutcome()
        {
            // Arrange & Act
            var outcome = Outcome<int, TestError>.From(42);

            // Assert
            Assert.That(outcome.IsSuccess, Is.True);
            Assert.That(outcome.IsError, Is.False);
            Assert.That(outcome.Value, Is.EqualTo(42));
        }

        [Test]
        public void FromError_ShouldCreateFailureOutcomeWithSingleError()
        {
            // Arrange & Act
            var outcome = Outcome<int, TestError>.FromError(TestError.NotFound);

            // Assert
            Assert.That(outcome.IsError, Is.True);
            Assert.That(outcome.IsSuccess, Is.False);
            Assert.That(outcome.Errors, Has.Count.EqualTo(1));
            Assert.That(outcome.Errors[0], Is.EqualTo(TestError.NotFound));
        }

        [Test]
        public void FromErrors_ShouldCreateFailureOutcomeWithMultipleErrors()
        {
            // Arrange
            var errors = new List<TestError> { TestError.NotFound, TestError.Unauthorized };

            // Act
            var outcome = Outcome<int, TestError>.FromErrors(errors);

            // Assert
            Assert.That(outcome.IsError, Is.True);
            Assert.That(outcome.Errors, Has.Count.EqualTo(2));
            Assert.That(outcome.Errors, Is.EqualTo(errors));
        }

        [Test]
        public void AccessingValue_OnErrorOutcome_ShouldThrow()
        {
            // Arrange
            var outcome = Outcome<int, TestError>.FromError(TestError.NotFound);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => { var _ = outcome.Value; });
        }

        [Test]
        public void AccessingErrors_OnSuccessOutcome_ShouldThrow()
        {
            // Arrange
            var outcome = Outcome<int, TestError>.From(42);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => { var _ = outcome.Errors; });
        }

        [Test]
        public void ImplicitConversion_FromValue_ShouldCreateSuccessOutcome()
        {
            // Arrange & Act
            Outcome<int, TestError> outcome = 42;

            // Assert
            Assert.That(outcome.IsSuccess, Is.True);
            Assert.That(outcome.Value, Is.EqualTo(42));
        }

        [Test]
        public void ImplicitConversion_FromError_ShouldCreateFailureOutcome()
        {
            // Arrange & Act
            Outcome<int, TestError> outcome = TestError.Unauthorized;

            // Assert
            Assert.That(outcome.IsError, Is.True);
            Assert.That(outcome.Errors, Has.Count.EqualTo(1));
            Assert.That(outcome.Errors[0], Is.EqualTo(TestError.Unauthorized));
        }

        [Test]
        public void ImplicitConversion_FromError_CanBeReturnedFromMethod()
        {
            // Act
            var outcome = MethodThatReturnsError();

            // Assert
            Assert.That(outcome.IsError, Is.True);
            Assert.That(outcome.Errors[0], Is.EqualTo(TestError.Conflict));
        }

        [Test]
        public void ImplicitConversion_FromValue_CanBeReturnedFromMethod()
        {
            // Act
            var outcome = MethodThatReturnsValue();

            // Assert
            Assert.That(outcome.IsSuccess, Is.True);
            Assert.That(outcome.Value, Is.EqualTo("hello"));
        }

        [Test]
        public void ImplicitConversion_FromStringError_ShouldCreateFailureOutcome()
        {
            // Arrange & Act
            Outcome<int, string> outcome = "something went wrong";

            // Assert
            Assert.That(outcome.IsError, Is.True);
            Assert.That(outcome.Errors[0], Is.EqualTo("something went wrong"));
        }

        [Test]
        public void ToString_SuccessOutcome_ShouldContainValue()
        {
            // Arrange
            var outcome = Outcome<int, TestError>.From(42);

            // Act
            var result = outcome.ToString();

            // Assert
            Assert.That(result, Does.Contain("Success"));
            Assert.That(result, Does.Contain("42"));
        }

        [Test]
        public void ToString_ErrorOutcome_ShouldContainErrors()
        {
            // Arrange
            var outcome = Outcome<int, TestError>.FromError(TestError.NotFound);

            // Act
            var result = outcome.ToString();

            // Assert
            Assert.That(result, Does.Contain("Error"));
            Assert.That(result, Does.Contain("NotFound"));
        }

        [Test]
        public void Deconstruct_ThreeParameter_SuccessOutcome_ShouldReturnValueAndNullErrors()
        {
            // Arrange
            var outcome = Outcome<int, TestError>.From(42);

            // Act
            var (isSuccess, value, errors) = outcome;

            // Assert
            Assert.That(isSuccess, Is.True);
            Assert.That(value, Is.EqualTo(42));
            Assert.That(errors, Is.Null);
        }

        [Test]
        public void Deconstruct_ThreeParameter_ErrorOutcome_ShouldReturnErrorsAndDefaultValue()
        {
            // Arrange
            var outcome = Outcome<int, TestError>.FromError(TestError.Unauthorized);

            // Act
            var (isSuccess, value, errors) = outcome;

            // Assert
            Assert.That(isSuccess, Is.False);
            Assert.That(value, Is.EqualTo(default(int)));
            Assert.That(errors, Has.Count.EqualTo(1));
            Assert.That(errors![0], Is.EqualTo(TestError.Unauthorized));
        }

        [Test]
        public void Deconstruct_TwoParameter_SuccessOutcome_ShouldReturnValueAndEmptyErrors()
        {
            // Arrange
            var outcome = Outcome<string, TestError>.From("test");

            // Act
            var (value, errors) = outcome;

            // Assert
            Assert.That(value, Is.EqualTo("test"));
            Assert.That(errors, Is.Empty);
        }

        [Test]
        public void Deconstruct_TwoParameter_ErrorOutcome_ShouldReturnErrors()
        {
            // Arrange
            var outcome = Outcome<int, TestError>.FromError(TestError.Conflict);

            // Act
            var (value, errors) = outcome;

            // Assert
            Assert.That(errors, Has.Count.EqualTo(1));
            Assert.That(errors![0], Is.EqualTo(TestError.Conflict));
        }

        [Test]
        public void IsError_ShouldBeInverseOfIsSuccess()
        {
            // Arrange
            var success = Outcome<int, TestError>.From(1);
            var failure = Outcome<int, TestError>.FromError(TestError.NotFound);

            // Assert
            Assert.That(success.IsSuccess, Is.True);
            Assert.That(success.IsError, Is.False);
            Assert.That(failure.IsSuccess, Is.False);
            Assert.That(failure.IsError, Is.True);
        }

        private static Outcome<string, TestError> MethodThatReturnsError()
        {
            return TestError.Conflict;
        }

        private static Outcome<string, TestError> MethodThatReturnsValue()
        {
            return "hello";
        }
    }
}
