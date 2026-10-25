namespace Ngangi.App;

/// <summary>
/// Couleurs utilisées dans le code (badges, champs, boutons de durée).
/// Les valeurs de <see cref="Hex"/> portent le même nom que les clés de Colors.xaml et DOIVENT rester identiques
/// (contrôle : python3 tools/verify_palette.py). Aucune couleur ne s'écrit ailleurs dans le code.
/// </summary>
public static class Palette
{
    /// <summary>Valeurs hexadécimales : clair (nom seul) et sombre (suffixe D), comme dans Colors.xaml.</summary>
    public static class Hex
    {
        public const string Clear = "#00000000";
        public const string Bg = "#F3F8FA", BgD = "#0A1822";
        public const string Card = "#FFFFFF", CardD = "#112633";
        public const string Line = "#D3E0E7", LineD = "#223D4D";
        public const string InputLine = "#6B8797", InputLineD = "#5E8296";
        public const string Pri = "#0A5C7D", PriD = "#5BC0E0";
        public const string OnPri = "#FFFFFF", OnPriD = "#03212E";
        public const string Tint = "#E0EFF5", TintD = "#153446";
        public const string OkText = "#0A5C7D", OkTextD = "#7FD0EC";
        public const string Accent = "#8F4F1E", AccentD = "#E3A877";
        public const string Hero = "#0B4A66", HeroD = "#0F4560";
        public const string OnHero = "#FFFFFF", OnHeroD = "#EAF4F9";
        public const string HeroTile = "#1A6384", HeroTileD = "#1A5A78";
        public const string Text = "#0F2A3B", TextD = "#E9F2F7";
        public const string Muted = "#4A6071", MutedD = "#9DB4C2";
        public const string WarnBg = "#FBEBDB", WarnBgD = "#2E2113";
        public const string WarnLine = "#D9A06A", WarnLineD = "#7D5A33";
        public const string WarnText = "#6A3A12", WarnTextD = "#F2CFA6";
        public const string Bad = "#B3261E", BadD = "#F0806F";
        public const string BadBg = "#FBE3E0", BadBgD = "#3A1D19";
        public const string DisBg = "#DAE3E8", DisBgD = "#1C3342";
        public const string DisText = "#6F8190", DisTextD = "#7893A4";
        public const string PressedPri = "#073F57", PressedPriD = "#8AD3EC";
        public const string LogoRing = "#0A5C7D", LogoRingD = "#8AD3EC";
        public const string LogoRibbon = "#2A93B8", LogoRibbonD = "#4FB3D6";
        public const string LogoCopper = "#B06A32", LogoCopperD = "#E3A877";
    }

    private static bool Dark => Application.Current?.RequestedTheme == AppTheme.Dark;
    private static Color C(string light, string dark) => Color.FromArgb(Dark ? dark : light);

    public const string Ok = "ok", Bad = "bad", Warn = "warn", Next = "next";

    /// <summary>Aucune couleur (fond transparent) : seule « couleur » permise hors palette.</summary>
    public static Color None => Color.FromArgb(Hex.Clear);

    public static (Color Bg, Color Fg) Tone(string tone) => tone switch
    {
        Ok => (C(Hex.Tint, Hex.TintD), C(Hex.OkText, Hex.OkTextD)),
        Bad => (C(Hex.BadBg, Hex.BadBgD), C(Hex.Bad, Hex.BadD)),
        Warn => (C(Hex.WarnBg, Hex.WarnBgD), C(Hex.WarnText, Hex.WarnTextD)),
        _ => (None, C(Hex.Muted, Hex.MutedD)),
    };

    public static Color Primary => C(Hex.Pri, Hex.PriD);
    public static Color OnPrimary => C(Hex.OnPri, Hex.OnPriD);
    public static Color TextColor => C(Hex.Text, Hex.TextD);
    public static Color Chip => C(Hex.Card, Hex.CardD);
    public static Color ErrorColor => C(Hex.Bad, Hex.BadD);
    public static Color OkTextColor => C(Hex.OkText, Hex.OkTextD);

    public static Brush FieldStroke(bool error) =>
        new SolidColorBrush(error ? ErrorColor : C(Hex.InputLine, Hex.InputLineD));
}
