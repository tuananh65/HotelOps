// Read-only independent candidate safety review. Does not execute the worker.
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict'),crypto=require('node:crypto');
const hash=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const textHash=s=>crypto.createHash('sha256').update(s).digest('hex');
const read=p=>JSON.parse(fs.readFileSync(p,'utf8').replace(/^\uFEFF/,''));
function manifest(dir,name){const pins=read(path.join(dir,name));assert(Array.isArray(pins)&&pins.length);const names=new Set();for(const p of pins){assert(typeof p.file==='string'&&!path.isAbsolute(p.file)&&!p.file.split(/[\\/]/).includes('..'));assert(!names.has(p.file));names.add(p.file);assert.equal(hash(path.join(dir,p.file)),p.sha256)}return hash(path.join(dir,name))}
function review(dir){
 const parentManifest=manifest(dir,'RECEIPT_SHA256.json'),workerDir=path.join(dir,'worker'),workerManifest=manifest(workerDir,'CANDIDATE_RECEIPT_SHA256.json');
 const host=read(path.join(dir,'HOST_SAFETY_SUMMARY.json')),result=read(path.join(workerDir,'CANDIDATE_SAFETY_RESULT.json'));
 assert.equal(host.status,'PASS_CANDIDATE_SAFETY_REQUIRES_INDEPENDENT_REVIEW');assert.equal(host.workerNativeExit,0);assert.equal(host.ownedRuntimeRootsRemaining,0);assert.equal(result.status,'PASS_CANDIDATE_SAFETY_REQUIRES_REVIEW');
 assert.equal(Number(fs.readFileSync(path.join(dir,'worker.exitcode'),'utf8').replace(/^\uFEFF/,'')),0);
 assert.equal(result.checkpoint008DemoAccepted,false);
 const journalDir=path.join(workerDir,'journal');assert(!fs.readdirSync(journalDir).some(n=>n.endsWith('.pending')));
 let previous='GENESIS',seq=0;const events=fs.readdirSync(journalDir).filter(n=>n.endsWith('.event.json')).sort().map(n=>{const outer=read(path.join(journalDir,n));assert.equal(textHash(outer.body),outer.hash);const e=JSON.parse(outer.body);assert.equal(e.sequence,++seq);assert.equal(e.previous,previous);previous=outer.hash;return e});
 const one=kind=>{const a=events.filter(e=>e.kind===kind);assert.equal(a.length,1,kind);return a[0]};
 const base=read(path.join(workerDir,'PROTECTED_PRE_FIXTURE_SNAPSHOT.json'));
 const after=one('CLEANUP_POSTCOMMIT_SQL_SNAPSHOT').payload;
 assert.equal(after.SchemaHash,base.SchemaHash);assert.equal(Object.keys(after.Rows).length,Object.keys(base.Rows).length);
 for(const [key,row] of Object.entries(base.Rows))assert.equal(after.Rows[key]?.Hash,row.Hash,'Protected row mismatch '+key);
 const before=one('CLEANUP_LOCKED_SQL_SNAPSHOT').payload;assert.equal(before.SchemaHash,base.SchemaHash);
 const proof=one('CLEANUP_GRAPH_PROOF').payload;assert.deepEqual(proof.problems,[]);
 const removed=events.filter(e=>e.kind==='EXACT_PK_DELETE_PENDING').map(e=>e.payload.Table+':'+JSON.stringify(e.payload.Pk));assert.equal(new Set(removed).size,removed.length);assert.deepEqual([...removed].sort(),[...proof.owned].sort());
 for(const key of removed){assert(before.Rows[key]);assert(!base.Rows[key]);assert(!after.Rows[key]);}
 const absent=Object.keys(before.Rows).filter(k=>!after.Rows[k]);assert.deepEqual(absent.sort(),[...removed].sort());
 assert(one('SEED_COMMIT_ACK').sequence<one('CLEANUP_COMMIT_INTENT').sequence);assert(one('CLEANUP_COMMIT_INTENT').sequence<one('CLEANUP_COMMIT_ACK').sequence);assert(one('CLEANUP_COMMIT_ACK').sequence<one('CLEANUP_POSTCOMMIT_SQL_SNAPSHOT').sequence);assert(one('CLEANUP_INDEPENDENT_POSTCHECK_PASS').sequence<one('LEASE_RELEASE_VERIFIED').sequence);assert.equal(one('HARNESS_TERMINAL').payload.status,'PASS_CANDIDATE_SAFETY_REQUIRES_REVIEW');
 const cleanup=read(path.join(workerDir,'EXACT_PK_CLEANUP_RESULT.json'));assert.equal(cleanup.Status,'PASS_EXACT_PK_CLEANUP');assert.equal(cleanup.CommitAcknowledged,true);assert.equal(cleanup.IndependentPostcheck,true);assert.equal(cleanup.Deleted,removed.length);
 const session=read(path.join(workerDir,'SESSION.json'));assert.equal(session.DatabaseGuid,'6e68f686-d7c1-4002-94d9-a58fa73175da');assert(/^D2A[0-9a-f]{10}$/.test(session.Marker));
 const release=path.join(__dirname,'CANDIDATE_RELEASE.json');assert.equal(hash(release),session.SourceIdentity);const repo=path.resolve(__dirname,'../../..');for(const pin of read(release).files)assert.equal(hash(path.join(repo,pin.path)),pin.sha256);
 const accepted=path.join(repo,'docs/integration-notes/DEV2-FULLSTACK-007-RUNTIME/CHECKPOINT007_SOURCE_SHA256.json');assert.equal(hash(accepted),'11483526be3e092a4f7e95e7034979d3559cf84e263a1285c6e68772c8abf3fe');const pins=read(accepted);assert.equal(pins.length,682);for(const pin of pins)assert.equal(hash(path.join(repo,pin.path)),pin.sha256);
 if(result.mode==='browser-acceptance'){
  assert.equal(host.tlsFilesRemoved,true);assert.equal(one('BROWSER_NATIVE_EXIT').payload.ExitCode,0);assert(one('BROWSER_TREE_ABSENCE_VERIFIED').payload.trackedIdentities>0);assert.equal(read(path.join(dir,'OWNED_RUNTIME_SHUTDOWN.json')).status,'PASS_EXACT_IDENTITIES_ABSENT');
  const actors=new Set(events.filter(e=>e.kind==='REAL_COOKIE_CONTEXT_AUTHENTICATED').map(e=>e.payload.actor));assert.equal(actors.size,3);
  const roots=events.filter(e=>e.kind==='INTERACTIVE_RETURNED_ROOT_PK');assert.equal(roots.length,1);const r=roots[0].payload;assert.equal(r.Table,'dbo.incidents');assert.equal(r.Method,'POST');assert.equal(r.Route,'/api/incidents');assert.equal(r.Status,201);
  const key=r.Table+':'+JSON.stringify(r.Pk);assert(before.Rows[key]);assert(removed.includes(key));
  assert(Object.values(before.Rows).some(a=>a.Table==='dbo.audit_logs'&&a.Values.action==='CREATE'&&a.Values.actor_user_id===String(r.Actor)&&a.Values.entity_type==='incidents'&&a.Values.entity_id===r.Pk.incident_id));
 }else assert.equal(result.mode,'fixture-acceptance');
 return {status:'PASS_CANDIDATE_SAFETY_ONLY',mode:result.mode,session,knownOwnedDeleted:removed.length,protectedRowsUnchanged:Object.keys(base.Rows).length,parentManifestSha256:parentManifest,workerManifestSha256:workerManifest,checkpoint008DemoAccepted:false,sqlFaultInjectionAcceptance:'NOT_CERTIFIED_BY_THIS_REVIEW',historicalSharedPreservation:'UNCHANGED_NOT_RETROSPECTIVELY_CERTIFIED'};
}
module.exports={review};
if(require.main===module){try{assert(process.argv.length===4,'Exact host receipt directory and NEW review file required');const r=review(path.resolve(process.argv[2]));fs.writeFileSync(process.argv[3],JSON.stringify(r,null,2)+'\n',{flag:'wx'});console.log(r.status)}catch(e){console.error('CANDIDATE_REVIEW_BLOCKED: '+e.message);process.exitCode=1}}
