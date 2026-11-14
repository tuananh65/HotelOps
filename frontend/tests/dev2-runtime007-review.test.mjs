import {test} from 'node:test';
import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {fileURLToPath} from 'node:url';
const require=createRequire(import.meta.url);
const {review}=require('../../docs/integration-notes/DEV2-FULLSTACK-007-RUNTIME/review-runtime.cjs');
test('007 reviewer rejects historical005 PASS as independent007 acceptance',()=>{
  const old=fileURLToPath(new URL('../../docs/integration-notes/DEV2-FULLSTACK-005-FRONTEND/host-receipts/DEMO005_20261113_140714_9423afdc',import.meta.url));
  assert.throws(()=>review(old),/independent007 receipt directory/);
});
test('007 reviewer rejects arbitrary output directories before reading receipts',()=>{
  assert.throws(()=>review(process.cwd()),/independent007 receipt directory/);
});
