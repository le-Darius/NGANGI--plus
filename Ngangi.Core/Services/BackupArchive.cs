using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ngangi.Core.Models;

namespace Ngangi.Core.Services;

/// <summary>Archive .ngangi portable, chiffrée par mot de passe et indépendante du Keystore.</summary>
public static class BackupArchive
{
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("NGNG");
    public const int CurrentVersion = 1;
    public const int CurrentIterations = 600_000;
    private const int SaltLength = 16, NonceLength = 12, TagLength = 16, FixedHeaderLength = 4 + 4 + 8 + 4 + 4 + 4 + SaltLength + NonceLength + TagLength;
    private const int MaxArchiveBytes = 64 * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = false };

    public sealed record Metadata(int Version, DateTimeOffset CreatedAt, int MemberCount, int LoanCount);
    public sealed record RestorePreview(Metadata Metadata, NjangiData Data);

    public static byte[] Create(NjangiData data, string password, DateTimeOffset? createdAt = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        ValidatePassword(password);
        var issues = DataValidator.Validate(data);
        if (issues.Count > 0) throw new InvalidDataException("Données invalides : " + issues[0]);
        var metadata = new Metadata(CurrentVersion, createdAt ?? DateTimeOffset.UtcNow, data.Members.Count, data.Loans.Count);
        var salt = RandomNumberGenerator.GetBytes(SaltLength);
        var nonce = RandomNumberGenerator.GetBytes(NonceLength);
        var header = BuildHeader(metadata, CurrentIterations, salt, nonce);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, CurrentIterations, HashAlgorithmName.SHA256, 32);
        var plain = JsonSerializer.SerializeToUtf8Bytes(data, JsonOptions);
        var cipher = new byte[plain.Length]; var tag = new byte[TagLength];
        try { using var aes = new AesGcm(key, TagLength); aes.Encrypt(nonce, plain, cipher, tag, header); }
        finally { CryptographicOperations.ZeroMemory(key); CryptographicOperations.ZeroMemory(plain); }
        tag.CopyTo(header, header.Length - TagLength);
        var result = new byte[header.Length + cipher.Length];
        Buffer.BlockCopy(header, 0, result, 0, header.Length); Buffer.BlockCopy(cipher, 0, result, header.Length, cipher.Length);
        return result;
    }

    public static Metadata ReadMetadata(ReadOnlySpan<byte> archive)
    {
        var parsed = ParseHeader(archive);
        return parsed.Metadata;
    }

    public static RestorePreview Open(ReadOnlySpan<byte> archive, string password)
    {
        ValidatePassword(password);
        if (archive.Length > MaxArchiveBytes || archive.Length < FixedHeaderLength) throw new InvalidDataException("Fichier de sauvegarde invalide.");
        var parsed = ParseHeader(archive);
        if (parsed.Metadata.Version > CurrentVersion) throw new NotSupportedException("Cette sauvegarde provient d’une version plus récente de Ngangi+.");
        if (parsed.Iterations < 100_000 || parsed.Iterations > 2_000_000) throw new InvalidDataException("Paramètres de chiffrement invalides.");
        var header = archive[..FixedHeaderLength].ToArray();
        var tag = header[^TagLength..]; Array.Clear(header, header.Length - TagLength, TagLength);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, parsed.Salt, parsed.Iterations, HashAlgorithmName.SHA256, 32);
        var cipher = archive[FixedHeaderLength..]; var plain = new byte[cipher.Length];
        try
        {
            using var aes = new AesGcm(key, TagLength); aes.Decrypt(parsed.Nonce, cipher, tag, plain, header);
            var data = JsonSerializer.Deserialize<NjangiData>(plain, JsonOptions) ?? throw new InvalidDataException("Sauvegarde vide.");
            var issues = DataValidator.Validate(data);
            if (issues.Count > 0) throw new InvalidDataException("Données invalides : " + issues[0]);
            if (data.Members.Count != parsed.Metadata.MemberCount || data.Loans.Count != parsed.Metadata.LoanCount)
                throw new InvalidDataException("Les métadonnées ne correspondent pas aux données restaurées.");
            return new RestorePreview(parsed.Metadata, data);
        }
        catch (CryptographicException ex) { throw new InvalidDataException("Mot de passe incorrect ou fichier modifié.", ex); }
        catch (JsonException ex) { throw new InvalidDataException("Contenu de sauvegarde invalide.", ex); }
        finally { CryptographicOperations.ZeroMemory(key); CryptographicOperations.ZeroMemory(plain); }
    }

    private sealed record Parsed(Metadata Metadata, int Iterations, byte[] Salt, byte[] Nonce);
    private static byte[] BuildHeader(Metadata m, int iterations, byte[] salt, byte[] nonce)
    {
        var h = new byte[FixedHeaderLength]; int p = 0;
        Magic.CopyTo(h, p); p += 4; BitConverter.GetBytes(m.Version).CopyTo(h, p); p += 4;
        BitConverter.GetBytes(m.CreatedAt.ToUnixTimeSeconds()).CopyTo(h, p); p += 8;
        BitConverter.GetBytes(m.MemberCount).CopyTo(h, p); p += 4; BitConverter.GetBytes(m.LoanCount).CopyTo(h, p); p += 4;
        BitConverter.GetBytes(iterations).CopyTo(h, p); p += 4; salt.CopyTo(h, p); p += SaltLength; nonce.CopyTo(h, p);
        // Header layout includes an extra iterations field; account for it in the actual size.
        return h;
    }
    private static Parsed ParseHeader(ReadOnlySpan<byte> a)
    {
        const int headerSize = 4 + 4 + 8 + 4 + 4 + 4 + SaltLength + NonceLength + TagLength;
        if (a.Length < headerSize || a.Length > MaxArchiveBytes || !a[..4].SequenceEqual(Magic)) throw new InvalidDataException("Fichier .ngangi invalide.");
        int p = 4; int version = BitConverter.ToInt32(a[p..]); p += 4; long unix = BitConverter.ToInt64(a[p..]); p += 8;
        int members = BitConverter.ToInt32(a[p..]); p += 4; int loans = BitConverter.ToInt32(a[p..]); p += 4; int iterations = BitConverter.ToInt32(a[p..]); p += 4;
        if (version < 1 || members < 0 || loans < 0 || members > 100_000 || loans > 100_000) throw new InvalidDataException("En-tête de sauvegarde invalide.");
        DateTimeOffset created; try { created = DateTimeOffset.FromUnixTimeSeconds(unix); } catch (ArgumentOutOfRangeException ex) { throw new InvalidDataException("Date de sauvegarde invalide.", ex); }
        var salt = a.Slice(p, SaltLength).ToArray(); p += SaltLength; var nonce = a.Slice(p, NonceLength).ToArray();
        return new Parsed(new Metadata(version, created, members, loans), iterations, salt, nonce);
    }
    private static void ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8 || password.Length > 128)
            throw new ArgumentException("Le mot de passe de sauvegarde doit contenir entre 8 et 128 caractères.", nameof(password));
    }
}
