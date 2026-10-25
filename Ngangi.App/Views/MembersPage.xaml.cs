using Ngangi.App.ViewModels;
namespace Ngangi.App.Views;
public partial class MembersPage : ContentPage
{
    private readonly MembersViewModel _vm;
    public MembersPage(MembersViewModel vm) { InitializeComponent(); BindingContext = _vm = vm; }
    protected override void OnAppearing() { base.OnAppearing(); _vm.Refresh(); }
    private async void OnAddClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("addmember");
}
