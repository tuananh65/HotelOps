import assert from 'node:assert/strict';
import fs from 'node:fs';
import { chromium, expect } from '@playwright/test';

const origin = process.env.CONFIG_UI_ORIGIN || 'http://127.0.0.1:5174';
assert(['127.0.0.1', 'localhost'].includes(new URL(origin).hostname));
const browser = await chromium.launch({ channel: 'msedge', headless: true });
const out = '../artifacts/configuration-validation';
fs.mkdirSync(out, { recursive: true });
const errors = [], requests = [];
try {
  for (const restricted of [false, true]) {
    let shiftActive = true, blockRemoval = false, shiftWrites = 0;
    const permissions = restricted ? ['SHIFT_MANAGE'] : ['SHIFT_MANAGE', 'ROOM_SUPPLY_CONFIG', 'IR_VIEW', 'ASSET_VIEW', 'INCIDENT_VIEW'];
    const context = await browser.newContext({ viewport: { width: 1440, height: 1000 }, ignoreHTTPSErrors: true });
    await context.addInitScript(() => sessionStorage.setItem('hotelops.compact.session', 'configuration-fixture'));
    await context.route('**/api/v2/**', async route => {
      const url = new URL(route.request().url()), path = url.pathname.replace('/api/v2/', '');
      assert(['GET','PUT'].includes(route.request().method()), 'Only intercepted fixture reads/shift updates allowed');
      requests.push({ path, branch: url.searchParams.get('branchId'), restricted });
      let json = {};
      if (path === 'auth/me') json = {
        user: { fields: { userId: 1, fullName: 'OM kiểm thử' } }, branches: [{ branchId: 1, name: 'Chi nhánh thử' }, { branchId: 2, name: 'Chi nhánh thứ hai' }],
        roles: [1, 2].map(branchId => ({ code: 'OPERATION_MANAGER', branchId })),
        grants: [1, 2].flatMap(branchId => permissions.map(code => ({ code, branchId, dataScope: 'BRANCH' }))), memberships: [], requiresPasswordChange: false,
      };
      else if (path === 'workday/shifts/1' && route.request().method() === 'PUT') {
        shiftWrites++;
        const body = route.request().postDataJSON();
        assert.equal(body.branchId, 1);
        if (!body.isActive && blockRemoval) { await route.fulfill({status:409,json:{title:'SHIFT_HAS_OPEN_ASSIGNMENTS'}}); return; }
        shiftActive = body.isActive; json = {fields:{...body,shiftId:1}};
      }
      else if (path === 'workday') json = { canManage: true, shifts: [], assignments: [], weekStart: '2026-11-23', today: '2026-11-24' };
      else if (path === 'workday/calendar') json = { days: [], notice: '' };
      else if (path === 'workday/configuration') json = {
        shifts: [{ shiftId: 1, name: 'Ca sáng', department: 'Buồng phòng', departmentId: 1, role: 'Nhân viên buồng phòng', roleId: 1, startMinute: 480, endMinute: 960, capacity: 2, isActive: shiftActive, rowVersion:'AAAAAAAAAAE=' }],
        departments: [{ departmentId: 1, name: 'Buồng phòng' }], roles: [{ roleId: 1, name: 'Nhân viên buồng phòng' }], departmentRoles: [{ departmentId: 1, roleId: 1 }],
      };
      else if (path === 'workspace/options') json = { departments: [], items: [], warehouses: [{ warehouseId: 1, name: 'Kho chi nhánh' }], references: [] };
      else if (path === 'supplies/configuration' || path === 'assets' || path === 'work/room-issue-types') json = [];
      else if (path === 'work/internal-request-types') json = { items: [] };
      else if (path.startsWith('notifications')) json = { items: [], total: 0 };
      await route.fulfill({ json });
    });
    const page = await context.newPage();
    page.on('pageerror', e => errors.push(e.message));
    await page.goto(`${origin}/#workday`);
    await page.getByRole('button', { name: 'Cấu hình ca', exact: true }).click();
    await page.getByRole('button', {name:'Xóa ca Ca sáng 08:00–16:00',exact:true}).click();
    await expect(page.getByRole('dialog')).toContainText('Lịch sử ca đã làm được giữ nguyên');
    await page.getByRole('dialog').getByRole('button',{name:'Quay lại',exact:true}).click();
    assert.equal(shiftWrites,0);
    blockRemoval=true;
    await page.getByRole('button', {name:'Xóa ca Ca sáng 08:00–16:00',exact:true}).click();
    await page.getByRole('dialog').getByRole('button',{name:'Xóa ca',exact:true}).click();
    await expect(page.getByRole('dialog')).toContainText('Ca còn phân công chưa kết thúc');
    await page.getByRole('dialog').getByRole('button',{name:'Quay lại',exact:true}).click();
    blockRemoval=false;
    await page.getByRole('button', {name:'Xóa ca Ca sáng 08:00–16:00',exact:true}).click();
    await page.getByRole('dialog').getByRole('button',{name:'Xóa ca',exact:true}).click();
    await expect(page.getByRole('dialog')).toHaveCount(0);
    await expect(page.getByRole('button',{name:'Sửa Ca sáng',exact:true})).toHaveCount(0);
    await page.getByRole('checkbox',{name:'Hiện ca đã xóa / ngừng sử dụng',exact:true}).check();
    await expect(page.getByText('Đã ngừng',{exact:true})).toBeVisible();
    await page.getByRole('button',{name:'Sửa Ca sáng',exact:true}).click();
    await page.getByRole('checkbox',{name:'Đang dùng',exact:true}).check();
    await page.getByRole('button',{name:'Lưu ca',exact:true}).click();
    await expect(page.getByRole('dialog')).toHaveCount(0);
    await expect(page).toHaveURL(/#configuration\?tab=shifts$/);
    await expect(page.locator('.topbar h1')).toHaveText('Cấu hình');
    await expect(page.locator('.topbar h1')).toHaveCount(1);
    const tabs = page.getByRole('navigation', { name: 'Nhóm cấu hình', exact: true });
    await expect(tabs.getByRole('link')).toHaveCount(restricted ? 1 : 4);
    await expect(page.getByRole('button', { name: 'Sửa Ca sáng' })).toBeVisible();
    await page.getByRole('button', { name: 'Tạo ca', exact: true }).click();
    await expect(page.getByRole('dialog').getByRole('combobox', { name: /^Đối tượng/ })).toBeDisabled();
    await page.getByRole('dialog').locator('select[name="departmentId"]').selectOption('1');
    await expect(page.getByRole('dialog').getByRole('combobox', { name: /^Đối tượng/ })).toBeEnabled();
    await page.keyboard.press('Escape');
    await page.getByRole('button', { name: 'Quay lại lịch' }).click();
    await expect(page).toHaveURL(/#workday$/);
    await page.getByRole('button', { name: 'Cấu hình ca', exact: true }).click();
    if (!restricted) {
      for (const [tab, label] of [['supplies', 'Vật tư phòng'], ['requests', 'Yêu cầu nội bộ'], ['room', 'Báo cáo phòng']]) {
        await tabs.getByRole('link', { name: label, exact: true }).click();
        await expect(page).toHaveURL(new RegExp(`#configuration\\?tab=${tab}$`));
        await expect(page.locator('.configuration-heading')).toHaveText(label);
        await page.reload();
        await expect(page.locator('.configuration-heading')).toHaveText(label);
        await expect(page.locator('.topbar h1')).toHaveText('Cấu hình');
      }
      for (const [route, title] of [['supply-config', 'Vật tư phòng'], ['internal-settings', 'Yêu cầu nội bộ'], ['internal-settings?tab=room', 'Báo cáo phòng']]) {
        await page.goto(`${origin}/#${route}`);
        await expect(page.locator('.configuration-heading')).toHaveText(title);
      }
    } else {
      const before = requests.length;
      await page.goto(`${origin}/#configuration?tab=supplies`);
      await expect(page.getByText('Bạn chưa có quyền truy cập nhóm cấu hình này.')).toBeVisible();
      assert(!requests.slice(before).some(r => r.path === 'supplies/configuration'));
    }
    await page.goto(`${origin}/#configuration?tab=shifts`);
    await page.getByRole('button', { name: 'Đổi khách sạn', exact: true }).click();
    await page.getByRole('button', { name: /Chi nhánh thứ hai/ }).click();
    await page.getByRole('button', { name: 'Sử dụng khách sạn này' }).click();
    await expect.poll(() => requests.some(r => r.path === 'workday/configuration' && r.branch === '2' && r.restricted === restricted)).toBe(true);
    await expect(page.locator('.configuration-heading')).toHaveText('Ca làm việc');
    for (const width of [1440, 390, 320]) {
      await page.setViewportSize({ width, height: 900 });
      await expect(page.getByRole('button', { name: 'Sửa Ca sáng' })).toBeVisible();
      if (width < 600) await expect.poll(() => page.locator('.sidebar').evaluate(el => el.getBoundingClientRect().right)).toBeLessThanOrEqual(1);
      assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), `No page overflow at ${width}`);
      await page.screenshot({ path: `${out}/${restricted ? 'restricted' : 'full'}-${width}.png` });
    }
    await context.close();
  }
  assert.deepEqual(errors, []);
  fs.writeFileSync(`${out}/browser-results.json`, JSON.stringify({ passed: true, origin, fixtureApi: true, checks: ['Schedule shortcut and return', 'Four tabs and reload persistence', 'Legacy deep links', 'One page title', 'Permission-reduced role has no forbidden data fetch', 'Shift form department/role selection', '1440/390/320px layout'], requests }, null, 2));
  console.log('PASS unified configuration tabs, legacy routes, permission guards and responsive layout');
} finally { await browser.close(); }
