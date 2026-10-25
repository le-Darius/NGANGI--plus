using Ngangi.App.ViewModels;
namespace Ngangi.App.Views;
public partial class AddMemberPage : ContentPage
{
    public AddMemberPage(AddMemberViewModel vm) { InitializeComponent(); BindingContext = vm; }
    protected override void OnAppearing() { base.OnAppearing(); NameEntry.Focus(); }
}
