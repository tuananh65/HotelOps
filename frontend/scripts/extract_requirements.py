from pathlib import Path
import zipfile
import xml.etree.ElementTree as ET
import json

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'docs' / 'requirements'
OUT.mkdir(parents=True, exist_ok=True)
NS = {'w': 'http://schemas.openxmlformats.org/wordprocessingml/2006/main'}
for source in ROOT.parent.glob('*.docx'):
    with zipfile.ZipFile(source) as archive:
        document = ET.fromstring(archive.read('word/document.xml'))
    paragraphs = [''.join(p.itertext()) for p in []]
    paragraphs = [''.join(t.text or '' for t in p.findall('.//w:t', NS)) for p in document.findall('.//w:p', NS)]
    (OUT / (source.stem + '.txt')).write_text('\n'.join(paragraphs), encoding='utf-8')
    tables = []
    for table in document.findall('.//w:tbl', NS):
        tables.append([['\n'.join(''.join(t.text or '' for t in p.findall('.//w:t', NS)) for p in cell.findall('.//w:p', NS)) for cell in row.findall('w:tc', NS)] for row in table.findall('w:tr', NS)])
    (OUT / (source.stem + '.tables.json')).write_text(json.dumps(tables, ensure_ascii=False, indent=2), encoding='utf-8')
    print(source.name, len(paragraphs), 'paragraphs', len(tables), 'tables')
