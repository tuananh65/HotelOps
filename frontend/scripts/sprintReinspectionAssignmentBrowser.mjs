import { chromium } from "@playwright/test";
let input = "";
for await (const chunk of process.stdin) input += chunk;
const fixture = JSON.parse(input); input = "";
let browser;
let stage = "login admission";
try {
  browser = await chromium.launch({ executablePath: "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe", headless: true });
  const page = await browser.newPage(); page.setDefaultTimeout(30000);
  const origin = "https://localhost:7143";
  const destination = `/inspections/reworks/${fixture.rework}/assign?branchId=${fixture.branch}`;
  const api = `/api/branches/${fixture.branch}/reworks/${fixture.rework}`;
  await page.goto(origin + destination); await page.waitForURL("**/login?*");
  await page.getByLabel("Username or email").fill(fixture.username);
  await page.getByLabel("Password", { exact: true }).fill(fixture.password); fixture.password = "";
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  stage = "return to assignment";
  await page.waitForURL(origin + destination);
  stage = "assignment options";
  await page.getByRole("heading", { name: "Assign reinspection", exact: true }).waitFor();
  stage = "options API admission";
  const admission = await page.evaluate(async api => {
    const response = await fetch(api + "/reinspection-options", { credentials: "include" });
    const body = await response.json();
    return { status: response.status, title: body.title, canAssign: body.canAssign,
      reason: body.unavailableReason, inspectors: body.inspectors?.length };
  }, api);
  if (admission.status !== 200 || !admission.canAssign) throw Error("Assignment options: " + JSON.stringify(admission));
  stage = "inspector form";
  await page.getByLabel("Inspector", { exact: true }).waitFor();
  const security = await page.evaluate(async ({ api, actor, branch, rework }) => {
    const detailResponse = await fetch(api + "/reinspection-options", { credentials: "include" });
    const detail = await detailResponse.json();
    if (detailResponse.status !== 200 || detail.reworkETag !== detailResponse.headers.get("ETag") || !detail.canAssign
      || !detail.inspectors.some(p => p.id === actor)) throw Error("Assignment options not authorized");
    const csrf = await (await fetch("/api/auth/csrf", { credentials: "include" })).json();
    const command = { inspectorId: actor, dueAt: new Date(Date.now() + 3600000).toISOString(), reason: "Synthetic browser assignment" };
    const headers = { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf.csrfToken,
      "If-Match": detail.reworkETag, "Idempotency-Key": crypto.randomUUID() };
    const post = async (h, body = command) => (await fetch(api + "/reinspection", { method: "POST", credentials: "include", headers: h, body: JSON.stringify(body) })).status;
    const noCsrf = { ...headers }; delete noCsrf["X-CSRF-TOKEN"];
    const noTag = { ...headers }; delete noTag["If-Match"];
    const statuses = [await post(noCsrf), await post(noTag), await post({ ...headers, "If-Match": '"FFFFFFFFFFFFFFFF"' }),
      await post(headers, { ...command, inspectorId: actor + 100000 }), await post(headers, { ...command, dueAt: "2000-01-01T00:00:00Z" })];
    const foreign = await fetch(`/api/branches/${branch + 100000}/reworks/${rework}/reinspection-options`, { credentials: "include" });
    return { statuses, foreign: foreign.status };
  }, { api, actor: fixture.actor, branch: fixture.branch, rework: fixture.rework });
  stage = "canonical assignment form";
  if (JSON.stringify(security.statuses) !== JSON.stringify([400, 428, 412, 400, 400]) || ![403, 404].includes(security.foreign))
    throw Error("Assignment authorization/preconditions: " + JSON.stringify(security));
  await page.getByLabel("Inspector", { exact: true }).selectOption(String(fixture.actor));
  const due = new Date(Date.now() + 3600000);
  const local = `${due.getFullYear()}-${String(due.getMonth() + 1).padStart(2, "0")}-${String(due.getDate()).padStart(2, "0")}T${String(due.getHours()).padStart(2, "0")}:${String(due.getMinutes()).padStart(2, "0")}`;
  await page.getByLabel("Due time", { exact: true }).fill(local);
  await page.getByLabel("Assignment reason", { exact: true }).fill("Reinspect submitted remediation");
  await page.getByRole("button", { name: "Review assignment", exact: true }).click();
  const pending = page.waitForResponse(response => response.url().endsWith(api + "/reinspection") && response.request().method() === "POST");
  await page.getByRole("button", { name: "Confirm assignment", exact: true }).click();
  const response = await pending, body = await response.json();
  if (response.status() !== 201 || body.contract !== "FE05_REINSPECTION_ASSIGNMENT_V1" || body.state !== "ASSIGNED")
    throw Error("Canonical assignment failed: " + response.status());
  await page.getByText(`Inspection assigned successfully. Inspection #${body.id}.`, { exact: true }).waitFor();
  const headers = await response.request().allHeaders(), requestBody = response.request().postData();
  const replay = await page.evaluate(async ({ api, headers, requestBody }) => {
    const send = async value => { const result = await fetch(api + "/reinspection", { method: "POST", credentials: "include",
      headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": headers["x-csrf-token"], "If-Match": headers["if-match"], "Idempotency-Key": headers["idempotency-key"] }, body: value });
      return { status: result.status, body: await result.json() }; };
    return { same: await send(requestBody), changed: await send(JSON.stringify({ ...JSON.parse(requestBody), reason: "Changed replay" })) };
  }, { api, headers, requestBody });
  if (replay.same.status !== 201 || replay.same.body.id !== body.id || replay.same.body.sourceETag !== body.sourceETag || replay.changed.status !== 409)
    throw Error("Assignment replay mismatch");
  await page.reload();
  stage = "assignment reload";
  await page.getByText(`An inspection is already assigned: #${body.id}.`, { exact: true }).waitFor();
  if (await page.getByRole("button", { name: "Review assignment", exact: true }).count()) throw Error("Duplicate assignment remains editable");
  stage = "assigned reinspection detail and evidence";
  const detailCheck = await page.evaluate(async ({ branch, inspection, actor, rework }) => {
    const path = `/api/branches/${branch}/reinspections/${inspection}`;
    const response = await fetch(path + "/execution", { credentials: "include" });
    const d = await response.json();
    if (response.status !== 200 || d.id !== inspection || d.reworkId !== rework || d.inspectorId !== actor
      || d.state !== "ASSIGNED" || d.sourceETag !== response.headers.get("ETag") || !d.defects?.length
      || d.defects.some(x => !x.remediation?.trim() || x.fixed !== null)) throw Error("Reinspection detail rejected: " + response.status);
    const photo = d.defects.flatMap(x => [...x.originalPhotos, ...x.remediationPhotos])[0];
    const image = await fetch(path + "/evidence/" + photo, { credentials: "include" });
    if (image.status !== 200 || image.headers.get("X-Content-Type-Options") !== "nosniff" || !(await image.arrayBuffer()).byteLength)
      throw Error("Scoped reinspection evidence rejected");
    const foreign = await fetch(`/api/branches/${branch + 100000}/reinspections/${inspection}/execution`, { credentials: "include" });
    const unrelated = await fetch(path + "/evidence/9223372036854775807", { credentials: "include" });
    if (![403, 404].includes(foreign.status) || unrelated.status !== 404) throw Error("Reinspection read scope leaked");
    const csrf = await (await fetch("/api/auth/csrf", { credentials: "include" })).json();
    const generic = await fetch(`/api/branches/${branch}/inspections/${inspection}/start`, { method: "POST", credentials: "include",
      headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf.csrfToken, "If-Match": d.sourceETag },
      body: JSON.stringify({ reason: "Must use exact-defect workflow" }) });
    const after = await fetch(path + "/execution", { credentials: "include" });
    if (generic.status !== 503 || after.status !== 200 || after.headers.get("ETag") !== d.sourceETag)
      throw Error("Generic inspection endpoint changed reinspection");
    return { detail: true, evidence: true, foreignDenied: true, readiness: d.effectiveReadiness };
  }, { branch: fixture.branch, inspection: body.id, actor: fixture.actor, rework: fixture.rework });
  let conclusion = null;
  if (fixture.conclude) {
    stage = "canonical conclusion admission";
    await page.getByRole("link", { name: "Open reinspection", exact: true }).click();
    await page.waitForURL(`${origin}/reinspections/${fixture.branch}/${body.id}/execution`);
    await page.getByRole("heading", { name: "Reinspect room", exact: true }).waitFor();
    const path = `/api/branches/${fixture.branch}/reinspections/${body.id}`;
    const negative = await page.evaluate(async ({ path, outcome }) => {
      const response = await fetch(path + "/execution", { credentials: "include" }), d = await response.json();
      const csrf = await (await fetch("/api/auth/csrf", { credentials: "include" })).json();
      const command = { reworkETag: d.reworkETag, bindingETag: d.bindingETag, reason: "Synthetic conclusion validation",
        defects: d.defects.map(x => ({ defectId: x.id, fixed: outcome === "PASS", note: "Checked", photos: [] })) };
      const headers = { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf.csrfToken, "If-Match": d.sourceETag, "Idempotency-Key": crypto.randomUUID() };
      const send = async (h, body = command) => (await fetch(path + "/conclude", { method: "POST", credentials: "include", headers: h, body: JSON.stringify(body) })).status;
      const noCsrf = { ...headers }; delete noCsrf["X-CSRF-TOKEN"];
      const noTag = { ...headers }; delete noTag["If-Match"];
      const statuses = [await send(noCsrf), await send(noTag), await send({ ...headers, "If-Match": '"FFFFFFFFFFFFFFFF"' }),
        await send(headers, { ...command, bindingETag: '"FFFFFFFFFFFFFFFF"' }), await send(headers, { ...command, reworkETag: '"FFFFFFFFFFFFFFFF"' }),
        await send(headers, { ...command, defects: [] }), await send(headers, { ...command, defects: [{ defectId: 2147483647, fixed: true }] })];
      const after = await fetch(path + "/execution", { credentials: "include" });
      if (after.status !== 200 || after.headers.get("ETag") !== d.sourceETag) throw Error("Rejected HTTP conclusion mutated source");
      return { statuses, d };
    }, { path, outcome: fixture.outcome });
    if (JSON.stringify(negative.statuses) !== JSON.stringify([400, 428, 412, 412, 412, 400, 400]))
      throw Error("Conclusion security/preconditions: " + JSON.stringify(negative.statuses));
    const original = page.getByRole("link", { name: `Original inspection #${negative.d.originalInspectionId}`, exact: true });
    await original.waitFor();
    await page.getByRole("button", { name: "Review conclusion", exact: true }).click();
    if (await page.getByRole("button", { name: "Confirm conclusion", exact: true }).count()) throw Error("Empty decisions passed form validation");
    for (const defect of negative.d.defects) {
      await page.getByLabel(`Result for defect #${defect.id}`, { exact: true }).selectOption(fixture.outcome === "PASS" ? "FIXED" : "NOT_FIXED");
      await page.getByLabel(`Note for defect #${defect.id}`, { exact: true }).fill("Checked synthetic remediation");
    }
    await page.getByLabel(`Photos for defect #${negative.d.defects[0].id}`, { exact: true }).setInputFiles({ name: "synthetic-reinspection.png", mimeType: "image/png",
      buffer: Buffer.from("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aS1cAAAAASUVORK5CYII=", "base64") });
    await page.getByLabel("Conclusion reason", { exact: true }).fill("Synthetic browser conclusion");
    await page.getByRole("button", { name: "Review conclusion", exact: true }).click();
    stage = "canonical HTTPS conclusion";
    const pendingConclusion = page.waitForResponse(r => r.url().endsWith(path + "/conclude") && r.request().method() === "POST");
    await page.getByRole("button", { name: "Confirm conclusion", exact: true }).click();
    const concludedResponse = await pendingConclusion, concluded = await concludedResponse.json();
    if (concludedResponse.status() !== 200 || concluded.state !== (fixture.outcome === "PASS" ? "PASSED" : "FAILED")
      || concluded.contract !== "FE05_REINSPECTION_CONCLUSION_V1" || concluded.effectiveReadiness !== (detailCheck.readiness === "BLOCKED" ? "BLOCKED" : fixture.outcome === "PASS" ? "READY" : "REWORK_REQUIRED"))
      throw Error("HTTPS conclusion rejected: " + concludedResponse.status() + " " + JSON.stringify(concluded));
    await page.getByText(fixture.outcome === "PASS" ? "Reinspection Passed. Rework Completed." : "Reinspection Failed. Rework Failed.", { exact: true }).waitFor();
    if (detailCheck.readiness === "BLOCKED") await page.getByText("The room remains blocked (MSG17).", { exact: true }).waitFor();
    if (fixture.outcome === "FAIL") {
      stage = "next-round Request Rework admission";
      await page.getByRole("heading", { name: "Request cleaning rework", exact: true }).waitFor();
      await page.getByRole("group", { name: "Defects, assignee and due time", exact: true }).waitFor();
      if (await page.getByRole("checkbox").count() !== negative.d.defects.length) throw Error("Remaining defects not offered for next rework");
      for (const checkbox of await page.getByRole("checkbox").all()) if (!await checkbox.isChecked()) throw Error("Remaining defect not preselected");
    }
    if (await page.getByRole("button", { name: "Confirm conclusion", exact: true }).count()) throw Error("Duplicate conclusion form remains");
    const sentHeaders = await concludedResponse.request().allHeaders(), sentBody = concludedResponse.request().postData();
    const replay = await page.evaluate(async ({ path, headers, body }) => {
      const post = async b => { const r = await fetch(path + "/conclude", { method: "POST", credentials: "include",
        headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": headers["x-csrf-token"], "If-Match": headers["if-match"], "Idempotency-Key": headers["idempotency-key"] }, body: b }); return { status: r.status, body: await r.json() }; };
      return { same: await post(body), changed: await post(JSON.stringify({ ...JSON.parse(body), reason: "Changed conclusion replay" })) };
    }, { path, headers: sentHeaders, body: sentBody });
    if (replay.same.status !== 200 || JSON.stringify(replay.same.body) !== JSON.stringify(concluded) || replay.changed.status !== 409) throw Error("HTTPS conclusion replay mismatch");
    stage = "terminal reload and evidence";
    await page.reload();
    await page.getByText(`Current room readiness: ${concluded.effectiveReadiness}.`, { exact: true }).waitFor();
    if (await page.getByRole("button", { name: "Review conclusion", exact: true }).count()) throw Error("Terminal reload reopened editable decisions");
    const terminal = await page.evaluate(async path => {
      const r = await fetch(path + "/execution", { credentials: "include" }), d = await r.json();
      if (r.status !== 200 || !d.conclusion || d.sourceETag !== r.headers.get("ETag")) throw Error("Terminal receipt unavailable");
      const files = d.defects.flatMap(x => x.reinspectionPhotos);
      if (!files.length) throw Error("Terminal photo missing");
      for (const file of files) {
        const photo = await fetch(path + "/evidence/" + file, { credentials: "include" });
        if (photo.status !== 200 || photo.headers.get("X-Content-Type-Options") !== "nosniff" || !(await photo.arrayBuffer()).byteLength) throw Error("Terminal private evidence unavailable");
      }
      return d;
    }, path);
    if (terminal.state !== concluded.state || terminal.conclusion.operationId !== concluded.operationId) throw Error("Reload changed conclusion receipt");
    if (fixture.outcome === "FAIL") await page.getByRole("group", { name: "Defects, assignee and due time", exact: true }).waitFor();
    conclusion = { success: true, outcome: fixture.outcome, csrfEtag: true, staleAggregate: true, exactDefects: true, photo: true, replay: true, readiness: concluded.effectiveReadiness };
  }
  process.stdout.write(JSON.stringify({ success: true, inspection: body.id, canonicalAssignment: true, csrfEtag: true, conclusion,
    invalidInspectorDueDenied: true, foreignBranchDenied: true, replay: true, reloadPersisted: true, reinspectionRead: detailCheck }));
} catch (error) {
  process.stdout.write(JSON.stringify({ success: false, stage, diagnostic: String(error.message).split("\n")[0] })); process.exitCode = 1;
} finally { await browser?.close(); }
