'use strict';

const $ = id => document.getElementById(id);
const views = ['档案城概览', '居民', '建筑', '任务档案', '背包', '派系', '危机', '内置 AI', '探索与战斗'];
const resources = { Food: '食物', Water: '饮水', Power: '电力', Medicine: '药品', Parts: '零件', MemoryShards: '记忆碎片' };
const roles = { Doctor: '医生', Engineer: '工程师', Scout: '侦察员', Guard: '守卫', Worker: '工人', Scholar: '学者', Soldier: '卫士', Child: '孩子', Civilian: '居民', Trader: '商人', Farmer: '农夫', Stranger: '外来者' };
const npcStatuses = { Idle: '待命', Working: '执行任务', Injured: '养伤', Missing: '失踪', Dead: '已故' };
const questStatuses = { Active: '进行中', Completed: '已完成', NotStarted: '待开启', Failed: '失败' };
const crisisTypes = { Famine: '饥荒', Plague: '瘟疫', PowerOutage: '停电', Riot: '骚乱', MemoryStorm: '记忆风暴', RaiderRaid: '袭击' };
const endings = [
  { type: 'Broadcast', title: '重启广播', desc: '公布档案城坐标，邀请幸存者加入。' },
  { type: 'Silence', title: '封存记忆', desc: '关闭广播，守住居民的隐私与档案。' },
  { type: 'Upload', title: '上传意识', desc: '用记忆碎片建立数字档案，让居民的记忆继续存续。' }
];
const tabs = [...document.querySelectorAll('.tabs button')];
const progressDialog = $('progressModal');
const narrowLayout = window.matchMedia('(max-width: 680px)');
const shortDesktop = window.matchMedia('(min-width: 761px) and (max-height: 500px)');
const maxSaveFileBytes = 2 * 1024 * 1024;
let pendingSaveFile = null;

const state = {
  tab: 0, status: null, npcs: [], quests: [], crises: [], allies: new Set(),
  map: 'abandoned_hospital', mapPickerOpen: false, questFilter: 'Active', residentFilter: 'all',
  busy: false, autoTimer: null, auto: false, viewVersion: 0, canDepart: false,
  targetIndex: 0, combatLog: '', saveLabel: ''
};

const escapes = { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' };
const escapeHtml = value => String(value ?? '').replace(/[&<>"']/g, character => escapes[character]);
const resName = key => resources[key] || key;
const roleName = key => roles[key] || '居民';
const plus = value => value > 0 ? '+' + value : value === 0 ? '—' : String(value);
const pill = (content, tone = '') => `<span class="pill ${tone}">${escapeHtml(content)}</span>`;
const disabled = condition => condition ? ' disabled' : '';
const price = entries => entries.map(entry => `${resName(entry.resource)} ${entry.amount}`).join(' · ');
const note = (message, error = false) => {
  $('actionFeedback').textContent = message || '';
  $('actionFeedback').classList.toggle('error', error);
};

async function request(route, options) {
  const response = await fetch('/api/' + route, options);
  if (!response.headers.get('Content-Type')?.includes('application/json')) {
    throw new Error('通讯失败，请重试。');
  }
  const body = await response.json();
  if (!response.ok) throw new Error(body.error || '通讯失败，请重试。');
  return body;
}
const get = route => request(route);
const post = (route, body = {}) => request(route, {
  method: 'POST', headers: { 'Content-Type': 'application/json' },
  body: body instanceof File ? body : JSON.stringify(body)
});

function loadTheme() {
  try { return localStorage.getItem('theme') === 'light'; } catch { return false; }
}
function switchTheme(light) {
  document.documentElement.classList.toggle('light', light);
  document.querySelector('meta[name="theme-color"]').content = light ? '#eaf0f3' : '#182136';
  const label = light ? '切换深色主题' : '切换浅色主题';
  $('themeToggleBtn').setAttribute('aria-label', label);
  $('themeToggleBtn').title = label;
  try { localStorage.setItem('theme', light ? 'light' : 'dark'); } catch {}
}

function syncControls() {
  const status = state.status;
  $('advanceBtn').disabled = state.busy || !status?.canSave || status.isGameOver;
  $('advanceBtn').title = status && !status.canSave ? '返回基地后可推进' : '结算今天并保存';
  $('saveBtn').disabled = state.busy || !status?.canSave;
  $('loadBtn').disabled = state.busy || !status;
  $('exportSaveBtn').disabled = state.busy || !status?.canSave;
  $('importSaveBtn').disabled = state.busy || !status?.canSave;
  $('newGameBtn').disabled = state.busy || !status;
  $('autoplayBtn').disabled = !state.auto && (state.busy || !status?.canSave || status.isGameOver);
  $('saveState').textContent = state.saveLabel || (status && !status.canSave ? '远征结束后可存档' : '');
}

function headline() {
  const status = state.status;
  $('title').textContent = status.isGameOver ? `第${status.day}天 · 已结局` :
    `第${status.day}天 · ${{ Day: '白天', Night: '夜晚', Summary: '结算' }[status.phase] || status.phase}`;
  $('statusDot').dataset.state = status.phase;
  $('resbar').innerHTML = status.forecast.map(item => {
    const change = item.produced - item.used;
    const tone = item.shortfall ? 'bad' : change < 0 ? 'warn' : change > 0 ? 'good' : 'muted';
    const caption = item.shortfall ? `缺 ${item.shortfall}` : plus(change);
    return `<div class="resource" data-resource="${escapeHtml(item.resource)}"><span class="resource-name"><i aria-hidden="true"></i>${resName(item.resource)}</span>
      <div class="reading"><strong>${item.stock}</strong>${change || item.shortfall ? `<span class="delta ${tone}" title="本日结算预计变化">${caption}</span>` : ''}</div></div>`;
  }).join('');
  syncControls();
}

function questObjective(objective) {
  const label = escapeHtml(objective.label || objective.target || '完成目标');
  return `<div class="objective"><div class="metric"><span>${label}</span><span>${objective.current}/${objective.required}</span></div>
    <progress aria-label="${label}" max="${Math.max(1, objective.required)}" value="${objective.current}"></progress></div>`;
}

function briefing() {
  const target = $('briefing');
  const main = state.quests.filter(quest => quest.type === 'MainQuest');
  const completed = main.filter(quest => quest.status === 'Completed').length;
  target.innerHTML = `<header><h2>主线进程</h2><span class="chapter-count">${completed} / ${main.length}</span></header>
    <ol class="chapter-track">${main.map(quest => `<li data-status="${escapeHtml(quest.status)}"${quest.status === 'Active' ? ' aria-current="step"' : ''}>
      <div><strong>${escapeHtml(quest.title)}</strong><small>${questStatuses[quest.status] || ''}</small></div>
      ${quest.status === 'Active' ? quest.objectives.map(questObjective).join('') : ''}</li>`).join('')}</ol>
    <button data-tab="3" data-quest-filter="MainQuest" class="quiet">查看主线档案</button>`;
}

async function refreshLog() {
  const { log } = await get('log');
  $('eventCount').textContent = log.length ? `${log.length} 条` : '';
  $('log').innerHTML = log.length ? log.slice().reverse().map(item => `<li>${escapeHtml(item)}</li>`).join('') :
    '<li>今天还没有行动记录。</li>';
}

async function refresh() {
  const [status, npcs, quests, crisis] = await Promise.all(['status', 'npcs', 'quests', 'crisis'].map(get));
  state.status = status;
  state.npcs = npcs;
  state.quests = quests;
  state.crises = crisis.crises;
  headline();
  briefing();
  await Promise.all([renderPage(), refreshLog()]);
}

function showTab(tab) {
  if (!views[tab]) return;
  if (state.tab !== tab) {
    state.tab = tab;
    state.mapPickerOpen = false;
    window.scrollTo(0, 0);
  }
  for (const button of tabs) {
    if (+button.dataset.tab === tab) button.setAttribute('aria-current', 'page');
    else button.removeAttribute('aria-current');
  }
  return renderPage().catch(error => note(error.message, true));
}

function section(title, detail = '') {
  return `<header class="section-heading"><h2>${escapeHtml(title)}</h2>${detail ? `<p>${escapeHtml(detail)}</p>` : ''}</header>`;
}

function relayPanel() {
  const campaign = state.status.campaign;
  if (campaign.canInstall) return `<section class="panel planning"><header><h2>安装广播组件</h2></header>
    <p>安装消耗 ${campaign.relayParts} 零件、${campaign.relayPower} 电力。日终另需 ${campaign.relayTestPower} 电力试送电。</p>
    <div class="row-actions"><button class="primary" data-action="install-relay"${disabled(!campaign.canAffordRelay)}>
      安装广播组件</button>${campaign.canAffordRelay ? '' : '<span class="muted">材料不足</span>'}</div></section>`;
  if (campaign.relayAwaitingTest) {
    const needsCare = campaign.scenario === 'MedicalShortage' && campaign.care.some(person => person.afterCare < campaign.criticalHealth);
    return `<section class="panel planning"><h2>广播组件已安装</h2><p>${campaign.relayTestIssue ?
      escapeHtml(campaign.relayTestIssue) : `日终将使用 ${campaign.relayTestPower} 电力试送电。`}</p>
      ${needsCare ? '<p>可在居民面板用药。缺药时，可派人去废弃医院的药房。</p><button data-tab="1">查看居民伤情</button>' : ''}</section>`;
  }
  return '';
}

function dutyPanel() {
  const { duties, duty, openings, scenario, criticalResidents, criticalHealth, care } = state.status.campaign;
  if (!duties.length || state.status.isGameOver) return '';
  const opening = openings.find(option => option.id === scenario);
  const worker = state.npcs.find(npc => npc.id === duty.workerId);
  return `<section class="panel planning"><header><h2>营地排班</h2>${pill(opening.title)}</header>
    ${scenario !== 'Standard' ? `<p>${escapeHtml(opening.desc)}${criticalResidents ? ` 现有 ${criticalResidents} 位重伤员。` : ''}</p>` : ''}
    ${care.length ? `<ul class="care-list">${care.map(patient => `<li>${escapeHtml(patient.name)} · 生命 ${patient.health} → ${patient.afterCare}
      ${patient.treated ? ' · 值班救治' : ''}${patient.afterCare < criticalHealth ? ' · 日终仍需治疗' : ''}</li>`).join('')}</ul>
      <button data-tab="1">查看居民伤情</button>` : ''}
    ${worker ? `<p>${escapeHtml(worker.name)} · ${escapeHtml(duties.find(option => option.id === duty.kind)?.title)}，${duty.canRun ? '日终交班' : '当前无法履职'}。</p>` :
    duties.map(option => `<div class="shift"><h3>${escapeHtml(option.title)}</h3><p>${escapeHtml(option.desc)}</p>
      <label for="shift-${option.id}">${escapeHtml(option.requirement)} · 今天留守</label>
      <div class="row-actions"><select id="shift-${option.id}"${disabled(!option.candidates.length)}>
        ${option.candidates.map(npc => `<option value="${escapeHtml(npc.id)}">${escapeHtml(npc.name)}</option>`).join('') || '<option>没有可留守的居民</option>'}</select>
        <button data-action="assign-duty" data-id="${option.id}"${disabled(!option.affordable || !option.candidates.length || !state.status.canSave)}>安排值班</button>
        ${!option.affordable ? '<small class="warn">物资不足</small>' : ''}</div></div>`).join('')}</section>`;
}

function overview() {
  const status = state.status;
  if (status.isGameOver) {
    const opening = status.campaign.openings.find(option => option.id === status.campaign.scenario);
    const route = { Homestead: '本地生产', Caravan: '商队补给' }[status.campaign.route];
    const wounded = status.campaign.care.filter(patient => patient.health < status.campaign.criticalHealth);
    return `<article class="panel ending"><header><h2>${escapeHtml(status.ending?.title || '档案已封存')}</h2>
      ${pill(status.isVictory ? '旅程完成' : '行动终止', status.isVictory ? 'good' : 'bad')}</header>
      <p>${escapeHtml(status.ending?.epilogue || '这段旅程已经结束。')}</p>
      <p>第 ${status.day} 天 · ${escapeHtml(opening.title)}${route ? ' · ' + route : ''} · ${status.aliveNPCs} 位居民幸存</p>
      <p>完成 ${status.totalExplorations} 次远征，赢得 ${status.totalCombats} 场战斗。${wounded.length ?
        '仍需救治的居民有' + wounded.map(patient => escapeHtml(patient.name)).join('、') + '。' : ''}</p>
      <div class="row-actions"><button class="primary" data-action="newgame">再开一局</button></div></article>`;
  }
  const need = status.forecast.some(item => item.shortfall);
  const active = state.quests.find(quest => quest.type === 'MainQuest' && quest.status === 'Active');
  const opening = status.campaign.openings.find(option => option.id === status.campaign.scenario);
  const destination = status.canSave && active?.id === 'main_2' ? 2 :
    status.canSave && (status.campaign.canInstall || status.campaign.relayAwaitingTest) ? 3 : 8;
  const action = !status.canSave ? '返回远征' : destination === 2 ? '规划补给路线' : destination === 3 ? '查看广播组件' : '查看探索地图';
  const priorities = status.forecast.filter(item => ['Food', 'Water', 'Power'].includes(item.resource));
  const endingOptions = status.canChooseEnding ? `<section id="ending-choices" class="panel ending planning">
    <header><h2>记忆的去向</h2>${pill('广播塔已修复', 'good')}</header>
    <p>你可以现在决定结局。若继续经营到第 15 天，档案城将自动评定结局。</p>
    <div class="grid choice-grid">${endings.map(ending => `<section class="panel"><h3>${ending.title}</h3>
    <p>${ending.desc}</p>${ending.type === 'Upload' ? `<p class="cost">需要 ${status.uploadShardCost} 枚碎片</p>` : ''}
    <div class="row-actions"><button data-action="ending" data-id="${ending.type}"
      ${disabled(ending.type === 'Upload' && status.resources.shards < status.uploadShardCost)}>选择${ending.title}</button></div></section>`).join('')}</div></section>` : '';
  return `<section class="hero"><div class="hero-content">
      ${opening && status.campaign.scenario !== 'Standard' ? `<span class="hero-kicker">${escapeHtml(opening.title)}</span>` : ''}
      <h2>${escapeHtml(active?.title || '记忆的去向')}</h2>
      <p>${escapeHtml(active?.desc || '广播塔已修复。档案将如何留给后来的人，由你决定。')}</p>
      ${status.canChooseEnding ? '<a class="hero-link" href="#ending-choices">查看结局选项</a>' : `<button class="primary" data-tab="${destination}">${action}</button>`}
    </div>
  </section>
  ${relayPanel()}${endingOptions}
  <div id="supplyDock"><section id="supplySheet" class="panel planning supply-sheet"><header><h2>今日补给预测</h2>${pill(need ? '预计短缺' : '配给充足', need ? 'warn' : 'good')}</header>
    <table class="forecast"><thead><tr><th scope="col">物资</th><th scope="col">产出</th><th scope="col">消耗</th><th scope="col">结算后</th></tr></thead>
    <tbody>${priorities.map(item => `<tr><th scope="row">${resName(item.resource)}</th><td>${item.produced}</td><td>${item.used}</td>
      <td class="${item.shortfall ? 'bad' : 'good'}">${item.shortfall ? `缺 ${item.shortfall}` : `余 ${item.reserve}`}</td></tr>`).join('')}</tbody></table>
    <div class="row-actions"><button data-tab="2">规划建筑和交换</button>
      ${state.crises.length ? `<button data-tab="6">查看 ${state.crises.length} 项危机</button>` : ''}</div>
  </section></div>${dutyPanel()}`;
}

function npcMetric(label, value, color = '', max = 100) {
  return `<div><div class="progress-label"><span>${label}</span><span>${value}/${max}</span></div>
    <progress aria-label="${label}" class="${color}" max="${max}" value="${value}"></progress></div>`;
}

function relationOverview() {
  const population = new Map(state.npcs.map(npc => [npc.id, npc.name]));
  const seen = new Set();
  const links = [];
  for (const npc of state.npcs) {
    for (const relation of npc.relationships) {
      if (!population.has(relation.targetId)) continue;
      const pair = [npc.id, relation.targetId].sort().join(':');
      if (seen.has(pair)) continue;
      seen.add(pair);
      links.push({ from: npc.name, to: population.get(relation.targetId), score: relation.value });
    }
  }
  if (!links.length) return '';
  links.sort((a, b) => Math.abs(b.score) - Math.abs(a.score));
  return `<details class="panel relation-list"><summary>城内关系 · ${links.length} 对</summary>
    <div class="grid">${links.map(link => `<div class="offer"><span>${escapeHtml(link.from)} · ${escapeHtml(link.to)}</span>
      ${pill(link.score >= 0 ? '+' + link.score : link.score, link.score >= 0 ? 'good' : 'bad')}</div>`).join('')}</div></details>`;
}

function residentView() {
  const filter = state.residentFilter;
  const ready = state.npcs.filter(npc => npc.canExplore);
  const people = state.npcs.filter(npc => filter === 'all' || (filter === 'ready' && ready.includes(npc)) ||
    (filter === 'care' && npc.isAlive && (npc.health < 75 || npc.fatigue >= 55)));
  return `${relationOverview()}<div class="segmented" aria-label="筛选居民">${[['all', '全部居民'], ['ready', '待命'], ['care', '需要照料']].map(([id,label]) =>
    `<button data-action="resident-filter" data-id="${id}" aria-pressed="${filter === id}">${label}</button>`).join('')}</div>
    <div class="grid residents">${people.map(npc => `<article class="panel resident">
    <header><div class="person-name"><span class="avatar" aria-hidden="true">${escapeHtml(npc.name[0])}</span>
      <div><h2>${escapeHtml(npc.name)}</h2><span class="muted">${roleName(npc.role)}</span></div></div>
      ${pill(npcStatuses[npc.status] || npc.status, npc.status === 'Injured' ? 'warn' : '')}</header>
    <div class="person-metrics">
      ${npcMetric('生命', npc.health, npc.health < 50 ? 'bad' : 'good')}
      ${npcMetric('士气', npc.morale)}
      ${npcMetric('疲劳', npc.fatigue, npc.fatigue >= 55 ? 'warn' : '')}
    </div>
    <p class="person-desc">${escapeHtml(npc.desc)}</p>
    <div class="tags">${pill(npc.mental)}${pill('饥渴 ' + npc.hunger)}
      ${npc.weapon ? pill(npc.weapon) : ''}${npc.armor ? pill(npc.armor) : ''}</div>
    <div class="row-actions">
      ${npc.isAlive ? `<button data-action="talk" data-id="${escapeHtml(npc.id)}">交流对话</button>` : ''}
      <button data-action="diary" data-id="${escapeHtml(npc.id)}">生存日记</button>
      <button data-action="relations" data-id="${escapeHtml(npc.id)}">关系</button>
      ${npc.isAlive && npc.health < 100 ? `<button data-action="heal" data-id="${escapeHtml(npc.id)}"${disabled(!state.status.resources.medicine || state.status.isGameOver || !state.status.canSave)}>治疗</button>` : ''}
    </div></article>`).join('') || '<p class="muted">这组筛选没有居民。</p>'}</div>`;
}

function buildingView(buildings, market) {
  const campaign = state.status.campaign;
  const route = campaign.route;
  const supplyPlan = route === 'Homestead' ? '本地生产已生效。温室与水处理站每级额外产出 3 单位。' :
    route === 'Caravan' ? '商队补给已生效。集市每日多一次交换，远征出发只消耗 2 食物和 1 饮水。' :
    state.quests.some(quest => quest.id === 'main_2' && quest.status === 'Active') ?
      `温室和水处理站可提高日产。建好集市并预留 ${campaign.caravanDeposit} 零件可签商队。日终只确认一条路线，两条都就绪时请指定。` : '';
  return `${supplyPlan ? `<section class="panel stack"><h2>营地补给</h2><p>${supplyPlan}</p>
      ${campaign.supplyDecisionOpen ? `<div class="row-actions">${[['Homestead','本地生产',campaign.homesteadReady],['Caravan','商队补给',campaign.caravanReady]].map(([id,label,ready]) =>
        `<button data-action="plan-supply" data-id="${id}" aria-pressed="${campaign.plannedRoute === id}"${disabled(!ready || !state.status.canSave || state.status.isGameOver)}>${label}${campaign.plannedRoute === id ? ' · 已选' : ''}</button>`).join('')}</div>` : ''}</section>` : ''}
    <div class="grid choice-grid">${buildings.map(building => {
    const adjustable = !building.built || building.level < building.maxLevel;
    const output = building.output.map(entry => ({ ...entry, amount: entry.amount * (building.level || 1) }));
    return `<article class="panel building-card"><header><h2>${escapeHtml(building.name)}${building.built ? ` <small>Lv${building.level}</small>` : ''}</h2>
      ${pill(building.built ? '已建造' : '待建造', building.built ? 'good' : '')}</header>
      <p>${escapeHtml(building.desc)}</p>
      <div class="tags">${output.length ? pill(`${building.built ? '日产' : '建成后日产'} ${price(output)}`, 'good') : ''}
        ${building.upkeep.length ? pill('日耗 ' + price(building.upkeep), 'warn') : ''}
        ${!output.length && building.effect ? pill(building.effect) : ''}</div>
      ${adjustable ? `<p class="cost"><strong>${building.built ? '升级' : '建造'}消耗</strong> · ${price(building.cost) || '无'}${building.affordable ? '' : ' · 物资不足'}</p>
      <div class="row-actions"><button class="${building.affordable ? 'primary' : 'quiet'}" data-action="${building.built ? 'upgrade' : 'build'}"
        data-id="${escapeHtml(building.id)}"${disabled(!building.affordable || state.status.isGameOver)}>${building.built ? '升级建筑' : '建造此建筑'}</button></div>`
      : '<p class="cost">已达到最高等级</p>'}</article>`;
  }).join('')}</div>
  <section class="panel market"><header><h2>集市交换</h2>${pill(market.capacity ? `今日剩余 ${market.remaining}/${market.capacity}` : '建造集市后开放')}</header>
    <p>用闲置物资换取短缺的补给。交换额度每天恢复。</p>
    <div class="market-list">${market.offers.map(offer => `<div class="offer"><span>${price([offer.cost])} → ${price([offer.reward])}</span>
      <button data-action="trade" data-id="${escapeHtml(offer.id)}"${disabled(!offer.available || state.status.isGameOver)}>交换</button></div>`).join('')}</div>
  </section>`;
}

function questView() {
  const visible = state.quests.filter(quest => state.questFilter === 'all' || quest.status === state.questFilter ||
    (state.questFilter === 'MainQuest' && quest.type === 'MainQuest'));
  return `${relayPanel()}<div class="segmented" aria-label="筛选任务">${[['Active','进行中'], ['MainQuest','主线'], ['Completed','已完成'], ['all','全部任务']].map(([id,label]) =>
    `<button data-action="quest-filter" data-id="${id}" aria-pressed="${id === state.questFilter}">${label}</button>`).join('')}</div>
  <div class="stack">${visible.map(quest => `<article class="panel quest-card ${quest.status === 'Completed' ? 'completed' : ''}" data-kind="${escapeHtml(quest.type)}">
    <header><h2>${escapeHtml(quest.title)}</h2>${pill(quest.type === 'MainQuest' ? '主线' : '支线')}
      ${pill(questStatuses[quest.status] || quest.status, quest.status === 'Completed' ? 'good' : '')}</header>
    <p>${escapeHtml(quest.desc)}</p>${quest.objectives.map(questObjective).join('')}
    ${quest.rewards.length ? `<p class="quest-reward">奖励 · ${price(quest.rewards)}</p>` : ''}</article>`).join('') ||
    '<p class="muted">当前筛选下没有任务。选择「全部任务」可查看其余档案。</p>'}</div>`;
}

function inventoryView(items, recipes) {
  const grouped = new Map();
  for (const item of items) {
    if (!grouped.has(item.id)) grouped.set(item.id, { item, count: 0 });
    grouped.get(item.id).count++;
  }
  const workbench = `<section class="panel workbench"><header><h2>合成工作台</h2></header>
    <div class="recipe-grid choice-grid">${recipes.map(recipe => `<div class="panel"><h3>${escapeHtml(recipe.name)}</h3>
      <p class="cost">${price(recipe.cost)}</p>
      <div class="row-actions"><button data-action="craft" data-id="${escapeHtml(recipe.id)}"
        ${disabled(!recipe.available || state.status.isGameOver)}>制作合成</button></div></div>`).join('')}</div></section>`;
  return `${section('库存', `已用 ${items.length} / 30 格`)}
    <div class="grid">${[...grouped.values()].map(({item, count}) => {
      const usable = item.type === 'Consumable';
      const equipable = item.type === 'Weapon' || item.type === 'Armor';
      const stats = [item.attack ? '攻击 +' + item.attack : '', item.defense ? '防御 +' + item.defense : '',
        item.heal ? '生命 +' + item.heal : '', item.morale ? '士气 +' + item.morale : '',
        item.supplyAmount ? resName(item.supply) + ' +' + item.supplyAmount : ''].filter(Boolean);
      return `<article class="panel"><header><h3>${escapeHtml(item.name)}</h3>${pill(item.rarity + ' ×' + count)}</header>
        <p>${escapeHtml(item.desc)}</p><div class="tags">${stats.map(stat => pill(stat, 'good')).join('')}</div>
        ${usable || equipable ? `<div class="row-actions"><button data-action="${usable ? 'use' : 'equip'}"
          data-id="${escapeHtml(item.id)}" data-label="${escapeHtml(item.name)}"
          ${disabled(!state.status.canSave || state.status.isGameOver)}>${usable ? '交给居民使用' : '分配装备'}</button></div>` : ''}</article>`;
    }).join('') || '<p class="muted">背包为空，可在工作台制作补给。</p>'}</div>${workbench}`;
}

function expeditionSetup(status) {
  const maps = status.maps;
  const selection = maps.find(map => map.id === state.map) || maps[0];
  if (!selection) return '<section class="panel"><p>暂时没有开放的探索地点。</p></section>';
  state.map = selection.id;
  state.canDepart = !selection.lockedReason && status.departuresLeft > 0 && status.affordable && !state.status.isGameOver;
  const candidates = new Set(status.eligible);
  state.allies = new Set([...state.allies].filter(id => candidates.has(id)));
  const crew = state.npcs.filter(npc => npc.isAlive).sort((a,b) => Number(candidates.has(b.id)) - Number(candidates.has(a.id)) || b.combat - a.combat);
  return `<div class="muster"><section class="panel destinations">
    <header><h2>选择目的地地图</h2><span class="muted">今日可出发 ${status.departuresLeft} 次</span></header>
    <div class="site-summary"><strong>${escapeHtml(selection.name)}</strong>
      <button id="toggleMapPicker" data-action="map-picker" aria-expanded="${state.mapPickerOpen}" aria-controls="sitePicker">${state.mapPickerOpen ? '收起地点' : '更换地点'}</button></div>
    <div id="sitePicker">
    <div class="expedition-plan">
      <div class="site-photo" data-site="${escapeHtml(selection.id)}"><div class="photo-caption"><strong>${escapeHtml(selection.name)}</strong>
        <span>${selection.rooms} 间房间</span></div></div>
      <p>${escapeHtml(selection.desc)}</p>
      ${selection.cacheNotes.length ? `<p class="cache-notes">${selection.cacheNotes.map(escapeHtml).join('、')}</p>` : ''}</div>
    <div class="map-grid choice-grid" id="mapChoices">${maps.map(map => `<button class="map-option" data-action="map" data-id="${escapeHtml(map.id)}"
      aria-pressed="${map.id === state.map}"><strong>${escapeHtml(map.name)}</strong><small>${map.lockedReason ?
        escapeHtml(map.lockedReason) : '危险 ' + map.danger + ' / 5 · ' + map.loot.join('、')}</small></button>`).join('')}</div>
    </div></section>
    <section class="panel roster"><header><h2 id="crew-heading" tabindex="-1">组建队伍</h2><span id="crewCount">${state.allies.size} / 3</span></header>
    <div class="crew-grid">${crew.map(npc => {
      const eligible = candidates.has(npc.id);
      return `<label class="crew"><input type="checkbox" name="exploreTeamCheckbox" value="${escapeHtml(npc.id)}"
        ${state.allies.has(npc.id) ? ' checked' : ''}${disabled(!eligible || (state.allies.size >= 3 && !state.allies.has(npc.id)))} data-eligible="${eligible}">
        <span><strong>${escapeHtml(npc.name)}</strong><small>${roleName(npc.role)} · 生命 ${npc.health} · 疲劳 ${npc.fatigue}</small>
        <small>战斗 ${npc.combat} · 工程 ${npc.engineering} · 搜寻 ${npc.scavenging}</small>
        ${!eligible ? `<small>${npc.onDuty ? '今日留守' : npc.fatigue >= state.status.expeditionFatigueLimit ? '需要休整' : npc.status === 'Idle' ? escapeHtml(npc.mental) : npcStatuses[npc.status]}</small>` : ''}</span></label>`;
    }).join('')}</div>
    <p class="section-note">疲劳达到 ${state.status.expeditionFatigueLimit} 的居民需要留在基地休息。</p>
    <div class="launch-row"><div><strong id="selectedCrew">${escapeHtml(crewNames())}</strong>
      <small>${escapeHtml(selection.lockedReason) || (status.departuresLeft === 0 ? '今日名额已用完' :
        `消耗 ${status.foodCost} 食物、${status.waterCost} 饮水${status.affordable ? '' : ' · 补给不足'}`)}</small></div>
      <button class="primary" id="startExplorationBtn" data-action="launch"${disabled(!state.allies.size || !state.canDepart)}>开启野外探索</button></div>
  </section></div>`;
}

function expeditionView(status) {
  const skill = { engineering: '工程', medical: '医疗', combat: '战斗' };
  const check = status.activeCheck ? `<section class="panel"><header><h3>${escapeHtml(status.activeCheck.desc)}</h3>
    ${pill('难度 ' + status.activeCheck.difficulty, 'warn')}</header>
    <p>${skill[status.activeCheck.skill]}检定。选择相关技能较高的队员。</p>
    <div class="row-actions"><select id="skillCheckNPC" aria-label="执行检定的队员">
    ${status.team.map(npc => `<option value="${escapeHtml(npc.id)}">${escapeHtml(npc.name)} +${npc[status.activeCheck.skill]}</option>`).join('')}</select>
    <button class="primary" data-action="skill-check">执行检定</button></div></section>` : '';
  return `<article class="panel room-current scene-room"><div class="site-photo" data-site="${escapeHtml(status.mapId)}">
    <div class="photo-caption"><strong>${escapeHtml(status.mapName)}</strong></div></div>
    <div class="room-detail"><header><h2><small>当前位置：</small>${escapeHtml(status.roomName)}</h2>${pill(`危险 ${status.danger} / 5`, status.danger >= 4 ? 'bad' : 'warn')}</header>
    <p>${escapeHtml(status.roomDesc)}</p>
    <div class="row-actions">${status.activeCheck ? pill('待处理事件', 'warn') :
      `<button class="primary" data-action="search"${disabled(status.searched || !status.hasSurvivors || status.searchRequirement)}>搜寻当前房间</button>
        ${status.searchRequirement ? `<small class="warn">${escapeHtml(status.searchRequirement)}</small>` : ''}
        ${status.hasSurvivors ? '' : '<small class="warn">队伍无法继续行动，请结束远征。</small>'}
        ${status.offerBypass ? `<button data-action="bypass-tower"${disabled(!status.canBypass)}>现场接线绕过守备</button>
          <small class="muted">${escapeHtml(status.bypassRequirements)}${status.bypassIssue ? ' · ' + escapeHtml(status.bypassIssue) : ''}</small>` : ''}
        ${status.searched ? pill('已搜寻', 'good') : ''}`}</div></div></article>
    <div class="room-layout"><div class="stack">${check}
      <section class="panel"><h3>相邻房间</h3><div class="room-actions">
        ${status.connectedRooms.map(room => `<button data-action="move" data-id="${escapeHtml(room.id)}"
          ${disabled(!room.canEnter || status.activeCheck)}>${room.locked ? (room.canEnter ? '解锁并前往' : '已锁定') : '前往'}: ${escapeHtml(room.name)}
          <small class="muted">${room.blocked ? ' · ' + escapeHtml(room.blocked) :
            room.locked && !room.canEnter ? ' · ' + escapeHtml(room.keyName) : room.visited ? ' · 已到访' : ''}</small></button>`).join('')}
      </div></section></div>
      <section class="panel"><h3>远征小队</h3><div class="tags">${status.team.map(npc => pill(npc.name + ' · ' + npc.health + ' 生命')).join('')}</div>
        <p class="loot-summary">${status.loot.length ? '返城后入库 · ' + price(status.loot) : '队伍还没有收集到物资。'}</p>
        <p class="section-note">此刻返城 · 每位存活队员疲劳 +${status.fatigueOnReturn}</p>
        <div class="row-actions"><button data-action="return">携带物资返回安全屋</button></div></section>
    </div>`;
}

function combatView(combat, items) {
  const active = combat.timeline[combat.currentIndex];
  const foes = combat.timeline.filter(unit => !unit.isPlayerSide && !unit.isDead)
    .sort((left, right) => left.targetIndex - right.targetIndex);
  if (state.targetIndex >= foes.length) state.targetIndex = 0;
  const skills = {
    Doctor: ['群体治疗', '为每位存活队员恢复 15 点生命。'],
    Engineer: ['机械重载', '用 1.5 倍攻击力打击目标，并打断蓄力。'],
    Scout: ['搭建护盾挡板', '全队获得 12 点防御，抵挡各自下一次攻击。'],
    Guard: ['挺身挡击', '进入防御姿态，并在下次行动前替队友挡下一击。'],
    Child: ['破甲射击', '无视装甲，并有 40% 概率击晕目标。'],
    Stranger: ['洞察破绽', '降低目标 3 点防御，并造成两回合流血。']
  };
  const skill = skills[active?.role];
  const pack = [...new Map(items.filter(item => item.type === 'Consumable').map(item => [item.id, item])).values()];
  const unit = (entry, targeting, index) => {
    const content = `<strong>${escapeHtml(entry.name)}</strong>
      ${npcMetric('生命', entry.hp, entry.isPlayerSide ? 'good' : 'bad', entry.maxHp)}
      <small class="muted">攻 ${entry.attack} · 防 ${entry.defense}${entry.buffDefense ? ' +' + entry.buffDefense + ' 护盾' : ''}${entry.isDefending ? ' · 防御中' : ''}${entry.isCovering ? ' · 援护中' : ''}</small>
      ${entry.intent ? `<small class="combat-intent warn">${escapeHtml(entry.intent)}</small>` : ''}
      ${entry.incoming ? `<small class="combat-intent">${escapeHtml(entry.incoming)}</small>` : ''}
      ${entry.attackDamage ? `<small class="combat-intent">普攻预计造成 ${entry.attackDamage} 伤害</small>` : ''}`;
    return targeting ? `<button class="unit-select" aria-pressed="${state.targetIndex === index}" data-action="target" data-id="${index}">${content}</button>`
      : `<div class="unit ${entry === active ? 'current' : ''}">${content}</div>`;
  };
  const timeline = combat.timeline.slice(combat.currentIndex).concat(combat.timeline.slice(0, combat.currentIndex));
  const skillInfo = skill && !active.skillUsed ? `<p>${skill[0]}。${skill[1]}每场战斗可使用一次。</p>` : '';
  const supplies = pack.length ? `<div class="row-actions">
    <select id="combatSupply" aria-label="战斗消耗品">${pack.map(item => `<option value="${escapeHtml(item.id)}">${escapeHtml(item.name)}</option>`).join('')}</select>
    <button data-action="combat-item"${disabled(!combat.isPlayerTurn)}>使用选中物品</button></div>` : '';
  return `<section class="panel room-current combat"><header><h2>战术交战</h2>${pill(active?.name ? active.name + ' 行动' : '结算中', 'warn')}</header>
    <div class="timeline" aria-label="行动顺序">${timeline.filter(entry => !entry.isDead).map(entry =>
      `<span class="pill ${entry === active ? 'current' : ''}">${escapeHtml(entry.name)}</span>`).join('')}</div>
    <div class="battle-lines"><section class="foes"><h3>选择敌方目标</h3><div class="unit-grid">${foes.map((entry,index) => unit(entry, true, index)).join('')}</div></section>
    <section class="party"><h3>我方</h3><div class="unit-grid">${combat.timeline.filter(entry => entry.isPlayerSide).map(entry => unit(entry)).join('')}</div></section></div>
    <details class="planning"><summary>战斗记录</summary><pre class="combat-report">${escapeHtml(combat.combatLog)}</pre></details>
    <div class="battle-orders"><div class="order-context"><p>目标 <strong id="chosenEnemy">${escapeHtml(foes[state.targetIndex]?.name)}</strong></p>
      <small>药品 ${state.status.resources.medicine}</small></div><div class="row-actions">
      <button class="primary" data-action="combat" data-id="Attack"${disabled(!combat.isPlayerTurn)}>普通攻击</button>
      <button data-action="combat" data-id="Defend" title="下次行动前承受的伤害减半"${disabled(!combat.isPlayerTurn)}>防御</button>
      <button data-action="combat" data-id="UseSkill"${disabled(!combat.isPlayerTurn || !skill || active?.skillUsed)}>${skill?.[0] || '无职业技能'}</button>
      <button data-action="combat" data-id="UseMedicine"${disabled(!combat.isPlayerTurn || !state.status.resources.medicine)}>使用药品</button>
      <button class="danger" data-action="combat" data-id="Escape" title="成功率 50%"${disabled(!combat.isPlayerTurn)}>撤退</button>
      ${skillInfo || supplies ? `<details class="combat-pack"><summary>${skillInfo && supplies ? '技能与道具' : skillInfo ? '技能说明' : '道具'}</summary>
        <div class="combat-pack-content">${skillInfo}${supplies}</div></details>` : ''}
    </div></div></section>`;
}

function factionView(factions) {
  return `<div class="stack">${factions.map(faction => `<article class="panel"><header><h2>${escapeHtml(faction.name)}</h2>
    ${pill(faction.unlocked ? faction.repName + ' · ' + faction.rep : '尚未取得联系', faction.unlocked ? 'good' : '')}</header>
    <p>${escapeHtml(faction.desc)}</p><div class="tags">${pill('联系人 · ' + faction.leader)}</div></article>`).join('')}</div>`;
}

function crisisView() {
  return state.crises.length ? `<div class="stack">${state.crises.map(crisis => `<article class="panel"><header>
    <h2>${escapeHtml(crisisTypes[crisis.type] || crisis.type)}</h2>${pill('第 ' + crisis.startDay + ' 天起', 'warn')}</header>
    <p>${escapeHtml(crisis.desc)}</p></article>`).join('')}</div>` :
    '<section class="panel"><header><h2>城内平稳</h2><span class="good">暂无活跃危机</span></header><p>推进到下一天前，可在总览检查预计余量。</p><div class="row-actions"><button data-tab="0">查看补给预测</button></div></section>';
}

async function renderPage() {
  if (!state.status) return;
  const version = ++state.viewVersion;
  const tab = state.tab;
  let html;
  let context = '';
  let field = '';
  switch (tab) {
    case 0: html = overview(); context = state.status.isGameOver ? '旅程已结束' : `${state.status.aliveNPCs} 人在城`; break;
    case 1: html = residentView(); context = `${state.status.aliveNPCs} 人在城`; break;
    case 2: html = buildingView(...await Promise.all([get('buildings'), get('market')])); break;
    case 3: html = questView(); context = `${state.quests.filter(quest => quest.status === 'Active').length} 项进行中`; break;
    case 4: html = inventoryView(...await Promise.all([get('items'),get('recipes')])); break;
    case 5: html = factionView(await get('factions')); break;
    case 6: html = crisisView(); context = `${state.crises.length} 项活跃危机`; break;
    case 7: html = '<section class="panel"><h2>内置 AI</h2><p>网页试玩使用本地模拟生成对话和居民日记。</p></section>'; break;
    case 8: {
      const [combat, expedition] = await Promise.all([get('combat/status'), get('explore/status')]);
      if (combat.inCombat) html = combatView(combat, await get('items'));
      else if (expedition.isExploring) html = expeditionView(expedition);
      else html = expeditionSetup(expedition);
      context = combat.inCombat ? '交战中' : expedition.isExploring ? '远征进行中' : '基地';
      field = combat.inCombat ? 'combat' : expedition.isExploring ? 'rooms' : 'setup';
      if (state.combatLog && !combat.inCombat) html += `<section class="panel planning"><details><summary>上一次战斗记录</summary>
        <pre class="combat-report">${escapeHtml(state.combatLog)}</pre></details></section>`;
      break;
    }
  }
  if (version !== state.viewVersion) return;
  const changedField = field && document.body.dataset.field && field !== document.body.dataset.field;
  const focused = document.activeElement;
  const refocus = $('page').contains(focused) && focused.matches('button[data-action]:focus-visible');
  const pickerWasUsed = $('page').contains(focused) && focused.dataset.action === 'map' && narrowLayout.matches;
  document.body.dataset.tab = tab;
  document.body.dataset.field = field;
  $('pageTitle').textContent = views[tab];
  $('pageContext').textContent = context;
  $('resourceDock').append($('resbar'));
  if ($('page').contains($('briefing'))) $('cityIntel').prepend($('briefing'));
  $('supplySheet')?.remove();
  $('page').innerHTML = html;
  placeResources();
  placeForecast();
  if (changedField) {
    $('pageTitle').focus({ preventScroll: true });
    if (field === 'combat') $('page').querySelector('.combat').scrollIntoView({ block: 'start' });
    else $('pageTitle').scrollIntoView({ block: 'start' });
  } else if (pickerWasUsed) {
    $('toggleMapPicker')?.focus({ preventScroll: true });
    $('toggleMapPicker')?.scrollIntoView({ block: 'nearest' });
  } else if (refocus) {
    const replacement = [...$('page').querySelectorAll('button[data-action]')].find(button =>
      button.dataset.action === focused.dataset.action && button.dataset.id === focused.dataset.id);
    replacement?.focus({ preventScroll: true });
  }
}

function placeResources() {
  const belongsBelow = narrowLayout.matches && (state.tab === 0 ||
    ['setup', 'rooms'].includes(document.body.dataset.field));
  const container = belongsBelow ? $('page') : $('resourceDock');
  if ($('resbar').parentNode !== container) container.append($('resbar'));
}

narrowLayout.addEventListener('change', placeResources);

function placeForecast() {
  const sheet = $('supplySheet');
  if (!sheet) return;
  const chapters = $('briefing');
  const dock = $('supplyDock');
  const sidebar = $('cityIntel');
  if (shortDesktop.matches) {
    sidebar.prepend(sheet);
    dock.append(chapters);
  } else {
    dock.append(sheet);
    sidebar.prepend(chapters);
  }
}

shortDesktop.addEventListener('change', placeForecast);

function openDialog(title, content) {
  $('dialogHeading').textContent = title;
  $('dialogContent').innerHTML = content;
  if (!$('diaryModal').open) $('diaryModal').showModal();
}

function residentDialog(action, id) {
  const npc = state.npcs.find(entry => entry.id === id);
  if (!npc) return;
  if (action === 'diary') {
    openDialog(npc.name + '的生存日记', npc.diaries.length
      ? npc.diaries.slice().reverse().map(entry => `<p>${escapeHtml(entry)}</p>`).join('')
      : '<p>今天还没有日记。结算后可在这里回顾。</p>');
  } else {
    const relations = npc.relationships.filter(relation => relation.targetId !== 'player');
    openDialog(npc.name + '的关系', relations.length ? relations.map(relation => {
      const other = state.npcs.find(entry => entry.id === relation.targetId);
      return `<p>${escapeHtml(other?.name || relation.targetId)} · ${relation.value >= 0 ? '+' : ''}${relation.value}</p>`;
    }).join('') : '<p>当前没有已记录的关系。</p>');
  }
}

function gearDialog(action, id, label) {
  const options = state.npcs.filter(npc => npc.isAlive).map(npc =>
    `<option value="${escapeHtml(npc.id)}">${escapeHtml(npc.name)} · 生命 ${npc.health} · 士气 ${npc.morale}</option>`).join('');
  openDialog(label, `<label for="gearTarget">选择居民</label><select id="gearTarget">${options}</select>
    <div class="row-actions"><button class="primary" data-action="gear-${action}" data-id="${escapeHtml(id)}">${action === 'equip' ? '确认装备' : '确认使用'}</button></div>`);
}

function confirmation(title, copy, action, id = '') {
  openDialog(title, `<p>${escapeHtml(copy)}</p><div class="row-actions">
    <button class="primary" data-action="${action}" data-id="${escapeHtml(id)}">确认</button></div>`);
}

async function mutate(route, body, onSuccess) {
  if (state.busy) return null;
  const focusedOrder = route === 'combat/action' && document.activeElement.matches('.battle-orders button:focus-visible')
    ? document.activeElement : null;
  state.busy = true;
  syncControls();
  const locked = [...document.querySelectorAll('#page button[data-action]:enabled, #diaryModal button[data-action]:enabled')];
  locked.forEach(button => { button.disabled = true; });
  let result;
  try {
    result = await post(route, body);
    if (!result.ok) {
      note(result.msg || '操作未完成。', true);
      await refresh();
      return result;
    }
    const message = onSuccess ? onSuccess(result) : result.msg;
    await refresh();
    if (message) note(message);
  } catch (error) {
    note(error.message || '连接中断，请重试。', true);
  } finally {
    state.busy = false;
    locked.filter(button => button.isConnected).forEach(button => { button.disabled = false; });
    syncControls();
    if (focusedOrder && document.activeElement === document.body) {
      const orders = [...document.querySelectorAll('.battle-orders button:enabled')];
      const same = orders.find(button => button.dataset.action === focusedOrder.dataset.action && button.dataset.id === focusedOrder.dataset.id);
      (same || orders[0])?.focus({ preventScroll: true });
    }
  }
  return result;
}

async function exportSaveFile() {
  try {
    const result = await get('save/export');
    if (result.ok === false) { note(result.msg, true); return; }
    const blob = new Blob([JSON.stringify(result)], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = 'last-archive-day-' + result.data.CurrentDay + '.json';
    document.body.append(link);
    link.click();
    link.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
    note('存档文件已生成，导入该文件即可在其他设备继续。');
  } catch {
    note('无法导出存档，请重试。', true);
  }
}

async function selectSaveFile(event) {
  const input = event.target;
  const file = input.files[0];
  input.value = '';
  if (!file || !state.status?.canSave || state.busy) return;
  if (file.size > maxSaveFileBytes) { note('存档文件不能超过 2 MB。', true); return; }
  stopAuto();
  let preview;
  try {
    preview = JSON.parse(await file.text());
  } catch {
    note('这不是可读取的最后档案城存档。', true);
    return;
  }
  if (preview?.format !== 'last-archive-save' || !Number.isInteger(preview.data?.CurrentDay) ||
      preview.data.CurrentDay < 1) {
    note('这不是可读取的最后档案城存档。', true);
    return;
  }
  if (!state.status?.canSave || state.busy) return;
  pendingSaveFile = file;
  confirmation('导入存档文件', '此文件的进度为第 ' + preview.data.CurrentDay +
    ' 天。确认后将替换此会话的当前进度和服务器存档。', 'import-save-confirm');
}

function nextDay() {
  if (!state.status?.canSave || state.status.isGameOver) return;
  return mutate('nextday', { expectedDay: state.status.day }, result => {
    state.saveLabel = result.saved ? '自动存档 · 第 ' + result.day + ' 天' : '自动存档失败';
    if (result.gameOver) showTab(0);
    return result.ending ? result.ending.title : result.messages.join(' · ');
  });
}

function stopAuto() {
  state.auto = false;
  clearTimeout(state.autoTimer);
  state.autoTimer = null;
  $('autoplayBtn').textContent = '开启自动游玩';
  syncControls();
}
function nextAuto() {
  state.autoTimer = setTimeout(async () => {
    if (!state.auto) return;
    const result = await nextDay();
    if (!state.auto) return;
    if (!result?.ok || result.gameOver) stopAuto();
    else nextAuto();
  }, 2000);
}

function crewNames() {
  const names = state.npcs.filter(npc => state.allies.has(npc.id)).map(npc => npc.name);
  return names.length ? names.join('、') : '请选择出发队员';
}

function handleCrew(target) {
  if (target.checked) state.allies.add(target.value);
  else state.allies.delete(target.value);
  $('crewCount').textContent = `${state.allies.size} / 3`;
  $('selectedCrew').textContent = crewNames();
  $('startExplorationBtn').disabled = !state.allies.size || !state.canDepart;
  for (const checkbox of document.getElementsByName('exploreTeamCheckbox')) {
    checkbox.disabled = checkbox.dataset.eligible !== 'true' || (!checkbox.checked && state.allies.size >= 3);
  }
}

const saveActions = new Set(['save', 'load', 'newgame-confirm', 'ending-confirm']);
const simpleRoutes = { build: 'build', upgrade: 'upgrade', heal: 'heal', craft: 'craft', trade: 'market', move: 'explore/move' };
document.addEventListener('click', event => {
  const button = event.target.closest('button');
  if (!button) return;
  if (button.dataset.tab !== undefined) {
    if (button.dataset.questFilter) state.questFilter = button.dataset.questFilter;
    showTab(Number(button.dataset.tab));
    return;
  }
  const action = button.dataset.action;
  const id = button.dataset.id;
  if (!action || (state.busy && !(action === 'autoplay' && state.auto))) return;
  if (progressDialog.contains(button)) progressDialog.close();
  if (saveActions.has(action)) {
    const route = action === 'newgame-confirm' ? 'newgame' : action === 'ending-confirm' ? 'ending/choose' : action;
    if (route !== 'save') stopAuto();
    $('diaryModal').close();
    const body = action === 'newgame-confirm' ? { scenario: document.querySelector('[name="opening"]:checked').value } :
      action === 'ending-confirm' ? { type: id } : {};
    mutate(route, body, result => {
      if (route === 'save') state.saveLabel = '已保存 · 第 ' + state.status.day + ' 天';
      if (route === 'load' || route === 'newgame') {
        state.saveLabel = route === 'load' ? '已读取存档' : '';
        state.allies.clear();
        state.combatLog = '';
        if (route === 'newgame') showTab(0);
      }
      if (route === 'ending/choose') {
        showTab(0);
        state.saveLabel = result.saved ? '结局已保存' : '结局存档失败';
      }
      return result.ending?.title || result.msg;
    });
    return;
  }
  if (simpleRoutes[action]) {
    const body = action === 'move' ? { roomId: id } : action === 'craft' ? { recipeId: id } : { id };
    mutate(simpleRoutes[action], body, result => result.msg || (
      action === 'build' ? '建造完成。' : action === 'upgrade' ? '升级完成。' : action === 'heal' ? '已使用药品。' : '已抵达房间。'));
    return;
  }
  switch (action) {
    case 'export-save': exportSaveFile(); break;
    case 'import-save': $('importSaveFile').click(); break;
    case 'import-save-confirm': {
      const file = pendingSaveFile;
      pendingSaveFile = null;
      $('diaryModal').close();
      if (file) mutate('save/import', file, result => {
        state.saveLabel = '已导入 · 第 ' + result.day + ' 天';
        state.allies.clear();
        state.targetIndex = 0;
        state.combatLog = '';
        showTab(0);
        return result.msg;
      });
      break;
    }
    case 'newgame':
      stopAuto();
      openDialog('开启全新游戏', `<fieldset class="opening-choices"><legend>选择开局局势</legend>
        ${state.status.campaign.openings.map(option => `<label><input type="radio" name="opening" value="${escapeHtml(option.id)}"${option.id === 'Standard' ? ' checked' : ''}>
          <span><strong>${escapeHtml(option.title)}</strong><small>${escapeHtml(option.desc)}</small></span></label>`).join('')}</fieldset>
        <p>现有存档会保留到下次保存。</p><button class="primary" data-action="newgame-confirm">确认</button>`);
      break;
    case 'assign-duty': mutate('campaign/duty', { duty: id, npcId: $('shift-' + id).value }); break;
    case 'plan-supply': mutate('campaign/supply', { route: id }); break;
    case 'autoplay':
      if (state.auto) stopAuto();
      else confirmation('开启自动游玩', '每两秒结算并推进一天。你可以随时停止。', 'autoplay-go');
      break;
    case 'autoplay-go':
      $('diaryModal').close(); state.auto = true; $('autoplayBtn').textContent = '停止自动'; nextAuto(); syncControls(); break;
    case 'resident-filter': state.residentFilter = id; showTab(1); break;
    case 'quest-filter': state.questFilter = id; showTab(3); break;
    case 'diary': case 'relations': residentDialog(action, id); break;
    case 'talk':
      mutate('talk', { id }, result => {
        openDialog(state.npcs.find(npc => npc.id === id)?.name || '对话', `<p>${escapeHtml(result.dialogue)}</p>`);
        return '';
      });
      break;
    case 'use': case 'equip': gearDialog(action, id, button.dataset.label); break;
    case 'gear-use': case 'gear-equip': {
      const npcId = $('gearTarget').value;
      $('diaryModal').close();
      mutate(action === 'gear-use' ? 'items/use' : 'items/equip', { itemId: id, npcId });
      break;
    }
    case 'map-picker': {
      state.mapPickerOpen = !state.mapPickerOpen;
      button.setAttribute('aria-expanded', state.mapPickerOpen);
      button.textContent = state.mapPickerOpen ? '收起地点' : '更换地点';
      break;
    }
    case 'map': {
      const closePicker = narrowLayout.matches && state.mapPickerOpen;
      state.map = id;
      state.mapPickerOpen = false;
      showTab(8).then(() => {
        if (closePicker) $('toggleMapPicker')?.scrollIntoView({ block: 'nearest' });
      });
      break;
    }
    case 'launch':
      stopAuto();
      mutate('explore/start', { map: state.map, team: [...state.allies] }, () => {
        state.allies.clear(); state.combatLog = ''; state.targetIndex = 0; return '已派遣远征队。';
      });
      break;
    case 'search': mutate('explore/search', {}, result => result.desc); break;
    case 'bypass-tower': mutate('explore/bypass-tower', {}); break;
    case 'install-relay': mutate('campaign/relay', {}); break;
    case 'skill-check': mutate('explore/skillcheck', { npcId: $('skillCheckNPC').value }, result => result.resultText); break;
    case 'return': mutate('explore/return', {}, result => {
      if (!result.survived) return '远征小队全灭，营地未收到新的现场档案。';
      const cargo = result.loot.map(loot => `${resName(loot.type)} +${loot.amount}`).join(' · ');
      const injured = result.injured.map(id => state.npcs.find(npc => npc.id === id)?.name || id);
      return ['队伍已返城。', cargo && '物资入库 ' + cargo, injured.length && '本次伤亡 ' + injured.join('、')].filter(Boolean).join(' ');
    }); break;
    case 'target':
      state.targetIndex = +id;
      $('chosenEnemy').textContent = button.querySelector('strong').textContent;
      for (const target of document.querySelectorAll('.unit-select')) target.setAttribute('aria-pressed', target === button);
      break;
    case 'combat': case 'combat-item': {
      const itemId = action === 'combat-item' ? $('combatSupply').value : '';
      const combatAction = action === 'combat-item' ? 'UseItem' : id;
      mutate('combat/action', { action: combatAction, targetIndex: state.targetIndex, itemId }, result => {
        if (!result.combatEnded) return '';
        state.combatLog = result.combatLog;
        state.targetIndex = 0;
        return result.victory ? '战斗胜利。' : result.escaped ? '小队已撤离战斗。' : '小队战败。';
      });
      break;
    }
    case 'ending': {
      const ending = endings.find(entry => entry.type === id);
      confirmation(ending.title, ending.desc + ' 选择后将完成这局游戏。', 'ending-confirm', id);
      break;
    }
  }
});

document.addEventListener('change', event => {
  const target = event.target;
  if (target.name === 'exploreTeamCheckbox') handleCrew(target);
});
$('advanceBtn').addEventListener('click', nextDay);
$('progressBtn').addEventListener('click', () => progressDialog.showModal());
$('importSaveFile').addEventListener('change', selectSaveFile);
$('diaryModal').addEventListener('close', () => { pendingSaveFile = null; });
$('themeToggleBtn').addEventListener('click', () => switchTheme(!document.documentElement.classList.contains('light')));
switchTheme(loadTheme());
if (window.top !== window.self) $('openGame').hidden = false;
refresh().catch(error => {
  note(error.message || '无法连接档案城。', true);
  $('statusDot').dataset.state = 'error';
  $('title').textContent = '连接失败';
});
setInterval(() => {
  if (state.status && !state.busy && !document.hidden) refreshLog().catch(() => {});
}, 5000);
