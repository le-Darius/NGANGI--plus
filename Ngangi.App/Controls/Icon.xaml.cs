using Microsoft.Maui.Controls.Shapes;

namespace Ngangi.App.Controls;

/// <summary>Teintes disponibles. Chaque valeur correspond à un style IconTone_* de Styles.xaml.</summary>
public enum IconTone { Text, Muted, Ok, Accent, Bad }

/// <summary>
/// Icône vectorielle de 24 unités (trait de 2, extrémités arrondies), redimensionnable avec <see cref="Size"/>.
/// Elle est décorative pour les lecteurs d'écran : le nom accessible est porté par le bouton ou la ligne qui la contient.
/// </summary>
public partial class Icon : ContentView
{
    private const double Base = 24;
    private static readonly PathGeometryConverter Converter = new();

    public static readonly BindableProperty KindProperty = BindableProperty.Create(
        nameof(Kind), typeof(IconKind), typeof(Icon), IconKind.None,
        propertyChanged: (b, _, _) => ((Icon)b).Apply());

    public static readonly BindableProperty ToneProperty = BindableProperty.Create(
        nameof(Tone), typeof(IconTone), typeof(Icon), IconTone.Text,
        propertyChanged: (b, _, _) => ((Icon)b).Apply());

    public static readonly BindableProperty SizeProperty = BindableProperty.Create(
        nameof(Size), typeof(double), typeof(Icon), Base,
        propertyChanged: (b, _, _) => ((Icon)b).Apply());

    public IconKind Kind { get => (IconKind)GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public IconTone Tone { get => (IconTone)GetValue(ToneProperty); set => SetValue(ToneProperty, value); }
    public double Size { get => (double)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    public Icon()
    {
        InitializeComponent();
        AutomationProperties.SetIsInAccessibleTree(this, false);
        HorizontalOptions = LayoutOptions.Center;
        VerticalOptions = LayoutOptions.Center;
        Apply();
    }

    private void Apply()
    {
        var path = IconData.PathOf(Kind);
        Glyph.Data = path == null ? null : Converter.ConvertFromInvariantString(path) as Geometry;

        var style = UiStyles.Get("IconTone_" + Tone);
        if (style != null) Glyph.Style = style;

        // Le tracé est dessiné sur 24 × 24 ; pour une autre taille, on agrandit l'ensemble (le trait suit).
        WidthRequest = HeightRequest = Size;
        Glyph.Scale = Size / Base;
    }
}
