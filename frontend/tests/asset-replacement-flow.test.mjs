import test from 'node:test';
import assert from 'node:assert/strict';
import {build} from 'esbuild';
import {chromium,expect} from '@playwright/test';

test('OM approves without reason then confirms installed replacement before submitting',async()=>{
 const bundle=await build({stdin:{resolveDir:process.cwd(),loader:'tsx',contents:`import React from 'react';import {createRoot} from 'react-dom/client';import {ReplacementPanel} from './src/compact/asset-replacement';createRoot(document.getElementById('root')).render(<ReplacementPanel id={7} installation={location.hash==='#install'}/>);`},bundle:true,write:false,outfile:'test.js',format:'iife',jsx:'automatic'});
 const browser=await chromium.launch({executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',headless:true});
 try{const page=await browser.newPage();const writes=[];let install=false;
  await page.route('**/*',route=>{if(route.request().resourceType()==='document')return route.fulfill({contentType:'text/html',body:'<html lang="vi"><meta charset="utf-8"><div id="root"></div></html>'});if(route.request().method()==='POST'){writes.push({url:route.request().url(),body:route.request().postDataJSON()});return route.fulfill({json:{}});}return route.fulfill({json:{rowVersion:'v1',state:install?'REPLACEMENT_APPROVED':'REPLACEMENT_CONFIRMED',history:[],canDecide:!install,canInstall:install,replacementAssets:[{assetId:8,name:'Điều hòa mới',assetCode:'AC-NEW'}]}});});
  await page.goto('https://replacement.test');await page.addScriptTag({content:bundle.outputFiles.find(f=>f.path.endsWith('.js')).text});await page.getByRole('button',{name:'Đồng ý thay mới'}).click();await expect(page).toHaveURL(/#assets\?replacement=7$/);assert.equal(writes[0].body.reason,null);
  install=true;await page.goto('https://replacement.test/#install');await page.reload();await page.addScriptTag({content:bundle.outputFiles.find(f=>f.path.endsWith('.js')).text});assert.equal(await page.getByRole('combobox').count(),0);
  await page.getByRole('button',{name:'Xác nhận đã thay và gửi kiểm tra'}).click();await expect.poll(()=>writes.length).toBe(2);assert.equal(writes[1].body.replacementAssetId,null);assert.ok(writes[1].url.endsWith('/install'));
 }finally{await browser.close();}
});
