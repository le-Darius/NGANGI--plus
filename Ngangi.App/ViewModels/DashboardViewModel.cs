using System.Collections.ObjectModel;
using System.Windows.Input;
using Ngangi.Core.Models;
using Ngangi.Core.Services;

namespace Ngangi.App.ViewModels;

public class DashboardViewModel : BaseViewModel
{
    private readonly NjangiService _s;
    private string _groupName = "", _cycleLabel = "", _cash = "", _activeLoans = "", _interest = "";
    private string _finesTitle = "", _finesDetail = "", _nextDay = "", _nextMonth = "", _nextTitle = "", _nextDetail = "";
    private string _initials = "", _profileDescription = AppTexts.ProfileOpenDescription("");
    private bool _hasFines, _hasNext;

    public DashboardViewModel(NjangiService service)
    {
        _s = service;
        OpenFinesCommand = new Command(async () => await Nav.Go("fines"));
        OpenLoanRequestCommand = new Command(async () => await Nav.Go("loanrequest"));
        OpenContributionsCommand = new Command(async () => await Nav.Go("//main/contributions"));
        OpenProfileCommand = new Command(async () => await Nav.Go("profile"));
    }

    public ObservableCollection<Row> Loans { get; } = new();

    public ICommand OpenFinesCommand { get; }
    public ICommand OpenLoanRequestCommand { get; }
    public ICommand OpenContributionsCommand { get; }
    public ICommand OpenProfileCommand { get; }

    public string GroupName { get => _groupName; private set => Set(ref _groupName, value); }
    public string CycleLabel { get => _cycleLabel; private set => Set(ref _cycleLabel, value); }
    public string Cash { get => _cash; private set => Set(ref _cash, value); }
    public string ActiveLoans { get => _activeLoans; private set => Set(ref _activeLoans, value); }
    public string Interest { get => _interest; private set => Set(ref _interest, value); }
    public string FinesTitle { get => _finesTitle; private set => Set(ref _finesTitle, value); }
    public string FinesDetail { get => _finesDetail; private set => Set(ref _finesDetail, value); }
    public bool HasFines { get => _hasFines; private set => Set(ref _hasFines, value); }
    public bool HasNext
    {
        get => _hasNext;
        private set { if (Set(ref _hasNext, value)) OnPropertyChanged(nameof(HasNoNext)); }
    }
    public bool HasNoNext => !_hasNext;
    public string NextDay { get => _nextDay; private set => Set(ref _nextDay, value); }
    public string NextMonth { get => _nextMonth; private set => Set(ref _nextMonth, value); }
    public string NextTitle { get => _nextTitle; private set => Set(ref _nextTitle, value); }
    public string NextDetail { get => _nextDetail; private set => Set(ref _nextDetail, value); }
    public string Initials { get => _initials; private set => Set(ref _initials, value); }
    public string ProfileDescription { get => _profileDescription; private set => Set(ref _profileDescription, value); }
    public bool HasNoLoans => Loans.Count == 0;

    public void Refresh()
    {
        _s.RefreshFines();
        var d = _s.GetDashboard();

        GroupName = d.GroupName;
        CycleLabel = d.CycleLabel;
        Cash = Formatting.Fcfa(d.Cash);
        ActiveLoans = d.ActiveLoanCount.ToString();
        Interest = Formatting.Fcfa(d.InterestReceived);
        Initials = _s.CurrentMember?.Initials ?? "?";
        ProfileDescription = AppTexts.ProfileOpenDescription(_s.CurrentMember?.FullName ?? "");

        HasFines = d.PendingFineCount > 0;
        FinesTitle = $"{d.PendingFineCount} amende{(d.PendingFineCount > 1 ? "s" : "")} à encaisser";
        FinesDetail = $"({Formatting.Fcfa(d.PendingFineTotal)}) pour retards de cotisation ou de remboursement.";

        HasNext = d.NextSession != null;
        if (d.NextSession != null)
        {
            var date = d.NextSession.Date;
            NextDay = date.Day.ToString();
            NextMonth = Formatting.MonthShort(date).ToUpperInvariant();
            NextTitle = $"{Formatting.Long(date).Split(' ')[0]}, {d.NextSession.TimeText} · chez {d.NextSession.Host}";
            NextDetail = $"Cotisation : {Formatting.Fcfa(_s.Data.Group.ContributionAmount)} · {d.NextSessionPaid} sur {d.MemberCount} payées";
        }

        Loans.Clear();
        foreach (var loan in _s.ActiveLoans())
        {
            var member = _s.Member(loan.MemberId);
            var next = _s.InstallmentsOf(loan.Id).FirstOrDefault(i => i.PaidOn == null);
            bool late = next != null && _s.StatusOf(next) == InstallmentStatus.Late;
            var id = loan.Id;
            var row = new Row
            {
                Title = $"{member?.FullName} · {Formatting.Fcfa(loan.Principal)}",
                Detail = next == null ? "Toutes les échéances sont payées"
                    : $"Échéance du {next.DueDate.Day:00}/{next.DueDate.Month:00} · {Formatting.Fcfa(next.Amount)}",
                Tap = new Command(async () => await Nav.Go($"schedule?loanId={id}"))
            };
            row.WithBadge(late ? "En retard" : "À jour", late ? Palette.Bad : Palette.Ok);
            Loans.Add(row);
        }
        OnPropertyChanged(nameof(HasNoLoans));
    }
}
