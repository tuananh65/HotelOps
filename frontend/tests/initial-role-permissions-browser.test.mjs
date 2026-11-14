import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { build } from "esbuild";
import { readFileSync } from "node:fs";
import { chromium } from "@playwright/test";
import { session } from "./workday-fixtures.mjs";
const bundle = await build({
  stdin: {
    loader: "tsx",
    resolveDir: process.cwd(),
    contents: `import React,{useState}from'react';import{createRoot}from'react-dom/client';import{AuthProvider}from'./src/auth/AuthProvider';import{createAuthStore}from'./src/auth/authStore';import{createAuthApi}from'./src/auth/authApi';import{InitialRolePermissionsPanel}from'./src/roles/InitialRolePermissionsPanel';const s=window.scenario,calls=[],grants=new Map();let finish;const permission={permissionId:10,permissionCode:'ROOM_VIEW',permissionName:'View rooms',moduleCode:'ROOM',resourceCode:'room',actionCode:'view',description:null};window.fetch=async(u,o)=>{calls.push({url:u,method:o.method});if(u==='/api/auth/session')return Response.json(s.anonymous?{}:{...${JSON.stringify(session())},user:{id:s.actorId??"7",username:"synthetic",name:"Synthetic"},mustChangePassword:!!s.restricted},{status:s.anonymous?401:200});if(u==='/api/auth/csrf'){const run=()=>{const r=Response.json(s.csrfStatus?{detail:'SECRET'}:{headerName:'X-CSRF-TOKEN',csrfToken:'SYNTHETIC'},{status:s.csrfStatus??200});if(s.csrfUnreadable)r.text=async()=>{throw Error('SECRET')};return r;};if(s.deferCsrf)return new Promise(r=>finish=()=>r(run()));return run();}if(u.startsWith('/api/permissions/catalog'))return Response.json({items:[permission,{...permission,permissionId:11,permissionCode:'USER_MANAGE',permissionName:'Manage users',moduleCode:'ADMIN'}],page:1,pageSize:25,total:2});const roleId=Number(u.split('/')[3]);if(o.method==='GET'&&s.readStatus)return Response.json({detail:'SECRET'},{status:s.readStatus});if(o.method==='POST'){const run=()=>{if(s.network)throw Error('SECRET');if(s.status){const r=Response.json({detail:'SECRET',traceId:'SECRET'},{status:s.status});if(s.postUnreadable)r.text=async()=>{throw Error('SECRET')};return r;}const g=JSON.parse(o.body).grants;grants.set(roleId,g);return Response.json(s.malformed?{}:{contractVersion:1,roleId,grants:g},{status:201})};if(s.defer)return new Promise(r=>finish=()=>r(run()));return run();}const g=grants.get(roleId)??[];return Response.json({role:{roleId,roleCode:'CUSTOM_TEST',roleName:'Custom '+roleId,description:null,isSystemRole:!!s.system,isActive:true,assignedUserCount:0,permissionGrantCount:g.length},grants:g.map((v,i)=>({rolePermissionId:60+i,permission,dataScope:v.dataScope}))});};let store=createAuthStore(createAuthApi(window.fetch));function App(){const[id,setId]=useState(7);const[visible,setVisible]=useState(true);return <><button onClick={()=>setVisible(v=>!v)}>Toggle editor</button><button onClick={()=>setId(v=>v===7?8:7)}>Change role</button>{visible&&<InitialRolePermissionsPanel roleId={id} dispatchEnabled={s.enabled??false}/>}</>};const app=<App/>,root=createRoot(document.getElementById('root'));let revision=0;const render=()=>root.render(<AuthProvider key={revision} store={store} fallback={<p>Checking session</p>}>{app}</AuthProvider>);render();window.fixture={calls,finish:()=>finish(),replace:()=>store.revalidate(),replaceProvider:()=>{store=createAuthStore(createAuthApi(window.fetch));revision++;render();}};`,
  },
  plugins: [
    {
      name: "observe-operation-results",
      setup(b) {
        b.onLoad({ filter: /initialRolePermissionsApi\.ts$/ }, (args) => ({
          loader: "ts",
          contents:
            readFileSync(args.path, "utf8").replace(
              "export function createInitialRolePermissionsApi",
              "function baseInitialRolePermissionsApi",
            ) +
            `
export function createInitialRolePermissionsApi(...args: Parameters<typeof baseInitialRolePermissionsApi>){const api=baseInitialRolePermissionsApi(...args);return {...api,async save(...input: Parameters<typeof api.save>){(window as any).operationStates.push('PENDING');const r=await api.save(...input);(window as any).operationStates.push(r.outcome);return r;}}}`,
        }));
      },
    },
  ],
  bundle: true,
  write: false,
  format: "iife",
  jsx: "automatic",
  define: { "process.env.NODE_ENV": '"production"' },
});
let browser;
before(async () => {
  browser = await chromium.launch({
    executablePath:
      "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
    headless: true,
  });
});
after(async () => browser?.close());
async function run(s, fn) {
  const p = await browser.newPage();
  p.setDefaultTimeout(6000);
  const errors = [];
  p.on("pageerror", (e) => errors.push(e.message));
  await p.route("**/*", (r) =>
    r.request().resourceType() === "document"
      ? r.fulfill({ contentType: "text/html", body: '<div id="root"></div>' })
      : r.abort(),
  );
  try {
    await p.goto("https://offline.test");
    await p.evaluate((s) => {
      window.scenario = s;
      window.operationStates = [];
      for (const k of ["localStorage", "sessionStorage"])
        Object.defineProperty(window, k, {
          get() {
            throw Error("Storage forbidden");
          },
        });
    }, s);
    await p.addScriptTag({ content: bundle.outputFiles[0].text });
    await fn(p);
    assert.deepEqual(errors, []);
    assert.ok(!(await p.locator("body").innerText()).includes("SECRET"));
  } finally {
    await p.close();
  }
}
async function review(p) {
  await p.getByLabel("ROOM_VIEW", { exact: true }).check();
  await p
    .getByRole("button", { name: "Review permissions", exact: true })
    .click();
  await p
    .getByRole("region", { name: "Review permissions", exact: true })
    .waitFor();
}
const posts = (p) =>
  p.evaluate(() => window.fixture.calls.filter((c) => c.method === "POST"));
test("disabled capability loads real catalog but cannot dispatch", () =>
  run({}, async (p) => {
    await p
      .getByText("Mutation dispatch OFF pending runtime approval.")
      .waitFor();
    assert.equal(
      await p.getByLabel("ROOM_VIEW", { exact: true }).isDisabled(),
      true,
    );
    assert.equal((await posts(p)).length, 0);
  }));
for (const s of [{ anonymous: true }, { restricted: true }])
  test(
    "unconfirmed session never reads grants or sends mutation " +
      JSON.stringify(s),
    () =>
      run(s, async (p) => {
        await p.getByText(/Checking session|Confirmed unrestricted/).waitFor();
        assert.equal((await posts(p)).length, 0);
        assert.equal(
          (await p.evaluate(() => window.fixture.calls)).some((c) =>
            c.url.startsWith("/api/roles/"),
          ),
          false,
        );
      }),
  );
test("review confirm success refreshes authoritative grants, unsupported admin selection disabled", () =>
  run({ enabled: true }, async (p) => {
    await review(p);
    assert.equal((await posts(p)).length, 0);
    assert.equal(
      await p.getByLabel("USER_MANAGE", { exact: true }).isDisabled(),
      true,
    );
    await p.getByRole("button", { name: "Confirm and save" }).click();
    await p.getByText("Existing grants: 1", { exact: true }).waitFor();
    assert.equal((await posts(p)).length, 1);
    await p.getByText("ROOM_VIEW: BRANCH", { exact: true }).waitFor();
  }));
test("synchronous double confirmation sends one POST", () =>
  run({ enabled: true }, async (p) => {
    await review(p);
    await p.getByRole("button", { name: "Confirm and save" }).evaluate((b) => {
      b.click();
      b.click();
    });
    await p.getByText("Existing grants: 1", { exact: true }).waitFor();
    assert.equal((await posts(p)).length, 1);
  }));
for (const status of [403, 409])
  test("business rejection " + status + " is sanitized and no retry", () =>
    run({ enabled: true, status }, async (p) => {
      await review(p);
      await p.getByRole("button", { name: "Confirm and save" }).click();
      await p.getByRole("status").waitFor();
      assert.equal((await posts(p)).length, 1);
      assert.equal(
        await p
          .getByRole("region", { name: "Review permissions", exact: true })
          .count(),
        0,
      );
    }),
  );
for (const s of [{ network: true }, { status: 503 }, { malformed: true }])
  test("unknown outcome locks replay " + JSON.stringify(s), () =>
    run({ enabled: true, ...s }, async (p) => {
      await review(p);
      await p.getByRole("button", { name: "Confirm and save" }).click();
      await p.getByText(/Outcome unknown/).waitFor();
      assert.equal(
        await p
          .getByRole("button", { name: "Review permissions", exact: true })
          .isDisabled(),
        true,
      );
      assert.equal((await posts(p)).length, 1);
    }),
  );
test("401 clears role snapshot and changing target cannot bypass recovery", () =>
  run({ enabled: true, status: 401 }, async (p) => {
    await review(p);
    await p.getByRole("button", { name: "Confirm and save" }).click();
    await p.getByText("Confirmed unrestricted session required.").waitFor();
    await p.getByRole("button", { name: "Change role" }).click();
    assert.equal(await p.getByText("Custom 8", { exact: true }).count(), 0);
    assert.equal((await posts(p)).length, 1);
  }));
for (const operation of ["Change role", "replace"])
  test("late success discarded after " + operation, () =>
    run({ enabled: true, defer: true }, async (p) => {
      await review(p);
      await p.getByRole("button", { name: "Confirm and save" }).click();
      await p.waitForFunction(() =>
        window.fixture.calls.some((c) => c.method === "POST"),
      );
      if (operation === "replace")
        await p.evaluate(() => window.fixture.replace());
      else await p.getByRole("button", { name: "Change role" }).click();
      await p
        .getByText("Custom " + (operation === "replace" ? 7 : 8), {
          exact: true,
        })
        .waitFor();
      await p.evaluate(() => window.fixture.finish());
      assert.equal(
        await p.getByText(/Initial permissions confirmed/).count(),
        0,
      );
      assert.equal((await posts(p)).length, 1);
    }),
  );

for (const csrfStatus of [401, 403])
  test("CSRF bootstrap " + csrfStatus + " never sends POST", () =>
    run({ enabled: true, csrfStatus }, async (p) => {
      await review(p);
      await p.getByRole("button", { name: "Confirm and save" }).click();
      if (csrfStatus === 401)
        await p.getByText("Confirmed unrestricted session required.").waitFor();
      else await p.getByText("Access denied by server.").waitFor();
      assert.equal((await posts(p)).length, 0);
    }),
  );

for (const readStatus of [403, 503])
  test(
    "read denial/unavailable " + readStatus + " is not empty editable role",
    () =>
      run({ enabled: true, readStatus }, async (p) => {
        await p.getByText("No editable role snapshot available.").waitFor();
        assert.equal(await p.getByRole("checkbox").count(), 0);
        assert.equal(
          await p.getByText("Loading role and catalog...").count(),
          0,
        );
        assert.equal((await posts(p)).length, 0);
      }),
  );
for (const unreadable of [false, true])
  test(`csrf 401 unreadable=${unreadable} requires explicit recovery`, () =>
    run(
      { enabled: true, csrfStatus: 401, csrfUnreadable: unreadable },
      async (p) => {
        await review(p);
        await p.getByRole("button", { name: "Confirm and save" }).click();
        await p.getByText("Confirmed unrestricted session required.").waitFor();
        assert.equal(
          await p.getByLabel("ROOM_VIEW", { exact: true }).count(),
          0,
        );
        assert.equal((await posts(p)).length, 0);
        await p.getByRole("button", { name: "Revalidate session" }).click();
        await p.getByLabel("ROOM_VIEW", { exact: true }).waitFor();
        assert.equal(
          await p.getByLabel("ROOM_VIEW", { exact: true }).isChecked(),
          false,
        );
        assert.equal((await posts(p)).length, 0);
      },
    ));
test("unreadable POST 401 preserves uncertainty across recovery", () =>
  run({ enabled: true, status: 401, postUnreadable: true }, async (p) => {
    await review(p);
    await p.getByRole("button", { name: "Confirm and save" }).click();
    await p.getByText("Confirmed unrestricted session required.").waitFor();
    assert.equal((await posts(p)).length, 1);
    await p.getByRole("button", { name: "Revalidate session" }).click();
    await p
      .getByText(
        "Outcome unknown. Do not replay. Verify with an authorized owner.",
      )
      .waitFor();
    assert.equal(await p.getByLabel("ROOM_VIEW", { exact: true }).count(), 0);
    assert.equal((await posts(p)).length, 1);
  }));
for (const replacement of ["session", "role"])
  for (const phase of ["csrf", "post"])
    test(`late unreadable 401 ${phase} after ${replacement} replacement cannot invalidate current editor`, () =>
      run(
        {
          enabled: true,
          ...(phase === "csrf"
            ? { deferCsrf: true, csrfStatus: 401, csrfUnreadable: true }
            : { defer: true, status: 401, postUnreadable: true }),
        },
        async (p) => {
          await review(p);
          await p.getByRole("button", { name: "Confirm and save" }).click();
          await p.waitForFunction(
            (phase) =>
              window.fixture.calls.some((c) =>
                phase === "csrf"
                  ? c.url === "/api/auth/csrf"
                  : c.method === "POST",
              ),
            phase,
          );
          if (replacement === "session")
            await p.evaluate(() => window.fixture.replace());
          else await p.getByRole("button", { name: "Change role" }).click();
          await p
            .getByText("Custom " + (replacement === "session" ? 7 : 8), {
              exact: true,
            })
            .waitFor();
          await p.evaluate(() => window.fixture.finish());
          await p.waitForTimeout(50);
          assert.equal(
            await p
              .getByText("Confirmed unrestricted session required.")
              .count(),
            0,
          );
          assert.equal(
            await p.getByLabel("ROOM_VIEW", { exact: true }).isEnabled(),
            true,
          );
          assert.equal((await posts(p)).length, phase === "csrf" ? 0 : 1);
        },
      ));
for (const status of [403, 503])
  test(`unreadable CSRF ${status} denies without session recovery or POST`, () =>
    run(
      { enabled: true, csrfStatus: status, csrfUnreadable: true },
      async (p) => {
        await review(p);
        await p.getByRole("button", { name: "Confirm and save" }).click();
        await p
          .getByText(
            status === 403
              ? "Access denied by server."
              : "Initial configuration not confirmed.",
          )
          .waitFor();
        assert.equal(
          await p.getByRole("button", { name: "Revalidate session" }).count(),
          0,
        );
        assert.equal((await posts(p)).length, 0);
      },
    ));

async function start080(p) {
  await review(p);
  await p.getByRole("button", { name: "Confirm and save" }).click();
}
async function settled080(p, outcome) {
  await p.waitForFunction((o) => window.operationStates.includes(o), outcome);
}
async function noReplay080(p) {
  const c = p.getByLabel("ROOM_VIEW", { exact: true });
  if ((await c.count()) && (await c.isEnabled())) {
    const reviewButton = p.getByRole("button", {
      name: "Review permissions",
      exact: true,
    });
    if (await reviewButton.isDisabled()) {
      // A stale editor may still let the user inspect/select permissions, but not review/save.
      await reviewButton.evaluate((button) => button.click());
      assert.equal(
        await p.getByRole("button", { name: "Confirm and save" }).count(),
        0,
      );
    } else {
      await start080(p);
    }
    await p
      .waitForFunction(
        () =>
          window.fixture.calls.filter((c) => c.method === "POST").length > 1,
        undefined,
        { timeout: 1000 },
      )
      .catch(() => {});
  }
  assert.equal(
    (await posts(p)).length,
    1,
    "uncertain operation must not allow second POST",
  );
}
for (const mode of ["unmount", "provider", "role"])
  test(`080 pending ${mode} late unknown lock`, () =>
    run(
      {
        enabled: true,
        defer: true,
        status: mode === "provider" ? 401 : 503,
        postUnreadable: true,
      },
      async (p) => {
        await start080(p);
        await p.waitForFunction(() =>
          window.fixture.calls.some((c) => c.method === "POST"),
        );
        if (mode === "unmount")
          await p.getByRole("button", { name: "Toggle editor" }).click();
        if (mode === "provider")
          await p.evaluate(() => window.fixture.replaceProvider());
        if (mode === "provider")
          await p.getByText("Custom 7", { exact: true }).waitFor();
        if (mode === "role")
          await p.getByRole("button", { name: "Change role" }).click();
        await p.evaluate(() => window.fixture.finish());
        await settled080(p, "OUTCOME_UNKNOWN");
        if (mode === "unmount")
          await p.getByRole("button", { name: "Toggle editor" }).click();
        if (mode === "role")
          await p.getByRole("button", { name: "Change role" }).click();
        await p.getByText("Custom 7", { exact: true }).waitFor();
        assert.equal(
          await p.getByText("Confirmed unrestricted session required.").count(),
          0,
        );
        await noReplay080(p);
      },
    ));
for (const mode of ["revalidate", "role"])
  test(`080 unknown survives ${mode}`, () =>
    run({ enabled: true, network: true }, async (p) => {
      await start080(p);
      await settled080(p, "OUTCOME_UNKNOWN");
      if (mode === "revalidate")
        await p.evaluate(() => window.fixture.replace());
      else {
        await p.getByRole("button", { name: "Change role" }).click();
        await p.getByText("Custom 8", { exact: true }).waitFor();
        assert.equal(
          await p.getByLabel("ROOM_VIEW", { exact: true }).isEnabled(),
          true,
        );
        await p.getByRole("button", { name: "Change role" }).click();
      }
      await p.getByText("Custom 7", { exact: true }).waitFor();
      await noReplay080(p);
    }));
test("080 different principal same role characterization", () =>
  run({ enabled: true, status: 401, postUnreadable: true }, async (p) => {
    await start080(p);
    await settled080(p, "OUTCOME_UNKNOWN");
    await p.evaluate(() => {
      window.scenario.actorId = "8";
      window.fixture.replaceProvider();
    });
    await p.getByText("Custom 7", { exact: true }).waitFor();
    assert.equal((await posts(p)).length, 1);
    assert.equal(
      await p.getByLabel("ROOM_VIEW", { exact: true }).isEnabled(),
      true,
    );
  }));
test("080 pending duplicate blocked then CONFIRMED", () =>
  run({ enabled: true, defer: true }, async (p) => {
    await review(p);
    await p.getByRole("button", { name: "Confirm and save" }).evaluate((b) => {
      b.click();
      b.click();
    });
    await p.waitForFunction(() =>
      window.fixture.calls.some((c) => c.method === "POST"),
    );
    assert.deepEqual(await p.evaluate(() => window.operationStates), [
      "PENDING",
    ]);
    assert.equal((await posts(p)).length, 1);
    await p.evaluate(() => window.fixture.finish());
    await settled080(p, "CONFIRMED");
  }));
for (const [s, o, n] of [
  [{ status: 409 }, "REJECTED", 1],
  [{ csrfStatus: 401 }, "NOT_DISPATCHED", 0],
])
  test(`080 ${o}`, () =>
    run({ enabled: true, ...s }, async (p) => {
      await start080(p);
      await settled080(p, o);
      assert.equal((await posts(p)).length, n);
    }));
test("080 reload limitation characterization", () =>
  run({ enabled: true, network: true }, async (p) => {
    await start080(p);
    await settled080(p, "OUTCOME_UNKNOWN");
    await p.reload();
    await p.evaluate(() => {
      window.scenario = { enabled: true };
      window.operationStates = [];
    });
    await p.addScriptTag({ content: bundle.outputFiles[0].text });
    await p.getByText("Custom 7", { exact: true }).waitFor();
    assert.equal(
      await p.getByLabel("ROOM_VIEW", { exact: true }).isEnabled(),
      true,
    );
    assert.equal((await posts(p)).length, 0);
  }));

// B03: pending exclusion must hold BEFORE the original response arrives.
for (const mode of ["unmount", "provider", "session", "role"])
  test("B03 pending reservation before completion: " + mode, () =>
    run(
      { enabled: true, defer: true, status: 503, postUnreadable: true },
      async (p) => {
        await start080(p);
        await p.waitForFunction(() =>
          window.fixture.calls.some((c) => c.method === "POST"),
        );
        if (mode === "unmount") {
          await p.getByRole("button", { name: "Toggle editor" }).click();
          await p.getByRole("button", { name: "Toggle editor" }).click();
        } else if (mode === "role") {
          await p.getByRole("button", { name: "Change role" }).click();
          await p.getByText("Custom 8", { exact: true }).waitFor();
          await p.getByRole("button", { name: "Change role" }).click();
        } else
          await p.evaluate(
            (mode) =>
              mode === "provider"
                ? window.fixture.replaceProvider()
                : window.fixture.replace(),
            mode,
          );
        await p.getByText("Custom 7", { exact: true }).waitFor();
        const select = p.getByLabel("ROOM_VIEW", { exact: true });
        if (await select.isEnabled()) await select.check();
        assert.equal(
          await p
            .getByRole("button", { name: "Review permissions", exact: true })
            .isDisabled(),
          true,
        );
        await noReplay080(p);
        await p.evaluate(() => window.fixture.finish());
        await settled080(p, "OUTCOME_UNKNOWN");
        await noReplay080(p);
      },
    ),
  );
for (const profile of [
  { status: 401, postUnreadable: true },
  { status: 503, postUnreadable: true },
  { network: true },
  { malformed: true },
])
  test(
    "B03 provider round trip retains unknown " + JSON.stringify(profile),
    () =>
      run({ enabled: true, ...profile }, async (p) => {
        await start080(p);
        await settled080(p, "OUTCOME_UNKNOWN");
        await p.evaluate(() => window.fixture.replaceProvider());
        await p.getByText(/Outcome unknown/).waitFor();
        await noReplay080(p);
        await p.evaluate(() => {
          window.scenario.actorId = "8";
          window.fixture.replaceProvider();
        });
        await p.getByText("Custom 7", { exact: true }).waitFor();
        assert.equal(await p.getByText(/Outcome unknown/).count(), 0);
        if (profile.malformed) {
          // This fixture committed the grants but returned malformed success.
          // Another principal sees the server's configured role, not an uncertainty lock.
          await p.getByText("Existing grants: 1", { exact: true }).waitFor();
          assert.equal(
            await p
              .getByRole("button", { name: "Review permissions", exact: true })
              .count(),
            0,
          );
        } else {
          await review(p);
          assert.equal(
            await p
              .getByRole("button", { name: "Confirm and save" })
              .isEnabled(),
            true,
          );
        }
        await p.evaluate(() => {
          window.scenario.actorId = "7";
          window.fixture.replaceProvider();
        });
        await p.getByText(/Outcome unknown/).waitFor();
        assert.equal(
          await p.getByRole("button", { name: "Confirm and save" }).count(),
          0,
        );
        await noReplay080(p);
      }),
  );
