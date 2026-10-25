using Ngangi.App.ViewModels;

namespace Ngangi.App.Views;

public partial class SchedulePage : ContentPage, IQueryAttributable
{
    private readonly ScheduleViewModel _vm;

    public SchedulePage(ScheduleViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("loanId", out var value) && int.TryParse(value?.ToString(), out var id))
        {
            _vm.LoanId = id;
            _vm.Refresh();
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.Refresh();
    }
}
