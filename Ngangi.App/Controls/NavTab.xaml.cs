namespace Ngangi.App.Controls;

public partial class NavTab : ContentView
{
    public static readonly BindableProperty KindProperty = BindableProperty.Create(
        nameof(Kind), typeof(IconKind), typeof(NavTab), IconKind.None,
        propertyChanged: (b, _, n) => ((NavTab)b).Glyph.Kind = (IconKind)n);

    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text), typeof(string), typeof(NavTab), "",
        propertyChanged: (b, _, _) => ((NavTab)b).Apply());

    public static readonly BindableProperty RouteProperty = BindableProperty.Create(
        nameof(Route), typeof(string), typeof(NavTab), "");

    public static readonly BindableProperty PositionProperty = BindableProperty.Create(
        nameof(Position), typeof(int), typeof(NavTab), 1, propertyChanged: (b, _, _) => ((NavTab)b).Apply());

    public static readonly BindableProperty CountProperty = BindableProperty.Create(
        nameof(Count), typeof(int), typeof(NavTab), 1, propertyChanged: (b, _, _) => ((NavTab)b).Apply());

    public static readonly BindableProperty IsSelectedProperty = BindableProperty.Create(
        nameof(IsSelected), typeof(bool), typeof(NavTab), false,
        propertyChanged: (b, _, _) => ((NavTab)b).Apply());

    public IconKind Kind { get => (IconKind)GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <summary>Nom de la route dans la barre « main » (ex. members) : la touche ouvre //main/{Route}.</summary>
    public string Route { get => (string)GetValue(RouteProperty); set => SetValue(RouteProperty, value); }

    /// <summary>Rang de l'onglet (1 à Count) et nombre d'onglets, annoncés par le lecteur d'écran. Renseignés par BottomBar.</summary>
    public int Position { get => (int)GetValue(PositionProperty); set => SetValue(PositionProperty, value); }
    public int Count { get => (int)GetValue(CountProperty); set => SetValue(CountProperty, value); }
    public bool IsSelected { get => (bool)GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }

    public NavTab()
    {
        InitializeComponent();
        Apply();
    }

    private void Apply()
    {
        Caption.Text = Text;
        Marker.IsVisible = Pill.IsVisible = IsSelected;
        Glyph.Tone = IsSelected ? IconTone.Ok : IconTone.Muted;

        var style = UiStyles.Get(IsSelected ? "TabLabelSelected" : "TabLabel");
        if (style != null) Caption.Style = style;

        SemanticProperties.SetDescription(Hit, AppTexts.TabDescription(Text, Position, Count, IsSelected));
    }

    private async void OnClicked(object? sender, EventArgs e)
    {
        if (IsSelected || string.IsNullOrEmpty(Route)) return;
        await Shell.Current.GoToAsync("//main/" + Route);
    }
}
