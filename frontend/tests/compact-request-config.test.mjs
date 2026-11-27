import {test} from 'node:test';
import assert from 'node:assert/strict';
import {build} from 'esbuild';
import {chromium,expect} from '@playwright/test';

const catalog={configured:true,roles:[{roleId:1,name:'Lễ tân'}],recipientRoles:[{roleId:2,name:'Giám sát'}],departments:[{departmentId:10,name:'Tiền sảnh'},{departmentId:20,name:'Buồng phòng'}],recipients:[{userId:7,fullName:'Nguyễn An',employeeCode:'NV7',roleIds:[2]}],items:[{referenceId:8,name:'Hỗ trợ khách',description:'Nêu phòng và nhu cầu',senderRoleIds:[1],senderDepartmentIds:[10],recipientRoleIds:[2],requireDescription:true,isActive:true,rowVersion:'v1'}]};
const bundle=await build({stdin:{resolveDir:process.cwd(),loader:'tsx',contents:`import React from 'react';import {createRoot} from 'react-dom/client';import {Auth} from './src/compact/auth';import Settings from './src/compact/internal-settings';import Fields from './src/compact/request-recipient-fields';createRoot(document.getElementById('root')).render(location.hash==='#fields'?<form><Fields catalog={${JSON.stringify(catalog)}} departments={${JSON.stringify(catalog.departments)}}/></form>:<Auth><Settings section="requests"/></Auth>);`},bundle:true,write:false,outfile:'test.js',format:'iife',jsx:'automatic',define:{'process.env.NODE_ENV':'"test"','import.meta.env':'{}'}});
test('OM edits sender department, recipient mode and required content without losing them on toggle; creator sees route',async()=>{
 const browser=await chromium.launch({executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',headless:true});
 try{
  const page=await browser.newPage();const writes=[],errors=[];page.on('pageerror',e=>errors.push(e.message));
  await page.addInitScript(()=>sessionStorage.setItem('hotelops.compact.session','fixture'));
  await page.route('**/*',async route=>{
   const req=route.request();const path=new URL(req.url()).pathname;
   if(req.resourceType()==='document')return route.fulfill({contentType:'text/html',body:'<html lang="vi"><meta charset="utf-8"><div id="root"></div></html>'});
   if(path.endsWith('/auth/me'))return route.fulfill({json:{user:{fields:{userId:1,fullName:'OM'}},branches:[{branchId:1,name:'Chi nhánh',isActive:true}],roles:[{code:'OPERATION_MANAGER',branchId:1}],memberships:[],grants:[{code:'IR_VIEW',branchId:1,dataScope:'BRANCH'}]}});
   if(path.includes('internal-request-types')){
    if(req.method()!=='GET'){writes.push(req.postDataJSON());Object.assign(catalog.items[0],req.postDataJSON());return route.fulfill({json:{referenceId:8,rowVersion:'v2'}});}
    return route.fulfill({json:catalog});
   }
   return route.fulfill({json:{}});
  });
  await page.goto('http://request-config.test');await page.addScriptTag({content:bundle.outputFiles.find(f=>f.path.endsWith('.js')).text});
  await page.getByRole('button',{name:'Chỉnh sửa',exact:true}).click();
  await expect(page.getByRole('checkbox',{name:'Tiền sảnh',exact:true})).toBeChecked();
  await expect(page.getByRole('checkbox',{name:'Bắt buộc mô tả chi tiết'})).toBeChecked();
  await page.getByLabel('Cách chọn nơi nhận').selectOption('targets');
  await page.locator('input[name=recipientDepartmentId][value="20"]').check();
  await page.locator('input[name=recipientUserId][value="7"]').check();
  await page.getByRole('button',{name:'Lưu thay đổi'}).click();await expect(page.locator('dialog')).toHaveCount(0);
  assert.deepEqual(writes.at(-1).senderDepartmentIds,[10]);assert.deepEqual(writes.at(-1).recipientDepartmentIds,[20]);assert.deepEqual(writes.at(-1).recipientUserIds,[7]);assert.equal(writes.at(-1).recipientRoleIds,null);assert.equal(writes.at(-1).requireDescription,true);
  await page.getByRole('switch',{name:/Hỗ trợ khách/}).click();await expect(page.getByRole('switch')).toHaveAttribute('aria-checked','false');assert.deepEqual(writes.at(-1).senderDepartmentIds,[10]);assert.equal(writes.at(-1).requireDescription,true);
  await page.goto('http://request-config.test/#fields');await page.addScriptTag({content:bundle.outputFiles.find(f=>f.path.endsWith('.js')).text});
  await page.locator('select[name=categoryId]').selectOption('8');
  await expect(page.getByLabel('Quy định loại yêu cầu')).toContainText('Tiền sảnh');await expect(page.getByLabel('Quy định loại yêu cầu')).toContainText('mô tả chi tiết');
  await expect(page.locator('select[name=requestRecipient] option[value="user:7"]')).toHaveCount(1);await expect(page.locator('select[name=requestRecipient] option[value="department:10"]')).toHaveCount(0);
  assert.deepEqual(errors,[]);
 }finally{await browser.close();}
});
