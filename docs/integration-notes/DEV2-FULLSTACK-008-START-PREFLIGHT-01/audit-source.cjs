// Offline file/Git audit; no SQL, tests, runtime or writes to old evidence.
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto'),cp=require('node:child_process'),assert=require('node:assert/strict');
const root=path.resolve(__dirname,'../../..');
const read=p=>JSON.parse(fs.readFileSync(p,'utf8').replace(/^\uFEFF/,''));
const hash=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const git=(...a)=>cp.execFileSync('git',a,{cwd:root,encoding:'utf8'});
const rel=p=>path.relative(root,p).replaceAll('\\','/');
function verify(){
 const branch=git('branch','--show-current').trim(),head=git('rev-parse','HEAD').trim();assert.equal(branch,'demo/dev2-fullstack-20261110');assert.equal(head,'7067523e030eacd46213d8e87b86e4bad4c97e49');
 const p007=path.join(root,'docs/integration-notes/DEV2-FULLSTACK-007-RUNTIME'),pinFile=path.join(p007,'CHECKPOINT007_SOURCE_SHA256.json'),pins=read(pinFile);assert.equal(pins.length,682);
 for(const p of pins)assert.equal(hash(path.join(root,p.path)),p.sha256,'Accepted007 source drift: '+p.path);
 const acceptance=require(path.join(p007,'review-runtime.cjs')).review(path.join(p007,'host-receipts/RUNTIME007_20261114_102625_ce3cc638'));
 assert.equal(acceptance.receiptManifestSha256,'ba068cd4a2439d379c20b6411f196bb2159a83c8ceceedcbbeaaff73e77861ad');
 const recovery=path.join(root,'docs/integration-notes/DEV2-FULLSTACK-008-RECOVERY001');
 const evidence=[['OWNER',path.join(recovery,'owner-disposition/receipts/OWNER_A_20261114122301282_72e134cf'),'5e046346e0cf83c70d030c830e6d6e7b8ddabc15c505e470ec34a98519f60fe4'],['POSTEXIT04',path.join(recovery,'post-exit/completed-cleanup-analysis/receipts/CLASSIFY_POSTEXIT04_20261114114639926_a6ada7ad'),'749b674707da8af963bd8509589a7ccae7d390280eee593e76c4291db1e686c7'],['POSTEXIT05',path.join(recovery,'final-historical-gap-review/receipts/FINAL_POSTEXIT05_20261114120948539_a174e784'),'ba3c7186f0ccaaacc06af43041d99a9a365e2b748acf699e36a5f7b1aa9e8231']];
 const final=read(path.join(evidence[2][1],'FINAL_HISTORICAL_EVIDENCE_REVIEW.json'));
 evidence.push(['SQL_CAPTURE',path.join(root,final.sourceReferences.successfulSqlCapture),final.sourceReferences.successfulSqlManifestSha256]);
 const verifiedEvidence=[];
 for(const [name,dir,expected] of evidence){const manifest=path.join(dir,'RECEIPT_SHA256.json');assert.equal(hash(manifest),expected);const list=read(manifest);for(const p of list){assert.equal(path.basename(p.file),p.file);assert.equal(hash(path.join(dir,p.file)),p.sha256)}verifiedEvidence.push({name,directory:rel(dir),manifestSha256:expected,verifiedFiles:list.length})}
 const owner=read(path.join(evidence[0][1],'OWNER_DECISION.json'));assert.equal(owner.decision,'A_SCOPED_ADMINISTRATIVE_CLOSURE');assert.equal(owner.disposition.CHECKPOINT008_START_NOW,'NO');assert.equal(owner.disposition.TECHNICAL_GATE_WAIVER,'NONE');
 const failed=read(path.join(p007,'host-receipts/RUNTIME007_20261114_140237_b8516b4a/HOST_RUN_SUMMARY.json'));assert.equal(failed.status,'FAIL');
 const initial=read(path.join(root,'docs/integration-notes/DEV2-FULLSTACK-008-READINESS/WORKTREE_SNAPSHOT.json'));assert.equal(initial.counts.trackedDirty,27);assert.equal(initial.counts.untracked,5427);
 const prior=new Map(initial.files.map(p=>[p.path,p]));
 const kind=p=>p.startsWith('src/')||p.startsWith('frontend/src/')?'PRODUCT_SOURCE':p.startsWith('scripts/')||p.startsWith('frontend/tests/')||/\.(cjs|mjs|ps1)$/.test(p)?'TEST_HARNESS_OR_TOOLING':/\.(zip|patch|diff|bak)$/.test(p)?'GENERATED_ARTIFACT':'HISTORICAL_OR_PREPARATION_EVIDENCE';
 const sensitive=p=>/\.(pfx|p12|pem|key|bak)$/i.test(p)||/(^|\/)(secrets\.json|\.env(\..*)?)$/i.test(p)&&!p.endsWith('.env.example');
 const inventory=git('status','--porcelain=v1','-z','--untracked-files=all').split('\0').filter(Boolean).map(line=>{
  const status=line.slice(0,2),p=line.slice(3);assert([' M','??'].includes(status),'Unexpected staged/deleted/renamed entry');
  const s=fs.lstatSync(path.join(root,p));assert(!s.isSymbolicLink(),'Symlink requires review');
  const sha=sensitive(p)?null:hash(path.join(root,p));return {path:p,status,category:kind(p),sha256:sha,sensitiveContentNotRead:sensitive(p),sinceInitial008:!prior.has(p)?'ADDED':sha&&prior.get(p).sha256===sha?'UNCHANGED':'CHANGED_OR_NOT_HASHED'};
 });
 const pinned=new Set(pins.map(p=>p.path)),unexpectedRuntimeInputs=[];
 function walk(dir){for(const e of fs.readdirSync(path.join(root,dir),{withFileTypes:true})){const p=dir+'/'+e.name;if(e.isSymbolicLink()){unexpectedRuntimeInputs.push(p);continue}if(e.isDirectory()){if(!['bin','obj','node_modules','dist'].includes(e.name))walk(p)}else if(/\.(cs|csproj|props|targets|tsx?|jsx?|mjs|cjs|json|css|html)$/.test(p)&&!pinned.has(p))unexpectedRuntimeInputs.push(p)}}
 for(const dir of ['src','frontend/src','scripts/verification/Dev2Runtime007','scripts/verification/Dev2AllActions','scripts/verification/Dev2FinalHost'])walk(dir);
 const supplemental=[];
 for(const dir of ['','frontend/'])for(const e of fs.readdirSync(path.join(root,dir),{withFileTypes:true}))if(e.isFile()){
  const p=dir+e.name;if(e.name.startsWith('.env')&&e.name!=='.env.example'&&!pinned.has(p))unexpectedRuntimeInputs.push(p);
  if(/^(global\.json|Directory\..*\.(props|targets)|[Nn]u[Gg]et\.[Cc]onfig|package(-lock)?\.json|tsconfig.*\.json|vite\.config\..*|index\.html)$/.test(e.name)&&!pinned.has(p)){
   let same=false;try{same=git('rev-parse','HEAD:'+p).trim()===git('hash-object','--path='+p,p).trim()}catch{}
   if(!same)unexpectedRuntimeInputs.push(p);else supplemental.push({path:p,sha256:hash(path.join(root,p)),authority:'UNCHANGED_ACCEPTED_HEAD_BLOB'});
  }
 }
 assert.equal(unexpectedRuntimeInputs.length,0,'Unpinned implicit build/runtime source');
 const counts={all:inventory.length,trackedModified:inventory.filter(p=>p.status===' M').length,untracked:inventory.filter(p=>p.status==='??').length,staged:0};
 const categories=Object.fromEntries(['PRODUCT_SOURCE','TEST_HARNESS_OR_TOOLING','GENERATED_ARTIFACT','HISTORICAL_OR_PREPARATION_EVIDENCE'].map(k=>[k,inventory.filter(p=>p.category===k).length]));
 return {status:'PASS_SOURCE_AND_HISTORICAL_EVIDENCE_ONLY',utc:new Date().toISOString(),branch,head,acceptedSourcePins:682,acceptedManifestSha256:hash(pinFile),acceptedSourceIdentity:crypto.createHash('sha256').update(JSON.stringify(pins)).digest('hex'),acceptedPinnedSourceChanges:[],unpinnedRuntimeInputs:unexpectedRuntimeInputs,supplementalBuildInputs:supplemental,initial008Counts:initial.counts,currentCounts:counts,categories,inventory,additionalSinceInitial008:inventory.filter(p=>p.sinceInitial008!=='UNCHANGED'),verifiedEvidence,acceptedRegressionBaseline:acceptance,buildAndRegressionScope:'INHERITED_SEALED_007_SOURCE_IDENTICAL_NOT_FRESH_BUILD_OR_VULNERABILITY_ASSESSMENT',ownerDecision:'A_SCOPED_ADMINISTRATIVE_CLOSURE',dev1Acknowledgment:owner.owner.dev1MasterScopeAcknowledgment,executionSignoff:'NOT_GRANTED',historicalHostStatus:'FAIL_PRESERVED',historicalSharedPreservation:'NOT_INDEPENDENTLY_CERTIFIED',sqlConnections:0,runtimeStarted:false};
}
module.exports={verify};
if(require.main===module){try{const output=process.argv[2];assert(output,'New output path required');const result=verify();fs.writeFileSync(output,JSON.stringify(result,null,2)+'\n',{flag:'wx'});console.log(JSON.stringify({status:result.status,pins:682,counts:result.currentCounts,categories:result.categories,acceptedSourceChanges:0}));}catch(e){console.error(e.message);process.exitCode=1}}
