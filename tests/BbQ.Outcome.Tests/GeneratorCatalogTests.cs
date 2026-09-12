using BbQ.Outcome;
using NUnit.Framework;
using System.ComponentModel;

namespace BbQ.Outcome.Tests.CatalogOne
{
    [QbqOutcome]
    public enum Status
    {
        [ErrorCode("USER_MISSING")]
        [ErrorResourceKey("Errors.UserMissing")]
        [Description("A \"quoted\" user error")]
        [ErrorSeverity(ErrorSeverity.Warning)]
        Missing,
        /// <summary>A documented error.</summary>
        Other
    }
}

namespace BbQ.Outcome.Tests.CatalogTwo
{
    [QbqOutcome]
    public enum Status { Missing }
}

namespace BbQ.Outcome.Tests
{
    [TestFixture]
    public sealed class GeneratorCatalogTests
    {
        [Test]
        public void EqualEnumNamesInDifferentNamespacesGenerateIndependentHelpers()
        {
            Assert.That(CatalogOne.StatusErrors.MissingError.Code, Is.EqualTo(CatalogOne.Status.Missing));
            Assert.That(CatalogTwo.StatusErrors.MissingError.Code, Is.EqualTo(CatalogTwo.Status.Missing));
            Assert.That(CatalogOne.StatusErrors.All.Count, Is.EqualTo(2));
            Assert.That(CatalogTwo.StatusErrors.All.Count, Is.EqualTo(1));
        }

        [Test]
        public void CatalogPreservesCodesResourcesDescriptionsAndSeverity()
        {
            var descriptor = CatalogOne.StatusErrors.Describe(CatalogOne.Status.Missing);
            Assert.That(descriptor.Code, Is.EqualTo("USER_MISSING"));
            Assert.That(descriptor.ResourceKey, Is.EqualTo("Errors.UserMissing"));
            Assert.That(descriptor.Description, Is.EqualTo("A \"quoted\" user error"));
            Assert.That(descriptor.Severity, Is.EqualTo(ErrorSeverity.Warning));
            Assert.That(CatalogOne.StatusErrors.OtherError.Description, Is.EqualTo("A documented error."));
            Assert.Throws<ArgumentOutOfRangeException>(() => CatalogOne.StatusErrors.Describe((CatalogOne.Status)999));
        }

        [Test]
        public void ProviderKeepsPerOccurrenceDescriptionAndSeverity()
        {
            var error = CatalogOne.StatusErrors.MissingError with { Description = "Specific occurrence", Severity = ErrorSeverity.Critical };
            var descriptor = CatalogOne.StatusErrors.DescriptorProvider.Describe(error);
            Assert.That(descriptor.Code, Is.EqualTo("USER_MISSING"));
            Assert.That(descriptor.Description, Is.EqualTo("Specific occurrence"));
            Assert.That(descriptor.Severity, Is.EqualTo(ErrorSeverity.Critical));
        }
    }
}
