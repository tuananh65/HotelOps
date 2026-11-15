import { chromium } from "@playwright/test";
import { verifySubmission } from "./sprintReworkSubmitBrowser.mjs";
let input = "";
for await (const chunk of process.stdin) input += chunk;
const fixture = JSON.parse(input);
input = "";
let browser,
  page,
  stage = "login",
  lastStatus = null;
try {
  browser = await chromium.launch({
    executablePath:
      "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
    headless: true,
  });
  page = await browser.newPage();
  page.setDefaultTimeout(30000);
  const origin = "https://localhost:7143",
    path = `/api/branches/${fixture.branch}/reworks/${fixture.rework}`;
  await page.goto(origin + "/notifications");
  await page.waitForURL("**/login?*");
  await page.getByLabel("Username or email").fill(fixture.username);
  await page.getByLabel("Password", { exact: true }).fill(fixture.password);
  fixture.password = "";
  const login = page.waitForResponse((r) =>
    r.url().endsWith("/api/auth/login"),
  );
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  lastStatus = (await login).status();
  await page.waitForURL(origin + "/notifications");
  stage = "authorized source";
  const detail = page.waitForResponse((r) =>
    r.url().endsWith(path + "/execution"),
  );
  await page.getByRole("button", { name: "Open rework", exact: true }).click();
  const initial = await detail;
  lastStatus = initial.status();
  if (lastStatus !== 200) throw Error("Restricted source read failed");
  const source = await initial.json();
  if (
    source.state !== "PENDING_ACCEPTANCE" ||
    source.capability !== "ACCEPT_START"
  )
    throw Error("Missing committed receive capability");
  await page.getByText("Synthetic rejection defect", { exact: true }).waitFor();
  stage = "private defect evidence";
  const photoLink = await page
    .getByRole("link", { name: /^View defect photo / })
    .getAttribute("href");
  if (!photoLink?.startsWith(path + "/evidence/"))
    throw Error("Missing scoped defect photo");
  const photo = await page.evaluate(async (url) => {
    const response = await fetch(url, { credentials: "include" });
    const bytes = new Uint8Array(await response.arrayBuffer());
    return {
      status: response.status,
      mime: response.headers.get("content-type"),
      base64: btoa(String.fromCharCode(...bytes)),
    };
  }, photoLink);
  if (
    photo.status !== 200 ||
    photo.mime !== "image/png" ||
    !Buffer.from(photo.base64, "base64").equals(
      Buffer.from(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aS1cAAAAASUVORK5CYII=",
        "base64",
      ),
    )
  )
    throw Error("Private defect photo bytes mismatch");
  if (
    (await page.evaluate(
      async (url) => (await fetch(url, { credentials: "include" })).status,
      path + "/evidence/9223372036854775807",
    )) !== 404
  )
    throw Error("Foreign evidence disclosed");
  const anonymous = await browser.newContext();
  try {
    const outsider = await anonymous.newPage();
    await outsider.goto(origin);
    if (
      (await outsider.evaluate(
        async (url) => (await fetch(url, { credentials: "include" })).status,
        photoLink,
      )) !== 401
    )
      throw Error("Anonymous evidence disclosed");
  } finally {
    await anonymous.close();
  }
  await page
    .getByRole("button", { name: "Accept rework", exact: true })
    .waitFor();
  stage = "security preconditions";
  const security = await page.evaluate(
    async ({ path, tag }) => {
      const csrf = await (
        await fetch("/api/auth/csrf", { credentials: "include" })
      ).json();
      const body = JSON.stringify({ note: "Synthetic receive verification" });
      const headers = {
        "Content-Type": "application/json",
        "If-Match": tag,
        "Idempotency-Key": crypto.randomUUID(),
      };
      const missingCsrf = await fetch(path + "/accept", {
        method: "POST",
        credentials: "include",
        headers,
        body,
      });
      delete headers["If-Match"];
      headers["X-CSRF-TOKEN"] = csrf.csrfToken;
      const missingTag = await fetch(path + "/accept", {
        method: "POST",
        credentials: "include",
        headers,
        body,
      });
      return [missingCsrf.status, missingTag.status];
    },
    { path, tag: source.sourceETag },
  );
  if (security[0] !== 400 || security[1] !== 428)
    throw Error("Missing CSRF or version admitted");
  stage = "accept";
  const accepted = page.waitForResponse(
    (r) =>
      r.url().endsWith(path + "/accept") && r.request().method() === "POST",
  );
  await page
    .getByRole("button", { name: "Accept rework", exact: true })
    .click();
  const acceptResponse = await accepted;
  lastStatus = acceptResponse.status();
  const acceptBody = await acceptResponse.json();
  if (
    lastStatus !== 200 ||
    acceptBody.state !== "ACCEPTED" ||
    acceptBody.intervalId !== null ||
    acceptBody.readiness !== fixture.readiness
  )
    throw Error("Accept contract failed");
  await page
    .getByRole("button", { name: "Start rework", exact: true })
    .waitFor();
  await page
    .getByText("Rework has been saved successfully.", { exact: true })
    .waitFor();
  stage = "start";
  const started = page.waitForResponse(
    (r) => r.url().endsWith(path + "/start") && r.request().method() === "POST",
  );
  await page.getByRole("button", { name: "Start rework", exact: true }).click();
  const startResponse = await started;
  lastStatus = startResponse.status();
  const startBody = await startResponse.json();
  if (
    lastStatus !== 200 ||
    startBody.state !== "IN_PROGRESS" ||
    !(startBody.intervalId > 0) ||
    startBody.readiness !== fixture.readiness
  )
    throw Error("Start contract failed");
  const request = startResponse.request(),
    headers = await request.allHeaders();
  const replay = await page.evaluate(
    async ({ path, headers, body }) => {
      const result = await fetch(path + "/start", {
        method: "POST",
        credentials: "include",
        headers: {
          "Content-Type": "application/json",
          "If-Match": headers["if-match"],
          "Idempotency-Key": headers["idempotency-key"],
          "X-CSRF-TOKEN": headers["x-csrf-token"],
        },
        body,
      });
      return { status: result.status, body: await result.json() };
    },
    { path, headers, body: request.postData() },
  );
  if (
    replay.status !== 200 ||
    replay.body.intervalId !== startBody.intervalId ||
    replay.body.sourceETag !== startBody.sourceETag
  )
    throw Error("HTTPS retry changed receipt");
  stage = "reload";
  await page.reload();
  await page
    .getByText(`Rework #${fixture.rework}: in progress.`, { exact: true })
    .waitFor();
  if (
    await page.getByRole("button", { name: /^(Accept|Start) rework$/ }).count()
  )
    throw Error("Completed receive actions remain enabled");
  if (fixture.submission) {
    stage = "submit rework";
    await verifySubmission(page, fixture, path, startBody);
  }
  process.stdout.write(
    JSON.stringify({
      success: true,
      sourceRead: true,
      csrf: true,
      etag: true,
      accept: true,
      start: true,
      explicitReplay: true,
      reload: true,
      submission: !!fixture.submission,
      effectiveReadiness: fixture.readiness,
    }),
  );
} catch (error) {
  process.stdout.write(
    JSON.stringify({
      success: false,
      stage,
      lastStatus,
      diagnostic: String(error.message).split("\n")[0],
    }),
  );
  process.exitCode = 1;
} finally {
  await browser?.close();
}
