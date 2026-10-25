#!/usr/bin/env python3
"""Génère tous les visuels de marque Ngangi+ (SVG) à partir de Colors.xaml.
Usage : python3 tools/generate_brand_assets.py
Écrit : icône adaptative (fond, avant-plan, monochrome), splash, logos de l'app (clair/sombre),
        et les sources SVG du Play Store (icône 512, bannière 1024x500).
L'emblème est une version simplifiée et vectorielle du logo fourni : anneau (2 arcs) + « N » en ruban.
Les couleurs viennent uniquement de Colors.xaml (une seule source de vérité).
La police du mot-symbole (Inter Bold, licence OFL) est convertie en tracés : les SVG ne dépendent d'aucune police."""
import math, pathlib, re, sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
APP = ROOT / "Ngangi.App"
PAL = {k: v.upper() for k, v in re.findall(r'<Color x:Key="(\w+)">(#[0-9A-Fa-f]{6})</Color>',
                                           (APP / "Resources/Styles/Colors.xaml").read_text(encoding="utf-8"))}

# --- Emblème (repère local 0..100) --------------------------------------------------------------
def pt(a, r=44.0):
    return 50 + r * math.cos(math.radians(a)), 50 + r * math.sin(math.radians(a))

def arc(a0, a1, large):
    (x0, y0), (x1, y1) = pt(a0), pt(a1)
    return f"M{x0:.2f},{y0:.2f} A44,44 0 {large} 1 {x1:.2f},{y1:.2f}"

def emblem(ring, ribbon, copper, scale=1.0, cx=50.0, cy=50.0):
    """Emblème centré en (cx, cy), diamètre = 96 * scale."""
    tx, ty = cx - 50 * scale, cy - 50 * scale
    return (
        f'<g transform="translate({tx:.2f} {ty:.2f}) scale({scale:.4f})" fill="none" stroke-linecap="round" stroke-linejoin="round">'
        f'<path d="{arc(50, 235, 1)}" stroke="{ring}" stroke-width="8"/>'
        f'<path d="{arc(-105, 35, 0)}" stroke="{copper}" stroke-width="8"/>'
        f'<path d="M32,68 L40,32 L62,68" stroke="{ribbon}" stroke-width="9"/>'
        f'<path d="M62,68 L70,32" stroke="{copper}" stroke-width="9"/>'
        f'</g>')

def light():  return PAL["LogoRing"], PAL["LogoRibbon"], PAL["LogoCopper"]
def dark():   return PAL["LogoRingD"], PAL["LogoRibbonD"], PAL["LogoCopperD"]

def svg(w, h, body, vb=None, title=None):
    t = f"<title>{title}</title>" if title else ""
    return (f'<?xml version="1.0" encoding="UTF-8" ?>\n'
            f'<svg xmlns="http://www.w3.org/2000/svg" width="{w}" height="{h}" viewBox="{vb or f"0 0 {w} {h}"}">{t}\n{body}\n</svg>\n')

def write(path, content):
    path = ROOT / path
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content, encoding="utf-8")
    print("écrit", path.relative_to(ROOT))

# --- Texte en tracés (Inter, OFL) -----------------------------------------------------------------
def text_path(text, font_file, size, x, y_base):
    from fontTools.ttLib import TTFont
    from fontTools.pens.svgPathPen import SVGPathPen
    from fontTools.pens.transformPen import TransformPen
    f = TTFont(font_file); gs = f.getGlyphSet(); cmap = f.getBestCmap(); upm = f["head"].unitsPerEm
    s = size / upm; cur = 0.0; parts = []
    for ch in text:
        g = cmap[ord(ch)]
        pen = SVGPathPen(gs)
        gs[g].draw(TransformPen(pen, (s, 0, 0, -s, x + cur, y_base)))
        parts.append(pen.getCommands())
        cur += f["hmtx"][g][0] * s
    return " ".join(parts), cur

INTER = pathlib.Path("/usr/share/fonts/opentype/inter")

def main():
    bg, hero = PAL["Bg"], PAL["Hero"]
    ring, ribbon, copper = light()
    dring, dribbon, dcopper = dark()

    # 1) Icône adaptative Android (calque 456x456 = 108 dp). Zone sûre = cercle central de 66 dp (61 %).
    #    L emblème (cercle) fait 60 % de la largeur : jamais rogné, quel que soit le masque (rond, carré, goutte).
    k = 0.60 * 456 / 96
    write("Ngangi.App/Resources/AppIcon/appicon.svg", svg(456, 456, f'<rect width="456" height="456" fill="{bg}"/>'))
    write("Ngangi.App/Resources/AppIcon/appiconfg.svg", svg(456, 456, emblem(ring, ribbon, copper, k, 228, 228), title="Ngangi+"))
    mono = PAL["LogoRing"]
    write("Ngangi.App/Resources/AppIcon/appiconmono.svg", svg(456, 456, emblem(mono, mono, mono, k, 228, 228)))

    # 2) Écran de démarrage : emblème clair sur le fond « Hero » (l'écran de démarrage Android ne suit pas le thème)
    ks = 0.50 * 456 / 96
    write("Ngangi.App/Resources/Splash/splash.svg", svg(456, 456, emblem(dring, dribbon, dcopper, ks, 228, 228)))

    # 3) Logos de l'app (petits) : clair et sombre
    write("Ngangi.App/Resources/Images/logo_ngangi.svg", svg(96, 96, emblem(ring, ribbon, copper, 1.0, 48, 48), title="Ngangi+"))
    write("Ngangi.App/Resources/Images/logo_ngangi_dark.svg", svg(96, 96, emblem(dring, dribbon, dcopper, 1.0, 48, 48), title="Ngangi+"))

    # 4) Play Store : icône 512x512 (carré plein, sans coins arrondis : Google applique son masque)
    k5 = 0.70 * 512 / 96
    write("PlayStore/icon-512.svg", svg(512, 512, f'<rect width="512" height="512" fill="{bg}"/>' + emblem(ring, ribbon, copper, k5, 256, 256), title="Ngangi+"))

    # 5) Play Store : bannière 1024x500 (logo, nom, accroche) — texte converti en tracés
    tagline = "Votre njangi, simple et hors ligne"
    try:
        name_d, name_w = text_path("Ngangi+", INTER / "Inter-Bold.otf", 124, 0, 0)
        tag_d, tag_w = text_path(tagline, INTER / "Inter-Regular.otf", 33, 0, 0)
    except Exception as e:  # police absente : repli sur du texte SVG (rendu dépendant de la police)
        print("Police Inter introuvable, repli sur <text> :", e); name_d = tag_d = None; name_w, tag_w = 470, 560
    em = 250; gap = 56
    block_w = max(name_w, tag_w); total = em + gap + block_w
    x0 = (1024 - total) / 2; cx = x0 + em / 2; tx = x0 + em + gap
    ky = em / 96
    body = f'<rect width="1024" height="500" fill="{hero}"/>' + emblem(dring, dribbon, dcopper, ky, cx, 250)
    on = PAL["OnHero"]
    if name_d:
        n, _ = text_path("Ngangi+", INTER / "Inter-Bold.otf", 124, tx, 262)
        t, _ = text_path(tagline, INTER / "Inter-Regular.otf", 33, tx, 322)
        body += f'<path d="{n}" fill="{on}"/><path d="{t}" fill="{on}"/>'
    else:
        body += (f'<text x="{tx}" y="262" font-family="Inter, Roboto, Arial, sans-serif" font-weight="700" font-size="124" fill="{on}">Ngangi+</text>'
                 f'<text x="{tx}" y="322" font-family="Inter, Roboto, Arial, sans-serif" font-size="33" fill="{on}">{tagline}</text>')
    write("PlayStore/banner-1024x500.svg", svg(1024, 500, body, title="Ngangi+ — " + tagline))

if __name__ == "__main__":
    main()
