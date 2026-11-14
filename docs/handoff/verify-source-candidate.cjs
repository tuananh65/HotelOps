// Read-only identity audit. Never authorizes runtime, SQL, grants or Git delivery.
const fs=require('node:fs'),path=require('node:path'),cp=require('node:child_process'),crypto=require('node:crypto'),assert=require('node:assert/strict');
const root=path.resolve(__dirname,'../..');const manifest=JSON.parse(fs.readFileSync(path.join(__dirname,'SOURCE_CANDIDATE_IDENTITY.json'),'utf8'));
const hash=f=>crypto.createHash('sha256').update(fs.readFileSync(f)).digest('hex');
const git=(...args)=>cp.execFileSync('git',['--no-optional-locks','-C',root,...args],{encoding:'utf8',stdio:['ignore','pipe','pipe'],maxBuffer:25e6});
function verify(){
 assert.equal(manifest.runtimeAuthorized,false);assert.equal(manifest.historicalCertificateApplicable,false);
 assert.equal(git('rev-parse','HEAD').trim(),manifest.baseCommit);assert.equal(git('branch','--show-current').trim(),manifest.branch);
 const files=[...manifest.payloadFiles,...manifest.retainedTargetBuildFiles];const known=new Set(files.map(f=>f.path));
 for(const f of files){assert(!path.isAbsolute(f.path)&&!f.path.split('/').includes('..'));assert.equal(hash(path.join(root,f.path)),f.sha256,'Candidate drift: '+f.path);}
 const payload=new Set(manifest.payloadFiles.map(f=>f.path));payload.add('docs/handoff/SOURCE_CANDIDATE_IDENTITY.json');
 for(const s of git('status','--porcelain=v1','-z','-uall').split('\0').filter(Boolean)){assert(payload.has(s.slice(3)),'Unallowlisted change: '+s.slice(3));assert(s.startsWith(' M ')||s.startsWith('?? '),'Staging/deletion/rename not authorized');}
 for(const rel of ['.env','.env.local','.env.development','.env.development.local','frontend/.env','frontend/.env.local','frontend/.env.development','frontend/.env.development.local'])assert(!fs.existsSync(path.join(root,rel)),'Unpinned environment override');
 function walk(dir){if(!fs.existsSync(dir))return;for(const e of fs.readdirSync(dir,{withFileTypes:true})){if(['bin','obj','node_modules'].includes(e.name))continue;const f=path.join(dir,e.name);if(e.isDirectory())walk(f);else if(/\.(cs|csproj|tsx?|m?js|css|json)$/.test(e.name)){const rel=path.relative(root,f).replaceAll('\\','/');assert(known.has(rel),'Unpinned implicit source: '+rel);}}}
 for(const dir of ['src','frontend/src','scripts/verification/Dev2AllActions','scripts/verification/Dev2Safety008'])walk(path.join(root,dir));
 const canonical=JSON.stringify({baseCommit:manifest.baseCommit,branch:manifest.branch,files:files.sort((a,b)=>a.path.localeCompare(b.path))});assert.equal(crypto.createHash('sha256').update(canonical).digest('hex'),manifest.sourceIdentitySha256);
 return {status:'PASS_DERIVED_SOURCE_IDENTITY_ONLY',payloadFiles:payload.size,retainedTargetBuildFiles:manifest.retainedTargetBuildFiles.length,sourceIdentitySha256:manifest.sourceIdentitySha256,runtimeAuthorized:false,sqlConnections:0,historicalCertificateApplicable:false};
}
if(require.main===module){try{console.log(JSON.stringify(verify()))}catch(e){console.error('SOURCE_CANDIDATE_BLOCKED: '+e.message);process.exitCode=1;}}
module.exports={verify};
