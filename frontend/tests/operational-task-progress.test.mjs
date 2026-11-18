import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import ts from "typescript";
const source = ts.transpileModule(
  readFileSync(
    new URL("../src/operationalTasks/progress.ts", import.meta.url),
    "utf8",
  ),
  {
    compilerOptions: {
      module: ts.ModuleKind.ESNext,
      target: ts.ScriptTarget.ES2022,
    },
  },
).outputText;
const { resumeTask, pauseTask, readPauseReasons, readTaskProgress, saveTaskProgress, initializeTaskProgress, TaskProgressError } = await import(
  "data:text/javascript;base64," + Buffer.from(source).toString("base64")
);
const data = {
  id: 4,
  branchId: 2,
  kind: "task",
  state: "IN_PROGRESS",
  percent: 10,
  evidence: [],
  items: [
    {
      id: 8,
      label: "Criterion",
      required: true,
      order: 0,
      done: null,
      outcome: null,
      note: null,
    },
  ],
};
const etag = '"0000000000000001"';
const current = { data, etag };
const input = {
  percent: 100,
  items: [{ id: 8, outcome: "PASS", note: "Checked" }],
  note: "Progress only",
};
const signal = new AbortController().signal;
const response = (body = data, version = etag) =>
  Response.json(body, { headers: { ETag: version } });
test("task response requires exact identity, kind, state, criteria and strong version", async (t) => {
  for (const body of [
    { ...data, branchId: 3 },
    { ...data, id: 5 },
    { ...data, kind: "cleaning" },
    { ...data, state: "COMPLETED" },
    { ...data, percent: 101 },
    { ...data, items: [...data.items, ...data.items] },
    { ...data, items: [{ ...data.items[0], outcome: "UNKNOWN" }] },
  ]) {
    const mock = t.mock.method(globalThis, "fetch", async () => response(body));
    await assert.rejects(
      readTaskProgress(2, 4, signal),
      /Invalid task progress response/,
    );
    mock.mock.restore();
  }
  t.mock.method(globalThis, "fetch", async () => response(data, "W/" + etag));
  await assert.rejects(
    readTaskProgress(2, 4, signal),
    /Invalid task progress version/,
  );
});
test("paused task is accepted without converting its state", async (t) => {
  t.mock.method(globalThis, "fetch", async () =>
    response({ ...data, state: "PAUSED" }),
  );
  assert.equal((await readTaskProgress(2, 4, signal)).data.state, "PAUSED");
});
test("save gets fresh CSRF and sends exact criterion outcome with ETag, no completion request", async (t) => {
  const calls = [];
  t.mock.method(globalThis, "fetch", async (url, options) => {
    calls.push(url);
    assert.equal(options.credentials, "same-origin");
    assert.equal(options.cache, "no-store");
    assert.equal(options.redirect, "error");
    assert.equal(options.signal, signal);
    if (url === "/api/auth/csrf")
      return Response.json({
        headerName: "X-CSRF-TOKEN",
        csrfToken: "synthetic-token",
      });
    assert.equal(url, "/api/branches/2/operational-tasks/4/progress");
    assert.equal(options.method, "PUT");
    assert.equal(options.headers["If-Match"], etag);
    assert.equal(options.headers["X-CSRF-TOKEN"], "synthetic-token");
    assert.deepEqual(JSON.parse(options.body), input);
    return response({ ...data, percent: 100 });
  });
  assert.equal(
    (await saveTaskProgress(2, 4, current, input, signal)).data.percent,
    100,
  );
  assert.equal(calls.length, 2);
});
test("foreign criteria, duplicates, invalid percent and control text never reach network", async (t) => {
  t.mock.method(globalThis, "fetch", () => assert.fail("network"));
  for (const bad of [
    { ...input, percent: -1 },
    { ...input, items: [{ ...input.items[0], id: 9 }] },
    { ...input, items: [...input.items, ...input.items] },
    { ...input, note: "bad\ntext" },
  ])
    await assert.rejects(
      saveTaskProgress(2, 4, current, bad, signal),
      /Invalid task progress input/,
    );
});
test("invalid CSRF cannot dispatch a write", async (t) => {
  const mock = t.mock.method(globalThis, "fetch", async () =>
    Response.json({ headerName: "OTHER", csrfToken: "token" }),
  );
  await assert.rejects(
    saveTaskProgress(2, 4, current, input, signal),
    /Phản hồi CSRF không hợp lệ/,
  );
  assert.equal(mock.mock.callCount(), 1);
});
test("authority abort after CSRF prevents write", async (t) => {
  const controller = new AbortController();
  const mock = t.mock.method(globalThis, "fetch", async () => {
    controller.abort();
    return Response.json({ headerName: "X-CSRF-TOKEN", csrfToken: "token" });
  });
  await assert.rejects(
    saveTaskProgress(2, 4, current, input, controller.signal),
    { name: "AbortError" },
  );
  assert.equal(mock.mock.callCount(), 1);
});
test("stale and unknown outcomes are never retried", async (t) => {
  for (const status of [412, 503]) {
    const mock = t.mock.method(globalThis, "fetch", async (url) =>
      url === "/api/auth/csrf"
        ? Response.json({ headerName: "X-CSRF-TOKEN", csrfToken: "token" })
        : new Response("", { status }),
    );
    await assert.rejects(
      saveTaskProgress(2, 4, current, input, signal),
      (e) => e instanceof TaskProgressError && e.status === status,
    );
    assert.equal(mock.mock.callCount(), 2);
    mock.mock.restore();
  }
});
test("unsafe IDs never reach network", async (t) => {
  t.mock.method(globalThis, "fetch", () => assert.fail("network"));
  await assert.rejects(readTaskProgress(0, 4, signal), /Invalid task identity/);
});
test("saved evidence must belong to this exact task and use its protected download endpoint", async (t) => {
  const file = {
    id: 11,
    entityType: "TASK",
    entityId: 4,
    name: "synthetic.png",
    mimeType: "image/png",
    bytes: 68,
    uploadedAt: "2026-11-15T00:00:00Z",
    downloadPath: "/api/branches/2/operational-tasks/4/progress/evidence/11",
  };
  for (const bad of [
    { ...file, entityId: 5 },
    { ...file, entityType: "INSPECTION" },
    { ...file, downloadPath: "https://example.invalid/file" },
    { ...file, downloadPath: "//example.invalid/file" },
    {
      ...file,
      downloadPath: "/api/branches/3/operational-tasks/4/progress/evidence/11",
    },
    {
      ...file,
      downloadPath: "/api/branches/2/operational-tasks/4/progress/evidence/12",
    },
    { ...file, mimeType: "text/html" },
    { ...file, bytes: -1 },
  ]) {
    const mock = t.mock.method(globalThis, "fetch", async () =>
      response({ ...data, evidence: [bad] }),
    );
    await assert.rejects(
      readTaskProgress(2, 4, signal),
      /Invalid task progress response/,
    );
    mock.mock.restore();
  }
  t.mock.method(globalThis, "fetch", async () =>
    response({ ...data, evidence: [file] }),
  );
  assert.deepEqual((await readTaskProgress(2, 4, signal)).data.evidence, [
    file,
  ]);
});

test('initialization validates definitions before dispatch',async t=>{
 t.mock.method(globalThis,'fetch',()=>assert.fail('network'));
 for(const items of [[],[{label:'A',required:true},{label:' a ',required:false}],[{label:'',required:true}]])
  await assert.rejects(initializeTaskProgress(2,4,items,'Valid setup reason',signal),/Dữ liệu tiêu chí công việc không hợp lệ/);
});
test('initialization uses fresh CSRF and validates created task identity',async t=>{
 const calls=[];
 t.mock.method(globalThis,'fetch',async(url,options)=>{
  calls.push(url);if(url==='/api/auth/csrf')return Response.json({headerName:'X-CSRF-TOKEN',csrfToken:'synthetic'});
  assert.equal(url,'/api/branches/2/operational-tasks/4/progress-initialization');assert.equal(options.method,'POST');
  assert.equal(options.headers['X-CSRF-TOKEN'],'synthetic');assert.deepEqual(JSON.parse(options.body),{items:[{label:'A',required:true}],reason:'Valid setup reason'});
  return Response.json({id:4,kind:'task',initialized:true},{status:201});
 });
 await initializeTaskProgress(2,4,[{label:' A ',required:true}],' Valid setup reason ',signal);assert.equal(calls.length,2);
});
test('duplicate initialization is not retried',async t=>{
 const mock=t.mock.method(globalThis,'fetch',async url=>url==='/api/auth/csrf'?Response.json({headerName:'X-CSRF-TOKEN',csrfToken:'synthetic'}):new Response('',{status:409}));
 await assert.rejects(initializeTaskProgress(2,4,[{label:'A',required:true}],'Valid setup reason',signal),e=>e instanceof TaskProgressError&&e.status===409);
 assert.equal(mock.mock.callCount(),2);
});

test("resume binds paused snapshot, fresh CSRF and ETag",async()=>{
 const old=globalThis.fetch,calls=[];globalThis.fetch=async(url,options)=>{calls.push({url,options});return calls.length===1?Response.json({headerName:"X-CSRF-TOKEN",csrfToken:"fresh"}):response(data,'"0000000000000002"');};
 try{const result=await resumeTask(2,4,{data:{...data,state:"PAUSED"},etag},signal);assert.equal(result.data.state,"IN_PROGRESS");assert.equal(calls[1].url,"/api/branches/2/operational-tasks/4/resume");assert.equal(calls[1].options.method,"POST");assert.equal(calls[1].options.headers["If-Match"],etag);assert.equal(calls[1].options.headers["X-CSRF-TOKEN"],"fresh");}finally{globalThis.fetch=old;}
});
test("resume rejects running state and never retries stale mutation",async()=>{
 const old=globalThis.fetch;let count=0;globalThis.fetch=async()=>++count===1?Response.json({headerName:"X-CSRF-TOKEN",csrfToken:"fresh"}):new Response(null,{status:412});
 try{await assert.rejects(()=>resumeTask(2,4,current,signal));assert.equal(count,0);await assert.rejects(()=>resumeTask(2,4,{data:{...data,state:"PAUSED"},etag},signal));assert.equal(count,2);}finally{globalThis.fetch=old;}
});

test('pause binds reason and note to running snapshot with fresh CSRF and ETag',async t=>{
 const calls=[];t.mock.method(globalThis,'fetch',async(url,options)=>{calls.push({url,options});return calls.length===1?Response.json({headerName:'X-CSRF-TOKEN',csrfToken:'fresh'}):response({...data,state:'PAUSED'},'"0000000000000002"');});
 const value=await pauseTask(2,4,current,signal,7,'Synthetic note');assert.equal(value.data.state,'PAUSED');assert.equal(calls[1].url,'/api/branches/2/operational-tasks/4/pause');assert.deepEqual(JSON.parse(calls[1].options.body),{reasonId:7,note:'Synthetic note'});assert.equal(calls[1].options.headers['If-Match'],etag);
});
test('pause rejects paused snapshot before fetching and does not retry stale write',async t=>{
 let count=0;t.mock.method(globalThis,'fetch',async()=>++count===1?Response.json({headerName:'X-CSRF-TOKEN',csrfToken:'fresh'}):new Response(null,{status:412}));
 await assert.rejects(pauseTask(2,4,{data:{...data,state:'PAUSED'},etag},signal,7,''));assert.equal(count,0);
 await assert.rejects(pauseTask(2,4,current,signal,7,''),e=>e.status===412);assert.equal(count,2);
});
test('pause catalog rejects invalid IDs and accepts backend labels',async t=>{
 t.mock.method(globalThis,'fetch',async()=>Response.json([{id:7,code:'OTHER',label:'Other'}]));assert.deepEqual(await readPauseReasons(2,4,signal),[{id:7,code:'OTHER',label:'Other'}]);
 globalThis.fetch=async()=>Response.json([{id:0,code:'OTHER',label:'Other'}]);await assert.rejects(readPauseReasons(2,4,signal),/Invalid pause reasons/);
});
