# Report PDF font

NotoSans-Regular.ttf is embedded for server-side Unicode PDF rendering, independent of operating-system fonts.

- Source: https://github.com/notofonts/noto-fonts/blob/ffebf8c1ee449e544955a7e813c54f9b73848eac/hinted/ttf/NotoSans/NotoSans-Regular.ttf
- SHA256: B85C38ECEA8A7CFB39C24E395A4007474FA5A4FC864F6EE33309EB4948D232D5
- Original SIL Open Font License 1.1 is included in OFL.txt and copied on publish.
- Unsupported glyphs cause an explicit export error; they are never silently replaced with boxes. CSV/XLSX remain available.

PDFsharp is pinned to MIT release6.2.4 (supports .NET10). NuGet metadata and official history were checked during checkpoint168;6.5.0 uses AGPL and was not selected for delivery. There was no PDF dependency in the baseline; existing dependency versions are unchanged.
References: https://docs.pdfsharp.net/General/History.html and https://docs.pdfsharp.net/PDFsharp/Topics/Fonts/Font-Resolving.html.
