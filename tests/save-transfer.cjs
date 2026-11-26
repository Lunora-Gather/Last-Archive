const assert = require('node:assert/strict');
const fs = require('node:fs/promises');
const { webkit } = require('playwright');

// HEAD day two: build the greenhouse, search the old city twice, then settle the day.
// Visiting the tower on the third trip finished the old two-slot chapter without installing a relay.
function preCampaignSave(file, finished) {
  const fields = [
    'Version', 'CurrentDay', 'CurrentPhase', 'Resources', 'NPCStates', 'BuildingStates', 'QuestStates',
    'ExploredMaps', 'UnlockedLocations', 'StoryFlags', 'Items', 'Factions', 'UnlockedAchievements',
    'SaveTime', 'LastSettledDay', 'StarvationDays', 'TotalDaysSurvived', 'TotalExplorationsCompleted',
    'TotalCombatsWon', 'IsGameOver', 'IsVictory', 'LastDailyEvent', 'ChosenEnding', 'MarketDay',
    'MarketTrades', 'LocationVisits', 'Rooms', 'Crises', 'AchievementDays'
  ];
  const data = structuredClone(Object.fromEntries(fields.map(key => [key, file.data[key]])));
  data.TotalExplorationsCompleted = finished ? 3 : 2;
  data.LocationVisits = { archive_ruins: 2, gate: 2, hall: 2 };
  if (finished) Object.assign(data.LocationVisits, { broadcast_tower: 1, lobby: 1 });
  data.Rooms = data.Rooms.map(({ MapId, RoomId, Locked }) => ({
    MapId, RoomId, Locked, Visited: Object.hasOwn(data.LocationVisits, RoomId)
  }));
  const amounts = { Food: 55, Water: 35, Power: finished ? 28 : 18, Medicine: 8, Parts: 25, MemoryShards: finished ? 19 : 9 };
  for (const item of data.Resources) item.Amount = amounts[item.Type];
  const chapters = {
    main_1: [5], main_2: [1], main_3: [1], main_4: [finished ? 1 : 0, 20],
    side_1: [5], side_2: [0], side_3: [0], side_4: [10], side_5: [0]
  };
  for (const quest of data.QuestStates) {
    const counts = chapters[quest.QuestId];
    if (!counts) continue;
    quest.ObjectiveProgress = counts;
    quest.Status = counts[0] ? 'Completed' : 'Active';
  }
  for (const faction of data.Factions) faction.Unlocked = faction.Type !== 'Wanderers' || finished;
  return { ...file, data };
}

async function verifySaveTransfer(browser, base) {
  const from = await browser.newContext({ acceptDownloads: true });
  const to = await browser.newContext();
  const button = (page, name) => page.getByRole('button', { name, exact: true });
  const openProgress = page => button(page, '进度管理').click();
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

  async function post(page, route, data = {}, success = true) {
    const response = await page.request.post(base + '/api/' + route, { data });
    assert.equal(response.status(), 200, route);
    const result = await response.json();
    assert.equal(result.ok, success, route + ': ' + result.msg);
    return result;
  }

  async function snapshot(page) {
    const { data } = await get(page, 'save/export');
    delete data.SaveTime;
    return data;
  }

  async function verifyLegacy(target, file) {
    for (const finished of [false, true]) {
      const legacy = preCampaignSave(file, finished);
      await post(target, 'save/import', legacy);
      const restored = await snapshot(target);
      const quest = id => restored.QuestStates.find(state => state.QuestId === id);
      assert.deepEqual(quest('main_1').ObjectiveProgress, [4]);
      assert.deepEqual(quest('main_4').ObjectiveProgress, finished ? [1, 1] : [0, 0]);
      assert.equal(quest('main_4').Status, finished ? 'Completed' : 'Active');
      assert.equal(restored.SupplyRoute, 'Homestead');
      assert.equal(restored.ArchiveRecovered, true);
      assert.equal(restored.RelayCommissioned, finished);
      assert.deepEqual(restored.Resources, legacy.data.Resources, 'completed chapters must not pay out twice');
      assert.deepEqual(restored.LocationVisits, legacy.data.LocationVisits);
      assert.deepEqual(restored.FieldedResidents, []);
      await post(target, 'save/import', await get(target, 'save/export'));
      assert.deepEqual(await snapshot(target), restored);
      if (finished) {
        // Local checkpoints loaded by RestoreSave before export can still contain HEAD's completed counts.
        const inherited = await get(target, 'save/export');
        inherited.data.QuestStates.find(q => q.QuestId === 'main_1').ObjectiveProgress = [5];
        inherited.data.QuestStates.find(q => q.QuestId === 'main_4').ObjectiveProgress = [1, 20];
        await post(target, 'save/import', inherited);
        assert.deepEqual(await snapshot(target), restored);
      }
    }
  }

  async function verifyRoster(source, target, file) {
    // Import into a different scenario with a different pool of eligible staff.
    await post(source, 'newgame', { scenario: 'WaterShortage' });
    await post(source, 'explore/start', { map: 'ruined_park', team: ['old_zhou'] });
    await post(source, 'explore/return');
    await post(source, 'campaign/duty', { duty: 'Pump', npcId: 'qin_mechanic' });
    const roster = await get(source, 'save/export');
    assert.equal(roster.data.RosterDay, roster.data.CurrentDay);
    assert.deepEqual(roster.data.FieldedResidents, ['old_zhou']);
    assert.equal(roster.data.DutyWorker, 'qin_mechanic');
    await post(target, 'save/import', roster);
    const checkpoint = await snapshot(target);
    assert.deepEqual(checkpoint, await snapshot(source));

    await post(target, 'talk', { id: 'anna' });
    const unsaved = await snapshot(target);
    assert.notDeepEqual(unsaved, checkpoint);
    const malformed = [
      ['unknown worker', { DutyWorker: 'missing' }],
      ['unqualified worker', { DutyWorker: 'echo_child' }],
      ['unavailable duty', { Scenario: 'Standard' }],
      ['worker also fielded', { DutyWorker: 'old_zhou' }],
      ['unknown fielded resident', { FieldedResidents: ['missing'] }],
      ['duplicate fielded resident', { FieldedResidents: ['old_zhou', 'old_zhou'] }],
      ['missing roster day', { RosterDay: 0 }],
      ['fielded without a departure', { DepartureDay: 0, Departures: 0 }],
      ['claimed relay installation without tower data', { RelayInstalled: true }],
      ['commissioned without installation', { RelayCommissioned: true }],
      ['chosen ending during play', { ChosenEnding: 'Broadcast' }],
      ['unknown unlocked map', { UnlockedLocations: ['missing'] }]
    ].map(([label, changes]) => [label, { ...roster, data: { ...roster.data, ...changes } }]);
    for (const counts of [[20], [0, 21], [0, 20, 0]]) {
      const legacy = preCampaignSave(file, false);
      legacy.data.QuestStates.find(quest => quest.QuestId === 'main_4').ObjectiveProgress = counts;
      malformed.push(['legacy tower ' + JSON.stringify(counts), legacy]);
    }
    const current = structuredClone(roster);
    current.data.QuestStates.find(q => q.QuestId === 'main_1').ObjectiveProgress = [5];
    malformed.push(['old shard cap on a current active chapter', current]);
    const wrongSideQuest = preCampaignSave(file, false);
    wrongSideQuest.data.QuestStates.find(q => q.QuestId === 'side_1').ObjectiveProgress.push(0);
    malformed.push(['extra side objective in legacy file', wrongSideQuest]);
    for (const [label, invalid] of malformed) {
      await post(target, 'save/import', invalid, false);
      assert.deepEqual(await snapshot(target), unsaved, label);
    }
    await post(target, 'load');
    assert.deepEqual(await snapshot(target), checkpoint, 'failed imports must preserve the stored checkpoint');

    const status = await get(target, 'status');
    const water = status.forecast.find(entry => entry.resource === 'Water');
    assert.equal(water.produced, 16);
    await post(target, 'nextday');
    const settled = await snapshot(target);
    assert.equal(settled.Resources.find(entry => entry.Type === 'Water').Amount, water.reserve);
    assert.equal(settled.CampDuty, 'None');
    assert.deepEqual(settled.FieldedResidents, []);
    await post(target, 'save/import', await get(target, 'save/export'));
    assert.deepEqual(await snapshot(target), settled, 'yesterday’s departures may outlive the roster');

    for (const scenario of ['MedicalShortage', 'Standard']) {
      await post(source, 'newgame', { scenario });
      if (scenario === 'Standard') await post(source, 'build', { id: 'med_room' });
      await post(source, 'campaign/duty', { duty: 'Triage', npcId: 'lin_doctor' });
      const triage = await get(source, 'save/export');
      if (scenario === 'MedicalShortage')
        assert.equal(triage.data.Resources.find(entry => entry.Type === 'Medicine').Amount, 0);
      await post(target, 'save/import', triage);
      assert.deepEqual(await snapshot(target), await snapshot(source), 'prepaid triage survives import');
    }
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
    await openProgress(page);
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

    await openProgress(source);
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
    await openProgress(source);
    await action(source, 'load', button(source, '读取历史存档'));
    assert.equal((await progress(source)).buildings.find(building => building.id === 'greenhouse').built, false);

    await openProgress(target);
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
    await openProgress(target);
    await action(target, 'load', button(target, '读取历史存档'));
    assert.deepEqual(await progress(target), previous);

    await chooseFile(target, upload);
    assert.equal((await importFile(target)).day, 2);
    await target.getByText('已导入 · 第 2 天', { exact: true }).waitFor();
    assert.deepEqual(await progress(target), original);
    await target.reload({ waitUntil: 'domcontentloaded' });
    await day(target, 2);
    await openProgress(target);
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
    await verifyLegacy(clean, file);
    await verifyRoster(source, clean, file);
    console.log('PASS portable transfer, pre-campaign chapters, prepaid duties, and atomic rejection of malformed saves');
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
