// Run after scaffolding Compact. Database FKs remain authoritative; scalar models avoid
// EF incorrectly making nullable alternate-key fields required and filtered unique FKs 1:1.
import {readFileSync,writeFileSync,readdirSync} from 'node:fs';
const root='src/HotelOps.Api/Compact';
for(const file of readdirSync(`${root}/Entities`).filter(x=>x.endsWith('.cs'))){
 const path=`${root}/Entities/${file}`;
 const code=readFileSync(path,'utf8').replace(/^    public virtual .*\r?\n/gm,'');
 writeFileSync(path,code);
}
const path=`${root}/Data/HotelOpsCompactDbContext.cs`;
let code=readFileSync(path,'utf8').replace(/            entity\.HasOne\([\s\S]*?;\r?\n/g,'');
code=code.replace(/(entity\.Property\(e => e\.(?:RoomId|TurnoverNo|ChecklistTemplateId)\)\s*)\.IsRequired\(\)\s*/g,'$1');
writeFileSync(path,code);
