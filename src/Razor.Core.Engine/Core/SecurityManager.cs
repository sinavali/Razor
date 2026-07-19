using Microsoft.Extensions.Logging;
using Razor.Core.Engine.Core.Exceptions;
using System.Security.Cryptography;
using System.Text;
using System.Reflection;

namespace Razor.Core.Engine.Core;

/// <summary>
/// Manages encryption, signing, and anti‑tampering.
/// </summary>
internal interface ISecurityManager
{
    /// <summary>Gets the ephemeral public key for ECDH.</summary>
    string GetPublicKey();

    /// <summary>Sets the cloud's public key.</summary>
    void SetCloudPublicKey(string cloudPublicKey);

    /// <summary>Sets the nonce for key derivation.</summary>
    void SetNonce(string nonce);

    /// <summary>Derives the shared secret and session key.</summary>
    void DeriveSharedSecret();

    /// <summary>Encrypts a plain text message using the session key.</summary>
    string EncryptMessage(string plainText);

    /// <summary>Decrypts a cipher text using the session key.</summary>
    string DecryptMessage(string cipherText);

    /// <summary>Generates a signed challenge for AuthConfirm.</summary>
    string GenerateChallenge(string nonce);

    /// <summary>Verifies the engine binary integrity.</summary>
    bool VerifyIntegrity();

    /// <summary>Detects if a debugger is attached.</summary>
    bool IsDebuggerAttached();

    /// <summary>Increments the sequence number and returns it.</summary>
    ulong GetNextSequenceNumber();
}

/// <summary>
/// Default implementation.
/// </summary>
internal sealed class SecurityManager : ISecurityManager
{
    private readonly ECDiffieHellman _ecdh;
    private string? _cloudPublicKey;
    private string? _nonce;
    private byte[]? _sharedSecret;
    private byte[]? _sessionKey;
    private ulong _sequenceNumber;
    private ulong _lastAcceptedSequence;
    private readonly object _lock = new();
    private readonly ILogger<SecurityManager>? _logger;

    private static readonly Action<ILogger, ulong, ulong, Exception?> _logRejectedMessage =
        LoggerMessage.Define<ulong, ulong>(LogLevel.Warning, 0, "Rejected replayed or out-of-order message: sequence {Sequence} is not greater than last accepted {LastAccepted}.");

    /// <summary>Initializes a new security manager.</summary>
    public SecurityManager(ILogger<SecurityManager>? logger)
    {
        _ecdh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        _logger = logger;
    }

    /// <inheritdoc/>
    public string GetPublicKey()
    {
        lock (_lock)
        {
            byte[] publicKeyBytes = _ecdh.PublicKey.ExportSubjectPublicKeyInfo();
            return Convert.ToBase64String(publicKeyBytes);
        }
    }

    /// <inheritdoc/>
    public void SetCloudPublicKey(string cloudPublicKey)
    {
        lock (_lock)
        {
            _cloudPublicKey = cloudPublicKey;
        }
    }

    /// <inheritdoc/>
    public void SetNonce(string nonce)
    {
        lock (_lock)
        {
            _nonce = nonce;
        }
    }

    /// <inheritdoc/>
    public void DeriveSharedSecret()
    {
        lock (_lock)
        {
            if (string.IsNullOrEmpty(_cloudPublicKey))
            {
                throw new InvalidOperationException("Cloud public key not set.");
            }

            byte[] cloudPublicKeyBytes = Convert.FromBase64String(_cloudPublicKey);

            // Use cross-platform ECDH derivation
            using var cloudKey = ECDiffieHellman.Create();
            cloudKey.ImportSubjectPublicKeyInfo(cloudPublicKeyBytes, out _);

            // Derive shared secret using the engine's private key
            byte[]? sharedSecret = _ecdh.DeriveKeyFromHash(cloudKey.PublicKey, HashAlgorithmName.SHA256, null, null);
            _sharedSecret = sharedSecret;

            // Derive session key using PBKDF2 with the nonce as salt
            byte[] salt = string.IsNullOrEmpty(_nonce) ? new byte[32] : Encoding.UTF8.GetBytes(_nonce);
            _sessionKey = Rfc2898DeriveBytes.Pbkdf2(_sharedSecret, salt, 100000, HashAlgorithmName.SHA256, 32);
        }
    }

    /// <inheritdoc/>
    public string EncryptMessage(string plainText)
    {
        lock (_lock)
        {
            if (_sessionKey == null)
            {
                throw new InvalidOperationException("Session key not derived.");
            }

            byte[] sequenceBytes = BitConverter.GetBytes(GetNextSequenceNumber());
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] combined = new byte[sequenceBytes.Length + plainBytes.Length];
            Buffer.BlockCopy(sequenceBytes, 0, combined, 0, sequenceBytes.Length);
            Buffer.BlockCopy(plainBytes, 0, combined, sequenceBytes.Length, plainBytes.Length);

            using var gcm = new AesGcm(_sessionKey, 16);
            byte[] nonce = new byte[12];
            RandomNumberGenerator.Fill(nonce);
            byte[] cipherText = new byte[combined.Length];
            byte[] tag = new byte[16];
            gcm.Encrypt(nonce, combined, cipherText, tag);

            // Format: nonce (12) + tag (16) + cipher
            byte[] result = new byte[nonce.Length + tag.Length + cipherText.Length];
            Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
            Buffer.BlockCopy(tag, 0, result, nonce.Length, tag.Length);
            Buffer.BlockCopy(cipherText, 0, result, nonce.Length + tag.Length, cipherText.Length);
            return Convert.ToBase64String(result);
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Decryption enforces strictly-increasing monotonic validation of the 8-byte
    /// sequence number prepended to the plain text. A message whose sequence number
    /// is less than or equal to the last accepted sequence number is treated as a
    /// replay or out-of-order delivery and is rejected with a logged
    /// <see cref="SecurityException"/>. No decrypted output is emitted for rejected
    /// messages.
    /// </remarks>
    public string DecryptMessage(string cipherText)
    {
        lock (_lock)
        {
            if (_sessionKey == null)
            {
                throw new InvalidOperationException("Session key not derived.");
            }

            byte[] data = Convert.FromBase64String(cipherText);
            const int nonceSize = 12;
            const int tagSize = 16;
            if (data.Length < nonceSize + tagSize)
            {
                throw new InvalidOperationException("Invalid cipher text format.");
            }

            byte[] nonce = new byte[nonceSize];
            Buffer.BlockCopy(data, 0, nonce, 0, nonceSize);
            byte[] tag = new byte[tagSize];
            Buffer.BlockCopy(data, nonceSize, tag, 0, tagSize);
            byte[] cipher = new byte[data.Length - nonceSize - tagSize];
            Buffer.BlockCopy(data, nonceSize + tagSize, cipher, 0, cipher.Length);

            byte[] plain = new byte[cipher.Length];
            using var gcm = new AesGcm(_sessionKey, 16);
            gcm.Decrypt(nonce, cipher, tag, plain);

            // Strip sequence number (first 8 bytes)
            ulong sequence = BitConverter.ToUInt64(plain, 0);

            // Enforce strictly-increasing monotonic validation to reject replays
            // and out-of-order messages (Product Model 6.1 replay protection).
            if (sequence <= _lastAcceptedSequence)
            {
                if (_logger != null)
                {
                    _logRejectedMessage(_logger, sequence, _lastAcceptedSequence, null);
                }
                throw new SecurityException(
                    $"Rejected message with sequence {sequence}: strictly-increasing sequence validation failed (last accepted {_lastAcceptedSequence}).");
            }

            _lastAcceptedSequence = sequence;

            byte[] payload = new byte[plain.Length - 8];
            Buffer.BlockCopy(plain, 8, payload, 0, payload.Length);
            return Encoding.UTF8.GetString(payload);
        }
    }

    /// <inheritdoc/>
    public string GenerateChallenge(string nonce)
    {
        // HMAC of nonce with session key
        if (_sessionKey == null)
        {
            throw new InvalidOperationException("Session key not derived.");
        }

        using var hmac = new HMACSHA256(_sessionKey);
        byte[] nonceBytes = Encoding.UTF8.GetBytes(nonce);
        byte[] hash = hmac.ComputeHash(nonceBytes);
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Verifies the integrity of the running engine assembly in a fail-closed manner.
    /// </summary>
    /// <remarks>
    /// In production the entry (engine) assembly is hashed with SHA-256 and compared against a
    /// pinned, documented expected hash. In development or when a debugger is attached the check is
    /// bypassed to allow local iteration. The method is <b>fail-closed</b>: on any verification
    /// failure, missing configuration, or exception it returns <c>false</c> rather than throwing,
    /// so a tampered or unverifiable binary can never be reported as intact.
    /// </remarks>
    /// <returns><c>true</c> only when the assembly hash matches the pinned expected hash; otherwise <c>false</c>.</returns>
    public bool VerifyIntegrity()
    {
        // Skip integrity checks when debugging or in development mode.
        if (IsDebuggerAttached() || RuntimeEnvironment.IsDevelopment)
        {
            return true;
        }

        try
        {
            const string pinnedHash =
                "0000000000000000000000000000000000000000000000000000000000000000";

            Assembly entryAssembly = Assembly.GetEntryAssembly()
                ?? throw new InvalidOperationException("Entry assembly is unavailable.");

            string location = entryAssembly.Location;
            if (string.IsNullOrEmpty(location))
            {
                return false;
            }

            byte[] bytes = File.ReadAllBytes(location);
            byte[] actualHash = SHA256.HashData(bytes);
            string actualHashHex = Convert.ToHexString(actualHash).ToLowerInvariant();

            return string.Equals(actualHashHex, pinnedHash, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            // Fail closed: any error during verification is treated as a tamper/integrity failure.
            return false;
        }
    }

    /// <inheritdoc/>
    public bool IsDebuggerAttached() => System.Diagnostics.Debugger.IsAttached;

    /// <inheritdoc/>
    public ulong GetNextSequenceNumber()
    {
        lock (_lock)
        {
            return ++_sequenceNumber;
        }
    }
}
