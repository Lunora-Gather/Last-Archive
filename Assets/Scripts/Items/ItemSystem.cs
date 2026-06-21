// ============================================================
// Last Archive - 物品/装备系统
// ============================================================

using System;
using System.Collections.Generic;

namespace LastArchive
{
    /// <summary>
    /// 物品稀有度
    /// </summary>
    public enum ItemRarity
    {
        Common,      // 普通
        Uncommon,    // 优秀
        Rare,        // 稀有
        Epic,        // 史诗
        Legendary    // 传说
    }

    /// <summary>
    /// 物品类型
    /// </summary>
    public enum ItemType
    {
        Weapon,      // 武器 - 增加攻击
        Armor,       // 护甲 - 增加防御
        Consumable,  // 消耗品 - 使用后消失
        Material,    // 材料 - 用于建造/升级
        KeyItem,     // 关键物品 - 剧情/任务
        Relic        // 遗物 - 记忆碎片相关
    }

    /// <summary>
    /// 物品实例
    /// </summary>
    public class ItemInstance
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public ItemType Type { get; set; }
        public ItemRarity Rarity { get; set; }
        public int Value { get; set; }           // 价值（可交易时使用）

        // 装备属性
        public int AttackBonus { get; set; }     // 攻击加成
        public int DefenseBonus { get; set; }    // 防御加成
        public int HealthBonus { get; set; }     // 生命加成

        // 消耗品属性
        public int HealAmount { get; set; }      // 治疗量
        public int MoraleBoost { get; set; }     // 士气提升
        public ResourceType? ResourceType { get; set; } // 消耗品给的资源类型
        public int ResourceAmount { get; set; }  // 消耗品给的资源数量

        // 使用条件
        public int RequiredLevel { get; set; }   // 需要的天数
        public string Source { get; set; }       // 来源描述
        public bool IsConsumable => Type == ItemType.Consumable;
        public bool IsEquippable => Type == ItemType.Weapon || Type == ItemType.Armor;

        public string GetRarityName()
        {
            switch (Rarity)
            {
                case ItemRarity.Common: return "普通";
                case ItemRarity.Uncommon: return "优秀";
                case ItemRarity.Rare: return "稀有";
                case ItemRarity.Epic: return "史诗";
                case ItemRarity.Legendary: return "传说";
                default: return "未知";
            }
        }

        public string GetRarityColor()
        {
            switch (Rarity)
            {
                case ItemRarity.Common: return "白";
                case ItemRarity.Uncommon: return "绿";
                case ItemRarity.Rare: return "蓝";
                case ItemRarity.Epic: return "紫";
                case ItemRarity.Legendary: return "橙";
                default: return "白";
            }
        }
    }

    /// <summary>
    /// 物品事件
    /// </summary>
    public struct OnItemAcquired { public string ItemId; public string ItemName; public string Source; }
    public struct OnItemUsed { public string ItemId; public string ItemName; public string UserNPCId; }
    public struct OnItemEquipped { public string ItemId; public string ItemName; public string NPCId; }

    /// <summary>
    /// 物品系统 - 管理背包、装备和使用物品
    /// </summary>
    public class ItemSystem
    {
        private readonly Dictionary<string, ItemInstance> _itemTemplates = new Dictionary<string, ItemInstance>();
        private readonly List<ItemInstance> _inventory = new List<ItemInstance>();
        private readonly Dictionary<string, string> _equippedWeapons = new Dictionary<string, string>(); // npcId -> itemId
        private readonly Dictionary<string, string> _equippedArmors = new Dictionary<string, string>();  // npcId -> itemId

        private NPCSystem _npcSystem;
        private ResourceSystem _resourceSystem;

        public const int MaxInventorySize = 30;

        public ItemSystem(NPCSystem npcSystem, ResourceSystem resourceSystem)
        {
            _npcSystem = npcSystem;
            _resourceSystem = resourceSystem;
        }

        /// <summary>注册物品模板</summary>
        public void RegisterTemplate(ItemInstance item)
        {
            _itemTemplates[item.Id] = item;
        }

        /// <summary>获取模板</summary>
        public ItemInstance GetTemplate(string id)
        {
            return _itemTemplates.TryGetValue(id, out var t) ? t : null;
        }

        /// <summary>添加物品到背包</summary>
        public bool AddItem(ItemInstance item, string source = "")
        {
            if (_inventory.Count >= MaxInventorySize) return false;

            _inventory.Add(item);
            EventBus.Publish(new OnItemAcquired { ItemId = item.Id, ItemName = item.Name, Source = source });
            return true;
        }

        /// <summary>从模板创建并添加</summary>
        public bool AddItemFromTemplate(string templateId, string source = "")
        {
            var template = GetTemplate(templateId);
            if (template == null) return false;

            var item = new ItemInstance
            {
                Id = template.Id, Name = template.Name, Description = template.Description,
                Type = template.Type, Rarity = template.Rarity, Value = template.Value,
                AttackBonus = template.AttackBonus, DefenseBonus = template.DefenseBonus,
                HealthBonus = template.HealthBonus, HealAmount = template.HealAmount,
                MoraleBoost = template.MoraleBoost, ResourceType = template.ResourceType,
                ResourceAmount = template.ResourceAmount, RequiredLevel = template.RequiredLevel
            };
            return AddItem(item, source);
        }

        /// <summary>移除物品</summary>
        public bool RemoveItem(string itemId)
        {
            for (int i = 0; i < _inventory.Count; i++)
            {
                if (_inventory[i].Id == itemId)
                {
                    _inventory.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        /// <summary>获取背包</summary>
        public List<ItemInstance> GetInventory()
        {
            return new List<ItemInstance>(_inventory);
        }

        /// <summary>获取背包中指定类型的物品</summary>
        public List<ItemInstance> GetItemsByType(ItemType type)
        {
            var result = new List<ItemInstance>();
            foreach (var item in _inventory)
            {
                if (item.Type == type) result.Add(item);
            }
            return result;
        }

        /// <summary>装备武器</summary>
        public bool EquipWeapon(string npcId, string itemId)
        {
            var npc = _npcSystem.GetNPC(npcId);
            if (npc == null) return false;

            // 检查物品是否在背包中
            bool found = false;
            foreach (var item in _inventory)
            {
                if (item.Id == itemId && item.Type == ItemType.Weapon) { found = true; break; }
            }
            if (!found) return false;

            // 检查数量，避免单件装备被多个NPC重复穿戴
            int totalCount = 0;
            foreach (var item in _inventory)
            {
                if (item.Id == itemId) totalCount++;
            }
            int equippedCount = 0;
            foreach (var kv in _equippedWeapons)
            {
                if (kv.Key != npcId && kv.Value == itemId) equippedCount++;
            }
            if (equippedCount >= totalCount) return false;

            // 卸下旧武器
            if (_equippedWeapons.ContainsKey(npcId))
            {
                _equippedWeapons.Remove(npcId);
            }

            _equippedWeapons[npcId] = itemId;
            EventBus.Publish(new OnItemEquipped { ItemId = itemId, ItemName = "", NPCId = npcId });
            return true;
        }

        /// <summary>装备护甲</summary>
        public bool EquipArmor(string npcId, string itemId)
        {
            var npc = _npcSystem.GetNPC(npcId);
            if (npc == null) return false;

            bool found = false;
            foreach (var item in _inventory)
            {
                if (item.Id == itemId && item.Type == ItemType.Armor) { found = true; break; }
            }
            if (!found) return false;

            // 检查数量，避免单件装备被多个NPC重复穿戴
            int totalCount = 0;
            foreach (var item in _inventory)
            {
                if (item.Id == itemId) totalCount++;
            }
            int equippedCount = 0;
            foreach (var kv in _equippedArmors)
            {
                if (kv.Key != npcId && kv.Value == itemId) equippedCount++;
            }
            if (equippedCount >= totalCount) return false;

            if (_equippedArmors.ContainsKey(npcId))
            {
                _equippedArmors.Remove(npcId);
            }

            _equippedArmors[npcId] = itemId;
            EventBus.Publish(new OnItemEquipped { ItemId = itemId, ItemName = "", NPCId = npcId });
            return true;
        }

        /// <summary>使用消耗品</summary>
        public bool UseConsumable(string itemId, string npcId)
        {
            ItemInstance target = null;
            int targetIdx = -1;
            for (int i = 0; i < _inventory.Count; i++)
            {
                if (_inventory[i].Id == itemId && _inventory[i].Type == ItemType.Consumable)
                {
                    target = _inventory[i];
                    targetIdx = i;
                    break;
                }
            }
            if (target == null) return false;

            // 应用效果
            var npc = _npcSystem.GetNPC(npcId);

            if (target.HealAmount > 0 && npc != null)
            {
                npc.Health = Math.Min(GameConfig.MaxNPCHealth, npc.Health + target.HealAmount);
            }

            if (target.MoraleBoost > 0 && npc != null)
            {
                npc.Morale = Math.Min(GameConfig.MaxNPCMorale, npc.Morale + target.MoraleBoost);
            }

            if (target.ResourceType.HasValue && target.ResourceAmount > 0)
            {
                _resourceSystem.AddResource(target.ResourceType.Value, target.ResourceAmount);
            }

            _inventory.RemoveAt(targetIdx);
            EventBus.Publish(new OnItemUsed { ItemId = itemId, ItemName = target.Name, UserNPCId = npcId });
            return true;
        }

        /// <summary>获取NPC的攻击加成</summary>
        public int GetNPCAttackBonus(string npcId)
        {
            int bonus = 0;
            if (_equippedWeapons.TryGetValue(npcId, out var weaponId))
            {
                foreach (var item in _inventory)
                {
                    if (item.Id == weaponId) { bonus += item.AttackBonus; break; }
                }
            }
            return bonus;
        }

        /// <summary>获取NPC的防御加成</summary>
        public int GetNPCDefenseBonus(string npcId)
        {
            int bonus = 0;
            if (_equippedArmors.TryGetValue(npcId, out var armorId))
            {
                foreach (var item in _inventory)
                {
                    if (item.Id == armorId) { bonus += item.DefenseBonus; break; }
                }
            }
            return bonus;
        }

        /// <summary>获取NPC装备信息</summary>
        public string GetNPCEquipmentInfo(string npcId)
        {
            string weapon = "无";
            string armor = "无";
            if (_equippedWeapons.TryGetValue(npcId, out var wid))
            {
                foreach (var item in _inventory)
                {
                    if (item.Id == wid) { weapon = item.Name; break; }
                }
            }
            if (_equippedArmors.TryGetValue(npcId, out var aid))
            {
                foreach (var item in _inventory)
                {
                    if (item.Id == aid) { armor = item.Name; break; }
                }
            }
            return $"武器:{weapon} 护甲:{armor}";
        }

        /// <summary>背包是否已满</summary>
        public bool IsFull => _inventory.Count >= MaxInventorySize;

        /// <summary>背包物品数量</summary>
        public int Count => _inventory.Count;

        /// <summary>生成随机战利品</summary>
        public List<ItemInstance> GenerateLoot(int dangerLevel, Random rng)
        {
            var loot = new List<ItemInstance>();
            var candidates = new List<ItemInstance>();

            foreach (var template in _itemTemplates.Values)
            {
                // 根据危险等级筛选合适的物品
                int rarityScore = (int)template.Rarity;
                if (rarityScore <= dangerLevel / 2 + 1)
                {
                    candidates.Add(template);
                }
            }

            if (candidates.Count == 0) return loot;

            // 1~2个战利品
            int count = Math.Min(rng.Next(1, 3), candidates.Count);
            for (int i = 0; i < count; i++)
            {
                var template = candidates[rng.Next(candidates.Count)];
                loot.Add(new ItemInstance
                {
                    Id = template.Id, Name = template.Name, Description = template.Description,
                    Type = template.Type, Rarity = template.Rarity, Value = template.Value,
                    AttackBonus = template.AttackBonus, DefenseBonus = template.DefenseBonus,
                    HealthBonus = template.HealthBonus, HealAmount = template.HealAmount,
                    MoraleBoost = template.MoraleBoost, ResourceType = template.ResourceType,
                    ResourceAmount = template.ResourceAmount, RequiredLevel = template.RequiredLevel
                });
            }
            return loot;
        }
    }
}
