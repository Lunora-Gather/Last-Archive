const assert = require('node:assert/strict');
const fs = require('node:fs/promises');
const { webkit } = require('playwright');

async function verifySaveTransfer(browser, base) {
  const from = await browser.newContext({ acceptDownloads: true });
  const to = await browser.newContext();
  const button = (page, name) => page.getByRole('button', { name, exact: true });
  const dialog = page => page.locator('#diaryModal');
  const day = (page, number) => page.waitForFunction(value =>
    document.querySelector('#title').textContent.includes('第' + value + '天'), number);

  async function open(context) {
    const page = await context.newPage();
    page.setDefaultTimeout(18_000);
    await page.goto(base, { waitUntil: 'domcontentloaded' });
    await page.getByText('今日补给预测').waitFor();
    return page;
  }

  async function get(page, route) {
    const response = await page.request.get(base + '/api/' + route);
    assert.equal(response.status(), 200, route);
    return response.json();
  }

  async function progress(page) {
    const [status, buildings, quests, items] = await Promise.all(
      ['status', 'buildings', 'quests', 'items'].map(route => get(page, route)));
    return { day: status.day, resources: status.resources, buildings, quests, items };
  }

  async function action(page, route, control, success = true) {
    const [response] = await Promise.all([
      page.waitForResponse(result => result.url() === base + '/api/' + route && result.request().method() === 'POST'),
      control.click()
    ]);
    assert.equal(response.status(), 200, route);
    const result = await response.json();
    assert.equal(result.ok, success, result.msg);
    await page.waitForFunction(() => !document.querySelector('#loadBtn').disabled);
    return result;
  }

  async function chooseFile(page, file) {
    const [chooser] = await Promise.all([
      page.waitForEvent('filechooser'),
      button(page, '导入存档文件').click()
    ]);
    await chooser.setFiles(file);
    await dialog(page).getByText(/此文件的进度为第 2 天/).waitFor();
  }

  const importFile = (page, success = true) =>
    action(page, 'save/import', button(dialog(page), '确认'), success);

  try {
    const [source, target] = await Promise.all([open(from), open(to)]);
    await day(source, 1);
    await day(target, 1);
    assert.equal((await action(source, 'nextday', button(source, '推进到下一天'))).saved, true);
    await day(source, 2);
    await source.locator('.tabs').getByRole('button', { name: /建筑/ }).click();
    const greenhouse = source.locator('.building-card').filter({ hasText: '温室' });
    await action(source, 'build', button(greenhouse, '建造此建筑'));
    await greenhouse.getByText('温室 Lv1', { exact: false }).waitFor();
    const original = await progress(source);

    const [download] = await Promise.all([
      source.waitForEvent('download'),
      button(source, '导出存档文件').click()
    ]);
    assert.equal(download.suggestedFilename(), 'last-archive-day-2.json');
    const filePath = await download.path();
    assert.ok(filePath);
    const file = JSON.parse(await fs.readFile(filePath, 'utf8'));
    assert.equal(file.format, 'last-archive-save');
    assert.equal(file.version, 1);
    assert.equal(file.data.CurrentDay, 2);
    assert.equal(file.data.BuildingStates.find(building => building.Id === 'greenhouse').Built, true);
    const upload = { name: download.suggestedFilename(), mimeType: 'application/json', buffer: await fs.readFile(filePath) };
    // Export includes the unsaved greenhouse without changing the source's stored checkpoint.
    await action(source, 'load', button(source, '读取历史存档'));
    assert.equal((await progress(source)).buildings.find(building => building.id === 'greenhouse').built, false);

    await action(target, 'save', button(target, '保存当前进度'));
    const previous = await progress(target);
    assert.equal(previous.day, 1);
    await chooseFile(target, upload);
    await button(dialog(target), '关闭').click();
    assert.deepEqual(await progress(target), previous);
    await chooseFile(target, {
      ...upload,
      buffer: Buffer.from(JSON.stringify({ ...file, data: { ...file.data, Rooms: [] } }))
    });
    const rejected = await importFile(target, false);
    assert.match(rejected.msg, /数据不完整/);
    await target.getByText(rejected.msg, { exact: true }).waitFor();
    assert.deepEqual(await progress(target), previous);
    await action(target, 'load', button(target, '读取历史存档'));
    assert.deepEqual(await progress(target), previous);

    await chooseFile(target, upload);
    assert.equal((await importFile(target)).day, 2);
    await target.getByText('已导入 · 第 2 天', { exact: true }).waitFor();
    assert.deepEqual(await progress(target), original);
    await target.reload({ waitUntil: 'domcontentloaded' });
    await day(target, 2);
    await action(target, 'load', button(target, '读取历史存档'));
    assert.deepEqual(await progress(target), original);
    assert.equal((await action(target, 'nextday', button(target, '推进到下一天'))).day, 3);
    assert.equal((await progress(source)).day, 2);

    await target.close();
    await to.clearCookies();
    const clean = await open(to);
    await day(clean, 1);
    await chooseFile(clean, upload);
    await importFile(clean);
    assert.deepEqual(await progress(clean), original);
    console.log('PASS 两个独立浏览器会话导出和导入文件，拒绝损坏文件，成功续玩，清除 Cookie 后可重新恢复');
  } finally {
    await Promise.all([from.close(), to.close()]);
  }
}

if (require.main === module) {
  const base = process.argv[2];
  if (!base) throw new Error('Usage: node tests/save-transfer.cjs <base URL>');
  (async () => {
    const browser = await webkit.launch();
    try {
      await verifySaveTransfer(browser, base.replace(/\/$/, ''));
    } finally {
      await browser.close();
    }
  })().catch(error => {
    console.error(error);
    process.exitCode = 1;
  });
}

module.exports = { verifySaveTransfer };
