using System.Windows.Input;
using Microsoft.Maui.ApplicationModel;

namespace Ngangi.App.ViewModels;

public class AboutViewModel : BaseViewModel
{
    public AboutViewModel()
    {
        BackCommand = new Command(async () => await Nav.Back());
    }

    public ICommand BackCommand { get; }

    public string Version
    {
        get
        {
            try { return AppTexts.AboutVersion(AppInfo.Current.VersionString, AppInfo.Current.BuildString); }
            catch (Exception) { return AppTexts.AppName; }
        }
    }

    public string LicenseText => Licenses.IconLicenses;
}
