using System.Windows.Input;

namespace Ngangi.App.Controls;

public partial class ChoiceTile : ContentView
{
    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text), typeof(string), typeof(ChoiceTile), "", propertyChanged: (b, _, _) => ((ChoiceTile)b).Apply());

    public static readonly BindableProperty IsSelectedProperty = BindableProperty.Create(
        nameof(IsSelected), typeof(bool), typeof(ChoiceTile), false, propertyChanged: (b, _, _) => ((ChoiceTile)b).Apply());

    public static readonly BindableProperty CommandProperty = BindableProperty.Create(
        nameof(Command), typeof(ICommand), typeof(ChoiceTile), null);

    public static readonly BindableProperty CommandParameterProperty = BindableProperty.Create(
        nameof(CommandParameter), typeof(object), typeof(ChoiceTile), null);

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public bool IsSelected { get => (bool)GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }
    public ICommand? Command { get => (ICommand?)GetValue(CommandProperty); set => SetValue(CommandProperty, value); }
    public object? CommandParameter { get => GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }

    public ChoiceTile()
    {
        InitializeComponent();
        Apply();
    }

    private void Apply()
    {
        Caption.Text = Text;
        Check.IsVisible = IsSelected;

        var box = UiStyles.Get(IsSelected ? "ChoiceTileSelected" : "ChoiceTile");
        if (box != null) Box.Style = box;
        var label = UiStyles.Get(IsSelected ? "ChoiceLabelSelected" : "ChoiceLabel");
        if (label != null) Caption.Style = label;

        SemanticProperties.SetDescription(Hit, AppTexts.ThemeOptionDescription(Text, IsSelected));
    }

    private void OnClicked(object? sender, EventArgs e)
    {
        if (Command?.CanExecute(CommandParameter) == true) Command.Execute(CommandParameter);
    }
}
