// Offline evidence audit/sealing only; no SQL, network, browser or application startup.
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto'),assert=require('node:assert/strict');
const repo=path.resolve(__dirname,'../../..'),rel=p=>path.relative(repo,p).replaceAll('\\','/');
const hash=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const read=p=>JSON.parse(fs.readFileSync(p,'utf8').replace(/^\uFEFF/,''));
function write(name,value){fs.writeFileSync(path.join(__dirname,name),JSON.stringify(value,null,2)+'\n',{flag:'wx'});}
function walk(dir){return fs.readdirSync(dir,{withFileTypes:true}).flatMap(e=>{assert(!e.isSymbolicLink(),'Reparse input requires review');const p=path.join(dir,e.name);return e.isDirectory()?['bin','obj'].includes(e.name)?[]:walk(p):[p]});}
function verify(){
 const audit=require('../DEV2-FULLSTACK-008-START-PREFLIGHT-01/audit-source.cjs').verify();
 const prior=path.join(repo,'docs/integration-notes/DEV2-FULLSTACK-008-START-PREFLIGHT-01');
 assert.equal(hash(path.join(prior,'PACKAGE_SHA256.json')),'11c473f33a9318764ff50e43bba342a5d5305202c7c65a75a799a4c7662240c4');
 const old=read(path.join(prior,'PACKAGE_SHA256.json'));
 for(const p of old){const key=p.path??p.file;assert(key,'Historical seal path');const f=key.startsWith('docs/')?path.join(repo,key):path.join(prior,key);assert.equal(hash(f),p.sha256,'Historical preflight drift '+key);}
 const scope=read(path.join(prior,'PROPOSED_GIT_HANDOFF_SCOPE.json'));assert.equal(scope.sourcePayloadCandidates.length,129);
 const handoff=scope.sourcePayloadCandidates.map(p=>{assert.equal(hash(path.join(repo,p.path)),p.sha256,'129 source payload drift '+p.path);return {...p,currentHash:p.sha256,disposition:'PRESERVED_CANDIDATE_ONLY_NOT_STAGED_OR_APPROVED'};});
 return {audit,handoff};
}
function seal(){
 const {audit,handoff}=verify();
 write('SOURCE_AND_HISTORY_VERIFICATION.json',audit);
 write('HANDOFF_129_AUDIT.json',{count:handoff.length,allCurrentHashesMatch:true,paths:handoff,newSafetyCandidate:'SEPARATE_NOT_AUTOMATICALLY_INCLUDED_IN_129',mergeReadiness:'NO_GO',ownershipOverlaps:'10_CACHED_TARGET_OVERLAPS_REMAIN_PENDING_DEV1_DEV3_REVIEW'});
 const candidate=walk(path.join(repo,'scripts/verification/Dev2Safety008'));
 const tools=['RUN_WINDOWS_SAFETY_ACCEPTANCE.ps1','seal-candidate.cjs','review-candidate.cjs','VALIDATE_STATIC.ps1','AUTHORIZATION_TEMPLATE.json'].map(n=>path.join(__dirname,n));
 const verifier=path.join(repo,'.tmp-dev2-safety008/bin/Dev2Safety008/debug');
 const api=path.join(repo,'.tmp-dev2-safety008/bin/HotelOps.Api/debug');
 assert(fs.existsSync(path.join(verifier,'Dev2Safety008.dll')));assert(fs.existsSync(path.join(api,'HotelOps.Api.dll')));
 const binaries=[verifier,api].flatMap(dir=>walk(dir).filter(p=>/\.(dll|json)$/.test(p)));
 const files=[...new Set([...candidate,...tools,...binaries])].sort().map(p=>({path:rel(p),sha256:hash(p)}));
 write('CANDIDATE_RELEASE.json',{scope:'NEW_SAFETY008_CANDIDATE_NOT_ACCEPTED007',runtimeDll:rel(path.join(verifier,'Dev2Safety008.dll')),apiDll:rel(path.join(api,'HotelOps.Api.dll')),accepted682ManifestSha256:'11483526be3e092a4f7e95e7034979d3559cf84e263a1285c6e68772c8abf3fe',files});
 write('CANDIDATE_SOURCE_SHA256.json',[...candidate,...tools].sort().map(p=>({path:rel(p),sha256:hash(p),kind:p.includes('/browser/')?'BROWSER_HARNESS':'CANDIDATE_RUNTIME_OR_TOOLING'})));
 console.log(JSON.stringify({status:'PASS_OFFLINE_SOURCE_AND_HISTORY',acceptedPins:682,handoffPaths:129,releaseHash:hash(path.join(__dirname,'CANDIDATE_RELEASE.json')),runtimeAcceptance:'NOT_RUN'}));
}
module.exports={verify,hash,read,walk};
if(require.main===module){try{if(process.argv[2]==='seal')seal();else {const {audit,handoff}=verify();console.log(JSON.stringify({status:audit.status,acceptedPins:audit.acceptedSourcePins,handoffPaths:handoff.length}));}}catch(e){console.error(e.stack);process.exitCode=1;}}
