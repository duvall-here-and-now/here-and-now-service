using FluentAssertions;
using HereAndNowService.Models;

namespace HereAndNowService.TaskTests.Models;

public class DerivedReminderIdTests
{
    [Fact]
    public void Generate_ValidUtcInput_ReturnsCorrectFormat()
    {
        // Arrange
        var configId = "abc123";
        var dt = new DateTime(2026, 5, 17, 7, 0, 0, DateTimeKind.Utc);

        // Act
        var result = DerivedReminderId.Generate(configId, dt);

        // Assert
        result.Should().Be("rtr_abc123_2026-05-17T07:00:00Z");
    }

    [Fact]
    public void Generate_PrefixIsLowercaseRtrUnderscore()
    {
        // Arrange
        var configId = "cfg-1";
        var dt = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc);

        // Act
        var result = DerivedReminderId.Generate(configId, dt);

        // Assert
        result.Should().StartWith("rtr_");
    }

    [Fact]
    public void Generate_OmitsMilliseconds()
    {
        // Arrange
        var configId = "cfg-1";
        var dt = new DateTime(2026, 5, 17, 7, 0, 0, 123, DateTimeKind.Utc);

        // Act
        var result = DerivedReminderId.Generate(configId, dt);

        // Assert
        result.Should().NotContain(".123");
    }

    [Fact]
    public void Generate_EndsWithZ()
    {
        // Arrange
        var configId = "cfg-1";
        var dt = new DateTime(2026, 3, 10, 14, 30, 0, DateTimeKind.Utc);

        // Act
        var result = DerivedReminderId.Generate(configId, dt);

        // Assert
        result.Should().EndWith("Z");
    }

    [Fact]
    public void Generate_IsDeterministic()
    {
        // Arrange
        var configId = "cfg-1";
        var dt = new DateTime(2026, 5, 17, 7, 0, 0, DateTimeKind.Utc);

        // Act
        var result1 = DerivedReminderId.Generate(configId, dt);
        var result2 = DerivedReminderId.Generate(configId, dt);

        // Assert
        result1.Should().Be(result2);
    }

    [Fact]
    public void Generate_DifferentConfigIds_ProduceDifferentIds()
    {
        // Arrange
        var dt = new DateTime(2026, 5, 17, 7, 0, 0, DateTimeKind.Utc);

        // Act
        var result1 = DerivedReminderId.Generate("cfg-1", dt);
        var result2 = DerivedReminderId.Generate("cfg-2", dt);

        // Assert
        result1.Should().NotBe(result2);
    }

    [Fact]
    public void Generate_DifferentDateTimes_ProduceDifferentIds()
    {
        // Arrange
        var configId = "cfg-1";

        // Act
        var result1 = DerivedReminderId.Generate(configId, new DateTime(2026, 5, 17, 7, 0, 0, DateTimeKind.Utc));
        var result2 = DerivedReminderId.Generate(configId, new DateTime(2026, 5, 18, 7, 0, 0, DateTimeKind.Utc));

        // Assert
        result1.Should().NotBe(result2);
    }

    [Fact]
    public void Generate_WithLocalDateTime_ThrowsArgumentException()
    {
        // Arrange
        var configId = "cfg-1";
        var localDt = new DateTime(2026, 5, 17, 7, 0, 0, DateTimeKind.Local);

        // Act
        var act = () => DerivedReminderId.Generate(configId, localDt);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("recurrenceDateAndTime")
            .WithMessage("*must be UTC*");
    }

    [Fact]
    public void Generate_WithUnspecifiedDateTime_ThrowsArgumentException()
    {
        // Arrange
        var configId = "cfg-1";
        var unspecifiedDt = new DateTime(2026, 5, 17, 7, 0, 0, DateTimeKind.Unspecified);

        // Act
        var act = () => DerivedReminderId.Generate(configId, unspecifiedDt);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("recurrenceDateAndTime")
            .WithMessage("*must be UTC*");
    }
}
