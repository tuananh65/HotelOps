const fs=require('fs'),path=require('path'),crypto=require('crypto');
const sha=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const read=p=>JSON.parse(fs.readFileSync(p,'utf8').replace(/^\uFEFF/,''));
const requireGate=(x,n)=>{if(!x)throw Error('007 acceptance gate failed: '+n)};
function review(directory){
 const root=path.resolve(__dirname,'host-receipts')+path.sep,dir=path.resolve(directory);
 requireGate(dir.startsWith(root)&&/^RUNTIME007_/.test(path.basename(dir)),'independent007 receipt directory');
 const file=n=>path.join(dir,n),r=n=>read(file(n+'.json'));
 const manifest=r('RECEIPT_SHA256');for(const x of manifest){requireGate(path.basename(x.file)===x.file&&sha(file(x.file))===x.sha256,'receipt hash');}
 const repo=path.resolve(__dirname,'../../..');
 for(const x of read(path.join(__dirname,'CHECKPOINT007_SOURCE_SHA256.json')))requireGate(sha(path.join(repo,x.path))===x.sha256,'current source seal');
 for(const x of read(path.join(__dirname,'BASELINE_PRESERVATION.json')))requireGate(sha(path.join(repo,x.path))===x.sha256,'003-006 preservation');
 const guid='6e68f686-d7c1-4002-94d9-a58fa73175da';
 const host=r('HOST_RUN_SUMMARY'),run=r('CHECKPOINT007_RUNTIME'),clean=r('FIXTURE_CLEANUP'),api=r('NORMAL_API_STARTUP'),front=r('FRONTEND_STARTUP'),pre=r('SQL_ENVIRONMENT_PREFLIGHT'),backup=r('BACKUP_AND_ROLLBACK');
 requireGate(host.mode==='Acceptance'&&run.mode==='acceptance','interactive is not acceptance');
 requireGate(host.apiStopped===true&&host.frontendStopped===true&&host.SQL_E2E==='PASS_CHECKPOINT007_SELECTED_WORKFLOWS','controlled shutdown');
 requireGate(run.status==='PASS'&&run.browserPositive&&run.browserNegativeNoEffects&&run.cleanup&&run.sqlBacked&&run.credentialsPersisted===false&&run.databaseGuid===guid,'independent runtime result');
 requireGate(clean.status==='PASS'&&clean.remainingRoots===0&&clean.databaseGuid===guid,'fixture cleanup');
 requireGate(api.hooksDisabled===true&&api.authorizationBypass===false&&front.tlsVerificationDisabled===false,'standard Cookie authority and TLS');
 requireGate(pre.status==='PASS'&&pre.identity.database_name==='HotelOps_DEV2_FullStack_Trial'&&pre.identity.database_guid.toLowerCase()===guid&&pre.identity.machine_name==='KIENPC','exact SQL identity');
 requireGate(backup.backupVerified&&backup.restoreVerifyOnly==='PASS'&&backup.databaseGuid.toLowerCase()===guid,'backup');
 for(const n of ['solution-build','host-harness-build','all61-harness-build','EF-schema-check','frontend-typecheck','frontend-build','frontend-regressions','runtime007-SQL'])requireGate(Number(fs.readFileSync(file(n+'.exitcode'),'utf8'))===0&&r(n).exitCode===0,'native exit '+n);
 for(const phase of ['positive','negative']){
  const b=r('BROWSER_'+phase);requireGate(b.status==='PASS'&&b.checks.length>0&&b.checks.every(x=>x.pass===true&&(x.expectedStatus===undefined||x.expectedStatus===x.actualStatus)),'browser '+phase);
  requireGate(r('BROWSER_EXIT_'+phase).exitCode===0,'browser child exit');
 }
 requireGate(r('BROWSER_negative').checks.some(x=>x.name==='anonymous business API rejected401'&&x.actualStatus===401),'anonymous business401');
 const sql=r('BROWSER_SQL_EFFECTS');requireGate(sql.status==='PASS'&&Object.keys(sql.checks).length===11&&Object.values(sql.checks).every(x=>x===true),'11 independent SQL checks');
 const negative=r('NEGATIVE_SQL_NO_EFFECTS');requireGate(negative.status==='PASS'&&negative.businessAndAuditUnchanged===true,'negative no effects');
 const ref=r('FIXTURE_REFERENCE_PRESERVATION');requireGate(ref.status==='PASS'&&ref.borrowedPriorityPreserved&&ref.remainingCreatedReferences===0,'shared references');
 const bytes=fs.readFileSync(file('frontend-regressions.log')),log=bytes.toString(bytes[0]===255&&bytes[1]===254?'utf16le':'utf8');
 const counts=Object.fromEntries([...log.matchAll(/^# (tests|pass|fail|cancelled|skipped) (\d+)\s*$/gm)].map(m=>[m[1],Number(m[2])]));
 requireGate(counts.tests===267&&counts.pass===267&&counts.fail===0&&counts.cancelled===0&&counts.skipped===0,'267 serial regressions');
 return {verdict:'PASS_SELECTED_RUNTIME_WORKFLOWS',CHECKPOINT007:'ACCEPTED_SELECTED_RUNTIME_WORKFLOWS',receiptDirectory:dir,databaseGuid:guid,sourceSeals:'PASS',browser:'PASS',sqlChecks:11,negativeSecurity:'PASS',cleanup:'PASS_0_ROOTS',processShutdown:'PASS_HOST_RECEIPT',frontendRegressions:counts,excludedFeatures:'All Option A exclusions retained; not112-action UI certification or production acceptance',receiptManifestSha256:sha(file('RECEIPT_SHA256.json'))};
}
module.exports={review};
if(require.main===module){try{const result=review(process.argv[2]||'');fs.writeFileSync(path.join(__dirname,'FINAL_CHECKPOINT007_ACCEPTANCE.json'),JSON.stringify(result,null,2)+'\n');console.log(JSON.stringify(result));}catch{console.error('Checkpoint007 acceptance remains pending; inspect exact receipts locally.');process.exitCode=1;}}
