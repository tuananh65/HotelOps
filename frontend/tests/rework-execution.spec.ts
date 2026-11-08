import { test, expect, type Page } from "@playwright/test";
const tag = '"0000000000000001"',
  nextTag = '"0000000000000002"';
const initial = {
  id: 3,
  branchId: 2,
  cleaningTaskId: 4,
  inspectionId: 5,
  roomId: 6,
  assigneeId: 7,
  state: "PENDING_ACCEPTANCE",
  dueAtUtc: null,
  sourceETag: tag,
  inspectionETag: tag,
  capability: "PROVISIONAL_NOT_ACCEPTED",
  sourceAuthority: "SYNTHETIC_TEST_ONLY",
  defects: [
    {
      id: 8,
      description: "Synthetic mirror defect",
      remediation: null,
      inspectionAttachmentIds: [9],
      remediationAttachmentIds: [],
    },
  ],
};
const open = (page: Page) => page.goto("/rework/3?branchId=2");
test("rework list reuses scoped Monitor and navigates to the server execution panel", async ({
  page,
}) => {
  const item = {
    kind: "rework",
    id: 3,
    cleaningTaskId: 4,
    taskNumber: "SERVER-REWORK",
    roomId: 6,
    roomNumber: "101",
    floorId: 1,
    floorName: null,
    recordedStatus: "IN_PROGRESS",
    workflowState: "PENDING_REINSPECTION",
    priorityId: null,
    priorityCode: null,
    assigneeId: 7,
    assigneeName: "Trial staff",
    dueAtUtc: null,
    isOverdue: null,
    progressPercent: 100,
    blockerCode: null,
    blockerNote: null,
    description: "Synthetic round",
    isCurrentCycle: true,
    hasUnresolvedBlocker: false,
  };
  await page.route("**/api/branches/2/cleaning-monitor?**", (r) => {
    expect(new URL(r.request().url()).searchParams.get("kind")).toBe("rework");
    return r.fulfill({
      json: {
        items: [item],
        total: 1,
        page: 1,
        pageSize: 20,
        fromDate: "2026-11-07",
        toDate: "2026-11-07",
        timezone: "Asia/Ho_Chi_Minh",
        asOfUtc: "2026-11-07T00:00:00Z",
      },
    });
  });
  await page.route("**/api/branches/2/cleaning-monitor/rework/3?**", (r) =>
    r.fulfill({
      json: {
        item,
        assignments: [],
        assignmentCount: 0,
        progress: [],
        progressCount: 0,
        audit: [],
        auditCount: 0,
        timelineLimit: 20,
        timelinePage: 1,
        asOfUtc: "2026-11-07T00:00:00Z",
      },
    }),
  );
  await page.route("**/api/branches/2/reworks/3/execution", (r) =>
    r.fulfill({
      headers: { ETag: tag },
      json: { ...initial, state: "PENDING_REINSPECTION" },
    }),
  );
  await page.goto("/rework?branchId=2&kind=cleaning");
  await page.getByRole("link", { name: "SERVER-REWORK", exact: true }).click();
  await expect(page.getByText("100%", { exact: true })).toBeVisible();
  await page
    .getByRole("link", { name: "Mở thực hiện rework", exact: true })
    .click();
  await expect(page.getByText(/Đã gửi rework; chờ supervisor/)).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Complete rework", exact: true }),
  ).toHaveCount(0);
});
async function csrf(page: Page) {
  await page.route("**/api/auth/csrf", (r) =>
    r.fulfill({ json: { headerName: "X-CSRF-TOKEN", csrfToken: "token" } }),
  );
}
function receipt(action: string, key: string) {
  return {
    id: 3,
    branchId: 2,
    cleaningTaskId: 4,
    inspectionId: 5,
    assigneeId: 7,
    action,
    state:
      action === "accept"
        ? "ACCEPTED"
        : action === "start"
          ? "IN_PROGRESS"
          : "PENDING_REINSPECTION",
    operationId: key,
    sourceETag: nextTag,
    occurredAtUtc: "2026-11-07T00:00:00Z",
    intervalId: action === "accept" ? null : 10,
    publicationId:
      action === "complete" ? "11111111-1111-4111-8111-111111111111" : null,
    readiness: action === "complete" ? "PENDING_INSPECTION" : "REWORK_REQUIRED",
    notification: action === "complete" ? "PENDING_DELIVERY" : "NOT_REQUIRED",
    contract: "PROVISIONAL_NOT_ACCEPTED",
  };
}
test("rework reads authorized source and accepts before enabling start", async ({
  page,
}) => {
  let state = "PENDING_ACCEPTANCE",
    version = tag;
  let posts = 0;
  await csrf(page);
  await page.route("**/api/branches/2/reworks/3/execution", (r) =>
    r.fulfill({
      headers: { ETag: version },
      json: { ...initial, state, sourceETag: version },
    }),
  );
  await page.route("**/api/branches/2/reworks/3/accept", (r) => {
    posts++;
    expect(r.request().headers()["if-match"]).toBe(tag);
    expect(r.request().headers()["x-csrf-token"]).toBe("token");
    state = "ACCEPTED";
    version = nextTag;
    return r.fulfill({
      headers: { ETag: nextTag },
      json: receipt("accept", r.request().headers()["idempotency-key"]),
    });
  });
  await open(page);
  await expect(
    page.getByText("Synthetic mirror defect", { exact: true }),
  ).toBeVisible();
  await expect(page.getByRole("link", { name: /Ảnh \/ tệp/ })).toHaveAttribute(
    "href",
    "/api/branches/2/reworks/3/evidence/9",
  );
  await expect(
    page.getByRole("button", { name: "Start rework", exact: true }),
  ).toHaveCount(0);
  await page
    .getByLabel("Ghi chú thao tác", { exact: true })
    .fill("Accept synthetic rework");
  await page
    .getByRole("button", { name: "Accept rework", exact: true })
    .click();
  await expect(
    page.getByRole("button", { name: "Start rework", exact: true }),
  ).toBeVisible();
  expect(posts).toBe(1);
});
test("rework completion requires every note and retains same key/body for explicit retry", async ({
  page,
}) => {
  let submitted = false;
  const commands: { key: string; body: string }[] = [];
  await csrf(page);
  await page.route("**/api/branches/2/reworks/3/execution", (r) =>
    r.fulfill({
      headers: { ETag: submitted ? nextTag : tag },
      json: {
        ...initial,
        state: submitted ? "PENDING_REINSPECTION" : "IN_PROGRESS",
        sourceETag: submitted ? nextTag : tag,
      },
    }),
  );
  await page.route("**/api/branches/2/reworks/3/complete", (r) => {
    commands.push({
      key: r.request().headers()["idempotency-key"],
      body: r.request().postData()!,
    });
    if (commands.length === 1)
      return r.fulfill({ status: 503, json: { title: "UNAVAILABLE" } });
    submitted = true;
    return r.fulfill({
      headers: { ETag: nextTag },
      json: receipt("complete", commands[1].key),
    });
  });
  await open(page);
  await page
    .getByLabel("Ghi chú thao tác", { exact: true })
    .fill("Submit rework");
  await page
    .getByRole("button", { name: "Complete rework", exact: true })
    .click();
  expect(commands).toHaveLength(0);
  await page
    .getByLabel("Remediation 8", { exact: true })
    .fill("Mirror polished");
  await page
    .getByRole("button", { name: "Complete rework", exact: true })
    .click();
  await expect(
    page.getByRole("button", { name: "Thử lại cùng yêu cầu", exact: true }),
  ).toBeVisible();
  expect(commands).toHaveLength(1);
  await page
    .getByRole("button", { name: "Thử lại cùng yêu cầu", exact: true })
    .click();
  await expect(page.getByText(/Đã gửi rework; chờ supervisor/)).toBeVisible();
  expect(commands).toHaveLength(2);
  expect(commands[0]).toEqual(commands[1]);
});
test("stale rework reloads source and does not repost", async ({ page }) => {
  let reads = 0,
    posts = 0;
  await csrf(page);
  await page.route("**/api/branches/2/reworks/3/execution", (r) => {
    reads++;
    return r.fulfill({
      headers: { ETag: posts ? nextTag : tag },
      json: {
        ...initial,
        state: "IN_PROGRESS",
        sourceETag: posts ? nextTag : tag,
      },
    });
  });
  await page.route("**/api/branches/2/reworks/3/complete", (r) => {
    posts++;
    return r.fulfill({ status: 412, json: { title: "STALE" } });
  });
  await open(page);
  await page.getByLabel("Remediation 8", { exact: true }).fill("Fixed");
  await page.getByLabel("Ghi chú thao tác", { exact: true }).fill("Submit");
  const initialReads = reads;
  await page
    .getByRole("button", { name: "Complete rework", exact: true })
    .click();
  await expect(page.getByText(/Dữ liệu đã thay đổi/)).toBeVisible();
  await expect(page.getByLabel("Remediation 8", { exact: true })).toHaveValue(
    "",
  );
  expect(reads).toBeGreaterThan(initialReads);
  expect(posts).toBe(1);
});
for (const status of [401, 403, 404, 503])
  test(`rework ${status} does not expose prototype or mutation`, async ({
    page,
  }) => {
    await page.route("**/api/branches/2/reworks/3/execution", (r) =>
      r.fulfill({ status, json: { title: "DENIED" } }),
    );
    await open(page);
    await expect(page.getByRole("alert")).toBeVisible();
    await expect(
      page.getByRole("button", {
        name: /Accept rework|Start rework|Complete rework/,
      }),
    ).toHaveCount(0);
  });
