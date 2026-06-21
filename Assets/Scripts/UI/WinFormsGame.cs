#if WINDOWS
// ============================================================
// Last Archive - WinForms 图形界面（完整交互版）
// 零外部原生依赖，.NET 内置
// ============================================================

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace LastArchive
{
    public class WinFormsGame : Form
    {
        private GameManager _game;
        private TabControl _tabs;
        private RichTextBox _logBox;
        private Label _topLabel;
        private SplitContainer _split;
        private System.Windows.Forms.Timer _autoPlayTimer;
        private bool _autoPlaying = false;

        // 颜色
        static readonly Color BG = Color.FromArgb(18, 18, 28);
        static readonly Color PANEL = Color.FromArgb(30, 30, 45);
        static readonly Color ACCENT = Color.FromArgb(80, 180, 255);
        static readonly Color ACCENT2 = Color.FromArgb(255, 180, 60);
        static readonly Color DANGER = Color.FromArgb(255, 80, 80);
        static readonly Color SUCCESS = Color.FromArgb(80, 255, 120);
        static readonly Color TEXT = Color.FromArgb(220, 220, 230);
        static readonly Color TEXT_DIM = Color.FromArgb(140, 140, 160);
        static readonly Color BTN_BG = Color.FromArgb(50, 50, 70);
        static readonly Color BTN_HOVER = Color.FromArgb(70, 70, 100);
        private void InitUI()
        {
            // 顶部状态栏
            _topLabel = new Label { Dock = DockStyle.Top, Height = 36, BackColor = PANEL,
                ForeColor = ACCENT, Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(12, 0, 0, 0) };
            Controls.Add(_topLabel);

            // 分割容器：上面Tab，下面日志
            _split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal,
                SplitterDistance = 550, BackColor = BG, Panel1MinSize = 200, Panel2MinSize = 100 };
            Controls.Add(_split);
            _split.BringToFront();

            // Tab
            _tabs = new TabControl { Dock = DockStyle.Fill, Font = new Font("Microsoft YaHei UI", 10F) };
            AddTab("📊 概览", DrawOverview);
            AddTab("👥 居民", DrawNPCs);
            AddTab("🏗 建筑", DrawBuildings);
            AddTab("📜 任务", DrawQuests);
            AddTab("🎒 背包", DrawItems);
            AddTab("🏛 派系", DrawFactions);
            AddTab("⚠ 危机", DrawCrisis);
            _split.Panel1.Controls.Add(_tabs);

            // 日志
            _logBox = new RichTextBox { Dock = DockStyle.Fill, BackColor = PANEL, ForeColor = TEXT_DIM,
                Font = new Font("Consolas", 9F), ReadOnly = true, ScrollBars = RichTextBoxScrollBars.Vertical };
            _split.Panel2.Controls.Add(_logBox);

            // 菜单
            var menu = new MenuStrip { BackColor = PANEL, ForeColor = TEXT, Font = new Font("Microsoft YaHei UI", 9F) };
            var miAct = new ToolStripMenuItem("操作");
            miAct.DropDownItems.AddRange(new ToolStripItem[] {
                new ToolStripMenuItem("💾 保存", null, (s,e)=>DoSave()),
                new ToolStripMenuItem("📂 读档", null, (s,e)=>DoLoad()),
                new ToolStripMenuItem("🌙 结束白天", null, (s,e)=>DoNight()),
                new ToolStripMenuItem("⏭ 下一天", null, (s,e)=>DoNextDay()),
                new ToolStripMenuItem("🔄 刷新", null, (s,e)=>RefreshCurrentTab()),
                new ToolStripMenuItem("🤖 自动游玩", null, (s,e)=>ToggleAutoPlay()),
                new ToolStripMenuItem("🆕 新游戏", null, (s,e)=>DoNewGame()),
                new ToolStripMenuItem("📊 终局统计", null, (s,e)=>ShowEndStats()),
            });
            menu.Items.Add(miAct);
            MainMenuStrip = menu;
            Controls.Add(menu);

            // 快捷键
            KeyPreview = true;
            KeyDown += (s, e) => {
                if (e.Control && e.KeyCode == Keys.S) DoSave();
                else if (e.KeyCode == Keys.N) DoNight();
                else if (e.KeyCode == Keys.D) DoNextDay();
                else if (e.KeyCode == Keys.R) RefreshCurrentTab();
                else if (e.KeyCode >= Keys.F1 && e.KeyCode <= Keys.F7)
                    _tabs.SelectedIndex = Math.Min((int)e.KeyCode - (int)Keys.F1, _tabs.TabPages.Count - 1);
            };

            _tabs.SelectedIndexChanged += (s, e) => RefreshCurrentTab();
        }

        private void AddTab(string name, Action<TabPage> drawer)
        {
            var page = new TabPage(name) { BackColor = BG, ForeColor = TEXT, AutoScroll = true };
            _tabs.TabPages.Add(page);
            drawer(page);
        }

        private void StartGame()
        {
            _game = new GameManager();
            _game.Initialize(new MockAIProvider());
            _game.StartNewGame();
            AddLog("欢迎来到档案城！第1天开始。");
            UpdateTopBar();
            RefreshCurrentTab();
        }

        private void UpdateTopBar()
        {
            string phase = _game.Time.CurrentPhase == GamePhase.Day ? "☀白天" :
                           _game.Time.CurrentPhase == GamePhase.Night ? "🌙夜晚" : "📊结算";
            _topLabel.Text = $"第{_game.Time.CurrentDay}天 {phase}  │  " +
                $"🍖{_game.Resources.GetResourceAmount(ResourceType.Food)}  " +
                $"💧{_game.Resources.GetResourceAmount(ResourceType.Water)}  " +
                $"⚡{_game.Resources.GetResourceAmount(ResourceType.Power)}  " +
                $"💊{_game.Resources.GetResourceAmount(ResourceType.Medicine)}  " +
                $"🔧{_game.Resources.GetResourceAmount(ResourceType.Parts)}  " +
                $"💎{_game.Resources.GetResourceAmount(ResourceType.MemoryShards)}  " +
                $"│  👥{_game.NPCs.GetAliveCount()}  " +
                $"⚔{_game.TotalCombatsWon}  " +
                $"🗺{_game.TotalExplorationsCompleted}";
        }

        private void RefreshCurrentTab()
        {
            if (_tabs.SelectedIndex < 0 || _game == null) return;
            var page = _tabs.TabPages[_tabs.SelectedIndex];
            page.Controls.Clear();
            switch (_tabs.SelectedIndex)
            {
                case 0: DrawOverview(page); break;
                case 1: DrawNPCs(page); break;
                case 2: DrawBuildings(page); break;
                case 3: DrawQuests(page); break;
                case 4: DrawItems(page); break;
                case 5: DrawFactions(page); break;
                case 6: DrawCrisis(page); break;
            }
            UpdateTopBar();
        }

        // ═══════════════ 各 Tab ═══════════════

        private void DrawOverview(TabPage page)
        {
            int y = 10;
            L(page, "📊 档案城概览", 10, y, 16, true, ACCENT); y += 36;

            // 操作按钮组
            var btnExplore = MakeBtn("🗺 开始探索", 10, y, 150, 32);
            btnExplore.Click += (s, e) => ShowExploreDialog();
            page.Controls.Add(btnExplore);

            var btnCombat = MakeBtn("⚔ 战斗", 170, y, 120, 32);
            btnCombat.Click += (s, e) => ShowCombatDialog();
            page.Controls.Add(btnCombat);

            var btnTalk = MakeBtn("💬 对话", 300, y, 120, 32);
            btnTalk.Click += (s, e) => ShowTalkDialog();
            page.Controls.Add(btnTalk);

            var btnWork = MakeBtn("🔧 分配工作", 430, y, 150, 32);
            btnWork.Click += (s, e) => ShowWorkDialog();
            page.Controls.Add(btnWork);

            y += 50;

            L(page, $"存活天数: {_game.Time.CurrentDay}", 10, y, 11, false, TEXT); y += 24;
            L(page, $"存活居民: {_game.NPCs.GetAliveCount()}/{_game.NPCs.GetAllNPCs().Count}", 10, y, 11, false, TEXT); y += 24;
            int completed = _game.Quests.GetAllQuests().Count(q => q.Status == QuestStatus.Completed);
            L(page, $"已完成任务: {completed}", 10, y, 11, false, TEXT); y += 24;
            L(page, $"总探索: {_game.TotalExplorationsCompleted}  总战斗胜场: {_game.TotalCombatsWon}", 10, y, 11, false, TEXT); y += 40;

            L(page, "📦 资源", 10, y, 14, true, ACCENT2); y += 30;
            ResBar(page, "食物", _game.Resources.GetResourceAmount(ResourceType.Food), 100, 10, ref y);
            ResBar(page, "水", _game.Resources.GetResourceAmount(ResourceType.Water), 100, 10, ref y);
            ResBar(page, "电力", _game.Resources.GetResourceAmount(ResourceType.Power), 50, 10, ref y);
            ResBar(page, "药品", _game.Resources.GetResourceAmount(ResourceType.Medicine), 30, 10, ref y);
            ResBar(page, "零件", _game.Resources.GetResourceAmount(ResourceType.Parts), 50, 10, ref y);
            ResBar(page, "碎片", _game.Resources.GetResourceAmount(ResourceType.MemoryShards), 20, 10, ref y);
            y += 20;

            L(page, "🏗 建筑", 10, y, 14, true, ACCENT2); y += 30;
            foreach (var b in _game.Buildings.GetAllBuildings())
            {
                L(page, $"  {(b.Built ? "✅" : "⬜")} {b.Name}: {(b.Built ? $"Lv{b.Level}" : "未建")}", 10, y, 11, false, b.Built ? SUCCESS : TEXT_DIM);
                y += 22;
            }
        }

        private void DrawNPCs(TabPage page)
        {
            int y = 10;
            L(page, "👥 居民列表", 10, y, 16, true, ACCENT); y += 40;

            foreach (var npc in _game.NPCs.GetAllNPCs())
            {
                Color nc = npc.IsAlive ? TEXT : TEXT_DIM;
                string st = !npc.IsAlive ? "💀" : npc.Status == NPCStatus.Injured ? "🤕" : npc.Status == NPCStatus.Working ? "🔧" : "😊";
                L(page, $"{st} {npc.Name} ({npc.Role})", 10, y, 13, true, nc); y += 22;
                if (npc.IsAlive)
                {
                    L(page, $"  HP:{npc.Health}  士气:{npc.Morale}  忠诚:{npc.Loyalty}  饥饿:{npc.Hunger}", 10, y, 10, false, TEXT_DIM); y += 20;
                    var mental = _game.Psychology.GetState(npc.Id);
                    Color mc = mental == MentalState.Hopeful ? SUCCESS : mental == MentalState.Despair || mental == MentalState.Traumatized ? DANGER : mental == MentalState.Anxious ? ACCENT2 : TEXT;
                    L(page, $"  心理: {_game.Psychology.GetStateName(mental)}  工作: {npc.CurrentWork}", 10, y, 10, false, mc); y += 22;

                    // 对话按钮
                    var btnTalk = MakeBtn("💬", 400, y - 22, 40, 22);
                    string talkId = npc.Id;
                    string talkName = npc.Name;
                    btnTalk.Click += (s, e) => {
                        string dlg = _game.TalkToNPC(talkId);
                        AddLog($"💬 {talkName}: {dlg.Substring(0, Math.Min(80, dlg.Length))}...");
                    };
                    page.Controls.Add(btnTalk);

                    // 治疗按钮
                    if (npc.Status == NPCStatus.Injured)
                    {
                        var btnHeal = MakeBtn("💊", 445, y - 22, 40, 22);
                        string healId = npc.Id;
                        string healName = npc.Name;
                        btnHeal.BackColor = DANGER;
                        btnHeal.Click += (s, e) => {
                            if (_game.Resources.GetResourceAmount(ResourceType.Medicine) > 0)
                            {
                                _game.Resources.ConsumeResource(ResourceType.Medicine, 1);
                                _game.NPCs.HealNPC(healId, 30);
                                AddLog($"💊 治疗了 {healName}");
                                RefreshCurrentTab();
                            }
                            else AddLog("❌ 没有药品");
                        };
                        page.Controls.Add(btnHeal);
                    }
                    y += 4;
                }
            }
        }

        private void DrawBuildings(TabPage page)
        {
            int y = 10;
            L(page, "🏗 建筑管理", 10, y, 16, true, ACCENT); y += 40;

            foreach (var b in _game.Buildings.GetAllBuildings())
            {
                Color c = b.Built ? SUCCESS : TEXT;
                L(page, $"{b.Name}: {(b.Built ? $"已建 Lv{b.Level}" : "未建造")}", 10, y, 12, true, c); y += 22;
                L(page, $"  {b.Description}", 10, y, 10, false, TEXT_DIM); y += 22;

                // 建造/升级按钮
                if (!b.Built)
                {
                    var btn = MakeBtn($"建造", 30, y, 100, 28);
                    string bid = b.Id;
                    btn.Click += (s, e) => {
                        if (_game.Buildings.Build(bid)) { AddLog($"✅ 建造了 {b.Name}"); RefreshCurrentTab(); }
                        else AddLog("❌ 资源不足，无法建造");
                    };
                    page.Controls.Add(btn);
                }
                else if (b.Level < b.MaxLevel)
                {
                    var btn = MakeBtn($"升级到 Lv{b.Level + 1}", 30, y, 150, 28);
                    string bid = b.Id;
                    btn.Click += (s, e) => {
                        if (_game.Buildings.Upgrade(bid)) { AddLog($"✅ {b.Name} 升级到 Lv{b.Level + 1}"); RefreshCurrentTab(); }
                        else AddLog("❌ 资源不足，无法升级");
                    };
                    page.Controls.Add(btn);
                }
                y += 36;
            }
        }

        private void DrawQuests(TabPage page)
        {
            int y = 10;
            L(page, "📜 任务列表", 10, y, 16, true, ACCENT); y += 40;

            foreach (var q in _game.Quests.GetAllQuests())
            {
                Color c = q.Status == QuestStatus.Active ? ACCENT2 : q.Status == QuestStatus.Completed ? SUCCESS : TEXT_DIM;
                string st = q.Status == QuestStatus.Active ? "▶" : q.Status == QuestStatus.Completed ? "✅" : "⏸";
                L(page, $"{st} [{q.Type}] {q.Title}", 10, y, 12, true, c); y += 22;
                L(page, $"  {q.Description}", 10, y, 10, false, TEXT_DIM); y += 20;

                // 目标进度
                foreach (var obj in q.Objectives)
                {
                    string progress = obj.CurrentProgress >= obj.RequiredAmount ? "✅" : $"({obj.CurrentProgress}/{obj.RequiredAmount})";
                    L(page, $"    {obj.Type}:{obj.TargetId} {progress}", 10, y, 9, false, TEXT_DIM);
                    y += 18;
                }
                y += 8;
            }
        }

        private void DrawItems(TabPage page)
        {
            int y = 10;
            L(page, "🎒 背包物品", 10, y, 16, true, ACCENT); y += 40;

            var items = _game.Items.GetInventory();
            if (items.Count == 0) { L(page, "(背包为空)", 10, y, 11, false, TEXT_DIM); return; }

            foreach (var item in items)
            {
                Color c = item.Rarity == ItemRarity.Legendary ? ACCENT2 : item.Rarity == ItemRarity.Epic ? ACCENT : TEXT;
                L(page, $"[{item.GetRarityName()}] {item.Name}", 10, y, 12, true, c); y += 20;
                string desc = item.Description;
                if (item.AttackBonus > 0) desc += $" 攻+{item.AttackBonus}";
                if (item.DefenseBonus > 0) desc += $" 防+{item.DefenseBonus}";
                if (item.HealAmount > 0) desc += $" 治{item.HealAmount}HP";
                L(page, $"  {desc}", 10, y, 10, false, TEXT_DIM); y += 22;

                // 装备按钮
                if (item.Type == ItemType.Weapon)
                {
                    var btn = MakeBtn("装备", 30, y, 80, 24);
                    string itemId = item.Id;
                    btn.Click += (s, e) => ShowEquipDialog(itemId);
                    page.Controls.Add(btn);
                    y += 6;
                }
                else if (item.Type == ItemType.Consumable)
                {
                    var btn = MakeBtn("使用", 30, y, 80, 24);
                    string itemId = item.Id;
                    btn.Click += (s, e) => {
                        // 简单使用：治疗所有受伤NPC
                        foreach (var npc in _game.NPCs.GetAllNPCs())
                        {
                            if (npc.IsAlive && npc.Health < 100)
                            {
                                npc.Health = Math.Min(100, npc.Health + item.HealAmount);
                                AddLog($"使用 {item.Name} 治疗了 {npc.Name}");
                                break;
                            }
                        }
                        RefreshCurrentTab();
                    };
                    page.Controls.Add(btn);
                    y += 6;
                }
                y += 24;
            }
        }

        private void DrawFactions(TabPage page)
        {
            int y = 10;
            L(page, "🏛 派系声望", 10, y, 16, true, ACCENT); y += 40;
            foreach (var f in _game.Factions.GetAllFactions())
            {
                Color c = f.Unlocked ? ACCENT2 : TEXT_DIM;
                L(page, $"{f.Name}{(f.Unlocked ? "" : " (未解锁)")}", 10, y, 13, true, c); y += 24;
                L(page, $"  声望: {f.Reputation} ({f.GetReputationName()})", 10, y, 10, false, TEXT); y += 20;
                L(page, $"  {f.Description}", 10, y, 10, false, TEXT_DIM); y += 28;
            }
        }

        private void DrawCrisis(TabPage page)
        {
            int y = 10;
            L(page, "⚠ 危机 / 心理", 10, y, 16, true, ACCENT); y += 40;

            var crises = _game.Crises.ActiveCrises;
            if (crises.Count > 0)
            {
                L(page, "【活跃危机】", 10, y, 12, true, DANGER); y += 24;
                foreach (var c in crises) { L(page, $"  ⚠ {c.Type}: {c.Description}", 10, y, 10, false, DANGER); y += 22; }
            }
            else { L(page, "✅ 无活跃危机", 10, y, 12, true, SUCCESS); y += 30; }

            L(page, "【NPC心理】", 10, y, 12, true, ACCENT2); y += 24;
            foreach (var npc in _game.NPCs.GetAllNPCs())
            {
                if (!npc.IsAlive) continue;
                var state = _game.Psychology.GetState(npc.Id);
                Color c = state == MentalState.Hopeful ? SUCCESS : state == MentalState.Despair || state == MentalState.Traumatized ? DANGER : state == MentalState.Anxious ? ACCENT2 : TEXT;
                L(page, $"  {npc.Name}: {_game.Psychology.GetStateName(state)}", 10, y, 11, false, c); y += 22;
            }

            y += 20;
            L(page, "【结局预测】", 10, y, 12, true, ACCENT2); y += 24;
            try
            {
                var ending = _game.Endings.CalculateEnding(_game);
                L(page, $"  → {ending.Title}", 10, y, 12, true, ACCENT); y += 24;
                L(page, $"  {ending.Epilogue}", 10, y, 10, false, TEXT_DIM);
            }
            catch { L(page, "  计算中...", 10, y, 10, false, TEXT_DIM); }
        }

        // ═══════════════ 交互对话框 ═══════════════

        private void ShowExploreDialog()
        {
            var maps = _game.Exploration.GetAllMaps();
            using (var dlg = new Form())
            {
                dlg.Text = "选择探索地图";
                dlg.Size = new Size(500, 400);
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.BackColor = BG;
                dlg.ForeColor = TEXT;

                var lbl = new Label { Text = "选择地图和队员：", Location = new Point(10, 10), AutoSize = true, ForeColor = ACCENT, Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold) };
                dlg.Controls.Add(lbl);

                var mapList = new CheckedListBox { Location = new Point(10, 50), Size = new Size(220, 250), BackColor = PANEL, ForeColor = TEXT };
                foreach (var m in maps) mapList.Items.Add(m.Name);
                dlg.Controls.Add(mapList);

                var npcList = new CheckedListBox { Location = new Point(250, 50), Size = new Size(220, 250), BackColor = PANEL, ForeColor = TEXT };
                foreach (var n in _game.NPCs.GetAllNPCs()) if (n.IsAlive) npcList.Items.Add(n.Name);
                dlg.Controls.Add(npcList);

                var lbl1 = new Label { Text = "地图", Location = new Point(10, 30), AutoSize = true, ForeColor = ACCENT2 };
                var lbl2 = new Label { Text = "队员", Location = new Point(250, 30), AutoSize = true, ForeColor = ACCENT2 };
                dlg.Controls.Add(lbl1); dlg.Controls.Add(lbl2);

                var btnGo = new Button { Text = "出发！", Location = new Point(150, 320), Size = new Size(100, 30), BackColor = ACCENT, ForeColor = Color.Black };
                btnGo.Click += (s, e) => {
                    if (mapList.CheckedItems.Count == 0 || npcList.CheckedItems.Count == 0) { MessageBox.Show("请选择地图和队员"); return; }
                    string mapId = maps[mapList.CheckedIndices[0]].Id;
                    var teamIds = new List<string>();
                    foreach (int idx in npcList.CheckedIndices)
                    {
                        var npc = _game.NPCs.GetAllNPCs().Where(n => n.IsAlive).ToList()[idx];
                        teamIds.Add(npc.Id);
                    }
                    if (_game.Exploration.StartExploration(mapId, teamIds))
                    {
                        AddLog($"🗺 开始探索 {maps[mapList.CheckedIndices[0]].Name}");
                        // 自动搜索几个房间
                        for (int i = 0; i < 3 && _game.Exploration.IsExploring; i++)
                        {
                            _game.Exploration.SearchRoom();
                            var connected = _game.Exploration.GetConnectedRooms();
                            if (connected.Count > 0) _game.Exploration.MoveToRoom(connected[0].Id);
                        }
                        _game.Exploration.ReturnToBase();
                        AddLog("✅ 探索完成，返回基地");
                        RefreshCurrentTab();
                        dlg.Close();
                    }
                    else AddLog("❌ 无法开始探索");
                };
                dlg.Controls.Add(btnGo);
                dlg.ShowDialog(this);
            }
        }

        private void ShowCombatDialog()
        {
            var enemies = new[] { "wanderer", "shadow_creature", "mutant_plant", "scavenger_gang", "memory_ghost", "toxic_spore" };
            using (var dlg = new Form())
            {
                dlg.Text = "战斗";
                dlg.Size = new Size(400, 350);
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.BackColor = BG; dlg.ForeColor = TEXT;

                var lbl1 = new Label { Text = "选择队员：", Location = new Point(10, 10), AutoSize = true, ForeColor = ACCENT };
                dlg.Controls.Add(lbl1);
                var npcList = new CheckedListBox { Location = new Point(10, 40), Size = new Size(170, 200), BackColor = PANEL, ForeColor = TEXT };
                foreach (var n in _game.NPCs.GetAllNPCs()) if (n.IsAlive) npcList.Items.Add(n.Name);
                dlg.Controls.Add(npcList);

                var lbl2 = new Label { Text = "敌人：", Location = new Point(200, 10), AutoSize = true, ForeColor = DANGER };
                dlg.Controls.Add(lbl2);
                var enemyList = new ListBox { Location = new Point(200, 40), Size = new Size(170, 200), BackColor = PANEL, ForeColor = TEXT };
                foreach (var e in enemies) enemyList.Items.Add(e);
                enemyList.SelectedIndex = 0;
                dlg.Controls.Add(enemyList);

                var btn = new Button { Text = "战斗！", Location = new Point(120, 270), Size = new Size(100, 30), BackColor = DANGER, ForeColor = Color.White };
                btn.Click += (s, e) => {
                    if (npcList.CheckedItems.Count == 0) { MessageBox.Show("请选择队员"); return; }
                    var teamIds = new List<string>();
                    foreach (int idx in npcList.CheckedIndices)
                    {
                        var npc = _game.NPCs.GetAllNPCs().Where(n => n.IsAlive).ToList()[idx];
                        teamIds.Add(npc.Id);
                    }
                    var enemyIds = new List<string> { enemies[enemyList.SelectedIndex] };
                    if (_game.Combat.StartCombat(teamIds, enemyIds))
                    {
                        AddLog($"⚔ 战斗开始！敌人: {enemies[enemyList.SelectedIndex]}");
                        int turns = 0;
                        while (_game.Combat.InCombat && turns < 20)
                        {
                            var result = _game.Combat.ExecutePlayerAction(CombatAction.Attack);
                            if (result != null)
                            {
                                AddLog(result.Victory ? "✅ 战斗胜利！" : result.Escaped ? "🏃 成功逃跑" : "💀 战斗失败");
                                break;
                            }
                            turns++;
                        }
                        RefreshCurrentTab();
                        dlg.Close();
                    }
                    else AddLog("❌ 无法开始战斗");
                };
                dlg.Controls.Add(btn);
                dlg.ShowDialog(this);
            }
        }

        private void ShowTalkDialog()
        {
            using (var dlg = new Form())
            {
                dlg.Text = "与NPC对话";
                dlg.Size = new Size(500, 400);
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.BackColor = BG; dlg.ForeColor = TEXT;

                var lbl = new Label { Text = "选择NPC：", Location = new Point(10, 10), AutoSize = true, ForeColor = ACCENT };
                dlg.Controls.Add(lbl);
                var list = new ListBox { Location = new Point(10, 40), Size = new Size(200, 250), BackColor = PANEL, ForeColor = TEXT };
                foreach (var n in _game.NPCs.GetAllNPCs()) if (n.IsAlive) list.Items.Add(n.Name);
                dlg.Controls.Add(list);

                var resultBox = new RichTextBox { Location = new Point(220, 40), Size = new Size(250, 250), BackColor = PANEL, ForeColor = TEXT, ReadOnly = true };
                dlg.Controls.Add(resultBox);

                var btn = new Button { Text = "对话", Location = new Point(50, 310), Size = new Size(100, 30), BackColor = ACCENT, ForeColor = Color.Black };
                btn.Click += (s, e) => {
                    if (list.SelectedIndex < 0) return;
                    var npc = _game.NPCs.GetAllNPCs().Where(n => n.IsAlive).ToList()[list.SelectedIndex];
                    string dialogue = _game.TalkToNPC(npc.Id);
                    resultBox.Text = $"【{npc.Name}】\n\n{dialogue}";
                    AddLog($"💬 与 {npc.Name} 对话");
                };
                dlg.Controls.Add(btn);
                dlg.ShowDialog(this);
            }
        }

        private void ShowWorkDialog()
        {
            using (var dlg = new Form())
            {
                dlg.Text = "分配工作";
                dlg.Size = new Size(500, 400);
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.BackColor = BG; dlg.ForeColor = TEXT;

                var lbl = new Label { Text = "NPC → 工作：", Location = new Point(10, 10), AutoSize = true, ForeColor = ACCENT };
                dlg.Controls.Add(lbl);

                var grid = new DataGridView { Location = new Point(10, 40), Size = new Size(460, 250), BackgroundColor = PANEL,
                    ForeColor = TEXT, RowHeadersVisible = false, AllowUserToAddRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
                grid.Columns.Add("NPC", "NPC");
                grid.Columns.Add("Work", "工作");
                var workCol = new DataGridViewComboBoxColumn { HeaderText = "分配工作" };
                foreach (var wt in Enum.GetNames(typeof(WorkType))) workCol.Items.Add(wt);
                grid.Columns.Add(workCol);

                foreach (var npc in _game.NPCs.GetAllNPCs())
                {
                    if (!npc.IsAlive) continue;
                    int row = grid.Rows.Add();
                    grid.Rows[row].Cells[0].Value = npc.Name;
                    grid.Rows[row].Cells[1].Value = npc.CurrentWork.ToString();
                    grid.Rows[row].Cells[2].Value = npc.CurrentWork.ToString();
                }
                dlg.Controls.Add(grid);

                var btn = new Button { Text = "确认", Location = new Point(180, 310), Size = new Size(100, 30), BackColor = ACCENT, ForeColor = Color.Black };
                btn.Click += (s, e) => {
                    var npcs = _game.NPCs.GetAllNPCs().Where(n => n.IsAlive).ToList();
                    for (int i = 0; i < npcs.Count && i < grid.Rows.Count; i++)
                    {
                        string workStr = grid.Rows[i].Cells[2].Value?.ToString() ?? "None";
                        if (Enum.TryParse<WorkType>(workStr, out var wt)) npcs[i].CurrentWork = wt;
                    }
                    AddLog("🔧 工作分配完成");
                    RefreshCurrentTab();
                    dlg.Close();
                };
                dlg.Controls.Add(btn);
                dlg.ShowDialog(this);
            }
        }

        private void ShowEquipDialog(string itemId)
        {
            using (var dlg = new Form())
            {
                dlg.Text = "装备武器";
                dlg.Size = new Size(300, 300);
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.BackColor = BG; dlg.ForeColor = TEXT;

                var lbl = new Label { Text = "装备给谁？", Location = new Point(10, 10), AutoSize = true, ForeColor = ACCENT };
                dlg.Controls.Add(lbl);
                var list = new ListBox { Location = new Point(10, 40), Size = new Size(260, 180), BackColor = PANEL, ForeColor = TEXT };
                foreach (var n in _game.NPCs.GetAllNPCs()) if (n.IsAlive) list.Items.Add(n.Name);
                dlg.Controls.Add(list);

                var btn = new Button { Text = "装备", Location = new Point(80, 230), Size = new Size(100, 30), BackColor = ACCENT, ForeColor = Color.Black };
                btn.Click += (s, e) => {
                    if (list.SelectedIndex < 0) return;
                    var npc = _game.NPCs.GetAllNPCs().Where(n => n.IsAlive).ToList()[list.SelectedIndex];
                    if (_game.Items.EquipWeapon(npc.Id, itemId))
                    {
                        AddLog($"⚔ {npc.Name} 装备了 {itemId}");
                        RefreshCurrentTab();
                        dlg.Close();
                    }
                    else AddLog("❌ 装备失败");
                };
                dlg.Controls.Add(btn);
                dlg.ShowDialog(this);
            }
        }

        // ═══════════════ 操作 ═══════════════

        private void DoSave()
        {
            if (_game.SaveGame()) { AddLog("✅ 保存成功！"); MessageBox.Show("保存成功！"); }
            else { AddLog("❌ 保存失败！"); MessageBox.Show("保存失败！"); }
        }

        private void DoNight()
        {
            _game.Time.AdvanceToNight();
            AddLog("🌙 进入夜晚...");
            Beep(400, 200);
            RefreshCurrentTab();
        }

        private void DoNextDay()
        {
            // 完整一天：白天→夜晚→结算
            if (_game.Time.CurrentPhase == GamePhase.Day)
            {
                _game.Time.AdvanceToNight();
                AddLog("🌙 进入夜晚...");
            }
            _game.Time.AdvanceToSummary();
            _game.OnDayEnd();
            AddLog($"── 第{_game.Time.CurrentDay}天结算完成 ──");
            Beep(600, 150);

            // 显示每日事件
            if (!string.IsNullOrEmpty(_game.LastDailyEvent))
            {
                AddLog($"  {_game.LastDailyEvent}");
            }

            // 显示危机
            if (_game.Crises.HasAnyCrisis)
            {
                foreach (var c in _game.Crises.ActiveCrises)
                {
                    AddLog($"  ⚠ 危机: {c.Type} - {c.Description}");
                }
                Beep(200, 500); // 危机警告音
            }

            // 新手引导
            if (_game.Tutorial.Enabled)
            {
                string guide = _game.Tutorial.CheckDayGuide(_game.Time.CurrentDay, _game);
                if (!string.IsNullOrEmpty(guide)) AddLog(guide);
            }

            RefreshCurrentTab();

            if (_game.IsGameOver)
            {
                Beep(_game.IsVictory ? 800 : 200, 1000);
                try
                {
                    var ending = _game.Endings.CalculateEnding(_game);
                    MessageBox.Show($"{(_game.IsVictory ? "🎉 胜利！" : "💀 游戏结束")}\n\n{ending.Epilogue}", "结局");
                }
                catch { MessageBox.Show(_game.IsVictory ? "🎉 胜利！" : "💀 游戏结束", "结局"); }
            }
        }

        private void Beep(int freq, int duration)
        {
            try { Console.Beep(freq, duration); } catch { }
        }

        // ═══════════════ 新功能 ═══════════════

        private void DoLoad()
        {
            if (!_game.Save.HasSave())
            {
                MessageBox.Show("没有存档可读", "读档", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var data = _game.Save.LoadGame();
            if (data == null)
            {
                AddLog("❌ 读档失败！");
                MessageBox.Show("读档失败！", "错误");
                return;
            }
            _game.LoadGame();
            AddLog($"📂 读档成功！第{_game.Time.CurrentDay}天");
            RefreshCurrentTab();
            MessageBox.Show($"读档成功！第{_game.Time.CurrentDay}天", "读档");
        }

        private void DoNewGame()
        {
            if (MessageBox.Show("确定要开始新游戏？当前进度将丢失。", "新游戏",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            _logBox.Clear();
            StartGame();
        }

        private void ToggleAutoPlay()
        {
            _autoPlaying = !_autoPlaying;
            if (_autoPlaying)
            {
                AddLog("🤖 自动游玩开始（每2秒推进一天）");
                _autoPlayTimer = new System.Windows.Forms.Timer();
                _autoPlayTimer.Interval = 2000;
                _autoPlayTimer.Tick += (s, e) => {
                    if (_game.IsGameOver)
                    {
                        ToggleAutoPlay();
                        return;
                    }
                    DoNextDay();
                };
                _autoPlayTimer.Start();
            }
            else
            {
                AddLog("⏹ 自动游玩停止");
                _autoPlayTimer?.Stop();
                _autoPlayTimer?.Dispose();
                _autoPlayTimer = null;
            }
        }

        private void ShowEndStats()
        {
            if (_game == null) return;
            int completed = 0, active = 0, total = 0;
            foreach (var q in _game.Quests.GetAllQuests())
            {
                total++;
                if (q.Status == QuestStatus.Completed) completed++;
                else if (q.Status == QuestStatus.Active) active++;
            }
            int alive = _game.NPCs.GetAliveCount();
            int totalNpc = _game.NPCs.GetAllNPCs().Count;
            int buildingsBuilt = 0;
            foreach (var b in _game.Buildings.GetAllBuildings()) if (b.Built) buildingsBuilt++;

            string endingTitle = "未定";
            try { endingTitle = _game.Endings.CalculateEnding(_game).Title; } catch { }

            string stats = $"═══ 终局统计 ═══\n\n" +
                $"📅 存活天数: {_game.Time.CurrentDay}\n" +
                $"👥 居民存活: {alive}/{totalNpc}\n" +
                $"🏗 建筑建成: {buildingsBuilt}\n" +
                $"📜 任务完成: {completed}/{total} (进行中:{active})\n" +
                $"⚔ 战斗胜场: {_game.TotalCombatsWon}\n" +
                $"🗺 探索次数: {_game.TotalExplorationsCompleted}\n\n" +
                $"📦 资源:\n" +
                $"  食物: {_game.Resources.GetResourceAmount(ResourceType.Food)}\n" +
                $"  水: {_game.Resources.GetResourceAmount(ResourceType.Water)}\n" +
                $"  电力: {_game.Resources.GetResourceAmount(ResourceType.Power)}\n" +
                $"  药品: {_game.Resources.GetResourceAmount(ResourceType.Medicine)}\n" +
                $"  零件: {_game.Resources.GetResourceAmount(ResourceType.Parts)}\n" +
                $"  记忆碎片: {_game.Resources.GetResourceAmount(ResourceType.MemoryShards)}\n\n" +
                $"🏛 派系声望:\n" +
                string.Join("\n", _game.Factions.GetAllFactions().Select(f => $"  {f.Name}: {f.Reputation}")) + "\n\n" +
                $"🔮 结局预测: {endingTitle}\n" +
                $"{'═',15}";

            MessageBox.Show(stats, "📊 终局统计", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ═══════════════ UI 工具 ═══════════════

        private Label L(Control p, string text, int x, int y, int size, bool bold, Color color)
        {
            var lbl = new Label { Text = text, Location = new Point(x, y), AutoSize = true,
                BackColor = p.BackColor, ForeColor = color,
                Font = new Font("Microsoft YaHei UI", (float)size, bold ? FontStyle.Bold : FontStyle.Regular) };
            p.Controls.Add(lbl);
            return lbl;
        }

        private Button MakeBtn(string text, int x, int y, int w, int h)
        {
            return new Button { Text = text, Location = new Point(x, y), Size = new Size(w, h),
                BackColor = BTN_BG, ForeColor = TEXT, FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft YaHei UI", 9F) };
        }

        private void ResBar(TabPage page, string name, int amount, int max, int x, ref int y)
        {
            L(page, $"{name}: {amount}", x, y, 11, false, TEXT);
            int barX = x + 150, barW = 250, barH = 16;
            var bg = new Panel { Location = new Point(barX, y + 2), Size = new Size(barW, barH), BackColor = Color.FromArgb(40, 40, 55) };
            int fillW = (int)(barW * Math.Min(1.0, (double)amount / max));
            var fill = new Panel { Location = new Point(0, 0), Size = new Size(fillW, barH),
                BackColor = amount <= max * 0.3 ? DANGER : amount <= max * 0.6 ? ACCENT2 : SUCCESS };
            bg.Controls.Add(fill);
            page.Controls.Add(bg);
            y += 28;
        }

        private string CostStr(Dictionary<ResourceType, int> cost)
        {
            return string.Join(" ", cost.Select(kv => $"{kv.Value}{kv.Key}"));
        }

        private void AddLog(string msg)
        {
            string line = $"[{_game?.Time.CurrentDay ?? 0}] {msg}\n";
            if (_logBox.InvokeRequired) _logBox.Invoke(new Action(() => _logBox.AppendText(line)));
            else _logBox.AppendText(line);
        }
    }
}
#endif

