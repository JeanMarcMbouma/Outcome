// Tests for the QbqOutcome source generator.

using NUnit.Framework;
using BbQ.Outcome;

namespace BbQ.Outcome.Tests
{
    /// <summary>
    /// Test enum marked with QbqOutcome attribute to trigger source generator.
    /// </summary>
    [QbqOutcome]
    public enum TestErrorCode
    {
        /// <summary>
        /// A validation error occurred.
        /// </summary>
        [ErrorSeverity(ErrorSeverity.Validation)]
        ValidationError,

        /// <summary>
        /// A not found error occurred.
        /// </summary>
        NotFound,

        /// <summary>
        /// An internal server error occurred.
        /// </summary>
        [ErrorSeverity(ErrorSeverity.Critical)]
        InternalError
    }

    /// <summary>
    /// Tests for the QbqOutcome source generator.
    /// </summary>
    [TestFixture]
    public class OutcomeSourceGeneratorTests
    {
        [Test]
        public void SourceGenerator_ShouldCreateErrorProperties()
        {
            // Arrange & Act
            var validationError = TestErrorCodeErrors.ValidationErrorError;
            var notFoundError = TestErrorCodeErrors.NotFoundError;
            var internalError = TestErrorCodeErrors.InternalErrorError;

            // Assert
            Assert.That(validationError.Code, Is.EqualTo(TestErrorCode.ValidationError));
            Assert.That(validationError.Severity, Is.EqualTo(ErrorSeverity.Validation));
            Assert.That(validationError.Description, Is.Not.Null);
            Assert.That(validationError.Description, Is.Not.Empty);

            Assert.That(notFoundError.Code, Is.EqualTo(TestErrorCode.NotFound));
            Assert.That(notFoundError.Severity, Is.EqualTo(ErrorSeverity.Error));
            
            Assert.That(internalError.Code, Is.EqualTo(TestErrorCode.InternalError));
            Assert.That(internalError.Severity, Is.EqualTo(ErrorSeverity.Critical));
        }

        [Test]
        public void SourceGenerator_ErrorsShouldBeRecords()
        {
            // Arrange
            var error1 = TestErrorCodeErrors.ValidationErrorError;
            var error2 = TestErrorCodeErrors.ValidationErrorError;

            // Act & Assert
            Assert.That(error1, Is.EqualTo(error2));
        }

        [Test]
        public void SourceGenerator_ShouldUseDefaultSeverityWhenNotSpecified()
        {
            // Arrange & Act
            var notFoundError = TestErrorCodeErrors.NotFoundError;

            // Assert - NotFound should have default Error severity since no custom severity is specified
            Assert.That(notFoundError.Severity, Is.EqualTo(ErrorSeverity.Error));
        }

        [Test]
        public void SourceGenerator_ShouldUseCustomSeverityWhenSpecified()
        {
            // Arrange & Act
            var validationError = TestErrorCodeErrors.ValidationErrorError;
            var internalError = TestErrorCodeErrors.InternalErrorError;
            var outcome = internalError.ToOutcome<int>();

            int state = outcome.Match(
                onSuccess: _ => 1,
                onError: errors => 0);
            // Assert - should use the specified severity from the attribute
            Assert.That(validationError.Severity, Is.EqualTo(ErrorSeverity.Validation));
            Assert.That(internalError.Severity, Is.EqualTo(ErrorSeverity.Critical));
            Assert.That(outcome.IsSuccess, Is.False);
            Assert.That(state, Is.EqualTo(0));
        }
    }
}
