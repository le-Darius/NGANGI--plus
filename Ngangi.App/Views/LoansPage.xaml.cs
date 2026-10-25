using Ngangi.App.ViewModels;

namespace Ngangi.App.Views;

public partial class LoansPage : ContentPage
{
    private readonly LoansViewModel _vm;

    public LoansPage(LoansViewModel vm)
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
