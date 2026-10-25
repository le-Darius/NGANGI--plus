using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Ngangi.Core.Models;
using Ngangi.Core.Services;

namespace Ngangi.App.ViewModels;

public class LoanRequestViewModel : BaseViewModel
{
    private static readonly int[] Durations = { 2, 3, 4, 6 };

    private readonly NjangiService _s;
    private List<Member> _borrowers = new();
    private List<string> _borrowerNames = new();
    private int _borrowerIndex;
    private string _amountText = "";
    private int _months = 4;
    private string _hint = "", _rateText = "", _interestLabel = "", _interest = "—", _total = "—", _monthly = "—", _capital = "—", _firstDue = "—", _errors = "";
    private bool _canSubmit;
    private string _notice = "";

    public LoanRequestViewModel(NjangiService service)
    {
        _s = service;
        SubmitCommand = new Command(async () => await SubmitAsync(), () => _canSubmit);
        BackCommand = new Command(async () => await Nav.Back());
    }

    public ObservableCollection<ChipRow> Chips { get; } = new();
    public Command SubmitCommand { get; }
    public ICommand BackCommand { get; }

    public List<string> BorrowerNames { get => _borrowerNames; private set => Set(ref _borrowerNames, value); }

    public int BorrowerIndex
    {
        get => _borrowerIndex;
        set
        {
            if (value < 0 || value == _borrowerIndex) return;
            _borrowerIndex = value;
            OnPropertyChanged();
            Recompute();
        }
    }

    public string AmountText { get => _amountText; set { var digits = new string((value ?? "").Where(char.IsDigit).ToArray());
            string formatted = digits.Length == 0 ? "" : Formatting.Number(long.TryParse(digits, out var parsed) ? parsed : long.MaxValue);
            if (Set(ref _amountText, formatted)) Recompute(); } }
    public string Hint { get => _hint; private set => Set(ref _hint, value); }
    public string RateText { get => _rateText; private set => Set(ref _rateText, value); }
    public string InterestLabel { get => _interestLabel; private set => Set(ref _interestLabel, value); }
    public string Capital { get => _capital; private set => Set(ref _capital, value); }
    public string Interest { get => _interest; private set => Set(ref _interest, value); }
    public string Total { get => _total; private set => Set(ref _total, value); }
    public string Monthly { get => _monthly; private set => Set(ref _monthly, value); }
    public string FirstDue { get => _firstDue; private set => Set(ref _firstDue, value); }
    public string Errors { get => _errors; private set { if (Set(ref _errors, value)) OnPropertyChanged(nameof(HasErrors)); } }
    public bool HasErrors => _errors != "";

    /// <summary>Explication affichée quand aucune demande n'est possible (pas de membre, caisse trop petite).</summary>
    public string Notice { get => _notice; private set { if (Set(ref _notice, value)) OnPropertyChanged(nameof(HasNotice)); } }
    public bool HasNotice => _notice != "";
    public bool CanSubmit { get => _canSubmit; private set { if (Set(ref _canSubmit, value)) SubmitCommand.ChangeCanExecute(); } }
    public bool CanPickBorrower => _s.CanManage;

    public void Refresh()
    {
        var me = _s.CurrentMember;
        _borrowers = _s.CanManage ? _s.Data.Members.ToList() : _s.Data.Members.Where(m => m.Id == me?.Id).ToList();
        BorrowerNames = _borrowers.Select(m => m.FullName).ToList();
        _borrowerIndex = Math.Max(_borrowers.FindIndex(m => m.Id == me?.Id), 0);
        OnPropertyChanged(nameof(BorrowerIndex));
        OnPropertyChanged(nameof(CanPickBorrower));
        _amountText = "";
        OnPropertyChanged(nameof(AmountText));
        _months = 4;
        RateText = $"{(_s.Data.Group.MonthlyRate * 100).ToString("0.##", CultureInfo.InvariantCulture)} % par mois";
        Recompute();
    }

    private void BuildChips()
    {
        Chips.Clear();
        foreach (var m in Durations)
        {
            var months = m;
            bool selected = months == _months;
            Chips.Add(new ChipRow
            {
                Label = $"{months} mois",
                Bg = selected ? Palette.Primary : Palette.Chip,
                Fg = selected ? Palette.OnPrimary : Palette.TextColor,
                Command = new Command(() => { _months = months; Recompute(); })
            });
        }
    }

    private void Recompute()
    {
        BuildChips();
        Hint = $"Disponible en caisse : {Formatting.Fcfa(_s.CashAvailable())}";
        Notice = _borrowers.Count == 0 ? AppTexts.LoanNoMemberNotice
               : !_s.HasLoanableCash() ? AppTexts.LoanNoFundsNotice(Formatting.Fcfa(Limits.MinLoan))
               : "";

        // La lecture et le contrôle du montant sont des règles métier : elles vivent dans Ngangi.Core.
        long amount = 0;
        string? amountError = string.IsNullOrWhiteSpace(_amountText) ? null : Validation.ParseAmount(_amountText, out amount);
        var member = _borrowerIndex < _borrowers.Count ? _borrowers[_borrowerIndex] : null;
        decimal rate = _s.Data.Group.MonthlyRate;      // decimal : jamais de double pour un taux
        InterestLabel = $"Intérêt ({(rate * 100).ToString("0.##", CultureInfo.InvariantCulture)} % × {_months} mois)";

        if (amountError == null && amount > 0 && ( _s.QuoteLoan(amount, _months) is { } q))
        {
            Capital = Formatting.Fcfa(q.Principal);
            Interest = "+ " + Formatting.Fcfa(q.Interest);
            Total = Formatting.Fcfa(q.Total);
            Monthly = Formatting.Fcfa(q.Monthly);
            FirstDue = Formatting.DayMonthYear(_s.FirstLoanDueDate());
        }
        else
        {
            Capital = Interest = Total = Monthly = FirstDue = "—";
        }

        List<string> errors;
        if (amountError != null) errors = new List<string> { amountError };
        else if (member == null) errors = new List<string> { "Choisissez un membre." };
        else if (amount == 0) errors = new List<string>();
        else errors = _s.ValidateLoanRequest(member.Id, amount, _months);

        Errors = string.IsNullOrWhiteSpace(_amountText) ? "" : string.Join("\n", errors);
        CanSubmit = amountError == null && amount > 0 && member != null && errors.Count == 0;
    }

    private async Task SubmitAsync()
    {
        if (_borrowerIndex < 0 || _borrowerIndex >= _borrowers.Count) return;
        var member = _borrowers[_borrowerIndex];
        var amountError = Validation.ParseAmount(_amountText, out var amount);
        if (amountError != null)
        {
            await Nav.Alert("Demande impossible", amountError);
            return;
        }
        var r = _s.RequestLoan(member.Id, amount, _months);
        if (!r.Ok)
        {
            await Nav.Alert("Demande impossible", r.Message);
            return;
        }
        await Nav.Alert("Demande envoyée", "Le président ou le trésorier peut l'approuver depuis l'onglet Prêts.");
        await Nav.Go("//main/loans");
    }
}
