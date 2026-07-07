// -----------------------------------------------------------------------------
// <copyright file="Credentials.cs" company="Chronos Platform">
//   Copyright (c) Chronos Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

#pragma warning disable CA1303 // Do not pass literals as localized parameters

namespace Chronos.Core.Engine.Core;

using Chronos.Core.Engine.Core.Exceptions;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Manages cloud credentials (username, password, instance API key).
/// Credentials are stored in memory encrypted; they are never persisted to disk.
/// </summary>
internal static class Credentials
{
    private static byte[]? _encryptedCredentials;
    private static string? _username;
    private static string? _password;
    private static string? _instanceApiKey;
    private static bool _isEncrypted;

    /// <summary>Gets the username. Throws if not set.</summary>
    public static string Username => _username ?? throw new InvalidOperationException("Credentials not set.");

    /// <summary>Gets the password. Throws if not set.</summary>
    public static string Password => _password ?? throw new InvalidOperationException("Credentials not set.");

    /// <summary>Gets the instance API key. Throws if not set.</summary>
    public static string InstanceApiKey =>
        _instanceApiKey ?? throw new InvalidOperationException("Credentials not set.");

    /// <summary>Returns true if credentials are available (set).</summary>
    public static bool IsAvailable => _encryptedCredentials != null && _isEncrypted;

    /// <summary>Sets credentials directly (e.g., from --auth flag).</summary>
    /// <param name="username">Cloud username.</param>
    /// <param name="password">Cloud password.</param>
    /// <param name="apiKey">Instance API key.</param>
    public static void SetCredentials(string username, string password, string apiKey)
    {
        _username = username;
        _password = password;
        _instanceApiKey = apiKey;
        EncryptCredentials();
    }

    /// <summary>Ensures credentials are available. If not, prompts the user.</summary>
    public static void EnsureAvailable()
    {
        if (IsAvailable)
        {
            return;
        }

        PromptForCredentials();
    }

    /// <summary>Prompts the user for credentials interactively.</summary>
    public static void PromptForCredentials()
    {
        Console.WriteLine("=== Engine Authentication ===");
        Console.Write("Cloud Username: ");
        string? username = Console.ReadLine();
        Console.Write("Cloud Password: ");
        string password = ReadPasswordFromConsole();
        Console.Write("Instance API Key: ");
        string instanceApiKey = ReadPasswordFromConsole();

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password) ||
            string.IsNullOrWhiteSpace(instanceApiKey))
        {
            throw new EngineException("All credentials are required.");
        }

        SetCredentials(username, password, instanceApiKey);
        Console.WriteLine("Credentials stored securely in memory.");
    }

    private static void EncryptCredentials()
    {
        if (string.IsNullOrEmpty(_instanceApiKey))
        {
            throw new InvalidOperationException("Instance API key not available for encryption.");
        }

        // Generate a random 32-byte salt
        byte[] salt = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(salt);
        }

        string credentialsString = $"{_username}:{_password}:{_instanceApiKey}";
        byte[] credentialsBytes = Encoding.UTF8.GetBytes(credentialsString);

        // Use 600,000 iterations as per NIST recommendation (PBKDF2-HMAC-SHA256)
        byte[] key = Rfc2898DeriveBytes.Pbkdf2(
            _instanceApiKey,
            salt,
            600000,
            HashAlgorithmName.SHA256,
            32);

        using Aes aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();

        using ICryptoTransform encryptor = aes.CreateEncryptor();
        byte[] iv = aes.IV;
        byte[] encrypted = encryptor.TransformFinalBlock(credentialsBytes, 0, credentialsBytes.Length);

        // Blob format: salt (32) + iv (16) + ciphertext
        _encryptedCredentials = new byte[salt.Length + iv.Length + encrypted.Length];
        Buffer.BlockCopy(salt, 0, _encryptedCredentials, 0, salt.Length);
        Buffer.BlockCopy(iv, 0, _encryptedCredentials, salt.Length, iv.Length);
        Buffer.BlockCopy(encrypted, 0, _encryptedCredentials, salt.Length + iv.Length, encrypted.Length);
        _isEncrypted = true;
    }

    private static void DecryptCredentials()
    {
        if (_encryptedCredentials == null || !_isEncrypted)
        {
            throw new InvalidOperationException("Credentials not encrypted.");
        }

        if (string.IsNullOrEmpty(_instanceApiKey))
        {
            throw new InvalidOperationException("Instance API key not available for decryption.");
        }

        // Read salt (first 32 bytes)
        byte[] salt = new byte[32];
        Buffer.BlockCopy(_encryptedCredentials, 0, salt, 0, salt.Length);

        // Read IV (next 16 bytes)
        byte[] iv = new byte[16];
        Buffer.BlockCopy(_encryptedCredentials, salt.Length, iv, 0, iv.Length);

        // Read ciphertext (remaining bytes)
        byte[] encrypted = new byte[_encryptedCredentials.Length - salt.Length - iv.Length];
        Buffer.BlockCopy(_encryptedCredentials, salt.Length + iv.Length, encrypted, 0, encrypted.Length);

        byte[] key = Rfc2898DeriveBytes.Pbkdf2(
            _instanceApiKey,
            salt,
            600000,
            HashAlgorithmName.SHA256,
            32);

        using Aes aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;

        using ICryptoTransform decryptor = aes.CreateDecryptor();
        byte[] decrypted = decryptor.TransformFinalBlock(encrypted, 0, encrypted.Length);
        string credentialsString = Encoding.UTF8.GetString(decrypted);

        string[] parts = credentialsString.Split(':');
        if (parts.Length != 3)
        {
            throw new InvalidOperationException("Invalid credentials format.");
        }

        _username = parts[0];
        _password = parts[1];
        _instanceApiKey = parts[2];
    }

    private static string ReadPasswordFromConsole()
    {
        StringBuilder password = new StringBuilder();
        ConsoleKeyInfo keyInfo;

        do
        {
            keyInfo = Console.ReadKey(true);
            if (keyInfo.Key == ConsoleKey.Backspace && password.Length > 0)
            {
                password.Remove(password.Length - 1, 1);
                Console.Write("\b \b");
            }
            else if (keyInfo.Key != ConsoleKey.Enter && keyInfo.Key != ConsoleKey.Backspace)
            {
                password.Append(keyInfo.KeyChar);
                Console.Write("*");
            }
        } while (keyInfo.Key != ConsoleKey.Enter);

        Console.WriteLine();
        return password.ToString();
    }
}

#pragma warning restore CA1303
