import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import vm from 'node:vm';
import { createRequire } from 'node:module';
import ts from 'typescript';
import React from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
const require=createRequire(import.meta.url);
const source=fs.readFileSync(new URL('../src/compact/room-issues.tsx',import.meta.url),'utf8');
const compiled=ts.transpileModule(source,{compilerOptions:{jsx:ts.JsxEmit.ReactJSX,module:ts.ModuleKind.CommonJS,target:ts.ScriptTarget.ES2022}}).outputText;
function render(overrides={}){
  const data={info:{reusable:true,kind:'BROKEN',quantity:3,objectName:'Linen'},work:{fields:{title:'Incident',rowVersion:'v1',status:'OPEN'}},room:{code:'101',rowVersion:'r1'},stage:'ROOM_ISSUE_REPORTED',canIssueReusable:true,store:true,reusableRemedy:{issuedQuantity:0,returnedQuantity:0},maintenance:[],materials:[],events:[],...overrides};
  const pass=({children})=>children;
  const exports={};
  const context={exports,location:{hash:'#room-issues?id=9'},URLSearchParams,crypto:globalThis.crypto,require:(id)=>{
    if(id==='react'||id==='react/jsx-runtime')return require(id);
    if(id.endsWith('.css'))return {};
    if(id==='./auth')return {useAuth:()=>({branch:1,can:()=>true,profile:{roles:[{code:'HOUSEKEEPING_SUPERVISOR',branchId:1}],memberships:[],user:{fields:{userId:3}}}})};
    if(id==='./api')return {timestamp:()=>'',message:String,ApiError:class extends Error{}};
    if(id==='./ui')return {Dialog:pass,State:pass,Empty:pass,Pager:()=>null,ConfirmationActions:()=>null,useQuery:path=>({data:!path?null:path.startsWith('requests/')?{actions:['claim']}:path.startsWith('workspace/options')?{warehouses:[{warehouseId:1,name:'Main store'}]}:path==='work/room-issues/9'?data:{items:[]}})};
    if(id==='./alert-popup')return {AlertPopup:pass};
    if(id==='./asset-replacement')return {ReplacementPanel:()=>null};
    return {default:()=>null};
  }};
  vm.runInNewContext(compiled,context);
  return renderToStaticMarkup(React.createElement(exports.default,{detailOnly:true,selectedId:9}));
}
test('warehouse receives actual damaged returns and full replacement, not maintenance approval',()=>{
  const html=render();
  assert.match(html,/max="3"[^>]*name="returnedQuantity"/);
  assert.match(html,/Main store/);
  assert.match(html,/Cấp đủ 3 đồ sạch/);
  assert.doesNotMatch(html,/Xác nhận báo cáo/);
});
test('failed inspection allows remaining returns without a second stock issue',()=>{
  const html=render({stage:'ROOM_ISSUE_INSPECTION_FAILED',canIssueReusable:false,reusableRemedy:{issuedQuantity:3,returnedQuantity:1}});
  assert.match(html,/max="2"[^>]*name="returnedQuantity"/);
  assert.match(html,/lần này không xuất thêm kho/);
});
test('missing items disclose unresolved accounting and have no return input',()=>{
  const html=render({info:{reusable:true,kind:'MISSING',quantity:3},reusableRemedy:{issuedQuantity:0,returnedQuantity:0,lossAccountingReviewRequired:true}});
  assert.doesNotMatch(html,/name="returnedQuantity"/);
  assert.match(html,/chưa được ghi nhận hoàn trả hoặc xóa sổ/);
});
test('supervisor can reopen or cancel warehouse refusal',()=>{
  const html=render({stage:'ROOM_ISSUE_REPLACEMENT_REJECTED',canIssueReusable:false,store:false});
  assert.match(html,/Xác minh lại và chuyển kho/);
  assert.match(html,/Hủy báo cáo sai/);
});
test('ordinary reports retain supervisor approval without reusable controls',()=>{
  const html=render({info:{reusable:false,kind:'BROKEN'},canIssueReusable:false});
  assert.match(html,/Xác nhận báo cáo/);
  assert.doesNotMatch(html,/Thay thế đồ tái sử dụng/);
});
