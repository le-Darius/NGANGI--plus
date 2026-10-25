using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Ngangi.Core.Models;
using Ngangi.Core.Services;

namespace Ngangi.App.ViewModels;

public class LoansViewModel : BaseViewModel
{
    private readonly NjangiService _s;

    public LoansViewModel(NjangiService service)
    {
        _s = service;
        RequestCommand = new Command(async () => await Nav.Go("loanrequest"));
    }

    public ObservableCollection<Row> Loans { get; } = new();
    public ICommand RequestCommand { get; }
    public bool HasNoLoans => Loans.Count == 0;

    public void Refresh()
    {
        _s.RefreshFines();
        Loans.Clear();
        var rate = (_s.Data.Group.MonthlyRate * 100).ToString("0.##", CultureInfo.InvariantCulture);

        foreach (var loan in _s.Data.Loans.Where(l => l.Status != LoanStatus.Rejected).OrderByDescending(l => l.RequestedOn).ThenByDescending(l => l.Id))
        {
            var member = _s.Member(loan.MemberId);
            var id = loan.Id;
            var row = new Row { Title = $"{member?.FullName} · {Formatting.Fcfa(loan.Principal)}" };

            if (loan.Status == LoanStatus.Pending)
            {
                row.Detail = $"Demande du {Formatting.DayMonthYear(loan.RequestedOn)} · {loan.DurationMonths} mois";
                row.WithBadge("En attente", Palette.Warn);
                if (_s.CanManage)
                {
                    row.PrimaryText = "Approuver";
                    row.PrimaryCommand = new Command(async () =>
                    {
                        var r = _s.ApproveLoan(id);
                        if (!r.Ok) await Nav.Alert("Impossible", r.Message);
                        Refresh();
                    });
                    row.SecondaryText = "Refuser";
                    row.SecondaryCommand = new Command(async () =>
                    {
                        var r = _s.RejectLoan(id);
                        if (!r.Ok) await Nav.Alert("Impossible", r.Message);
                        Refresh();
                    });
                }
            }
            else
            {
                var repaid = _s.RepaidOf(id);
                var total = _s.TotalDueOf(id);
                row.Detail = $"{loan.DurationMonths} mois · {rate} % par mois · {Formatting.Number(repaid)} / {Formatting.Fcfa(total)} remboursés";
                row.Tap = new Command(async () => await Nav.Go($"schedule?loanId={id}"));
                if (loan.Status == LoanStatus.Closed) row.WithBadge("Soldé", Palette.Next);
                else if (_s.InstallmentsOf(id).Any(i => _s.StatusOf(i) == InstallmentStatus.Late)) row.WithBadge("En retard", Palette.Bad);
                else row.WithBadge("En cours", Palette.Ok);
            }
            Loans.Add(row);
        }
        OnPropertyChanged(nameof(HasNoLoans));
    }
}
