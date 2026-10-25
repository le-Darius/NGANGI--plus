# NOTICE — éléments libres utilisés par Ngangi+

## Icônes (Ngangi.App/Resources/Icons/*.svg)

Les 19 icônes de l'application sont des icônes de **Lucide** (https://lucide.dev), version 1.61.0 du paquet `lucide-static`,
copiées telles quelles puis renommées. Aucune n'a été redessinée ; elles sont converties en tracés par
`tools/generate_icons.py` (arcs convertis en courbes de Bézier, rendu comparé à l'original : écart < 0,4 % des pixels).

- Licence de Lucide : **ISC**.
- Certaines icônes de Lucide dérivent de **Feather** (Cole Bemis) : licence **MIT**. Parmi les nôtres : `calendar`, `back`
  (arrow-left), `logout` (log-out), `chevron-right`, `add` (plus), `check`, `info`.

| Fichier | Icône Lucide | Fichier | Icône Lucide |
|---|---|---|---|
| home | house | members | users |
| contributions | wallet | loans | hand-coins |
| report | chart-pie | add | plus |
| fine | clock-alert | calendar | calendar |
| share | share-2 | settings | settings |
| back | arrow-left | logout | log-out |
| chevron-right | chevron-right | user | user |
| save | save | info | info |
| privacy | shield-check | terms | file-text |
| check | check | | |

Le texte complet des deux licences est affiché dans l'application (Profil › À propos) et reproduit ci-dessous.

### ISC License (Lucide)

```
ISC License

Copyright (c) 2026 Lucide Icons and Contributors

Permission to use, copy, modify, and/or distribute this software for any
purpose with or without fee is hereby granted, provided that the above
copyright notice and this permission notice appear in all copies.

THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES
WITH REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF
MERCHANTABILITY AND FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR
ANY SPECIAL, DIRECT, INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES
WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS, WHETHER IN AN
ACTION OF CONTRACT, NEGLIGENCE OR OTHER TORTIOUS ACTION, ARISING OUT OF
OR IN CONNECTION WITH THE USE OR PERFORMANCE OF THIS SOFTWARE.
```

### The MIT License (Feather)

```
The MIT License (MIT)

Copyright (c) 2013-present Cole Bemis

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## Autres composants

| Composant | Licence | Remarque |
|---|---|---|
| .NET et .NET MAUI (Microsoft) | MIT | cadre de l'application |
| System.Security.Cryptography.ProtectedData (Microsoft) | MIT | Windows seulement, protège la clé de chiffrement |
| Inter (polices converties en tracés dans `PlayStore/banner-1024x500.svg`) | SIL OFL 1.1 | visuel du Play Store uniquement, pas dans l'application |
