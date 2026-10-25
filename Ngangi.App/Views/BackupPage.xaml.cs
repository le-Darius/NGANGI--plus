using System.Security.Cryptography;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Storage;
using Ngangi.Core.Services;
using Ngangi.Core.Models;

namespace Ngangi.App.Views;

public partial class BackupPage : ContentPage
{
    private readonly NjangiService _service;
    public BackupPage(NjangiService service) { InitializeComponent(); _service = service; }

    private async void OnBackClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync("..");

    private async void OnBackupClicked(object sender, EventArgs e)
    {
        var password = BackupPassword.Text ?? "";
        if (password.Length < 8 || password.Length > 128 || password != BackupPasswordConfirm.Text)
        { await DisplayAlert("Vérifier le mot de passe", "Saisissez deux fois le même mot de passe (8 à 128 caractères).", "OK"); return; }
        string? path = null;
        try
        {
            var bytes = BackupArchive.Create(_service.Data, password);
            path = Path.Combine(FileSystem.CacheDirectory, $"ngangi-{DateTime.UtcNow:yyyyMMdd-HHmmss}.ngangi");
            await File.WriteAllBytesAsync(path, bytes);
            await Share.Default.RequestAsync(new ShareFileRequest { Title = "Sauvegarde Ngangi+", File = new ShareFile(path, "application/octet-stream") });
            Preferences.Default.Set("ngangi.last-backup-utc", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException or IOException or UnauthorizedAccessException)
        { await DisplayAlert("Sauvegarde impossible", ex.Message, "OK"); }
        finally
        {
            if (path != null) try { if (File.Exists(path)) File.Delete(path); } catch (IOException) { }
            BackupPassword.Text = BackupPasswordConfirm.Text = "";
        }
    }

    private async void OnRestoreClicked(object sender, EventArgs e)
    {
        FileResult? picked;
        try { picked = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Choisir une sauvegarde Ngangi+" }); }
        catch (Exception ex) { await DisplayAlert("Ouverture impossible", ex.Message, "OK"); return; }
        if (picked == null) return;
        try
        {
            await using var input = await picked.OpenReadAsync();
            if (input.Length > 64L * 1024 * 1024) throw new InvalidDataException("Le fichier est trop volumineux.");
            using var memory = new MemoryStream(); await input.CopyToAsync(memory);
            var metadata = BackupArchive.ReadMetadata(memory.ToArray());
            if (metadata.Version > BackupArchive.CurrentVersion) throw new NotSupportedException("Cette sauvegarde provient d’une version plus récente de Ngangi+.");
            var password = await DisplayPromptAsync("Mot de passe", "Mot de passe de cette sauvegarde", "Valider", "Annuler", "Mot de passe", 128, Keyboard.Default, "");
            if (password == null) return;
            var preview = BackupArchive.Open(memory.ToArray(), password);
            var confirm = await DisplayAlert("Confirmer la restauration",
                $"Sauvegarde du {preview.Metadata.CreatedAt.ToLocalTime():dd/MM/yyyy HH:mm}\nMembres : {preview.Metadata.MemberCount}\nPrêts : {preview.Metadata.LoanCount}\n\nLes données actuelles seront remplacées après création d’une copie de sécurité.", "Remplacer les données", "Annuler");
            if (!confirm) return;
            var safety = BackupArchive.Create(_service.Data, password);
            var safetyPath = Path.Combine(FileSystem.AppDataDirectory, $"avant-restauration-{DateTime.UtcNow:yyyyMMdd-HHmmss}.ngangi");
            await File.WriteAllBytesAsync(safetyPath, safety);
            _service.ReplaceData(preview.Data);
            await DisplayAlert("Restauration terminée", "Les données ont été restaurées. La copie de sécurité est conservée dans le dossier privé de l’application.", "OK");
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or ArgumentException or IOException or CryptographicException)
        { await DisplayAlert("Restauration refusée", ex.Message, "OK"); }
    }
}
