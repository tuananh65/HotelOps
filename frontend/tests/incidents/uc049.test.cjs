// Run: UC049_NODE_MODULES=<existing dependencies>/node_modules node --test frontend/tests/incidents/uc049.test.cjs
// Dependencies are read only. The exact committed host auth sources are materialized
// in this worktree's ignored node_modules directory, never copied into production source.
const { test, before, after } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const cp = require('node:child_process');
const http = require('node:http');
const dep = path.resolve(process.env.UC049_NODE_MODULES || 'frontend/node_modules');
const esbuild = require(path.join(dep, 'esbuild'));
const { chromium } = require(path.join(dep, 'playwright'));
const root = path.resolve('frontend/node_modules/.uc049-validation');
let navigation, server, browser, base;
function put(file, value) { fs.mkdirSync(path.dirname(file), { recursive: true }); fs.writeFileSync(file, value); }
before(async () => {
  for (const name of ['AuthProvider.tsx', 'authStore.ts', 'authApi.ts', 'http.ts', 'contracts.ts'])
    put(`${root}/src/auth/${name}`, cp.execFileSync('git', ['show', `d0a5a073b7921040f1e2ed726782ca97e0e30e61:frontend/src/auth/${name}`]));
  for (const file of ['incidents/api.ts', 'incidents/navigation.ts', 'incidents/ReportIncidentRoute.tsx', 'screens/maintenance/ReportIncidentScreen.tsx'])
    put(`${root}/src/${file}`, fs.readFileSync(`frontend/src/${file}`));
  const tsconfig = { compilerOptions: { noEmit: true, strict: true, target: 'ES2022', module: 'ESNext', moduleResolution: 'Bundler', jsx: 'react-jsx', skipLibCheck: true,
    typeRoots: [dep + '/@types'], paths: { react: [dep + '/@types/react/index.d.ts'], 'react/jsx-runtime': [dep + '/@types/react/jsx-runtime.d.ts'], 'react-router-dom': [dep + '/react-router-dom/dist/index.d.ts'] } }, include: ['src/**/*'] };
  put(`${root}/tsconfig.json`, JSON.stringify(tsconfig));
  cp.execFileSync(process.execPath, [dep + '/typescript/bin/tsc', '-p', root + '/tsconfig.json'], { stdio: 'pipe' });
  console.log('TYPECHECK_PASS: FE06 sources + exact d0a5a073 host authentication dependencies');
  const ts = require(path.join(dep, 'typescript'));
  put(root + '/navigation.cjs', ts.transpileModule(fs.readFileSync(root + '/src/incidents/navigation.ts', 'utf8'), { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }).outputText);
  navigation = require(root + '/navigation.cjs');
  put(root + '/fixture.jsx', `
    import React from 'react'; import {createRoot} from 'react-dom/client';
    import {BrowserRouter,Routes,Route,Link} from 'react-router-dom';
    import {AuthProvider} from './src/auth/AuthProvider'; import ReportIncidentRoute from './src/incidents/ReportIncidentRoute';
    const mode=new URLSearchParams(location.search).get('actor');
    const state={status:mode==='anonymous'?'ANONYMOUS':mode==='restricted'?'RESTRICTED':'AUTHENTICATED',
      session:mode==='anonymous'?null:{user:{id:'7',username:'fixture',name:'Fixture'},expiresAt:'2099-01-01T00:00:00Z',absoluteExpiresAt:'2099-01-01T00:00:00Z',mustChangePassword:mode==='restricted'},pending:null,error:null};
    const store={getSnapshot:()=>state,subscribe:()=>()=>{},initialize:async()=>({ok:true,data:state.session})};
    createRoot(document.getElementById('root')).render(<AuthProvider store={store}><BrowserRouter>
      <Link to='/incidents/new?branchId=2&roomId=22'>Other context</Link>
      <Routes><Route path='/incidents/new' element={<ReportIncidentRoute/>}/></Routes>
    </BrowserRouter></AuthProvider>);
  `);
  // Node handles file access explicitly: the sandbox does not permit native
  // bundler directory discovery through every ancestor of the workspace.
  const bundle = await esbuild.build({ stdin: { contents: fs.readFileSync(root + '/fixture.jsx','utf8'), resolveDir: root, loader: 'jsx' },
    bundle: true, write: false, platform: 'browser', jsx: 'automatic', tsconfigRaw: {},
    plugins: [{ name: 'explicit-fixture-files', setup(build) {
      build.onResolve({filter:/.*/}, args => {
        let file;
        if(args.path.startsWith('.')) {
          const basePath=path.resolve(args.resolveDir,args.path);
          file=['','.tsx','.ts','.jsx','.js','/index.js'].map(ext=>basePath+ext).find(p=>fs.existsSync(p)&&fs.statSync(p).isFile());
        } else file=require.resolve(args.path,{paths:[dep]});
        if(!file)throw new Error('Missing fixture dependency '+args.path);
        return {path:file,namespace:'fixture'};
      });
      build.onLoad({filter:/.*/,namespace:'fixture'}, args=>({contents:fs.readFileSync(args.path,'utf8'),resolveDir:path.dirname(args.path),loader:args.path.endsWith('.tsx')?'tsx':args.path.endsWith('.ts')?'ts':args.path.endsWith('.jsx')?'jsx':'js'}));
    }}] });
  put(root + '/app.js', bundle.outputFiles[0].contents);
  console.log('BUNDLE_PASS: candidate route under one existing AuthProvider and BrowserRouter fixture');
  server = http.createServer((req, res) => { res.setHeader('Content-Type', req.url === '/app.js' ? 'application/javascript' : 'text/html'); res.end(req.url === '/app.js' ? fs.readFileSync(root + '/app.js') : '<div id="root"></div><script src="/app.js"></script>'); });
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve)); base = `http://127.0.0.1:${server.address().port}`;
  browser = await chromium.launch({ headless: true, executablePath: process.env.UC049_BROWSER || 'C:/Program Files/Google/Chrome/Application/chrome.exe' });
});
after(async () => { await browser?.close(); if (server) await new Promise(resolve => server.close(resolve)); });

for (const [query, expected] of [ ['', {}], ['?branchId=1', {branchId:1}], ['?branchId=1&roomId=11', {branchId:1,roomId:11}], ['?branchId=9007199254740991', {branchId:9007199254740991}] ])
  test('parser accepts ' + (query || 'direct-open'), () => assert.deepEqual(navigation.parseIncidentContext(query), expected));
for (const query of ['roomId=1', 'branchId=', 'branchId=0', 'branchId=-1', 'branchId=01', 'branchId=+1', 'branchId=1.0', 'branchId=1e2', 'branchId=NaN', 'branchId=9007199254740992', 'branchId=1&branchId=1', 'branchId=1&roomId=1&roomId=2', 'branchId=1&roomId=0', 'branchId=1&roomId=-2', 'branchId=1&roomId=9007199254740992'])
  test('parser rejects ' + query, () => assert.throws(() => navigation.parseIncidentContext(query)));
test('typed FE03 builder has canonical encoded v1 signature', () => {
  assert.equal(navigation.reportIncidentHref('1','11'), '/incidents/new?branchId=1&roomId=11');
  for (const id of ['01','0','1&roomId=99','9007199254740992']) assert.throws(() => navigation.reportIncidentHref(id,'11'));
});

const options = branch => ({ branches:[{id:1,name:'One'},{id:2,name:'Two'}], rooms: branch ? [{id:branch===1?11:22,name:'Room'}] : [], areas:[],assets:[],severities:[],priorities:[],canCreate:!!branch });
async function fixture(run, config = {}) {
  const page = await browser.newPage(); const calls = []; const writes = []; const errors=[];
  page.on('pageerror', e => errors.push(e.message));
  await page.route('**/api/**', async route => {
    const req=route.request(), url=new URL(req.url()); calls.push(url.pathname+url.search);
    if(url.pathname==='/api/auth/csrf') return route.fulfill({json:{csrfToken:'fixture-token',headerName:'X-CSRF-TOKEN'}});
    if(req.method()==='POST') {
      writes.push({body:req.postDataJSON(),headers:req.headers()});
      return route.fulfill(config.postFailure ? {status:config.postFailure,json:{title:'Post denied'}} : {status:201,json:{incidentId:50,incidentNo:'INC-FIXTURE',branchId:1,roomId:11}});
    }
    if(config.failure) return route.fulfill({status:config.failure,json:{title:'Context rejected'}});
    const value=options(Number(url.searchParams.get('branchId')));
    if(config.canCreate===false)value.canCreate=false;
    return route.fulfill({json:value});
  });
  try { await run(page,calls,writes); assert.deepEqual(errors,[], 'no duplicate router/provider or runtime errors'); }
  finally { await page.close(); }
}
test('registered fixture route preserves valid room context and refresh', () => fixture(async page => {
  await page.goto(base+'/incidents/new?branchId=1&roomId=11');
  await page.getByRole('heading',{name:'Report incident'}).waitFor();
  assert.equal(await page.getByLabel('Room',{exact:true}).inputValue(),'11');
  assert.equal(await page.getByLabel('Branch',{exact:true}).isDisabled(),true);
  await page.reload(); await page.getByRole('heading',{name:'Report incident'}).waitFor();
  assert.equal(await page.getByLabel('Room',{exact:true}).inputValue(),'11');
}));
test('direct-open requires explicit branch selection, no first-branch fallback', () => fixture(async page => {
  await page.goto(base+'/incidents/new'); await page.getByRole('heading').waitFor();
  assert.equal(await page.getByLabel('Branch',{exact:true}).inputValue(),'');
  assert.equal(await page.getByRole('button',{name:'Report incident'}).isDisabled(),true);
  await page.getByLabel('Branch',{exact:true}).selectOption('1');
  await page.getByRole('button',{name:'Report incident'}).waitFor();
}));
test('invalid explicit context performs no options read and cannot submit', () => fixture(async (page,calls) => {
  await page.goto(base+'/incidents/new?branchId=0&roomId=11'); await page.getByRole('alert').waitFor();
  assert.equal(calls.length,0); assert.equal(await page.locator('form').count(),0);
}));
for(const actor of ['anonymous','restricted']) test(actor+' cannot render form or fetch eligibility', () => fixture(async (page,calls) => {
  await page.goto(base+'/incidents/new?branchId=1&roomId=11&actor='+actor); await page.getByRole('alert').waitFor(); assert.equal(calls.length,0); assert.equal(await page.locator('form').count(),0);
}));
for(const [name,status] of [['unknown/deleted/inactive/stale/cross-branch room',400],['revoked branch grant',403],['expired session',401]])
  test(name+' eligibility failure stays at explicit URL', () => fixture(async page => {
    await page.goto(base+'/incidents/new?branchId=1&roomId=11'); await page.getByRole('alert').waitFor();
    assert.equal(await page.locator('form').count(),0); assert.match(page.url(),/branchId=1&roomId=11$/);
  },{failure:status}));
test('canCreate false prevents submit even when options render', () => fixture(async page => {
  await page.goto(base+'/incidents/new?branchId=1&roomId=11'); await page.getByRole('heading').waitFor();
  assert.equal(await page.getByRole('button',{name:'Report incident'}).isDisabled(),true);
},{canCreate:false}));
test('browser back and forward replace context without first-branch fallback', () => fixture(async page => {
  await page.goto(base+'/incidents/new?branchId=1&roomId=11'); await page.getByRole('heading').waitFor();
  await page.getByRole('link',{name:'Other context'}).click();
  await page.waitForFunction(()=>document.querySelector('select[aria-label="Room"]')?.value==='22');
  await page.goBack(); await page.waitForFunction(()=>document.querySelector('select[aria-label="Room"]')?.value==='11');
  await page.goForward(); await page.waitForFunction(()=>document.querySelector('select[aria-label="Room"]')?.value==='22');
}));
async function submit(page) {
  await page.goto(base+'/incidents/new?branchId=1&roomId=11'); await page.getByRole('heading').waitFor();
  await page.getByLabel('Title',{exact:true}).fill('Fault'); await page.getByLabel('Description',{exact:true}).fill('Broken fixture');
  await page.getByLabel('Discovered at').fill('2026-11-11T10:00'); await page.getByRole('button',{name:'Report incident'}).click();
}
test('create uses session-bound CSRF transport and aligned DTO without reporter', () => fixture(async (page,calls,writes) => {
  await submit(page); await page.getByRole('status').filter({hasText:'INC-FIXTURE'}).waitFor();
  assert.equal(writes.length,1); assert.equal(writes[0].headers['x-csrf-token'],'fixture-token');
  assert.equal('reportedBy' in writes[0].body,false); assert.equal(writes[0].body.roomId,11); assert.equal(writes[0].body.branchId,1);
  assert.match(page.url(),/branchId=1&roomId=11$/); assert.ok(calls.includes('/api/auth/csrf'));
}));
for(const status of [401,403,500]) test('POST '+status+' disables further submit and does not retry', () => fixture(async (page,calls,writes) => {
  await submit(page); await page.getByRole('alert').waitFor(); assert.equal(writes.length,1);
  assert.equal(await page.getByRole('button',{name:'Report incident'}).isDisabled(),true);
},{postFailure:status}));
