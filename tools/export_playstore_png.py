#!/usr/bin/env python3
"""Exporte les SVG du Play Store en PNG 32 bits (RGBA) avec Chromium (Playwright).
Prérequis : pip install playwright pillow && playwright install chromium
Usage : python3 tools/export_playstore_png.py
Alternatives sans Python :
  inkscape PlayStore/icon-512.svg --export-type=png -w 512 -h 512 -o PlayStore/icon-512.png
  inkscape PlayStore/banner-1024x500.svg --export-type=png -w 1024 -h 500 -o PlayStore/banner-1024x500.png
  magick -background none -density 384 PlayStore/icon-512.svg -resize 512x512 PNG32:PlayStore/icon-512.png"""
import pathlib
from playwright.sync_api import sync_playwright
from PIL import Image

ROOT = pathlib.Path(__file__).resolve().parent.parent
JOBS = [("PlayStore/icon-512.svg", 512, 512, "PlayStore/icon-512.png"),
        ("PlayStore/banner-1024x500.svg", 1024, 500, "PlayStore/banner-1024x500.png")]

with sync_playwright() as p:
    browser = p.chromium.launch()
    for src, w, h, out in JOBS:
        page = browser.new_page(viewport={"width": w, "height": h}, device_scale_factor=1)
        page.goto("file://" + str(ROOT / src))
        page.screenshot(path=str(ROOT / out), omit_background=True)
        page.close()
    browser.close()
for _, w, h, out in JOBS:
    f = ROOT / out
    im = Image.open(f).convert("RGBA")   # 32 bits
    im.save(f, optimize=True)
    print(f"{out} : {im.size[0]}x{im.size[1]} {im.mode} {f.stat().st_size} octets")
