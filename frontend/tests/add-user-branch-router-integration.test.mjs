import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { existsSync } from "node:fs";
import { build } from "esbuild";
import { chromium } from "@playwright/test";

const secret = "PRIVATE-MEMBERSHIP-ERROR";
const token = "PRIVATE-CSRF-TOKEN";
const tag = '"ABCDEF0123456789"',
  nextTag = '"ABCDEF0123456790"';
const profile = (id = 42, fullName = "Server Employee") => ({
  id,
  fullName,
  email: "server@example.test",
  phone: "123",
});
const ok = (id = 42, etag = tag, name = "Server Employee") => ({
  body: profile(id, name),
  headers: { ETag: etag },
});
const problem = (status, fields = {}) => ({
  status,
  body: { status, title: secret, detail: secret, traceId: token, ...fields },
});
const session = (restricted = false, id = "7") => ({
  user: { id, username: "manager", name: "Confirmed Manager" },
  expiresAt: "2026-11-07T12:00:00Z",
  absoluteExpiresAt: "2026-11-07T20:00:00Z",
  mustChangePassword: restricted,
  csrfToken: token,
});
const unknown =
  "Branch membership addition outcome could not be confirmed. The membership may already have been created. Do not retry automatically. Ask an authorized administrator to verify the current membership before making another change.";

const bundle = await build({
  stdin: {
    loader: "tsx",
    resolveDir: process.cwd(),
    contents: `
  import React, { StrictMode } from "react";
  import { createRoot } from "react-dom/client";
  import { BrowserRouter } from "react-router-dom";
  import { AuthProvider } from "./src/auth/AuthProvider";
  import { AuthRouteHost } from "./src/auth/AuthRouteHost";
  import { createAuthStore } from "./src/auth/authStore";
  import { createAuthApi } from "./src/auth/authApi";
  import { destinationBranchId } from "./src/users/useAddUserBranch";
  const s=window.scenario, calls=[], unexpected=[], waiting=new Map();
  const queues={
    "/api/auth/session":[...(s.sessions??[{body:${JSON.stringify(session())}}])],
    "/api/auth/csrf":[...(s.csrf??[{body:{csrfToken:${JSON.stringify(token)},headerName:"X-CSRF-TOKEN"}}])],
    "/api/users/42/profile":[...(s.reads??[${JSON.stringify(ok())}])],
    "/api/users/42/branches":[...(s.posts??[{status:201,body:{accessId:91,userId:42,branchId:123,accessLevel:"STANDARD",isPrimary:false},headers:{ETag:${JSON.stringify(nextTag)}}}])],
    "/api/auth/login":[{body:${JSON.stringify(session(false, "8"))}}],
    ...s.responses
  };
  function respond(r){if(r.network)throw new Error(${JSON.stringify(secret)});if(r.timeout)throw new DOMException(${JSON.stringify(secret)},"TimeoutError");if(r.cancelled)throw new DOMException(${JSON.stringify(secret)},"AbortError");return new Response(r.raw??JSON.stringify(r.body),{status:r.status??200,headers:{"Content-Type":r.type??"application/json",...r.headers}});}
  const fetcher=async(url,init)=>{const c={url,method:init.method,body:init.body,headers:Object.fromEntries(new Headers(init.headers)),credentials:init.credentials,cache:init.cache,redirect:init.redirect,aborted:init.signal?.aborted??false};calls.push(c);init.signal?.addEventListener("abort",()=>{c.aborted=true;},{once:true});const r=queues[url]?.shift();if(!r){unexpected.push(url);throw new Error("Unexpected request");}if(r.deferred)return new Promise((resolve,reject)=>waiting.set(r.deferred,{resolve,reject}));return respond(r);};
  window.fetch=fetcher;const store=createAuthStore(createAuthApi(fetcher));const host=<AuthRouteHost/>;const root=createRoot(document.getElementById("root"));
  root.render(<StrictMode><BrowserRouter><AuthProvider store={store} fallback={host}>{host}</AuthProvider></BrowserRouter></StrictMode>);
  window.fixture={calls,unexpected,parseBranch:destinationBranchId,snapshot:()=>store.getSnapshot(),revalidate:()=>store.revalidate(),login:()=>store.login({username:"manager",password:"test-only"}),
    navigate(path){history.pushState(null,"",path);dispatchEvent(new PopStateEvent("popstate"));},
    finish(tag,r){const p=waiting.get(tag);if(!p)throw new Error("Missing deferred response");waiting.delete(tag);try{p.resolve(respond(r));}catch(e){p.reject(e);}},unmount(){root.unmount();}
  };
`,
  },
  bundle: true,
  write: false,
  metafile: true,
  format: "iife",
  platform: "browser",
  jsx: "automatic",
  define: { "process.env.NODE_ENV": '"development"' },
});
let browser;
before(async () => {
  const executablePath =
    process.env.HOTELOPS_TEST_BROWSER ||
    [
      "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
      "C:/Program Files/Google/Chrome/Application/chrome.exe",
    ].find(existsSync);
  browser = await chromium.launch({
    headless: true,
    ...(executablePath ? { executablePath } : {}),
  });
});
after(async () => browser?.close());
async function settle(page) {
  await page.evaluate(
    () =>
      new Promise((resolve) =>
        requestAnimationFrame(() => requestAnimationFrame(resolve)),
      ),
  );
}

async function calls(page, url) {
  return page.evaluate(
    (url) => window.fixture.calls.filter((c) => !url || c.url === url),
    url,
  );
}
async function finish(page, label, reply) {
  await page.evaluate(
    ({ label, reply }) => window.fixture.finish(label, reply),
    { label, reply },
  );
  await settle(page);
}
async function navigate(page, path) {
  await page.evaluate((path) => window.fixture.navigate(path), path);
  await settle(page);
}
const body = (page) => page.locator("body").innerText();
const review = async (page, branchId = "123") => {
  await page
    .getByRole("textbox", { name: "Destination Branch ID", exact: true })
    .fill(branchId);
  await page
    .getByRole("button", { name: "Review Add Branch", exact: true })
    .click();
};
const confirm = async (page) => {
  await page
    .getByRole("button", { name: "Confirm Add Branch", exact: true })
    .click();
  await settle(page);
};
const ready = (page) =>
  page
    .getByRole("form", { name: "Add branch membership", exact: true })
    .waitFor();
async function withMembership(scenario, run, path = "/users/42/branches/add") {
  const page = await browser.newPage(),
    errors = [],
    logs = [],
    network = [];
  page.on("pageerror", (e) => errors.push(e.message));
  page.on("console", (m) => logs.push(m.text()));
  await page.route("**/*", (route) => {
    if (route.request().resourceType() === "document")
      return route.fulfill({
        contentType: "text/html",
        body: '<div id="root"></div>',
      });
    network.push(route.request().url());
    return route.abort();
  });
  try {
    await page.goto(`http://hotelops.test${path}`);
    await page.evaluate((scenario) => {
      window.scenario = scenario;
      window.storageTouches = 0;
      for (const key of ["localStorage", "sessionStorage", "indexedDB"])
        Object.defineProperty(window, key, {
          configurable: true,
          get() {
            window.storageTouches++;
            throw new Error("Forbidden storage");
          },
        });
    }, scenario);
    await page.addScriptTag({ content: bundle.outputFiles[0].text });
    await settle(page);
    await run(page);
    await settle(page);
    assert.deepEqual(errors, []);
    assert.deepEqual(network, []);
    assert.deepEqual(await page.evaluate(() => window.fixture.unexpected), []);
    assert.equal(await page.evaluate(() => window.storageTouches), 0);
    assert.ok(
      !logs.some((line) => line.includes(secret) || line.includes(token)),
    );
    assert.ok(!(await body(page)).includes(secret));
    assert.ok(!(await body(page)).includes(token));
    for (const c of await calls(page)) {
      assert.equal(c.credentials, "include");
      assert.equal(c.cache, "no-store");
      assert.equal(c.redirect, "error");
      assert.equal(c.headers.authorization, undefined);
      if (c.method === "POST" && c.url.startsWith("/api/users/")) {
        assert.equal(c.headers["x-csrf-token"], token);
        assert.equal(c.headers["content-type"], "application/json");
        assert.ok(/^"[0-9A-F]{16}"$/.test(c.headers["if-match"]));
      }
    }
  } finally {
    await page.close();
  }
}

const membership = (userId = 42, branchId = 123, etag = nextTag) => ({
  status: 201,
  body: {
    accessId: 91,
    userId,
    branchId,
    accessLevel: "STANDARD",
    isPrimary: false,
  },
  headers: { ETag: etag },
});
const posts = async (p) =>
  (await calls(p)).filter(
    (c) => c.method === "POST" && c.url.startsWith("/api/users/"),
  );
test("real graph includes accepted clients without mock Shell or assignment prototypes", () => {
  const paths = Object.keys(bundle.metafile.inputs).map((p) =>
    p.replaceAll("\\", "/"),
  );
  for (const file of [
    "auth/authStore.ts",
    "auth/AuthProvider.tsx",
    "users/userProfileApi.ts",
    "users/userBranchMembershipApi.ts",
    "users/AddUserBranchFlowBoundary.tsx",
  ])
    assert.ok(paths.some((p) => p.endsWith(file)));
  assert.ok(
    !paths.some((p) =>
      /src\/(services(?:\/|\.ts)|ui\.tsx|i18n\.tsx|mocks\/|screens\/users\/)/.test(
        p,
      ),
    ),
  );
});
for (const [restricted, path] of [
  [false, "/login"],
  [true, "/change-password"],
])
  test(`guard ${path} has no management call`, async () => {
    await withMembership(
      { sessions: [restricted ? { body: session(true) } : problem(401)] },
      async (p) => {
        await p.waitForFunction((path) => location.pathname === path, path);
        assert.equal(
          (await calls(p)).filter((c) => c.url.startsWith("/api/users")).length,
          0,
        );
      },
    );
  });
for (const reply of [{ deferred: "startup" }, problem(503)])
  test(`startup ${reply.status ?? "pending"} hides mutation`, async () => {
    await withMembership({ sessions: [reply] }, async (p) => {
      assert.equal(await p.locator("input[name=branchId]").count(), 0);
      assert.equal(
        (await calls(p)).filter((c) => c.url.startsWith("/api/users")).length,
        0,
      );
    });
  });
for (const id of ["0", "-1", "1.5", "bad", "9007199254740992"])
  test(`invalid target ${id} no GET/POST`, async () => {
    await withMembership(
      {},
      async (p) => {
        await p
          .getByRole("heading", { name: "Invalid user ID", exact: true })
          .waitFor();
        assert.equal(
          (await calls(p)).filter((c) => c.url.startsWith("/api/users")).length,
          0,
        );
      },
      `/users/${id}/branches/add`,
    );
  });
for (const etag of [
  undefined,
  "",
  'W/"ABCDEF0123456789"',
  '"abcdef0123456789"',
])
  test(`invalid GET ETag ${String(etag)} cannot Add`, async () => {
    await withMembership(
      {
        reads: [
          {
            body: profile(),
            headers: etag === undefined ? {} : { ETag: etag },
          },
        ],
      },
      async (p) => {
        await p.getByRole("alert").waitFor();
        assert.equal(await p.locator("input[name=branchId]").count(), 0);
        assert.equal((await posts(p)).length, 0);
      },
    );
  });
test("wrong target DTO is rejected before showing identity", async () => {
  await withMembership({ reads: [ok(43, tag, "Wrong Target")] }, async (p) => {
    await p.getByRole("alert").waitFor();
    assert.ok(!(await body(p)).includes("Wrong Target"));
    assert.equal(await p.locator("input[name=branchId]").count(), 0);
  });
});
for (const reply of [problem(403), problem(404), problem(503)])
  test(`GET failure ${reply.status} hides snapshot`, async () => {
    await withMembership({ reads: [reply] }, async (p) => {
      await p.getByRole("alert").waitFor();
      assert.ok(!(await body(p)).includes("server@example.test"));
      assert.equal((await posts(p)).length, 0);
    });
  });
test("profile-only admission, server identity and exact confirmed POST without permission inference", async () => {
  await withMembership({}, async (p) => {
    await ready(p);
    const text = await body(p);
    assert.ok(text.includes("Server Employee"));
    assert.ok(text.includes("server@example.test"));
    assert.ok(text.includes("Loading a profile does not grant permission"));
    assert.ok(
      !/Target is ACTIVE|Branch is active|Membership does not exist/.test(text),
    );
    assert.equal(
      (await calls(p)).filter(
        (c) => c.url === "/api/users" || c.url.includes("effective-access"),
      ).length,
      0,
    );
    await review(p);
    assert.equal((await posts(p)).length, 0);
    const preview = await p
      .getByRole("region", { name: "Review branch membership" })
      .innerText();
    assert.ok(preview.includes("Account ID: 42"));
    assert.ok(preview.includes("Destination Branch ID: 123"));
    await confirm(p);
    await p.getByRole("status", { name: "Confirmed membership" }).waitFor();
    const write = (await posts(p))[0];
    assert.equal((await posts(p)).length, 1);
    assert.equal(write.url, "/api/users/42/branches");
    assert.equal(write.body, '{"branchId":123}');
    assert.equal(write.headers["if-match"], tag);
    assert.equal((await calls(p, "/api/auth/csrf")).length, 1);
    const result = await p
      .getByRole("status", { name: "Confirmed membership" })
      .innerText();
    for (const value of [
      "accessId: 91",
      "userId: 42",
      "branchId: 123",
      "accessLevel: STANDARD",
      "isPrimary: false",
    ])
      assert.ok(result.includes(value));
    assert.equal(new URL(p.url()).pathname, "/users/42/branches/add");
    assert.equal(
      (await p.evaluate(() => window.fixture.snapshot())).session.user.id,
      "7",
    );
  });
});
for (const branch of [
  "",
  "0",
  "-1",
  "1.5",
  "1e3",
  "9007199254740992",
  " 123 ",
  "abc",
  "+123",
])
  test(`invalid decimal destination ${JSON.stringify(branch)} has no confirmation/POST`, async () => {
    await withMembership({}, async (p) => {
      await ready(p);
      await review(p, branch);
      await p.getByRole("alert").waitFor();
      assert.equal(
        await p
          .getByRole("button", { name: "Confirm Add Branch", exact: true })
          .count(),
        0,
      );
      assert.equal((await posts(p)).length, 0);
    });
  });
for (const branch of ["1", "000123", String(Number.MAX_SAFE_INTEGER)])
  test(`valid decimal destination ${branch} snapshotted numerically`, async () => {
    await withMembership(
      { posts: [membership(42, Number(branch))] },
      async (p) => {
        await ready(p);
        await review(p, branch);
        await confirm(p);
        await p.getByRole("status", { name: "Confirmed membership" }).waitFor();
        assert.equal(
          (await posts(p))[0].body,
          JSON.stringify({ branchId: Number(branch) }),
        );
      },
    );
  });
test("editing destination invalidates review; Enter does not POST", async () => {
  await withMembership({}, async (p) => {
    await ready(p);
    await review(p);
    await p
      .getByRole("textbox", { name: "Destination Branch ID", exact: true })
      .fill("456");
    assert.equal(
      await p
        .getByRole("button", { name: "Confirm Add Branch", exact: true })
        .count(),
      0,
    );
    await p
      .getByRole("textbox", { name: "Destination Branch ID", exact: true })
      .press("Enter");
    assert.equal((await posts(p)).length, 0);
  });
});
test("StrictMode, double confirm and repeated Enter never duplicate POST", async () => {
  await withMembership({ posts: [{ deferred: "post" }] }, async (p) => {
    await ready(p);
    assert.equal((await calls(p, "/api/users/42/profile")).length, 1);
    assert.equal((await posts(p)).length, 0);
    await review(p);
    await p
      .getByRole("button", { name: "Confirm Add Branch", exact: true })
      .evaluate((b) => {
        b.click();
        b.click();
        b.dispatchEvent(
          new KeyboardEvent("keydown", { key: "Enter", bubbles: true }),
        );
      });
    await settle(p);
    assert.equal((await posts(p)).length, 1);
    await finish(p, "post", membership());
    await p.getByRole("status", { name: "Confirmed membership" }).waitFor();
    assert.equal((await posts(p)).length, 1);
  });
});
for (const [code, message] of [
  ["TARGET_NOT_ACTIVE", "target account is not active"],
  ["MEMBERSHIP_EXISTS", "previously revoked"],
  ["EXISTING_ROLE_PATH_CONFLICT", "destination role path"],
  ["OPERATIONS_MANAGER_SCOPE_CONFLICT", "Operations Manager scope constraint"],
])
  test(`known conflict ${code} remains rejected without repair`, async () => {
    await withMembership(
      { posts: [problem(409, { code, title: code })] },
      async (p) => {
        await ready(p);
        await review(p);
        await confirm(p);
        await p.getByRole("alert").waitFor();
        assert.ok((await body(p)).includes(message));
        assert.equal((await posts(p)).length, 1);
        assert.equal(
          await p.getByRole("status", { name: "Confirmed membership" }).count(),
          0,
        );
      },
    );
  });
for (const [status, code] of [
  [400, "BRANCH_UNAVAILABLE"],
  [400, "CSRF_VALIDATION_FAILED"],
  [403, "BRANCH_ASSIGNMENT_NOT_ALLOWED"],
  [404, "USER_NOT_FOUND"],
  [428, "IF_MATCH_REQUIRED"],
  [413, "REQUEST_TOO_LARGE"],
  [415, "UNSUPPORTED_MEDIA_TYPE"],
])
  test(`POST rejected ${status}/${code} no replay`, async () => {
    await withMembership({ posts: [problem(status, { code })] }, async (p) => {
      await ready(p);
      await review(p);
      await confirm(p);
      await p.getByRole("alert").waitFor();
      assert.equal((await posts(p)).length, 1);
      if ([403, 404, 428].includes(status)) {
        assert.ok(!(await body(p)).includes("server@example.test"));
        assert.equal(await p.locator("input[name=branchId]").count(), 0);
      }
      if (status === 403)
        assert.ok(
          (await body(p)).includes(
            "does not confirm whether the account exists",
          ),
        );
    });
  });
test("412 removes old confirmation; explicit Reload and review use fresh ETag", async () => {
  await withMembership(
    {
      reads: [ok(), ok(42, nextTag, "Changed Target")],
      posts: [problem(412), membership(42, 456, '"ABCDEF0123456791"')],
      csrf: [
        { body: { csrfToken: token, headerName: "X-CSRF-TOKEN" } },
        { body: { csrfToken: token, headerName: "X-CSRF-TOKEN" } },
      ],
    },
    async (p) => {
      await ready(p);
      await review(p);
      await confirm(p);
      await p.getByRole("alert").waitFor();
      assert.equal((await calls(p, "/api/users/42/profile")).length, 1);
      assert.equal(await p.locator("input[name=branchId]").count(), 0);
      await p
        .getByRole("button", { name: "Reload profile", exact: true })
        .click();
      await ready(p);
      assert.ok((await body(p)).includes("Changed Target"));
      assert.equal(
        await p
          .getByRole("textbox", { name: "Destination Branch ID", exact: true })
          .inputValue(),
        "",
      );
      assert.equal((await posts(p)).length, 1);
      await review(p, "456");
      await confirm(p);
      await p.getByRole("status", { name: "Confirmed membership" }).waitFor();
      assert.equal((await posts(p))[1].headers["if-match"], nextTag);
    },
  );
});
for (const recovery of [{ body: session() }, problem(401), problem(503)])
  test(`401 bounded recovery ${recovery.status ?? 200}, no POST replay`, async () => {
    await withMembership(
      { posts: [problem(401)], sessions: [{ body: session() }, recovery] },
      async (p) => {
        await ready(p);
        await review(p);
        await confirm(p);
        await p.waitForFunction(
          () =>
            window.fixture.calls.filter((c) => c.url === "/api/auth/session")
              .length === 2,
        );
        await settle(p);
        assert.equal((await posts(p)).length, 1);
        assert.equal((await calls(p, "/api/users/42/profile")).length, 1);
        assert.equal(await p.locator("input[name=branchId]").count(), 0);
      },
    );
  });
test("bootstrap failure stays NOT_DISPATCHED without automatic retry", async () => {
  await withMembership({ csrf: [problem(503)] }, async (p) => {
    await ready(p);
    await review(p);
    await confirm(p);
    await p.getByRole("alert").waitFor();
    assert.ok((await body(p)).includes("No membership POST was sent."));
    assert.equal((await posts(p)).length, 0);
    assert.equal((await calls(p, "/api/auth/csrf")).length, 1);
  });
});
for (const reply of [
  problem(409, { code: "UNKNOWN_CONFLICT" }),
  problem(500),
  problem(503),
  { network: true },
  { timeout: true },
  { cancelled: true },
  { status: 201, raw: "{", headers: { ETag: nextTag } },
])
  test(`uncertain ${JSON.stringify(reply)} stays locked after Reload`, async () => {
    await withMembership(
      { posts: [reply], reads: [ok(), ok(42, nextTag)] },
      async (p) => {
        await ready(p);
        await review(p);
        await confirm(p);
        await p.getByText(unknown, { exact: true }).waitFor();
        await p
          .getByRole("button", { name: "Reload profile", exact: true })
          .click();
        await p
          .getByText("Email: server@example.test", { exact: true })
          .waitFor();
        assert.equal(await p.locator("input[name=branchId]").count(), 0);
        assert.equal(
          await p
            .getByRole("button", {
              name: /Confirm Add Branch|Review Add Branch|Retry Add/,
            })
            .count(),
          0,
        );
        assert.equal((await posts(p)).length, 1);
        assert.equal(
          await p.getByRole("status", { name: "Confirmed membership" }).count(),
          0,
        );
      },
    );
  });
for (const id of ["7", "8"])
  test(`owner replacement ${id} aborts POST and ignores late 201`, async () => {
    await withMembership(
      {
        posts: [{ deferred: "post" }],
        sessions: [{ body: session() }, { body: session(false, id) }],
      },
      async (p) => {
        await ready(p);
        await review(p);
        await confirm(p);
        await p.evaluate(() => window.fixture.revalidate());
        await settle(p);
        await finish(p, "post", membership());
        await p.getByText(unknown, { exact: true }).waitFor();
        assert.equal((await posts(p))[0].aborted, true);
        assert.equal(
          await p.getByRole("status", { name: "Confirmed membership" }).count(),
          0,
        );
        assert.equal(await p.locator("input[name=branchId]").count(), 0);
      },
    );
  });
test("session unavailable while POST pending stays uncertain after explicit recovery", async () => {
  await withMembership(
    {
      posts: [{ deferred: "post" }],
      sessions: [{ body: session() }, problem(503), { body: session() }],
    },
    async (p) => {
      await ready(p);
      await review(p);
      await confirm(p);
      await p.evaluate(() => window.fixture.revalidate());
      await settle(p);
      assert.equal(await p.locator("input[name=branchId]").count(), 0);
      assert.ok(!(await body(p)).includes("server@example.test"));
      await finish(p, "post", membership());
      await p
        .getByRole("button", { name: "Check session", exact: true })
        .click();
      await p.getByText(unknown, { exact: true }).waitFor();
      assert.equal((await posts(p))[0].aborted, true);
      assert.equal(
        await p.getByRole("status", { name: "Confirmed membership" }).count(),
        0,
      );
    },
  );
});
test("expired session during POST cannot be restored by late 201", async () => {
  await withMembership(
    {
      posts: [{ deferred: "post" }],
      sessions: [{ body: session() }, problem(401)],
    },
    async (p) => {
      await ready(p);
      await review(p);
      await confirm(p);
      await p.evaluate(() => window.fixture.revalidate());
      await settle(p);
      await finish(p, "post", membership());
      assert.equal(new URL(p.url()).pathname, "/login");
      assert.ok(!(await body(p)).includes("server@example.test"));
      assert.equal((await posts(p))[0].aborted, true);
    },
  );
});
for (const id of ["7", "8"])
  test(`owner replacement ${id} drops late GET`, async () => {
    await withMembership(
      {
        reads: [{ deferred: "read" }, ok(42, nextTag, "Fresh Owner")],
        sessions: [{ body: session() }, { body: session(false, id) }],
      },
      async (p) => {
        await p.waitForFunction(() =>
          window.fixture.calls.some((c) => c.url === "/api/users/42/profile"),
        );
        await p.evaluate(() => window.fixture.revalidate());
        await ready(p);
        await finish(p, "read", ok(42, tag, "Old Owner"));
        assert.ok((await body(p)).includes("Fresh Owner"));
        assert.ok(!(await body(p)).includes("Old Owner"));
      },
    );
  });
test("target navigation aborts old GET and ignores late identity", async () => {
  await withMembership(
    {
      reads: [{ deferred: "read" }],
      responses: {
        "/api/users/43/profile": [ok(43, nextTag, "Second Target")],
      },
    },
    async (p) => {
      await p.waitForFunction(() =>
        window.fixture.calls.some((c) => c.url === "/api/users/42/profile"),
      );
      await navigate(p, "/users/43/branches/add");
      await ready(p);
      await finish(p, "read", ok());
      assert.ok((await body(p)).includes("Second Target"));
      assert.ok(!(await body(p)).includes("Server Employee"));
      assert.equal((await calls(p, "/api/users/42/profile"))[0].aborted, true);
    },
  );
});
for (const destination of ["/profile", "/users/43/branches/add"])
  test(`navigation ${destination} aborts POST; late result cannot publish or redirect`, async () => {
    await withMembership(
      {
        posts: [{ deferred: "post" }],
        responses: {
          "/api/users/43/profile": [ok(43, nextTag, "Second Target")],
        },
      },
      async (p) => {
        await ready(p);
        await review(p);
        await confirm(p);
        await navigate(p, destination);
        await finish(p, "post", membership());
        assert.equal(new URL(p.url()).pathname, destination);
        assert.equal((await posts(p))[0].aborted, true);
        assert.equal(
          await p.getByRole("status", { name: "Confirmed membership" }).count(),
          0,
        );
      },
    );
  });
test("route unmount aborts GET; late profile stays hidden", async () => {
  await withMembership({ reads: [{ deferred: "read" }] }, async (p) => {
    await p.waitForFunction(() =>
      window.fixture.calls.some((c) => c.url === "/api/users/42/profile"),
    );
    await navigate(p, "/profile");
    await finish(p, "read", ok());
    assert.equal((await calls(p, "/api/users/42/profile"))[0].aborted, true);
    assert.ok(!(await body(p)).includes("server@example.test"));
  });
});
test("confirmed membership receipt is removed on ownership replacement", async () => {
  await withMembership(
    { sessions: [{ body: session() }, { body: session(false, "8") }] },
    async (p) => {
      await ready(p);
      await review(p);
      await confirm(p);
      await p.getByRole("status", { name: "Confirmed membership" }).waitFor();
      await p.evaluate(() => window.fixture.revalidate());
      await settle(p);
      assert.equal(
        await p.getByRole("status", { name: "Confirmed membership" }).count(),
        0,
      );
    },
  );
});

test("decimal parser rejects newline and whitespace rather than normalizing selectors", async () => {
  await withMembership({}, async (p) => {
    await ready(p);
    const values = await p.evaluate(() =>
      ["123\n", "123\r", "123\t", " 123", "123 ", "1e3", "-1", "1.5"].map(
        window.fixture.parseBranch,
      ),
    );
    assert.deepEqual(values, Array(8).fill(null));
    assert.equal((await posts(p)).length, 0);
  });
});

test("contradictory 409 identifiers lock replay without exposing private response", async () => {
  await withMembership(
    {
      posts: [
        problem(409, { code: "MEMBERSHIP_EXISTS", title: "TARGET_NOT_ACTIVE" }),
      ],
    },
    async (p) => {
      await ready(p);
      await review(p);
      await confirm(p);
      await p.getByRole("alert").waitFor();
      assert.ok((await body(p)).includes(unknown));
      assert.equal((await posts(p)).length, 1);
      assert.equal(
        await p.getByRole("status", { name: "Confirmed membership" }).count(),
        0,
      );
      assert.equal((await body(p)).includes(secret), false);
      assert.equal((await body(p)).includes(token), false);
    },
  );
});
