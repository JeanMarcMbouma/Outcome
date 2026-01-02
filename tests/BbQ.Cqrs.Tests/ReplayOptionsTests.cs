using BbQ.Events.Engine;
using NUnit.Framework;

namespace BbQ.Cqrs.Tests;

/// <summary>
/// Tests for ReplayOptions validation and configuration.
/// </summary>
[TestFixture]
public class ReplayOptionsTests
{
    [Test]
    public void ReplayOptions_DefaultValues_AreCorrect()
    {
        // Act
        var options = new ReplayOptions();

        // Assert
        Assert.That(options.FromCheckpoint, Is.False);
        Assert.That(options.FromPosition, Is.Null);
        Assert.That(options.ToPosition, Is.Null);
        Assert.That(options.BatchSize, Is.Null);
        Assert.That(options.Partition, Is.Null);
        Assert.That(options.DryRun, Is.False);
        Assert.That(options.CheckpointMode, Is.EqualTo(CheckpointMode.Normal));
    }

    [Test]
    public void Validate_WithValidOptions_DoesNotThrow()
    {
        // Arrange
        var options = new ReplayOptions
        {
            FromPosition = 0,
            ToPosition = 100,
            BatchSize = 50
        };

        // Act & Assert
        Assert.DoesNotThrow(() => options.Validate());
    }

    [Test]
    public void Validate_WithNegativeFromPosition_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = new ReplayOptions
        {
            FromPosition = -1
        };

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.That(ex!.Message, Does.Contain("FromPosition must be non-negative"));
    }

    [Test]
    public void Validate_WithNegativeToPosition_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = new ReplayOptions
        {
            ToPosition = -1
        };

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.That(ex!.Message, Does.Contain("ToPosition must be non-negative"));
    }

    [Test]
    public void Validate_WithFromPositionGreaterThanToPosition_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = new ReplayOptions
        {
            FromPosition = 100,
            ToPosition = 50
        };

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.That(ex!.Message, Does.Contain("cannot be greater than ToPosition"));
    }

    [Test]
    public void Validate_WithZeroBatchSize_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = new ReplayOptions
        {
            BatchSize = 0
        };

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.That(ex!.Message, Does.Contain("BatchSize must be positive"));
    }

    [Test]
    public void Validate_WithNegativeBatchSize_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = new ReplayOptions
        {
            BatchSize = -1
        };

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.That(ex!.Message, Does.Contain("BatchSize must be positive"));
    }

    [Test]
    public void Validate_WithFromPositionEqualToToPosition_DoesNotThrow()
    {
        // Arrange
        var options = new ReplayOptions
        {
            FromPosition = 50,
            ToPosition = 50
        };

        // Act & Assert
        Assert.DoesNotThrow(() => options.Validate());
    }

    [Test]
    public void Validate_WithNullPositions_DoesNotThrow()
    {
        // Arrange
        var options = new ReplayOptions
        {
            FromPosition = null,
            ToPosition = null
        };

        // Act & Assert
        Assert.DoesNotThrow(() => options.Validate());
    }

    [Test]
    public void Validate_WithOnlyFromPosition_DoesNotThrow()
    {
        // Arrange
        var options = new ReplayOptions
        {
            FromPosition = 100
        };

        // Act & Assert
        Assert.DoesNotThrow(() => options.Validate());
    }

    [Test]
    public void Validate_WithOnlyToPosition_DoesNotThrow()
    {
        // Arrange
        var options = new ReplayOptions
        {
            ToPosition = 100
        };

        // Act & Assert
        Assert.DoesNotThrow(() => options.Validate());
    }

    [Test]
    public void CheckpointMode_Normal_IsDefaultValue()
    {
        // Arrange & Act
        var mode = default(CheckpointMode);

        // Assert
        Assert.That(mode, Is.EqualTo(CheckpointMode.Normal));
    }

    [Test]
    public void CheckpointMode_HasExpectedValues()
    {
        // Assert
        Assert.That((int)CheckpointMode.Normal, Is.EqualTo(0));
        Assert.That((int)CheckpointMode.FinalOnly, Is.EqualTo(1));
        Assert.That((int)CheckpointMode.None, Is.EqualTo(2));
    }

    [Test]
    public void ReplayOptions_CanConfigureAllProperties()
    {
        // Arrange & Act
        var options = new ReplayOptions
        {
            FromCheckpoint = true,
            FromPosition = 100,
            ToPosition = 200,
            BatchSize = 50,
            Partition = "partition-1",
            DryRun = true,
            CheckpointMode = CheckpointMode.FinalOnly
        };

        // Assert
        Assert.That(options.FromCheckpoint, Is.True);
        Assert.That(options.FromPosition, Is.EqualTo(100));
        Assert.That(options.ToPosition, Is.EqualTo(200));
        Assert.That(options.BatchSize, Is.EqualTo(50));
        Assert.That(options.Partition, Is.EqualTo("partition-1"));
        Assert.That(options.DryRun, Is.True);
        Assert.That(options.CheckpointMode, Is.EqualTo(CheckpointMode.FinalOnly));
    }
}
