// Explicit live-only runner: no page.route, fixtures-as-responses, or fake sessions.
import https from 'node:https';
import fs from 'node:fs';
import path from 'node:path';
import {spawn} from 'node:child_process';
import assert from 'node:assert/strict';
import {chromium,expect as baseExpect} from '@playwright/test';
const expect=baseExpect.configure({timeout:120000});
const output=path.resolve(process.argv[2]||'');
const environment=path.resolve(process.argv[3] || path.join(output,'environment.json'));
const env=JSON.parse(fs.readFileSync(environment,'utf8'));
if(!env.success||!env.createdByThisRun||!env.syntheticOnly||!env.owner)throw Error('Isolation receipt required');
const runtime=JSON.parse(fs.readFileSync(path.join(output,'runtime.private.json'),'utf8'));
const harness=path.resolve('../scripts/verification/ChecklistLive/bin/Debug/net10.0/ChecklistLive.dll');
const cert=fs.readFileSync(path.join(output,'runtime.private.cert.pem'));
const key=fs.readFileSync(path.join(output,'runtime.private.key.pem'));
const dist=path.resolve('dist');if(!fs.existsSync(path.join(dist,'index.html')))throw Error('Built frontend required');
const report={database:env.database,owner:env.owner,origin:runtime.origin,api:runtime.api,live:true,httpInterception:false,cases:[],http:[],success:false};
async function sql(mode){
  await new Promise((resolve,reject)=>{const child=spawn('dotnet',[harness,mode,environment,output],{windowsHide:true,stdio:['ignore','pipe','pipe']});
    child.stdout.resume();child.stderr.resume();const timer=setTimeout(()=>{child.kill();reject(Error(`Owned SQL ${mode} timeout`));},60000);
    child.on('error',error=>{clearTimeout(timer);reject(error);});child.on('close',code=>{clearTimeout(timer);code===0?resolve():reject(Error(`Owned SQL ${mode} failed; exit ${code}`));});
  });
}
await sql('probe');
const proxy=https.createServer({cert,key},async(req,res)=>{
  if(req.url.startsWith('/api/')){
    // Real HTTP forwarding, no fabricated responses. Probe ownership before explicit unsafe requests.
    if(!['GET','HEAD','OPTIONS'].includes(req.method)){try{await sql('probe');}catch{res.writeHead(503);res.end();return;}}
    const upstream=https.request(new URL(req.url,runtime.api),{method:req.method,ca:cert,headers:req.headers},response=>{
      report.http.push({method:req.method,path:req.url,status:response.statusCode});
      res.writeHead(response.statusCode,response.headers);response.pipe(res);
    });
    upstream.on('error',()=>{if(!res.headersSent)res.writeHead(502);res.end();});req.pipe(upstream);return;
  }
  const pathname=decodeURIComponent(new URL(req.url,runtime.origin).pathname);
  let file=path.resolve(dist,'.'+pathname);
  if(!file.startsWith(dist+path.sep)&&file!==dist){res.writeHead(403);res.end();return;}
  if(!fs.existsSync(file)||fs.statSync(file).isDirectory())file=path.join(dist,'index.html');
  const mime={'.html':'text/html','.js':'text/javascript','.css':'text/css','.svg':'image/svg+xml','.png':'image/png'};
  res.writeHead(200,{'Content-Type':mime[path.extname(file)]||'application/octet-stream','Cache-Control':'no-store'});fs.createReadStream(file).pipe(res);
});
await new Promise(resolve=>proxy.listen(7448,'127.0.0.1',resolve));
const browser=await chromium.launch({executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'});
const context=await browser.newContext({ignoreHTTPSErrors:true});const page=await context.newPage();page.setDefaultTimeout(120000);
async function check(name,action){try{await action();report.cases.push({name,result:'PASS'});console.log('PASS '+name);}catch(error){report.cases.push({name,result:'FAIL',error:String(error.message).slice(0,1200)});throw error;}}
async function request(p,method,url,body,etag,csrf=true){return p.evaluate(async({method,url,body,etag,csrf})=>{
  const headers={Accept:'application/json'};if(body!==undefined)headers['Content-Type']='application/json';if(etag)headers['If-Match']=etag;
  if(csrf&&method!=='GET'){const token=await(await fetch('/api/auth/csrf',{credentials:'include'})).json();headers[token.headerName]=token.csrfToken;}
  const response=await fetch(url,{method,headers,body:body===undefined?undefined:JSON.stringify(body),credentials:'include'});
  return {status:response.status,etag:response.headers.get('etag'),body:await response.json().catch(()=>null)};
},{method,url,body,etag,csrf});}
async function login(p,username){const response=await request(p,'POST','/api/auth/login',{username,password:runtime.password});assert.equal(response.status,200);assert.equal((await request(p,'GET','/api/auth/session')).status,200);}
const root=`/api/branches/${runtime.branch}/inspection-checklists`;
let mainId,route;
async function transition(action){await page.getByRole('button',{name:action==='publish'?'Công bố Draft':'Ngừng sử dụng bản công bố',exact:true}).click();const form=page.getByRole('form',{name:'Xác nhận phiên bản'});await form.getByLabel('Lý do',{exact:true}).fill('Live integration '+action);await form.getByLabel('Tôi xác nhận thao tác').check();const response=page.waitForResponse(r=>r.request().method()==='POST' && r.url().endsWith('/'+action),{timeout:120000});await form.getByRole('button',{name:'Xác nhận',exact:true}).click();assert.equal((await response).status(),200);await expect(page.getByText('Đã lưu thay đổi trên server.',{exact:true})).toBeVisible();}
try{
  await page.goto(runtime.origin+`/checklists?branchId=${runtime.branch}`);
  await login(page,'int.hk');await page.reload();
  await check('resume: published version 2 persisted after previous harness timeout',async()=>{
    const list=await request(page,'GET',root);mainId=list.body.items.find(i=>i.code==='INT_MAIN').id;route=root+'/'+mainId;
    await page.getByRole('button',{name:'Mở INT_MAIN',exact:true}).click();
    await expect(page.getByRole('heading',{name:'Phiên bản 2 — PUBLISHED',exact:true})).toBeVisible();
    await expect(page.getByRole('heading',{name:'Phiên bản 1 — RETIRED',exact:true})).toBeVisible();
  });
  await check('resume: Retire through UI, refresh collection and reload persisted history',async()=>{
    await transition('retire');await expect(page.getByRole('heading',{name:'Phiên bản 2 — RETIRED',exact:true})).toBeVisible();
    await page.reload();await page.getByRole('button',{name:'Mở INT_MAIN',exact:true}).click();
    await expect(page.getByRole('heading',{name:'Phiên bản 2 — RETIRED',exact:true})).toBeVisible();
    await expect(page.getByText('Edited Draft',{exact:true})).toBeVisible();await expect(page.getByText('Next version',{exact:true})).toBeVisible();
  });
  await check('live search/status/active/RoomType filtering and server pagination',async()=>{
    for(let i=0;i<20;i++)assert.equal((await request(page,'POST',root,{code:`INT_PAGE_${i}`,name:`Synthetic page ${i}`,roomTypeIds:[runtime.typeId],criteria:[{text:'Synthetic',group:null,mandatory:true}]})).status,201);
    await page.getByRole('button',{name:'Tải lại danh sách',exact:true}).click();await expect(page.getByText('Tổng cộng 21 checklist · Trang 1/2')).toBeVisible();await page.getByRole('button',{name:'Trang sau',exact:true}).click();await expect(page.getByText('Tổng cộng 21 checklist · Trang 2/2')).toBeVisible();
    await page.getByLabel('Tìm mã hoặc tên').fill('INT_MAIN');await page.getByRole('combobox',{name:/Hoạt động/}).selectOption('true');await page.getByLabel('Trạng thái phiên bản').selectOption('RETIRED');await page.getByLabel('ID loại phòng',{exact:true}).fill(String(runtime.typeId));await page.getByRole('button',{name:'Áp dụng bộ lọc',exact:true}).click();await expect(page.getByText('Tổng cộng 1 checklist · Trang 1/1')).toBeVisible();await expect(page.getByRole('row').filter({has:page.getByRole('button',{name:'Mở INT_MAIN',exact:true})})).toContainText('RETIRED');
  });
  await check('CSRF rejection is real and does not insert checklist',async()=>{assert.equal((await request(page,'POST',root,{code:'INT_NO_CSRF'},undefined,false)).status,400);});
  await check('branch isolation and actual frontend forbidden state',async()=>{assert.equal((await request(page,'GET',`/api/branches/${runtime.foreign}/inspection-checklists`)).status,403);await page.goto(runtime.origin+`/checklists?branchId=${runtime.foreign}`);await expect(page.getByRole('alert').first()).toBeVisible();await expect(page.getByRole('button',{name:'Tạo checklist',exact:true})).toBeDisabled();});
  await check('real denied user gets 403; anonymous browser gets 401',async()=>{
    const other=await browser.newContext({ignoreHTTPSErrors:true});const p=await other.newPage();await p.goto(runtime.origin+`/checklists?branchId=${runtime.branch}`);assert.equal((await request(p,'GET',root)).status,401);await login(p,'int.denied');assert.equal((await request(p,'GET',root)).status,403);await other.close();
  });
  await check('independent SQL persistence and historical FK verification',async()=>await sql('verify'));
  report.success=true;
}catch(error){report.error=String(error.message).slice(0,1200);console.error('FAIL '+report.error);process.exitCode=1;}
finally{await fs.promises.writeFile(path.join(output,'live-results.json'),JSON.stringify(report,null,2));await browser.close();proxy.closeAllConnections();await new Promise(resolve=>proxy.close(resolve));}