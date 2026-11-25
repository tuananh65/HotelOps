import test from 'node:test';
import assert from 'node:assert/strict';
import {mkdtempSync,writeFileSync,readFileSync,existsSync,rmSync} from 'node:fs';
import {tmpdir} from 'node:os';
import path from 'node:path';
import {spawnSync} from 'node:child_process';

test('database package authenticates ciphertext and refuses overwrite',()=>{
 const dir=mkdtempSync(path.join(tmpdir(),'hotelops-package-test-'));
 const file=name=>path.join(dir,name);
 const run=(...args)=>spawnSync(process.execPath,['deployment/database-package.mjs',...args],{encoding:'utf8'});
 try{
  writeFileSync(file('source'),Buffer.from('Synthetic backup bytes for package verification'));
  assert.equal(run('encrypt',file('source'),file('encrypted'),file('key')).status,0);
  assert.equal(run('decrypt',file('encrypted'),file('restored'),file('key')).status,0);
  assert.deepEqual(readFileSync(file('restored')),readFileSync(file('source')));
  assert.notEqual(run('decrypt',file('encrypted'),file('restored'),file('key')).status,0);
  const changed=readFileSync(file('encrypted'));changed[changed.length-1]^=1;writeFileSync(file('changed'),changed);
  assert.notEqual(run('decrypt',file('changed'),file('tampered-output'),file('key')).status,0);
  assert.equal(existsSync(file('tampered-output')),false);
 }finally{rmSync(dir,{recursive:true,force:true});}
});
