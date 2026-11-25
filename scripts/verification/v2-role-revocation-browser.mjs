import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {writeFile} from 'node:fs/promises';
const require=createRequire(new URL('../../frontend/package.json',import.meta.url));
const {chromium,expect}=require('@playwright/test');

// Call only for an activated account created by the current invitation fixture.
// request is the invitation runner's real HTTP client; no mocked API responses.
export async function verifyRoleRevocation({fixture,user,password,request,directory}) {
 assert.ok(user.username.startsWith(fixture.prefix+'_'),'Owned synthetic account required');
 const report={at:new Date().toISOString(),userId:user.id,checks:[],limits:'TEST account only; HTTP role mutation and two browser sessions. No SignalR or SQL timestamp evidence in this helper.'};
 const browser=await chromium.launch({executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',headless:true});
 const contexts=[];
 const check=(name)=>report.checks.push({name,status:'PASS'});
 try {
  const catalog=await request('/catalog/roles?pageSize=100');assert.equal(catalog.status,200);
  const role=catalog.data.items.map(x=>x.fields).find(x=>x.code==='HK_STAFF'&&x.isActive);
  assert.ok(role,'Active HK_STAFF role required');
  const details=await request('/users/'+user.id);assert.equal(details.status,200);
  assert.ok(!details.data.roles.some(x=>x.roleId===role.roleId),'Fresh account must not already have selected role');
  async function loginPair(){
   const pair=[];
   for(let i=0;i<2;i++){
    const context=await browser.newContext({viewport:{width:i?390:1280,height:844}});contexts.push(context);
    await context.route('**/api/**',async route=>{
     const url=new URL(route.request().url());
     if(url.pathname.startsWith('/api/v2/realtime/'))return route.abort();
     url.hostname='127.0.0.1';url.port='5778';url.protocol='http:';
     try{await route.fulfill({response:await route.fetch({url:url.toString()})});}catch{await route.abort().catch(()=>{});}
    });
    await context.routeWebSocket('**/*',socket=>socket.close());
    const page=await context.newPage();await page.goto('http://127.0.0.1:5174/login');
    await page.getByLabel('Tên đăng nhập',{exact:true}).fill(user.username);
    await page.locator('#login-password').fill(password);
    const response=page.waitForResponse(r=>new URL(r.url()).pathname==='/api/v2/auth/login'&&r.request().method()==='POST');
    await page.getByRole('button',{name:'Đăng nhập',exact:true}).click();
    const login=await response;assert.equal(login.status(),200);const body=await login.json();
    assert.equal(body.requiresPasswordChange,false);
    assert.equal((await request('/auth/me',{token:body.accessToken})).status,200);
    pair.push({page,token:body.accessToken});
   }
   assert.notEqual(pair[0].token,pair[1].token,'Two independent sessions required');return pair;
  }
  async function expired(pair,action){
   for(const [index,{page,token}] of pair.entries()){
    assert.equal((await request('/auth/me',{token})).status,401,action+' must revoke session '+index);
    await page.reload();await expect(page.getByRole('heading',{name:'Đăng nhập',exact:true})).toBeVisible();
    assert.equal(await page.evaluate(()=>sessionStorage.getItem('hotelops.compact.session')),null);
    await page.screenshot({path:directory+'/role-'+action+'-session-'+index+'.png',fullPage:true});
   }
   check(action+' invalidates both tokens and browser sessions');
  }
  const beforeGrant=await loginPair();
  const granted=await request('/users/'+user.id+'/roles',{method:'POST',body:{roleId:role.roleId,branchId:fixture.branch}});
  assert.equal(granted.status,200);check('Admin grants branch role through actual API');
  await expired(beforeGrant,'grant');
  const beforeRevoke=await loginPair();
  const duplicate=await request('/users/'+user.id+'/roles',{method:'POST',body:{roleId:role.roleId,branchId:fixture.branch}});
  assert.equal(duplicate.status,409);
  for(const session of beforeRevoke)assert.equal((await request('/auth/me',{token:session.token})).status,200);
  check('Rejected duplicate grant preserves both active sessions');
  const detail=await request('/users/'+user.id);assert.equal(detail.status,200);
  const assignment=detail.data.roles.find(x=>x.roleId===role.roleId&&x.branchId===fixture.branch);assert.ok(assignment);
  const stale=await request('/users/role-assignments/'+assignment.userRoleId+'/revoke',{method:'POST',body:{rowVersion:Buffer.alloc(8).toString('base64')}});
  assert.equal(stale.status,409);
  for(const session of beforeRevoke)assert.equal((await request('/auth/me',{token:session.token})).status,200);
  assert.equal((await request('/auth/me')).status,200,'Administrator session must remain valid');
  check('Stale revoke preserves both sessions and the unrelated administrator');
  const revoked=await request('/users/role-assignments/'+assignment.userRoleId+'/revoke',{method:'POST',body:{rowVersion:assignment.rowVersion}});
  assert.equal(revoked.status,200);check('Admin revokes branch role through actual API');
  await expired(beforeRevoke,'revoke');
  const final=await request('/users/'+user.id);assert.equal(final.status,200);
  assert.ok(!final.data.roles.some(x=>x.userRoleId===assignment.userRoleId));
  // Never reconfigure a shared role. The fixture cleanup deactivates this exact
  // generated code because the catalog deliberately does not expose IsActive.
  const createdRole=await request('/catalog/roles',{method:'POST',body:{fields:{code:fixture.prefix+'_CONFIG',name:'TEST role configuration rollback'}}});
  assert.equal(createdRole.status,200);
  const testRole=createdRole.data.fields;
  assert.ok(Number.isSafeInteger(testRole.roleId));
  const assignedTest=await request('/users/'+user.id+'/roles',{method:'POST',body:{roleId:testRole.roleId,branchId:fixture.branch}});
  assert.equal(assignedTest.status,200);
  const beforeConfigure=await loginPair();
  const prior=await request('/users/roles/'+testRole.roleId+'/permissions');assert.equal(prior.status,200);
  const permissions=await request('/catalog/permissions?pageSize=100');assert.equal(permissions.status,200);
  const safePermission=permissions.data.items.map(x=>x.fields).find(x=>x.code==='USER_VIEW'&&x.isActive);
  assert.ok(safePermission,'Safe existing permission required');
  const invalid=await request('/users/roles/'+testRole.roleId+'/permissions',{method:'PUT',body:[{permissionId:-1,dataScope:'OWN'}]});
  assert.equal(invalid.status,409,'Nonexistent permission must roll back the transaction');
  const afterInvalid=await request('/users/roles/'+testRole.roleId+'/permissions');assert.equal(afterInvalid.status,200);
  assert.deepEqual(afterInvalid.data,prior.data,'Rejected configuration must preserve all grants');
  for(const session of beforeConfigure)assert.equal((await request('/auth/me',{token:session.token})).status,200);
  check('Invalid permission FK rolls back configuration and preserves both sessions');
  const configured=await request('/users/roles/'+testRole.roleId+'/permissions',{method:'PUT',body:[{permissionId:safePermission.permissionId,dataScope:'OWN'}]});
  assert.equal(configured.status,200);
  await expired(beforeConfigure,'configure');
  assert.equal((await request('/auth/me')).status,200,'Unrelated administrator survives permission configuration');
  const grants=await request('/users/roles/'+testRole.roleId+'/permissions');assert.equal(grants.status,200);
  assert.ok(grants.data.permissions.some(x=>x.permissionId===safePermission.permissionId&&x.dataScope==='OWN'));
  check('Successful dedicated role configuration commits grant and leaves administrator session valid');
  report.status='PASS';return report;
 }catch(error){
  report.status='FAIL';report.error=String(error.message).split(password).join('[redacted]').replace(/[a-f\d]{64}/gi,'[redacted]');
  throw new Error(report.error);
 }finally{
  await writeFile(directory+'/role-change-browser-results.json',JSON.stringify(report,null,2));
  await browser.close();
 }
}
