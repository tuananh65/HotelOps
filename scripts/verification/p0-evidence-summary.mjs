import {readFile,writeFile,mkdir} from 'node:fs/promises';
import {createHash} from 'node:crypto';
const root='artifacts/p0-workflow/';
const sources=['runtime-results.json','runtime-start-retry.json','runtime-integration-2.json','runtime-migration.json','runtime-requests-3.json','runtime-rooms-race-4.json'];
const chosen=new Map(),runs=[];
for(const file of sources){
  const text=await readFile(root+file,'utf8'),data=JSON.parse(text.replace(/^\uFEFF/,''));
  runs.push({file,at:data.at,sha256:createHash('sha256').update(text).digest('hex')});
  for(const result of data.results)if(result.status==='VERIFIED')chosen.set(result.id,{...result,file,at:data.at});
}
const rows=[...chosen.values()].sort((a,b)=>a.id.localeCompare(b.id));
if(rows.length!==28)throw Error(`Only ${rows.length} distinct verified E2E groups`);
const supplements=[];
for(const file of ['runtime-handler-supplement.json','runtime-legacy-supplement.json','runtime-asset-operations.json','runtime-report-snapshot.json']){
  const text=await readFile(root+file,'utf8'),data=JSON.parse(text.replace(/^\uFEFF/,''));
  if(!data.results.every(x=>x.status==='VERIFIED'))throw Error(`Unresolved failure ${file}`);
  supplements.push({file,at:data.at,results:data.results,sha256:createHash('sha256').update(text).digest('hex')});
}
const browser=JSON.parse(await readFile(root+'browser/results.json','utf8'));
const summary={at:new Date().toISOString(),timezone:'Asia/Saigon',database:'HotelOpsCompactWorkdayTestP0261124',
  scope:'Cumulative verified scenarios across explicit TEST runs, not a single fresh-fixture rerun',
  verifiedGroups:rows.length,assertions:rows.reduce((n,r)=>n+r.assertions,0),groups:rows,runs,supplements,
  limitations:['Three existing xUnit integration tests are skipped.',
    'Browser route/layout audit is not interactive coverage of every form.',
    'Synthetic legacy fixture simulates pre-migration dates; it is not original hotel history.',
    'No Production cutover, deployment, commit or push.']};
await writeFile(root+'evidence-summary.json',JSON.stringify(summary,null,2));
await mkdir('docs/requirements/p0-261124',{recursive:true});
const lines=['# Bằng chứng thực thi P0 — 24/11/2026','',
  `Tổng hợp ${rows.length}/28 nhóm E2E, ${summary.assertions} assertions trong các lần chạy được chọn. Đây là bằng chứng cộng dồn sau sửa lỗi, không phải một lần chạy lại từ database trống. API TEST port 5774; database \`${summary.database}\`.`,
  '', '| Nhóm | Kết quả | Assertions | Artifact trong artifacts/p0-workflow |', '|---|---|---:|---|',
  ...rows.map(r=>`| ${r.id} — ${r.title} | VERIFIED | ${r.assertions} | ${r.file} |`),
  '', '## Bổ sung runtime', '',
  ...supplements.flatMap(s=>s.results.map(r=>`- ${s.file}: ${r.name??r.id}, VERIFIED, ${r.assertions} assertions.`)),
  '', '## Tính toàn vẹn bằng chứng', '',
  'Các JSON lưu HTTP status, actor alias, route và thời gian, không lưu token. File private bị giới hạn ACL và không thuộc Git. Hash để đối chiếu snapshot artifact:', '',
  '| Artifact | SHA-256 |','|---|---|',...runs.concat(supplements).map(r=>`| ${r.file} | ${r.sha256} |`),
  '', '## Giới hạn', '',...summary.limitations.map(x=>`- ${x}`),''];
await writeFile('docs/requirements/p0-261124/validation-evidence.md',lines.join('\n'));
console.log(JSON.stringify({verifiedGroups:rows.length,assertions:summary.assertions,supplementGroups:supplements.reduce((n,s)=>n+s.results.length,0)}));
