using System.Windows.Input;

namespace Ngangi.App.Controls;

/// <summary>Bouton rond à icône seule (retour, etc.). <see cref="Description"/> est lu par les lecteurs d'écran.</summary>
public partial class IconButton : ContentView
{
    public static readonly BindableProperty KindProperty = BindableProperty.Create(
        nameof(Kind), typeof(IconKind), typeof(IconButton), IconKind.None,
        propertyChanged: (b, _, n) => ((IconButton)b).Glyph.Kind = (IconKind)n);

    public static readonly BindableProperty ToneProperty = BindableProperty.Create(
        nameof(Tone), typeof(IconTone), typeof(IconButton), IconTone.Text,
        propertyChanged: (b, _, n) => ((IconButton)b).Glyph.Tone = (IconTone)n);

    public static readonly BindableProperty CommandProperty = BindableProperty.Create(
        nameof(Command), typeof(ICommand), typeof(IconButton), null);

    public static readonly BindableProperty CommandParameterProperty = BindableProperty.Create(
        nameof(CommandParameter), typeof(object), typeof(IconButton), null);

    public static readonly BindableProperty DescriptionProperty = BindableProperty.Create(
        nameof(Description), typeof(string), typeof(IconButton), "",
        propertyChanged: (b, _, n) => SemanticProperties.SetDescription(((IconButton)b).Hit, (string)n));

    public IconKind Kind { get => (IconKind)GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public IconTone Tone { get => (IconTone)GetValue(ToneProperty); set => SetValue(ToneProperty, value); }
    public ICommand? Command { get => (ICommand?)GetValue(CommandProperty); set => SetValue(CommandProperty, value); }
    public object? CommandParameter { get => GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }
    public string Description { get => (string)GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    public IconButton() => InitializeComponent();

    private void OnClicked(object? sender, EventArgs e)
    {
        if (Command?.CanExecute(CommandParameter) == true) Command.Execute(CommandParameter);
    }
}
