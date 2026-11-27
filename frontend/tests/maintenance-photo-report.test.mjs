import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { build } from 'esbuild';
await build({entryPoints:['src/compact/cleaning-completion-report.tsx'],bundle:true,platform:'node',format:'esm',jsx:'automatic',external:['react/jsx-runtime'],outfile:'.test-output/maintenance-photo-report.mjs',plugins:[{name:'component-harness',setup(b){
  b.onResolve({filter:/^react$/},()=>({path:'react',namespace:'test'}));
  b.onResolve({filter:/^\.\/api$/},()=>({path:'api',namespace:'test'}));
  b.onResolve({filter:/^\.\/ui$/},()=>({path:'ui',namespace:'test'}));
  b.onLoad({filter:/.*/,namespace:'test'},({path})=>({contents:path==='react'?'export const useRef=x=>({current:x}), useState=x=>[x,()=>{}], useEffect=()=>{};':path==='api'?'export const api=(...args)=>globalThis.photoHarness.api(...args), action=(rowVersion,body)=>({rowVersion,...body}), message=e=>e.message, fields=x=>x||[], sessionToken=()=>"", timestamp=x=>x; export class ApiError extends Error {}':'export const ConfirmationActions=()=>null, State=()=>null, useQuery=()=>({});'}));
}}]});
const {default: Report, CleaningReportHistory}=await import('../.test-output/maintenance-photo-report.mjs');
function nodes(root){return !root||typeof root!=='object'?[]:[root,...[root.props?.children].flat(Infinity).flatMap(nodes)];}
test('unsuccessful maintenance history binds photos to the persisted outcome event',()=>{
  const tree=CleaningReportHistory({detail:{attachments:[{attachmentId:81,mimeType:'image/png',originalName:'repair.png'}],events:[{workEventId:12,eventType:'MAINTENANCE_UNREPAIRABLE',occurredAt:'2026-11-27',snapshotJson:JSON.stringify({attachmentIds:[81]})}]}});
  const report=nodes(tree).find(n=>n.type==='section'&&n.key==='12');
  assert.ok(report,'the outcome must render as a report rather than an unbound historical photo');
  assert.ok(nodes(report).some(n=>n.props?.file?.attachmentId===81));
});
async function run(outcome,{photo=true,failOnce=false,note='Repair result and diagnosis'}={}){
  const calls=[];let saved=0;let failed=false;
  globalThis.photoHarness={api:async(path,body)=>{calls.push({path,body});if(path==='evidence')return {fields:{attachmentId:81}};if(failOnce&&!failed){failed=true;throw Error('temporary');}return {};}};
  const tree=Report({work:{workId:21,branchId:3,rowVersion:'version'},maintenanceOutcome:outcome,pending:false,busy:()=>{},saved:()=>saved++,cancel:()=>{}});
  if(photo){const input=nodes(tree).find(n=>n.type==='input'&&n.props.multiple);input.props.onChange({target:{files:[new File(['image'],'repair.png',{type:'image/png'})],value:''}});}
  const NativeFormData=globalThis.FormData;
  globalThis.FormData=class extends NativeFormData {constructor(form){super();if(form&&note)this.set('reason',note);}};
  try{await tree.props.onSubmit({preventDefault(){},currentTarget:{}});if(failOnce)await tree.props.onSubmit({preventDefault(){},currentTarget:{}});}finally{globalThis.FormData=NativeFormData;}
  return {calls,saved,tree};
}
test('maintenance success uploads real file first and submits returned attachment IDs',async()=>{const {calls,saved}=await run('complete');assert.equal(saved,1);assert.equal(calls[0].path,'evidence');assert.equal(calls[0].body.get('workId'),'21');assert.equal(calls[0].body.get('file').name,'repair.png');assert.equal(calls[1].path,'work/21/actions/complete');assert.deepEqual(calls[1].body.attachmentIds,[81]);assert.equal(calls[1].body.rowVersion,'version');});
test('successful repair needs photo only and has no reason field',async()=>{const {calls,saved,tree}=await run('complete',{note:''});assert.equal(saved,1);assert.equal(calls[1].body.reason,null);assert.equal(nodes(tree).filter(n=>n.type==='textarea').length,0);});
test('failed repair needs reason before any photo upload',async()=>{const {calls,saved,tree}=await run('unrepairable',{note:''});assert.equal(saved,0);assert.equal(calls.length,0);assert.equal(nodes(tree).find(n=>n.type==='textarea').props.required,true);});
test('unrepairable sends distinct photo-backed action for supervisor replacement route',async()=>{const {calls,saved}=await run('unrepairable');assert.equal(saved,1);assert.equal(calls[1].path,'work/21/actions/unrepairable');assert.deepEqual(calls[1].body.attachmentIds,[81]);});
test('neither maintenance outcome can submit without a photo',async()=>{for(const outcome of ['complete','unrepairable']){const {calls,saved}=await run(outcome,{photo:false});assert.equal(calls.length,0);assert.equal(saved,0);}});
test('retry reuses uploaded IDs and operation key instead of uploading duplicate photos',async()=>{const {calls,saved}=await run('unrepairable',{failOnce:true});assert.equal(saved,1);assert.equal(calls.filter(c=>c.path==='evidence').length,1);assert.equal(calls[1].body.operationKey,calls[2].body.operationKey);assert.deepEqual(calls[2].body.attachmentIds,[81]);});
test('housekeeping defaults preserve complete endpoint',async()=>{const {calls,saved}=await run(undefined);assert.equal(saved,1);assert.equal(calls[1].path,'work/21/actions/complete');});
test('maintenance uses supervisor handover only and pending blocks acceptance and start',async()=>{const source=await readFile('src/compact/work.tsx','utf8');assert.ok(source.includes('!["CLEANING", "REWORK", "MAINTENANCE"].includes(w.kind) && !readOnlyWork'));assert.ok(source.includes('!(["accept", "start"].includes(a) && detail.data?.capabilities?.hasPendingHandover)'));assert.ok(source.includes('canRequestHandover && !ownAssignment?.acceptedAt && !w?.actualStartAt'));});
