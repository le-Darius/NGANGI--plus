#!/usr/bin/env python3
"""Contrôles automatiques de l'identité visuelle Ngangi+ (aucun .NET requis).
  1. Colors.xaml et Palette.cs ont exactement les mêmes clés et les mêmes valeurs.
  2. Aucune couleur écrite en dur ailleurs (XAML, C#, SVG, csproj) : chaque couleur vient de la palette.
  3. Aucune taille de texte chiffrée hors Typography.xaml ; l'échelle ne contient que 24, 18, 16, 13 (jamais < 12).
  4. Tous les contrastes AA (appelle tools/contrast.py).
Usage : python3 tools/verify_palette.py     Code de sortie 0 = tout est conforme."""
import re, sys, pathlib, subprocess

ROOT = pathlib.Path(__file__).resolve().parent.parent
APP = ROOT / "Ngangi.App"
errors = []

def files(*patterns):
    for pat in patterns:
        for f in ROOT.rglob(pat):
            if not any(part in ("bin", "obj", ".vs") for part in f.parts):
                yield f

# 1. XAML == C#
xaml = {k: v.upper() for k, v in re.findall(r'<Color x:Key="(\w+)">(#[0-9A-Fa-f]{6,8})</Color>',
                                            (APP / "Resources/Styles/Colors.xaml").read_text(encoding="utf-8"))}
cs_text = (APP / "Palette.cs").read_text(encoding="utf-8")
hex_block = cs_text[cs_text.index("class Hex"):cs_text.index("private static bool Dark")]
cs = {k: v.upper() for k, v in re.findall(r'\b(\w+) = "(#[0-9A-Fa-f]{6,8})"', hex_block)}
for k in sorted(set(xaml) | set(cs)):
    if k not in cs: errors.append(f"Palette.cs : clé absente « {k} » (présente dans Colors.xaml)")
    elif k not in xaml: errors.append(f"Colors.xaml : clé absente « {k} » (présente dans Palette.cs)")
    elif xaml[k] != cs[k]: errors.append(f"Valeur différente pour {k} : XAML {xaml[k]} / C# {cs[k]}")
allowed = set(xaml.values())
print(f"1. Palette : {len(xaml)} clés dans Colors.xaml, {len(cs)} dans Palette.cs")

# 2. Couleurs en dur
HEX = re.compile(r'#[0-9A-Fa-f]{8}\b|#[0-9A-Fa-f]{6}\b|#[0-9A-Fa-f]{3}\b')
NAMED_XAML = re.compile(r'(?:Color|Stroke|Fill)="(Transparent|White|Black|Gray|Grey|Red|Green|Blue|Orange|Yellow|Pink|Purple|Silver|Navy|Teal)"')
for f in files("*.xaml", "*.cs", "*.svg", "*.csproj", "*.xml"):
    rel = f.relative_to(ROOT).as_posix()
    if rel in ("Ngangi.App/Resources/Styles/Colors.xaml", "Ngangi.App/Palette.cs"): continue
    text = f.read_text(encoding="utf-8", errors="ignore")
    if f.suffix == ".cs":
        # ignorer les commentaires de documentation / lignes de commentaire
        text = "\n".join(l for l in text.splitlines() if not l.strip().startswith("//"))
        for m in re.finditer(r'\bColors\.\w+|Color\.From\w+\(', text):
            errors.append(f"{rel} : couleur en dur « {m.group(0)} »")
    if f.suffix == ".xaml":
        for m in NAMED_XAML.finditer(text):
            errors.append(f"{rel} : couleur nommée en dur « {m.group(0)} »")
    for m in HEX.finditer(re.sub(r'<!--.*?-->', '', text, flags=re.S)):
        h = m.group(0).upper()
        if f.suffix in (".svg", ".csproj") and h in allowed: continue   # SVG/csproj : doit venir de la palette
        if f.suffix == ".csproj" or f.suffix == ".svg":
            errors.append(f"{rel} : couleur {h} absente de Colors.xaml")
        elif f.suffix in (".xaml", ".cs", ".xml"):
            errors.append(f"{rel} : couleur en dur {h}")
print("2. Couleurs : recherche de couleurs en dur dans .xaml, .cs, .svg, .csproj, .xml — terminée")

# csproj : couleur de fond de l'icône = Bg, du splash = Hero
proj = (APP / "Ngangi.App.csproj").read_text(encoding="utf-8")
m_icon = re.search(r'<MauiIcon[^>]*Color="(#\w+)"', proj, re.S); m_spl = re.search(r'<MauiSplashScreen[^>]*Color="(#\w+)"', proj)
if not m_icon or m_icon.group(1).upper() != xaml["Bg"]: errors.append("csproj : la couleur de MauiIcon doit être Bg")
if not m_spl or m_spl.group(1).upper() != xaml["Hero"]: errors.append("csproj : la couleur de MauiSplashScreen doit être Hero")
bgsvg = (APP / "Resources/AppIcon/appicon.svg").read_text(encoding="utf-8")
if f'fill="{xaml["Bg"]}"'.lower() not in bgsvg.lower(): errors.append("appicon.svg : le fond doit être Bg")

# 3. Typographie
typo = (APP / "Resources/Styles/Typography.xaml").read_text(encoding="utf-8")
sizes = sorted({int(v) for v in re.findall(r'<x:Double x:Key="Font\w+">(\d+)</x:Double>', typo)})
if sizes != [13, 16, 18, 24]: errors.append(f"Typography.xaml : échelle attendue 13/16/18/24, trouvée {sizes}")
for f in files("*.xaml"):
    rel = f.relative_to(ROOT).as_posix()
    if rel.endswith("Typography.xaml"): continue
    for m in re.finditer(r'FontSize(?:"\s+Value)?="(\d+(?:\.\d+)?)"', f.read_text(encoding="utf-8")):
        errors.append(f"{rel} : taille de texte chiffrée ({m.group(1)}) — utiliser les ressources de Typography.xaml")
for f in files("*.cs"):
    if "FontSize =" in f.read_text(encoding="utf-8", errors="ignore"):
        errors.append(f"{f.relative_to(ROOT).as_posix()} : FontSize écrit dans le code")
print(f"3. Typographie : échelle {sizes}, aucune taille chiffrée hors Typography.xaml")

# 4. Contrastes
r = subprocess.run([sys.executable, "-I", str(ROOT / "tools/contrast.py")], capture_output=True, text=True)
last = [l for l in r.stdout.splitlines() if l.startswith("Échecs")]
print("4. Contrastes AA :", last[0] if last else r.stderr)
if r.returncode != 0: errors.append("contrastes sous le seuil AA (voir tools/contrast.py)")

print()
if errors:
    print("ÉCHECS :"); [print(" -", e) for e in errors]; sys.exit(1)
print("Tout est conforme.")
