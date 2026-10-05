using Microsoft.AspNetCore.WebUtilities;
using NewDynamicFormGenAPI.Models.Interfaces;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace NewDynamicFormGenAPI.Models.Services;

public class PublicIdEncoder: IPublicIdEncoder
{
    public const string SecretKeySettingName = "PublicLink:SecretKey";
    private const int MinSecretKeyLength = 32;
    private const int BlockSizeInBytes = 16;
    private const int IdSizeInBytes = 4;
    private const int PublicIdLength = 22;
    private static readonly byte[] CheckBytes = Encoding.ASCII.GetBytes("FORMGENLINK1");
    private readonly byte[] _aesKey;

    public PublicIdEncoder(IConfiguration configuration)
    {
        var lstrSecretKey = configuration[SecretKeySettingName];

        if (string.IsNullOrWhiteSpace(lstrSecretKey) || lstrSecretKey.Length < MinSecretKeyLength)
        {
            throw new InvalidOperationException(
                $"The '{SecretKeySettingName}' setting is missing or shorter than {MinSecretKeyLength} characters. " +
                "Set a long random value in configuration (environment variable 'PublicLink__SecretKey'). " +
                "Keep it the same on every server and do not change it, or shared fill links will stop working.");
        }

        // SHA-256 turns a secret of any length into the 32 bytes AES-256 needs.
        _aesKey = SHA256.HashData(Encoding.UTF8.GetBytes(lstrSecretKey));
    }
    public string Encode(int aNumFormVersionId)
    {
        if (aNumFormVersionId <= 0)
            throw new ArgumentOutOfRangeException(nameof(aNumFormVersionId), "The form version ID must be greater than zero.");

        var larrPlainBlock = new byte[BlockSizeInBytes];
        BinaryPrimitives.WriteInt32BigEndian(larrPlainBlock.AsSpan(0, IdSizeInBytes), aNumFormVersionId);
        CheckBytes.CopyTo(larrPlainBlock, IdSizeInBytes);

        using var lobjAes = Aes.Create();
        lobjAes.Key = _aesKey;

        // The block is exactly one AES block, so no padding is needed.
        var larrCipherBlock = lobjAes.EncryptEcb(larrPlainBlock, PaddingMode.None);

        return WebEncoders.Base64UrlEncode(larrCipherBlock);
    }

    /// <inheritdoc />
    public bool TryDecode(string? aStrPublicId, out int aNumFormVersionId)
    {
        aNumFormVersionId = 0;
        if (string.IsNullOrEmpty(aStrPublicId) || aStrPublicId.Length != PublicIdLength)
            return false;
        byte[] larrCipherBlock;
        try
        {
            larrCipherBlock = WebEncoders.Base64UrlDecode(aStrPublicId);
        }
        catch (FormatException)
        {
            return false;
        }

        if (larrCipherBlock.Length != BlockSizeInBytes)
            return false;

        // Only the exact text produced by Encode is accepted, so one form has one link.
        if (!string.Equals(WebEncoders.Base64UrlEncode(larrCipherBlock), aStrPublicId, StringComparison.Ordinal))
            return false;

        using var lobjAes = Aes.Create();
        lobjAes.Key = _aesKey;

        var larrPlainBlock = lobjAes.DecryptEcb(larrCipherBlock, PaddingMode.None);

        // A changed or made-up value decrypts to random bytes, so the check bytes will not match.
        if (!CryptographicOperations.FixedTimeEquals(larrPlainBlock.AsSpan(IdSizeInBytes), CheckBytes))
            return false;

        var lnumFormVersionId = BinaryPrimitives.ReadInt32BigEndian(larrPlainBlock.AsSpan(0, IdSizeInBytes));
        if (lnumFormVersionId <= 0)
            return false;

        aNumFormVersionId = lnumFormVersionId;
        return true;
    }

}
