using Ngangi.App.ViewModels;

namespace Ngangi.App.Views;

public partial class FinesPage : ContentPage
{
    private readonly FinesViewModel _vm;

    public FinesPage(FinesViewModel vm)
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
