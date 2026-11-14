// Windows-host only. Real Vite -> API -> trial SQL. No request interception or synthetic identity.
import { chromium, expect } from "@playwright/test";
import { existsSync, writeFileSync } from "node:fs";
import path from "node:path";
import {
  materialRequestDemoInput,
  submitReadyRequest,
  sanitizedHttpFailure,
} from "./dev2-demo-inputs.mjs";
let raw = "";
for await (const chunk of process.stdin) raw += chunk;
const input = JSON.parse(raw);
raw = "";
const receipt = { status: "RUNNING", phase: input.phase, checks: [], ids: {} };
let browser,
  stage = "browser-start";
const file = path.join(input.output, `BROWSER_${input.phase}.json`);
const save = () => writeFileSync(file, JSON.stringify(receipt, null, 2));
const check = (name, value, detail = {}) => {
  receipt.checks.push({ name, pass: !!value, ...detail });
  save();
  if (!value) throw Error("Acceptance check failed");
};
let context, page;
async function login(actor) {
  if (context) await context.close();
  context = await browser.newContext();
  page = await context.newPage();
  await page.goto(input.frontend + "/login");
  await page.locator('[name="username"]').fill(input.users[actor].Username);
  await page.locator('[name="password"]').fill(input.password);
  await page.getByRole("button", { name: /sign in/i }).click();
  await page.waitForURL(actor === 6 ? "**/change-password" : "**/dashboard");
  check(
    "real Cookie login actor " + actor,
    (await context.cookies(input.frontend)).some(
      (x) => x.name.includes("HotelOps") && x.httpOnly && x.secure,
    ),
  );
}
async function open(module, id, action) {
  await page.goto(
    `${input.frontend}/operations/${module}${id ? "/" + id : ""}${action ? "?action=" + encodeURIComponent(action) : ""}`,
  );
  await page.waitForLoadState("networkidle");
  const form = page.locator(`form[data-operation="${action}"]`);
  await expect(form).toBeVisible();
  await expect(form.locator("fieldset").first()).toBeEnabled();
  return form;
}
async function fill(form, values, prefix = "") {
  for (const [key, value] of Object.entries(values)) {
    if (Array.isArray(value)) {
      for (let i = 0; i < value.length; i++) {
        const first = Object.keys(value[i])[0];
        if (
          !(await form
            .locator(`[name="${prefix}${key}.${i}.${first}"]`)
            .count())
        )
          await form
            .getByRole("button", {
              name: `Add ${key.replace(/([a-z])([A-Z])/g, "$1 $2").toLowerCase()} line`,
              exact: true,
            })
            .click();
        await fill(form, value[i], `${prefix}${key}.${i}.`);
      }
    } else {
      const field = form.locator(`[name="${prefix}${key}"]`);
      if (typeof value === "boolean") await field.setChecked(value);
      else if (await field.evaluate((el) => el.tagName === "SELECT"))
        await field.selectOption(String(value));
      else await field.fill(String(value));
    }
  }
}
async function submit(
  module,
  id,
  action,
  values,
  endpoint,
  method = "POST",
  expected = 200,
) {
  stage = action;
  const form = await open(module, id, action);
  await fill(form, values);
  const response = page.waitForResponse(
    (r) =>
      new URL(r.url()).pathname === endpoint && r.request().method() === method,
  );
  await form.getByRole("button", { name: /^Save / }).click();
  const r = await response;
  const body = await r.json().catch(() => undefined);
  check(action + " exact HTTP", r.status() === expected, {
    expectedStatus: expected,
    actualStatus: r.status(),
    endpoint,
    method,
    ...(r.status() !== expected
      ? { validation: sanitizedHttpFailure(r.status(), body) }
      : {}),
  });
  await page.waitForLoadState("networkidle");
  return body;
}
try {
  const edge = "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe";
  browser = await chromium.launch(
    existsSync(edge)
      ? { executablePath: edge, headless: true }
      : { headless: true },
  );
  if (input.phase === "negative") {
    context = await browser.newContext();
    page = await context.newPage();
    await page.goto(input.frontend + "/operations/assets");
    await page.waitForURL("**/login");
    check("anonymous UI redirects to existing login", true);
    const anonymous=await context.request.get(input.frontend+'/api/assets/'+input.assetId);
    check("anonymous business API rejected401",anonymous.status()===401,{expectedStatus:401,actualStatus:anonymous.status()});
    for (const actor of [3, 4]) {
      await login(actor);
      await submit(
        "assets",
        input.assetId,
        "A004A-016",
        { areaId: input.areaId },
        `/api/assets/${input.assetId}/location`,
        "POST",
        403,
      );
      await expect(page.getByRole("alert")).toContainText("Access denied");
      check(
        actor === 3 ? "no-grant UI denied" : "wrong-branch UI denied",
        true,
      );
    }
    await login(0);
    const csrfDenied = await context.request.post(
      input.frontend + `/api/assets/${input.assetId}/location`,
      { data: { actorUserId: input.users[0].UserId, areaId: input.areaId } },
    );
    check("real missing-CSRF rejected400", csrfDenied.status() === 400);
    await login(6);
    await expect(
      page.getByRole("heading", { name: "Change Password", exact: true }),
    ).toBeVisible();
    const session = await (
      await context.request.get(input.frontend + "/api/auth/session")
    ).json();
    const restricted = await context.request.post(
      input.frontend + `/api/assets/${input.assetId}/location`,
      {
        headers: { "X-CSRF-TOKEN": session.csrfToken },
        data: { actorUserId: input.users[6].UserId, areaId: input.areaId },
      },
    );
    check("restricted real session rejected403", restricted.status() === 403);
  } else {
    await login(0);
    const incident = await submit(
      "incidents",
      null,
      "A004A-021",
      {
        branchId: input.branchId,
        areaId: input.areaId,
        incidentTitle: input.marker + " Browser incident",
        description: "Browser integration incident",
        discoveredAt: new Date(Date.now() - 60000).toISOString().slice(0, 16),
      },
      "/api/incidents",
      "POST",
      201,
    );
    receipt.ids.incidentId = incident.incidentId;
    save();
    const work = await submit(
      "maintenance",
      incident.incidentId,
      "A004A-024",
      {
        title: input.marker + " Browser work",
        departmentId: input.departmentId,
      },
      `/api/incidents/${incident.incidentId}/maintenance-works`,
      "POST",
      201,
    );
    receipt.ids.workId = work.maintenanceWorkId;
    save();
    await submit(
      "maintenance",
      work.maintenanceWorkId,
      "A004A-025",
      {
        assigneeId: input.users[0].UserId,
        instruction: "Repair using issued material",
      },
      `/api/maintenance-works/${work.maintenanceWorkId}/assignment`,
      "PATCH",
    );
    await submit(
      "maintenance",
      work.maintenanceWorkId,
      "A004A-026",
      {},
      `/api/maintenance-works/${work.maintenanceWorkId}/start`,
    );
    const request = await submit(
      "material-requests",
      null,
      "A004A-043",
      materialRequestDemoInput(input, incident.incidentId),
      "/api/material-requests",
      "POST",
      201,
    );
    receipt.ids.requestId = request.requestId;
    save();
    check(
      "A004A-043 persisted submit prerequisites before A045",
      submitReadyRequest(request, input.warehouseId),
    );
    await submit(
      "material-requests",
      request.requestId,
      "A004A-045",
      { assignedApproverUserId: input.users[1].UserId },
      `/api/material-requests/${request.requestId}/submit`,
    );
    await login(1);
    await submit(
      "material-requests",
      request.requestId,
      "A004A-047",
      { lines: [{ approvedQty: 10 }] },
      `/api/material-requests/${request.requestId}/approve`,
    );
    await login(2);
    const fulfillment = await submit(
      "material-requests",
      request.requestId,
      "A004A-051",
      {
        mode: "Partial",
        documentDate: new Date().toISOString().slice(0, 10),
        lines: [{ quantity: 2 }],
      },
      `/api/material-requests/${request.requestId}/fulfillments`,
      "POST",
      201,
    );
    receipt.ids.fulfillmentId = fulfillment.fulfillmentId;
    save();
    await login(0);
    const availability = page.waitForResponse(
      (r) =>
        new URL(r.url()).pathname ===
        `/api/maintenance-works/${work.maintenanceWorkId}/material-availability`,
    );
    await open("maintenance", work.maintenanceWorkId, "A004A-034");
    const available = await availability;
    const materials = await available.json();
    check(
      "SQL-backed material availability HTTP200",
      available.status() === 200,
    );
    check(
      "issued material linked to new workflow",
      materials.items.some(
        (x) =>
          x.requestId === request.requestId &&
          x.fulfillmentId === fulfillment.fulfillmentId &&
          x.remainingBaseQty === 2,
      ),
    );
    await submit(
      "assets",
      input.assetId,
      "A004A-016",
      { areaId: input.areaId },
      `/api/assets/${input.assetId}/location`,
    );
    const repl = await submit(
      "replenishments",
      null,
      "A004A-056",
      {
        warehouseId: input.warehouseId,
        lines: [
          {
            itemId: input.item56,
            sourceType: "LOW_STOCK",
            sourceThresholdId: input.thresholdId,
            requestedBaseQty: 10,
          },
        ],
      },
      "/api/replenishments",
      "POST",
      201,
    );
    receipt.ids.replenishmentId = repl.replenishmentId;
    save();
    await login(2);
    const draft = await submit(
      "inventory",
      null,
      "inventory:CreateReceipt",
      {
        destinationWarehouseId: input.warehouseId,
        documentDate: new Date().toISOString().slice(0, 10),
        lines: [{ itemId: input.itemId, uomId: input.uomId, expectedQty: 3 }],
      },
      "/api/inventory/receipts",
      "POST",
      201,
    );
    receipt.ids.documentId = draft.documentId;
    save();
    await submit(
      "inventory",
      draft.documentId,
      "inventory:PostReceipt",
      { lines: [{ acceptedQty: 3 }] },
      `/api/inventory/receipts/${draft.documentId}/post`,
    );
    await open("inventory", null, "inventory:Movements");
    await expect(page.getByRole("table")).toBeVisible();
    check("real stock movement UI", true);
  }
  receipt.status = "PASS";
  save();
} catch (error) {
  receipt.status = "FAIL";
  receipt.failedStage = stage;
  receipt.exceptionType = error?.name ?? "Error";
  receipt.rawErrorsRecorded = false;
  save();
  process.exitCode = 1;
} finally {
  input.password = "";
  if (browser) await browser.close();
}
