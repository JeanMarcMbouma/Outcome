using BbQ.Events.Engine;
using NUnit.Framework;

namespace BbQ.Cqrs.Tests;

/// <summary>
/// Tests for ProjectionNameResolver to ensure consistent projection name resolution.
/// </summary>
[TestFixture]
public class ProjectionNameResolverTests
{
    // Test projection class
    private class TestProjection { }

    [Test]
    public void Resolve_WithNullProjectionType_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            ProjectionNameResolver.Resolve(null!, null));
    }

    [Test]
    public void Resolve_WithNullOptions_ReturnsTypeName()
    {
        // Arrange
        var projectionType = typeof(TestProjection);

        // Act
        var result = ProjectionNameResolver.Resolve(projectionType, null);

        // Assert
        Assert.That(result, Is.EqualTo("TestProjection"));
    }

    [Test]
    public void Resolve_WithOptionsButNullProjectionName_ReturnsTypeName()
    {
        // Arrange
        var projectionType = typeof(TestProjection);
        var options = new ProjectionOptions
        {
            ProjectionName = null!
        };

        // Act
        var result = ProjectionNameResolver.Resolve(projectionType, options);

        // Assert
        Assert.That(result, Is.EqualTo("TestProjection"));
    }

    [Test]
    public void Resolve_WithOptionsAndEmptyProjectionName_ReturnsTypeName()
    {
        // Arrange
        var projectionType = typeof(TestProjection);
        var options = new ProjectionOptions
        {
            ProjectionName = string.Empty
        };

        // Act
        var result = ProjectionNameResolver.Resolve(projectionType, options);

        // Assert
        Assert.That(result, Is.EqualTo("TestProjection"));
    }

    [Test]
    public void Resolve_WithOptionsAndWhitespaceProjectionName_ReturnsTypeName()
    {
        // Arrange
        var projectionType = typeof(TestProjection);
        var options = new ProjectionOptions
        {
            ProjectionName = "   "
        };

        // Act
        var result = ProjectionNameResolver.Resolve(projectionType, options);

        // Assert
        Assert.That(result, Is.EqualTo("TestProjection"));
    }

    [Test]
    public void Resolve_WithOptionsAndCustomProjectionName_ReturnsCustomName()
    {
        // Arrange
        var projectionType = typeof(TestProjection);
        var customName = "CustomProjectionName";
        var options = new ProjectionOptions
        {
            ProjectionName = customName
        };

        // Act
        var result = ProjectionNameResolver.Resolve(projectionType, options);

        // Assert
        Assert.That(result, Is.EqualTo(customName));
    }

    [Test]
    public void Resolve_WithDifferentProjectionTypes_ReturnsCorrectTypeNames()
    {
        // Arrange & Act & Assert
        Assert.That(
            ProjectionNameResolver.Resolve(typeof(TestProjection), null),
            Is.EqualTo("TestProjection"));

        Assert.That(
            ProjectionNameResolver.Resolve(typeof(ProjectionNameResolverTests), null),
            Is.EqualTo("ProjectionNameResolverTests"));

        Assert.That(
            ProjectionNameResolver.Resolve(typeof(string), null),
            Is.EqualTo("String"));
    }

    [Test]
    public void Resolve_ConsistencyBetweenCalls_ReturnsSameResult()
    {
        // Arrange
        var projectionType = typeof(TestProjection);
        var options = new ProjectionOptions
        {
            ProjectionName = "ConsistentName"
        };

        // Act
        var result1 = ProjectionNameResolver.Resolve(projectionType, options);
        var result2 = ProjectionNameResolver.Resolve(projectionType, options);

        // Assert
        Assert.That(result1, Is.EqualTo(result2));
        Assert.That(result1, Is.EqualTo("ConsistentName"));
    }
}
