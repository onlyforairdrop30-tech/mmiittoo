using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectInfinity.Quests
{
    /// <summary>Tracks the player's active quest and objective progress, and raises completion.</summary>
    public class QuestLog : MonoBehaviour
    {
        [SerializeField] private QuestDefinition activeQuest;

        private readonly List<string> _completed = new List<string>();

        public QuestDefinition Active => activeQuest;
        public IReadOnlyList<string> Completed => _completed;

        public event Action<QuestDefinition> QuestAccepted;
        public event Action<QuestDefinition> QuestProgressed;
        public event Action<QuestDefinition> QuestCompleted;

        public void Bind(QuestDefinition initial)
        {
            activeQuest = initial;
            if (activeQuest != null) QuestAccepted?.Invoke(activeQuest);
        }

        public bool Accept(QuestDefinition quest)
        {
            if (quest == null) return false;
            if (activeQuest != null && !activeQuest.isRepeatable) return false;
            if (!quest.isRepeatable && _completed.Contains(quest.questId)) return false;

            quest.ResetProgress();
            activeQuest = quest;
            QuestAccepted?.Invoke(quest);
            return true;
        }

        public void ReportKill(string enemyId) => Advance(QuestObjectiveType.Kill, enemyId, 1);
        public void ReportCollect(string itemId, int count = 1) => Advance(QuestObjectiveType.Collect, itemId, count);
        public void ReportReach(string locationId) => Advance(QuestObjectiveType.Reach, locationId, 1);
        public void ReportTalk(string npcId) => Advance(QuestObjectiveType.Talk, npcId, 1);

        private void Advance(QuestObjectiveType type, string targetId, int amount)
        {
            if (activeQuest == null || activeQuest.objectives == null) return;

            bool changed = false;
            foreach (var objective in activeQuest.objectives)
            {
                if (objective.type != type) continue;
                if (!string.IsNullOrEmpty(objective.targetId) && objective.targetId != targetId) continue;
                if (objective.IsComplete) continue;

                objective.progress = Mathf.Min(objective.required, objective.progress + amount);
                changed = true;
            }

            if (!changed) return;

            QuestProgressed?.Invoke(activeQuest);

            if (activeQuest.AllObjectivesComplete) CompleteActive();
        }

        private void CompleteActive()
        {
            var quest = activeQuest;
            if (quest == null) return;

            if (!_completed.Contains(quest.questId)) _completed.Add(quest.questId);
            QuestCompleted?.Invoke(quest);

            if (quest.isRepeatable)
            {
                quest.ResetProgress();
            }
            else
            {
                activeQuest = null;
            }
        }
    }
}
