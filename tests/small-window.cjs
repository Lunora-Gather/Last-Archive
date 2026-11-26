const assert = require('node:assert/strict');
const path = require('node:path');
const { webkit } = require('playwright');

async function verifySmallWindow(browser, base) {
  const context = await browser.newContext({ viewport: { width: 1280, height: 720 } });
  const page = await context.newPage();
  page.setDefaultTimeout(15000);
  const menu = page.getByRole('button', { name: '进度管理' });
  const advance = page.getByRole('button', { name: '推进到下一天' });
  const tabs = page.getByRole('navigation', { name: '游戏导航' });
  const menuDialog = page.getByRole('dialog', { name: '进度管理' });
  const map = page.getByRole('button', { name: '查看探索地图' });

  async function inView(locator) {
    const bounds = await locator.boundingBox();
    const { width, height } = page.viewportSize();
    assert.ok(bounds && bounds.x >= 0 && bounds.y >= 0 &&
      bounds.x + bounds.width <= width + 1 && bounds.y + bounds.height <= height + 1,
      JSON.stringify({ bounds, width, height }));
  }

  try {
    await page.goto(base, { waitUntil: 'domcontentloaded' });
    await page.getByText('今日补给预测').waitFor();
    const coverWidth = await page.evaluate(async () => {
      const cover = new Image();
      cover.src = '/assets/archive-city.jpg';
      await cover.decode();
      return cover.naturalWidth;
    });
    assert.ok(coverWidth > 0);
    for (const [width, height] of [[1320, 810], [1280, 720], [1024, 600], [900, 430], [390, 620], [320, 568]]) {
      await page.setViewportSize({ width, height });
      assert.equal(await page.evaluate(() => document.documentElement.scrollWidth), width);
      await inView(menu);
      await inView(advance);
      if (width <= 390) {
        for (const link of await tabs.getByRole('button').all()) await inView(link);
        await inView(page.locator('.forecast'));
      }
      if (width >= 900) await inView(map);
      if (width >= 900) await inView(page.locator('#supplySheet'));
      await menu.click();
      await inView(menuDialog);
      assert.equal(await menuDialog.locator('.game-controls button').count(), 6);
      await inView(menuDialog.getByRole('button', { name: '开启全新游戏' }));
      await menuDialog.getByRole('button', { name: '关闭' }).click();
    }

    await page.setViewportSize({ width: 1024, height: 600 });
    if (process.env.LAST_ARCHIVE_SCREENSHOTS) {
      await page.screenshot({ path: path.join(process.env.LAST_ARCHIVE_SCREENSHOTS, 'small-window.png') });
    }
    await page.getByRole('button', { name: '查看主线档案' }).click();
    await page.locator('.quest-card').first().waitFor();
    assert.equal(await page.locator('.quest-card').count(), 4);
    assert.equal(await page.locator('.quest-card:not([data-kind="MainQuest"])').count(), 0);
    await tabs.getByRole('button', { name: '建筑' }).click();
    const lastBuilding = page.locator('.building-card').last();
    await lastBuilding.waitFor();
    await page.mouse.move(600, 460);
    await page.mouse.wheel(0, 4000);
    await page.waitForFunction(card => window.scrollY > 0 &&
      card.getBoundingClientRect().bottom <= innerHeight, await lastBuilding.elementHandle());
    await inView(menu);
    await tabs.getByRole('button', { name: '探索/战斗' }).click();
    await page.getByText('选择目的地地图').waitFor();
    assert.equal(await page.evaluate(() => window.scrollY), 0);
    const launch = page.getByRole('button', { name: '开启野外探索' });
    await inView(launch);

    await page.setViewportSize({ width: 320, height: 568 });
    const mapPicker = page.getByRole('button', { name: '更换地点' });
    await inView(mapPicker);
    await inView(page.locator('.crew').first());
    await inView(launch);
    for (const mapId of ['subway_ruins', 'abandoned_hospital']) {
      await mapPicker.click();
      await page.locator(`#mapChoices [data-id="${mapId}"]`).click();
      await mapPicker.waitFor();
      assert.equal(await mapPicker.getAttribute('aria-expanded'), 'false');
      assert.equal(await page.locator('.site-photo').getAttribute('data-site'), mapId);
    }
    for (const id of ['iron_guard', 'big_hu', 'lin_doctor']) {
      await page.locator(`input[name="exploreTeamCheckbox"][value="${id}"]`).check();
      await inView(launch);
    }
    await launch.click();
    await page.getByRole('button', { name: /前往: 走廊/ }).click();
    await page.getByRole('button', { name: '搜寻当前房间' }).click();
    const attack = page.getByRole('button', { name: '普通攻击' });
    await attack.waitFor();
    await inView(attack);
    await inView(page.locator('.unit-select').last());
    await attack.focus();
    const scrollBeforeAttack = await page.evaluate(() => scrollY);
    const response = page.waitForResponse(res => res.url().endsWith('/api/combat/action') && res.request().method() === 'POST');
    await page.keyboard.press('Enter');
    assert.equal((await (await response).json()).ok, true);
    await page.waitForFunction(() => !document.getElementById('loadBtn').disabled);
    assert.equal(await page.evaluate(() => document.activeElement.dataset.id), 'Attack');
    assert.equal(await page.evaluate(() => scrollY), scrollBeforeAttack);
    await inView(attack);
    console.log('PASS 矮窗口补给表、地点选择、组队出发和键盘交战，320 px 无横向溢出');
  } finally {
    await context.close();
  }
}

if (require.main === module) {
  const base = process.argv[2];
  if (!base) throw new Error('Usage: node tests/small-window.cjs <base URL>');
  (async () => {
    const browser = await webkit.launch();
    try {
      await verifySmallWindow(browser, base);
    } finally {
      await browser.close();
    }
  })().catch(error => {
    console.error(error);
    process.exitCode = 1;
  });
}

module.exports = { verifySmallWindow };
