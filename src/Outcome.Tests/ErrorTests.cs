// Tests for Error and ErrorSeverity types.

using Outcome;
using NUnit.Framework;

namespace Outcome.Tests
{
    /// <summary>
    /// Tests for Error and ErrorSeverity types.
    /// </summary>
    [TestFixture]
    public class ErrorTests
    {
        [Test]
        public void Error_ShouldBeRecord()
        {
            // Arrange
            var error1 = new Error<string>("CODE", "Description");
            var error2 = new Error<string>("CODE", "Description");

            // Act & Assert
            Assert.That(error1, Is.EqualTo(error2));
        }

        [Test]
        public void Error_DefaultSeverity_ShouldBeError()
        {
            // Arrange
            var error = new Error<string>("CODE", "Description");

            // Act & Assert
            Assert.That(error.Severity, Is.EqualTo(ErrorSeverity.Error));
        }

        [Test]
        public void Error_CustomSeverity_ShouldBeSet()
        {
            // Arrange
            var error = new Error<string>("CODE", "Description", ErrorSeverity.Critical);

            // Act & Assert
            Assert.That(error.Severity, Is.EqualTo(ErrorSeverity.Critical));
        }
    }
}
