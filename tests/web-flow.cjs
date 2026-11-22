const assert = require('node:assert/strict');
const { spawn } = require('node:child_process');
const fs = require('node:fs/promises');
const net = require('node:net');
const os = require('node:os');
const path = require('node:path');
const { once } = require('node:events');
const { setTimeout: delay } = require('node:timers/promises');
const { webkit } = require('playwright');
const { verifySaveTransfer } = require('./save-transfer.cjs');

const timeout = 12_000;
const dll = path.resolve(process.argv[2] || 'bin/Release/net10.0/LastArchive.dll');
const dotnet = process.env.DOTNET_EXECUTABLE || 'dotnet';

async function freePort() {
  const listener = net.createServer().listen(0, '127.0.0.1');
  await once(listener, 'listening');
  const { port } = listener.address();
  await new Promise((resolve, reject) => listener.close(error => error ? reject(error) : resolve()));
  return port;
}

async function main() {
  const directory = await fs.mkdtemp(path.join(os.tmpdir(), 'last-archive-web-test-'));
  const saveDirectory = path.join(directory, 'saves');
  const port = await freePort();
  const base = 'http://127.0.0.1:' + port;
  const browser = await webkit.launch();
  let server;
  let serverOutput = '';
  const errors = [];
  const report = message => console.log('PASS', message);

  async function startServer() {
    server = spawn(dotnet, [dll, '--web'], {
      cwd: directory,
      env: { ...process.env, PORT: String(port), LAST_ARCHIVE_SAVE_DIR: saveDirectory }
    });
    server.stdout.on('data', bytes => { serverOutput += bytes; });
    server.stderr.on('data', bytes => { serverOutput += bytes; });
    server.on('error', error => { serverOutput += error.message; });
    for (let i = 0; i < 100; i++) {
      try {
        const response = await fetch(base + '/api/ai/config');
        if (response.ok) return;
      } catch {}
      if (server.exitCode !== null) break;
      await delay(100);
    }
    throw new Error('Web server failed to start');
  }

  async function stopServer() {
    if (!server || server.exitCode !== null || server.signalCode !== null || !server.pid) return;
    const exited = once(server, 'exit');
    server.kill();
    await exited;
  }

  async function open(context) {
    const page = await context.newPage();
    page.setDefaultTimeout(timeout);
    page.on('pageerror', error => errors.push(error.message));
    page.on('dialog', dialog => dialog.accept());
    await page.goto(base, { waitUntil: 'domcontentloaded' });
    await page.getByText('档案城概览').waitFor();
    return page;
  }

  const get = async (page, route) => {
    const response = await page.request.get(base + '/api/' + route);
    assert.equal(response.status(), 200, route);
    return response.json();
  };
  const post = (page, route, data = {}) => page.request.post(base + '/api/' + route, { data });
  async function action(page, route, button) {
    const [response] = await Promise.all([
      page.waitForResponse(r => r.url() === base + '/api/' + route && r.request().method() === 'POST'),
      button.click()
    ]);
    assert.equal(response.status(), 200, route);
    const result = await response.json();
    assert.equal(result.ok, true, route + ': ' + (result.msg || ''));
    return result;
  }

  const button = (page, label) => page.getByRole('button', { name: label });
  async function tab(page, label, marker) {
    await page.locator('.tabs').getByRole('button', { name: label }).click();
    await page.locator('#page').getByText(marker).filter({ visible: true }).first().waitFor();
  }
  async function day(page, number) {
    await page.waitForFunction(day => document.querySelector('#title').textContent.includes('第' + day + '天'), number);
  }
  async function startExplore(page, map, team) {
    await tab(page, /探索\/战斗/, '选择目的地地图');
    assert.equal(await page.locator('#mapChoices .map-option').count(), 8);
    const choice = page.locator('#mapChoices [data-id="' + map + '"]');
    await choice.click();
    await page.locator('#mapChoices [data-id="' + map + '"][aria-pressed="true"]').waitFor();
    for (const id of team) await page.locator('input[name="exploreTeamCheckbox"][value="' + id + '"]').check();
    await action(page, 'explore/start', button(page, '开启野外探索'));
    await page.getByText('搜寻当前房间').waitFor();
  }
  async function move(page, label, roomId) {
    await action(page, 'explore/move', button(page, label));
    assert.equal((await get(page, 'explore/status')).roomId, roomId);
    await page.locator('#page').getByText('当前位置：', { exact: false }).waitFor();
  }
  async function returnToBase(page) {
    await action(page, 'explore/return', button(page, '携带物资返回安全屋'));
    await page.locator('#mapChoices').waitFor();
  }
  function quest(quests, id) { return quests.find(q => q.id === id).status; }

  try {
    await fs.writeFile(path.join(directory, 'ai_config.json'), '{"ApiKey":"test-local-placeholder"}');
    await startServer();
    const first = await browser.newContext({ viewport: { width: 1440, height: 960 } });
    const second = await browser.newContext();
    const [page, other] = await Promise.all([open(first), open(second)]);
    assert.equal((await get(page, 'status')).day, 1);
    assert.equal((await get(other, 'status')).aliveNPCs, 15);
    await page.getByText('今日补给预测').waitFor();
    assert.equal((await get(page, 'status')).forecast.find(item => item.resource === 'Food').reserve, 35);
    const stylesheet = await fetch(base + '/assets/game.css');
    assert.ok(stylesheet.headers.get('etag'));
    const cached = await fetch(base + '/assets/game.css', { headers: { 'If-None-Match': stylesheet.headers.get('etag') } });
    assert.equal(cached.status, 304);
    if (process.env.LAST_ARCHIVE_SCREENSHOTS) {
      await page.screenshot({ path: path.join(process.env.LAST_ARCHIVE_SCREENSHOTS, 'overview.png'), animations: 'disabled' });
      await button(page, '切换浅色主题').click();
      assert.equal(await page.locator('html').getAttribute('class'), 'light');
      await page.screenshot({ path: path.join(process.env.LAST_ARCHIVE_SCREENSHOTS, 'overview-light.png'), animations: 'disabled' });
      await button(page, '切换深色主题').click();
    }
    await tab(page, /AI/, '内置 AI');
    assert.equal(await page.locator('input[type=password]').count(), 0);
    assert.deepEqual(await get(page, 'ai/config'), { provider: 'Mock', configurable: false });
    assert.equal((await fetch(base + '/api/status')).status, 409);
    const securePage = await fetch(base, { headers: { 'X-Forwarded-Proto': 'https' } });
    assert.match(securePage.headers.get('set-cookie'), /^__Host-last_archive_player=.+; HttpOnly; SameSite=Lax; Secure$/);
    assert.equal((await page.request.post(base + '/api/save', { data: 'save' })).status(), 415);
    for (const route of ['ai/config', 'ai/test']) assert.equal((await post(page, route)).status(), 403);
    assert.equal(await fs.readFile(path.join(directory, 'ai_config.json'), 'utf8'), '{"ApiKey":"test-local-placeholder"}');
    report('公开 AI 仅提供 Mock，禁止读取密钥和修改配置');

    await tab(page, /居民/, '林医生');
    assert.match((await action(page, 'talk', button(page, '交流对话').first())).dialogue, /林医生/);
    await page.locator('#diaryModal[open]').waitFor();
    await button(page, '关闭').click();
    await tab(page, /建筑/, '温室');
    const greenhouse = page.locator('.building-card').filter({ hasText: '温室' });
    await action(page, 'build', greenhouse.getByRole('button', { name: '建造此建筑' }));
    await greenhouse.getByText('温室 Lv1', { exact: false }).waitFor();
    assert.equal((await get(other, 'buildings')).find(b => b.id === 'greenhouse').built, false);
    assert.equal((await get(page, 'status')).forecast.find(item => item.resource === 'Food').reserve, 40);

    await tab(other, /建筑/, '集市交换');
    await action(other, 'build', other.locator('.building-card').filter({ hasText: '集市' }).getByRole('button', { name: '建造此建筑' }));
    const beforeMarket = (await get(other, 'status')).resources.water;
    await action(other, 'market', other.locator('.offer').filter({ hasText: '饮水' }).getByRole('button', { name: '交换' }));
    assert.equal((await get(other, 'status')).resources.water, beforeMarket + 12);
    assert.equal((await get(other, 'market')).remaining, 0);
    report('经营预测反映温室产出，集市交换使用每日额度');

    await action(page, 'nextday', button(page, '推进到下一天'));
    await day(page, 2);
    assert.equal((await get(page, 'status')).phase, 'Day');
    assert.equal((await get(other, 'status')).day, 1);
    assert.equal((await (await post(page, 'nextday', { expectedDay: 1 })).json()).ok, false);
    assert.equal((await get(page, 'status')).day, 2);
    await tab(page, /居民/, '林医生');
    await button(page, '生存日记').first().click();
    await page.locator('#diaryModal').getByText(/第1天/).waitFor();
    await button(page, '关闭').click();
    report('真实开局、早建温室、日记和自动存档，并行浏览器会话互不影响');

    const team = ['iron_guard', 'big_hu', 'lin_doctor'];
    await startExplore(page, 'abandoned_hospital', team);
    assert.equal(await button(page, '保存当前进度').isDisabled(), true);
    assert.equal((await (await post(page, 'save')).json()).ok, false);
    await move(page, /前往: 走廊/, 'corridor');
    assert.equal((await action(page, 'explore/search', button(page, '搜寻当前房间'))).combatTriggered, true);
    let combat;
    for (let turns = 0; turns < 20; turns++) {
      combat = await action(page, 'combat/action', button(page, /普通攻击/));
      if (combat.combatEnded) break;
    }
    assert.equal(combat.combatEnded, true);
    assert.equal(combat.victory, true);
    await move(page, /前往: 手术室/, 'surgery');
    const hospitalLoot = await action(page, 'explore/search', button(page, '搜寻当前房间'));
    assert.equal(hospitalLoot.rewards.find(r => r.type === 'MemoryShards').amount, 2);
    await returnToBase(page);
    report('组队、房间移动、搜寻触发战斗、逐回合取胜、拾取碎片及返城');

    await startExplore(page, 'archive_ruins', team);
    await action(page, 'explore/search', button(page, '搜寻当前房间'));
    await move(page, /前往: 大厅/, 'hall');
    await action(page, 'explore/search', button(page, '搜寻当前房间'));
    await returnToBase(page);
    let quests = await get(page, 'quests');
    assert.deepEqual(['main_1', 'main_2', 'main_3'].map(id => quest(quests, id)),
      ['Completed', 'Completed', 'Completed']);
    await startExplore(page, 'broadcast_tower', team);
    await action(page, 'explore/search', button(page, '搜寻当前房间'));
    assert.ok((await get(page, 'items')).some(item => item.id === 'tower_key'));
    await move(page, /前往: 楼梯间/, 'stairs');
    await move(page, /解锁并前往: 天线平台/, 'antenna_deck');
    assert.ok(!(await get(page, 'items')).some(item => item.id === 'tower_key'));
    await returnToBase(page);
    quests = await get(page, 'quests');
    assert.equal(quest(quests, 'main_4'), 'Completed');
    report('通过实玩完成四个主线任务并用找到的钥匙打开天线平台');

    await tab(page, /背包/, '合成工作台');
    await action(page, 'craft', button(page, '制作合成').first());
    const knife = page.locator('.panel').filter({ has: page.getByRole('heading', { name: '生锈的刀' }) }).filter({ has: button(page, '分配装备') }).last();
    await knife.getByRole('button', { name: '分配装备' }).click();
    await page.locator('#gearTarget').selectOption('iron_guard');
    await action(page, 'items/equip', button(page, '确认装备'));
    assert.equal((await get(page, 'npcs')).find(npc => npc.id === 'iron_guard').weapon, '生锈的刀');
    const inventory = await get(page, 'items');
    await action(page, 'save', button(page, '保存当前进度'));
    await button(page, '开启全新游戏').click();
    await action(page, 'newgame', page.locator('#diaryModal').getByRole('button', { name: '确认', exact: true }));
    await day(page, 1);
    assert.equal((await get(page, 'buildings')).find(b => b.id === 'greenhouse').built, false);
    for (let i = 0; i < 2; i++) {
      await action(page, 'load', button(page, '读取历史存档'));
      await day(page, 2);
      assert.deepEqual(await get(page, 'items'), inventory);
    }
    await stopServer();
    await startServer();
    await page.reload({ waitUntil: 'domcontentloaded' });
    await day(page, 2);
    assert.deepEqual(await get(page, 'items'), inventory);
    assert.equal(quest(await get(page, 'quests'), 'main_4'), 'Completed');
    assert.equal((await get(other, 'status')).day, 1);
    assert.equal((await get(page, 'npcs')).find(npc => npc.id === 'iron_guard').weapon, '生锈的刀');
    assert.ok((await get(page, 'npcs')).find(npc => npc.id === 'iron_guard').fatigue > 20);
    report('远征疲劳、装备随存档恢复，重开和连续读档状态正确');

    const mobile = await browser.newContext({
      viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true
    });
    const small = await open(mobile);
    await small.locator('.tabs').getByRole('button', { name: /探索\/战斗/ }).tap();
    await small.locator('#mapChoices').waitFor();
    const widths = await small.evaluate(() => ({
      viewport: innerWidth, document: document.documentElement.scrollWidth,
      content: document.querySelector('#page').getBoundingClientRect().width
    }));
    assert.equal(widths.document, widths.viewport);
    assert.ok(widths.content >= 340);
    await small.setViewportSize({ width: 320, height: 740 });
    assert.equal(await small.evaluate(() => document.documentElement.scrollWidth), 320);
    await small.setViewportSize({ width: 390, height: 844 });
    if (process.env.LAST_ARCHIVE_SCREENSHOTS) {
      await small.locator('.tabs').getByRole('button', { name: '概览' }).tap();
      await small.getByText('今日补给预测').waitFor();
      await small.screenshot({ path: path.join(process.env.LAST_ARCHIVE_SCREENSHOTS, 'mobile.png'), fullPage: true });
    }
    report('320—390 px 触屏可打开探索，页面无横向溢出');

    const cookie = (await first.cookies()).find(x => x.name === 'last_archive_player');
    assert.equal(cookie.httpOnly, true);
    const savePath = path.join(saveDirectory, cookie.value + '.json');
    const saved = JSON.parse(await fs.readFile(savePath, 'utf8'));
    await page.locator('.tabs').getByRole('button', { name: '概览' }).click();
    await button(page, '选择封存记忆').click();
    const chosen = await action(page, 'ending/choose', page.locator('#diaryModal').getByRole('button', { name: '确认', exact: true }));
    assert.equal(chosen.ending.type, 'Silence');
    await page.getByText('永恒沉默', { exact: false }).first().waitFor();
    assert.equal((await get(page, 'status')).isVictory, true);
    assert.equal(JSON.parse(await fs.readFile(savePath, 'utf8')).ChosenEnding, 'Silence');
    report('可主动封存记忆并存下对应结局');
    await stopServer();
    // Reuse the completed playthrough with a full backpack and a late-game checkpoint.
    while (saved.Items.length < 30) saved.Items.push(saved.Items.find(item => item.Id === 'herb_paste'));
    saved.CurrentDay = 15;
    saved.LastSettledDay = 14;
    await fs.writeFile(savePath, JSON.stringify(saved));
    await startServer();
    await page.reload({ waitUntil: 'domcontentloaded' });
    await day(page, 15);
    const before = (await get(page, 'status')).resources;
    const fullCraft = await (await post(page, 'craft', { recipeId: 'herb_paste' })).json();
    assert.equal(fullCraft.ok, false);
    assert.deepEqual((await get(page, 'status')).resources, before);
    assert.equal((await get(page, 'items')).length, 30);
    const victory = await action(page, 'nextday', button(page, '推进到下一天'));
    assert.equal(victory.gameOver, true);
    assert.equal(victory.victory, true);
    assert.ok(victory.ending.title);
    report('满背包合成失败无材料损失，主线完成的第 15 天结局正确判胜');

    const autoResult = other.waitForResponse(r => r.url() === base + '/api/nextday' && r.request().method() === 'POST');
    await button(other, '开启自动游玩').click();
    await other.locator('#diaryModal').getByRole('button', { name: '确认', exact: true }).click();
    assert.equal((await (await autoResult).json()).day, 2);
    await day(other, 2);
    await button(other, '停止自动').click();
    report('自动游玩也能进入下一天并停止');
    await verifySaveTransfer(browser, base);
    assert.deepEqual(errors, []);
  } catch (error) {
    console.error(serverOutput);
    throw error;
  } finally {
    await browser.close();
    await stopServer();
    await fs.rm(directory, { recursive: true, force: true });
  }
}

main().catch(error => {
  console.error(error);
  process.exitCode = 1;
});
