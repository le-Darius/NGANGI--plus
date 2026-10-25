#!/usr/bin/env python3
"""Génère Ngangi.App/Controls/IconData.g.cs à partir des SVG de Ngangi.App/Resources/Icons/.

Les SVG sont la source unique des icônes (base 24 dp, trait de 2, extrémités et jointures arrondies, tracé « currentColor »).
Le code ne lit pas les SVG au démarrage : ce script les convertit une fois en données de tracé que le contrôle Icon
dessine et teinte par code (aucune bibliothèque, aucune image bitmap).

Pour rester sûr avec l'analyseur de tracés de MAUI, chaque icône devient UNE chaîne n'utilisant que M, L, C et Z en
coordonnées absolues, nombres séparés par des espaces. Les arcs, cercles, rectangles arrondis, lignes et commandes
relatives sont convertis ici (arcs -> courbes de Bézier cubiques, erreur < 0,03 %).

Usage :
  python3 tools/generate_icons.py          écrit IconData.g.cs
  python3 tools/generate_icons.py --check  échoue (code 1) si IconData.g.cs n'est pas à jour avec les SVG
  python3 tools/generate_icons.py --svg-out DIR   écrit aussi, dans DIR, un SVG par icône avec le tracé normalisé
                                                  (sert à comparer le rendu avec l'original)
"""
import math, re, sys, pathlib
import xml.etree.ElementTree as ET

ROOT = pathlib.Path(__file__).resolve().parent.parent
ICONS = ROOT / "Ngangi.App/Resources/Icons"
OUT = ROOT / "Ngangi.App/Controls/IconData.g.cs"
NS = "{http://www.w3.org/2000/svg}"

def fmt(v):
    s = f"{v:.3f}".rstrip("0").rstrip(".")
    return "0" if s in ("-0", "") else s

# ------------------------------------------------------------------ lecture d'un tracé SVG
def parse_path(d):
    """Retourne une liste de segments absolus : ('M',x,y) ('L',x,y) ('C',x1,y1,x2,y2,x,y) ('A',...) ('Z',)."""
    segs = []
    i, n = 0, len(d)
    cx = cy = sx = sy = 0.0
    last_cmd, last_ctrl = None, None
    num = re.compile(r"[-+]?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?")
    flag = re.compile(r"[01]")

    def skip():
        nonlocal i
        while i < n and d[i] in " ,\t\r\n":
            i += 1

    def read_num():
        nonlocal i
        skip()
        m = num.match(d, i)
        if not m:
            raise ValueError(f"nombre attendu en position {i} dans « {d} »")
        i = m.end()
        return float(m.group(0))

    def read_flag():
        nonlocal i
        skip()
        m = flag.match(d, i)
        if not m:
            raise ValueError(f"indicateur d'arc attendu en position {i} dans « {d} »")
        i += 1
        return int(m.group(0))

    cmd = None
    while True:
        skip()
        if i >= n:
            break
        if d[i].isalpha():
            cmd = d[i]
            i += 1
            if cmd in "Zz":
                segs.append(("Z",))
                cx, cy = sx, sy
                last_cmd = "Z"
                continue
        elif cmd is None or cmd in "Zz":
            raise ValueError(f"commande attendue en position {i} dans « {d} »")
        rel = cmd.islower()
        up = cmd.upper()
        if up == "M":
            x, y = read_num(), read_num()
            if rel: x, y = cx + x, cy + y
            segs.append(("M", x, y)); cx, cy = sx, sy = x, y
            cmd = "l" if rel else "L"       # paires suivantes = lignes
            last_cmd = "M"
        elif up == "L":
            x, y = read_num(), read_num()
            if rel: x, y = cx + x, cy + y
            segs.append(("L", x, y)); cx, cy = x, y; last_cmd = "L"
        elif up == "H":
            x = read_num()
            if rel: x += cx
            segs.append(("L", x, cy)); cx = x; last_cmd = "H"
        elif up == "V":
            y = read_num()
            if rel: y += cy
            segs.append(("L", cx, y)); cy = y; last_cmd = "V"
        elif up == "C":
            v = [read_num() for _ in range(6)]
            if rel: v = [v[0]+cx, v[1]+cy, v[2]+cx, v[3]+cy, v[4]+cx, v[5]+cy]
            segs.append(("C", *v)); last_ctrl = (v[2], v[3]); cx, cy = v[4], v[5]; last_cmd = "C"
        elif up == "S":
            v = [read_num() for _ in range(4)]
            if rel: v = [v[0]+cx, v[1]+cy, v[2]+cx, v[3]+cy]
            if last_cmd in ("C", "S") and last_ctrl:
                x1, y1 = 2*cx - last_ctrl[0], 2*cy - last_ctrl[1]
            else:
                x1, y1 = cx, cy
            segs.append(("C", x1, y1, v[0], v[1], v[2], v[3])); last_ctrl = (v[0], v[1]); cx, cy = v[2], v[3]; last_cmd = "S"
        elif up == "Q":
            v = [read_num() for _ in range(4)]
            if rel: v = [v[0]+cx, v[1]+cy, v[2]+cx, v[3]+cy]
            segs.append(_quad(cx, cy, *v)); last_qctrl = (v[0], v[1]); cx, cy = v[2], v[3]; last_cmd = "Q"
            last_ctrl = last_qctrl
        elif up == "T":
            x, y = read_num(), read_num()
            if rel: x, y = cx + x, cy + y
            if last_cmd in ("Q", "T") and last_ctrl:
                qx, qy = 2*cx - last_ctrl[0], 2*cy - last_ctrl[1]
            else:
                qx, qy = cx, cy
            segs.append(_quad(cx, cy, qx, qy, x, y)); last_ctrl = (qx, qy); cx, cy = x, y; last_cmd = "T"
        elif up == "A":
            rx, ry, rot = read_num(), read_num(), read_num()
            large, sweep = read_flag(), read_flag()
            x, y = read_num(), read_num()
            if rel: x, y = cx + x, cy + y
            segs.extend(arc_to_cubics(cx, cy, rx, ry, rot, large, sweep, x, y))
            cx, cy = x, y; last_cmd = "A"
        else:
            raise ValueError(f"commande non gérée : {cmd}")
    return segs

def _quad(x0, y0, qx, qy, x, y):
    return ("C", x0 + 2/3*(qx - x0), y0 + 2/3*(qy - y0), x + 2/3*(qx - x), y + 2/3*(qy - y), x, y)

def arc_to_cubics(x1, y1, rx, ry, phi_deg, large, sweep, x2, y2):
    """Arc elliptique SVG -> courbes de Bézier cubiques (une par quart de tour au plus)."""
    if (x1, y1) == (x2, y2):
        return []
    if rx == 0 or ry == 0:
        return [("L", x2, y2)]
    rx, ry = abs(rx), abs(ry)
    phi = math.radians(phi_deg % 360)
    cp, sp = math.cos(phi), math.sin(phi)
    dx, dy = (x1 - x2) / 2, (y1 - y2) / 2
    x1p, y1p = cp*dx + sp*dy, -sp*dx + cp*dy
    lam = x1p**2/rx**2 + y1p**2/ry**2
    if lam > 1:
        s = math.sqrt(lam); rx *= s; ry *= s
    num = rx**2*ry**2 - rx**2*y1p**2 - ry**2*x1p**2
    den = rx**2*y1p**2 + ry**2*x1p**2
    coef = math.sqrt(max(0.0, num/den)) * (-1 if large == sweep else 1)
    cxp, cyp = coef*rx*y1p/ry, -coef*ry*x1p/rx
    cx = cp*cxp - sp*cyp + (x1 + x2)/2
    cy = sp*cxp + cp*cyp + (y1 + y2)/2
    def ang(ux, uy, vx, vy):
        a = math.atan2(ux*vy - uy*vx, ux*vx + uy*vy)
        return a
    th1 = ang(1, 0, (x1p - cxp)/rx, (y1p - cyp)/ry)
    dth = ang((x1p - cxp)/rx, (y1p - cyp)/ry, (-x1p - cxp)/rx, (-y1p - cyp)/ry)
    if not sweep and dth > 0: dth -= 2*math.pi
    elif sweep and dth < 0: dth += 2*math.pi
    n = max(1, math.ceil(abs(dth) / (math.pi/2) - 1e-9))
    step = dth / n
    t = 4/3 * math.tan(step/4)
    out = []
    a = th1
    for _ in range(n):
        b = a + step
        ca, sa, cb, sb = math.cos(a), math.sin(a), math.cos(b), math.sin(b)
        p1 = (ca - t*sa, sa + t*ca)
        p2 = (cb + t*sb, sb - t*cb)
        p3 = (cb, sb)
        pts = []
        for ux, uy in (p1, p2, p3):
            X, Y = rx*ux, ry*uy
            pts += [cp*X - sp*Y + cx, sp*X + cp*Y + cy]
        out.append(("C", *pts))
        a = b
    # le dernier point est exactement le point d'arrivée demandé
    last = list(out[-1]); last[-2], last[-1] = x2, y2
    out[-1] = tuple(last)
    return out

# ------------------------------------------------------------------ formes SVG -> tracé
def shape_to_d(el):
    tag = el.tag.replace(NS, "")
    g = lambda k, default=0.0: float(el.get(k, default))
    if tag == "path":
        return el.get("d")
    if tag == "circle":
        cx, cy, r = g("cx"), g("cy"), g("r")
        return f"M {cx-r} {cy} A {r} {r} 0 1 0 {cx+r} {cy} A {r} {r} 0 1 0 {cx-r} {cy} Z"
    if tag == "ellipse":
        cx, cy, rx, ry = g("cx"), g("cy"), g("rx"), g("ry")
        return f"M {cx-rx} {cy} A {rx} {ry} 0 1 0 {cx+rx} {cy} A {rx} {ry} 0 1 0 {cx-rx} {cy} Z"
    if tag == "line":
        return f"M {g('x1')} {g('y1')} L {g('x2')} {g('y2')}"
    if tag in ("polyline", "polygon"):
        pts = [float(v) for v in re.findall(r"[-+]?(?:\d+\.?\d*|\.\d+)", el.get("points", ""))]
        pairs = list(zip(pts[0::2], pts[1::2]))
        d = "M " + " L ".join(f"{x} {y}" for x, y in pairs)
        return d + (" Z" if tag == "polygon" else "")
    if tag == "rect":
        x, y, w, h = g("x"), g("y"), g("width"), g("height")
        rx = g("rx", el.get("ry", 0) or 0); ry = g("ry", el.get("rx", 0) or 0)
        if rx == 0 and ry == 0:
            return f"M {x} {y} H {x+w} V {y+h} H {x} Z"
        rx, ry = min(rx, w/2), min(ry, h/2)
        return (f"M {x+rx} {y} H {x+w-rx} A {rx} {ry} 0 0 1 {x+w} {y+ry} V {y+h-ry} A {rx} {ry} 0 0 1 {x+w-rx} {y+h} "
                f"H {x+rx} A {rx} {ry} 0 0 1 {x} {y+h-ry} V {y+ry} A {rx} {ry} 0 0 1 {x+rx} {y} Z")
    raise ValueError(f"élément SVG non géré : {tag}")

def svg_to_path(svg_file):
    root = ET.parse(svg_file).getroot()
    if root.get("viewBox") != "0 0 24 24":
        raise ValueError(f"{svg_file.name} : viewBox attendu « 0 0 24 24 »")
    if root.get("stroke-width") != "2" or root.get("stroke") != "currentColor":
        raise ValueError(f"{svg_file.name} : trait attendu : stroke=currentColor, stroke-width=2")
    parts = []
    for el in root:
        if el.tag.replace(NS, "") in ("title", "desc"):
            continue
        segs = parse_path(shape_to_d(el))
        for s in segs:
            parts.append(s[0] + (" " + " ".join(fmt(v) for v in s[1:]) if len(s) > 1 else ""))
    return " ".join(parts)

# ------------------------------------------------------------------ sortie
def pascal(name):
    return "".join(p.capitalize() for p in re.split(r"[-_]", name))

def build():
    files = sorted(ICONS.glob("*.svg"))
    if not files:
        raise SystemExit("Aucun SVG dans " + str(ICONS))
    data = {pascal(f.stem): svg_to_path(f) for f in files}
    lines = [
        "// <auto-generated>",
        "// Généré par tools/generate_icons.py à partir de Resources/Icons/*.svg — NE PAS MODIFIER À LA MAIN.",
        "// Pour changer une icône : modifier le SVG puis relancer  python3 tools/generate_icons.py",
        "// Jeu d'icônes : Lucide (ISC) et Feather (MIT), voir NOTICE.md.",
        "// </auto-generated>",
        "#nullable enable",
        "namespace Ngangi.App.Controls;",
        "",
        "/// <summary>Icônes disponibles. Chaque valeur correspond à un fichier de Resources/Icons.</summary>",
        "public enum IconKind",
        "{",
        "    None = 0,",
    ]
    lines += [f"    {k}," for k in data]
    lines += [
        "}",
        "",
        "/// <summary>Tracés des icônes (base 24, uniquement M, L, C, Z). Dessinés et teintés par le contrôle <see cref=\"Icon\"/>.</summary>",
        "internal static class IconData",
        "{",
        "    public static string? PathOf(IconKind kind) => kind switch",
        "    {",
    ]
    lines += [f"        IconKind.{k} => \"{v}\"," for k, v in data.items()]
    lines += ["        _ => null,", "    };", "}", ""]
    return "\n".join(lines), data

def main():
    text, data = build()
    if "--check" in sys.argv:
        current = OUT.read_text(encoding="utf-8") if OUT.exists() else ""
        if current.replace("\r\n", "\n") != text:
            print("IconData.g.cs n'est pas à jour : relancer python3 tools/generate_icons.py"); sys.exit(1)
        print(f"IconData.g.cs à jour ({len(data)} icônes)."); return
    OUT.write_text(text, encoding="utf-8", newline="\n")
    print(f"{OUT.relative_to(ROOT)} écrit : {len(data)} icônes")
    if "--svg-out" in sys.argv:
        out = pathlib.Path(sys.argv[sys.argv.index("--svg-out") + 1]); out.mkdir(parents=True, exist_ok=True)
        for k, v in data.items():
            (out / f"{k}.svg").write_text(
                '<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="black" '
                f'stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="{v}"/></svg>', encoding="utf-8")

if __name__ == "__main__":
    main()
