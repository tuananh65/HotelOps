import test from 'node:test';
import assert from 'node:assert/strict';
import {build} from 'esbuild';
import {chromium,expect} from '@playwright/test';

test('broken reports only assets; missing reports allow replacement materials; switching clears selection',async()=>{
 const bundle=await build({stdin:{resolveDir:process.cwd(),loader:'tsx',contents:`import React from 'react';import {createRoot} from 'react-dom/client';import Form from './src/compact/room-issue-report-form';createRoot(document.getElementById('root')).render(<Form objects={{code:'103',assets:[{assetId:1,name:'Điều hòa'}],missingAssets:[{assetId:1,name:'Điều hòa'}],items:[{itemId:2,name:'Khăn mặt'}],reusableItems:[{itemId:2,name:'Khăn mặt'}],missingReusableItems:[{itemId:2,name:'Khăn mặt',maxReportableQuantity:2}]}} types={[{referenceId:1,kind:'BROKEN',isActive:true,name:'Hỏng đồ'},{referenceId:2,kind:'MISSING',isActive:true,name:'Thiếu đồ'}]} reception={false} pending={false} cleaningWorkId={7} submit={(type,lines)=>window.submitted={type,lines}}/>);`},bundle:true,write:false,format:'iife',jsx:'automatic'});
 const browser=await chromium.launch({executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',headless:true});
 try{
  const page=await browser.newPage();await page.setContent('<html lang="vi"><meta charset="utf-8"><div id="root"></div></html>');await page.addScriptTag({content:bundle.outputFiles[0].text});
  await page.getByRole('combobox').selectOption('1');
  await expect(page.getByRole('checkbox',{name:'Điều hòa'})).toBeVisible();await expect(page.getByRole('checkbox',{name:/Khăn mặt/})).toHaveCount(0);
  await page.getByRole('combobox').selectOption('2');await page.getByRole('checkbox',{name:/Khăn mặt/}).check();await page.getByRole('button',{name:'Gửi báo cáo'}).click();
  assert.deepEqual(await page.evaluate(()=>window.submitted.lines),[{assetId:null,itemId:2,quantity:1}]);
  await page.getByRole('combobox').selectOption('1');await expect(page.getByRole('button',{name:'Gửi báo cáo'})).toBeDisabled();await expect(page.getByRole('checkbox',{name:/Khăn mặt/})).toHaveCount(0);
  await page.getByRole('checkbox',{name:'Điều hòa'}).check();await page.getByRole('button',{name:'Gửi báo cáo'}).click();assert.deepEqual(await page.evaluate(()=>window.submitted.lines),[{assetId:1,itemId:null,quantity:1}]);
 }finally{await browser.close();}
});
