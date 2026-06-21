// ============================================================
// Last Archive - NPC记忆数据结构
// ============================================================

using System;
using System.Collections.Generic;

namespace LastArchive
{
    /// <summary>
    /// 单条记忆
    /// </summary>
    [Serializable]
    public class MemoryEntry
    {
        public string MemoryId { get; set; }
        public int Day { get; set; }
        public string Actor { get; set; }
        public string Target { get; set; }
        public MemoryEventType EventType { get; set; }
        public string Description { get; set; }
        public int EmotionalWeight { get; set; }
        public List<string> Tags { get; set; } = new List<string>();
    }

    /// <summary>
    /// NPC记忆容器
    /// </summary>
    [Serializable]
    public class NPCMemory
    {
        public string NPCId { get; set; }
        public List<MemoryEntry> Entries { get; set; } = new List<MemoryEntry>();
        public string Summary { get; set; } = "";
        public List<string> DiaryHistory { get; set; } = new List<string>();

        public void Add(MemoryEntry entry)
        {
            Entries.Add(entry);
            if (Entries.Count > 200)
            {
                Entries.RemoveAt(0);
            }
        }
    }
}
