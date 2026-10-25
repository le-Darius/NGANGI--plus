using System.Windows.Input;
using Ngangi.Core.Models;
using Ngangi.Core.Services;

namespace Ngangi.App.ViewModels;

public class AddMemberViewModel : BaseViewModel
{
    private readonly NjangiService _service;
    private string _name = "", _phone = "", _error = "", _selectedRole = "Membre";
    public AddMemberViewModel(NjangiService service)
    {
        _service = service;
        SaveCommand = new Command(async () => await SaveAsync(), () => CanSave);
        CancelCommand = new Command(async () => await Nav.Back());
    }
    public string Name { get => _name; set { if (Set(ref _name, value)) Changed(); } }
    public string Phone { get => _phone; set { if (Set(ref _phone, value)) Changed(); } }
    public string SelectedRole { get => _selectedRole; set { if (Set(ref _selectedRole, value)) Changed(); } }
    public string Error { get => _error; private set { if (Set(ref _error, value)) OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => !string.IsNullOrWhiteSpace(Error);
    public string NameError => Name.Length == 0 ? "" : Validation.NameError(Name) ?? "";
    public string PhoneError => Phone.Length == 0 ? "" : Validation.PhoneError(Phone) ?? "";
    public bool HasNameError => NameError.Length > 0;
    public bool HasPhoneError => PhoneError.Length > 0;
    public bool CanSave => Validation.NameError(Name) == null && Validation.PhoneError(Phone) == null;
    public IReadOnlyList<string> Roles { get; } = new[] { "Membre", "Secrétaire", "Trésorier" };
    public ICommand CancelCommand { get; }
    public Command SaveCommand { get; }
    private void Changed()
    {
        Error = "";
        Raise(nameof(NameError), nameof(PhoneError), nameof(HasNameError), nameof(HasPhoneError), nameof(CanSave));
        SaveCommand.ChangeCanExecute();
    }
    private async Task SaveAsync()
    {
        var role = SelectedRole switch { "Secrétaire" => Role.Secretaire, "Trésorier" => Role.Tresorier, _ => Role.Membre };
        var result = _service.AddMember(Name, Phone, role);
        if (!result.Ok) { Error = result.Message; return; }
        await Nav.Alert("Membre ajouté", $"{Validation.CleanName(Name)} a été ajouté au groupe.");
        await Nav.Back();
    }
}
