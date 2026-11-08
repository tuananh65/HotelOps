import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { existsSync, readFileSync } from "node:fs";
import { build } from "esbuild";
import { chromium } from "@playwright/test";
import {
  tag,
  nextTag,
  secret,
  structure,
  editable,
  area,
  typeItem,
  roomTypes,
  room,
  rooms,
  roomDetail,
  roomHistory,
  department,
  mutationCases,
  requests,
} from "./hotel-fixtures.mjs";
const session = (id = "7", restricted = false) => ({
  user: { id, username: "server-user", name: "Confirmed User" },
  expiresAt: "2026-12-01T12:00:00Z",
  absoluteExpiresAt: "2026-12-01T20:00:00Z",
  mustChangePassword: restricted,
  csrfToken: secret,
});
const problem = (status, code) => ({
  status,
  body: { status, code, title: code, detail: secret, traceId: secret },
});
const defaults = {
  "GET /api/branches/1/structure": { body: structure() },
  "GET /api/branches/1/floors/11": { body: editable, headers: { ETag: tag } },
  "GET /api/branches/1/areas/12": {
    body: { ...area, kind: "area", floorNumber: null },
    headers: { ETag: tag },
  },
  "GET /api/room-types/14": { body: typeItem, headers: { ETag: tag } },
  "GET /api/room-types": { body: roomTypes },
  "GET /api/rooms": { body: rooms },
  "GET /api/rooms/15/edit": { body: room, headers: { ETag: tag } },
  "GET /api/rooms/15": { body: roomDetail },
  "GET /api/rooms/15/history": { body: roomHistory },
  "GET /api/users": {
    body: { items: [], page: 1, pageSize: 20, totalCount: 0 },
  },
  "GET /api/branches/1/departments/13": {
    body: department,
    headers: { ETag: tag },
  },
};
for (const [kind, , url, body] of mutationCases)
  defaults[
    `${kind.startsWith("update") ? "PUT" : "POST"} ${url.split("?")[0]}`
  ] = {
    status: kind.startsWith("create") ? 201 : 200,
    body,
    headers: { ETag: nextTag },
  };
const bundle = await build({
  stdin: {
    loader: "tsx",
    resolveDir: process.cwd(),
    contents: `
import React,{StrictMode} from 'react'; import {createRoot} from 'react-dom/client'; import {BrowserRouter} from 'react-router-dom';
import {AuthProvider} from './src/auth/AuthProvider'; import {HotelRouteHost} from './src/hotel/HotelRouteHost'; import {createAuthApi} from './src/auth/authApi'; import {createAuthStore} from './src/auth/authStore';
const scenario=window.scenario, calls=[], unexpected=[], waiting=new Map();
const defaults=${JSON.stringify(defaults)}; const sessions=[...(scenario.sessions??[{body:${JSON.stringify(session())}}])];
function respond(r){if(r.network)throw new Error(${JSON.stringify(secret)});if(r.timeout)throw new DOMException(${JSON.stringify(secret)},'TimeoutError');if(r.cancelled)throw new DOMException(${JSON.stringify(secret)},'AbortError');return new Response(r.raw??JSON.stringify(r.body),{status:r.status??200,headers:{'Content-Type':r.type??'application/json',...r.headers}});}
const fetcher=async(url,init)=>{const parsed=new URL(url,'https://hotelops.test'),key=init.method+' '+parsed.pathname; const c={url,method:init.method,body:init.body,headers:Object.fromEntries(new Headers(init.headers)),credentials:init.credentials,cache:init.cache,redirect:init.redirect,aborted:init.signal?.aborted??false};calls.push(c);init.signal?.addEventListener('abort',()=>{c.aborted=true},{once:true});
let r;if(parsed.pathname==='/api/auth/session')r=sessions.shift();else if(parsed.pathname==='/api/auth/csrf')r=scenario.csrf??{body:{csrfToken:${JSON.stringify(secret)},headerName:'X-CSRF-TOKEN'}};else if(scenario.responses?.[key])r=scenario.responses[key].shift();else r=defaults[key];
if(!r){unexpected.push(key);throw new Error('Unexpected request');}if(r.deferred)return new Promise((resolve,reject)=>waiting.set(r.deferred,{resolve,reject}));return respond(r);};
window.fetch=fetcher; const store=createAuthStore(createAuthApi(fetcher));const host=<HotelRouteHost/>;const root=createRoot(document.getElementById('root'));root.render(<StrictMode><BrowserRouter><AuthProvider store={store} fallback={host}>{host}</AuthProvider></BrowserRouter></StrictMode>);
window.fixture={calls,unexpected,revalidate:()=>store.revalidate(),navigate(path){history.pushState(null,'',path);dispatchEvent(new PopStateEvent('popstate'));},finish(label,r){const p=waiting.get(label);if(!p)throw new Error('No pending response');waiting.delete(label);try{p.resolve(respond(r))}catch(e){p.reject(e)}},unmount(){root.unmount()}};
`,
  },
  bundle: true,
  write: false,
  metafile: true,
  format: "iife",
  platform: "browser",
  jsx: "automatic",
  define: { "process.env.NODE_ENV": '"development"', "import.meta.env": "{}" },
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
async function settle(p) {
  await p.evaluate(
    () =>
      new Promise((r) => requestAnimationFrame(() => requestAnimationFrame(r))),
  );
}
async function withHotel(path, scenario, run) {
  const p = await browser.newPage(),
    errors = [],
    logs = [],
    network = [];
  p.on("pageerror", (e) => errors.push(e.message));
  p.on("console", (m) => logs.push(m.text()));
  await p.route("**/*", (route) => {
    if (route.request().resourceType() === "document")
      return route.fulfill({
        contentType: "text/html",
        body: '<div id="root"></div>',
      });
    network.push(route.request().url());
    return route.abort();
  });
  try {
    await p.goto(`http://hotelops.test${path}`);
    await p.evaluate((s) => {
      window.scenario = s;
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
    await p.addScriptTag({ content: bundle.outputFiles[0].text });
    await settle(p);
    await run(p);
    await settle(p);
    assert.deepEqual(errors, []);
    assert.deepEqual(network, []);
    assert.deepEqual(await p.evaluate(() => window.fixture.unexpected), []);
    assert.equal(await p.evaluate(() => window.storageTouches), 0);
    assert.ok(!logs.some((v) => v.includes(secret)));
    assert.ok(!(await p.locator("body").innerText()).includes(secret));
    for (const call of await calls(p)) {
      assert.equal(call.credentials, "include");
      assert.equal(call.cache, "no-store");
      assert.equal(call.redirect, "error");
      assert.equal(call.headers.authorization, undefined);
    }
  } finally {
    await p.close();
  }
}
async function calls(p) {
  return p.evaluate(() => window.fixture.calls);
}
async function writes(p) {
  return (await calls(p)).filter((c) => c.method !== "GET");
}
async function ready(p) {
  await p.getByRole("heading", { name: /Hotel structure —/ }).waitFor();
}
async function load(p) {
  await p
    .getByRole("button", { name: "Load from server", exact: true })
    .click();
  await settle(p);
}
async function select(p, action, id) {
  await ready(p);
  await p
    .getByRole("combobox", { name: "Action", exact: true })
    .selectOption(action);
  if (id) await p.locator('input[name="recordId"]').fill(String(id));
}
async function fill(p, kind) {
  for (const [key, value] of Object.entries(requests[kind])) {
    if (key === "branchId" || key === "confirmation" || value === null)
      continue;
    const input = p
      .getByRole("form", { name: "Hotel structure change" })
      .locator(`[name="${key}"]`);
    if (await input.count()) await input.fill(String(value));
  }
}
async function review(p) {
  await p.getByRole("button", { name: "Review change", exact: true }).click();
}
async function confirm(p) {
  await p.getByRole("button", { name: "Confirm change", exact: true }).click();
  await settle(p);
}
async function finish(p, label, reply) {
  await p.evaluate(({ label, reply }) => window.fixture.finish(label, reply), {
    label,
    reply,
  });
  await settle(p);
}
test("actual entry integrates new route host while accepted Auth/Users graph stays reusable", () => {
  assert.ok(readFileSync("index.html", "utf8").includes("/src/hotel/main.tsx"));
  const paths = Object.keys(bundle.metafile.inputs).map((p) =>
    p.replaceAll("\\", "/"),
  );
  for (const file of [
    "auth/AuthProvider.tsx",
    "auth/authStore.ts",
    "auth/AuthRouteHost.tsx",
    "hotel/hotelApi.ts",
  ])
    assert.ok(paths.some((p) => p.endsWith(file)));
  assert.ok(
    !paths.some((p) =>
      /src\/(services\.ts|ui\.tsx|i18n\.tsx|domain\.ts|screens\/(hotel|housekeeping|inspections)\/)/.test(
        p,
      ),
    ),
  );
});
for (const [reply, destination] of [
  [problem(401, "SESSION_INVALID"), "/login"],
  [{ body: session("7", true) }, "/change-password"],
])
  test(`guard ${destination} has no hotel request`, async () => {
    await withHotel("/hotel?branchId=1", { sessions: [reply] }, async (p) => {
      await p.waitForFunction(
        (path) => location.pathname === path,
        destination,
      );
      assert.equal(
        (await calls(p)).filter((c) => !c.url.startsWith("/api/auth/")).length,
        0,
      );
    });
  });
for (const reply of [
  { deferred: "startup" },
  problem(503, "AUTH_SERVICE_UNAVAILABLE"),
])
  test(`unconfirmed session ${reply.status ?? "pending"} renders no hotel form/data`, async () => {
    await withHotel("/hotel?branchId=1", { sessions: [reply] }, async (p) => {
      assert.equal(
        await p.locator('[aria-label="Hotel structure selectors"]').count(),
        0,
      );
      assert.equal(
        (await calls(p)).filter((c) => !c.url.startsWith("/api/auth/")).length,
        0,
      );
    });
  });
for (const invalid of ["0", "-1", "1e3", "1.5", "9007199254740992", " 1 "])
  test(`invalid branch selector ${invalid} cannot fetch hotel data`, async () => {
    await withHotel("/hotel", {}, async (p) => {
      await ready(p);
      await p.locator('input[name="branchId"]').fill(invalid);
      assert.equal(
        await p.getByRole("button", { name: "Load from server" }).isDisabled(),
        true,
      );
      assert.equal(
        (await calls(p)).filter((c) => c.url.includes("/structure")).length,
        0,
      );
    });
  });
for (const [path, expected] of [
  ["/hotel?branchId=1", "Server Branch"],
  ["/floors?branchId=1", "Server Floor"],
  ["/room-types?branchId=1", "Server Deluxe"],
  ["/rooms?branchId=1", "101"],
])
  test(`real ${path} browser read displays validated server data`, async () => {
    await withHotel(path, {}, async (p) => {
      await ready(p);
      await load(p);
      await p
        .getByRole("region", {
          name: path.startsWith("/hotel")
            ? "Progressive branch structure"
            : "Server hotel data",
        })
        .waitFor();
      assert.ok((await p.locator("body").innerText()).includes(expected));
      assert.equal((await writes(p)).length, 0);
    });
  });
for (const [screen, collection] of [
  ["areas", "areas"],
  ["departments", "departments"],
])
  test(`${screen} browse uses structure collection contract`, async () => {
    await withHotel(
      `/${screen}?branchId=1`,
      {
        responses: {
          "GET /api/branches/1/structure": [{ body: structure(collection) }],
        },
      },
      async (p) => {
        await ready(p);
        await load(p);
        await p.getByRole("region", { name: "Server hotel data" }).waitFor();
        const c = (await calls(p)).find((c) => c.url.includes("/structure"));
        assert.equal(
          new URL(c.url, "http://test.local").searchParams.get("collection"),
          collection,
        );
      },
    );
  });
for (const [kind, id, url, dto] of mutationCases) {
  const screen = kind.includes("Floor")
    ? "floors"
    : kind.includes("Area")
      ? "areas"
      : kind.includes("Type")
        ? "room-types"
        : kind.includes("Room")
          ? "rooms"
          : "departments";
  const action = kind.startsWith("create")
    ? "create"
    : kind === "deactivateType"
      ? "deactivate"
      : "edit";
  test(`${kind} form reviews and confirms exactly one real-client mutation`, async () => {
    await withHotel(`/${screen}?branchId=1`, {}, async (p) => {
      await select(p, action, action === "create" ? null : id);
      if (action !== "create") await load(p);
      await p.getByRole("form", { name: "Hotel structure change" }).waitFor();
      await fill(p, kind);
      await review(p);
      assert.equal((await writes(p)).length, 0);
      await p
        .getByRole("region", { name: "Review hotel structure change" })
        .waitFor();
      await confirm(p);
      await p.getByRole("status", { name: "Confirmed hotel change" }).waitFor();
      const c = (await writes(p))[0];
      assert.equal((await writes(p)).length, 1);
      assert.equal(c.url, url);
      assert.equal(c.method, kind.startsWith("update") ? "PUT" : "POST");
      assert.deepEqual(JSON.parse(c.body), requests[kind]);
      assert.equal(c.headers["x-csrf-token"], secret);
      assert.equal(
        c.headers["if-match"],
        action === "create" ? undefined : tag,
      );
      assert.ok((await p.locator("body").innerText()).includes(String(dto.id)));
    });
  });
}
test("review edits and Enter cannot dispatch; synchronous double confirm sends once", async () => {
  await withHotel(
    "/floors?branchId=1",
    { responses: { "POST /api/branches/1/floors": [{ deferred: "post" }] } },
    async (p) => {
      await select(p, "create");
      await fill(p, "createFloor");
      await review(p);
      await p.locator('input[name="name"]').fill("Edited Floor");
      assert.equal(
        await p.getByRole("button", { name: "Confirm change" }).count(),
        0,
      );
      await p.locator('input[name="name"]').press("Enter");
      assert.equal((await writes(p)).length, 0);
      await review(p);
      await p.getByRole("button", { name: "Confirm change" }).evaluate((b) => {
        b.click();
        b.click();
      });
      await settle(p);
      assert.equal((await writes(p)).length, 1);
      await finish(p, "post", {
        status: 201,
        body: { ...editable, name: "Edited Floor" },
      });
      await p.getByRole("status", { name: "Confirmed hotel change" }).waitFor();
    },
  );
});
for (const reply of [
  problem(403, "PERMISSION_DENIED"),
  problem(404, "ROOM_NOT_FOUND"),
  problem(503, "ROOM_DIRECTORY_UNAVAILABLE"),
  { raw: "{", status: 200 },
])
  test(`GET error ${reply.status} never falls back to mock data`, async () => {
    await withHotel(
      "/rooms?branchId=1",
      { responses: { "GET /api/rooms": [reply] } },
      async (p) => {
        await ready(p);
        await load(p);
        await p.getByRole("alert").waitFor();
        assert.equal(
          await p.getByRole("region", { name: "Server hotel data" }).count(),
          0,
        );
        assert.equal((await writes(p)).length, 0);
      },
    );
  });
for (const reply of [
  problem(500, "INTERNAL_SERVER_ERROR"),
  problem(503, "UNAVAILABLE"),
  { network: true },
  { timeout: true },
  { cancelled: true },
  { status: 201, raw: "{" },
  problem(409, "UNKNOWN"),
])
  test(`uncertain mutation ${JSON.stringify(reply)} stays locked after manual read`, async () => {
    await withHotel(
      "/floors?branchId=1",
      {
        responses: {
          "PUT /api/branches/1/floors/11": [
            reply.status === 201 ? { ...reply, status: 200 } : reply,
          ],
        },
      },
      async (p) => {
        await select(p, "edit", 11);
        await load(p);
        await p.getByRole("form", { name: "Hotel structure change" }).waitFor();
        await fill(p, "updateFloor");
        await review(p);
        await confirm(p);
        await p
          .getByText(
            "The change could not be confirmed. It may already have been saved. Do not retry automatically. Ask an authorized administrator to verify the record before making another change.",
            { exact: true },
          )
          .waitFor();
        await load(p);
        await p.getByRole("region", { name: "Server hotel data" }).waitFor();
        assert.equal(
          await p.getByRole("form", { name: "Hotel structure change" }).count(),
          0,
        );
        assert.equal((await writes(p)).length, 1);
      },
    );
  });
test("412 clears version and requires fresh read/review before another PUT", async () => {
  await withHotel(
    "/floors?branchId=1",
    {
      responses: {
        "GET /api/branches/1/floors/11": [
          { body: editable, headers: { ETag: tag } },
          {
            body: { ...editable, name: "Fresh Floor" },
            headers: { ETag: nextTag },
          },
        ],
        "PUT /api/branches/1/floors/11": [
          problem(412, "STALE"),
          {
            body: { ...editable, name: "Fresh Floor" },
            headers: { ETag: '"' + "C".repeat(64) + '"' },
          },
        ],
      },
    },
    async (p) => {
      await select(p, "edit", 11);
      await load(p);
      await p.getByRole("form", { name: "Hotel structure change" }).waitFor();
      await fill(p, "updateFloor");
      await review(p);
      await confirm(p);
      await p.getByRole("alert").waitFor();
      assert.equal(
        await p.getByRole("form", { name: "Hotel structure change" }).count(),
        0,
      );
      assert.equal((await writes(p)).length, 1);
      await load(p);
      await p.getByRole("form", { name: "Hotel structure change" }).waitFor();
      assert.equal(
        await p.locator('input[name="name"]').inputValue(),
        "Fresh Floor",
      );
      await p
        .locator('input[name="reason"]')
        .fill("Review the refreshed floor");
      await review(p);
      await confirm(p);
      await p.getByRole("status", { name: "Confirmed hotel change" }).waitFor();
      assert.equal((await writes(p))[1].headers["if-match"], nextTag);
    },
  );
});
for (const id of ["7", "8"])
  test(`session replacement ${id} aborts pending write and ignores late success`, async () => {
    await withHotel(
      "/floors?branchId=1",
      {
        sessions: [{ body: session() }, { body: session(id) }],
        responses: { "POST /api/branches/1/floors": [{ deferred: "post" }] },
      },
      async (p) => {
        await select(p, "create");
        await fill(p, "createFloor");
        await review(p);
        await confirm(p);
        await p.evaluate(() => window.fixture.revalidate());
        await settle(p);
        await finish(p, "post", { status: 201, body: editable });
        assert.equal((await writes(p))[0].aborted, true);
        assert.equal(
          await p
            .getByRole("status", { name: "Confirmed hotel change" })
            .count(),
          0,
        );
        assert.equal(
          await p.getByRole("form", { name: "Hotel structure change" }).count(),
          0,
        );
      },
    );
  });
test("branch change aborts old GET; late branch data remains hidden", async () => {
  await withHotel(
    "/hotel?branchId=1",
    { responses: { "GET /api/branches/1/structure": [{ deferred: "read" }] } },
    async (p) => {
      await ready(p);
      await load(p);
      await p.locator('input[name="branchId"]').fill("2");
      await finish(p, "read", { body: structure() });
      assert.equal(
        await p
          .getByRole("region", { name: "Progressive branch structure" })
          .count(),
        0,
      );
      assert.equal(
        (await calls(p)).find((c) => c.url.includes("/structure")).aborted,
        true,
      );
    },
  );
});
test("navigation aborts mutation; late success cannot publish in accepted Profile", async () => {
  await withHotel(
    "/floors?branchId=1",
    { responses: { "POST /api/branches/1/floors": [{ deferred: "post" }] } },
    async (p) => {
      await select(p, "create");
      await fill(p, "createFloor");
      await review(p);
      await confirm(p);
      await p.evaluate(() => window.fixture.navigate("/profile"));
      await settle(p);
      await finish(p, "post", { status: 201, body: editable });
      assert.equal(new URL(p.url()).pathname, "/profile");
      assert.equal((await writes(p))[0].aborted, true);
      assert.equal(
        await p.getByRole("status", { name: "Confirmed hotel change" }).count(),
        0,
      );
    },
  );
});

test("UC013 selects a floor node and child collection using server contract", async () => {
  const data = {
    ...structure("areas"),
    nodeType: "floor",
    selectedFloor: editable,
  };
  await withHotel(
    "/hotel?branchId=1",
    {
      responses: {
        "GET /api/branches/1/structure": [
          { body: structure() },
          { body: data },
        ],
      },
    },
    async (p) => {
      await ready(p);
      await load(p);
      await p
        .getByRole("button", { name: "floor: Server Floor", exact: true })
        .click();
      await p
        .getByRole("region", { name: "Progressive branch structure" })
        .waitFor();
      const c = (await calls(p))
        .filter((c) => c.url.includes("/structure"))
        .at(-1);
      const q = new URL(c.url, "http://test.local").searchParams;
      assert.equal(q.get("nodeType"), "floor");
      assert.equal(q.get("nodeId"), "11");
      assert.equal(q.get("collection"), "areas");
    },
  );
});
test("UC023 recorded history is fetched through constructed scoped URL", async () => {
  await withHotel("/rooms?branchId=1", {}, async (p) => {
    await select(p, "history", 15);
    await load(p);
    await p.getByRole("region", { name: "Server hotel data" }).waitFor();
    assert.ok(
      (await p.locator("body").innerText()).includes(
        "RECORDED_STATUS_EVENTS_ONLY",
      ),
    );
    const c = (await calls(p)).find((c) => c.url.includes("/history"));
    assert.equal(c.url, "/api/rooms/15/history?branchId=1&page=1&pageSize=20");
    assert.equal((await writes(p)).length, 0);
  });
});
test("global room read remains subject to backend denial without local permission inference", async () => {
  await withHotel(
    "/rooms",
    { responses: { "GET /api/rooms": [problem(403, "PERMISSION_DENIED")] } },
    async (p) => {
      await ready(p);
      await p
        .getByRole("checkbox", { name: "All branches (server-authorized)" })
        .check();
      await load(p);
      await p.getByRole("alert").waitFor();
      const c = (await calls(p)).find((c) => c.url.startsWith("/api/rooms"));
      const q = new URL(c.url, "http://test.local").searchParams;
      assert.equal(q.get("allBranches"), "true");
      assert.equal(q.has("branchId"), false);
      assert.equal(
        await p.getByRole("region", { name: "Server hotel data" }).count(),
        0,
      );
    },
  );
});
test("new entry still renders accepted Users routes", async () => {
  await withHotel("/users", {}, async (p) => {
    await p.waitForFunction(() =>
      window.fixture.calls.some((c) => c.url.startsWith("/api/users?")),
    );
    await settle(p);
    assert.equal(new URL(p.url()).pathname, "/users");
    assert.equal((await writes(p)).length, 0);
    assert.equal(
      await p.getByRole("heading", { name: /Hotel structure/ }).count(),
      0,
    );
  });
});
test("pending write and session unavailability stay unknown after manual session recovery", async () => {
  await withHotel(
    "/floors?branchId=1",
    {
      sessions: [
        { body: session() },
        problem(503, "AUTH_SERVICE_UNAVAILABLE"),
        { body: session() },
      ],
      responses: { "POST /api/branches/1/floors": [{ deferred: "post" }] },
    },
    async (p) => {
      await select(p, "create");
      await fill(p, "createFloor");
      await review(p);
      await confirm(p);
      await p.evaluate(() => window.fixture.revalidate());
      await settle(p);
      assert.equal(
        await p.getByRole("form", { name: "Hotel structure change" }).count(),
        0,
      );
      await finish(p, "post", { status: 201, body: editable });
      await p
        .getByRole("button", { name: "Check session", exact: true })
        .click();
      await ready(p);
      assert.equal(
        await p.getByRole("form", { name: "Hotel structure change" }).count(),
        0,
      );
      assert.equal(
        await p.getByRole("status", { name: "Confirmed hotel change" }).count(),
        0,
      );
      assert.equal((await writes(p))[0].aborted, true);
    },
  );
});
test("room type active-room blocker is rejected without bypass or replay", async () => {
  await withHotel(
    "/room-types?branchId=1",
    {
      responses: {
        "POST /api/room-types/14/deactivate": [
          problem(409, "ACTIVE_ROOMS_EXIST"),
        ],
      },
    },
    async (p) => {
      await select(p, "deactivate", 14);
      await load(p);
      await p.getByRole("form", { name: "Hotel structure change" }).waitFor();
      await fill(p, "deactivateType");
      await review(p);
      await confirm(p);
      await p.getByRole("alert").waitFor();
      assert.ok((await p.locator("body").innerText()).includes("active rooms"));
      assert.equal((await writes(p)).length, 1);
      assert.equal(
        await p.getByRole("status", { name: "Confirmed hotel change" }).count(),
        0,
      );
    },
  );
});

test("dashboard exposes FE02 without changing accepted AuthRouteHost", async () => {
  await withHotel("/dashboard", {}, async (p) => {
    await p.getByRole("link", { name: "Hotel structure", exact: true }).click();
    await ready(p);
    assert.equal(new URL(p.url()).pathname, "/hotel");
    assert.equal(
      (await calls(p)).filter((c) => !c.url.startsWith("/api/auth/")).length,
      0,
    );
  });
});
for (const result of [
  problem(401, "SESSION_INVALID"),
  problem(403, "PERMISSION_DENIED"),
])
  test(`mutation ${result.status} clears editor; 401 performs bounded revalidation only`, async () => {
    await withHotel(
      "/floors?branchId=1",
      {
        sessions: [{ body: session() }, { body: session() }],
        responses: { "PUT /api/branches/1/floors/11": [result] },
      },
      async (p) => {
        await select(p, "edit", 11);
        await load(p);
        await p.getByRole("form", { name: "Hotel structure change" }).waitFor();
        await fill(p, "updateFloor");
        await review(p);
        await confirm(p);
        await settle(p);
        assert.equal(
          await p.getByRole("form", { name: "Hotel structure change" }).count(),
          0,
        );
        assert.equal(
          await p.getByRole("region", { name: "Server hotel data" }).count(),
          0,
        );
        assert.equal((await writes(p)).length, 1);
        assert.equal(
          (await calls(p)).filter((c) => c.url === "/api/auth/session").length,
          result.status === 401 ? 2 : 1,
        );
        assert.equal(
          (await calls(p)).filter(
            (c) => c.url.includes("/floors/11") && c.method === "GET",
          ).length,
          1,
        );
      },
    );
  });
