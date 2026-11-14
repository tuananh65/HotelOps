import { test } from "node:test";
import assert from "node:assert/strict";
import { pageTest } from "./independent-browser.mjs";
const instruction =
  "For password assistance, contact your Administrator through your usual internal support channel.";
const anonymous = {
  status: 401,
  body: { status: 401, title: "SESSION_INVALID" },
};
test("Login Forgot Password link opens identifier form and returns to Login", () =>
  pageTest(
    { auth: anonymous },
    async (p) => {
      await p.getByRole("link", { name: /forgot password/i }).click();
      await p.waitForURL("**/forgot-password");
      await p.getByRole("textbox", { name: "Username or email" }).waitFor();
      await p.getByRole("link", { name: "Return to sign in" }).click();
      await p.waitForURL("**/login");
      assert.equal(await p.locator("input[type=password]").count(), 1);
    },
    "/login",
  ));
test("required and whitespace validation before any instructions, no extra requests", () =>
  pageTest(
    { auth: anonymous },
    async (p) => {
      await p.getByRole("textbox", { name: "Username or email" }).waitFor();
      const before = await p.evaluate(() => window.fixture.calls.length);
      await p.getByRole("button", { name: "Show instructions" }).click();
      await p.getByRole("alert").waitFor();
      assert.equal(await p.getByText(instruction, { exact: true }).count(), 0);
      await p.getByRole("textbox", { name: "Username or email" }).fill("   ");
      await p.getByRole("button", { name: "Show instructions" }).click();
      await p.getByRole("alert").waitFor();
      assert.equal(await p.evaluate(() => window.fixture.calls.length), before);
    },
    "/forgot-password",
  ));
test("username/email/nonexistent identifiers all yield exactly the same instruction without lookup", () =>
  pageTest(
    { auth: anonymous },
    async (p) => {
      const input = p.getByRole("textbox", { name: "Username or email" });
      await input.waitFor();
      const before = await p.evaluate(() => window.fixture.calls.length);
      for (const identifier of [
        "known-user",
        "missing-user",
        "known@example.invalid",
        "unknown@example.invalid",
      ]) {
        await input.fill(identifier);
        await p.getByRole("button", { name: "Show instructions" }).click();
        assert.equal(await p.getByRole("status").innerText(), instruction);
        assert.equal(await input.inputValue(), "");
        assert.ok(!(await p.locator("body").innerText()).includes(identifier));
      }
      assert.equal(await p.evaluate(() => window.fixture.calls.length), before);
      assert.equal(
        (
          await p.evaluate(() =>
            window.fixture.calls.filter((c) =>
              /reset|forgot|recovery|email/.test(c.url),
            ),
          )
        ).length,
        0,
      );
      assert.doesNotMatch(
        await p.locator("body").innerText(),
        /email (was|has been) sent|password (was|has been) reset|account (exists|not found)/i,
      );
    },
    "/forgot-password",
  ));
test("instruction-only screen leaves authenticated Change Password flow intact", () =>
  pageTest(
    {},
    async (p) => {
      await p.getByRole("textbox", { name: "Username or email" }).waitFor();
      await p.evaluate(() => window.fixture.navigate("/change-password"));
      await p.waitForURL("**/change-password");
      await p.locator("input[type=password]").first().waitFor();
      assert.equal(await p.locator("input[type=password]").count(), 3);
      assert.equal(
        (
          await p.evaluate(() =>
            window.fixture.calls.filter((c) => c.method !== "GET"),
          )
        ).length,
        0,
      );
    },
    "/forgot-password",
  ));
