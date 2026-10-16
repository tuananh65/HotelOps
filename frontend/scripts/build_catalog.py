import json, re, zipfile
from pathlib import Path
import xml.etree.ElementTree as ET
ROOT = Path(__file__).resolve().parents[1]
NS = {'w':'http://schemas.openxmlformats.org/wordprocessingml/2006/main'}
tables=json.loads((ROOT/'docs/requirements/Report-3_Software Requirement Specification.tables.json').read_text(encoding='utf-8'))
with zipfile.ZipFile(ROOT.parent/'Report-3_Software Requirement Specification.docx') as z:
    body=ET.fromstring(z.read('word/document.xml')).find('w:body',NS)
ucs={}; current=None
for item in body:
    if item.tag.endswith('}p'):
        txt=''.join(t.text or '' for t in item.findall('.//w:t',NS))
        m=re.match(r'^\d+\.\d+\s+(UC-\d{3})\s+(.+)',txt)
        if m: current=(m[1],m[2])
    elif item.tag.endswith('}tbl') and current:
        rows=[['\n'.join(''.join(t.text or '' for t in p.findall('.//w:t',NS)) for p in c.findall('.//w:p',NS)) for c in r.findall('w:tc',NS)] for r in item.findall('w:tr',NS)]
        if rows and rows[0][0]=='Primary Actors':
            spec={r[0]:r[1] for r in rows if len(r)>1}
            ucs[current[0]]={'id':current[0],'name':current[1],'actors':spec['Primary Actors'].split(', '),'spec':spec,'rules':sorted(set(re.findall(r'GBR-\d{3}',str(spec))))}
            current=None
inventory=json.loads((ROOT/'docs/requirements/UC_Screen List.tables.json').read_text(encoding='utf-8'))[0][1:]
routes='login forgot-password change-password profile dashboard notifications notifications/:id users users/:id roles roles/:id permissions branches branches/:id locations locations/:id room-types room-types/:id rooms rooms/:id departments departments/:id room-readiness cleaning/tasks cleaning/tasks/new cleaning/tasks/:id cleaning/assignment rework rework/:id rooms/turnover checklists checklists/:id inspections/queue inspections/:id/perform inspections/:id/result inspections/:id/rework inspections/:id/reinspect inspections/history incidents incidents/new incidents/:id maintenance/assignment maintenance/tasks maintenance/tasks/:id maintenance/schedules maintenance/schedules/:id reference-data assets assets/:id assets/:id/history inventory/warehouses inventory/warehouses/:id inventory/dashboard inventory/items inventory/items/:id inventory/units inventory/transactions inventory/transactions/:id inventory/stocktakes inventory/stocktakes/:id inventory/ledger material-requests material-requests/:id replenishments replenishments/:id tasks tasks/:id tasks/assignment workload overdue shifts shifts/:id workdays handovers handovers/:id internal-requests internal-requests/:id reports audit-logs audit-logs/:id'.split()
screens=[]
for row,route in zip(inventory,routes):
    ids=[]
    for part in row[4].split(','):
        found=re.findall(r'UC-(\d{3})',part)
        if '→' in part and len(found)==2: ids.extend(f'UC-{n:03}' for n in range(int(found[0]),int(found[1])+1))
        else: ids.extend('UC-'+n for n in found)
    screens.append({'id':int(row[0]),'name':row[1],'feature':row[2],'description':row[3],'ucs':ids,'route':'/'+route,'roles':sorted(set(a for id in ids for a in ucs[id]['actors']))})
assert len(screens)==80 and len(ucs)==140
catalog={'screens':screens,'ucs':ucs,'rules':{r[0]:r[1] for r in tables[163][1:]},'parameters':tables[166][1:],'statuses':tables[167][1:],'transitions':tables[168][1:]}
(ROOT/'src').mkdir(exist_ok=True)
(ROOT/'docs/requirements/catalog-full.json').write_text(json.dumps(catalog,ensure_ascii=False,indent=2),encoding='utf-8')
runtime={**catalog,'ucs':{id:{k:v for k,v in uc.items() if k!='spec'} for id,uc in ucs.items()}}
(ROOT/'src/catalog.json').write_text(json.dumps(runtime,ensure_ascii=False,indent=2),encoding='utf-8')
(ROOT/'docs/screen-matrix.md').write_text('# Screen traceability\n\nSource: supplied SRS (without `(4)` suffix) and UC_Screen List.\n\n| # | Screen | FE | UC | Route | Status |\n|---|---|---|---|---|---|\n'+'\n'.join(f"| {s['id']} | {s['name']} | {s['feature']} | {', '.join(s['ucs'])} | `{s['route']}` | Pending functional verification |" for s in screens),encoding='utf-8')
print(f'{len(screens)} screens, {len(ucs)} UC specifications, {len(catalog["rules"])} rules')
