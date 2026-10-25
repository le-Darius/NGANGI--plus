using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Ngangi.Core.Models;
using Ngangi.Core.Services;

namespace Ngangi.App.ViewModels;

public class MembersViewModel : BaseViewModel
{
    private readonly NjangiService _s;
    private string _query = "";
    private string _countText = "";
    private string _emptyTitle = "", _emptyBody = "";
    private bool _canAdd;

    public MembersViewModel(NjangiService service)
    {
        _s = service;
        ShareSummaryCommand = new Command(async () =>
        {
            var g = _s.Data.Group;
            var summary = $"Groupe : {g.Name}\nCycle : {g.CycleStart:dd/MM/yyyy} au {g.CycleEnd:dd/MM/yyyy}\nCotisation : {Formatting.Fcfa(g.ContributionAmount)}\nTaux mensuel : {g.MonthlyRate:P0}\nMembres : {_s.Data.Members.Count}\n\nNgangi+ fonctionne hors ligne sur le téléphone du bureau.";
            await Share.Default.RequestAsync(new ShareTextRequest { Title = "Résumé du groupe", Text = summary });
        });
    }

    public ObservableCollection<Row> Members { get; } = new();
    public ICommand ShareSummaryCommand { get; }

    public string Query { get => _query; set { if (Set(ref _query, value)) Rebuild(); } }
    public string CountText { get => _countText; private set => Set(ref _countText, value); }
    public string EmptyTitle { get => _emptyTitle; private set => Set(ref _emptyTitle, value); }
    public string EmptyBody { get => _emptyBody; private set => Set(ref _emptyBody, value); }
    public bool HasNoMembers => Members.Count == 0;
    public bool CanAdd { get => _canAdd; private set => Set(ref _canAdd, value); }

    public void Refresh()
    {
        _s.RefreshFines();
        CanAdd = _s.CanManage;
        Rebuild();
    }

    public OperationResult AddMember(string name, string phone)
    {
        var r = _s.AddMember(name, phone);
        if (r.Ok) Rebuild();
        return r;
    }

    private void Rebuild()
    {
        CountText = AppTexts.MemberCount(_s.Data.Members.Count);
        var q = _query.Trim();
        var borrowers = _s.ActiveLoans().Select(l => l.MemberId).ToHashSet();

        Members.Clear();
        foreach (var m in _s.Data.Members.Where(m => q == "" || m.FullName.Contains(q, StringComparison.OrdinalIgnoreCase)))
        {
            var row = new Row
            {
                Initials = m.Initials,
                Title = m.FullName,
                Detail = m.RoleLabel + (borrowers.Contains(m.Id) ? " · 1 prêt en cours" : "")
            };
            switch (_s.StandingOf(m.Id))
            {
                case Standing.LoanLate: row.WithBadge("Prêt en retard", Palette.Bad); break;
                case Standing.ContributionLate: row.WithBadge("Retard", Palette.Bad); break;
                default: row.WithBadge("À jour", Palette.Ok); break;
            }
            Members.Add(row);
        }

        if (_s.Data.Members.Count == 0) { EmptyTitle = AppTexts.NoMemberTitle; EmptyBody = AppTexts.NoMemberBody; }
        else { EmptyTitle = AppTexts.NoMemberFoundTitle; EmptyBody = AppTexts.NoMemberFoundBody; }
        OnPropertyChanged(nameof(HasNoMembers));
    }
}
