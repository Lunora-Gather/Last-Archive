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
      if (width >= 900) await inView(map);
      if (width >= 1024) await inView(page.locator('.panel.planning').filter({ hasText: '今日补给预测' }));
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
    await launch.scrollIntoViewIfNeeded();
    await inView(launch);
    console.log('PASS 矮窗口首屏、进度操作、页面滚动和切换导航正常，320 px 无横向溢出');
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
