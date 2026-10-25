using System.Windows.Input;

namespace Ngangi.App.Controls;

public partial class SettingsRow : ContentView
{
    public static readonly BindableProperty KindProperty = BindableProperty.Create(
        nameof(Kind), typeof(IconKind), typeof(SettingsRow), IconKind.None,
        propertyChanged: (b, _, n) => ((SettingsRow)b).Glyph.Kind = (IconKind)n);

    public static readonly BindableProperty TitleProperty = BindableProperty.Create(
        nameof(Title), typeof(string), typeof(SettingsRow), "",
        propertyChanged: (b, _, _) => ((SettingsRow)b).Apply());

    public static readonly BindableProperty CaptionProperty = BindableProperty.Create(
        nameof(Caption), typeof(string), typeof(SettingsRow), "",
        propertyChanged: (b, _, _) => ((SettingsRow)b).Apply());

    /// <summary>Texte court affiché à la place du chevron (ex. « Bientôt ») : l'information est dite par écrit, pas par la couleur.</summary>
    public static readonly BindableProperty BadgeProperty = BindableProperty.Create(
        nameof(Badge), typeof(string), typeof(SettingsRow), "",
        propertyChanged: (b, _, _) => ((SettingsRow)b).Apply());

    public static readonly BindableProperty CommandProperty = BindableProperty.Create(
        nameof(Command), typeof(ICommand), typeof(SettingsRow), null);

    public IconKind Kind { get => (IconKind)GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Caption { get => (string)GetValue(CaptionProperty); set => SetValue(CaptionProperty, value); }
    public string Badge { get => (string)GetValue(BadgeProperty); set => SetValue(BadgeProperty, value); }
    public ICommand? Command { get => (ICommand?)GetValue(CommandProperty); set => SetValue(CommandProperty, value); }

    public SettingsRow() => InitializeComponent();

    private void Apply()
    {
        TitleLabel.Text = Title;
        CaptionLabel.Text = Caption;
        CaptionLabel.IsVisible = Caption != "";
        BadgeLabel.Text = Badge;
        BadgeBox.IsVisible = Badge != "";
        Chevron.IsVisible = Badge == "";

        var parts = new List<string> { Title };
        if (Caption != "") parts.Add(Caption);
        if (Badge != "") parts.Add(Badge);
        SemanticProperties.SetDescription(Hit, string.Join(", ", parts));
    }

    private void OnClicked(object? sender, EventArgs e)
    {
        if (Command?.CanExecute(null) == true) Command.Execute(null);
    }
}
