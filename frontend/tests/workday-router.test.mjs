import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { existsSync } from "node:fs";
import { build } from "esbuild";
import { chromium } from "@playwright/test";
import { day, shift, session, secret } from "./workday-fixtures.mjs";
const bundles = new Map();
for (const flag of [undefined, "true", "false", "TRUE"]) {
  const result = await build({
    entryPoints: ["src/hotel/main.tsx"],
    bundle: true,
    write: false,
    format: "iife",
    jsx: "automatic",
    loader: { ".css": "empty" },
    define: {
      "import.meta.env": JSON.stringify(
        flag === undefined ? {} : { VITE_FE12_READ_ENABLED: flag },
      ),
      "process.env.NODE_ENV": '"production"',
    },
  });
  bundles.set(flag, result.outputFiles[0].text);
}
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
const workCalls = (p) =>
  p.evaluate(() => window.calls.filter((c) => c.url.includes("/workdays/")));
const settle = (p) =>
  p.evaluate(
    () =>
      new Promise((r) => requestAnimationFrame(() => requestAnimationFrame(r))),
  );
async function pageTest(
  {
    flag = "true",
    route = "/workday",
    auth = { body: session() },
    responses = {},
  },
  run,
) {
  const p = await browser.newPage(),
    errors = [],
    network = [];
  p.setDefaultTimeout(6000);
  p.on("pageerror", (e) => errors.push(e.message));
  await p.route("**/*", (r) => {
    if (r.request().resourceType() === "document")
      return r.fulfill({
        contentType: "text/html",
        body: '<div id="root"></div>',
      });
    network.push(r.request().url());
    return r.abort();
  });
  try {
    await p.goto(`http://offline.test${route}`);
    await p.evaluate(
      ({ auth, responses, day, shift }) => {
        window.calls = [];
        window.waiting = new Map();
        window.responses = responses;
        const respond = (r) =>
          new Response(r.status === 204 ? null : JSON.stringify(r.body), {
            status: r.status ?? 200,
            headers: { "Content-Type": "application/json" },
          });
        window.finish = (label, r) => window.waiting.get(label)(respond(r));
        window.fetch = async (url, init) => {
          const call = {
            url,
            method: init.method,
            credentials: init.credentials,
            cache: init.cache,
            headers: Object.fromEntries(init.headers),
            aborted: false,
          };
          window.calls.push(call);
          init.signal?.addEventListener(
            "abort",
            () => {
              call.aborted = true;
            },
            { once: true },
          );
          let reply = responses[url];
          if (url === "/api/auth/session") reply = auth;
          if (!reply && url.includes("/workdays/")) {
            const branch = Number(url.split("/")[3]);
            reply = url.endsWith("own-shifts")
              ? {
                  body: [{ ...shift, branchId: branch, shiftId: 100 + branch }],
                }
              : { body: { ...day, branchId: branch, workdayId: 200 + branch } };
          }
          if (!reply) reply = { status: 503, body: {} };
          if (reply.deferred)
            return new Promise((resolve) =>
              window.waiting.set(reply.deferred, resolve),
            );
          return respond(reply);
        };
        for (const name of ["localStorage", "sessionStorage"])
          Object.defineProperty(window, name, {
            get() {
              throw Error("Browser storage forbidden");
            },
          });
      },
      { auth, responses, day: day(), shift: shift() },
    );
    await p.addScriptTag({
      content: bundles.get(flag === "missing" ? undefined : flag),
    });
    await run(p);
    await settle(p);
    assert.deepEqual(errors, []);
    assert.deepEqual(network, []);
    assert.ok(!(await p.locator("body").innerText()).includes(secret));
    for (const call of await workCalls(p)) {
      assert.equal(call.method, "GET");
      assert.equal(call.credentials, "include");
      assert.equal(call.cache, "no-store");
      assert.equal(call.headers.authorization, undefined);
      assert.match(
        call.url,
        /^\/api\/branches\/[1-9][0-9]*\/workdays\/(own-shifts|current)$/,
      );
    }
  } finally {
    await p.close();
  }
}
async function selectBranch(p, id) {
  await p.getByRole("textbox", { name: "Branch ID", exact: true }).fill(id);
  await p.getByRole("button", { name: "View workday", exact: true }).click();
}
for (const flag of ["missing", "false", "TRUE"])
  test(`flag ${flag} blocks route and navigation without requests`, () =>
    pageTest({ flag }, async (p) => {
      await p.getByRole("heading", { name: "HotelOps workspace" }).waitFor();
      assert.equal(
        await p.getByRole("textbox", { name: "Branch ID" }).count(),
        0,
      );
      await p.evaluate(() => {
        history.pushState(null, "", "/dashboard");
        dispatchEvent(new PopStateEvent("popstate"));
      });
      await p.waitForURL("**/dashboard");
      assert.equal(
        await p.getByRole("link", { name: "Workday", exact: true }).count(),
        0,
      );
      assert.equal((await workCalls(p)).length, 0);
    }));
test("flag ON exposes dashboard link; route waits for explicit branch", () =>
  pageTest({ route: "/dashboard" }, async (p) => {
    await p.getByRole("link", { name: "Workday", exact: true }).click();
    await p.getByRole("textbox", { name: "Branch ID" }).waitFor();
    assert.equal((await workCalls(p)).length, 0);
    await selectBranch(p, "3");
    await p.getByText("Workday 203,", { exact: false }).waitFor();
    await p.getByText("Ca 103:", { exact: false }).waitFor();
    assert.equal((await workCalls(p)).length, 2);
  }));
for (const [auth, destination] of [
  [{ status: 401, body: {} }, "/login"],
  [{ body: session(true) }, "/change-password"],
])
  test(`unconfirmed/restricted redirects ${destination} without workday GET`, () =>
    pageTest({ auth }, async (p) => {
      await p.waitForURL(`**${destination}`);
      await settle(p);
      assert.equal(
        await p.getByRole("textbox", { name: "Branch ID" }).count(),
        0,
      );
      assert.equal((await workCalls(p)).length, 0);
    }));
test("branch validation and edit invalidate old selection immediately", () =>
  pageTest({}, async (p) => {
    await p.getByRole("textbox", { name: "Branch ID" }).waitFor();
    for (const id of ["0", "-1", "1.5", "9007199254740992", " 1", "1e2"]) {
      await selectBranch(p, id);
      await p.getByRole("alert").waitFor();
      assert.equal((await workCalls(p)).length, 0);
    }
    await selectBranch(p, "2");
    await p.getByText("Workday 202,", { exact: false }).waitFor();
    await p.getByRole("textbox", { name: "Branch ID" }).fill("3");
    assert.equal(
      await p.getByText("Workday 202,", { exact: false }).count(),
      0,
    );
    assert.equal((await workCalls(p)).length, 2);
    await p.getByRole("button", { name: "View workday" }).click();
    await p.getByText("Workday 203,", { exact: false }).waitFor();
  }));
for (const denied of ["own-shifts", "current"])
  test(`router preserves independent permission when ${denied} denied`, () =>
    pageTest(
      {
        responses: {
          [`/api/branches/2/workdays/${denied}`]: {
            status: 403,
            body: { detail: secret },
          },
        },
      },
      async (p) => {
        await p.getByRole("textbox", { name: "Branch ID" }).waitFor();
        await selectBranch(p, "2");
        await p.getByRole("alert").waitFor();
        await p
          .getByText(denied === "current" ? "Ca 102:" : "Workday 202,", {
            exact: false,
          })
          .waitFor();
        assert.equal((await workCalls(p)).length, 2);
      },
    ));
test("backend inactive 404 is unavailable for both independent reads", () =>
  pageTest(
    {
      responses: {
        "/api/branches/2/workdays/own-shifts": { status: 404, body: {} },
        "/api/branches/2/workdays/current": { status: 404, body: {} },
      },
    },
    async (p) => {
      await p.getByRole("textbox", { name: "Branch ID" }).waitFor();
      await selectBranch(p, "2");
      await p.waitForFunction(
        () => document.querySelectorAll("[role=alert]").length === 2,
      );
      assert.equal(await p.getByText("Không có workday đang làm.").count(), 0);
    },
  ));
test("branch change aborts late current and preserves FE01/FE02 navigation", () =>
  pageTest(
    { responses: { "/api/branches/2/workdays/current": { deferred: "old" } } },
    async (p) => {
      await p.getByRole("textbox", { name: "Branch ID" }).waitFor();
      await selectBranch(p, "2");
      await p.getByText("Ca 102:", { exact: false }).waitFor();
      await selectBranch(p, "3");
      await p.getByText("Workday 203,", { exact: false }).waitFor();
      await p.evaluate((d) => window.finish("old", { body: d }), day(2));
      await settle(p);
      assert.equal(
        await p.getByText("Workday 20,", { exact: false }).count(),
        0,
      );
      assert.ok(
        (await workCalls(p)).find(
          (c) => c.url === "/api/branches/2/workdays/current",
        ).aborted,
      );
      await p.getByRole("link", { name: "Hồ sơ", exact: true }).click();
      await p.waitForURL("**/profile");
      await p.goBack();
      await p.waitForURL("**/workday");
      await p.getByRole("textbox", { name: "Branch ID" }).waitFor();
      assert.equal(
        await p.getByText("Workday 203,", { exact: false }).count(),
        0,
      );
      await selectBranch(p, "3");
      await p.getByText("Workday 203,", { exact: false }).waitFor();
      await p
        .getByRole("link", { name: "Cấu trúc khách sạn", exact: true })
        .click();
      await p.waitForURL("**/hotel");
      assert.equal(
        await p
          .getByRole("heading", {
            name: "Ca làm và workday của tôi",
            exact: true,
          })
          .count(),
        0,
      );
    },
  ));
