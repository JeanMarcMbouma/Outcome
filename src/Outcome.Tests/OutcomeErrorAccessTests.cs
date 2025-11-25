// Tests for strongly-typed error access from Outcome<T>.

using NUnit.Framework;

namespace BbQ.Outcome.Tests
{
    [TestFixture]
    public class OutcomeErrorAccessTests
    {
        [Test]
        public void GetErrors_WithSpecificType_ShouldReturnOnlyThatType()
        {
            // Arrange
            var stringError = new Error<string>("CODE1", "String error");
            var intError = new Error<int>(42, "Int error");
            var outcome = Outcome<int>.FromErrors([stringError, intError]);

            // Act
            var stringErrors = outcome.GetErrors<int, string>();
            var intErrors = outcome.GetErrors<int, int>();

            // Assert
            Assert.That(stringErrors.Count(), Is.EqualTo(1));
            Assert.That(stringErrors.First(), Is.EqualTo(stringError));
            Assert.That(intErrors.Count(), Is.EqualTo(1));
            Assert.That(intErrors.First(), Is.EqualTo(intError));
        }

        [Test]
        public void GetError_WithSpecificType_ShouldReturnFirstOrNull()
        {
            // Arrange
            var error1 = new Error<string>("CODE1", "Error 1");
            var error2 = new Error<string>("CODE2", "Error 2");
            var outcome = Outcome<int>.FromErrors([error1, error2]);

            // Act
            var retrieved = outcome.GetError<int, string>();

            // Assert
            Assert.That(retrieved, Is.EqualTo(error1));
        }

        [Test]
        public void GetError_WithNoMatchingType_ShouldReturnNull()
        {
            // Arrange
            var error = new Error<string>("CODE", "Error");
            var outcome = Outcome<int>.FromErrors([error]);

            // Act
            var retrieved = outcome.GetError<int, int>();

            // Assert
            Assert.That(retrieved, Is.Null);
        }

        [Test]
        public void HasErrors_WithMatchingType_ShouldReturnTrue()
        {
            // Arrange
            var error = new Error<string>("CODE", "Error");
            var outcome = Outcome<int>.FromErrors([error]);

            // Act
            var hasErrors = outcome.HasErrors<int, string>();

            // Assert
            Assert.That(hasErrors, Is.True);
        }

        [Test]
        public void HasErrors_WithNoMatchingType_ShouldReturnFalse()
        {
            // Arrange
            var error = new Error<string>("CODE", "Error");
            var outcome = Outcome<int>.FromErrors([error]);

            // Act
            var hasErrors = outcome.HasErrors<int, int>();

            // Assert
            Assert.That(hasErrors, Is.False);
        }

        [Test]
        public void GetErrors_WithPredicate_ShouldFilterCorrectly()
        {
            // Arrange
            var error1 = new Error<string>("CODE1", "Error 1", ErrorSeverity.Validation);
            var error2 = new Error<string>("CODE2", "Error 2", ErrorSeverity.Error);
            var error3 = new Error<string>("CODE3", "Error 3", ErrorSeverity.Validation);
            var outcome = Outcome<int>.FromErrors([error1, error2, error3]);

            // Act
            var validationErrors = outcome.GetErrors<int, string>(
                e => e.Severity == ErrorSeverity.Validation
            );

            // Assert
            Assert.That(validationErrors.Count(), Is.EqualTo(2));
            Assert.That(validationErrors, Does.Contain(error1));
            Assert.That(validationErrors, Does.Contain(error3));
        }

        [Test]
        public void GetErrors_OnSuccessOutcome_ShouldReturnEmpty()
        {
            // Arrange
            var outcome = Outcome<int>.From(42);

            // Act
            var errors = outcome.GetErrors<int, string>();

            // Assert
            Assert.That(errors.Count(), Is.EqualTo(0));
        }

        [Test]
        public void StronglyTypedErrorAccess_RealWorldExample()
        {
            // This demonstrates the improved developer experience
            // Arrange
            var appError = new Error<AppError>(AppError.UserNotFound, "User not found");
            var outcome = Outcome<string>.FromError(appError);

            // Act - Easy, strongly-typed access
            var userErrors = outcome.GetErrors<string, AppError>();
            var userNotFoundErrors = outcome.GetErrors<string, AppError>(
                e => e.Code == AppError.UserNotFound
            );

            // Assert
            Assert.That(userErrors.Count(), Is.EqualTo(1));
            Assert.That(userNotFoundErrors.Count(), Is.EqualTo(1));
            Assert.That(userNotFoundErrors.First().Code, Is.EqualTo(AppError.UserNotFound));
        }
    }

    // Simplified test enum for the real-world example
    public enum AppError
    {
        UserNotFound,
        InvalidInput
    }
}
