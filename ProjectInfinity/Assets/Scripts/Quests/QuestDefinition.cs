using System;
using UnityEngine;

namespace ProjectInfinity.Quests
{
    public enum QuestObjectiveType { Kill, Collect, Reach, Talk }

    [Serializable]
    public class QuestObjective
    {
        public QuestObjectiveType type = QuestObjectiveType.Kill;
        [Tooltip("Enemy id, item id, location id or npc id depending on the type.")]
        public string targetId = "";
        public string description = "Objective";
        public int required = 1;

        [NonSerialized] public int progress;

        public bool IsComplete => progress >= required;
        public string ProgressLabel => $"{description} ({Mathf.Min(progress, required)}/{required})";
    }

    [CreateAssetMenu(menuName = "Project Infinity/Quest", fileName = "Quest_")]
    public class QuestDefinition : ScriptableObject
    {
        public string questId = "quest.new";
        public string title = "Untitled Quest";
        [TextArea] public string description = "";
        public int requiredLevel = 1;
        public QuestObjective[] objectives = Array.Empty<QuestObjective>();

        [Header("Rewards")]
        public long xpReward = 120;
        public int goldReward = 25;
        public string[] itemRewards = Array.Empty<string>();

        [Header("Flags")]
        public bool isRepeatable;
        public bool isMainStory;

        public void ResetProgress()
        {
            if (objectives == null) return;
            foreach (var objective in objectives) objective.progress = 0;
        }

        public bool AllObjectivesComplete
        {
            get
            {
                if (objectives == null || objectives.Length == 0) return false;
                foreach (var objective in objectives)
                {
                    if (!objective.IsComplete) return false;
                }
                return true;
            }
        }
    }
}
