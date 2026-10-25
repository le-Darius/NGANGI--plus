using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Ngangi.Core.Services;

namespace Ngangi.App.ViewModels;

public class ReportViewModel : BaseViewModel
{
    private readonly NjangiService _s;
    private bool _projection = true;
    private string _banner = "", _total = "", _breakdown = "", _title = "", _emptyTitle = "", _emptyBody = "";
    private bool _hasEmpty;

    public ReportViewModel(NjangiService service)
    {
        _s = service;
        ShareCommand = new Command(async () =>
            await Share.Default.RequestAsync(new ShareTextRequest
            {
                Title = "Bilan du cycle",
                Text = _s.BuildReportText(_projection)
            }));
    }

    public ObservableCollection<Row> Shares { get; } = new();
    public ICommand ShareCommand { get; }

    public bool IsProjection
    {
        get => _projection;
        set { if (Set(ref _projection, value)) Refresh(); }
    }

    public string Title { get => _title; private set => Set(ref _title, value); }
    public string EmptyTitle { get => _emptyTitle; private set => Set(ref _emptyTitle, value); }
    public string EmptyBody { get => _emptyBody; private set => Set(ref _emptyBody, value); }
    /// <summary>Vrai quand il n'y a encore rien à partager (aucun membre, ou aucune somme encaissée).</summary>
    public bool HasEmpty { get => _hasEmpty; private set => Set(ref _hasEmpty, value); }

    public string Banner { get => _banner; private set => Set(ref _banner, value); }
    public string Total { get => _total; private set => Set(ref _total, value); }
    public string Breakdown { get => _breakdown; private set => Set(ref _breakdown, value); }

    public void Refresh()
    {
        var r = _s.BuildReport(_projection);
        var end = _s.Data.Group.CycleEnd;
        Title = AppTexts.ReportTitle(_s.Data.Group.CycleYear);
        // Pas de date de clôture affichée tant que le groupe n'a pas de cycle défini (évite « 01 janv. 0001 »).
        string closing = end.Year >= 2000 ? $" · clôture prévue le {Formatting.DayMonthYear(end)}" : "";
        Banner = _projection
            ? $"Projection au {Formatting.DayMonthYear(_s.Today)}{closing}"
            : $"Situation au {Formatting.DayMonthYear(_s.Today)} (sommes déjà encaissées)";
        Total = Formatting.Fcfa(r.TotalToShare);
        Breakdown = $"Part caisse des cotisations : {Formatting.Fcfa(r.Contributions)}\nIntérêts {(_projection ? "prévus ou encaissés" : "encaissés")} : {Formatting.Fcfa(r.Interest)}\nAmendes encaissées : {Formatting.Fcfa(r.Fines)}";

        if (r.Shares.Count == 0)
        {
            EmptyTitle = AppTexts.ReportNoMemberTitle;
            EmptyBody = AppTexts.ReportNoMemberBody;
            HasEmpty = true;
        }
        else if (r.TotalToShare == 0)
        {
            EmptyTitle = AppTexts.ReportEmptyTitle;
            EmptyBody = AppTexts.ReportEmptyBody;
            HasEmpty = true;
        }
        else HasEmpty = false;

        Shares.Clear();
        foreach (var s in r.Shares)
        {
            Shares.Add(new Row
            {
                Initials = s.Member.Initials,
                Title = s.Member.FullName,
                Detail = $"Part caisse des cotisations : {Formatting.Fcfa(s.Contributed)}\nPart des gains (intérêts + amendes) : {Formatting.Fcfa(s.Gains)}",
                Trailing = Formatting.Fcfa(s.Total)
            });
        }
    }
}
