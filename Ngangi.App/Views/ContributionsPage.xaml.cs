using Ngangi.App.ViewModels;

namespace Ngangi.App.Views;

public partial class ContributionsPage : ContentPage
{
    private readonly ContributionsViewModel _vm;

    public ContributionsPage(ContributionsViewModel vm)
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
