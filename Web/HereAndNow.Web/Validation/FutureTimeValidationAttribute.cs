using System.ComponentModel.DataAnnotations;

namespace HereAndNowService.Validation;

/// <summary>
/// Validates that a DateTime value is in the future.
/// Returns INVALID_SCHEDULED_TIME error code when validation fails.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public class FutureTimeValidationAttribute : ValidationAttribute
{
    /// <summary>
    /// Creates a new FutureTimeValidationAttribute with default error message
    /// </summary>
    public FutureTimeValidationAttribute()
        : base("Scheduled time must be in the future")
    {
    }

    /// <summary>
    /// Validates that the DateTime value is in the future (after UTC now)
    /// </summary>
    /// <param name="value">The DateTime value to validate (null is valid - optional field)</param>
    /// <param name="validationContext">The validation context</param>
    /// <returns>ValidationResult.Success if valid or value is null, otherwise a validation error</returns>
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        // Null is valid - the scheduledTime is optional
        if (value is null)
        {
            return ValidationResult.Success;
        }

        if (value is DateTime dateTime)
        {
            // Ensure we're comparing UTC times
            // Use ToUniversalTime() for proper conversion (not SpecifyKind which only relabels)
            var utcDateTime = dateTime.Kind == DateTimeKind.Utc
                ? dateTime
                : dateTime.ToUniversalTime();

            if (utcDateTime <= DateTime.UtcNow)
            {
                return new ValidationResult(
                    FormatErrorMessage(validationContext.DisplayName),
                    new[] { validationContext.MemberName ?? string.Empty });
            }

            return ValidationResult.Success;
        }

        return new ValidationResult(
            "Value must be a DateTime",
            new[] { validationContext.MemberName ?? string.Empty });
    }
}
