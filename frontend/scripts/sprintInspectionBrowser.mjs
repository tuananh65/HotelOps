import { chromium } from "@playwright/test";
let input = "";
for await (const chunk of process.stdin) input += chunk;
const fixture = JSON.parse(input);
input = "";
let browser,
  stage = "launch";
const checks = [];
try {
  browser = await chromium.launch({
    executablePath:
      "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
    headless: true,
  });
  const context = await browser.newContext();
  const page = await context.newPage();
  page.setDefaultTimeout(15000);
  const origin = "https://localhost:7142",
    path = `/inspections/${fixture.branch}/${fixture.inspection}/execution`;
  const api = `${origin}/api/branches/${fixture.branch}/inspections/${fixture.inspection}`;
  // Use the browser's validated HTTPS connection and cookie jar for direct API checks.
  // No Node-specific certificate bypass or separate request-context trust settings.
  async function request(url, options = {}) {
    return page.evaluate(
      async ({ url, options }) => {
        const response = await fetch(url, {
          method: options.method ?? "GET",
          credentials: "include",
          cache: "no-store",
          headers: {
            ...(options.data ? { "Content-Type": "application/json" } : {}),
            ...options.headers,
          },
          body: options.data ? JSON.stringify(options.data) : undefined,
        });
        return {
          status: response.status,
          etag: response.headers.get("ETag"),
          text: await response.text(),
        };
      },
      { url, options },
    );
  }
  async function snapshot() {
    const response = await request(api + "/execution");
    if (response.status !== 200) throw Error("Snapshot denied");
    return { etag: response.etag, body: JSON.parse(response.text) };
  }
  async function rejected(
    label,
    endpoint,
    expected,
    data,
    headers = {},
    method = "POST",
  ) {
    stage = label;
    const before = await snapshot();
    const response = await request(endpoint, {
      method,
      data,
      headers,
    });
    if (response.status !== expected)
      throw Error(
        "Unexpected denial status: expected=" +
          expected +
          " actual=" +
          response.status,
      );
    const after = await snapshot();
    if (
      after.etag !== before.etag ||
      JSON.stringify(after.body) !== JSON.stringify(before.body)
    )
      throw Error(
        "Rejected request changed execution snapshot: " +
          JSON.stringify({
            beforeTag: before.etag,
            afterTag: after.etag,
            changedKeys: Object.keys(before.body).filter(
              (k) =>
                JSON.stringify(before.body[k]) !==
                JSON.stringify(after.body[k]),
            ),
          }),
      );
    checks.push({
      label,
      expected,
      actual: response.status,
      snapshotUnchanged: true,
    });
  }
  stage = "anonymous execution API denied";
  await page.goto(origin + "/login");
  const anonymous = await request(api + "/execution");
  if (anonymous.status !== 401) throw Error("Anonymous execution exposed");
  checks.push(stage);
  stage = "anonymous deep link and login";
  await page.goto(origin + path);
  await page.waitForURL("**/login?*");
  await page.getByLabel("Username or email").fill(fixture.username);
  await page.getByLabel("Password", { exact: true }).fill(fixture.password);
  fixture.password = "";
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await page.waitForURL(origin + path);
  checks.push(stage);
  stage = "canonical inspection admission";
  await page
    .getByRole("heading", {
      name: `Inspection #${fixture.inspection}`,
      exact: true,
    })
    .waitFor();
  checks.push(stage);
  const bootstrap = await request(origin + "/api/auth/csrf");
  if (bootstrap.status !== 200) throw Error("CSRF bootstrap failed");
  const csrf = JSON.parse(bootstrap.text).csrfToken;
  const initial = await snapshot();
  const headers = { "X-CSRF-TOKEN": csrf, "If-Match": initial.etag };
  const reason = { reason: "Synthetic rejected operation" };
  await rejected("missing CSRF", api + "/start", 400, reason, {
    "If-Match": initial.etag,
  });
  await rejected("missing ETag", api + "/start", 428, reason, {
    "X-CSRF-TOKEN": csrf,
  });
  await rejected("weak ETag", api + "/start", 400, reason, {
    ...headers,
    "If-Match": "W/" + initial.etag,
  });
  await rejected("stale ETag", api + "/start", 412, reason, {
    ...headers,
    "If-Match": '"0000000000000000"',
  });
  await rejected(
    "wrong branch",
    `${origin}/api/branches/${fixture.branch + 100000}/inspections/${fixture.inspection}/start`,
    403,
    reason,
    headers,
  );
  await rejected("finish before start", api + "/finish", 409, reason, headers);
  stage = "start";
  await page
    .getByLabel("Reason", { exact: true })
    .fill("Synthetic inspection verification");
  await page
    .getByRole("button", { name: "Start Inspection", exact: true })
    .click();
  await page.getByText(/^State: INSPECTING\./).waitFor();
  checks.push(stage);
  await rejected("mandatory result missing", api + "/finish", 400, reason, {
    ...headers,
    "If-Match": (await snapshot()).etag,
  });
  async function missingItems(requirement, explanation) {
    stage = "UI lists " + requirement;
    const before = await snapshot();
    await page
      .getByLabel("Reason", { exact: true })
      .fill("Synthetic inspection verification");
    const acknowledgement = page.waitForResponse(
      (r) => r.url() === api + "/finish" && r.request().method() === "POST",
    );
    await page.getByRole("button", { name: /^Finish/ }).click();
    const response = await acknowledgement;
    const problem = await response.json();
    if (
      response.status() !== 400 ||
      problem.title !== "INSPECTION_INCOMPLETE" ||
      problem.messageCode !== "MSG03" ||
      problem.inspectionId !== fixture.inspection ||
      problem.branchId !== fixture.branch ||
      problem.versionId !== before.body.versionId ||
      problem.etag !== before.etag ||
      JSON.stringify(problem.missingItems) !==
        JSON.stringify([
          { itemId: before.body.criteria[0].itemId, requirement },
        ])
    )
      throw Error(
        "Missing-item response is not scoped to current inspection revision",
      );
    await page
      .getByRole("list", { name: "Missing inspection items" })
      .getByText(explanation, { exact: false })
      .waitFor();
    const after = await snapshot();
    if (JSON.stringify(before) !== JSON.stringify(after))
      throw Error("Incomplete finish mutated inspection");
    checks.push({ label: stage, snapshotUnchanged: true });
  }
  await missingItems("RESULT_REQUIRED", "Record and save Pass or Fail");
  const inspectionStarted = await snapshot();
  for (const invalidNote of ["x".repeat(4001), "unsafe\u0000note"])
    await rejected(
      "invalid criterion note rejected",
      api + "/results",
      400,
      {
        reason: "Synthetic note validation",
        results: [
          {
            itemId: inspectionStarted.body.criteria[0].itemId,
            outcome: "FAIL",
            note: invalidNote,
          },
        ],
      },
      { ...headers, "If-Match": inspectionStarted.etag },
      "PUT",
    );
  await rejected(
    "criterion audit failure rolls back result and uploaded file",
    api + "/results",
    500,
    {
      reason: "Synthetic audit fault",
      results: [
        {
          itemId: inspectionStarted.body.criteria[0].itemId,
          outcome: "FAIL",
          photos: [
            {
              name: "rollback.png",
              mimeType: "image/png",
              base64: "iVBORw0KGgo=",
            },
          ],
        },
      ],
    },
    { ...headers, "If-Match": inspectionStarted.etag },
    "PUT",
  );
  stage = "save failed criterion";
  await page.getByRole("combobox", { name: /^Result/ }).selectOption("FAIL");
  await page
    .getByLabel("Note", { exact: true })
    .fill("Synthetic criterion note\nSecond line");
  await page
    .getByLabel(/^Criterion evidence #/)
    .setInputFiles({
      name: "criterion.png",
      mimeType: "image/png",
      buffer: Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]),
    });
  await page.getByRole("button", { name: "Save results", exact: true }).click();
  await page
    .getByLabel("Failed criterion")
    .locator("option")
    .filter({ hasText: "Synthetic criterion" })
    .waitFor({ state: "attached" });
  checks.push(stage);
  stage =
    "save draft reload retains criterion note and evidence while Inspecting";
  await page.reload();
  await page.getByText(/^State: INSPECTING\./).waitFor();
  if (
    (await page.getByLabel("Note", { exact: true }).inputValue()) !==
    "Synthetic criterion note\nSecond line"
  )
    throw Error("Draft note lost");
  await page.getByText("Saved evidence files: 1", { exact: true }).waitFor();
  if (
    await page.getByRole("list", { name: "Missing inspection items" }).count()
  )
    throw Error("Stale missing-item list survived reload");
  checks.push(stage);
  const failed = await snapshot();
  await rejected(
    "failed criterion without defect",
    api + "/finish",
    400,
    reason,
    { ...headers, "If-Match": failed.etag },
  );
  await missingItems("DEFECT_REQUIRED", "Add a defect with a description");
  const defect = {
    itemId: failed.body.criteria[0].itemId,
    severityId: fixture.severity,
    description: "Synthetic rejected defect",
    location: "Synthetic bed",
  };
  await rejected(
    "defect audit failure rolls back defect and uploaded file",
    api + "/defects",
    500,
    {
      ...defect,
      description: "Synthetic audit fault",
      photos: [
        { name: "rollback.png", mimeType: "image/png", base64: "iVBORw0KGgo=" },
      ],
    },
    { ...headers, "If-Match": failed.etag },
  );
  await rejected(
    "foreign criterion",
    api + "/defects",
    400,
    { ...defect, itemId: 900000000 },
    { ...headers, "If-Match": failed.etag },
  );
  await rejected(
    "unknown severity",
    api + "/defects",
    400,
    { ...defect, severityId: 900000000 },
    { ...headers, "If-Match": failed.etag },
  );
  await rejected(
    "second attachment invalid rolls back first",
    api + "/defects",
    400,
    {
      ...defect,
      photos: [
        { name: "first.png", mimeType: "image/png", base64: "iVBORw0KGgo=" },
        { name: "bad.png", mimeType: "image/png", base64: "bm90LXBuZw==" },
      ],
    },
    { ...headers, "If-Match": failed.etag },
  );
  stage = "defect with server catalog and evidence";
  const item = await page
    .getByLabel("Failed criterion")
    .locator("option")
    .evaluateAll((xs) => xs.map((x) => x.value).find((v) => v));
  await page.getByLabel("Failed criterion").selectOption(item);
  await page
    .getByRole("combobox", { name: /^Severity/ })
    .selectOption(String(fixture.severity));
  await page.getByLabel("Description", { exact: true }).fill("Synthetic stain");
  await page.getByLabel("Location", { exact: true }).fill("Synthetic bed");
  await page.getByLabel(/Evidence \(JPG/).setInputFiles({
    name: "evidence.png",
    mimeType: "image/png",
    buffer: Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]),
  });
  await page
    .getByRole("button", { name: "Record defect", exact: true })
    .click();
  await page.getByText(/Defect #.*Synthetic stain/).waitFor();
  checks.push(stage);
  stage = "edit defect while inspecting";
  const beforeEdit = await snapshot();
  const defectId = beforeEdit.body.defects[0].id;
  await page
    .getByRole("button", { name: `Edit defect #${defectId}`, exact: true })
    .click();
  await page
    .getByLabel("Description", { exact: true })
    .fill("Synthetic stain corrected");
  await page
    .getByLabel("Location", { exact: true })
    .fill("Synthetic bed corrected");
  const editAcknowledgment = page.waitForResponse(
    (r) =>
      r.url() === api + `/defects/${defectId}` &&
      r.request().method() === "PUT",
  );
  await page
    .getByRole("button", { name: "Save defect changes", exact: true })
    .click();
  await page.getByText(/Defect #.*Synthetic stain corrected/).waitFor();
  if ((await editAcknowledgment).status() !== 200)
    throw Error("Edit was not acknowledged");
  const edited = await snapshot();
  if (
    edited.body.defects.length !== 1 ||
    edited.body.defects[0].id !== defectId ||
    JSON.stringify(edited.body.evidence) !==
      JSON.stringify(beforeEdit.body.evidence)
  )
    throw Error("Edit changed evidence/defect identity");
  checks.push(stage);
  const editBody = { ...defect, description: "Synthetic stale edit" };
  await rejected(
    "stale defect edit",
    api + `/defects/${defectId}`,
    412,
    editBody,
    { ...headers, "If-Match": beforeEdit.etag },
    "PUT",
  );
  await rejected(
    "foreign defect edit",
    api + "/defects/900000000",
    404,
    editBody,
    { ...headers, "If-Match": edited.etag },
    "PUT",
  );
  stage = "concurrent finish with one shared ETag";
  const beforeFinish = await snapshot();
  await rejected(
    "finish audit failure rolls back lifecycle and revision",
    api + "/finish",
    500,
    { reason: "Synthetic audit fault" },
    { ...headers, "If-Match": beforeFinish.etag },
  );
  stage = "concurrent finish with one shared ETag";
  const racing = await Promise.all(
    [1, 2].map(() =>
      request(api + "/finish", {
        method: "POST",
        data: { reason: "Synthetic simultaneous finish" },
        headers: { ...headers, "If-Match": beforeFinish.etag },
      }),
    ),
  );
  const statuses = racing.map((r) => r.status).sort((a, b) => a - b);
  if (JSON.stringify(statuses) !== JSON.stringify([200, 412]))
    throw Error("Concurrent finish did not enforce ETag");
  checks.push({ label: stage, statuses });
  stage = "stale UI finish requires explicit GET recovery";
  await page.getByRole("button", { name: /^Finish/ }).click();
  await page
    .getByText("This operation is blocked.", { exact: false })
    .waitFor();
  await page
    .getByRole("button", { name: "Verify current state (GET)", exact: true })
    .click();
  await page.getByText(/^State: PENDING_CONCLUSION\./).waitFor();
  await page
    .getByText("Passed criteria: 0; failed criteria: 1; defects: 1.", {
      exact: true,
    })
    .waitFor();
  checks.push(stage);
  stage = "edit defect while pending conclusion";
  await page
    .getByRole("button", { name: `Edit defect #${defectId}`, exact: true })
    .click();
  await page
    .getByLabel("Description", { exact: true })
    .fill("Synthetic stain final");
  const finalEditAcknowledgment = page.waitForResponse(
    (r) =>
      r.url() === api + `/defects/${defectId}` &&
      r.request().method() === "PUT",
  );
  await page
    .getByRole("button", { name: "Save defect changes", exact: true })
    .click();
  await page.getByText(/Defect #.*Synthetic stain final/).waitFor();
  if ((await finalEditAcknowledgment).status() !== 200)
    throw Error("Pending conclusion edit was not acknowledged");
  checks.push(stage);
  stage = "reload persists";
  await page.reload();
  await page.getByText(/^State: PENDING_CONCLUSION\./).waitFor();
  await page.getByText(/Defect #.*Synthetic stain/).waitFor();
  checks.push(stage);
  stage = "logout";
  await page.goto(origin + "/profile");
  await page.getByRole("button", { name: "Logout", exact: true }).click();
  await page.waitForURL("**/login");
  checks.push(stage);
  process.stdout.write(JSON.stringify({ success: true, checks }));
} catch (error) {
  process.stdout.write(
    JSON.stringify({
      success: false,
      stage,
      checks,
      screen:
        stage.startsWith("edit defect") || stage.startsWith("save draft")
          ? await browser.contexts()[0].pages()[0].locator("body").innerText()
          : undefined,
      diagnostic: stage.startsWith("edit defect")
        ? String(error?.message ?? "Browser failure")
        : String(error?.message ?? "Browser failure").split("\n")[0],
    }),
  );
  process.exitCode = 1;
} finally {
  await browser?.close();
}
