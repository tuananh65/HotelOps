import assert from 'node:assert/strict';
import fs from 'node:fs';
import {chromium} from '../../frontend/node_modules/@playwright/test/index.mjs';
const browser=await chromium.launch({channel:'msedge',headless:true});
try{
 const context=await browser.newContext({ignoreHTTPSErrors:true});const page=await context.newPage();const errors=[];
 page.on('pageerror',e=>errors.push(e.message));
 const response=await page.goto('https://127.0.0.1:5080/login');assert.equal(response.status(),200);
 const expected=fs.readFileSync('frontend/dist/index.html','utf8');const html=await response.text();
 const scripts=[...expected.matchAll(/(?:src|href)="(\/assets\/[^\"]+)"/g)].map(m=>m[1]);assert(scripts.length>=2);
 for(const asset of scripts){assert(html.includes(asset));assert.equal((await context.request.get('https://127.0.0.1:5080'+asset)).status(),200);}
 const statuses=[];
 for(const base of ['https://127.0.0.1:5080','https://localhost:5769','http://127.0.0.1:5774']){
  const response=await context.request.get(base+'/api/v2/work/maintenance-inbox?branchId=1');assert.equal(response.status(),401);
  statuses.push({base,unauthenticated:response.status()});
 }
 assert.deepEqual(errors,[]);
 await page.screenshot({path:'artifacts/maintenance-workspace/production-login.png'});
 fs.writeFileSync('artifacts/maintenance-workspace/local-release.json',JSON.stringify({checkedAt:new Date().toISOString(),scripts,statuses,errors},null,2));
 console.log(JSON.stringify({scripts,statuses,errors}));
}finally{await browser.close();}
