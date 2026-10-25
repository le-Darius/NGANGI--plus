using System.Collections.ObjectModel;
using System.Windows.Input;
using Ngangi.Core.Models;
using Ngangi.Core.Services;

namespace Ngangi.App.ViewModels;

public class FinesViewModel : BaseViewModel
{
    private readonly NjangiService _s;
    private string _pending = "", _paid = "", _scale1 = "", _scale2 = "";

    public FinesViewModel(NjangiService service)
    {
        _s = service;
        BackCommand = new Command(async () => await Nav.Back());
    }

    public ObservableCollection<Row> Fines { get; } = new();
    public ICommand BackCommand { get; }

    public string Pending { get => _pending; private set => Set(ref _pending, value); }
    public string Paid { get => _paid; private set => Set(ref _paid, value); }
    public string Scale1 { get => _scale1; private set => Set(ref _scale1, value); }
    public string Scale2 { get => _scale2; private set => Set(ref _scale2, value); }
    public bool HasNoFines => Fines.Count == 0;

    public void Refresh()
    {
        _s.RefreshFines();
        Pending = Formatting.Fcfa(_s.PendingFinesTotal());
        Paid = $"Déjà encaissées ce cycle : {Formatting.Fcfa(_s.PaidFinesTotal())}";
        Scale1 = Formatting.Fcfa(_s.Data.Group.LateContributionFine);
        Scale2 = Formatting.Fcfa(_s.Data.Group.LateRepaymentFine);

        Fines.Clear();
        Fine? previousFine = null;
        foreach (var f in _s.PendingFines().OrderBy(f => f.Reason).ThenBy(f => f.MemberId))
        {
            if (previousFine == null || previousFine.Reason != f.Reason)
                Fines.Add(new Row { Title = f.Reason == FineReason.Contribution ? "Amendes · cotisations en retard" : "Amendes · remboursements en retard", Detail = "Regroupement par cause" });
            previousFine = f;
            var member = _s.Member(f.MemberId);
            var id = f.Id;
            var row = new Row
            {
                Initials = member?.Initials ?? "?",
                Title = member?.FullName ?? "?",
                Detail = _s.DescribeFine(f),
                Trailing = Formatting.Fcfa(f.Amount)
            };
            if (_s.CanManage)
            {
                row.PrimaryText = "Marquer payée";
                row.PrimaryCommand = new Command(async () =>
                {
                    var r = _s.PayFine(id);
                    if (!r.Ok) await Nav.Alert("Impossible", r.Message);
                    Refresh();
                });
            }
            if (_s.IsPresident)
            {
                row.SecondaryText = "Annuler";
                row.SecondaryCommand = new Command(async () =>
                {
                    if (!await Nav.Confirm("Annuler l'amende",  $"Annuler l’amende de {Formatting.Fcfa(f.Amount)} pour {member?.FullName ?? "ce membre"} ? Cette opération est réservée à la présidente.")) return;
                    var r = _s.CancelFine(id);
                    if (!r.Ok) await Nav.Alert("Impossible", r.Message);
                    Refresh();
                });
            }
            Fines.Add(row);
        }
        OnPropertyChanged(nameof(HasNoFines));
    }
}
