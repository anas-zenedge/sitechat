using SiteChat.Backend.Api.Models;

namespace SiteChat.Backend.Api.Services;

/// <summary>
/// Provides platform-wide white-label configuration operations.
/// </summary>
public interface IPlatformConfigurationService
{
    /// <summary>
    /// Gets the current white-label configuration.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A white-label configuration.</returns>
    Task<PlatformWhiteLabelConfig> GetAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Updates the white-label configuration.
    /// </summary>
    /// <param name="request">The white-label configuration.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>An updated white-label configuration.</returns>
    Task<PlatformWhiteLabelConfig> UpdateAsync(PlatformWhiteLabelConfig request, CancellationToken cancellationToken);

    /// <summary>
    /// Resets the white-label configuration to defaults.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A reset white-label configuration.</returns>
    Task<PlatformWhiteLabelConfig> ResetAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Implements platform-wide white-label configuration behavior.
/// </summary>
public sealed class PlatformConfigurationService(IPlatformConfigurationRepository repository) : IPlatformConfigurationService
{
    private readonly IPlatformConfigurationRepository _repository = repository ?? throw new ArgumentNullException(nameof(repository));

    /// <inheritdoc />
    public async Task<PlatformWhiteLabelConfig> GetAsync(CancellationToken cancellationToken) =>
        await _repository.GetPlatformWhiteLabelConfigAsync(cancellationToken).ConfigureAwait(false) ?? new PlatformWhiteLabelConfig();

    /// <inheritdoc />
    public Task<PlatformWhiteLabelConfig> UpdateAsync(PlatformWhiteLabelConfig request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _repository.UpdatePlatformWhiteLabelConfigAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    public Task<PlatformWhiteLabelConfig> ResetAsync(CancellationToken cancellationToken) =>
        _repository.UpdatePlatformWhiteLabelConfigAsync(new PlatformWhiteLabelConfig(), cancellationToken);
}
