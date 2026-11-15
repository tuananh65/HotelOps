export async function verifySubmission(page, fixture, path, startBody) {
  const form = page.getByRole("form", { name: "Complete rework", exact: true });
  await form.waitFor();
  const detail = await page.evaluate(async path => (await fetch(path + "/execution", { credentials: "include" })).json(), path);
  if (detail.capability !== "SUBMIT_REWORK" || detail.state !== "IN_PROGRESS") throw Error("Submission capability missing");
  for (const defect of detail.defects) await form.getByLabel(`Remediation for defect ${defect.id}`, { exact: true }).fill("   ");
  await form.getByRole("button", { name: "Submit rework", exact: true }).click();
  await form.getByText("Enter a remediation note for every defect.", { exact: true }).waitFor();
  const security = await page.evaluate(async ({ path, detail }) => {
    const csrf = await (await fetch("/api/auth/csrf", { credentials: "include" })).json();
    const headers = { "Content-Type": "application/json", "If-Match": detail.sourceETag, "Idempotency-Key": crypto.randomUUID(), "X-CSRF-TOKEN": csrf.csrfToken };
    const command = { note: "Submit", remediations: detail.defects.map(d => ({ defectId: d.id, note: "Fixed" })) };
    const post = async (extra, body = command) => (await fetch(path + "/complete", { method: "POST", credentials: "include", headers: extra, body: JSON.stringify(body) })).status;
    const noCsrf = { ...headers }; delete noCsrf["X-CSRF-TOKEN"];
    const noTag = { ...headers }; delete noTag["If-Match"];
    return [await post(noCsrf), await post(noTag), await post({ ...headers, "If-Match": '"FFFFFFFFFFFFFFFF"' }), await post(headers, { note: "Submit", remediations: [] })];
  }, { path, detail });
  if (JSON.stringify(security) !== JSON.stringify([400, 428, 412, 400])) throw Error("Submission security preconditions failed: " + security);
  for (const defect of detail.defects) await form.getByLabel(`Remediation for defect ${defect.id}`, { exact: true }).fill("Defect remedied through HTTPS");
  const png = Buffer.from("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aS1cAAAAASUVORK5CYII=", "base64");
  await form.getByLabel(`Remediation photos for defect ${detail.defects[0].id}`, { exact: true }).setInputFiles({ name: "remediation.png", mimeType: "image/png", buffer: png });
  const responsePromise = page.waitForResponse(r => r.url().endsWith(path + "/complete") && r.request().method() === "POST");
  await form.getByRole("button", { name: "Submit rework", exact: true }).click();
  const response = await responsePromise, result = await response.json();
  if (response.status() !== 200 || result.contract !== "FE04_REWORK_SUBMISSION_V1" || result.state !== "PENDING_REINSPECTION"
    || result.intervalId !== startBody.intervalId || result.notification !== "PENDING_DELIVERY"
    || result.readiness !== (fixture.readiness === "BLOCKED" ? "BLOCKED" : "PENDING_CLEANING")) throw Error("Submission response failed: " + response.status());
  await page.getByText("Rework has been submitted and is waiting for reinspection.", { exact: true }).waitFor();
  const headers = await response.request().allHeaders(), body = response.request().postData();
  const retry = await page.evaluate(async ({ path, headers, body }) => {
    const send = async payload => {
      const response = await fetch(path + "/complete", { method: "POST", credentials: "include", headers: {
        "Content-Type": "application/json", "If-Match": headers["if-match"], "Idempotency-Key": headers["idempotency-key"], "X-CSRF-TOKEN": headers["x-csrf-token"],
      }, body: payload }); return { status: response.status, value: await response.json() };
    };
    return { same: await send(body), changed: await send(JSON.stringify({ ...JSON.parse(body), note: "Changed retry" })) };
  }, { path, headers, body });
  if (retry.same.status !== 200 || retry.same.value.sourceETag !== result.sourceETag || retry.same.value.publicationId !== result.publicationId || retry.changed.status !== 409) throw Error("Submission replay mismatch");
  await page.reload();
  await page.getByText(`Rework #${fixture.rework}: pending reinspection.`, { exact: true }).waitFor();
  await page.getByText("Remediation: Defect remedied through HTTPS", { exact: true }).first().waitFor();
  if (await page.getByRole("button", { name: "Submit rework", exact: true }).count()) throw Error("Submitted work remains editable");
  const url = await page.getByRole("link", { name: /^View remediation photo / }).getAttribute("href");
  if (!url?.startsWith(path + "/evidence/")) throw Error("Remediation photo source missing");
  const photo = await page.evaluate(async url => { const response = await fetch(url, { credentials: "include" }); return { status: response.status, base64: btoa(String.fromCharCode(...new Uint8Array(await response.arrayBuffer()))) }; }, url);
  if (photo.status !== 200 || !Buffer.from(photo.base64, "base64").equals(png)) throw Error("Persisted remediation photo mismatch");
}
