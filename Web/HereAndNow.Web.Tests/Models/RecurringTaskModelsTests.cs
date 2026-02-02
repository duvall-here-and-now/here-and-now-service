using FluentAssertions;
using HereAndNowService.Models;

namespace HereAndNow.Web.Tests.Models;

public class RecurringTaskModelsTests
{
    #region RecurringTaskStateOverrideDocument.GenerateId Tests

    [Fact]
    public void GenerateId_WithValidInputs_ReturnsCorrectFormat()
    {
        // Arrange
        var configId = "a1b2c3d4-e5f6-7890-abcd-ef1234567890";
        var recurrenceDateTime = new DateTime(2026, 2, 15, 9, 0, 0, DateTimeKind.Utc);

        // Act
        var result = RecurringTaskStateOverrideDocument.GenerateId(configId, recurrenceDateTime);

        // Assert
        result.Should().Be("a1b2c3d4-e5f6-7890-abcd-ef1234567890_2026-02-15T09:00:00Z");
    }

    [Fact]
    public void GenerateId_UsesUnderscoreSeparator()
    {
        // Arrange
        var configId = "config-123";
        var recurrenceDateTime = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        // Act
        var result = RecurringTaskStateOverrideDocument.GenerateId(configId, recurrenceDateTime);

        // Assert
        result.Should().Contain("_");
        result.Should().NotContain("config-123-2026"); // Not hyphen separator
    }

    [Fact]
    public void GenerateId_FormatsDateTimeWithoutFractionalSeconds()
    {
        // Arrange
        var configId = "test-config";
        var recurrenceDateTime = new DateTime(2026, 3, 15, 14, 30, 45, 123, DateTimeKind.Utc);

        // Act
        var result = RecurringTaskStateOverrideDocument.GenerateId(configId, recurrenceDateTime);

        // Assert
        result.Should().EndWith("2026-03-15T14:30:45Z");
        result.Should().NotContain(".123"); // No milliseconds
    }

    [Fact]
    public void GenerateId_PadsSingleDigitValues()
    {
        // Arrange
        var configId = "config";
        var recurrenceDateTime = new DateTime(2026, 1, 5, 8, 5, 3, DateTimeKind.Utc);

        // Act
        var result = RecurringTaskStateOverrideDocument.GenerateId(configId, recurrenceDateTime);

        // Assert
        result.Should().Be("config_2026-01-05T08:05:03Z");
    }

    [Fact]
    public void GenerateId_EndsWithZ_IndicatingUtc()
    {
        // Arrange
        var configId = "config";
        var recurrenceDateTime = new DateTime(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);

        // Act
        var result = RecurringTaskStateOverrideDocument.GenerateId(configId, recurrenceDateTime);

        // Assert
        result.Should().EndWith("Z");
    }

    [Fact]
    public void GenerateId_IsDeterministic()
    {
        // Arrange
        var configId = "same-config";
        var recurrenceDateTime = new DateTime(2026, 5, 20, 10, 30, 0, DateTimeKind.Utc);

        // Act
        var result1 = RecurringTaskStateOverrideDocument.GenerateId(configId, recurrenceDateTime);
        var result2 = RecurringTaskStateOverrideDocument.GenerateId(configId, recurrenceDateTime);

        // Assert
        result1.Should().Be(result2);
    }

    [Fact]
    public void GenerateId_DifferentDateTimes_ProduceDifferentIds()
    {
        // Arrange
        var configId = "same-config";
        var dateTime1 = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc);
        var dateTime2 = new DateTime(2026, 1, 2, 9, 0, 0, DateTimeKind.Utc);

        // Act
        var result1 = RecurringTaskStateOverrideDocument.GenerateId(configId, dateTime1);
        var result2 = RecurringTaskStateOverrideDocument.GenerateId(configId, dateTime2);

        // Assert
        result1.Should().NotBe(result2);
    }

    [Fact]
    public void GenerateId_DifferentConfigIds_ProduceDifferentIds()
    {
        // Arrange
        var configId1 = "config-1";
        var configId2 = "config-2";
        var sameDateTime = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc);

        // Act
        var result1 = RecurringTaskStateOverrideDocument.GenerateId(configId1, sameDateTime);
        var result2 = RecurringTaskStateOverrideDocument.GenerateId(configId2, sameDateTime);

        // Assert
        result1.Should().NotBe(result2);
    }

    [Fact]
    public void GenerateId_WithLocalDateTime_ThrowsArgumentException()
    {
        // Arrange
        var configId = "config-123";
        var localDateTime = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Local);

        // Act
        var act = () => RecurringTaskStateOverrideDocument.GenerateId(configId, localDateTime);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("recurrenceDateAndTime")
            .WithMessage("*must be UTC*");
    }

    [Fact]
    public void GenerateId_WithUnspecifiedDateTime_ThrowsArgumentException()
    {
        // Arrange
        var configId = "config-123";
        var unspecifiedDateTime = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Unspecified);

        // Act
        var act = () => RecurringTaskStateOverrideDocument.GenerateId(configId, unspecifiedDateTime);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("recurrenceDateAndTime")
            .WithMessage("*must be UTC*");
    }

    #endregion

    #region RecurringTaskConfigDocument Default Values Tests

    [Fact]
    public void RecurringTaskConfigDocument_HasCorrectDefaultType()
    {
        // Act
        var config = new RecurringTaskConfigDocument();

        // Assert
        config.Type.Should().Be("RecurringTaskConfig");
    }

    [Fact]
    public void RecurringTaskConfigDocument_DefaultValuesAreEmpty()
    {
        // Act
        var config = new RecurringTaskConfigDocument();

        // Assert
        config.Id.Should().BeEmpty();
        config.UserId.Should().BeEmpty();
        config.Text.Should().BeEmpty();
        config.Rrule.Should().BeEmpty();
    }

    #endregion

    #region RecurringTaskStateOverrideDocument Default Values Tests

    [Fact]
    public void RecurringTaskStateOverrideDocument_HasCorrectDefaultType()
    {
        // Act
        var stateOverride = new RecurringTaskStateOverrideDocument();

        // Assert
        stateOverride.Type.Should().Be("RecurringTaskStateOverride");
    }

    [Fact]
    public void RecurringTaskStateOverrideDocument_DefaultValuesAreEmpty()
    {
        // Act
        var stateOverride = new RecurringTaskStateOverrideDocument();

        // Assert
        stateOverride.Id.Should().BeEmpty();
        stateOverride.UserId.Should().BeEmpty();
        stateOverride.ConfigId.Should().BeEmpty();
        stateOverride.State.Should().BeEmpty();
    }

    #endregion

    #region TaskState Extended Constants Tests

    [Fact]
    public void TaskState_Scheduled_HasCorrectValue()
    {
        // Assert
        TaskState.Scheduled.Should().Be("Scheduled");
    }

    [Fact]
    public void TaskState_Skipped_HasCorrectValue()
    {
        // Assert
        TaskState.Skipped.Should().Be("Skipped");
    }

    [Fact]
    public void TaskState_RecurringTaskStates_ContainsAllExpectedStates()
    {
        // Assert
        TaskState.RecurringTaskStates.Should().Contain("Scheduled");
        TaskState.RecurringTaskStates.Should().Contain("OnDeck");
        TaskState.RecurringTaskStates.Should().Contain("InProgress");
        TaskState.RecurringTaskStates.Should().Contain("Completed");
        TaskState.RecurringTaskStates.Should().Contain("Skipped");
        TaskState.RecurringTaskStates.Should().HaveCount(5);
    }

    [Fact]
    public void TaskState_RecurringTaskStates_DoesNotContainDeleted()
    {
        // Assert - Deleted does NOT apply to recurring task instances (FR85)
        TaskState.RecurringTaskStates.Should().NotContain("Deleted");
    }

    [Theory]
    [InlineData("Scheduled", true)]
    [InlineData("OnDeck", true)]
    [InlineData("InProgress", true)]
    [InlineData("Completed", true)]
    [InlineData("Skipped", true)]
    [InlineData("Deleted", false)]
    [InlineData("InvalidState", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void TaskState_IsValidRecurringTaskState_ValidatesCorrectly(string? state, bool expected)
    {
        // Act
        var result = TaskState.IsValidRecurringTaskState(state);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void TaskState_AllStates_StillContainsOriginalFourStates()
    {
        // Assert - Original states unchanged
        TaskState.AllStates.Should().Contain("OnDeck");
        TaskState.AllStates.Should().Contain("InProgress");
        TaskState.AllStates.Should().Contain("Completed");
        TaskState.AllStates.Should().Contain("Deleted");
        TaskState.AllStates.Should().HaveCount(4);
    }

    [Fact]
    public void TaskState_IsValid_StillOnlyValidatesOriginalStates()
    {
        // Assert - Original validation unchanged
        TaskState.IsValid("OnDeck").Should().BeTrue();
        TaskState.IsValid("InProgress").Should().BeTrue();
        TaskState.IsValid("Completed").Should().BeTrue();
        TaskState.IsValid("Deleted").Should().BeTrue();
        TaskState.IsValid("Scheduled").Should().BeFalse(); // New states not in original validation
        TaskState.IsValid("Skipped").Should().BeFalse();
    }

    #endregion
}
