// Tests for the QbqOutcome source generator.

using NUnit.Framework;

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
        ValidationError,

        /// <summary>
        /// A not found error occurred.
        /// </summary>
        NotFound,

        /// <summary>
        /// An internal server error occurred.
        /// </summary>
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
            Assert.That(validationError.Severity, Is.EqualTo(ErrorSeverity.Error));
            Assert.That(validationError.Description, Is.Not.Null);
            Assert.That(validationError.Description, Is.Not.Empty);

            Assert.That(notFoundError.Code, Is.EqualTo(TestErrorCode.NotFound));
            Assert.That(internalError.Code, Is.EqualTo(TestErrorCode.InternalError));
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
    }
}
