import { test } from "node:test";
import assert from "node:assert/strict";
import { build } from "esbuild";
import { room, board, detail, history } from "./room-status-fixtures.mjs";
await build({
  entryPoints: ["src/api/dev1/roomBoard.ts"],
  bundle: true,
  platform: "node",
  format: "esm",
  outfile: ".test-output/fe03/board.mjs",
  define: { "import.meta.env": "{}" },
});
const { listRoomBoard, readRoomDetail, readRoomHistory, roomBoardError } =
  await import("../.test-output/fe03/board.mjs");
const query = () => new URLSearchParams({ branchId: "7" });
test("scoped filters, cookies, GET and cancellation; untrusted extra params omitted", async () => {
  const abort = new AbortController();
  globalThis.fetch = async (url, o) => {
    const q = new URL(url, "https://localhost").searchParams;
    for (const [k, v] of Object.entries({
      branchId: "7",
      floorId: "2",
      areaId: "3",
      roomTypeId: "4",
      operationalStatusCode: "DIRTY",
      search: "A",
    }))
      assert.equal(q.get(k), v);
    assert.equal(q.has("allBranches"), false);
    assert.equal(q.has("actorId"), false);
    assert.equal(o.method, "GET");
    assert.equal(o.credentials, "include");
    assert.equal(o.signal, abort.signal);
    return Response.json(board);
  };
  assert.equal(
    (
      await listRoomBoard(
        new URLSearchParams({
          branchId: "7",
          floorId: "2",
          areaId: "3",
          roomTypeId: "4",
          status: "DIRTY",
          search: " A ",
          allBranches: "true",
          actorId: "9",
        }),
        abort.signal,
      )
    ).totalCount,
    21,
  );
});
test("invalid filters fail before network", async () => {
  let calls = 0;
  globalThis.fetch = () => {
    calls++;
    throw Error("network");
  };
  for (const v of [
    { branchId: "0" },
    { branchId: "9007199254740992" },
    { floorId: "-1" },
    { areaId: "1x" },
    { roomTypeId: "0" },
    { page: "0" },
    { page: "2147483647" },
    { status: "BLOCKED" },
    { search: "x".repeat(201) },
  ])
    await assert.rejects(
      listRoomBoard(new URLSearchParams({ branchId: "7", ...v })),
    );
  assert.equal(calls, 0);
});
test("rejects cross-branch data, wrong page, missing or inconsistent counters", async () => {
  for (const bad of [
    { ...board, items: [{ ...room, branchId: 8 }] },
    { ...board, page: 2 },
    { ...board, statusCounts: null },
    { ...board, statusCounts: [{ operationalStatusCode: "DIRTY", count: -1 }] },
    { ...board, statusCounts: [{ operationalStatusCode: "DIRTY", count: 1 }] },
    {
      ...board,
      totalCount: 42,
      statusCounts: [...board.statusCounts, ...board.statusCounts],
    },
  ]) {
    globalThis.fetch = async () => Response.json(bad);
    await assert.rejects(listRoomBoard(query()));
  }
});
test("detail binds room/branch and preserves status separately from block records", async () => {
  globalThis.fetch = async (url) => {
    assert.equal(url, "/api/rooms/11?branchId=7");
    return Response.json(detail);
  };
  const data = await readRoomDetail("7", "11");
  assert.equal(data.room.operationalStatusCode, "DIRTY");
  assert.equal(data.unreleasedBlocks[0].reason, "Repair door");
  assert.equal(Object.hasOwn(data, "effectiveReadiness"), false);
  for (const bad of [
    { ...detail, room: { ...room, id: 99 } },
    { ...detail, room: { ...room, branchId: 8 } },
    {
      ...detail,
      unreleasedBlocks: [
        { ...detail.unreleasedBlocks[0], blockedAtUtc: "2026-11-09" },
      ],
    },
    {
      ...detail,
      currentStatusEvidence: {
        ...detail.currentStatusEvidence,
        source: { type: "INSPECTION", id: 9, availability: "UNRESOLVED" },
      },
    },
  ]) {
    globalThis.fetch = async () => Response.json(bad);
    await assert.rejects(readRoomDetail("7", "11"));
  }
});
test("history validates page, recorded-only coverage and event timestamps", async () => {
  globalThis.fetch = async (url) => {
    assert.equal(url, "/api/rooms/11/history?branchId=7&page=1&pageSize=20");
    return Response.json(history);
  };
  assert.equal(
    (await readRoomHistory("7", "11", 1)).items[0].toStatusCode,
    "DIRTY",
  );
  for (const bad of [
    { ...history, page: 2 },
    { ...history, coverage: "COMPLETE" },
    { ...history, items: [{ ...history.items[0], changedAtUtc: "bad" }] },
  ]) {
    globalThis.fetch = async () => Response.json(bad);
    await assert.rejects(readRoomHistory("7", "11", 1));
  }
});
test("401/403/409/400/503 propagate without fallback and have user messages", async () => {
  for (const status of [401, 403, 409, 400, 503]) {
    let calls = 0;
    globalThis.fetch = async () => {
      calls++;
      return Response.json({ title: "DENIED" }, { status });
    };
    await assert.rejects(listRoomBoard(query()), (e) => {
      assert.equal(e.status, status);
      assert.ok(roomBoardError(e));
      return true;
    });
    assert.equal(calls, 1);
  }
});

test("rejects records contradicting filters, counters or remaining page size", async () => {
  for (const [params, data] of [
    [{ floorId: "9" }, board],
    [{ areaId: "9" }, board],
    [{ roomTypeId: "9" }, board],
    [{ status: "READY" }, board],
    [{}, { ...board, totalCount: 0, statusCounts: [] }],
    [{}, { ...board, items: [room, room] }],
    [
      {},
      {
        ...board,
        statusCounts: [{ operationalStatusCode: "READY", count: 21 }],
      },
    ],
    [
      { page: "2" },
      {
        ...board,
        page: 2,
        totalCount: 20,
        statusCounts: [{ operationalStatusCode: "DIRTY", count: 20 }],
      },
    ],
  ]) {
    globalThis.fetch = async () => Response.json(data);
    await assert.rejects(
      listRoomBoard(new URLSearchParams({ branchId: "7", ...params })),
      /INVALID_ROOM_RESPONSE/,
    );
  }
});

test("history rejects duplicate or impossible page records", async () => {
  for (const bad of [
    { ...history, totalCount: 0 },
    { ...history, totalCount: 2, items: [history.items[0], history.items[0]] },
    { ...history, page: 2, totalCount: 20 },
  ]) {
    globalThis.fetch = async () => Response.json(bad);
    await assert.rejects(
      readRoomHistory("7", "11", bad.page),
      /INVALID_ROOM_RESPONSE/,
    );
  }
});

test("detail rejects impossible UTC dates, duplicate blocks and contradictory evidence", async () => {
  for (const bad of [
    {
      ...detail,
      unreleasedBlocks: [
        { ...detail.unreleasedBlocks[0], blockedAtUtc: "2026-02-30T00:00:00Z" },
      ],
    },
    {
      ...detail,
      unreleasedBlocks: [{ ...detail.unreleasedBlocks[0], blockedAtUtc: "1Z" }],
    },
    {
      ...detail,
      unreleasedBlocks: [
        detail.unreleasedBlocks[0],
        detail.unreleasedBlocks[0],
      ],
    },
    {
      ...detail,
      currentStatusEvidence: {
        ...detail.currentStatusEvidence,
        availability: "NOT_RECORDED",
      },
    },
    {
      ...detail,
      currentStatusEvidence: {
        ...detail.currentStatusEvidence,
        recordedAtUtc: null,
      },
    },
    {
      ...detail,
      currentStatusEvidence: {
        ...detail.currentStatusEvidence,
        source: { type: "INCIDENT", id: 9, availability: "VERIFIED_SAME_ROOM" },
      },
    },
    {
      ...detail,
      currentStatusEvidence: {
        ...detail.currentStatusEvidence,
        source: {
          type: "INSPECTION",
          id: null,
          availability: "VERIFIED_SAME_ROOM",
        },
      },
    },
  ]) {
    globalThis.fetch = async () => Response.json(bad);
    await assert.rejects(readRoomDetail("7", "11"), /INVALID_ROOM_RESPONSE/);
  }
});

test("empty out-of-range history pages and unavailable evidence remain valid", async () => {
  globalThis.fetch = async () =>
    Response.json({ ...history, page: 2, items: [] });
  assert.deepEqual((await readRoomHistory("7", "11", 2)).items, []);
  for (const availability of ["NOT_RECORDED", "OUT_OF_SYNC"]) {
    globalThis.fetch = async () =>
      Response.json({
        ...detail,
        currentStatusEvidence: {
          availability,
          reason: null,
          recordedAtUtc: null,
          source: null,
        },
      });
    assert.equal(
      (await readRoomDetail("7", "11")).currentStatusEvidence.availability,
      availability,
    );
  }
});

test("history accepts persisted actor or missing attribution and rejects invented identity", async () => {
  for (const changedByUserId of [8, null]) {
    globalThis.fetch = async () =>
      Response.json({
        ...history,
        items: [{ ...history.items[0], changedByUserId }],
      });
    assert.equal(
      (await readRoomHistory("7", "11", 1)).items[0].changedByUserId,
      changedByUserId,
    );
  }
  for (const changedByUserId of [undefined, 0, -1, "8", 9007199254740992]) {
    globalThis.fetch = async () =>
      Response.json({
        ...history,
        items: [{ ...history.items[0], changedByUserId }],
      });
    await assert.rejects(
      readRoomHistory("7", "11", 1),
      /INVALID_ROOM_RESPONSE/,
    );
  }
});
