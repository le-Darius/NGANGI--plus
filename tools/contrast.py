"""Rapport de contraste WCAG 2.x pour la palette Ngangi+.
Usage : python3 tools/contrast.py   (lit Ngangi.App/Resources/Styles/Colors.xaml)
Code de sortie 1 si une paire est sous son seuil (4,5 texte normal ; 3 grand texte / composant)."""
import re, sys, pathlib

ROOT = pathlib.Path(__file__).resolve().parent.parent
XAML = ROOT / "Ngangi.App/Resources/Styles/Colors.xaml"

def load():
    txt = XAML.read_text(encoding="utf-8")
    return {k: v.upper() for k, v in re.findall(r'<Color x:Key="(\w+)">(#[0-9A-Fa-f]{6})</Color>', txt)}

def lin(c):
    c /= 255
    return c / 12.92 if c <= 0.03928 else ((c + 0.055) / 1.055) ** 2.4

def lum(h):
    r, g, b = (int(h[i:i + 2], 16) for i in (1, 3, 5))
    return 0.2126 * lin(r) + 0.7152 * lin(g) + 0.0722 * lin(b)

def ratio(a, b):
    la, lb = sorted((lum(a), lum(b)), reverse=True)
    return (la + 0.05) / (lb + 0.05)

# (rôle, avant-plan, arrière-plan, seuil, exigé)  — exigé=False : élément désactivé (exempté par WCAG), simplement rapporté
TEXT, BIG, COMP = 4.5, 3.0, 3.0
PAIRS = [
    ("Texte courant", "Text", "Bg", TEXT, True),
    ("Texte sur carte", "Text", "Card", TEXT, True),
    ("Texte sur encadré teinté / avatar", "Text", "Tint", TEXT, True),
    ("Texte secondaire / légende", "Muted", "Bg", TEXT, True),
    ("Légende et indice de saisie sur carte", "Muted", "Card", TEXT, True),
    ("Légende sur encadré teinté", "Muted", "Tint", TEXT, True),
    ("Bouton principal (texte)", "OnPri", "Pri", TEXT, True),
    ("Bouton principal appuyé (texte)", "OnPri", "PressedPri", TEXT, True),
    ("Lien / bouton contour sur fond de page", "OkText", "Bg", TEXT, True),
    ("Lien / bouton contour sur carte", "OkText", "Card", TEXT, True),
    ("Badge « OK » et initiales d'avatar", "OkText", "Tint", TEXT, True),
    ("Titre de marque / onglet actif sur fond de page", "Pri", "Bg", TEXT, True),
    ("Onglet actif sur barre du bas", "Pri", "Card", TEXT, True),
    ("Onglet inactif sur barre du bas", "Muted", "Card", TEXT, True),
    ("Majuscules d'accent (cuivre) sur fond de page", "Accent", "Bg", TEXT, True),
    ("Majuscules d'accent (cuivre) sur carte", "Accent", "Card", TEXT, True),
    ("Majuscules d'accent (cuivre) sur encadré teinté", "Accent", "Tint", TEXT, True),
    ("Texte sur bandeau d'accueil", "OnHero", "Hero", TEXT, True),
    ("Texte sur tuile du bandeau", "OnHero", "HeroTile", TEXT, True),
    ("Avertissement (texte)", "WarnText", "WarnBg", TEXT, True),
    ("Erreur sur fond de page", "Bad", "Bg", TEXT, True),
    ("Erreur sur carte", "Bad", "Card", TEXT, True),
    ("Badge « retard »", "Bad", "BadBg", TEXT, True),
    ("Composant : bordure de champ sur carte", "InputLine", "Card", COMP, True),
    ("Composant : bordure de champ sur fond de page", "InputLine", "Bg", COMP, True),
    ("Composant : bouton, case, barre (Pri) sur carte", "Pri", "Card", COMP, True),
    ("Composant : bouton, case, barre (Pri) sur fond de page", "Pri", "Bg", COMP, True),
    # Étape 4 : navigation et réglages
    ("Icône et libellé d'onglet choisi sur pastille (OkText sur Tint : même paire que le badge « OK »)", "OkText", "Tint", TEXT, True),
    ("Indicateur de l'onglet choisi (barre en haut de l'onglet) sur la barre du bas", "Pri", "Card", COMP, True),
    ("Choix de thème sélectionné : trait épais (Pri) sur fond teinté", "Pri", "Tint", COMP, True),
    ("Choix de thème sélectionné : texte sur fond teinté", "Text", "Tint", TEXT, True),
    ("Icône sur ligne de réglage (OkText) sur carte", "OkText", "Card", COMP, True),
    ("Icône inactive (Muted) sur carte", "Muted", "Card", COMP, True),
    ("Bouton désactivé (exempté WCAG)", "DisText", "DisBg", COMP, False),
]

def main():
    pal = load()
    bad = 0
    for mode, suf in (("Clair", ""), ("Sombre", "D")):
        print(f"\n### Thème {mode}\n")
        print("| Usage | Texte / élément | Fond | Ratio | Seuil | Verdict |")
        print("|---|---|---|---|---|---|")
        for name, fg, bg, thr, req in PAIRS:
            f, b = fg + suf, bg + suf
            r = ratio(pal[f], pal[b])
            ok = r >= thr
            if req and not ok:
                bad += 1
            v = "AA ✔" if ok else ("sous le seuil (exempté)" if not req else "ÉCHEC ✘")
            print(f"| {name} | `{f}` {pal[f]} | `{b}` {pal[b]} | {r:.2f}:1 | {thr:g}:1 | {v} |")
    print(f"\nÉchecs : {bad}")
    sys.exit(1 if bad else 0)

if __name__ == "__main__":
    main()
