using System.Security.Cryptography;
using System.Text;
using Ngangi.Core.Models;
using Ngangi.Core.Services;
using Xunit;

namespace Ngangi.Tests;

/// <summary>Clé fixe pour les tests : aucun coffre du système n'est nécessaire.</summary>
internal sealed class FixedKeyProvider : IKeyProvider
{
    private readonly byte[] _key;
    public FixedKeyProvider(byte fill) => _key = Enumerable.Repeat(fill, 32).ToArray();
    public byte[] GetOrCreateKey() => (byte[])_key.Clone();
}

public class ValidationHardeningTests
{
    [Fact]
    public void Les_Chiffres_Non_Ascii_Sont_Refuses()
    {
        // « ٦٧٠١٢٣٤٥٦ » : neuf chiffres arabo-indiens, acceptés par char.IsDigit mais pas par l'application.
        var arabic = "\u0666\u0667\u0660\u0661\u0662\u0663\u0664\u0665\u0666";
        Assert.NotNull(Validation.PhoneError(arabic));
        Assert.Null(Validation.NormalizePhone(arabic));
    }

    [Theory]
    [InlineData("abc 670123456")]
    [InlineData("670123456; DROP TABLE")]
    [InlineData("6701234567890123456789012345678901234567890")]
    public void Telephone_Avec_Caracteres_Etrangers_Ou_Trop_Long_Est_Refuse(string input)
    {
        Assert.NotNull(Validation.PhoneError(input));
        Assert.Null(Validation.NormalizePhone(input));
    }

    [Theory]
    [InlineData("80000", 80_000)]
    [InlineData("80 000", 80_000)]
    [InlineData("  1 250 000  ", 1_250_000)]
    public void Montant_Valide_Est_Lu(string text, long expected)
    {
        Assert.Null(Validation.ParseAmount(text, out var amount));
        Assert.Equal(expected, amount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("-80000")]
    [InlineData("80.5")]
    [InlineData("80,5")]
    [InlineData("0")]
    [InlineData("00")]
    [InlineData("abc")]
    [InlineData("80000 FCFA")]
    [InlineData("100000001")]
    [InlineData("99999999999999999999999")]
    public void Montant_Negatif_Decimal_Nul_Ou_Trop_Grand_Est_Refuse(string text)
    {
        Assert.NotNull(Validation.ParseAmount(text, out var amount));
        Assert.Equal(0, amount);
    }

    [Fact]
    public void Nom_Trop_Long_Ou_Avec_Caracteres_De_Controle_Est_Refuse()
    {
        Assert.NotNull(Validation.NameError(new string('a', Limits.MaxNameLength + 1)));
        Assert.NotNull(Validation.NameError("Paul\nNkeng"));
        Assert.Null(Validation.NameError("Paul Nkeng"));
        Assert.Equal("Paul Nkeng", Validation.CleanName("  Paul    Nkeng "));
    }

    [Fact]
    public void Mot_De_Passe_Trop_Long_Ou_Trop_Simple_Est_Refuse()
    {
        Assert.NotNull(Validation.PasswordError(new string('a', Limits.MaxPasswordLength + 1) + "b"));
        Assert.NotNull(Validation.PasswordError("aaaaaaaaaa"));
        Assert.Null(Validation.PasswordError("motdepasse"));
    }

    [Fact]
    public void Duree_Et_Taux_Hors_Bornes_Sont_Refuses()
    {
        Assert.NotNull(Validation.MonthsError(0));
        Assert.NotNull(Validation.MonthsError(13));
        Assert.NotNull(Validation.MonthsError(-3));
        Assert.Null(Validation.MonthsError(4));
        Assert.NotNull(Validation.RateError(-0.01m));
        Assert.NotNull(Validation.RateError(5m));
        Assert.Null(Validation.RateError(0.05m));
    }
}

public class LoanCalculatorHardeningTests
{
    [Fact]
    public void Quote_Refuse_Les_Valeurs_Hors_Bornes()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LoanCalculator.Quote(-5_000, 4, 0.05m));
        Assert.Throws<ArgumentOutOfRangeException>(() => LoanCalculator.Quote(long.MaxValue, 4, 0.05m));
        Assert.Throws<ArgumentOutOfRangeException>(() => LoanCalculator.Quote(Limits.MaxAmount + 1, 4, 0.05m));
        Assert.Throws<ArgumentOutOfRangeException>(() => LoanCalculator.Quote(50_000, 13, 0.05m));
        Assert.Throws<ArgumentOutOfRangeException>(() => LoanCalculator.Quote(50_000, 4, -0.01m));
        Assert.Throws<ArgumentOutOfRangeException>(() => LoanCalculator.Quote(50_000, 4, 2m));
    }

    [Fact]
    public void Quote_Au_Maximum_Ne_Deborde_Pas()
    {
        var q = LoanCalculator.Quote(Limits.MaxAmount, Limits.MaxMonths, Limits.MaxMonthlyRate);
        Assert.Equal(360_000_000, q.Interest);
        Assert.Equal(460_000_000, q.Total);
        Assert.True(q.Monthly > 0);
    }

    [Fact]
    public void TryQuote_Ne_Leve_Pas_Dexception()
    {
        Assert.False(LoanCalculator.TryQuote(0, 4, 0.05m, out var none));
        Assert.Null(none);
        Assert.True(LoanCalculator.TryQuote(80_000, 4, 0.05m, out var q));
        Assert.Equal(96_000, q!.Total);
    }

    [Fact]
    public void Taux_Zero_Donne_Zero_Interet()
    {
        var q = LoanCalculator.Quote(60_000, 3, 0m);
        Assert.Equal(0, q.Interest);
        Assert.Equal(60_000, q.Total);
        Assert.Equal(20_000, q.Monthly);
    }

    [Fact]
    public void Echeancier_Toujours_Exact_Pour_Toutes_Les_Combinaisons()
    {
        foreach (long principal in new long[] { 10_000, 10_001, 33_333, 77_777, 123_457 })
            for (int months = Limits.MinMonths; months <= Limits.MaxMonths; months++)
            {
                var loan = new Loan { Id = 1, Principal = principal, DurationMonths = months, MonthlyRate = 0.05m };
                var q = LoanCalculator.Quote(principal, months, 0.05m);
                var schedule = LoanCalculator.BuildSchedule(loan, new DateTime(2026, 1, 31));
                Assert.Equal(months, schedule.Count);
                Assert.Equal(q.Total, schedule.Sum(i => i.Amount));
                Assert.Equal(q.Interest, schedule.Sum(i => i.InterestPart));
                Assert.Equal(principal, schedule.Sum(i => i.PrincipalPart));
                Assert.All(schedule, i => Assert.True(i.Amount > 0));
            }
    }

    [Fact]
    public void Echeancier_Fin_De_Mois_Ne_Derive_Pas()
    {
        var loan = new Loan { Id = 1, Principal = 30_000, DurationMonths = 3, MonthlyRate = 0.05m };
        var s = LoanCalculator.BuildSchedule(loan, new DateTime(2026, 1, 31));
        Assert.Equal(new DateTime(2026, 1, 31), s[0].DueDate);
        Assert.Equal(new DateTime(2026, 2, 28), s[1].DueDate);
        Assert.Equal(new DateTime(2026, 3, 31), s[2].DueDate);
    }

    [Fact]
    public void TryBuildSchedule_Refuse_Un_Pret_Ou_Une_Date_Invalide()
    {
        var bad = new Loan { Id = 1, Principal = 50_000, DurationMonths = 0, MonthlyRate = 0.05m };
        Assert.False(LoanCalculator.TryBuildSchedule(bad, new DateTime(2026, 11, 5), out var empty, out var error));
        Assert.Empty(empty);
        Assert.NotNull(error);

        var ok = new Loan { Id = 1, Principal = 50_000, DurationMonths = 3, MonthlyRate = 0.05m };
        Assert.False(LoanCalculator.TryBuildSchedule(ok, DateTime.MinValue, out _, out _));
        Assert.False(LoanCalculator.TryBuildSchedule(ok, DateTime.MaxValue, out _, out _));
        Assert.True(LoanCalculator.TryBuildSchedule(ok, new DateTime(2026, 11, 5), out var sched, out _));
        Assert.Equal(3, sched.Count);
    }

    [Fact]
    public void Jours_De_Retard_Jamais_Negatifs()
    {
        var today = new DateTime(2026, 10, 6);
        Assert.Equal(1, LoanCalculator.OverdueDays(new DateTime(2026, 10, 5), today));
        Assert.Equal(0, LoanCalculator.OverdueDays(new DateTime(2026, 10, 6), today));
        Assert.Equal(0, LoanCalculator.OverdueDays(new DateTime(2026, 12, 25), today));
    }

    [Fact]
    public void Ratio_Sans_Division_Par_Zero()
    {
        Assert.Equal(0m, Money.Ratio(5, 0));
        Assert.Equal(0m, Money.Ratio(-5, 10));
        Assert.Equal(1m, Money.Ratio(20, 10));
        Assert.Equal(0.5m, Money.Ratio(5, 10));
    }
}

public class PasswordSecurityTests
{
    [Fact]
    public void Nouveau_Hash_Est_Versionne_Et_Verifiable()
    {
        var (hash, salt) = Security.HashPassword("njangi2026");
        Assert.StartsWith("v2$", hash);
        Assert.True(Security.Verify("njangi2026", hash, salt));
        Assert.False(Security.Verify("njangi2027", hash, salt));
        Assert.False(Security.NeedsRehash(hash));
    }

    [Fact]
    public void Deux_Hash_Du_Meme_Mot_De_Passe_Sont_Differents()
    {
        var a = Security.HashPassword("njangi2026");
        var b = Security.HashPassword("njangi2026");
        Assert.NotEqual(a.Hash, b.Hash);
        Assert.NotEqual(a.Salt, b.Salt);
    }

    [Fact]
    public void Ancien_Hash_Reste_Valide_Et_Doit_Etre_Renforce()
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var legacy = Convert.ToBase64String(Rfc2898DeriveBytes.Pbkdf2("njangi2026", salt, 50_000, HashAlgorithmName.SHA256, 32));
        Assert.True(Security.Verify("njangi2026", legacy, Convert.ToBase64String(salt)));
        Assert.False(Security.Verify("autre", legacy, Convert.ToBase64String(salt)));
        Assert.True(Security.NeedsRehash(legacy));
    }

    [Theory]
    [InlineData("pas-du-base64!!", "AAAA")]
    [InlineData("v2$600000$AAAA", "AAAA")]
    [InlineData("v2$abc$AAAA", "AAAA")]
    [InlineData("v2$99999999999$AAAA", "AAAA")]
    [InlineData("v2$1$AAAA", "AAAA")]
    [InlineData("", "")]
    public void Donnees_Corrompues_Donnent_False_Et_Jamais_Une_Exception(string hash, string salt)
    {
        Assert.False(Security.Verify("njangi2026", hash, salt));
    }

    [Fact]
    public void Connexion_Renforce_Un_Ancien_Hash()
    {
        var data = SeedData.Create();
        var account = data.Accounts[0];
        var salt = RandomNumberGenerator.GetBytes(16);
        account.PasswordHash = Convert.ToBase64String(Rfc2898DeriveBytes.Pbkdf2(SeedData.DemoPassword, salt, 50_000, HashAlgorithmName.SHA256, 32));
        account.Salt = Convert.ToBase64String(salt);

        var s = new NjangiService(data, () => new DateTime(2026, 10, 6));
        Assert.True(s.Login(account.Phone, SeedData.DemoPassword).Ok);
        Assert.StartsWith("v2$", account.PasswordHash);
        s.Logout();
        Assert.True(s.Login(account.Phone, SeedData.DemoPassword).Ok);
    }
}

public class LoginLockoutTests
{
    [Fact]
    public void Apres_5_Echecs_Le_Numero_Est_Bloque_Puis_Debloque()
    {
        var now = new DateTime(2026, 10, 6, 10, 0, 0, DateTimeKind.Utc);
        var s = new NjangiService(SeedData.Create(), () => new DateTime(2026, 10, 6), () => now);

        for (int i = 0; i < NjangiService.MaxLoginAttempts; i++)
            Assert.False(s.Login(SeedData.DemoPresidentPhone, "mauvais-" + i).Ok);

        // Même avec le bon mot de passe : bloqué.
        var blocked = s.Login(SeedData.DemoPresidentPhone, SeedData.DemoPassword);
        Assert.False(blocked.Ok);
        Assert.Contains("Trop de tentatives", blocked.Message);

        now = now + NjangiService.LoginLockDuration + TimeSpan.FromSeconds(1);
        Assert.True(s.Login(SeedData.DemoPresidentPhone, SeedData.DemoPassword).Ok);
    }

    [Fact]
    public void Un_Succes_Remet_Le_Compteur_A_Zero()
    {
        var now = new DateTime(2026, 10, 6, 10, 0, 0, DateTimeKind.Utc);
        var s = new NjangiService(SeedData.Create(), () => new DateTime(2026, 10, 6), () => now);
        for (int round = 0; round < 3; round++)
        {
            for (int i = 0; i < NjangiService.MaxLoginAttempts - 1; i++)
                Assert.False(s.Login(SeedData.DemoPresidentPhone, "mauvais").Ok);
            Assert.True(s.Login(SeedData.DemoPresidentPhone, SeedData.DemoPassword).Ok);
        }
    }

    [Fact]
    public void Numero_Inconnu_Donne_Le_Meme_Message_Qu_Un_Mauvais_Mot_De_Passe()
    {
        var s = new NjangiService(SeedData.Create(), () => new DateTime(2026, 10, 6));
        var unknown = s.Login("655000000", "nimporte");
        var wrong = s.Login(SeedData.DemoPresidentPhone, "nimporte");
        Assert.Equal(wrong.Message, unknown.Message);
    }
}

public class DataValidatorTests
{
    [Fact]
    public void Les_Donnees_De_Demonstration_Sont_Valides()
    {
        Assert.Empty(DataValidator.Validate(SeedData.Create()));
    }

    [Fact]
    public void Montant_Negatif_Taux_Absurde_Ou_Duree_Nulle_Sont_Detectes()
    {
        var d = SeedData.Create();
        d.Loans[0].Principal = -1;
        d.Loans[1].MonthlyRate = 5m;
        d.Loans[2].DurationMonths = 0;
        d.Group.LoanFundShare = 2m;
        var errors = DataValidator.Validate(d);
        Assert.True(errors.Count >= 4);
    }

    [Fact]
    public void Identifiants_En_Double_Et_Liens_Casses_Sont_Detectes()
    {
        var d = SeedData.Create();
        d.Members[1].Id = d.Members[0].Id;
        Assert.NotEmpty(DataValidator.Validate(d));

        var e = SeedData.Create();
        e.Contributions[0].MemberId = 9999;
        Assert.NotEmpty(DataValidator.Validate(e));

        var f = SeedData.Create();
        f.Installments[0].LoanId = 9999;
        Assert.NotEmpty(DataValidator.Validate(f));
    }
}

public class EncryptedStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ngangi-sec-" + Guid.NewGuid());
    private string FilePath => Path.Combine(_dir, "ngangi-data.json");

    public EncryptedStoreTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }

    private static AesGcmCipher Cipher(byte key = 7) => new(new FixedKeyProvider(key));

    [Fact]
    public void Le_Fichier_Chiffre_Ne_Contient_Aucune_Donnee_Lisible()
    {
        var store = new JsonDataStore(FilePath, Cipher());
        store.Save(SeedData.Create());

        var bytes = File.ReadAllBytes(FilePath);
        var text = Encoding.UTF8.GetString(bytes);
        Assert.True(Cipher().IsEncrypted(bytes));
        Assert.DoesNotContain("Mireille", text);
        Assert.DoesNotContain("699041122", text);
        Assert.DoesNotContain("PasswordHash", text);
    }

    [Fact]
    public void Sauvegarde_Et_Rechargement_Chiffres()
    {
        var store = new JsonDataStore(FilePath, Cipher());
        store.Save(SeedData.Create());
        var loaded = store.Load();
        Assert.NotNull(loaded);
        Assert.Equal(8, loaded!.Members.Count);
        Assert.Null(store.LastProblem);
        Assert.Equal(340_000, new NjangiService(loaded, () => new DateTime(2026, 10, 6)).CashAvailable());
    }

    [Fact]
    public void Chaque_Ecriture_Utilise_Un_Nonce_Different()
    {
        var store = new JsonDataStore(FilePath, Cipher());
        var data = SeedData.Create();
        store.Save(data);
        var first = File.ReadAllBytes(FilePath);
        store.Save(data);
        var second = File.ReadAllBytes(FilePath);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Un_Ancien_Fichier_En_Clair_Est_Migre_Sans_Copie_En_Clair()
    {
        new JsonDataStore(FilePath).Save(SeedData.Create());                 // ancien format
        Assert.Contains("Mireille", File.ReadAllText(FilePath));

        var store = new JsonDataStore(FilePath, Cipher());
        var loaded = store.Load();
        Assert.NotNull(loaded);                                              // lu normalement
        store.Save(loaded!);                                                 // puis chiffré

        Assert.True(Cipher().IsEncrypted(File.ReadAllBytes(FilePath)));
        Assert.DoesNotContain("Mireille", File.ReadAllText(FilePath, Encoding.Latin1));
        Assert.False(File.Exists(FilePath + ".bak"));                        // aucune copie en clair conservée
    }

    [Fact]
    public void Un_Fichier_Modifie_Est_Detecte_Et_Mis_De_Cote_Sans_Etre_Ecrase()
    {
        var store = new JsonDataStore(FilePath, Cipher());
        store.Save(SeedData.Create());

        var bytes = File.ReadAllBytes(FilePath);
        bytes[^1] ^= 0xFF;                                                   // un seul octet modifié
        File.WriteAllBytes(FilePath, bytes);

        Assert.Null(store.Load());
        Assert.NotNull(store.LastProblem);
        Assert.Single(Directory.GetFiles(_dir, "*.corrupt-*"));              // conservé pour analyse
    }

    [Fact]
    public void Mauvaise_Cle_Est_Detectee()
    {
        new JsonDataStore(FilePath, Cipher(1)).Save(SeedData.Create());
        var other = new JsonDataStore(FilePath, Cipher(2));
        Assert.Null(other.Load());
        Assert.NotNull(other.LastProblem);
    }

    [Fact]
    public void Fichier_Abime_Restaure_La_Sauvegarde_Precedente()
    {
        var store = new JsonDataStore(FilePath, Cipher());
        var first = SeedData.Create();
        store.Save(first);
        var second = SeedData.Create();
        second.Group.Name = "Nom modifié";
        store.Save(second);                                                  // .bak = première version

        File.WriteAllText(FilePath, "ceci n'est pas un fichier valide");

        var loaded = store.Load();
        Assert.NotNull(loaded);
        Assert.Equal(first.Group.Name, loaded!.Group.Name);
        Assert.NotNull(store.LastProblem);
        Assert.True(File.Exists(FilePath));                                  // le fichier principal est reconstitué
    }

    [Fact]
    public void Donnees_Invalides_Ne_Sont_Pas_Chargees()
    {
        var bad = SeedData.Create();
        bad.Loans[0].Principal = -50_000;
        new JsonDataStore(FilePath, Cipher()).Save(bad);

        var store = new JsonDataStore(FilePath, Cipher());
        Assert.Null(store.Load());
        Assert.Contains("invalides", store.LastProblem);
    }

    [Fact]
    public void Aes_Gcm_Aller_Retour_Et_Rejet_Des_Fichiers_Trop_Courts()
    {
        var cipher = Cipher();
        var plain = Encoding.UTF8.GetBytes("{\"test\":1}");
        Assert.Equal(plain, cipher.Decrypt(cipher.Encrypt(plain)));
        Assert.False(cipher.IsEncrypted(new byte[] { 1, 2, 3 }));
        Assert.Throws<CryptographicException>(() => cipher.Decrypt(new byte[] { 1, 2, 3 }));
    }
}
