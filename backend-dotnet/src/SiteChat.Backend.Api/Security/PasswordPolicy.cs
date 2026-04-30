using Microsoft.Extensions.Options;
using SiteChat.Backend.Api.Configuration;

namespace SiteChat.Backend.Api.Security;

/// <summary>
/// Validates passwords against the configured SiteChat password policy.
/// </summary>
public interface IPasswordPolicy
{
    /// <summary>
    /// Validates the supplied password.
    /// </summary>
    /// <param name="password">The password candidate.</param>
    /// <returns>A validation result with an error message when validation fails.</returns>
    PasswordValidationResult Validate(string? password);
}

/// <summary>
/// Represents the result of password policy validation.
/// </summary>
/// <param name="IsValid"><see langword="true" /> if the password is valid; otherwise, <see langword="false" />.</param>
/// <param name="ErrorMessage">The validation error message.</param>
public sealed record PasswordValidationResult(bool IsValid, string ErrorMessage);

/// <summary>
/// Implements length and complexity validation for user passwords.
/// </summary>
public sealed class PasswordPolicy(IOptions<SiteChatOptions> options) : IPasswordPolicy
{
    private readonly SiteChatOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

    /// <inheritdoc />
    public PasswordValidationResult Validate(string? password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return new PasswordValidationResult(false, "Password is required");
        }

        if (password.Length < _options.PasswordPolicy.MinimumLength)
        {
            return new PasswordValidationResult(false, $"Password must be at least {_options.PasswordPolicy.MinimumLength} characters");
        }

        if (!_options.PasswordPolicy.RequireComplexity)
        {
            return new PasswordValidationResult(true, string.Empty);
        }

        if (!password.Any(char.IsUpper))
        {
            return new PasswordValidationResult(false, "Password must contain at least one uppercase letter");
        }

        if (!password.Any(char.IsLower))
        {
            return new PasswordValidationResult(false, "Password must contain at least one lowercase letter");
        }

        if (!password.Any(char.IsDigit))
        {
            return new PasswordValidationResult(false, "Password must contain at least one digit");
        }

        if (!password.Any(ch => !char.IsLetterOrDigit(ch)))
        {
            return new PasswordValidationResult(false, "Password must contain at least one special character");
        }

        return new PasswordValidationResult(true, string.Empty);
    }
}
