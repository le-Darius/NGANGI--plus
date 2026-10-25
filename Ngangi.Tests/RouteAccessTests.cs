using Ngangi.Core.Services;
using Xunit;

namespace Ngangi.Tests;

/// <summary>Étape 4 : après une déconnexion, aucun écran privé ne doit pouvoir s'ouvrir.</summary>
public class RouteAccessTests
{
    [Theory]
    [InlineData("//login")]
    [InlineData("login")]
    [InlineData("//login/register")]
    [InlineData("register")]
    [InlineData("//LOGIN")]
    [InlineData("//login?x=1")]
    public void Public_screens_open_without_session(string location)
    {
        Assert.True(RouteAccess.IsPublic(location));
        Assert.True(RouteAccess.CanOpen(location, signedIn: false));
    }

    [Theory]
    [InlineData("//main")]
    [InlineData("//main/dashboard")]
    [InlineData("//main/members")]
    [InlineData("//main/dashboard/profile")]
    [InlineData("//main/dashboard/schedule?loanId=3")]
    [InlineData("profile")]
    [InlineData("fines")]
    [InlineData("loanrequest")]
    [InlineData("//login/profile")]
    [InlineData("//login/register/profile")]
    [InlineData("//loginx")]
    [InlineData("//registered")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("/")]
    [InlineData("//")]
    [InlineData(null)]
    public void Private_or_unknown_screens_are_refused_without_session(string? location)
    {
        Assert.False(RouteAccess.IsPublic(location));
        Assert.False(RouteAccess.CanOpen(location, signedIn: false));
    }

    [Theory]
    [InlineData("//main/dashboard")]
    [InlineData("//main/dashboard/profile")]
    [InlineData("//login")]
    [InlineData("anything")]
    [InlineData(null)]
    public void Signed_in_user_can_open_any_screen(string? location)
    {
        Assert.True(RouteAccess.CanOpen(location, signedIn: true));
    }
}
