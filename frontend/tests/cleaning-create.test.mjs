import { test } from "node:test";
import assert from "node:assert/strict";
import { build } from "esbuild";
await build({
  entryPoints: ["src/api/dev1/cleaningCreate.ts"],
  bundle: true,
  platform: "node",
  format: "esm",
  outfile: ".test-output/fs008/create.mjs",
  define: { "import.meta.env": "{}" },
});
const { cleaningOptions, createCleaning } =
  await import("../.test-output/fs008/create.mjs");
const input = {
  roomId: 3,
  cleaningTypeId: 4,
  priorityId: 5,
  dueAt: "2027-01-01T00:00:00Z",
  instruction: null,
};
test("create uses shared cookie transport and fresh CSRF, no client actor or retries", async () => {
  const calls = [];
  globalThis.fetch = async (url, o) => {
    calls.push({ url, o });
    assert.equal(o.credentials, "include");
    if (url.endsWith("/auth/csrf"))
      return Response.json({ headerName: "X-CSRF-TOKEN", csrfToken: "fresh" });
    assert.equal(o.method, "POST");
    assert.equal(o.headers["X-CSRF-TOKEN"], "fresh");
    assert.deepEqual(JSON.parse(o.body), input);
    return Response.json(
      {
        id: 6,
        branchId: 7,
        roomId: 3,
        taskNumber: "CL-6",
        state: "PENDING_ASSIGNMENT",
        readiness: "PENDING_CLEANING",
        hasEffectiveBlock: false,
      },
      { status: 201 },
    );
  };
  assert.equal((await createCleaning("7", input)).id, 6);
  assert.equal(calls.length, 2);
});
test("create conflicts fail once without unsafe automatic retry", async () => {
  let posts = 0;
  globalThis.fetch = async (url) => {
    if (url.endsWith("/auth/csrf"))
      return Response.json({ headerName: "X-CSRF-TOKEN", csrfToken: "fresh" });
    posts++;
    return Response.json({ title: "DUPLICATE" }, { status: 409 });
  };
  await assert.rejects(createCleaning("7", input), (e) => e.status === 409);
  assert.equal(posts, 1);
});
test("linked cleaning binds explicit requirement and source version and never retries stale creation", async () => {
  let posts=0;
  const linked={...input,internalRequestId:8,requestLink:{version:"AAAAAAAAAAE=",mandatory:false}};
  globalThis.fetch=async(url,o)=>{
    if(url.endsWith("/auth/csrf"))return Response.json({headerName:"X-CSRF-TOKEN",csrfToken:"fresh"});
    posts++;assert.equal(o.headers["If-Match"],'"AAAAAAAAAAE="');assert.deepEqual(JSON.parse(o.body),linked);
    return Response.json({title:"STALE"},{status:412});
  };
  await assert.rejects(createCleaning("7",linked),e=>e.status===412);assert.equal(posts,1);
  await assert.rejects(createCleaning("7",{...input,internalRequestId:8}),/SOURCE_VERSION_REQUIRED/);
  assert.equal(posts,1);
});
test("create refuses invalid inputs and malformed CSRF before business POST", async () => {
  globalThis.fetch = () => {
    throw Error("must not fetch");
  };
  await assert.rejects(createCleaning("7", { ...input, roomId: 0 }));
  globalThis.fetch = async () => Response.json({ csrfToken: "bad" });
  await assert.rejects(createCleaning("7", input), /INVALID_CSRF_RESPONSE/);
});
test("options verifies exact room and branch and references", async () => {
  globalThis.fetch = async () =>
    Response.json({
      roomId: 3,
      roomNumber: "101",
      branchId: 8,
      cleaningTypes: [],
      priorities: [],
      cleaningItemCount: 2,
    });
  await assert.rejects(cleaningOptions("7", "3"), /INVALID_CREATE_OPTIONS/);
});

const planningOptions={roomId:3,roomNumber:'101',branchId:7,cleaningTypes:[],priorities:[],cleaningItemCount:0};
for(const minutes of [null,undefined,30,45]) test('room-type planning default '+minutes+' is read without inventing a deadline',async()=>{
 globalThis.fetch=async()=>Response.json({...planningOptions,standardCleaningMinutes:minutes});
 const result=await cleaningOptions('7','3');
 assert.equal(result.standardCleaningMinutes,minutes);
 assert.equal(result.dueAt,undefined);
});
for(const minutes of [0,-1,1.5,'30',2147483648]) test('invalid standard cleaning minutes '+minutes+' rejects options',async()=>{
 globalThis.fetch=async()=>Response.json({...planningOptions,standardCleaningMinutes:minutes});
 await assert.rejects(cleaningOptions('7','3'),/INVALID_CREATE_OPTIONS/);
});
test('room-type changes are reread and estimate never overrides the submitted deadline',async()=>{
 let minutes=30;
 globalThis.fetch=async()=>Response.json({...planningOptions,standardCleaningMinutes:minutes});
 assert.equal((await cleaningOptions('7','3')).standardCleaningMinutes,30);
 minutes=45;assert.equal((await cleaningOptions('7','3')).standardCleaningMinutes,45);
 let body;
 globalThis.fetch=async(url,o)=>{
  if(url.endsWith('/auth/csrf'))return Response.json({headerName:'X-CSRF-TOKEN',csrfToken:'fresh'});
  body=JSON.parse(o.body);
  return Response.json({id:6,branchId:7,roomId:3,taskNumber:'CL-6',state:'PENDING_ASSIGNMENT',readiness:'PENDING_CLEANING',hasEffectiveBlock:false,standardCleaningMinutes:45},{status:201});
 };
 await createCleaning('7',{...input,standardCleaningMinutes:999});
 assert.equal(body.dueAt,input.dueAt);assert.equal(body.standardCleaningMinutes,undefined);
});
