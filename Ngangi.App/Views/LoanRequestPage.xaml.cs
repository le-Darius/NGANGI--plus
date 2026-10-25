using Ngangi.App.ViewModels;

namespace Ngangi.App.Views;

public partial class LoanRequestPage : ContentPage
{
    private readonly LoanRequestViewModel _vm;

    public LoanRequestPage(LoanRequestViewModel vm)
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
