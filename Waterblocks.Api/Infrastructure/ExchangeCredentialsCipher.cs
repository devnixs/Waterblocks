using System.Security.Cryptography;
using System.Text.Json;

namespace Waterblocks.Api.Infrastructure;

/// <summary>
/// The exchange credentials inside a Fireblocks creds blob.
/// </summary>
public sealed record ExchangeCredentials(string ApiKey, string Secret, string? TenantId);

/// <summary>
/// Decrypts exchange credentials in the Fireblocks way: the client encrypts {"apiKey","secret","tenantId"} with the
/// credentials public key, using RSA-OAEP with SHA-256, and sends the Base64 result as creds.
/// </summary>
public interface IExchangeCredentialsCipher
{
    string PublicKeyPem { get; }

    ExchangeCredentials Decrypt(string creds);
}

public sealed class ExchangeCredentialsCipher : IExchangeCredentialsCipher, IDisposable
{
    private const int KeySizeBits = 3072;

    private readonly Lazy<RSA> _key = new(() => RSA.Create(KeySizeBits));
    private readonly object _sync = new();

    public string PublicKeyPem
    {
        get
        {
            lock (_sync)
            {
                return _key.Value.ExportSubjectPublicKeyInfoPem();
            }
        }
    }

    public ExchangeCredentials Decrypt(string creds)
    {
        byte[] cipherText;
        try
        {
            cipherText = Convert.FromBase64String(creds.Trim());
        }
        catch (FormatException)
        {
            throw InvalidCreds();
        }

        byte[] plainText;
        try
        {
            lock (_sync)
            {
                plainText = _key.Value.Decrypt(cipherText, RSAEncryptionPadding.OaepSHA256);
            }
        }
        catch (CryptographicException)
        {
            throw InvalidCreds();
        }

        try
        {
            using var document = JsonDocument.Parse(plainText);
            var apiKey = ReadString(document, "apiKey");
            var secret = ReadString(document, "secret");
            if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(secret))
            {
                throw InvalidCreds();
            }

            return new ExchangeCredentials(apiKey, secret, ReadString(document, "tenantId"));
        }
        catch (JsonException)
        {
            throw InvalidCreds();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plainText);
        }
    }

    public void Dispose()
    {
        if (_key.IsValueCreated)
        {
            _key.Value.Dispose();
        }
    }

    private static string? ReadString(JsonDocument document, string propertyName)
    {
        return document.RootElement.ValueKind == JsonValueKind.Object &&
               document.RootElement.TryGetProperty(propertyName, out var value) &&
               value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static ExchangeOperationException InvalidCreds()
    {
        return ExchangeOperationException.BadRequest(
            "INVALID_CREDENTIALS_BLOB",
            "creds must be the Base64 RSA-OAEP SHA-256 encryption of {\"apiKey\",\"secret\",\"tenantId\"} with the credentials public key");
    }
}
