// Tests for the core Outcome<T> struct covering basic functionality,
// error handling, and implicit conversions.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using BbQ.Outcome;

namespace BbQ.Outcome.Tests
{
    /// <summary>
    /// Tests for the core Outcome<T> struct covering basic functionality,
    /// error handling, and implicit conversions.
    /// </summary>
    [TestFixture]
    public class OutcomeStructTests
    {
        [Test]
        public void SuccessOutcome_ShouldHaveValue()
        {
            // Arrange
            var expectedValue = 42;
            var outcome = Outcome<int>.From(expectedValue);
            // Act & Assert
            Assert.That(outcome.IsSuccess, Is.True);
            Assert.That(outcome.Value, Is.EqualTo(expectedValue));
        }

        [Test]
        public void ErrorOutcome_ShouldHaveErrors()
        {
            // Arrange
            var errors = new List<object> { "Error1", "Error2" };
            var outcome = Outcome<int>.FromErrors(errors);
            // Act & Assert
            Assert.That(outcome.IsError, Is.True);
            Assert.That(outcome.Errors, Is.EqualTo(errors));
        }

        [Test]
        public void AccessingValue_OnErrorOutcome_ShouldThrow()
        {
            // Arrange
            var errors = new List<object> { "Error1" };
            var outcome = Outcome<int>.FromErrors(errors);
            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => { var value = outcome.Value; });
        }

        [Test]
        public void AccessingErrors_OnSuccessOutcome_ShouldThrow()
        {
            // Arrange
            var outcome = Outcome<int>.From(100);
            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => { var errors = outcome.Errors; });
        }

        [Test]
        public void IsError_ShouldBeInverseOfIsSuccess()
        {
            // Arrange & Act
            var successOutcome = Outcome<int>.From(42);
            var errorOutcome = Outcome<int>.FromErrors(new List<object> { "error" });

            // Assert
            Assert.That(successOutcome.IsSuccess, Is.True);
            Assert.That(successOutcome.IsError, Is.False);
            Assert.That(errorOutcome.IsSuccess, Is.False);
            Assert.That(errorOutcome.IsError, Is.True);
        }

        [Test]
        public void ImplicitConversion_FromValueToOutcome_ShouldCreateSuccessOutcome()
        {
            // Arrange & Act
            Outcome<int> outcome = 42;

            // Assert
            Assert.That(outcome.IsSuccess, Is.True);
            Assert.That(outcome.Value, Is.EqualTo(42));
        }

        [Test]
        public void ToString_SuccessOutcome_ShouldShowValue()
        {
            // Arrange
            var outcome = Outcome<int>.From(42);

            // Act
            var result = outcome.ToString();

            // Assert
            Assert.That(result, Does.Contain("Success"));
            Assert.That(result, Does.Contain("42"));
        }

        [Test]
        public void ToString_ErrorOutcome_ShouldShowErrors()
        {
            // Arrange
            var outcome = Outcome<int>.FromErrors(new List<object> { "Error1" });

            // Act
            var result = outcome.ToString();

            // Assert
            Assert.That(result, Does.Contain("Error"));
        }

        [Test]
        public void Deconstruct_SuccessOutcome_ShouldReturnValueAndNullErrors()
        {
            // Arrange
            var outcome = Outcome<int>.From(42);

            // Act
            var (isSuccess, value, errors) = outcome;

            // Assert
            Assert.That(isSuccess, Is.True);
            Assert.That(value, Is.EqualTo(42));
            Assert.That(errors, Is.Null);
        }

        [Test]
        public void Deconstruct_ErrorOutcome_ShouldReturnErrorsAndDefaultValue()
        {
            // Arrange
            var errorList = new List<object> { "Error1", "Error2" };
            var outcome = Outcome<int>.FromErrors(errorList);

            // Act
            var (isSuccess, value, errors) = outcome;

            // Assert
            Assert.That(isSuccess, Is.False);
            Assert.That(value, Is.EqualTo(default(int)));
            Assert.That(errors, Is.EqualTo(errorList));
        }

        [Test]
        public void Deconstruct_TwoParameter_ShouldReturnValueAndErrors()
        {
            // Arrange
            var outcome = Outcome<string>.From("test");

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
            var errorList = new List<object> { "Error1" };
            var outcome = Outcome<int>.FromErrors(errorList);

            // Act
            var (value, errors) = outcome;

            // Assert
            Assert.That(errors, Is.EqualTo(errorList));
        }
    }
}
