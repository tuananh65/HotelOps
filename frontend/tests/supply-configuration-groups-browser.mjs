import assert from 'node:assert/strict';
import fs from 'node:fs';
import { chromium, expect } from '@playwright/test';

// UI fixture tests: every API call is intercepted; never writes to a live database.
const browser = await chromium.launch({ channel: 'msedge', headless: true });
const checks = [], errors = [];
const origin = process.env.SUPPLY_UI_ORIGIN || 'http://127.0.0.1:5174';
assert(['127.0.0.1', 'localhost'].includes(new URL(origin).hostname));
const out = '../artifacts/supply-config-validation';
fs.mkdirSync(out, { recursive: true });
try {
  for (const branchId of [1, 2]) {
    let warehouseCount = 1, failSave = false;
    const rows = [
      { configurationId: 1, itemId: 11, assetId: null, name: 'Dầu gội thử nghiệm', supplyGroup: 'A', initialQuantity: 10, perGuestQuantity: 1 },
      { configurationId: 2, itemId: 12, assetId: null, name: 'Đồ dùng bền thử nghiệm', supplyGroup: 'B', initialQuantity: 0, perGuestQuantity: 0 },
      { configurationId: 3, itemId: null, assetId: 21, name: 'Máy lạnh thử nghiệm', supplyGroup: 'C', initialQuantity: 0, perGuestQuantity: 0 },
    ].map(r => ({ ...r, branchId, code: `TEST-${r.configurationId}`, unit: r.itemId ? 'Cái' : null, unitId: r.itemId ? 1 : null,
      warehouseId: r.itemId ? branchId * 10 : null, minimumStock: r.itemId ? 50 : 0, isActive: true, revision: 1,
      updatedBy: 1, updatedAt: '2026-11-24T08:00:00Z', rowVersion: 'fixture-version' }));
    const context = await browser.newContext({ viewport: { width: 1440, height: 1000 }, ignoreHTTPSErrors: true });
    await context.addInitScript(() => sessionStorage.setItem('hotelops.compact.session', 'fixture-only'));
    await context.route('**/api/v2/**', async route => {
      const url = new URL(route.request().url()), path = url.pathname.replace('/api/v2/', '');
      let json = {};
      if (route.request().method() !== 'GET') {
        assert.equal(path, 'supplies/configuration');
        const body = route.request().postDataJSON();
        assert.equal(body.warehouseId, null, 'UI must not choose per-item warehouse');
        assert.equal(body.branchId, branchId);
        if (failSave) return route.fulfill({ status: 409, json: { title: 'STALE_ROW_VERSION' } });
        const index = rows.findIndex(r => r.itemId === body.itemId && r.assetId === body.assetId);
        assert(index >= 0);
        rows[index] = { ...rows[index], ...body, warehouseId: body.itemId ? branchId * 10 : null, revision: rows[index].revision + 1 };
        return route.fulfill({ json: rows[index] });
      }
      if (path === 'auth/me') json = {
        user: { fields: { userId: 1, fullName: 'OM kiểm thử' } }, branches: [{ branchId, name: `Chi nhánh thử ${branchId}` }],
        roles: [{ code: 'OPERATION_MANAGER', branchId }], grants: ['ROOM_SUPPLY_CONFIG', 'ASSET_VIEW'].map(code => ({ code, branchId, dataScope: 'BRANCH' })),
        memberships: [], requiresPasswordChange: false,
      };
      else if (path === 'supplies/configuration') json = rows;
      else if (path === 'workspace/options') json = {
        items: [{ itemId: 11, name: 'Dầu gội thử nghiệm', code: 'TEST-1', baseUnitId: 1 }],
        warehouses: Array.from({ length: warehouseCount }, (_, i) => ({ warehouseId: branchId * 10 + i, name: `Kho chi nhánh thử ${branchId}-${i}` })),
        references: [{ referenceId: 1, name: 'Cái' }],
      };
      else if (path === 'assets') json = [{ assetId: 21, name: 'Máy lạnh thử nghiệm', assetCode: 'TEST-3', isActive: true }];
      else if (path.startsWith('notifications')) json = { items: [], total: 0 };
      await route.fulfill({ json });
    });
    const page = await context.newPage();
    page.on('pageerror', e => errors.push(e.message));
    await page.goto(`${origin}/#supply-config`);
    const tabs = page.getByRole('navigation', { name: 'Nhóm cấu hình vật tư' });
    await expect(tabs.getByRole('button')).toHaveCount(3);
    await expect(page.locator('.supply-table tbody tr')).toHaveCount(1);
    await expect(page.locator('.supply-table')).toContainText('Dầu gội thử nghiệm');
    await expect(page.locator('.supply-group-summary')).toContainText(`Kho chi nhánh thử ${branchId}-0`);
    await page.screenshot({ path: `${out}/groups-desktop-branch-${branchId}.png` });
    await page.getByRole('button', { name: 'Chỉnh sửa', exact: true }).click();
    const dialog = page.getByRole('dialog');
    await expect(dialog.getByLabel('Kho cấp vật tư')).toHaveCount(0);
    await expect(dialog.getByLabel('Cấp đầu ca')).toBeVisible();
    failSave = true;
    await dialog.getByRole('button', { name: 'Lưu cấu hình' }).click();
    await expect(dialog).toContainText('Cấu hình đã thay đổi');
    await expect(dialog).toBeVisible();
    failSave = false;
    await dialog.getByRole('button', { name: 'Lưu cấu hình' }).click();
    await expect(dialog).toHaveCount(0);
    await tabs.getByRole('button', { name: /B ·/ }).click();
    await expect(page.locator('.supply-table')).toContainText('Đồ dùng bền thử nghiệm');
    await expect(page.locator('.supply-table')).not.toContainText('Dầu gội thử nghiệm');
    await page.getByRole('button', { name: 'Chỉnh sửa', exact: true }).click();
    await expect(dialog.getByLabel('Cấp đầu ca')).toHaveCount(0);
    await expect(dialog.getByLabel('Tồn tối thiểu')).toBeVisible();
    await page.keyboard.press('Escape');
    await tabs.getByRole('button', { name: /C ·/ }).click();
    await page.getByRole('button', { name: 'Chỉnh sửa', exact: true }).click();
    await expect(dialog.getByLabel('Tồn tối thiểu')).toHaveCount(0);
    await expect(dialog.getByLabel('Cấp đầu ca')).toHaveCount(0);
    await page.keyboard.press('Escape');
    await page.getByRole('button', { name: 'Thêm cấu hình', exact: true }).click();
    await expect(dialog).toContainText('C · Thiết bị sửa chữa bên ngoài');
    await dialog.getByLabel('Vật tư / thiết bị').selectOption('asset:21');
    await expect(dialog.getByLabel('Tồn tối thiểu')).toHaveCount(0);
    for (const width of [390, 320]) {
      await page.setViewportSize({ width, height: 844 });
      assert(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth), 'No horizontal overflow');
      const box = await dialog.boundingBox(); assert(box.x >= 0 && box.x + box.width <= width + 1);
    }
    await page.screenshot({ path: `${out}/groups-mobile-branch-${branchId}.png` });
    await page.keyboard.press('Escape');
    await tabs.getByRole('button', { name: /A ·/ }).click();
    for (const count of [0, 2]) {
      warehouseCount = count;
      await page.getByRole('button', { name: 'Tải lại', exact: true }).click();
      await expect(page.locator('.supply-group-summary')).toContainText('Chưa xác định được kho duy nhất');
      await page.getByRole('button', { name: 'Chỉnh sửa', exact: true }).click();
      await expect(dialog.getByRole('button', { name: 'Lưu cấu hình' })).toBeDisabled();
      await expect(dialog).toContainText('Chi nhánh phải có đúng một kho hoạt động');
      await page.keyboard.press('Escape');
    }
    checks.push(`Branch ${branchId}: group isolation, conditional fields, automatic warehouse, failed save, zero/multiple warehouse guard, 390/320px dialogs`);
    await context.close();
  }
  assert.deepEqual(errors, []);
  fs.writeFileSync(`${out}/browser-results.json`, JSON.stringify({ passed: true, origin, fixtureApi: true, checks }, null, 2));
  console.log('PASS supply configuration browser checks for two branches and mobile widths');
} finally { await browser.close(); }
