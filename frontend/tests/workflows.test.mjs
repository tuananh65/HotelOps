import { test, beforeEach } from "node:test";
import assert from "node:assert/strict";
import {
  bundle,
  browserGlobals,
  json,
  entity,
  session,
} from "./api-test-helpers.mjs";
import {
  catalog,
  context,
  fixtureResponse,
  collections,
  ids,
} from "./fixtures/api-fixture.mjs";
let s,
  calls = [];
let caseNo = 0;
beforeEach(async () => {
  browserGlobals();
  calls = [];
  s = await bundle("src/services.ts", "services-" + caseNo++);
  globalThis.fetch = async (url, options = {}) => {
    calls.push({ url, options });
    const value = fixtureResponse(
      url,
      options.method,
      options.body ? JSON.parse(options.body) : {},
    );
    return value === undefined
      ? new Response(null, { status: 204 })
      : json(value);
  };
  await s.login("tester", "password");
});
test("all 80 screens have a successful API loading path using the proposed scoped context", async () => {
  for (const screen of catalog.screens.filter((s) => s.id > 2)) {
    const { domainFor } =
      await import("../.test-output/fixture-domain.mjs").catch(async () => {
        return bundle("src/domain.ts", "fixture-domain");
      });
    const key = [36, 37].includes(screen.id)
      ? "inspections"
      : screen.id === 50
        ? "assets"
        : domainFor(screen.id).key;
    await s.loadScreen(
      screen.id,
      screen.route.includes(":id") ? ids[key] : undefined,
    );
  }
  assert.equal(Object.keys(localStorage.data).length, 0);
  assert.ok(calls.some((call) => call.url.includes("/api/incidents")));
});
test("session grants pair each use case with its branch and department; scope cannot leak across grants", async () => {
  const auth = session([
    { useCase: "UC-021", branchIds: ["1"], departmentIds: ["2"] },
    { useCase: "UC-095", branchIds: ["b2"], departmentIds: ["d2"] },
  ]);
  globalThis.fetch = async () => json(auth);
  await s.login("test", "test");
  assert.equal(s.can("UC-021", { branchId: "b2", departmentId: "d2" }), false);
  assert.equal(s.can("UC-021", { branchId: "1", departmentId: "2" }), true);
  assert.equal(s.can("UC-004"), false);
});
test("a cleaning completion is a dedicated command, never a generic status update or browser readiness calculation", async () => {
  const state = structuredClone(context);
  state.collections.rooms[0].statusCode = "BLOCKED";
  const oldFetch = globalThis.fetch;
  globalThis.fetch = async (url, options = {}) => {
    if (url.includes("/api/ui/screens/26/context")) return json(state);
    if (url.endsWith("/api/cleaning-tasks/cl1/submit-for-inspection")) {
      calls.push({ url, options });
      state.collections.cleaning[0].statusCode = "PENDING_INSPECTION";
      return json(state.collections.cleaning[0]);
    }
    if (url.includes("/api/cleaning-tasks/cl1") && options.method === "GET")
      return json(state.collections.cleaning[0]);
    return oldFetch(url, options);
  };
  await s.loadScreen(26, "cl1");
  const r = s.visible("cleaning")[0];
  const saved = await s.perform(
    { key: "cleaning" },
    r,
    {
      label: "Submit for inspection",
      uc: "UC-034",
      from: ["In Progress"],
      own: true,
    },
    { note: "Done" },
  );
  assert.equal(saved.status, "Pending Inspection");
  assert.equal(s.snapshot().rooms[0].status, "Blocked");
  const call = calls.find((c) => c.url.endsWith("submit-for-inspection"));
  const body = JSON.parse(call.options.body);
  assert.equal(body.rowVersion, "AAAAAQ==");
  assert.equal(body.note, "Done");
  assert.equal("status" in body, false);
});
test("server availableActions and rowVersion are required before changing a record", async () => {
  await s.loadScreen(26, "cl1");
  const r = s.visible("cleaning")[0];
  assert.deepEqual(
    s.availableActions({ key: "cleaning" }, { ...r, allowedActions: [] }),
    [],
  );
  await assert.rejects(
    s.perform(
      { key: "cleaning" },
      { ...r, rowVersion: undefined },
      { label: "Submit for inspection", uc: "UC-034", from: ["In Progress"] },
      { note: "Done" },
    ),
    /rowVersion/,
  );
});
test("submit mutex blocks a second write while the first request is still in flight", async () => {
  let finish;
  const first = s.runMutation(
    "same",
    () =>
      new Promise((resolve) => {
        finish = resolve;
      }),
  );
  await assert.rejects(
    s.runMutation("same", async () => 42),
    /already being submitted/,
  );
  finish("done");
  assert.equal(await first, "done");
});
test("API errors never switch to mock data or write browser business storage", async () => {
  globalThis.fetch = async () => new Response(null, { status: 500 });
  await assert.rejects(s.loadScreen(19));
  assert.equal(s.snapshot().rooms.length, 0);
  assert.equal(localStorage.data.size, 0);
});
test("server stock projection is read directly; a ledger page is never summed into a balance", async () => {
  await s.loadScreen(61);
  assert.equal(s.balance(s.snapshot(), "w1", "item1"), 20);
  assert.ok(Number.isNaN(s.balance(s.snapshot(), "unknown", "item1")));
});
test("403 preserves the current session; 401 removes identity and cached business records", async () => {
  await s.loadScreen(19);
  globalThis.fetch = async () => new Response(null, { status: 403 });
  await assert.rejects(s.loadScreen(19));
  assert.ok(s.currentUser());
  globalThis.fetch = async () => new Response(null, { status: 401 });
  await assert.rejects(s.loadScreen(19));
  assert.equal(s.currentUser(), null);
  assert.equal(s.snapshot().rooms.length, 0);
});
test("readAll requests every page and refuses a changed snapshot", async () => {
  const api = await bundle("src/api/modules.ts", "paging");
  let pages = [];
  globalThis.fetch = async (url) => {
    const page = Number(new URL(url).searchParams.get("page"));
    pages.push(page);
    return json({
      items: [entity("r" + page)],
      page,
      pageSize: 1,
      totalCount: 2,
      snapshot: "snap1",
    });
  };
  assert.equal((await api.readAll("rooms", {})).length, 2);
  assert.deepEqual(pages, [1, 2]);
  globalThis.fetch = async (url) => {
    const page = Number(new URL(url).searchParams.get("page"));
    return json({
      items: [entity("r" + page)],
      page,
      pageSize: 1,
      totalCount: 2,
      snapshot: "snap" + page,
    });
  };
  await assert.rejects(api.readAll("rooms", {}), /snapshot changed/);
});
test("all declared workflow operations have distinct dispatch paths and never send a target status", async () => {
  const { actions, domains } = await bundle("src/domain.ts", "workflow-domain");
  const { actionContracts } = await bundle(
    "src/api/endpoints.ts",
    "workflow-contracts",
  );
  const recorded = [];
  globalThis.fetch = async (url, options = {}) => {
    if (options.method === "POST") {
      const data = JSON.parse(options.body);
      recorded.push({ url, data });
      const key = Object.keys(collections).find((key) =>
        url.includes("/" + key + "/"),
      );
      if (url.endsWith("/reset-password"))
        return json({
          record: collections.users[0],
          temporaryPassword: "OneTime!23",
          validForHours: 24,
        });
      const contractKey = Object.keys(actionContracts).find((name) =>
        url.endsWith("/" + actionContracts[name].path),
      );
      return json(collections[contractKey?.split(":")[0] ?? key ?? "tasks"][0]);
    }
    return json(context);
  };
  let count = 0;
  for (const d of domains) {
    for (const action of actions[d.key] ?? []) {
      const contract = actionContracts[d.key + ":" + action.label];
      const record = {
        id: "target",
        name: "Target",
        branchId: "1",
        branch: "Bui Vien",
        departmentId: "2",
        department: "Housekeeping",
        createdAt: context.snapshot,
        version: 0,
        rowVersion: "version",
        status: action.from?.[0] ?? "Active",
        allowedActions: [contract.id],
        assignee: "7",
        creator: "7",
        recipient: "7",
        type:
          action.label === "Post receipt"
            ? "Receipt"
            : action.label === "Post return"
              ? "Return"
              : "Transfer",
        ...(action.label === "Conclude reinspection" ? { rework: "rw1" } : {}),
      };
      const values = {
        reason: "A sufficient reason",
        note: "Result confirmed",
        lines: [{ id: "line1", item: "item1", unit: "unit1", quantity: 1 }],
        results: [],
        receivingDepartment: "2",
        warehouse: "w1",
        room: "201",
      };
      // Fields outside the action schema are discarded at the form/adapter boundary.
      if (!(action.fields ?? []).some((field) => field.key === "lines"))
        delete values.lines;
      await s.perform(d, record, action, values);
      count++;
    }
  }
  assert.equal(count, Object.keys(actionContracts).length);
  assert.equal(recorded.length, count);
  assert.ok(
    recorded.every(
      (call) => !("status" in call.data) && !("statusCode" in call.data),
    ),
  );
  assert.equal(localStorage.data.size, 0);
  assert.equal(JSON.stringify(s.snapshot()).includes("OneTime!23"), false);
});
test("linked-work and preselected-room context is sent to the server", async () => {
  await s.loadScreen(25, undefined, undefined, "?room=201&source=req1");
  const call = calls.find((call) =>
    call.url.includes("/api/ui/screens/25/context"),
  );
  const url = new URL(call.url);
  assert.equal(url.searchParams.get("roomId"), "201");
  assert.equal(url.searchParams.get("sourceRequestId"), "req1");
});
