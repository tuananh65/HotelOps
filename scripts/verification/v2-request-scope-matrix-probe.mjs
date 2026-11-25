import assert from 'node:assert/strict';
import {randomUUID} from 'node:crypto';
import {writeFile} from 'node:fs/promises';

// Caller owns the housekeeping fixture/API and supplies its actual HTTP client
// request(alias,path,method,body) and SQL Inspect callback. No fixture is created
// here and no response is mocked. All supplied actors must be branch-scoped.
export async function verifyRequestScopeMatrix({fixture,foreignBranch,request,inspect,directory,ordinaryRequest,managedRequests=[]}) {
  assert.ok(Number.isSafeInteger(foreignBranch)&&foreignBranch!==fixture.branch);
  const aliases=['manager','hk','peer','supervisor','lead','technician','reception','store'];
  for(const alias of aliases)assert.ok(fixture.actors.some(a=>a.alias===alias),'Missing actor '+alias);
  const report={at:new Date().toISOString(),status:'RUNNING',checks:[],limits:'Eight synthetic branch-scoped actors, actual HTTP and caller SQL snapshot. No SYS_ADMIN/global-role coverage, no password login, no browser or SignalR. Managed-work probes run only for explicitly supplied targets.'};
  const before=await inspect();
  try {
    for(const alias of aliases){
      for(const path of ['/requests?branchId='+foreignBranch+'&queue=all','/requests/types?branchId='+foreignBranch]){
        const result=await request(alias,path);
        assert.equal(result.status,403,alias+' must not read foreign request scope: '+path);
        report.checks.push({actor:alias,path,status:'PASS',httpStatus:result.status});
      }
    }
    if(ordinaryRequest){
      const detail=await request('manager','/requests/'+ordinaryRequest.workId);
      assert.equal(detail.status,200);assert.deepEqual(detail.data.actions,[]);
      for(const action of ['accept','approve','reject','cancel','complete']){
        const result=await request('manager','/work/'+ordinaryRequest.workId+'/actions/'+action,'POST',{
          rowVersion:ordinaryRequest.rowVersion,operationKey:randomUUID(),reason:'TEST denied ordinary request mutation'});
        assert.equal(result.status,403,'OM ordinary action '+action);
        report.checks.push({actor:'manager',family:'GENERAL_INTERNAL_REQUEST',action,status:'PASS',httpStatus:result.status});
      }
    }
    // A typed workflow must not be completed through legacy WorkService actions.
    // Callers supply a legitimate current actor/version to avoid merely testing
    // an unrelated stale-version or permission rejection.
    for(const target of managedRequests){
      assert.ok(['CONSUMABLE_REFILL_REQUEST','WAREHOUSE_REPLENISHMENT_REQUEST','DURABLE_ITEM_REMEDIATION','ROOM_REINSPECTION'].includes(target.type));
      assert.ok(aliases.includes(target.actor)&&Number.isSafeInteger(target.workId)&&target.rowVersion);
      const result=await request(target.actor,'/work/'+target.workId+'/actions/complete','POST',{
        rowVersion:target.rowVersion,operationKey:randomUUID(),reason:'TEST typed workflow cannot bypass domain'});
      assert.equal(result.status,403,'Managed workflow bypass must be rejected');
      assert.match(JSON.stringify(result.data),/USE_REQUEST_WORKFLOW/);
      report.checks.push({actor:target.actor,family:target.type,action:'legacy-complete-bypass',status:'PASS',httpStatus:result.status});
    }
    assert.deepEqual(await inspect(),before,'Denied operations must preserve the full caller SQL snapshot');
    report.status='PASS';return report;
  }catch(error){report.status='FAIL';report.error=String(error.message).replace(/[a-f\d]{64}/gi,'[redacted]');throw error;}
  finally{await writeFile(directory+'/request-scope-matrix-results.json',JSON.stringify(report,null,2));}
}
