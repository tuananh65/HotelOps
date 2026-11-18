import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, readFileSync } from 'node:fs';
import { build } from 'esbuild';
import { chromium } from '@playwright/test';

const bundle = await build({ stdin: { resolveDir: process.cwd(), loader: 'tsx', contents: `
import React from 'react';import {createRoot} from 'react-dom/client';
import {AuthProvider,useAuth} from './src/auth/AuthProvider';
import {IncidentClassificationPanel} from './src/dev2/IncidentClassificationPanel';
function App(){const {state}=useAuth();return state.status==='AUTHENTICATED'?<IncidentClassificationPanel data={{incidentId:7}}/>:null;}
createRoot(document.getElementById('root')).render(<AuthProvider><App/></AuthProvider>);
` }, bundle: true, write: false, format: 'iife', jsx: 'automatic', loader: { '.css':'empty' }, define: { 'process.env.NODE_ENV':'"production"' } });
let browser;
before(async()=>{const executablePath=['C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe','C:/Program Files/Google/Chrome/Application/chrome.exe'].find(existsSync);browser=await chromium.launch({headless:true,...(executablePath?{executablePath}:{})});});
after(async()=>browser?.close());
test('Incident detail keeps one classification form after repeated save and reload',async()=>{
 const detailBundle=await build({stdin:{resolveDir:process.cwd(),loader:'tsx',contents:`
 import React from 'react';import {createRoot} from 'react-dom/client';import {BrowserRouter} from 'react-router-dom';
 import {AuthProvider,useAuth} from './src/auth/AuthProvider';import {Screen} from './src/dev2/Workspace';import {modules} from './src/dev2/contracts';
 function App(){const {state}=useAuth();return state.status==='AUTHENTICATED'?<Screen readOnly={false} module="incidents" recordId="7" actor="42" config={modules.find(x=>x.detail==='A004A-023')} context={{}} enabledActions={['A004A-023','A004A-020','INCIDENT_MANAGE']} select={()=>{}} expired={()=>{}}/>:null;}
 createRoot(document.getElementById('root')).render(<AuthProvider><BrowserRouter><App/></BrowserRouter></AuthProvider>);
 `},plugins:[{name:'test-screen-export',setup(b){b.onLoad({filter:/dev2[\\/]Workspace\.tsx$/},args=>({contents:readFileSync(args.path,'utf8')+'\nexport { Screen };',loader:'tsx'}));}}],bundle:true,write:false,format:'iife',jsx:'automatic',loader:{'.css':'empty'},define:{'process.env.NODE_ENV':'"development"'}});
 const page=await browser.newPage();page.setDefaultTimeout(8000);let version=1,reads=0;const errors=[];
 page.on('console',m=>{if(m.type()==='error')errors.push(m.text());});page.on('pageerror',e=>errors.push(e.message));
 const tag=()=>`"${version.toString(16).toUpperCase().padStart(16,'0')}"`;
 try{
 await page.route('**/*',route=>{
  const req=route.request(),path=new URL(req.url()).pathname;
  if(path==='/api/auth/session')return route.fulfill({json:{user:{id:'42',username:'lead',name:'Lead'},expiresAt:'2099-01-01T00:00:00Z',absoluteExpiresAt:'2099-01-02T00:00:00Z',mustChangePassword:false,csrfToken:'synthetic'}});
  if(path==='/api/auth/csrf')return route.fulfill({json:{headerName:'X-CSRF-TOKEN',csrfToken:'synthetic'}});
  if(path==='/api/incidents/7') {reads++;return route.fulfill({json:{incidentId:7,branchId:1,status:'OPEN',triageETag:tag(),canTriage:false,categoryId:null,severityId:null}});}
  if(path==='/api/incidents/7/classification'){
   if(req.method()==='POST'){version++;return route.fulfill({json:{incidentId:7,eventId:version,version:tag(),suggestRoomBlock:false}});}
   return route.fulfill({json:{incidentId:7,categoryId:null,severityId:null,affectsRoomReadiness:null,version:tag(),categories:[{id:10,code:'ELECTRICAL',name:'Điện'}],severities:[{id:11,code:'HIGH',name:'Cao'}]}});
  }
  if(path.startsWith('/api/'))return route.fulfill({status:403,json:{}});
  return route.fulfill({contentType:'text/html',body:'<div id="root"></div>'});
 });
 await page.goto('https://hotelops.test');await page.addScriptTag({content:detailBundle.outputFiles[0].text});
 for(let i=0;i<4;i++){
  await page.getByLabel('Nhóm',{exact:true}).waitFor();
  assert.equal(await page.locator('#incident-classification').count(),1);
  assert.equal(await page.getByRole('heading',{name:'Chuyển cấp xử lý sự cố',exact:true}).count(),1);
  await page.getByLabel('Nhóm',{exact:true}).selectOption('10');await page.getByLabel('Mức độ',{exact:true}).selectOption('11');await page.getByLabel('Ảnh hưởng đến khả năng sẵn sàng của phòng',{exact:true}).selectOption('false');
  await page.getByRole('button',{name:'Lưu phân loại',exact:true}).click();
  await page.waitForFunction(()=>document.querySelector('#incident-classification select')?.value==='');
 }
 assert.equal(reads,5);assert.equal(await page.locator('#incident-classification').count(),1);
 assert.deepEqual(errors.filter(e=>/same key|unique.*key|Minified React|removeChild/i.test(e)),[]);
 }finally{await page.close();}
});
for(const scenario of ['save','stale','invalid-version','missing-category','forbidden','unchanged','legacy']) test(`Classification ${scenario}`,async()=>{
 const page=await browser.newPage();page.setDefaultTimeout(8000);const writes=[];
 try{
  await page.route('**/*',route=>{
   const req=route.request(),path=new URL(req.url()).pathname;
   if(path==='/api/auth/session')return route.fulfill({json:{user:{id:'42',username:'lead',name:'Lead'},expiresAt:'2099-01-01T00:00:00Z',absoluteExpiresAt:'2099-01-02T00:00:00Z',mustChangePassword:false,csrfToken:'synthetic'}});
   if(path==='/api/auth/csrf')return route.fulfill({json:{headerName:'X-CSRF-TOKEN',csrfToken:'synthetic'}});
   if(path==='/api/incidents/7/classification'){
    if(req.method()==='POST'){writes.push({body:req.postDataJSON(),headers:req.headers()});return route.fulfill(scenario==='stale'?{status:412,json:{}}:{json:{incidentId:7,eventId:20,version:'"0000000000000002"',suggestRoomBlock:false}});}
    return route.fulfill(scenario==='forbidden'?{status:403,json:{}}:{json:{incidentId:7,categoryId:scenario==='unchanged'?10:null,severityId:scenario==='legacy'?99:scenario==='unchanged'?11:null,affectsRoomReadiness:scenario==='unchanged'?false:null,version:scenario==='invalid-version'?'\u0000\u0000\u00a4\u0011':'"0000000000000001"',categories:scenario==='missing-category'?[]:[{id:10,code:'ELECTRICAL',name:'Điện'}],severities:[{id:11,code:'HIGH',name:'Cao'}]}});
   }
   return route.fulfill({contentType:'text/html',body:'<div id="root"></div>'});
  });
  await page.goto('https://hotelops.test');await page.addScriptTag({content:bundle.outputFiles[0].text});
  if(scenario==='forbidden'){
   await page.waitForResponse(r=>r.url().endsWith('/classification'));
   await page.locator('#incident-classification').waitFor({state:'hidden'});
   assert.equal(await page.getByRole('button',{name:'Lưu phân loại'}).count(),0);return;
  }
  await page.getByRole('heading',{name:'Phân loại sự cố',exact:true}).waitFor();
  assert.equal(await page.locator('#incident-classification').count(),1);
  if(scenario==='invalid-version'||scenario==='forbidden'){
   await page.getByRole('alert').waitFor();assert.match(await page.getByRole('alert').innerText(),scenario==='forbidden'?/chưa có quyền/:/Không xác minh/);assert.equal(await page.getByRole('button',{name:'Tải lại phân loại'}).isEnabled(),true);return;
  }
  const save=page.getByRole('button',{name:'Lưu phân loại',exact:true});await save.waitFor();assert.equal(await save.isDisabled(),true);
  if(scenario==='unchanged'){
   await page.getByText('Thông tin phân loại đã được lưu. Chỉ lưu lại khi có thay đổi.').waitFor();
   await page.getByLabel('Ảnh hưởng đến khả năng sẵn sàng của phòng',{exact:true}).selectOption('true');assert.equal(await save.isEnabled(),true);
   await page.getByLabel('Ảnh hưởng đến khả năng sẵn sàng của phòng',{exact:true}).selectOption('false');assert.equal(await save.isDisabled(),true);assert.equal(writes.length,0);return;
  }
  if(scenario==='legacy'){
   await page.getByText(/Sự cố đang dùng mức độ cũ/).waitFor();
   assert.equal(await page.getByLabel('Mức độ',{exact:true}).inputValue(),'99');
   await page.getByLabel('Nhóm',{exact:true}).selectOption('10');await page.getByLabel('Ảnh hưởng đến khả năng sẵn sàng của phòng',{exact:true}).selectOption('false');assert.equal(await save.isDisabled(),true);
  }
  if(scenario==='missing-category'){await page.getByRole('alert').waitFor();assert.match(await page.getByRole('alert').innerText(),/Chưa cấu hình nhóm/);return;}
  await page.getByLabel('Nhóm',{exact:true}).selectOption('10');await page.getByLabel('Mức độ',{exact:true}).selectOption('11');await page.getByLabel('Ảnh hưởng đến khả năng sẵn sàng của phòng',{exact:true}).selectOption('false');await save.click();
  await page.getByText(scenario==='stale'?'Sự cố đã thay đổi. Tải lại trước khi thử lại.':'MSG08: Đã lưu phân loại.',{exact:true}).waitFor();
  assert.equal(writes.length,1);assert.deepEqual(writes[0].body,{categoryId:10,severityId:11,affectsRoomReadiness:false});assert.equal(writes[0].headers['if-match'],'"0000000000000001"');assert.equal(writes[0].headers['x-csrf-token'],'synthetic');assert.equal(await save.isDisabled(),true);
 }finally{await page.close();}
});
