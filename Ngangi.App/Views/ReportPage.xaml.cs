using Ngangi.App.ViewModels;

namespace Ngangi.App.Views;

public partial class ReportPage : ContentPage
{
    private readonly ReportViewModel _vm;

    public ReportPage(ReportViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.Refresh();
    }
}
