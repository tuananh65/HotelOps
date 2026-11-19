import {test, before, after} from 'node:test';
import assert from 'node:assert/strict';
import {build} from 'esbuild';
import {chromium} from '@playwright/test';
const bundle=await build({stdin:{resolveDir:process.cwd(),loader:'tsx',contents:`
import React from 'react';import {createRoot} from 'react-dom/client';
import {AuthProvider} from './src/auth/AuthProvider';import {createAuthStore} from './src/auth/authStore';import {createAuthApi} from './src/auth/authApi';
import {RoomDepartureActions} from './src/screens/rooms/RoomDepartureActions';
const s=window.scenario;window.writes=[];window.reads=[];window.changed=0;
window.fetch=async(url,init)=>{
if(init?.method==='POST'){window.writes.push({url,body:init.body,headers:init.headers});return Response.json(s.fail?{code:'ROOM_OPERATION_STALE'}:{roomId:26,recordedStatus:'DIRTY',readiness:'PENDING_CLEANING',targetRoomId:25},{status:s.fail?412:200});}
window.reads.push(url);
if(url==='/api/auth/session')return Response.json({user:{id:'24',username:'huy.dinh',name:'Huy'},expiresAt:'2099-01-01T00:00:00Z',absoluteExpiresAt:'2099-01-02T00:00:00Z',mustChangePassword:false,csrfToken:'token'});
if(url==='/api/auth/csrf')return Response.json({headerName:'X-CSRF-TOKEN',csrfToken:'token'});
if(url==='/api/auth/navigation/operation-grants')return Response.json({grants:(s.grants??['ROOM_CHECKOUT','ROOM_OCCUPANCY_MOVE']).map(permission=>({permission,branchId:1,scope:'BRANCH'}))});
const source={canDepart:!s.blocked,version:'A'.repeat(64),recordedStatus:'OCCUPIED',unavailableReason:s.blocked?'Còn công việc chưa kết thúc.':null};
if(url.endsWith('/check-out'))return Response.json(source);
if(url.endsWith('/move-options'))return Response.json({source,targets:s.empty?[]:[{roomId:25,roomNumber:'DEMO-LT-02',floorName:'Tầng 1',roomTypeName:'Demo',version:'B'.repeat(64)}]});
return Response.json({},{status:404});};
createRoot(document.getElementById('root')).render(<AuthProvider store={createAuthStore(createAuthApi(window.fetch))}><RoomDepartureActions branchId={1} roomId={26} roomNumber="DEMO-LT-03" occupied={s.occupied!==false} onChanged={()=>window.changed++}/></AuthProvider>);
`},bundle:true,write:false,format:'iife',jsx:'automatic',define:{'process.env.NODE_ENV':'"development"','import.meta.env':'{}'}});
let browser;
before(async()=>{browser=await chromium.launch({headless:true,executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'});});
after(async()=>browser?.close());
async function run(scenario,check){
 const page=await browser.newPage();const errors=[];page.on('pageerror',e=>errors.push(e.message));
 try{await page.route('**/*',r=>r.request().resourceType()==='document'?r.fulfill({contentType:'text/html',body:'<div id="root"></div>'}):r.abort());await page.goto('https://test.local');await page.evaluate(s=>window.scenario=s,scenario);await page.addScriptTag({content:bundle.outputFiles[0].text});await page.waitForFunction(()=>window.reads.includes('/api/auth/navigation/operation-grants'));await page.waitForTimeout(100);await check(page);assert.deepEqual(errors,[]);}finally{await page.close();}
}
test('Reception departure controls are absent without the corresponding branch permissions',async()=>run({grants:[]},async p=>{
 assert.equal(await p.getByRole('button').count(),0);assert.equal(await p.evaluate(()=>window.reads.some(x=>x.endsWith('/move-options')||x.endsWith('/check-out'))),false);
}));
test('Unoccupied and unfinished-work rooms cannot be departed',async()=>{
 for(const s of [{occupied:false},{blocked:true}])await run(s,async p=>{assert.equal(await p.getByRole('button',{name:'Trả phòng',exact:true}).count(),0);assert.equal(await p.getByRole('button',{name:'Chuyển phòng',exact:true}).count(),0);});
});
test('Opening and cancelling the departure form performs no write',async()=>run({},async p=>{
 await p.getByRole('button',{name:'Trả phòng',exact:true}).click();await p.getByRole('button',{name:'Hủy',exact:true}).click();assert.deepEqual(await p.evaluate(()=>window.writes),[]);
}));
test('Room move requires target and reason and submits both server versions',async()=>run({},async p=>{
 await p.getByRole('button',{name:'Chuyển phòng',exact:true}).click();const submit=p.getByRole('button',{name:'Xác nhận chuyển phòng',exact:true});assert(await submit.isDisabled());
 await p.getByLabel('Phòng đích',{exact:true}).selectOption('25');assert(await submit.isDisabled());await p.getByLabel('Lý do chuyển phòng',{exact:true}).fill('Khách đề nghị');await submit.click();await p.waitForFunction(()=>window.changed===1);
 const writes=await p.evaluate(()=>window.writes);assert.equal(writes.length,1);assert.equal(writes[0].headers['If-Match'],'"'+'A'.repeat(64)+'"');assert.equal(JSON.parse(writes[0].body).targetVersion,'B'.repeat(64));assert.equal(JSON.parse(writes[0].body).targetRoomId,25);
}));
test('No ready destinations prevents submission',async()=>run({empty:true},async p=>{
 await p.getByRole('button',{name:'Chuyển phòng',exact:true}).click();assert(await p.getByRole('button',{name:'Xác nhận chuyển phòng',exact:true}).isDisabled());assert.equal(await p.getByLabel('Phòng đích',{exact:true}).locator('option').count(),1);
}));
test('Stale departure does not replay or retain a submit button',async()=>run({fail:true},async p=>{
 await p.getByRole('button',{name:'Trả phòng',exact:true}).click();await p.getByRole('button',{name:'Xác nhận trả phòng',exact:true}).click();await p.getByRole('alert').waitFor();assert.equal(await p.getByRole('button',{name:'Xác nhận trả phòng',exact:true}).count(),0);assert.equal(await p.evaluate(()=>window.writes.length),1);assert.equal(await p.evaluate(()=>window.changed),0);
}));
