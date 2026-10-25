using System.Windows.Input;
using Ngangi.Core.Services;

namespace Ngangi.App.ViewModels;

public class RegisterViewModel : BaseViewModel
{
    private readonly NjangiService _s;
    private string _fullName = "";
    private string _phone = "";
    private string _password = "";
    private string _confirm = "";
    private bool _accepted;
    private string _error = "";

    public RegisterViewModel(NjangiService service)
    {
        _s = service;
        SubmitCommand = new Command(async () => await SubmitAsync(), () => CanSubmit);
        BackCommand = new Command(async () => await Nav.Back());
    }

    public ICommand BackCommand { get; }
    public Command SubmitCommand { get; }

    public string FullName { get => _fullName; set { if (Set(ref _fullName, value)) Changed(); } }
    public string Phone { get => _phone; set { if (Set(ref _phone, value)) Changed(); } }
    public string Password { get => _password; set { if (Set(ref _password, value)) Changed(); } }
    public string Confirm { get => _confirm; set { if (Set(ref _confirm, value)) Changed(); } }
    public bool Accepted { get => _accepted; set { if (Set(ref _accepted, value)) Changed(); } }

    // Un message n'apparaît que lorsque le champ a été rempli.
    public string NameMessage => _fullName == "" ? "" : Validation.NameError(_fullName) ?? "";
    public string PhoneMessage => _phone == "" ? "" : Validation.PhoneError(_phone) ?? "";
    public string ConfirmMessage => _confirm == "" ? "" : Validation.ConfirmError(_password, _confirm) ?? "";

    public bool HasNameMessage => NameMessage != "";
    public bool HasPhoneMessage => PhoneMessage != "";
    public bool HasConfirmMessage => ConfirmMessage != "";

    public bool NameValid => _fullName != "" && Validation.NameError(_fullName) == null;

    public string PasswordHint =>
        _password == "" ? "8 caractères minimum"
        : Validation.PasswordError(_password) == null ? "✓ 8 caractères minimum"
        : "8 caractères minimum.";

    public Color PasswordHintColor =>
        _password != "" && Validation.PasswordError(_password) == null ? Palette.OkTextColor
        : _password == "" ? Palette.Tone(Palette.Next).Fg
        : Palette.ErrorColor;

    public Brush NameStroke => Palette.FieldStroke(HasNameMessage);
    public Brush PhoneStroke => Palette.FieldStroke(HasPhoneMessage);
    public Brush PasswordStroke => Palette.FieldStroke(_password != "" && Validation.PasswordError(_password) != null);
    public Brush ConfirmStroke => Palette.FieldStroke(HasConfirmMessage);

    public bool CanSubmit =>
        Validation.NameError(_fullName) == null && Validation.PhoneError(_phone) == null &&
        Validation.PasswordError(_password) == null && Validation.ConfirmError(_password, _confirm) == null && _accepted;

    public bool ShowFixHint => !CanSubmit && (_fullName != "" || _phone != "" || _password != "" || _confirm != "");

    public string Error
    {
        get => _error;
        private set { if (Set(ref _error, value)) OnPropertyChanged(nameof(HasError)); }
    }
    public bool HasError => _error != "";

    public void Refresh()
    {
        _fullName = _phone = _password = _confirm = "";
        _accepted = false;
        Error = "";
        Raise(nameof(FullName), nameof(Phone), nameof(Password), nameof(Confirm), nameof(Accepted));
        Changed();
    }

    private void Changed()
    {
        Raise(nameof(NameMessage), nameof(PhoneMessage), nameof(ConfirmMessage),
            nameof(HasNameMessage), nameof(HasPhoneMessage), nameof(HasConfirmMessage), nameof(NameValid),
            nameof(PasswordHint), nameof(PasswordHintColor),
            nameof(NameStroke), nameof(PhoneStroke), nameof(PasswordStroke), nameof(ConfirmStroke),
            nameof(CanSubmit), nameof(ShowFixHint));
        SubmitCommand.ChangeCanExecute();
    }

    private async Task SubmitAsync()
    {
        var result = _s.Register(_fullName, _phone, _password, _confirm, _accepted);
        if (!result.Ok)
        {
            Error = result.Message;
            return;
        }
        await Nav.GoToMainAsync();
    }
}
