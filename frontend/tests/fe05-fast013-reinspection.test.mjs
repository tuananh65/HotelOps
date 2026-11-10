import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import { pathToFileURL } from 'node:url';
import path from 'node:path';
import ts from 'typescript';
import React from 'react';
import { renderToStaticMarkup } from 'react-dom/server';

const dir=path.resolve('node_modules/.cache/fe05-fast013');
await fs.mkdir(dir,{recursive:true});
const source=await fs.readFile('src/features/inspectionReinspection/ReinspectionReviewCandidate.tsx','utf8');
const emitted=ts.transpileModule(source,{compilerOptions:{jsx:ts.JsxEmit.ReactJSX,module:ts.ModuleKind.ESNext,target:ts.ScriptTarget.ES2022}}).outputText;
const target=path.join(dir,'candidate.mjs');await fs.writeFile(target,emitted);
const {ReinspectionReviewCandidate}=await import(pathToFileURL(target).href);
const dto={inspectionId:21,reworkId:31,originalInspectionId:11,assignedChecklistVersionId:8,originalChecklistVersionId:5,
 inspectionRevision:'"0000000000000001"',reworkRevision:'"0000000000000002"',
 defects:[{defectId:41,remediationNote:'<script>private</script>',remediatedBy:6,remediatedAt:'2026-11-10T00:00:00Z'}],
 sourceBindingAccepted:false,mutationAvailable:false,dependencies:['EXACT_BINDING_REQUIRED']};
function render(value=dto){return renderToStaticMarkup(React.createElement(ReinspectionReviewCandidate,{source:value}));}
test('candidate preserves original and assigned version identity',()=>{const html=render();assert.match(html,/Original inspection #11/);assert.match(html,/Original checklist version 5; assigned version 8/);});
test('review has explicit unanswered, Fixed and Not Fixed choices',()=>{const html=render();assert.match(html,/Not reviewed/);assert.match(html,/>Fixed</);assert.match(html,/>Not Fixed</);assert.match(html,/0\/1 defects reviewed/);});
test('no mutation activation even with a forged runtime boolean',()=>{const html=render({...dto,mutationAvailable:true,sourceBindingAccepted:true});assert.match(html,/<button type="button" disabled="">Conclude unavailable<\/button>/);});
test('remediation content is escaped and no private evidence URLs emitted',()=>{const html=render();assert.ok(!html.includes('<script>'));assert.match(html,/&lt;script&gt;/);assert.ok(!html.includes('href=')&&!html.includes('<img'));});
test('empty source and missing remediation note render without invented defects',()=>{assert.match(render({...dto,defects:[]}),/0\/0 defects reviewed/);assert.match(render({...dto,defects:[{...dto.defects[0],remediationNote:null}]}),/No remediation note/);});
test('render dispatches no network request or mock authority',()=>{const prior=globalThis.fetch;globalThis.fetch=()=>{throw Error('Unexpected request');};try{render();}finally{globalThis.fetch=prior;}});
