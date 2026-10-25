using Ngangi.App.Views;

namespace Ngangi.App.Controls;

/// <summary>
/// Barre du bas. Chaque page principale reçoit sa propre barre, qui met en évidence l'onglet de la page où elle se trouve :
/// la sélection ne dépend d'aucun événement de navigation, donc aucun état à resynchroniser.
/// </summary>
public partial class BottomBar : ContentView
{
    public BottomBar()
    {
        InitializeComponent();

        // Position et nombre d'onglets annoncés par TalkBack (« onglet 2 sur 5 »), dans l'ordre d'affichage.
        var tabs = new[] { TabHome, TabMembers, TabContributions, TabLoans, TabReport };
        for (var i = 0; i < tabs.Length; i++)
        {
            tabs[i].Position = i + 1;
            tabs[i].Count = tabs.Length;
        }
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        Select();
    }

    private void Select()
    {
        Element? e = this;
        while (e != null && e is not Page) e = e.Parent;

        var route = e switch
        {
            DashboardPage => "dashboard",
            MembersPage => "members",
            ContributionsPage => "contributions",
            LoansPage => "loans",
            ReportPage => "report",
            _ => ""
        };

        foreach (var tab in new[] { TabHome, TabMembers, TabContributions, TabLoans, TabReport })
            tab.IsSelected = tab.Route == route;
    }
}
