// Tests for ErrorExtensions factory methods.

using Outcome;
using NUnit.Framework;

namespace Outcome.Tests
{
    /// <summary>
    /// Tests for ErrorExtensions factory methods.
    /// </summary>
    [TestFixture]
    public class ErrorExtensionTests
    {
        [Test]
        public void Info_ShouldCreateInfoError()
        {
            // Act
            var error = Error<string>.Info("CODE", "Info message");

            // Assert
            Assert.That(error.Severity, Is.EqualTo(ErrorSeverity.Info));
            Assert.That(error.Code, Is.EqualTo("CODE"));
            Assert.That(error.Description, Is.EqualTo("Info message"));
        }

        [Test]
        public void Validation_ShouldCreateValidationError()
        {
            // Act
            var error = Error<string>.Validation("CODE", "Validation message");

            // Assert
            Assert.That(error.Severity, Is.EqualTo(ErrorSeverity.Validation));
        }

        [Test]
        public void Warning_ShouldCreateWarningError()
        {
            // Act
            var error = Error<string>.Warning("CODE", "Warning message");

            // Assert
            Assert.That(error.Severity, Is.EqualTo(ErrorSeverity.Warning));
        }

        [Test]
        public void Critical_ShouldCreateCriticalError()
        {
            // Act
            var error = Error<string>.Critical("CODE", "Critical message");

            // Assert
            Assert.That(error.Severity, Is.EqualTo(ErrorSeverity.Critical));
        }
    }
}
