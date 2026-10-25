using System.Collections.ObjectModel;
using Ngangi.Core.Models;
using Ngangi.Core.Services;

namespace Ngangi.App.ViewModels;

public class ContributionsViewModel : BaseViewModel
{
    private readonly NjangiService _s;
    private List<MeetingSession> _sessions = new();
    private List<string> _labels = new();
    private int _index;
    private string _title = "", _summary = "", _paidText = "";
    private double _progress;

    public ContributionsViewModel(NjangiService service) => _s = service;

    public ObservableCollection<Row> Rows { get; } = new();

    public List<string> SessionLabels { get => _labels; private set => Set(ref _labels, value); }

    public int SelectedIndex
    {
        get => _index;
        set
        {
            if (value < 0 || value == _index) return;
            _index = value;
            OnPropertyChanged();
            Rebuild();
        }
    }

    public string Title { get => _title; private set => Set(ref _title, value); }
    public string Summary { get => _summary; private set => Set(ref _summary, value); }
    public string PaidText { get => _paidText; private set => Set(ref _paidText, value); }
    public double Progress { get => _progress; private set => Set(ref _progress, value); }

    /// <summary>Vrai s'il existe au moins une séance ; sinon l'écran montre un état vide à la place du sélecteur.</summary>
    public bool HasSessions => _sessions.Count > 0;
    public bool HasNoSessions => _sessions.Count == 0;
    /// <summary>Vrai si la séance choisie n'a aucune cotisation (aucun membre).</summary>
    public bool HasNoRows => _sessions.Count > 0 && Rows.Count == 0;

    public void Refresh()
    {
        _s.RefreshFines();
        _sessions = _s.Data.Sessions.OrderBy(x => x.Date).ToList();
        var next = _s.NextSession();
        int idx = next == null ? _sessions.Count - 1 : _sessions.FindIndex(x => x.Id == next.Id);
        SessionLabels = _sessions.Select(x => $"{Formatting.DayMonth(x.Date)} · chez {x.Host}").ToList();
        _index = Math.Max(idx, 0);
        OnPropertyChanged(nameof(SelectedIndex));
        Rebuild();
    }

    private void Rebuild()
    {
        Rows.Clear();
        if (_sessions.Count == 0)
        {
            Title = Summary = PaidText = "";
            Progress = 0;
            Raise(nameof(HasSessions), nameof(HasNoSessions), nameof(HasNoRows));
            return;
        }
        var session = _sessions[Math.Min(_index, _sessions.Count - 1)];
        var list = _s.ContributionsOf(session.Id);
        long total = list.Sum(c => c.Amount);
        long paid = list.Where(c => c.PaidOn != null).Sum(c => c.Amount);
        int paidCount = list.Count(c => c.PaidOn != null);

        Title = $"Collecté pour la séance du {Formatting.DayMonth(session.Date)}";
        Summary = $"{Formatting.Number(paid)} / {Formatting.Fcfa(total)}";
        PaidText = $"{paidCount} sur {list.Count}";
        Progress = (double)Money.Ratio(paid, total);   // calcul en decimal ; double seulement pour la ProgressBar

        foreach (var c in list)
        {
            var member = _s.Member(c.MemberId);
            var status = _s.StatusOf(c);
            var fine = _s.Data.Fines.FirstOrDefault(f => f.Reason == FineReason.Contribution && f.SourceId == c.Id && !f.Cancelled);
            var row = new Row
            {
                Initials = member?.Initials ?? "?",
                Title = member?.FullName ?? "?",
                Detail = status == ContributionStatus.Late && fine != null
                    ? $"Amende de {Formatting.Fcfa(fine.Amount)} appliquée" : ""
            };
            switch (status)
            {
                case ContributionStatus.Paid: row.WithBadge("✓ Payé", Palette.Ok); break;
                case ContributionStatus.Late: row.WithBadge("⚠ En retard", Palette.Bad); break;
                default: row.WithBadge("◷ À venir", Palette.Next); break;
            }
            if (status != ContributionStatus.Paid && _s.CanManage)
            {
                var id = c.Id;
                row.PrimaryText = "Enregistrer le paiement";
                row.PrimaryCommand = new Command(async () =>
                {
                    if (!await Nav.Confirm("Confirmer le paiement", $"Enregistrer le paiement de {Formatting.Fcfa(c.Amount)} pour {member?.FullName ?? "ce membre"} ?")) return;
                    var r = _s.RecordContributionPayment(id);
                    if (!r.Ok) await Nav.Alert("Impossible", r.Message);
                    else await Nav.Alert("Paiement enregistré", $"{Formatting.Fcfa(c.Amount)} reçu pour {member?.FullName ?? "le membre"}.");
                    Rebuild();
                });
            }
            Rows.Add(row);
        }
        Raise(nameof(HasSessions), nameof(HasNoSessions), nameof(HasNoRows));
    }
}
