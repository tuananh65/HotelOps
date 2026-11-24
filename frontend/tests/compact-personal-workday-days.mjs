import assert from 'node:assert/strict';
import fs from 'node:fs';
import { chromium, expect } from '@playwright/test';

const base = process.env.UX_BASE || 'https://127.0.0.1:5082';
const out = '../artifacts/personal-workday-days';
fs.mkdirSync(out, { recursive: true });
const browser = await chromium.launch({ executablePath: 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe', headless: true });
const errors = [], checks = [];
try {
  for (const manager of [false, true]) {
    const context = await browser.newContext({ viewport: { width: 1440, height: 1000 }, timezoneId: 'Asia/Ho_Chi_Minh' });
    await context.addInitScript(() => sessionStorage.setItem('hotelops.compact.session', 'fixture'));
    const reads = [], writes = [];
    let working = false, failure = false;
    await context.route('**/api/v2/**', async route => {
      const req = route.request(), url = new URL(req.url()), path = url.pathname.replace('/api/v2/', '');
      let json = { items: [], total: 0 };
      if (path === 'auth/me') json = {
        user: { fields: { userId: 9, fullName: 'Nhân viên thử nghiệm' } }, branches: [{ branchId: 1, name: 'Bùi Viện' }],
        roles: [{ code: manager ? 'DEPT_MANAGER' : 'HK_STAFF', name: 'Vai trò thử nghiệm', branchId: 1 }], memberships: [], requiresPasswordChange: false,
        grants: (manager ? ['SHIFT_MANAGE', 'ATTENDANCE_RECORD'] : ['ATTENDANCE_RECORD']).map(code => ({ code, branchId: 1, dataScope: code === 'ATTENDANCE_RECORD' ? 'OWN' : 'BRANCH' }))
      };
      else if (path === 'workday/calendar') json = { markers: [], notice: '' };
      else if (path === 'workday') {
        const date = url.searchParams.get('date'); reads.push(date);
        if (failure) return route.fulfill({ status: 503, json: { title: 'SERVICE_UNAVAILABLE' } });
        const monday = new Date(`${date}T12:00:00Z`); monday.setUTCDate(monday.getUTCDate() - (monday.getUTCDay() + 6) % 7);
        const weekStart = monday.toISOString().slice(0, 10);
        json = { today: '2026-11-23', weekStart, currentMinute: 600, canManage: manager, timezone: 'Asia/Ho_Chi_Minh',
          shifts: [{ shiftId: 1, name: 'Ca sáng', startMinute: 480, endMinute: 960, departmentId: 1, roleId: 2, department: 'Buồng phòng', role: 'Nhân viên buồng phòng', capacity: 4, isActive: true }],
          assignments: weekStart === '2026-11-23' ? [
            { assignmentId: 42, userId: 9, fullName: 'Nhân viên thử nghiệm', shiftId: 1, workDate: '2026-11-23', status: working ? 'WORKING' : 'PLANNED', rowVersion: 'v1' },
            { assignmentId: 43, userId: 9, shiftId: 1, workDate: '2026-11-24', status: 'PLANNED', rowVersion: 'v2' },
            { assignmentId: 44, userId: 9, shiftId: 1, workDate: '2026-11-25', status: 'CANCELLED', rowVersion: 'v3' }
          ] : [], active: working ? { assignmentId: 42, name: 'Ca sáng', startedAt: '2026-11-23T01:00:00Z', rowVersion: 'v-working' } : null };
      } else if (path.startsWith('workday/assignments/') && req.method() === 'POST') {
        writes.push({ path, body: req.postDataJSON() }); working = path.endsWith('/start');
      }
      await route.fulfill({ status: 200, json });
    });
    const page = await context.newPage();
    await page.clock.setFixedTime(new Date('2026-11-23T03:00:00Z'));
    page.on('pageerror', e => errors.push(e.message));
    await page.goto(`${base}/#workday`);
    if (manager) {
      await expect(page.getByRole('heading', { name: 'Lịch theo đối tượng' })).toBeVisible();
      await page.getByRole('button', { name: 'Tuần sau', exact: true }).click();
      await expect(page.getByLabel('Ngày trong tuần')).toHaveValue('2026-11-30');
      await expect(page.locator('.calendar')).toBeVisible();
      await page.getByRole('button', { name: 'Tuần trước', exact: true }).click();
      await expect(page.getByLabel('Ngày trong tuần')).toHaveValue('2026-11-23');
      checks.push('manager keeps weekly calendar and navigation');
    } else {
      const day = page.getByLabel('Ngày làm việc', { exact: true });
      const next = page.getByRole('button', { name: 'Ngày sau', exact: true });
      const previous = page.getByRole('button', { name: 'Ngày trước', exact: true });
      await expect(page.getByRole('heading', { name: 'Công việc', exact: true })).toBeVisible();
      await expect(page.locator('.shift-list article')).toHaveCount(1);
      await page.getByRole('button', { name: 'Bắt đầu ca', exact: true }).click();
      await expect(page.getByRole('button', { name: 'Kết thúc ca', exact: true })).toBeVisible();
      await next.click(); await expect(day).toHaveValue('2026-11-24');
      await expect(page.locator('.shift-list article')).toHaveCount(1);
      await expect(page.locator('.shift-list')).toContainText('Thứ Ba');
      await expect(page.getByRole('button', { name: 'Bắt đầu ca' })).toHaveCount(0);
      await expect(page.getByRole('button', { name: 'Kết thúc ca' })).toBeVisible();
      await next.click(); await expect(day).toHaveValue('2026-11-25');
      await expect(page.locator('.shift-list')).toContainText('Bạn chưa được phân ca trong ngày này.');
      await day.fill('2026-11-30'); await expect(page.locator('.shift-list')).toContainText('Bạn chưa được phân ca');
      await previous.click(); await expect(day).toHaveValue('2026-11-29');
      await expect(previous).toBeEnabled(); assert(reads.includes('2026-11-29'));
      await day.fill('2026-12-31'); await expect(previous).toBeEnabled();
      await next.click(); await expect(day).toHaveValue('2027-01-01'); await expect(previous).toBeEnabled();
      assert(reads.includes('2027-01-01'));
      await page.getByRole('button', { name: 'Hôm nay', exact: true }).click(); await expect(day).toHaveValue('2026-11-23');
      await page.getByRole('button', { name: 'Kết thúc ca', exact: true }).click();
      await expect(page.getByRole('button', { name: 'Kết thúc ca' })).toHaveCount(0);
      assert.equal(writes.length, 2); assert.equal(writes[1].body.rowVersion, 'v-working');
      failure = true; await next.click(); await expect(page.getByRole('alert')).toBeVisible();
      await expect(previous).toBeVisible(); failure = false;
      await page.getByRole('button', { name: 'Thử lại', exact: true }).click();
      await expect(page.locator('.shift-list article')).toHaveCount(1);
      for (const width of [1440, 768, 375]) {
        await page.setViewportSize({ width, height: 1000 });
        await expect(next).toBeVisible(); await expect(previous).toBeVisible();
        assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), `overflow at ${width}`);
        await page.screenshot({ path: `${out}/daily-${width}.png`, fullPage: true });
      }
      checks.push('daily filtering, cancelled-only empty, next/previous/Today, week and year boundaries, active shift retained, start/end version, error/retry, responsive');
    }
    await context.close();
  }
  assert.deepEqual(errors, []);
  fs.writeFileSync(`${out}/verification.json`, JSON.stringify({ passed: true, fixtureOnly: true, checks, errors }, null, 2));
  console.log('PASS personal Workday day navigation and manager weekly regression.');
} finally { await browser.close(); }
