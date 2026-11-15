# Font guidance

Use Noto Naskh Arabic or Vazirmatn for Pashto and Dari text. Both are available
under the SIL Open Font License. Use Noto Sans or Inter for English text.

Bundle fonts with each app; do not load them from a CDN. This keeps text
readable when the app is offline. PDF reports must embed the same fonts.

For Pashto, check that these letters render correctly:

`ټ ډ ړ ږ ښ ګ ڼ ۍ ې`

## Bundled UI fonts

The shared UI bundles the official variable TTF files below. Their weight axes
provide both Regular and Bold styles. Both families use the SIL Open Font
License 1.1; the full licence texts are beside the fonts.

| File | Source URL | Version | Licence |
| --- | --- | --- | --- |
| `NotoNaskhArabic[wght].ttf` | [Google Fonts repository](https://raw.githubusercontent.com/google/fonts/cc39c4c2c304bc368c9b7c311ea2bcc6013299fe/ofl/notonaskharabic/NotoNaskhArabic%5Bwght%5D.ttf) | Noto Naskh Arabic 2.021 | SIL OFL 1.1 |
| `OFL-NotoNaskhArabic.txt` | [Google Fonts repository](https://raw.githubusercontent.com/google/fonts/cc39c4c2c304bc368c9b7c311ea2bcc6013299fe/ofl/notonaskharabic/OFL.txt) | Noto Naskh Arabic 2.021 | SIL OFL 1.1 |
| `NotoSans[wdth,wght].ttf` | [Google Fonts repository](https://raw.githubusercontent.com/google/fonts/cc39c4c2c304bc368c9b7c311ea2bcc6013299fe/ofl/notosans/NotoSans%5Bwdth%2Cwght%5D.ttf) | Noto Sans 2.015 | SIL OFL 1.1 |
| `OFL-NotoSans.txt` | [Google Fonts repository](https://raw.githubusercontent.com/google/fonts/cc39c4c2c304bc368c9b7c311ea2bcc6013299fe/ofl/notosans/OFL.txt) | Noto Sans 2.015 | SIL OFL 1.1 |

These are variable fonts, so the browser selects Regular or Bold from each
font's weight axis without requiring separate static files.
