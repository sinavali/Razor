// -----------------------------------------------------------------------------
// <copyright file="ICloudConnector.cs" company="Chronos Platform">
//   Copyright (c) Chronos Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Chronos.Core.Engine.Communication;

/// <summary>
/// Manages the WebSocket connection to the cloud with infinite retry on failure.
/// </summary>
internal interface ICloudConnector
{
    /// <summary>Gets a value indicating whether the connection is established.</summary>
    bool IsConnected { get; }

    /// <summary>Gets the current session identifier.</summary>
    string? SessionId { get; }

    /// <summary>Runs the main connection loop with infinite retry.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task RunAsync(CancellationToken cancellationToken);

    /// <summary>Disconnects gracefully.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task DisconnectAsync(CancellationToken cancellationToken);

    /// <summary>Sends a message to the cloud.</summary>
    /// <param name="message">The message to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task SendAsync(CloudMessage message, CancellationToken cancellationToken);

    /// <summary>Sends a binary file to the cloud using chunked transfer.</summary>
    /// <param name="filePath">Path to the file to send.</param>
    /// <param name="contentType">MIME type (e.g., "application/octet-stream").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the file is fully sent.</returns>
    Task SendBinaryAsync(string filePath, string contentType, CancellationToken cancellationToken);

    /// <summary>Sends the extension manifest to the cloud.</summary>
    /// <param name="manifest">The manifest object (from IExtensionManager.GetManifestAsync).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendExtensionManifestAsync(object manifest, CancellationToken cancellationToken);
}
