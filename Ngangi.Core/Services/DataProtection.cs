using System.Security.Cryptography;

namespace Ngangi.Core.Services;

/// <summary>Fournit la clé AES-256 (32 octets). L'implémentation décide où la clé est gardée (coffre du système).</summary>
public interface IKeyProvider
{
    byte[] GetOrCreateKey();
}

/// <summary>Chiffre et déchiffre les données avant leur écriture sur le disque.</summary>
public interface IDataCipher
{
    byte[] Encrypt(byte[] plain);
    byte[] Decrypt(byte[] data);
    bool IsEncrypted(byte[] data);
}

/// <summary>
/// Chiffrement authentifié AES-256-GCM : confidentialité ET détection de toute modification du fichier.
/// Format du fichier : « NGC1 » (4 octets, aussi authentifiés) + nonce (12) + tag (16) + données chiffrées.
/// Un nonce aléatoire neuf est tiré à chaque écriture.
/// Ne dépend d'aucune bibliothèque MAUI : seule la clé vient de l'extérieur (IKeyProvider).
/// </summary>
public sealed class AesGcmCipher : IDataCipher
{
    private static readonly byte[] Magic = { (byte)'N', (byte)'G', (byte)'C', 1 };
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int KeySize = 32;
    private const int HeaderSize = 4 + NonceSize + TagSize;

    private readonly IKeyProvider _keys;

    public AesGcmCipher(IKeyProvider keys) =>
        _keys = keys ?? throw new ArgumentNullException(nameof(keys));

    public bool IsEncrypted(byte[] data) =>
        data != null && data.Length >= HeaderSize && data.AsSpan(0, Magic.Length).SequenceEqual(Magic);

    public byte[] Encrypt(byte[] plain)
    {
        ArgumentNullException.ThrowIfNull(plain);
        var key = GetKey();
        try
        {
            var nonce = RandomNumberGenerator.GetBytes(NonceSize);
            var cipher = new byte[plain.Length];
            var tag = new byte[TagSize];
            using (var aes = new AesGcm(key, TagSize))
                aes.Encrypt(nonce, plain, cipher, tag, Magic);

            var result = new byte[HeaderSize + cipher.Length];
            Magic.CopyTo(result, 0);
            nonce.CopyTo(result, Magic.Length);
            tag.CopyTo(result, Magic.Length + NonceSize);
            cipher.CopyTo(result, HeaderSize);
            return result;
        }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    /// <summary>Lève CryptographicException si le fichier est modifié ou si la clé est mauvaise.</summary>
    public byte[] Decrypt(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (!IsEncrypted(data)) throw new CryptographicException("Format de fichier chiffré inconnu.");

        var key = GetKey();
        try
        {
            var nonce = data.AsSpan(Magic.Length, NonceSize);
            var tag = data.AsSpan(Magic.Length + NonceSize, TagSize);
            var cipher = data.AsSpan(HeaderSize);
            var plain = new byte[cipher.Length];
            using (var aes = new AesGcm(key, TagSize))
                aes.Decrypt(nonce, cipher, tag, plain, Magic);
            return plain;
        }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    private byte[] GetKey()
    {
        var key = _keys.GetOrCreateKey();
        if (key == null || key.Length != KeySize)
            throw new CryptographicException("La clé de chiffrement doit faire 32 octets.");
        return key;
    }
}
