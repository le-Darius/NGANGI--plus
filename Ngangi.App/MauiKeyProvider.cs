using System.Security.Cryptography;
using Microsoft.Maui.Storage;
using Ngangi.Core.Services;

namespace Ngangi.App;

/// <summary>
/// Fournit la clé AES-256 des données. La clé est tirée au hasard une seule fois, puis gardée dans le
/// coffre du système : Android Keystore sur Android, SecureStorage sur Windows empaqueté.
/// Sous Windows non empaqueté (dotnet run), SecureStorage n'est pas disponible : la clé est alors
/// protégée par DPAPI, liée au compte Windows de l'utilisateur.
/// La clé n'est jamais écrite en clair sur le disque, ni dans le fichier de données.
/// </summary>
public sealed class MauiKeyProvider : IKeyProvider
{
    private const string KeyName = "ngangi.datakey.v1";
    private const int KeyBytes = 32;

    private readonly object _lock = new();
    private byte[]? _cached;

    public byte[] GetOrCreateKey()
    {
        lock (_lock)
        {
            // Task.Run : l'appel se fait hors du fil de l'interface, donc sans risque de blocage (deadlock).
            _cached ??= Task.Run(LoadOrCreateAsync).GetAwaiter().GetResult();
            return (byte[])_cached.Clone();      // l'appelant efface sa copie après usage
        }
    }

    private static async Task<byte[]> LoadOrCreateAsync()
    {
        try
        {
            var stored = await SecureStorage.Default.GetAsync(KeyName);
            if (!string.IsNullOrEmpty(stored))
            {
                var existing = Convert.FromBase64String(stored);
                if (existing.Length == KeyBytes) return existing;
            }

            var created = RandomNumberGenerator.GetBytes(KeyBytes);
            await SecureStorage.Default.SetAsync(KeyName, Convert.ToBase64String(created));
            return created;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
#if WINDOWS
            System.Diagnostics.Debug.WriteLine($"SecureStorage indisponible ({ex.GetType().Name}) : clé protégée par DPAPI.");
            return LoadOrCreateWithDpapi();
#else
            throw;   // sur Android on ne retombe jamais sur un stockage moins sûr : on s'arrête
#endif
        }
    }

#if WINDOWS
    private static byte[] LoadOrCreateWithDpapi()
    {
        var path = Path.Combine(FileSystem.AppDataDirectory, "ngangi.key");
        if (File.Exists(path))
            return ProtectedData.Unprotect(File.ReadAllBytes(path), null, DataProtectionScope.CurrentUser);

        var key = RandomNumberGenerator.GetBytes(KeyBytes);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, ProtectedData.Protect(key, null, DataProtectionScope.CurrentUser));
        return key;
    }
#endif
}
