using Microsoft.Maui.Storage;
using Ngangi.App.ViewModels;
using Ngangi.App.Views;
using Ngangi.Core.Models;
using Ngangi.Core.Services;

namespace Ngangi.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        // Données 100 % locales (hors ligne), chiffrées sur le disque (AES-256-GCM).
        // La clé est gardée dans le coffre du système (voir MauiKeyProvider). Un ancien fichier JSON en clair
        // est lu puis chiffré automatiquement à la première sauvegarde.
        builder.Services.AddSingleton<IKeyProvider, MauiKeyProvider>();
        builder.Services.AddSingleton<IDataCipher>(sp => new AesGcmCipher(sp.GetRequiredService<IKeyProvider>()));
        builder.Services.AddSingleton(sp =>
            new JsonDataStore(Path.Combine(FileSystem.AppDataDirectory, "ngangi-data.json"),
                              sp.GetRequiredService<IDataCipher>()));

        builder.Services.AddSingleton(sp =>
        {
            var store = sp.GetRequiredService<JsonDataStore>();
            var loaded = store.Load();
            if (store.LastProblem != null)
                System.Diagnostics.Debug.WriteLine("Données : " + store.LastProblem);

            // Premier lancement (ou aucun fichier sain) : base vide, sans groupe ni compte ni donnée de démonstration.
            // Rien n'est écrit sur le disque tant que l'utilisateur n'a rien enregistré.
            var data = loaded ?? new NjangiData();
            var service = new NjangiService(data, () => AppSettings.Today);

            service.Changed += () =>
            {
                // Une erreur de disque (stockage plein, fichier verrouillé) ne doit pas faire planter l'écran :
                // les données restent en mémoire et la sauvegarde suivante réessaiera.
                try { store.Save(service.Data); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    System.Diagnostics.Debug.WriteLine("Sauvegarde impossible : " + ex.Message);
                }
            };
            return service;
        });

        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<RegisterViewModel>();
        builder.Services.AddTransient<DashboardViewModel>();
        builder.Services.AddTransient<MembersViewModel>();
        builder.Services.AddTransient<AddMemberViewModel>();
        builder.Services.AddTransient<ContributionsViewModel>();
        builder.Services.AddTransient<LoansViewModel>();
        builder.Services.AddTransient<LoanRequestViewModel>();
        builder.Services.AddTransient<ScheduleViewModel>();
        builder.Services.AddTransient<FinesViewModel>();
        builder.Services.AddTransient<ReportViewModel>();
        builder.Services.AddTransient<ProfileViewModel>();
        builder.Services.AddTransient<AboutViewModel>();

        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<RegisterPage>();
        builder.Services.AddTransient<DashboardPage>();
        builder.Services.AddTransient<MembersPage>();
        builder.Services.AddTransient<AddMemberPage>();
        builder.Services.AddTransient<ContributionsPage>();
        builder.Services.AddTransient<LoansPage>();
        builder.Services.AddTransient<LoanRequestPage>();
        builder.Services.AddTransient<SchedulePage>();
        builder.Services.AddTransient<FinesPage>();
        builder.Services.AddTransient<ReportPage>();
        builder.Services.AddTransient<ProfilePage>();
        builder.Services.AddTransient<BackupPage>();
        builder.Services.AddTransient<AboutPage>();

        return builder.Build();
    }
}
