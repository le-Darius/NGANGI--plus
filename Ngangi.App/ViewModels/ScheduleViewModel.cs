using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Ngangi.Core.Models;
using Ngangi.Core.Services;

namespace Ngangi.App.ViewModels;

public class ScheduleViewModel : BaseViewModel
{
    private readonly NjangiService _s;
    private string _borrower = "", _summary = "", _progressText = "";
    private double _progress;
    private bool _canRecord;
    private bool _hasLoan;

    public ScheduleViewModel(NjangiService service)
    {
        _s = service;
        BackCommand = new Command(async () => await Nav.Back());
        RecordCommand = new Command(async () =>
        {
            var loan = _s.Data.Loans.FirstOrDefault(l => l.Id == LoanId);
            var next = loan == null ? null : _s.InstallmentsOf(LoanId).FirstOrDefault(i => i.PaidOn == null);
            if (loan == null || next == null) return;
            var name = _s.Member(loan.MemberId)?.FullName ?? "le membre";
            if (!await Nav.Confirm("Confirmer le remboursement", $"Enregistrer {Formatting.Fcfa(next.Amount)} pour {name} (échéance du {Formatting.DayMonthYear(next.DueDate)}) ?")) return;
            var r = _s.RecordNextInstallmentPayment(LoanId);
            if (!r.Ok) await Nav.Alert("Impossible", r.Message);
            else await Nav.Alert("Remboursement enregistré", $"{Formatting.Fcfa(next.Amount)} reçu pour {name}.");
            Refresh();
        });
    }

    public int LoanId { get; set; }
    public ObservableCollection<Row> Installments { get; } = new();
    public ICommand BackCommand { get; }
    public ICommand RecordCommand { get; }

    public string Borrower { get => _borrower; private set => Set(ref _borrower, value); }
    public string Summary { get => _summary; private set => Set(ref _summary, value); }
    public string ProgressText { get => _progressText; private set => Set(ref _progressText, value); }
    public double Progress { get => _progress; private set => Set(ref _progress, value); }
    public bool CanRecord { get => _canRecord; private set => Set(ref _canRecord, value); }

    /// <summary>Faux si le prêt demandé n'existe pas : l'écran montre alors un état vide au lieu d'une fiche vide.</summary>
    public bool HasLoan { get => _hasLoan; private set { if (Set(ref _hasLoan, value)) OnPropertyChanged(nameof(HasNoLoan)); } }
    public bool HasNoLoan => !_hasLoan;
    /// <summary>Vrai si le prêt existe mais n'a pas (encore) d'échéancier.</summary>
    public bool HasNoInstallments => _hasLoan && Installments.Count == 0;

    public void Refresh()
    {
        _s.RefreshFines();
        var loan = _s.Data.Loans.FirstOrDefault(l => l.Id == LoanId);
        Installments.Clear();
        if (loan == null)
        {
            Borrower = Summary = ProgressText = "";
            Progress = 0;
            CanRecord = false;
            HasLoan = false;
            OnPropertyChanged(nameof(HasNoInstallments));
            return;
        }
        HasLoan = true;

        var member = _s.Member(loan.MemberId);
        var rate = (loan.MonthlyRate * 100).ToString("0.##", CultureInfo.InvariantCulture);
        var repaid = _s.RepaidOf(loan.Id);
        var total = _s.TotalDueOf(loan.Id);

        Borrower = member?.FullName ?? "?";
        Summary = $"{Formatting.Fcfa(loan.Principal)} · {loan.DurationMonths} mois · {rate} % par mois";
        ProgressText = $"{Formatting.Number(repaid)} remboursés sur {Formatting.Fcfa(total)}";
        Progress = (double)Money.Ratio(repaid, total);   // calcul en decimal ; double seulement pour la ProgressBar
        CanRecord = _s.CanManage && loan.Status == LoanStatus.Approved;

        var nextUnpaidId = _s.InstallmentsOf(loan.Id).FirstOrDefault(i => i.PaidOn == null)?.Id;
        foreach (var i in _s.InstallmentsOf(loan.Id))
        {
            var status = _s.StatusOf(i);
            var fine = _s.Data.Fines.FirstOrDefault(f => f.Reason == FineReason.Installment && f.SourceId == i.Id && !f.Cancelled);
            var row = new Row { Title = (i.Id == nextUnpaidId ? "À SUIVRE · " : "") + Formatting.DayMonthYear(i.DueDate), Detail = Formatting.Fcfa(i.Amount) };
            switch (status)
            {
                case InstallmentStatus.Paid:
                    row.WithBadge("✓ Payée", Palette.Ok);
                    break;
                case InstallmentStatus.Late:
                    int days = _s.LateDays(i);
                    row.Detail += $" · {days} jour{(days > 1 ? "s" : "")} de retard";
                    if (fine != null) row.Detail += $" + {Formatting.Fcfa(fine.Amount)} d'amende";
                    row.WithBadge("⚠ En retard", Palette.Bad);
                    break;
                default:
                    row.WithBadge("◷ À venir", Palette.Next);
                    break;
            }
            Installments.Add(row);
        }
        OnPropertyChanged(nameof(HasNoInstallments));
    }
}
