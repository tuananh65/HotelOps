import { test } from "node:test";
import assert from "node:assert/strict";
import { structureReply, session, secret } from "./independent-fixtures.mjs";
import { pageTest, reloadCompositionPage } from "./independent-browser.mjs";
const calls = (p) =>
  p.evaluate(() =>
    window.fixture.calls.filter((c) => c.url.includes("/structure")),
  );
const settle = (p) =>
  p.evaluate(
    () =>
      new Promise((r) => requestAnimationFrame(() => requestAnimationFrame(r))),
  );
async function load(p, branch = "1") {
  await p.getByRole("textbox", { name: "Branch ID", exact: true }).fill(branch);
  await p
    .getByRole("button", { name: "Load from server", exact: true })
    .click();
}
test("Branch -> Floor -> Area -> Room details uses one progressive request per click", () =>
  pageTest({}, async (p) => {
    await load(p);
    await p
      .getByRole("button", { name: "floor: Floor 11", exact: true })
      .click();
    await p.getByRole("button", { name: "area: Area 12", exact: true }).click();
    await p.getByRole("button", { name: "room: 101", exact: true }).click();
    await p
      .getByRole("heading", { name: "Selected room", exact: true })
      .waitFor();
    const urls = (await calls(p)).map(
      (c) => new URL(c.url, "https://offline.test"),
    );
    assert.equal(urls.length, 4);
    assert.deepEqual(
      urls.map((u) => [
        u.searchParams.get("nodeType"),
        u.searchParams.get("nodeId"),
        u.searchParams.get("collection"),
      ]),
      [
        ["branch", null, "floors"],
        ["floor", "11", "areas"],
        ["area", "12", "rooms"],
        ["room", "15", null],
      ],
    );
    await p
      .getByRole("button", { name: "floor: Floor 11", exact: true })
      .click();
    await p
      .getByRole("heading", { name: "Selected floor", exact: true })
      .waitFor();
  }));
test("floor rooms switch and pagination preserve node/branch; filter resets page", () =>
  pageTest({}, async (p) => {
    await load(p);
    await p
      .getByRole("button", { name: "floor: Floor 11", exact: true })
      .click();
    await p.getByText("21 records; page 1 of 2;", { exact: false }).waitFor();
    await p.getByRole("button", { name: "Next page", exact: true }).click();
    await p
      .getByRole("button", { name: "area: Area 32", exact: true })
      .waitFor();
    let u = new URL((await calls(p)).at(-1).url, "https://offline.test");
    assert.equal(u.searchParams.get("nodeType"), "floor");
    assert.equal(u.searchParams.get("nodeId"), "11");
    assert.equal(u.searchParams.get("page"), "2");
    await p
      .getByRole("combobox", { name: "Active filter" })
      .selectOption("false");
    await p
      .getByRole("button", { name: "area: Area 12", exact: true })
      .waitFor();
    u = new URL((await calls(p)).at(-1).url, "https://offline.test");
    assert.equal(u.searchParams.get("page"), "1");
    assert.equal(u.searchParams.get("isActive"), "false");
    await p.getByRole("button", { name: "Rooms", exact: true }).click();
    await p.getByRole("button", { name: "room: 101", exact: true }).waitFor();
    u = new URL((await calls(p)).at(-1).url, "https://offline.test");
    assert.equal(u.searchParams.get("nodeId"), "11");
    assert.equal(u.searchParams.get("collection"), "rooms");
  }));
for (const management of [false, true])
  test(`Departments controlled by response availability ${management}`, () =>
    pageTest({ management }, async (p) => {
      await load(p);
      await p.getByRole("heading", { name: "Selected branch" }).waitFor();
      assert.equal(
        await p
          .getByRole("button", { name: "Departments", exact: true })
          .count(),
        management ? 1 : 0,
      );
      if (management) {
        await p
          .getByRole("button", { name: "Departments", exact: true })
          .click();
        await p
          .getByRole("button", {
            name: "department: Server Department",
            exact: true,
          })
          .click();
        await p.getByRole("heading", { name: "Selected department" }).waitFor();
      }
    }));
test("empty collection shows zero count without alert", () => {
  const data = structureReply(
    "/api/branches/1/structure?nodeType=branch&collection=floors",
  );
  data.floors.items = [];
  data.floors.totalCount = 0;
  return pageTest({ structure: { body: data } }, async (p) => {
    await load(p);
    await p.getByText("No records on this page.").waitFor();
    assert.equal(await p.getByRole("alert").count(), 0);
    assert.equal((await calls(p)).length, 1);
  });
});
for (const status of [403, 404, 503])
  test(`structure ${status} error not empty`, () =>
    pageTest(
      {
        structure: { status, body: { status, title: secret, detail: secret } },
      },
      async (p) => {
        await load(p);
        await p.getByRole("alert").waitFor();
        assert.equal(await p.getByText("No records on this page.").count(), 0);
        assert.equal(
          await p
            .getByRole("region", { name: "Progressive branch structure" })
            .count(),
          0,
        );
      },
    ));
test("401 read revalidates and hides all protected tree data", () =>
  pageTest(
    {
      structure: {
        status: 401,
        body: { status: 401, title: "SESSION_INVALID" },
      },
    },
    async (p) => {
      await p.getByRole("textbox", { name: "Branch ID" }).waitFor();
      await p.evaluate(() => {
        window.fixture.scenario.auth = {
          status: 401,
          body: { status: 401, title: "SESSION_INVALID" },
        };
      });
      await load(p);
      await p.waitForURL("**/login");
      assert.equal(
        await p
          .getByRole("region", { name: "Progressive branch structure" })
          .count(),
        0,
      );
    },
  ));
test("branch edit aborts old response and removes old tree", () =>
  pageTest(
    { replies: { "1:branch:floors:1": { deferred: "old" } } },
    async (p) => {
      await load(p);
      await p.waitForFunction(() =>
        window.fixture.calls.some((c) => c.url.includes("/structure")),
      );
      await load(p, "2");
      await p
        .getByRole("button", { name: "Branch: Branch 2", exact: true })
        .waitFor();
      await p.evaluate(
        (d) => window.fixture.finish("old", { body: d }),
        structureReply(
          "/api/branches/1/structure?nodeType=branch&collection=floors",
        ),
      );
      await settle(p);
      assert.equal(
        await p
          .getByRole("button", { name: "Branch: Branch 1", exact: true })
          .count(),
        0,
      );
      assert.equal((await calls(p))[0].aborted, true);
    },
  ));
test("session replacement hides completed tree and late former-owner result", () =>
  pageTest(
    { replies: { "1:floor:areas:1": { deferred: "oldFloor" } } },
    async (p) => {
      await load(p);
      await p
        .getByRole("button", { name: "floor: Floor 11", exact: true })
        .click();
      await p.waitForFunction(() =>
        window.fixture.calls.some((c) => c.url.includes("nodeType=floor")),
      );
      await p.evaluate((s) => {
        window.fixture.scenario.auth = { body: s };
        return window.fixture.revalidate();
      }, session("8"));
      await p.evaluate(
        (d) => window.fixture.finish("oldFloor", { body: d }),
        structureReply(
          "/api/branches/1/structure?nodeType=floor&nodeId=11&collection=areas",
        ),
      );
      await settle(p);
      assert.equal(
        await p
          .getByRole("region", { name: "Progressive branch structure" })
          .count(),
        0,
      );
    },
  ));
test("read-only promotion excludes create/update navigation and mutation endpoints", () =>
  pageTest({}, async (p) => {
    await load(p);
    assert.equal(
      await p
        .getByRole("link", { name: /Create Branch|Update branch information/ })
        .count(),
      0,
    );
    await p.evaluate(() => window.fixture.navigate("/branches/42/edit"));
    await p.getByText("Page not found", { exact: true }).waitFor();
    assert.equal(
      (await p.evaluate(() => window.fixture.calls)).filter(
        (c) => c.method !== "GET",
      ).length,
      0,
    );
  }));

test("096 composed Users -> Structure -> browser Back -> refresh stays GET-only", () =>
  pageTest(
    {},
    async (p) => {
      await p.getByText("No matching accounts.").waitFor();
      await p.evaluate(() => window.fixture.navigate("/hotel?branchId=2"));
      assert.equal(
        await p
          .getByRole("textbox", { name: "Branch ID", exact: true })
          .inputValue(),
        "2",
      );
      await load(p, "2");
      await p
        .getByRole("button", { name: "Branch: Branch 2", exact: true })
        .waitFor();
      await p.goBack();
      await p.getByText("No matching accounts.").waitFor();
      assert.equal(
        await p
          .getByRole("region", { name: "Progressive branch structure" })
          .count(),
        0,
      );
      assert.equal(
        (await p.evaluate(() => window.fixture.calls)).filter(
          (c) => c.method !== "GET",
        ).length,
        0,
      );
      await reloadCompositionPage(p);
      await p
        .getByRole("heading", { name: "User directory", exact: true })
        .waitFor();
      await p.getByText("No matching accounts.").waitFor();
    },
    "/users",
  ));
test("096 late structure response after navigation to Users cannot restore former slice", () =>
  pageTest({ structure: { deferred: "old" } }, async (p) => {
    await load(p);
    await p.waitForFunction(() =>
      window.fixture.calls.some((c) => c.url.includes("/structure")),
    );
    await p.evaluate(() => window.fixture.navigate("/users"));
    await p.getByText("No matching accounts.").waitFor();
    await p.evaluate(
      (d) => window.fixture.finish("old", { body: d }),
      structureReply(
        "/api/branches/1/structure?nodeType=branch&collection=floors",
      ),
    );
    await settle(p);
    assert.equal(
      await p
        .getByRole("region", { name: "Progressive branch structure" })
        .count(),
      0,
    );
    assert.equal((await calls(p))[0].aborted, true);
  }));
