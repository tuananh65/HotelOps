import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { build } from "esbuild";
import { chromium } from "@playwright/test";
import { createServer } from "node:http";
import { mkdir } from "node:fs/promises";
await mkdir(".test-output/shared-host", { recursive: true });
await build({
  entryPoints: ["src/shared-host/returnUrl.ts"],
  outbase: "src",
  outdir: ".test-output/shared-host",
  platform: "node",
  format: "esm",
  bundle: true,
});
const { validateReturnUrl, approvedReturnDestination } =
  await import("../.test-output/shared-host/shared-host/returnUrl.js");
for (const bad of [
  "https://evil.test",
  "//evil.test",
  "/\\evil",
  "/%5cevil",
  "/%2f%2fevil",
  "/%252fevil",
  "/a\n",
  "/a%0d",
  "/%",
  "/%GG",
  "/a#fragment",
  "/a/../b",
  "/%2e%2e/a",
])
  test("deny redirect " + JSON.stringify(bad), () =>
    assert.equal(validateReturnUrl(bad), null),
  );
test("preserve local query but require fresh session and enabled destination", () => {
  const p = "/rooms/status?branchId=7&roomId=11";
  assert.equal(validateReturnUrl(p), p);
  assert.equal(approvedReturnDestination(p, false), null);
  assert.equal(approvedReturnDestination(p, true), p);
  for (const p of [
    "/incidents/new?branchId=7",
    "/inspections/assignment?branchId=7",
    "/login",
    "/unknown",
  ])
    assert.equal(approvedReturnDestination(p, true), null);
});
const bundle = await build({
  loader: { ".css": "empty" },
  stdin: {
    loader: "tsx",
    resolveDir: process.cwd(),
    contents: `
import{createAuthStore}from'./src/auth/authStore';import{createAuthApi}from'./src/auth/authApi';import React,{StrictMode}from'react';import{createRoot}from'react-dom/client';import{BrowserRouter}from'react-router-dom';import{AuthProvider}from'./src/auth/AuthProvider';import{AuthRouteHost}from'./src/auth/AuthRouteHost';
let snapshot={authorityGeneration:Object.freeze({}),status:'INITIALIZING',session:null,pending:null,error:null};const listeners=new Set();const session={user:{id:'7',username:'staff',name:'Staff'},expiresAt:'2030-01-01T00:00:00Z',absoluteExpiresAt:'2030-01-02T00:00:00Z',mustChangePassword:false};window.calls=[];window.fetch=async(url,init)=>{window.calls.push(String(url));(window.methods??=[]).push(init?.method??"GET");if(String(url)==="/api/dashboards/options")return new Response(JSON.stringify(window.dashboardOptions??{branches:[{id:7,code:"B7",name:"Permitted branch"},{id:8,code:"B8",name:"Second branch"}],departments:[{id:11,branchId:7,name:"Housekeeping"},{id:12,branchId:8,name:"Engineering"}]}),{status:window.optionsStatus??200});if(window.businessReply)return new Response(JSON.stringify(window.businessReply.body),{status:window.businessReply.status,headers:{"Content-Type":"application/json",...window.businessReply.headers}});if(window.realAuth){const response=window.realReplies[String(url)]?.shift();if(!response)throw Error("Unexpected offline request");if(response.deferred)await new Promise(r=>window.finishRealRevalidation=r);return new Response(response.status===204?null:JSON.stringify(response.body),{status:response.status,headers:{"Content-Type":"application/json"}})}return new Response('{}',{status:503})};function set(status){snapshot={authorityGeneration:Object.freeze({}),status,session:['AUTHENTICATED','RESTRICTED'].includes(status)?{...session,mustChangePassword:status==='RESTRICTED'}:null,pending:null,error:null};listeners.forEach(f=>f())}const store={invalidateSession(expected){if(expected===snapshot.authorityGeneration)set("EXPIRED_INVALID")},subscribe(f){listeners.add(f);return()=>listeners.delete(f)},getSnapshot:()=>snapshot,initialize:async()=>({ok:false,error:{kind:'network'}}),revalidate:async()=>{window.revalidationCalls=(window.revalidationCalls??0)+1;if(window.revalidationMode==="fail"){set("ERROR_UNAVAILABLE");return{ok:false,error:{kind:"network"}}}if(window.revalidationMode==="defer")await new Promise(r=>window.finishRevalidation=r);set('AUTHENTICATED');return{ok:true,data:snapshot.session}},login:async()=>{set('AUTHENTICATED');return{ok:true,data:snapshot.session}},logout:async()=>{set('ANONYMOUS');return{ok:true}},changePassword:async()=>{set('AUTHENTICATED');return{ok:true,data:snapshot.session}}};window.fixture={set,navigate(path){history.pushState(null,'',path);dispatchEvent(new PopStateEvent('popstate'))}};const host=<AuthRouteHost/>;createRoot(document.getElementById('root')).render(<StrictMode><BrowserRouter><AuthProvider store={window.realAuth?window.realStore??(window.realStore=createAuthStore(createAuthApi(window.fetch))):store} fallback={host}>{host}</AuthProvider></BrowserRouter></StrictMode>);
`,
  },
  bundle: true,
  write: false,
  format: "iife",
  jsx: "automatic",
  define: { "process.env.NODE_ENV": '"production"', "import.meta.env": "{}" },
});
let browser, server, origin;
before(async () => {
  server = createServer((req, res) => {
    res.setHeader("Content-Type", "text/html");
    res.end(
      '<div id="root"></div><script>' +
        bundle.outputFiles[0].text +
        "</script>",
    );
  });
  await new Promise((resolve) => server.listen(0, "127.0.0.1", resolve));
  origin = "http://127.0.0.1:" + server.address().port;
  browser = await chromium.launch({
    executablePath:
      "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
    headless: true,
  });
});
after(async () => {
  await browser?.close();
  await new Promise((resolve) => server?.close(resolve));
});
async function pageAt(path, status) {
  const page = await browser.newPage();
  page.setDefaultTimeout(10000);
  page.on("pageerror", (error) =>
    console.error("Browser error:", error.message),
  );
  await page.goto(origin + path);
  await page.waitForFunction(() => !!window.fixture);
  if (status) await page.evaluate((s) => window.fixture.set(s), status);
  return page;
}

const inspectionReply = {
  status: 200,
  headers: { ETag: '"0123456789ABCDEF"' },
  body: {
    id: 42,
    branchId: 7,
    roomId: 11,
    cleaningTaskId: 22,
    inspectorId: 7,
    versionId: 55,
    state: "ASSIGNED",
    dueAt: null,
    criteria: [],
    defects: [],
    evidence: [],
    severities: [{ id: 617, code: "MAJOR", name: "Database major" }],
  },
};

for (const phase of ["csrf", "mutation"])
  test(`canonical inspection authority change during ${phase} cannot replay a write`, async () => {
    const p = await pageAt("/inspections/7/42/execution");
    await p.evaluate((reply) => {
      window.businessReply = reply;
      window.fixture.set("AUTHENTICATED");
    }, inspectionReply);
    await p
      .getByRole("heading", { name: "Inspection #42", exact: true })
      .waitFor();
    await p.evaluate((phase) => {
      const send = window.fetch;
      window.inspectionWrites = 0;
      window.fetch = async (url, init) => {
        if (url === "/api/auth/csrf") {
          if (phase === "csrf")
            await new Promise((r) => (window.releaseInspection = r));
          return new Response(
            JSON.stringify({
              csrfToken: "test-csrf",
              headerName: "X-CSRF-TOKEN",
            }),
          );
        }
        if (init?.method === "POST") {
          window.inspectionWrites++;
          await new Promise((r) => (window.releaseInspection = r));
        }
        return send(url, init);
      };
    }, phase);
    await p.getByLabel("Reason", { exact: true }).fill("Start inspection");
    await p
      .getByRole("button", { name: "Start Inspection", exact: true })
      .click();
    await p.waitForFunction(
      () => typeof window.releaseInspection === "function",
    );
    const reads = await p.evaluate(() => window.calls.length);
    await p.evaluate(() => window.fixture.set("AUTHENTICATED"));
    await p
      .getByRole("button", { name: "Verify inspection access", exact: true })
      .waitFor();
    await p.evaluate(() => window.releaseInspection());
    await p.evaluate(
      () =>
        new Promise((r) =>
          requestAnimationFrame(() => requestAnimationFrame(r)),
        ),
    );
    assert.equal(
      await p.evaluate(() => window.inspectionWrites),
      phase === "csrf" ? 0 : 1,
    );
    assert.equal(
      await p.evaluate(() => window.calls.length),
      reads + (phase === "mutation" ? 1 : 0),
    );
    assert.equal(
      await p
        .getByRole("heading", { name: "Inspection #42", exact: true })
        .count(),
      0,
    );
    await p
      .getByRole("button", { name: "Verify inspection access", exact: true })
      .click();
    await p
      .getByRole("heading", { name: "Inspection #42", exact: true })
      .waitFor();
    assert.equal(
      await p.evaluate(() => window.inspectionWrites),
      phase === "csrf" ? 0 : 1,
    );
    await p.close();
  });

test("canonical inspection deep link returns after login without granting permission", () => {
  assert.equal(
    approvedReturnDestination("/inspections/7/42/execution", true),
    "/inspections/7/42/execution",
  );
  assert.equal(
    approvedReturnDestination("/inspections/7/42/execution", false),
    null,
  );
  assert.equal(
    approvedReturnDestination(
      "/inspections/9007199254740992/42/execution",
      true,
    ),
    null,
  );
});

test("canonical inspection mounts only after scoped API admission and requires explicit authority recovery", async () => {
  const p = await pageAt("/inspections/7/42/execution");
  await p.evaluate((reply) => {
    window.businessReply = reply;
    window.fixture.set("AUTHENTICATED");
  }, inspectionReply);
  await p
    .getByRole("heading", { name: "Inspection #42", exact: true })
    .waitFor();
  assert.ok(
    (await p.evaluate(() => window.calls)).every(
      (u) => u === "/api/branches/7/inspections/42/execution",
    ),
  );
  const before = await p.evaluate(() => window.calls.length);
  await p.evaluate(() => window.fixture.set("AUTHENTICATED"));
  await p
    .getByRole("button", { name: "Verify inspection access", exact: true })
    .waitFor();
  assert.equal(
    await p
      .getByRole("heading", { name: "Inspection #42", exact: true })
      .count(),
    0,
  );
  assert.equal(await p.evaluate(() => window.calls.length), before);
  await p
    .getByRole("button", { name: "Verify inspection access", exact: true })
    .click();
  await p
    .getByRole("heading", { name: "Inspection #42", exact: true })
    .waitFor();
  assert.ok((await p.evaluate(() => window.methods)).every((m) => m === "GET"));
  await p.close();
});

for (const status of [403, 503])
  test(`canonical inspection admission ${status} cannot mount forms`, async () => {
    const p = await pageAt("/inspections/7/42/execution");
    await p.evaluate((s) => {
      window.businessReply = { status: s, body: {} };
      window.fixture.set("AUTHENTICATED");
    }, status);
    await p
      .getByText("Inspection access could not be confirmed.", { exact: true })
      .waitFor();
    assert.equal(
      await p
        .getByRole("button", { name: "Start Inspection", exact: true })
        .count(),
      0,
    );
    assert.ok(
      (await p.evaluate(() => window.methods)).every((m) => m === "GET"),
    );
    await p.close();
  });

test("canonical inspection invalid route IDs never read protected resources", async () => {
  const p = await pageAt("/inspections/0/42/execution", "AUTHENTICATED");
  await p
    .getByText("Invalid inspection or branch ID.", { exact: true })
    .waitFor();
  assert.deepEqual(await p.evaluate(() => window.calls), []);
  await p.close();
});

for (const path of [
  "/users",
  "/users/42",
  "/users/42/roles/assign",
  "/hotel?branchId=7",
  "/audit",
  "/dashboard",
  "/notifications",
  "/inspections/7/42/execution",
]) {
  for (const state of [
    "INITIALIZING",
    "ANONYMOUS",
    "RESTRICTED",
    "ERROR_UNAVAILABLE",
  ]) {
    test(`canonical read route ${path} denies business reads for ${state}`, async () => {
      const p = await pageAt(path, state);
      await p.evaluate(
        () =>
          new Promise((resolve) =>
            requestAnimationFrame(() => requestAnimationFrame(resolve)),
          ),
      );
      assert.deepEqual(await p.evaluate(() => window.calls), []);
      assert.equal(
        await p
          .getByRole("heading", { name: "User directory", exact: true })
          .count(),
        0,
      );
      assert.equal(
        await p
          .getByRole("heading", {
            name: "Hotel structure — Branch structure",
            exact: true,
          })
          .count(),
        0,
      );
      await p.close();
    });
  }
}

test("canonical operations navigation reaches integrated Users and Structure reads", async () => {
  const p = await pageAt("/rooms/status", "AUTHENTICATED");
  await p.getByRole("link", { name: "Users", exact: true }).click();
  await p
    .getByRole("heading", { name: "User directory", exact: true })
    .waitFor();
  await p.waitForFunction(() =>
    window.calls.some((url) => url.startsWith("/api/users")),
  );
  await p.getByRole("link", { name: "Workspace", exact: true }).click();
  await p.getByRole("link", { name: "Structure", exact: true }).click();
  await p
    .getByRole("heading", {
      name: "Hotel structure — Branch structure",
      exact: true,
    })
    .waitFor();
  assert.equal(await p.getByText("Page not found", { exact: true }).count(), 0);
  await p.close();
});

test("canonical account navigation reaches the role assignment boundary without a mutation", async () => {
  const p = await pageAt("/users/42", "AUTHENTICATED");
  await p.getByRole("link", { name: "Assign role", exact: true }).click();
  await p.waitForFunction(() => location.pathname === "/users/42/roles/assign");
  await p.waitForFunction(() => window.calls.includes("/api/users/42/profile"));
  assert.ok(
    (await p.evaluate(() => window.methods)).every(
      (method) => method === "GET",
    ),
  );
  await p.close();
});

test("canonical account navigation reaches activation without dispatching a mutation", async () => {
  const p = await pageAt("/users/42", "AUTHENTICATED");
  await p.getByRole("link", { name: "Activate account", exact: true }).click();
  await p
    .getByRole("heading", { name: "Activate user account 42", exact: true })
    .waitFor();
  await p.waitForFunction(() => window.calls.includes("/api/users/42/profile"));
  assert.ok(
    (await p.evaluate(() => window.methods)).every(
      (method) => method === "GET",
    ),
  );
  await p.close();
});

for (const status of [
  "INITIALIZING",
  "ERROR_UNAVAILABLE",
  "ANONYMOUS",
  "RESTRICTED",
]) {
  test(`canonical activation does not load management data for ${status}`, async () => {
    const p = await pageAt("/users/42/activate", status);
    if (status === "ANONYMOUS") await p.waitForURL("**/login");
    else if (status === "RESTRICTED") await p.waitForURL("**/change-password");
    else
      await p
        .getByRole("main", { name: "Activation session recovery" })
        .waitFor();
    assert.equal(
      (await p.evaluate(() => window.calls)).some((url) =>
        url.startsWith("/api/users/"),
      ),
      false,
    );
    await p.close();
  });
}

test("canonical user read renders permission denial without granting access", async () => {
  const p = await pageAt("/users");
  await p.evaluate(() => {
    window.businessReply = {
      status: 403,
      body: { status: 403, code: "PERMISSION_DENIED" },
    };
    window.fixture.set("AUTHENTICATED");
  });
  await p.getByRole("alert").waitFor();
  assert.equal(await p.getByRole("table").count(), 0);
  assert.ok(
    (await p.evaluate(() => window.methods)).every(
      (method) => method === "GET",
    ),
  );
  await p.close();
});

test("canonical user directory discards data after same-account authority replacement", async () => {
  const p = await pageAt("/users");
  await p.evaluate(() => {
    window.businessReply = {
      status: 200,
      body: {
        items: [
          {
            id: 42,
            employeeCode: "SYN42",
            username: "synthetic42",
            fullName: "Synthetic Directory Target",
            email: "synthetic42@example.test",
            accountStatus: "ACTIVE",
            primaryBranch: null,
            department: null,
            roles: [],
          },
        ],
        page: 1,
        pageSize: 20,
        totalCount: 1,
      },
    };
    window.fixture.set("AUTHENTICATED");
  });
  await p.getByRole("link", { name: "Synthetic Directory Target" }).waitFor();
  await p.evaluate(() => {
    window.businessReply = {
      status: 200,
      body: { items: [], page: 1, pageSize: 20, totalCount: 0 },
    };
    window.fixture.set("AUTHENTICATED");
  });
  await p
    .getByRole("link", { name: "Synthetic Directory Target" })
    .waitFor({ state: "detached" });
  await p.waitForFunction(
    () =>
      window.calls.filter((url) => url.startsWith("/api/users")).length >= 2,
  );
  assert.ok(
    (await p.evaluate(() => window.methods)).every(
      (method) => method === "GET",
    ),
  );
  await p.close();
});
test("bootstrap fail closed and explicit recovery", async () => {
  const p = await pageAt("/rooms/status");
  await p.getByText("Checking your session...").waitFor();
  assert.equal(await p.getByRole("navigation").count(), 0);
  await p.evaluate(() => window.fixture.set("ERROR_UNAVAILABLE"));
  await p.getByRole("button", { name: "Check session" }).click();
  await p.getByRole("navigation").waitFor();
  await p.close();
});
test("anonymous deep link then login restores default only", async () => {
  const p = await pageAt("/rooms/status?branchId=7", "ANONYMOUS");
  await p.getByRole("heading", { name: "Sign In", exact: true }).waitFor();
  await p.getByLabel("Username or email").fill("staff");
  await p.getByLabel("Password", { exact: true }).fill("synthetic");
  await p.getByRole("button", { name: "Sign in", exact: true }).click();
  await p.getByRole("navigation").waitFor();
  assert.equal(new URL(p.url()).pathname, "/rooms/status");
  await p.close();
});
test("restricted cannot render operations; profile logout removes protected navigation", async () => {
  const p = await pageAt("/rooms/status", "RESTRICTED");
  await p
    .getByRole("heading", { name: "Change Password", exact: true })
    .waitFor();
  assert.equal(await p.getByRole("navigation").count(), 0);
  await p.evaluate(() => {
    window.fixture.set("AUTHENTICATED");
    window.fixture.navigate("/profile");
  });
  await p.getByRole("button", { name: "Logout", exact: true }).click();
  await p.getByRole("heading", { name: "Sign In", exact: true }).waitFor();
  await p.close();
});
for (const path of [
  "/rooms/status",
  "/rooms/turnover",
  "/cleaning/tasks",
  "/cleaning/tasks/new",
  "/cleaning/tasks/12",
  "/cleaning/assignment",
  "/rework",
  "/rework/12",
])
  test("existing operations route preserved " + path, async () => {
    const p = await pageAt(path, "AUTHENTICATED");
    await p.getByRole("navigation", { name: "Hotel operations" }).waitFor();
    assert.equal(
      await p.getByText("Page not found", { exact: true }).count(),
      0,
    );
    await p.close();
  });
for (const path of [
  "/inspections/assignment?branchId=7&roomId=11",
  "/incidents/new?branchId=7&roomId=11",
])
  test("disabled destination no API " + path, async () => {
    const p = await pageAt(path, "AUTHENTICATED");
    await p.getByText(/not activated/).waitFor();
    assert.deepEqual(await p.evaluate(() => window.calls), []);
    await p.close();
  });

for (const destination of [
  "/rooms/status?branchId=7&roomId=11",
  "/cleaning/tasks/12?branchId=7",
  "/rework?branchId=7",
])
  test("login restores local path and query " + destination, async () => {
    const p = await pageAt(
      "/login?" + new URLSearchParams({ returnTo: destination }),
      "ANONYMOUS",
    );
    await p.getByLabel("Username or email").fill("staff");
    await p.getByLabel("Password", { exact: true }).fill("synthetic");
    await p.getByRole("button", { name: "Sign in", exact: true }).click();
    await p.waitForURL(origin + destination);
    assert.equal(await p.evaluate(() => window.revalidationCalls), 1);
    await p.close();
  });
for (const destination of [
  "https://evil.test",
  "//evil.test",
  "/login",
  "/forgot-password",
  "/incidents/new?branchId=7",
  "/inspections/assignment?branchId=7",
  "/rooms/status?token=private",
  "/rooms/status?branchId=0",
])
  test("invalid or inactive return falls back " + destination, async () => {
    const p = await pageAt(
      "/login?" + new URLSearchParams({ returnTo: destination }),
      "AUTHENTICATED",
    );
    await p.waitForURL(origin + "/dashboard");
    await p.close();
  });
test("failed revalidation does not restore destination; explicit recovery required", async () => {
  const p = await pageAt(
    "/login?" + new URLSearchParams({ returnTo: "/rework?branchId=7" }),
    "ANONYMOUS",
  );
  await p.evaluate(() => (window.revalidationMode = "fail"));
  await p.getByLabel("Username or email").fill("staff");
  await p.getByLabel("Password", { exact: true }).fill("synthetic");
  await p.getByRole("button", { name: "Sign in", exact: true }).click();
  await p.getByRole("button", { name: "Check session" }).waitFor();
  assert.equal(await p.getByRole("navigation").count(), 0);
  assert.equal(new URL(p.url()).pathname, "/login");
  await p.evaluate(() => (window.revalidationMode = "success"));
  await p.getByRole("button", { name: "Check session" }).click();
  await p.waitForURL(origin + "/rework?branchId=7");
  await p.close();
});
test("post-login revalidation blocks destination until completion", async () => {
  const p = await pageAt(
    "/login?" + new URLSearchParams({ returnTo: "/rooms/status?branchId=7" }),
    "ANONYMOUS",
  );
  await p.evaluate(() => (window.revalidationMode = "defer"));
  await p.getByLabel("Username or email").fill("staff");
  await p.getByLabel("Password", { exact: true }).fill("synthetic");
  await p.getByRole("button", { name: "Sign in", exact: true }).click();
  await p.waitForFunction(() => !!window.finishRevalidation);
  assert.equal(await p.getByRole("navigation").count(), 0);
  await p.evaluate(() => window.finishRevalidation());
  await p.waitForURL(origin + "/rooms/status?branchId=7");
  await p.close();
});

const realSession = {
  user: { id: "7", username: "staff", name: "Staff" },
  expiresAt: "2030-01-01T00:00:00Z",
  absoluteExpiresAt: "2030-01-02T00:00:00Z",
  mustChangePassword: false,
  csrfToken: "SYNTHETIC-ONLY-CSRF",
};
async function realLoginPage(replies) {
  const p = await browser.newPage();
  await p.addInitScript((replies) => {
    window.realAuth = true;
    window.realReplies = replies;
  }, replies);
  await p.goto(
    origin + "/login?" + new URLSearchParams({ returnTo: "/profile" }),
  );
  await p.getByRole("heading", { name: "Sign In", exact: true }).waitFor();
  await p.getByLabel("Username or email").fill("staff");
  await p.getByLabel("Password", { exact: true }).fill("synthetic-only");
  await p.getByRole("button", { name: "Sign in", exact: true }).click();
  return p;
}
function repliesAfterLogin(last) {
  return {
    "/api/auth/session": [
      { status: 401, body: { status: 401, code: "AUTHENTICATION_REQUIRED" } },
      last,
    ],
    "/api/auth/csrf": [
      {
        status: 200,
        body: { csrfToken: "SYNTHETIC-ONLY-CSRF", headerName: "X-CSRF-TOKEN" },
      },
    ],
    "/api/auth/login": [{ status: 200, body: realSession }],
  };
}
test("real AuthApi/store StrictMode login revalidates before return URL and strips token", async () => {
  const p = await realLoginPage(
    repliesAfterLogin({ status: 200, body: realSession }),
  );
  await p.waitForURL(origin + "/profile");
  assert.deepEqual(await p.evaluate(() => window.calls), [
    "/api/auth/session",
    "/api/auth/csrf",
    "/api/auth/login",
    "/api/auth/session",
  ]);
  assert.equal(
    (await p.locator("body").innerText()).includes("SYNTHETIC-ONLY-CSRF"),
    false,
  );
  await p.close();
});
test("real AuthApi/store revalidation 401 denies restoration and explicit GET recovers", async () => {
  const r = repliesAfterLogin({
    status: 401,
    body: { status: 401, code: "SESSION_INVALID" },
  });
  r["/api/auth/session"].push({ status: 200, body: realSession });
  const p = await realLoginPage(r);
  await p.getByRole("button", { name: "Check session" }).waitFor();
  assert.equal(new URL(p.url()).pathname, "/login");
  assert.equal(await p.getByRole("navigation").count(), 0);
  await p.getByRole("button", { name: "Check session" }).click();
  await p.waitForURL(origin + "/profile");
  assert.equal(
    (await p.evaluate(() => window.calls)).filter(
      (x) => x === "/api/auth/login",
    ).length,
    1,
  );
  await p.close();
});
test("real restricted session cannot restore business destination", async () => {
  const restricted = { ...realSession, mustChangePassword: true };
  const r = repliesAfterLogin({ status: 200, body: restricted });
  r["/api/auth/login"] = [{ status: 200, body: restricted }];
  const p = await realLoginPage(r);
  await p.waitForURL(origin + "/change-password");
  assert.equal(await p.getByRole("navigation").count(), 0);
  await p.close();
});

function loginUrl(destination) {
  return "/login?" + new URLSearchParams({ returnTo: destination });
}
async function signIn(p) {
  await p.getByLabel("Username or email").fill("staff");
  await p.getByLabel("Password", { exact: true }).fill("synthetic");
  await p.getByRole("button", { name: "Sign in", exact: true }).click();
}
test("031 query A to B before login uses B", async () => {
  const p = await pageAt(loginUrl("/profile"), "ANONYMOUS");
  await p.evaluate(
    (path) => window.fixture.navigate(path),
    loginUrl("/rework?branchId=7"),
  );
  await signIn(p);
  await p.waitForURL(origin + "/rework?branchId=7");
  assert.equal(await p.evaluate(() => window.revalidationCalls), 1);
  await p.close();
});
for (const next of [
  "/rework?branchId=7",
  "https://evil.test",
  "/incidents/new?branchId=7",
  "/inspections/assignment?branchId=7",
])
  test("031 pending old GET cannot restore A after query " + next, async () => {
    const p = await pageAt(loginUrl("/profile"), "ANONYMOUS");
    await p.evaluate(() => (window.revalidationMode = "defer"));
    await signIn(p);
    await p.waitForFunction(() => !!window.finishRevalidation);
    await p.evaluate((path) => {
      window.oldFinish = window.finishRevalidation;
      window.fixture.navigate(path);
    }, loginUrl(next));
    await p.getByText("Checking your session...").waitFor();
    await p.evaluate(() => window.oldFinish());
    await p.waitForFunction(() => window.revalidationCalls === 2);
    assert.equal(new URL(p.url()).pathname, "/login");
    assert.equal(await p.getByRole("navigation").count(), 0);
    await p.evaluate(() => window.finishRevalidation());
    await p.waitForURL(
      origin + (next.startsWith("/rework") ? next : "/dashboard"),
    );
    await p.close();
  });
test("031 real same-account session replacement invalidates old GET without replaying login", async () => {
  const r = repliesAfterLogin({
    status: 200,
    body: realSession,
    deferred: true,
  });
  r["/api/auth/change-password"] = [
    {
      status: 200,
      body: { ...realSession, csrfToken: "REPLACEMENT-SYNTHETIC" },
    },
  ];
  const p = await realLoginPage(r);
  await p.waitForFunction(() => !!window.finishRealRevalidation);
  await p.evaluate(async () => {
    await window.realStore.changePassword({
      currentPassword: "synthetic-old",
      newPassword: "synthetic-new",
      confirmNewPassword: "synthetic-new",
    });
    window.finishRealRevalidation();
  });
  await p.getByRole("button", { name: "Check session" }).waitFor();
  assert.equal(new URL(p.url()).pathname, "/login");
  assert.equal(
    (await p.evaluate(() => window.calls)).filter(
      (x) => x === "/api/auth/login",
    ).length,
    1,
  );
  assert.equal(await p.getByRole("navigation").count(), 0);
  await p.close();
});
test("031 real logout during pending revalidation cannot restore protected destination", async () => {
  const r = repliesAfterLogin({
    status: 200,
    body: realSession,
    deferred: true,
  });
  r["/api/auth/logout"] = [{ status: 204 }];
  const p = await realLoginPage(r);
  await p.waitForFunction(() => !!window.finishRealRevalidation);
  await p.evaluate(async () => {
    await window.realStore.logout();
    window.finishRealRevalidation();
  });
  await p.getByRole("button", { name: "Check session" }).waitFor();
  assert.equal(new URL(p.url()).pathname, "/login");
  assert.equal(await p.getByRole("navigation").count(), 0);
  assert.equal(
    (await p.evaluate(() => window.calls)).filter(
      (x) => x === "/api/auth/login",
    ).length,
    1,
  );
  await p.close();
});

test("031 real store navigation change requires a fresh GET for current query", async () => {
  const replies = repliesAfterLogin({
    status: 200,
    body: realSession,
    deferred: true,
  });
  replies["/api/auth/session"].push({ status: 200, body: realSession });
  const p = await realLoginPage(replies);
  await p.waitForFunction(() => !!window.finishRealRevalidation);
  await p.evaluate(
    (path) => window.fixture.navigate(path),
    loginUrl("/profile?branchId=7&roomId=11"),
  );
  await p.getByText("Checking your session...").waitFor();
  await p.evaluate(() => window.finishRealRevalidation());
  await p.waitForURL(origin + "/profile?branchId=7&roomId=11");
  const calls = await p.evaluate(() => window.calls);
  assert.equal(calls.filter((x) => x === "/api/auth/session").length, 3);
  assert.equal(calls.filter((x) => x === "/api/auth/login").length, 1);
  await p.close();
});

test("canonical audit reads list and redacted detail then hides data on authority loss", async () => {
  const p = await pageAt("/audit", "INITIALIZING");
  const row = {
    id: 71,
    actor: "Audit fixture actor",
    action: "UPDATE",
    entityType: "rooms",
    entityId: "9",
    occurredAt: "2026-11-15T00:00:00Z",
  };
  await p.evaluate((row) => {
    window.businessReply = {
      status: 200,
      body: { items: [row], totalCount: 1, page: 1, pageSize: 25 },
    };
    window.fixture.set("AUTHENTICATED");
  }, row);
  await p.getByRole("button", { name: "View entry 71" }).waitFor();
  await p.evaluate((row) => {
    window.businessReply = {
      status: 200,
      body: {
        entry: row,
        before: { status: "DIRTY" },
        after: { status: "READY" },
        redacted: true,
      },
    };
  }, row);
  await p.getByRole("button", { name: "View entry 71" }).click();
  await p.getByRole("region", { name: "Audit entry" }).waitFor();
  assert.match(await p.locator("main").innerText(), /READY/);
  assert.ok(
    (await p.evaluate(() => window.calls)).includes("/api/audit-logs/71"),
  );
  await p.evaluate(() => window.fixture.set("ERROR_UNAVAILABLE"));
  await p
    .getByRole("region", { name: "Audit entry" })
    .waitFor({ state: "hidden" });
  assert.doesNotMatch(
    await p.locator("body").innerText(),
    /Audit fixture actor|READY/,
  );
  await p.close();
});

test("canonical audit ignores late data after authority replacement", async () => {
  const p = await pageAt("/audit", "INITIALIZING");
  await p.evaluate(() => {
    window.fetch = () =>
      new Promise((resolve) => (window.completeAudit = resolve));
    window.fixture.set("AUTHENTICATED");
  });
  await p.waitForFunction(() => !!window.completeAudit);
  await p.evaluate(() => {
    window.fixture.set("ANONYMOUS");
    window.completeAudit(
      new Response(
        JSON.stringify({
          items: [
            {
              id: 1,
              actor: "LATE-AUDIT-SECRET",
              action: "UPDATE",
              entityType: "rooms",
              entityId: "1",
              occurredAt: "2026-11-15T00:00:00Z",
            },
          ],
          totalCount: 1,
          page: 1,
          pageSize: 25,
        }),
        { status: 200, headers: { "Content-Type": "application/json" } },
      ),
    );
  });
  await p.waitForURL("**/login**");
  assert.doesNotMatch(await p.locator("body").innerText(), /LATE-AUDIT-SECRET/);
  await p.close();
});

test("audit 401 invalidates the current authority without automatic business replay", async () => {
  const p = await pageAt("/audit", "INITIALIZING");
  await p.evaluate(() => {
    window.businessReply = { status: 401, body: { status: 401 } };
    window.fixture.set("AUTHENTICATED");
  });
  await p.waitForURL("**/login**");
  const calls = await p.evaluate(() =>
    window.calls.filter((x) => x.startsWith("/api/audit-logs")),
  );
  assert.equal(calls.length, 1);
  assert.equal(
    await p
      .getByRole("heading", { name: "Audit history", exact: true })
      .count(),
    0,
  );
  await p.close();
});

const dashboardFixture = {
  snapshot: "2026-11-15T01:00:00Z",
  filters: {
    branchIds: [7],
    departmentId: null,
    from: "2026-11-01",
    to: "2026-11-15",
    calendar: "UTC",
  },
  metrics: [
    { key: "activeRooms", label: "Active rooms", value: 2, unit: "count" },
    {
      key: "firstInspectionPassRate",
      label: "First-time inspection pass rate",
      value: null,
      unit: "unavailable",
    },
  ],
  distributions: [
    {
      source: "rooms.operational_status",
      status: "READY",
      count: 1,
      severity: null,
    },
  ],
  quantities: [
    {
      kind: "netStockMovement",
      itemId: 1,
      uomId: 2,
      warehouseId: 3,
      quantity: "123456789012345.1234",
    },
  ],
  missingSources: ["INTERNAL-CONTRACT-DETAIL"],
};

test("dashboard renders unknown metrics and precise quantities with explicit filter refresh", async () => {
  const p = await pageAt("/dashboard", "INITIALIZING");
  await p.evaluate((body) => {
    window.businessReply = { status: 200, body };
    window.fixture.set("AUTHENTICATED");
  }, dashboardFixture);
  await p.getByText("123456789012345.1234", { exact: true }).waitFor();
  const content = await p.locator("main").innerText();
  assert.match(content, /N\/A/);
  assert.match(content, /Incomplete dashboard/);
  assert.doesNotMatch(content, /INTERNAL-CONTRACT-DETAIL/);
  await p.getByLabel("Branch", { exact: true }).selectOption("7");
  await p.getByRole("button", { name: "Apply filters", exact: true }).click();
  await p.waitForFunction(() =>
    window.calls.includes("/api/dashboards/operations?branchId=7"),
  );
  await p.evaluate(() => window.fixture.set("ERROR_UNAVAILABLE"));
  await p
    .getByText("123456789012345.1234", { exact: true })
    .waitFor({ state: "hidden" });
  await p.close();
});
for (const status of [403, 503]) {
  test(`dashboard ${status} hides raw errors without automatic retry`, async () => {
    const p = await pageAt("/dashboard", "INITIALIZING");
    await p.evaluate((status) => {
      window.businessReply = {
        status,
        body: { message: "SQL-PRIVATE-DETAIL" },
      };
      window.fixture.set("AUTHENTICATED");
    }, status);
    await p.getByRole("alert").waitFor();
    assert.doesNotMatch(
      await p.locator("main").innerText(),
      /SQL-PRIVATE-DETAIL/,
    );
    assert.equal(
      (
        await p.evaluate(() =>
          window.calls.filter((x) =>
            x.startsWith("/api/dashboards/operations"),
          ),
        )
      ).length,
      1,
    );
    await p.close();
  });
}
test("dashboard rejects numeric quantities instead of rounding", async () => {
  const p = await pageAt("/dashboard", "INITIALIZING");
  await p.evaluate((body) => {
    body.quantities[0].quantity = 1.25;
    window.businessReply = { status: 200, body };
    window.fixture.set("AUTHENTICATED");
  }, dashboardFixture);
  await p.getByRole("alert").waitFor();
  assert.equal(
    await p.getByRole("region", { name: "Dashboard indicators" }).count(),
    0,
  );
  await p.close();
});
test("dashboard current 401 invalidates authority without business replay", async () => {
  const p = await pageAt("/dashboard", "INITIALIZING");
  await p.evaluate(() => {
    window.businessReply = { status: 401, body: {} };
    window.fixture.set("AUTHENTICATED");
  });
  await p.waitForURL("**/login**");
  assert.equal(
    (
      await p.evaluate(() =>
        window.calls.filter((x) => x.startsWith("/api/dashboards/operations")),
      )
    ).length,
    1,
  );
  await p.close();
});
test("dashboard late 401 cannot invalidate replacement authority", async () => {
  const p = await pageAt("/dashboard", "INITIALIZING");
  await p.evaluate(() => {
    window.fetch = (url) =>
      String(url).endsWith("/options")
        ? Promise.resolve(new Response("{}", { status: 503 }))
        : new Promise((resolve) => (window.finishDashboard = resolve));
    window.fixture.set("AUTHENTICATED");
  });
  await p.waitForFunction(() => !!window.finishDashboard);
  await p.evaluate((body) => {
    const stale = window.finishDashboard;
    window.fetch = async () =>
      new Response(JSON.stringify(body), { status: 200 });
    window.fixture.set("AUTHENTICATED");
    stale(new Response("{}", { status: 401 }));
  }, dashboardFixture);
  await p.getByText("123456789012345.1234", { exact: true }).waitFor();
  assert.equal(new URL(p.url()).pathname, "/dashboard");
  await p.close();
});

test("dashboard dropdowns restrict departments and clear selection when branch changes", async () => {
  const p = await pageAt("/dashboard", "AUTHENTICATED");
  await p.getByLabel("Branch", { exact: true }).selectOption("7");
  const department = p.getByLabel("Department", { exact: true });
  assert.deepEqual(
    await department
      .locator("option")
      .evaluateAll((nodes) => nodes.map((n) => n.value)),
    ["", "11"],
  );
  await department.selectOption("11");
  await p.getByRole("button", { name: "Apply filters" }).click();
  await p.waitForFunction(() =>
    window.calls.includes(
      "/api/dashboards/operations?branchId=7&departmentId=11",
    ),
  );
  await p.getByLabel("Branch", { exact: true }).selectOption("8");
  assert.equal(await department.inputValue(), "");
  assert.deepEqual(
    await department
      .locator("option")
      .evaluateAll((nodes) => nodes.map((n) => n.value)),
    ["", "12"],
  );
  await p.getByRole("button", { name: "Apply filters" }).click();
  await p.waitForFunction(() =>
    window.calls.includes("/api/dashboards/operations?branchId=8"),
  );
  await p.close();
});

test("dashboard rejects foreign department options, supports explicit retry and clears old names on authority change", async () => {
  const p = await pageAt("/dashboard", "INITIALIZING");
  await p.evaluate(() => {
    window.dashboardOptions = {
      branches: [{ id: 7, code: "B7", name: "OLD-SCOPE" }],
      departments: [{ id: 11, branchId: 99, name: "FOREIGN-DEPARTMENT" }],
    };
    window.fixture.set("AUTHENTICATED");
  });
  await p.getByRole("button", { name: "Retry filters" }).waitFor();
  assert.equal(
    await p.getByLabel("Branch", { exact: true }).isDisabled(),
    true,
  );
  assert.doesNotMatch(
    await p.locator("body").innerText(),
    /OLD-SCOPE|FOREIGN-DEPARTMENT/,
  );
  assert.equal(
    await p.evaluate(
      () => window.calls.filter((x) => x.endsWith("/options")).length,
    ),
    1,
  );
  await p.evaluate(() => (window.dashboardOptions.departments = []));
  await p.getByRole("button", { name: "Retry filters" }).click();
  await p
    .getByRole("option", { name: "OLD-SCOPE (B7)", exact: true })
    .waitFor({ state: "attached" });
  await p.evaluate(() => {
    window.fetch = () =>
      new Promise((resolve) => {
        window.finishOldOptions = resolve;
      });
    window.fixture.set("AUTHENTICATED");
  });
  await p.waitForFunction(() => !!window.finishOldOptions);
  assert.doesNotMatch(await p.locator("body").innerText(), /OLD-SCOPE/);
  await p.close();
});

test("dashboard options 401 expires current session", async () => {
  const p = await pageAt("/dashboard", "INITIALIZING");
  await p.evaluate(() => {
    window.optionsStatus = 401;
    window.fixture.set("AUTHENTICATED");
  });
  await p.waitForURL("**/login**");
  await p.close();
});

const notificationFixture = {
  items: [
    {
      id: 71,
      type: "CLEANING_TASK_ASSIGNED",
      title: "Assigned room",
      message: "Private recipient text",
      priority: "NORMAL",
      createdAt: "2026-11-15T00:00:00Z",
      readAt: null,
    },
  ],
  totalCount: 1,
  unreadCount: 1,
  page: 1,
  pageSize: 25,
  snapshot: "2026-11-15T00:01:00Z",
};
test("notification center marks read through CSRF and refreshes only after success", async () => {
  const p = await pageAt("/notifications", "INITIALIZING");
  await p.evaluate((body) => {
    window.inbox = body;
    window.fetch = async (url, init) => {
      window.calls.push(String(url));
      if (String(url) === "/api/auth/csrf")
        return new Response(
          JSON.stringify({
            csrfToken: "synthetic-token",
            headerName: "X-CSRF-TOKEN",
          }),
        );
      if (init?.method === "POST") {
        if (init.headers["X-CSRF-TOKEN"] !== "synthetic-token")
          throw Error("CSRF missing");
        window.inbox.items[0].readAt = "2026-11-15T00:02:00Z";
        window.inbox.unreadCount = 0;
        return new Response(null, { status: 204 });
      }
      return new Response(JSON.stringify(window.inbox));
    };
    window.fixture.set("AUTHENTICATED");
  }, notificationFixture);
  await p
    .getByRole("button", { name: "Mark notification 71 as read", exact: true })
    .click();
  await p.getByText("Marked as read. Source records are unchanged.").waitFor();
  await p.waitForFunction(() => document.body.innerText.includes("Unread: 0"));
  assert.equal(
    await p
      .getByRole("button", {
        name: "Mark notification 71 as read",
        exact: true,
      })
      .isDisabled(),
    true,
  );
  assert.equal(
    await p.evaluate(
      () =>
        window.calls.filter((x) => x === "/api/notifications/71/read").length,
    ),
    1,
  );
  await p.evaluate(() => window.fixture.set("ERROR_UNAVAILABLE"));
  await p
    .getByText("Private recipient text", { exact: true })
    .waitFor({ state: "hidden" });
  await p.close();
});
test("notification authority replacement during CSRF bootstrap cannot send old write", async () => {
  const p = await pageAt("/notifications", "INITIALIZING");
  await p.evaluate((body) => {
    window.fetch = (url, init) => {
      window.calls.push(String(url));
      if (String(url) === "/api/auth/csrf")
        return new Promise((resolve) => (window.finishInboxCsrf = resolve));
      return Promise.resolve(new Response(JSON.stringify(body)));
    };
    window.fixture.set("AUTHENTICATED");
  }, notificationFixture);
  await p
    .getByRole("button", { name: "Mark notification 71 as read", exact: true })
    .click();
  await p.waitForFunction(() => !!window.finishInboxCsrf);
  await p.evaluate(() => {
    window.fixture.set("ANONYMOUS");
    window.finishInboxCsrf(
      new Response(
        JSON.stringify({ csrfToken: "stale", headerName: "X-CSRF-TOKEN" }),
      ),
    );
  });
  await p.waitForURL("**/login**");
  assert.equal(
    await p.evaluate(() => window.calls.some((x) => x.endsWith("/read"))),
    false,
  );
  await p.close();
});
test("notification uncertain write is not replayed and current 401 expires authority", async () => {
  const p = await pageAt("/notifications", "INITIALIZING");
  await p.evaluate((body) => {
    window.fetch = async (url, init) => {
      window.calls.push(String(url));
      if (String(url) === "/api/auth/csrf")
        return new Response(
          JSON.stringify({
            csrfToken: "synthetic",
            headerName: "X-CSRF-TOKEN",
          }),
        );
      if (init?.method === "POST") throw Error("SQL-PRIVATE-ERROR");
      return new Response(JSON.stringify(body));
    };
    window.fixture.set("AUTHENTICATED");
  }, notificationFixture);
  await p
    .getByRole("button", { name: "Mark all as read", exact: true })
    .click();
  await p
    .getByText(
      "Read status could not be confirmed. Refresh before trying again.",
    )
    .waitFor();
  assert.equal(
    await p.evaluate(
      () =>
        window.calls.filter((x) => x === "/api/notifications/read-all").length,
    ),
    1,
  );
  assert.doesNotMatch(await p.locator("body").innerText(), /SQL-PRIVATE-ERROR/);
  await p.evaluate(() => {
    window.fetch = async () => new Response("{}", { status: 401 });
  });
  await p.getByRole("button", { name: "Refresh", exact: true }).click();
  await p.waitForURL("**/login**");
  await p.close();
});
