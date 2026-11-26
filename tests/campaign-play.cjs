const assert = require('node:assert/strict');
const { webkit } = require('playwright');

async function playCampaign(browser, base, name, play) {
  const context = await browser.newContext({ viewport: { width: 1280, height: 750 } });
  const page = await context.newPage();
  page.setDefaultTimeout(20_000);
  const button = (label, within = page) => within.getByRole('button', {
    name: label, exact: typeof label === 'string'
  });
  const report = { name, trips: [], battles: [], purchases: [] };

  async function get(route) {
    const response = await page.request.get(base + '/api/' + route);
    assert.equal(response.status(), 200, route);
    return response.json();
  }
  const status = () => get('status');
  async function act(route, control) {
    const [response] = await Promise.all([
      page.waitForResponse(res => res.url() === base + '/api/' + route && res.request().method() === 'POST'),
      control.click()
    ]);
    assert.equal(response.status(), 200, route);
    const result = await response.json();
    assert.equal(result.ok, true, route + ': ' + (result.msg || ''));
    await page.waitForFunction(() => !document.querySelector('#loadBtn').disabled);
    return result;
  }
  const tab = label => button(label, page.locator('.tabs')).click();
  async function nextDay() {
    const result = await act('nextday', button('推进到下一天'));
    assert.equal(result.saved, true);
    return result.day;
  }
  async function build(label) {
    await tab('建筑');
    await act('build', button('建造此建筑', page.locator('.building-card').filter({ hasText: label })));
  }
  async function trade(label) {
    await tab('建筑');
    await act('market', button('交换', page.locator('.offer').filter({ hasText: label })));
    report.purchases.push(label);
  }
  async function opening(scenario) {
    await button('进度管理').click();
    await button('开启全新游戏').click();
    await page.locator(`input[name=opening][value="${scenario}"]`).check();
    await act('newgame', button('确认', page.locator('#diaryModal')));
    assert.equal((await status()).campaign.scenario, scenario);
  }
  async function assignDuty(kind, id) {
    await tab('概览');
    await page.locator('#shift-' + kind).selectOption(id);
    await act('campaign/duty', page.locator('[data-action=assign-duty][data-id="' + kind + '"]'));
    assert.equal((await status()).campaign.duty.workerId, id);
    report.duty = { kind, id };
  }
  async function restore(checkpoint) {
    await page.locator('#importSaveFile').setInputFiles({
      name: 'checkpoint.json', mimeType: 'application/json', buffer: Buffer.from(JSON.stringify(checkpoint))
    });
    await act('save/import', button('确认', page.locator('#diaryModal')));
  }
  async function depart(map, team) {
    const before = (await status()).resources;
    await tab('探索/战斗');
    const option = page.locator('#mapChoices [data-id="' + map + '"]');
    await option.click();
    await option.and(page.locator('[aria-pressed=true]')).waitFor();
    const details = await get('explore/status');
    for (const npc of team) await page.locator('input[name=exploreTeamCheckbox][value="' + npc + '"]').check();
    await act('explore/start', button('开启野外探索'));
    const after = await status();
    assert.equal(before.food - after.resources.food, details.foodCost);
    assert.equal(before.water - after.resources.water, details.waterCost);
    report.trips.push({ map, day: after.day, team, food: details.foodCost, water: details.waterCost });
  }
  async function move(name, id) {
    await act('explore/move', button(new RegExp('前往: ' + name)));
    assert.equal((await get('explore/status')).roomId, id);
  }
  const search = () => act('explore/search', button('搜寻当前房间'));
  const returnHome = () => act('explore/return', button('携带物资返回安全屋'));

  async function fight(style) {
    const before = await get('combat/status');
    assert.equal(before.inCombat, true);
    const moves = [];
    let result;
    for (let turn = 0; turn < 35; turn++) {
      const combat = await get('combat/status');
      const active = combat.timeline[combat.currentIndex];
      const foes = combat.timeline.filter(unit => !unit.isPlayerSide && !unit.isDead);
      const heavy = foes.find(unit => unit.intent.includes('重击'));
      const targeted = heavy?.intent.endsWith(active.name);
      const canCover = heavy && !targeted && !active.skillUsed && active.role === 'Guard' &&
        !combat.timeline.some(unit => unit.isCovering);
      const support = foes.find(unit => ['toxic_spore', 'shadow_swarm'].includes(unit.id));
      let target = support || foes[0];
      let action = '普通攻击';
      if (canCover && style === 'cover') {
        action = '挺身挡击';
      } else if (heavy && !active.skillUsed && active.role === 'Engineer' && style === 'interrupt') {
        action = '机械重载';
        target = heavy;
      } else if (heavy && !active.skillUsed && active.role === 'Scout') {
        action = '搭建护盾挡板';
      } else if (!active.skillUsed && active.role === 'Doctor' &&
        combat.timeline.some(unit => unit.isPlayerSide && !unit.isDead && unit.hp < 58)) {
        action = '群体治疗';
      } else if (targeted && active.hp < 35) {
        const medicine = (await status()).resources.medicine;
        action = medicine ? '使用药品' : '防御';
      } else if (canCover) {
        action = '挺身挡击';
      }
      if (action === '普通攻击' || action === '机械重载') {
        await page.locator('.unit-select[data-id="' + target.targetIndex + '"]').click();
      }
      result = await act('combat/action', button(action));
      moves.push(action);
      if (result.combatEnded) break;
    }
    assert.ok(result?.victory, result?.combatLog);
    const survivors = await get('npcs');
    report.battles.push({
      actions: moves, log: result.combatLog,
      netHpLost: before.timeline.filter(unit => unit.isPlayerSide)
        .reduce((sum, unit) => sum + unit.hp - survivors.find(npc => unit.id === npc.id).health, 0)
    });
    return result.combatLog;
  }

  async function hospital(team, style) {
    await depart('abandoned_hospital', team);
    await move('走廊', 'corridor');
    assert.ok(await button(/手术室/).isDisabled());
    assert.equal((await search()).combatTriggered, true);
    const first = await get('combat/status');
    assert.equal(first.timeline.filter(unit => !unit.isPlayerSide).length, 2);
    await fight(style);
    await move('手术室', 'surgery');
    assert.equal((await search()).rewards.find(loot => loot.type === 'MemoryShards').amount, 4);
    await returnHome();
  }
  async function park(team) {
    await depart('ruined_park', team);
    await move('喷泉广场', 'fountain');
    await move('温室遗迹', 'greenhouse');
    assert.equal((await search()).rewards.find(loot => loot.type === 'Food').amount, 16);
    await move('喷泉广场', 'fountain');
    await move('小树林', 'grove');
    assert.equal((await search()).rewards.find(loot => loot.type === 'MemoryShards').amount, 2);
    await returnHome();
  }
  async function subway(team) {
    await depart('subway_ruins', team);
    await move('控制室', 'control_room');
    assert.equal((await search()).rewards.find(loot => loot.type === 'MemoryShards').amount, 2);
    await move('站台', 'platform');
    await move('隧道', 'tunnel');
    await move('物资仓库', 'storage');
    assert.equal((await search()).rewards.find(loot => loot.type === 'Parts').amount, 10);
    await returnHome();
  }
  async function shelter(team) {
    await depart('underground_shelter', team);
    await move('走廊', 'corridor_b');
    await move('物资库', 'supply_room');
    assert.equal((await search()).rewards.find(loot => loot.type === 'Water').amount, 18);
    await returnHome();
  }
  async function relay(team) {
    await depart('relay_station', team);
    await move('中继站', 'relay_building');
    await move('屋顶', 'relay_roof');
    assert.equal((await search()).rewards.find(loot => loot.type === 'MemoryShards').amount, 4);
    await returnHome();
  }
  async function archive(team) {
    await depart('archive_ruins', team);
    assert.equal((await get('quests')).find(quest => quest.id === 'main_3').status, 'Active');
    await search();
    await move('大厅', 'hall');
    await search();
    assert.equal((await get('quests')).find(quest => quest.id === 'main_3').status, 'Active');
    await returnHome();
    assert.match((await get('explore/status')).maps.find(map => map.id === 'broadcast_tower').lockedReason, /次日/);
  }
  async function heal(id) {
    await tab('居民');
    await act('heal', page.locator('[data-action=heal][data-id="' + id + '"]'));
  }
  async function treatCriticalResidents(initial) {
    const critical = state => state.campaign.care.find(person => person.afterCare < state.campaign.criticalHealth);
    const first = critical(initial);
    await tab('背包');
    await page.locator('[data-action=use][data-id=herb_paste]').first().click();
    await page.locator('#gearTarget').selectOption(first.id);
    await act('items/use', button('确认使用'));
    report.fieldCare = [first.name];

    let current = await status();
    if (critical(current) && !current.resources.medicine) {
      await depart('abandoned_hospital', ['dawn_scout', 'old_zhou']);
      await move('走廊', 'corridor');
      await move('药房', 'pharmacy');
      assert.equal((await search()).rewards.find(item => item.type === 'Medicine').amount, 4);
      await returnHome();
      report.pharmacyTrip = true;
      current = await status();
    }
    for (let patient = critical(current); patient; patient = critical(current)) {
      assert.ok(current.resources.medicine > 0, '药品耗尽');
      await heal(patient.id);
      report.fieldCare.push(patient.name);
      const after = await status();
      assert.equal(after.resources.medicine, current.resources.medicine - 1);
      current = after;
    }
  }
  async function tower(team, style) {
    await depart('broadcast_tower', team);
    await search();
    await move('楼梯间', 'stairs');
    await move('天线平台', 'antenna_deck');
    if (style === 'engineering') {
      const before = (await status()).resources.power;
      await act('explore/bypass-tower', button('现场接线绕过守备'));
      assert.equal((await status()).resources.power, before - 4);
      assert.equal((await get('combat/status')).inCombat, false);
    } else {
      assert.equal((await search()).combatTriggered, true);
      const log = await fight(style);
      if (style === 'interrupt') assert.match(log, /打断了 .* 的蓄力/);
      if (style === 'cover') assert.match(log, /挡在 .* 身前/);
    }
    await returnHome();
    const before = await status();
    assert.equal(before.campaign.canInstall, true);
    await tab('任务');
    await act('campaign/relay', button('安装广播组件'));
    const installed = await status();
    assert.equal(installed.resources.parts, before.resources.parts - 8);
    assert.equal(installed.resources.power, before.resources.power - 5);
    assert.equal(installed.canChooseEnding, false);
    report.tower = style;
    report.testIssue = installed.campaign.relayTestIssue;
    if (report.testIssue) {
      await page.getByText(report.testIssue, { exact: true }).waitFor();
      if (report.testIssue.includes('电力')) {
        await build('发电机');
        report.powerInvestment = '发电机';
      } else {
        assert.match(report.testIssue, /重伤员/);
        await treatCriticalResidents(installed);
      }
      assert.equal((await status()).campaign.relayTestIssue, '');
    }
    await nextDay();
    assert.equal((await status()).canChooseEnding, true);
  }
  async function finish(label, type, route) {
    await tab('概览');
    await button(label).click();
    const result = await act('ending/choose', button('确认', page.locator('#diaryModal')));
    assert.equal(result.ending.type, type);
    assert.equal(result.saved, true);
    const final = await status();
    assert.equal(final.isVictory, true);
    assert.equal(final.campaign.route, route);
    report.day = final.day;
    report.ending = result.ending.type;
    report.resources = final.resources;
    report.alive = final.aliveNPCs;
  }

  try {
    await page.goto(base, { waitUntil: 'domcontentloaded' });
    await page.getByText('今日补给预测').waitFor();
    assert.equal((await status()).day, 1);
    await play({ build, trade, nextDay, status, get, opening, assignDuty, heal, treatCriticalResidents, restore,
      hospital, park, subway, shelter, relay, archive, tower, finish });
    if (report.ending) console.log('PASS campaign', JSON.stringify({
      ...report, battles: report.battles.map(({ actions, netHpLost }) => ({ actions, netHpLost }))
    }));
    else console.log('PASS', name);
    return report;
  } finally {
    await context.close();
  }
}

async function verifyScenarioStart(browser, base) {
  const context = await browser.newContext();
  try {
    const request = context.request;
    assert.equal((await request.get(base + '/')).status(), 200);
    const state = async () => (await request.get(base + '/api/status')).json();
    const restart = async scenario => (await request.post(base + '/api/newgame', { data: { scenario } })).json();
    assert.equal((await restart('WaterShortage')).ok, true);
    assert.equal((await state()).resources.water, 32, '净水故障开局的储水必须改变');
    assert.equal((await state()).resources.power, 8);
    assert.equal((await restart('MedicalShortage')).ok, true);
    assert.equal((await state()).resources.medicine, 1, '药品告急开局的药品必须改变');
    assert.equal((await (await request.post(base + '/api/nextday', { data: {} })).json()).day, 2);
    assert.equal((await restart('missing-scenario')).ok, false);
    assert.equal((await state()).day, 2, '拒绝未知局势时不能重置进度');
    const post = async (route, data) => (await request.post(base + '/api/' + route, { data })).json();
    assert.equal((await restart('WaterShortage')).ok, true);
    assert.equal((await post('explore/start', { map: 'ruined_park', team: ['old_zhou'] })).ok, true);
    assert.equal((await post('explore/return', {})).ok, true);
    assert.equal((await post('campaign/duty', { duty: 'Pump', npcId: 'old_zhou' })).ok, false);
    assert.equal((await post('campaign/duty', { duty: 'Pump', npcId: 'qin_mechanic' })).ok, true);
    assert.equal((await state()).resources.power, 5);
    assert.equal((await post('explore/start', { map: 'ruined_park', team: ['qin_mechanic'] })).ok, false);
    assert.equal((await post('campaign/duty', { duty: 'Pump', npcId: 'qin_mechanic' })).ok, false);
    const expected = (await state()).forecast.find(row => row.resource === 'Water');
    assert.equal(expected.produced, 16);
    assert.equal((await post('save', {})).ok, true);
    assert.equal((await post('load', {})).ok, true);
    assert.equal((await state()).campaign.duty.workerId, 'qin_mechanic');
    assert.equal((await post('nextday', {})).day, 2);
    const settled = await state();
    assert.equal(settled.resources.water, expected.reserve);
    assert.equal(settled.campaign.duty.kind, 'None');
    assert.equal(settled.campaign.scenario, 'WaterShortage');

    assert.equal((await restart('Standard')).ok, true);
    assert.equal((await post('explore/start', { map: 'relay_station', team: ['old_zhou'] })).ok, true);
    for (const roomId of ['relay_building', 'relay_roof'])
      assert.equal((await post('explore/move', { roomId })).ok, true);
    assert.equal((await post('explore/search', {})).ok, true);
    assert.equal((await post('explore/return', {})).ok, true);
    for (const id of ['greenhouse', 'water_station', 'market'])
      assert.equal((await post('build', { id })).ok, true, id);
    const page = await context.newPage();
    await page.setViewportSize({ width: 320, height: 568 });
    await page.goto(base);
    await page.locator('.tabs').getByRole('button', { name: '建筑', exact: true }).click();
    for (const id of ['Homestead', 'Caravan']) {
      const choice = page.locator('[data-action=plan-supply][data-id="' + id + '"]');
      await choice.click();
      await choice.and(page.locator('[aria-pressed=true]')).waitFor();
      assert.equal((await state()).campaign.plannedRoute, id);
    }
    assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth));
    assert.equal((await post('nextday', {})).day, 2);
    assert.equal((await state()).campaign.route, 'Caravan');
    console.log('PASS 320 px 下两条补给路线同时就绪时可改选，日终按最后选择签约');
  } finally {
    await context.close();
  }
}

async function verifyCampaignRoutes(browser, base) {
  await verifyScenarioStart(browser, base);
  const rescue = await playCampaign(browser, base, '药品耗尽后，转去医院药房取药并救回两名重伤员', async game => {
    await game.opening('MedicalShortage');
    await game.heal('big_hu');
    const depleted = await game.status();
    assert.equal(depleted.resources.medicine, 0);
    assert.equal(depleted.campaign.criticalResidents, 2);
    await game.treatCriticalResidents(depleted);
    assert.equal((await game.status()).campaign.criticalResidents, 0);
  });
  assert.equal(rescue.pharmacyTrip, true);
  const fightTeam = ['iron_guard', 'big_hu', 'lin_doctor'];
  const engineeringTeam = ['anna', 'old_zhou', 'fox_trader'];
  const salvageTeam = ['dawn_scout', 'qin_mechanic', 'lily_nurse'];
  const results = await Promise.all([
    playCampaign(browser, base, '本地生产＋守卫强攻', async game => {
      await game.hospital(fightTeam, 'cover');
      await game.build('温室');
      await game.build('水处理站');
      await game.nextDay();
      assert.equal((await game.status()).forecast.find(item => item.resource === 'Food').produced, 8);
      await game.archive(fightTeam);
      await game.nextDay();
      await game.tower(fightTeam, 'cover');
      await game.finish('选择重启广播', 'Broadcast', 'Homestead');
    }),
    playCampaign(browser, base, '商队补给＋接线绕行', async game => {
      await game.park(engineeringTeam);
      await game.subway(engineeringTeam);
      await game.build('集市');
      await game.nextDay();
      await game.trade('食物');
      await game.trade('饮水');
      await game.archive(engineeringTeam);
      await game.nextDay();
      await game.tower(engineeringTeam, 'engineering');
      await game.finish('选择封存记忆', 'Silence', 'Caravan');
    }),
    playCampaign(browser, base, '先囤补给＋轮换队伍争取上传', async game => {
      await game.park(salvageTeam);
      await game.shelter(['lin_doctor', 'big_hu', 'anna']);
      await game.build('温室');
      await game.build('水处理站');
      await game.nextDay();
      await game.subway(salvageTeam);
      await game.nextDay();
      await game.archive(salvageTeam);
      await game.hospital(['anna', 'shen_mu', 'mei_cook'], 'cover');
      await game.nextDay();
      await game.relay(['old_zhou', 'big_hu', 'lin_doctor']);
      await game.tower(['dawn_scout', 'qin_mechanic', 'iron_guard'], 'interrupt');
      await game.finish('选择上传意识', 'Upload', 'Homestead');
    })
  ]);
  assert.deepEqual(results.map(result => result.day), [4, 4, 5]);
  assert.deepEqual(results.map(result => result.battles.length), [2, 0, 2]);
  for (const scenario of ['WaterShortage', 'MedicalShortage']) {
    await compareTowerApproaches(browser, base, scenario);
  }
}

async function compareTowerApproaches(browser, base, scenario) {
  const scouts = ['anna', 'fox_trader'];
  const water = scenario === 'WaterShortage';
  let checkpoint, reserves;
  async function playTower(game, bypass) {
    assert.deepEqual((await game.status()).resources, reserves);
    assert.equal((await game.status()).day, 3);
    await game.assignDuty(water ? 'Pump' : 'Triage', water ? 'old_zhou' : 'lin_doctor');
    const team = bypass ? ['qin_mechanic', 'anna'] : water ? ['iron_guard', 'big_hu', 'lin_doctor'] :
      ['anna', 'big_hu', 'mei_cook'];
    if (!water) {
      const eligible = (await game.get('explore/status')).eligible;
      assert.ok(!eligible.includes('iron_guard') && !eligible.includes('lin_doctor'));
    }
    await game.tower(team, bypass ? 'engineering' : 'assault');
    await game.finish('选择重启广播', 'Broadcast', 'Homestead');
  }
  const assault = await playCampaign(browser, base, scenario + ' / 强攻', async game => {
    await game.opening(scenario);
    await game.park(scouts);
    await game.subway(scouts);
    await game.build('温室');
    await game.build('水处理站');
    await game.nextDay();
    await game.archive(scouts);
    await game.nextDay();
    checkpoint = await game.get('save/export');
    reserves = (await game.status()).resources;
    await playTower(game, false);
  });
  const bypass = await playCampaign(browser, base, scenario + ' / 接线', async game => {
    await game.restore(checkpoint);
    await playTower(game, true);
  });
  assert.equal(assault.battles.length, 1);
  assert.equal(bypass.battles.length, 0);
  assert.equal(assault.powerInvestment, undefined);
  assert.equal(bypass.powerInvestment, water ? '发电机' : undefined);

  const adapted = await playCampaign(browser, base, scenario + ' / 提前安排', async game => {
    await game.opening(scenario);
    await game.park(scouts);
    await game.subway(scouts);
    await game.build('温室');
    await game.build('水处理站');
    await game.nextDay();
    if (!water) await game.assignDuty('Triage', 'lin_doctor');
    await game.archive(scouts);
    if (water) await game.shelter(['big_hu', 'old_zhou']);
    await game.nextDay();
    if (!water) assert.ok((await game.get('explore/status')).eligible.includes('iron_guard'));
    await game.tower(water ? ['old_zhou', 'anna'] : ['iron_guard', 'big_hu', 'lin_doctor'],
      water ? 'engineering' : 'assault');
    await game.finish('选择重启广播', 'Broadcast', 'Homestead');
  });
  assert.equal(adapted.day, 4);
  assert.equal(adapted.powerInvestment, undefined);
  assert.equal(adapted.trips.length, (water ? 5 : 4) + (adapted.pharmacyTrip ? 1 : 0));
}

if (require.main === module) {
  const base = process.argv[2];
  if (!base) throw new Error('Usage: node tests/campaign-play.cjs <base URL>');
  (async () => {
    const browser = await webkit.launch();
    try {
      await verifyCampaignRoutes(browser, base.replace(/\/$/, ''));
    } finally {
      await browser.close();
    }
  })().catch(error => {
    console.error(error);
    process.exitCode = 1;
  });
}

module.exports = { verifyCampaignRoutes };
