namespace Ngangi.App.Controls;

/// <summary>Petit logo vectoriel. <see cref="Size"/> en unités indépendantes de l'écran ; <see cref="IsDecorative"/>
/// le cache aux lecteurs d'écran quand le nom « Ngangi+ » est déjà écrit à côté.</summary>
public partial class BrandLogo : ContentView
{
    public static readonly BindableProperty SizeProperty = BindableProperty.Create(
        nameof(Size), typeof(double), typeof(BrandLogo), 40.0,
        propertyChanged: (b, _, _) => ((BrandLogo)b).Apply());

    public static readonly BindableProperty IsDecorativeProperty = BindableProperty.Create(
        nameof(IsDecorative), typeof(bool), typeof(BrandLogo), false,
        propertyChanged: (b, _, _) => ((BrandLogo)b).Apply());

    public double Size { get => (double)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    public bool IsDecorative { get => (bool)GetValue(IsDecorativeProperty); set => SetValue(IsDecorativeProperty, value); }

    public BrandLogo()
    {
        InitializeComponent();
        Apply();
    }

    private void Apply()
    {
        Img.WidthRequest = Size;
        Img.HeightRequest = Size;
        AutomationProperties.SetIsInAccessibleTree(Img, !IsDecorative);
    }
}
