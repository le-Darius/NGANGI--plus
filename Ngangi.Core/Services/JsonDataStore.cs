using System.Security.Cryptography;
using System.Text.Json;
using Ngangi.Core.Models;

namespace Ngangi.Core.Services;

/// <summary>
/// Sauvegarde locale (hors ligne) dans un fichier, chiffré si un <see cref="IDataCipher"/> est fourni.
///
/// - Sans chiffrement (cipher = null) : JSON lisible, comme avant.
/// - Avec chiffrement : AES-256-GCM. Un ancien fichier JSON en clair est lu normalement,
///   puis chiffré à la première sauvegarde (migration automatique, sans copie en clair conservée).
/// - Écriture sûre : fichier temporaire, vidage sur disque, copie de secours (.bak), puis remplacement.
/// - Un fichier illisible, modifié ou invalide n'est JAMAIS écrasé en silence : il est mis de côté
///   (« .corrupt-… ») et la sauvegarde précédente est restaurée si elle est saine.
/// </summary>
public sealed class JsonDataStore
{
    private const long MaxFileBytes = 64L * 1024 * 1024;

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };
    private static readonly JsonSerializerOptions Compact = new() { WriteIndented = false };

    private readonly string _path;
    private readonly IDataCipher? _cipher;
    private readonly object _lock = new();

    public JsonDataStore(string path, IDataCipher? cipher = null)
    {
        _path = path ?? throw new ArgumentNullException(nameof(path));
        _cipher = cipher;
    }

    /// <summary>Explique pourquoi le dernier chargement a échoué ou a dû utiliser la sauvegarde (null si tout allait bien).</summary>
    public string? LastProblem { get; private set; }

    /// <summary>Retourne les données, ou null s'il n'y a pas de fichier (ou si rien de sain n'a pu être relu).</summary>
    public NjangiData? Load()
    {
        lock (_lock)
        {
            LastProblem = null;
            if (!File.Exists(_path)) return null;

            var data = TryRead(_path, out var problem);
            if (data != null) return data;

            LastProblem = problem;
            Quarantine(_path);                                   // on garde le fichier abîmé pour analyse

            var backup = TryRead(_path + ".bak", out _);
            if (backup == null) return null;

            LastProblem = (problem ?? "Fichier illisible.") + " La sauvegarde précédente a été restaurée.";
            SaveCore(backup);
            return backup;
        }
    }

    public void Save(NjangiData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        lock (_lock) SaveCore(data);
    }

    public void Delete()
    {
        lock (_lock)
        {
            foreach (var p in new[] { _path, _path + ".bak", _path + ".tmp" })
                if (File.Exists(p)) File.Delete(p);
        }
    }

    // ---------------------------------------------------------------- interne

    private NjangiData? TryRead(string path, out string? problem)
    {
        problem = null;
        if (!File.Exists(path)) { problem = "Fichier absent."; return null; }
        if (new FileInfo(path).Length > MaxFileBytes) { problem = "Fichier trop volumineux."; return null; }

        // Les erreurs d'accès (fichier verrouillé, droits) ne sont PAS capturées : mieux vaut un arrêt net
        // que de prendre un fichier sain pour un fichier corrompu.
        var bytes = File.ReadAllBytes(path);
        try
        {
            if (_cipher != null && _cipher.IsEncrypted(bytes)) bytes = _cipher.Decrypt(bytes);
            var data = JsonSerializer.Deserialize<NjangiData>(bytes, Compact);
            if (data == null) { problem = "Fichier vide."; return null; }

            var issues = DataValidator.Validate(data);
            if (issues.Count > 0) { problem = "Données invalides : " + issues[0]; return null; }
            return data;
        }
        catch (CryptographicException)
        {
            problem = "Fichier modifié ou clé de chiffrement différente.";
            return null;
        }
        catch (JsonException)
        {
            problem = "Fichier illisible.";
            return null;
        }
    }

    private void SaveCore(NjangiData data)
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var plain = JsonSerializer.SerializeToUtf8Bytes(data, _cipher == null ? Indented : Compact);
        byte[] bytes;
        if (_cipher == null) bytes = plain;
        else
        {
            bytes = _cipher.Encrypt(plain);
            CryptographicOperations.ZeroMemory(plain);
        }

        var tmp = _path + ".tmp";
        using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            fs.Write(bytes, 0, bytes.Length);
            fs.Flush(true);                                      // jusqu'au disque, pas seulement dans le cache du système
        }

        BackupCurrent();
        File.Move(tmp, _path, overwrite: true);
    }

    /// <summary>Copie de secours du fichier actuel. Un ancien fichier en clair n'est jamais copié.</summary>
    private void BackupCurrent()
    {
        if (!File.Exists(_path)) return;
        var bak = _path + ".bak";
        var current = File.ReadAllBytes(_path);
        if (_cipher == null || _cipher.IsEncrypted(current)) File.WriteAllBytes(bak, current);
        else if (File.Exists(bak)) File.Delete(bak);
    }

    private static void Quarantine(string path)
    {
        if (!File.Exists(path)) return;
        var target = $"{path}.corrupt-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
        File.Move(path, target, overwrite: true);
    }
}
