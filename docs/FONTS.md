# Font guidance

Use Inter for Latin text in every language and Noto Naskh Arabic for Pashto and
Dari script. Both font families are bundled locally under the SIL Open Font
License. No font is loaded from a CDN.

Bundle fonts with each app; do not load them from a CDN. This keeps text
readable when the app is offline. PDF reports must embed the same fonts.

For Pashto, check that these letters render correctly:

`ټ ډ ړ ږ ښ ګ ڼ ۍ ې`

## Bundled UI fonts

The shared UI uses Inter for Latin glyphs and Noto Naskh Arabic for Arabic-script
glyphs. Unicode ranges on the CSS font faces keep the scripts on their intended
families. Variable font axes provide the required weights.

| File | Source URL | Version | Licence |
| --- | --- | --- | --- |
| `InterVariable.woff2` | [Official Inter release v4.1](https://github.com/rsms/inter/releases/download/v4.1/Inter-4.1.zip), member `web/InterVariable.woff2` | Inter 4.1 | SIL OFL 1.1 |
| `OFL-Inter.txt` | [Official Inter release v4.1](https://github.com/rsms/inter/releases/download/v4.1/Inter-4.1.zip), member `LICENSE.txt` | Inter 4.1 | SIL OFL 1.1 |
| `NotoNaskhArabic[wght].ttf` | [Google Fonts repository](https://raw.githubusercontent.com/google/fonts/cc39c4c2c304bc368c9b7c311ea2bcc6013299fe/ofl/notonaskharabic/NotoNaskhArabic%5Bwght%5D.ttf) | Noto Naskh Arabic 2.021 | SIL OFL 1.1 |
| `OFL-NotoNaskhArabic.txt` | [Google Fonts repository](https://raw.githubusercontent.com/google/fonts/cc39c4c2c304bc368c9b7c311ea2bcc6013299fe/ofl/notonaskharabic/OFL.txt) | Noto Naskh Arabic 2.021 | SIL OFL 1.1 |
| `NotoSans[wdth,wght].ttf` | [Google Fonts repository](https://raw.githubusercontent.com/google/fonts/cc39c4c2c304bc368c9b7c311ea2bcc6013299fe/ofl/notosans/NotoSans%5Bwdth%2Cwght%5D.ttf) | Noto Sans 2.015 | SIL OFL 1.1 |
| `OFL-NotoSans.txt` | [Google Fonts repository](https://raw.githubusercontent.com/google/fonts/cc39c4c2c304bc368c9b7c311ea2bcc6013299fe/ofl/notosans/OFL.txt) | Noto Sans 2.015 | SIL OFL 1.1 |

The original Noto Sans asset remains bundled for compatibility but is no longer
referenced by the shared UI stylesheet.
