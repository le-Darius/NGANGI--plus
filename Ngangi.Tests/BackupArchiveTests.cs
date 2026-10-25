using Ngangi.Core.Models;
using Ngangi.Core.Services;
using Xunit;

namespace Ngangi.Tests;

public sealed class BackupArchiveTests
{
    private static NjangiData ValidData() => new()
    {
        Group = new Group { Name = "Test njangi", CycleYear = 2026, CycleStart = new DateTime(2026, 1, 1), CycleEnd = new DateTime(2026, 12, 31), ContributionAmount = 10_000 },
        Members = new() { new Member { Id = 1, FullName = "Membre Test", Phone = "690000000", Role = Role.President } },
        Sessions = new(), Contributions = new(), Loans = new(), Installments = new(), Fines = new(),
        Accounts = new() { new UserAccount { Phone = "690000000", FullName = "Membre Test", PasswordHash = "v2$600000$hash", Salt = "salt", MemberId = 1 } }
    };

    [Fact]
    public void RoundTrip_PreservesDataAndMetadata()
    {
        var data = ValidData(); var bytes = BackupArchive.Create(data, "motdepasse-solide", new DateTimeOffset(2026, 10, 25, 10, 0, 0, TimeSpan.Zero));
        var opened = BackupArchive.Open(bytes, "motdepasse-solide");
        Assert.Equal(1, opened.Metadata.MemberCount);
        Assert.Equal("Membre Test", opened.Data.Members[0].FullName);
        Assert.Equal(data.Group.Name, opened.Data.Group.Name);
    }

    [Fact]
    public void WrongPassword_IsRejected()
    {
        var bytes = BackupArchive.Create(ValidData(), "motdepasse-solide");
        Assert.Throws<InvalidDataException>(() => BackupArchive.Open(bytes, "mauvais-motdepasse"));
    }

    [Fact]
    public void ModifiedByte_IsRejected()
    {
        var bytes = BackupArchive.Create(ValidData(), "motdepasse-solide"); bytes[^1] ^= 0x40;
        Assert.Throws<InvalidDataException>(() => BackupArchive.Open(bytes, "motdepasse-solide"));
    }

    [Fact]
    public void FutureVersion_IsRejected()
    {
        var bytes = BackupArchive.Create(ValidData(), "motdepasse-solide"); BitConverter.GetBytes(BackupArchive.CurrentVersion + 1).CopyTo(bytes, 4);
        Assert.Throws<NotSupportedException>(() => BackupArchive.Open(bytes, "motdepasse-solide"));
    }

    [Fact]
    public void InvalidData_IsRejectedBeforeEncryption()
    {
        var data = ValidData(); data.Group.MonthlyRate = 9m;
        Assert.Throws<InvalidDataException>(() => BackupArchive.Create(data, "motdepasse-solide"));
    }
}
