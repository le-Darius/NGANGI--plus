using System.Windows.Input;

namespace Ngangi.App.Controls;

public partial class ShortcutTile : ContentView
{
    public static readonly BindableProperty KindProperty = BindableProperty.Create(
        nameof(Kind), typeof(IconKind), typeof(ShortcutTile), IconKind.None,
        propertyChanged: (b, _, n) => ((ShortcutTile)b).Glyph.Kind = (IconKind)n);

    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text), typeof(string), typeof(ShortcutTile), "",
        propertyChanged: (b, _, n) =>
        {
            var tile = (ShortcutTile)b;
            tile.Caption.Text = (string)n;
            SemanticProperties.SetDescription(tile.Hit, (string)n);
        });

    public static readonly BindableProperty CommandProperty = BindableProperty.Create(
        nameof(Command), typeof(ICommand), typeof(ShortcutTile), null);

    public IconKind Kind { get => (IconKind)GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public ICommand? Command { get => (ICommand?)GetValue(CommandProperty); set => SetValue(CommandProperty, value); }

    public ShortcutTile() => InitializeComponent();

    private void OnClicked(object? sender, EventArgs e)
    {
        if (Command?.CanExecute(null) == true) Command.Execute(null);
    }
}
