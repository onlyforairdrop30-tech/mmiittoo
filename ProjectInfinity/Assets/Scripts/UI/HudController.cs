using System.Collections.Generic;
using ProjectInfinity.Combat;
using ProjectInfinity.Core;
using ProjectInfinity.Enemies;
using ProjectInfinity.Inventory;
using ProjectInfinity.Player;
using ProjectInfinity.Progression;
using ProjectInfinity.Quests;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace ProjectInfinity.UI
{
    /// <summary>
    /// Builds the whole playable interface at runtime and keeps it in sync with the game state.
    /// Every control here is functional: bars read live values, ability buttons trigger real
    /// abilities and show real cooldowns, the inventory lists real items.
    /// </summary>
    public class HudController : MonoBehaviour
    {
        [SerializeField] private float noticeDuration = 4.5f;
        [SerializeField] private float textScale = 1f;

        private Canvas _canvas;
        private Text _nameText, _levelText, _xpHint, _questText, _noticeText, _goldText, _inventoryText;
        private Image _hpFill, _staminaFill, _energyFill, _xpFill, _bossFill;
        private RectTransform _bossPanel, _inventoryPanel;
        private Text _bossLabel;

        private readonly List<Text> _abilityCooldown = new List<Text>();
        private readonly List<Image> _abilityIcon = new List<Image>();
        private readonly List<(Text text, int baseSize)> _scalable = new List<(Text, int)>();
        private readonly List<string> _notices = new List<string>();

        private PlayerCombat _combat;
        private PlayerHealth _health;
        private PlayerProgression _progression;
        private InventoryComponent _inventory;
        private QuestLog _quests;
        private PlayerMotor _motor;
        private BossBrain _boss;
        private EnemyDefinition _bossDefinition;

        private float _noticeTimer;
        private bool _inventoryOpen;
        private string _questSignature = "";
        private string _inventorySignature = "";

        private void Start()
        {
            EnsureEventSystem();
            Acquire();
            Build();
            HookEvents();
        }

        private void OnDestroy()
        {
            var gm = GameManager.Instance;
            if (gm != null) gm.Notice -= PushNotice;
            if (_quests != null)
            {
                _quests.QuestAccepted -= OnQuestChanged;
                _quests.QuestProgressed -= OnQuestChanged;
                _quests.QuestCompleted -= OnQuestChanged;
            }
            if (_health != null) _health.HealthChanged -= OnHealthChanged;
        }

        private void Acquire()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            _progression = gm.Progression;
            _inventory = gm.Inventory;
            _quests = gm.Quests;
            _health = gm.PlayerHealth;
            _motor = gm.Motor;
            _combat = FindFirstObjectByType<PlayerCombat>();
            _boss = FindFirstObjectByType<BossBrain>();
            if (_boss != null)
            {
                _bossDefinition = _boss.Definition;
                _boss.Health.HealthChanged += OnHealthChanged;
            }
        }

        private void HookEvents()
        {
            var gm = GameManager.Instance;
            if (gm != null) gm.Notice += PushNotice;

            if (_quests != null)
            {
                _quests.QuestAccepted += OnQuestChanged;
                _quests.QuestProgressed += OnQuestChanged;
                _quests.QuestCompleted += OnQuestChanged;
            }
        }

        private void OnHealthChanged(float current, float max) { /* polled in Update */ }

        private void OnQuestChanged(QuestDefinition quest)
        {
            _questSignature = "";
            RebuildQuestText();
        }

        // ---------------------------------------------------------------- build

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;

            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var module = go.GetComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }

        private void Build()
        {
            _canvas = UiFactory.CreateCanvas("HUD", 10);
            var root = _canvas.transform;

            BuildVitals(root);
            BuildXpBar(root);
            BuildQuestTracker(root);
            BuildAbilityBar(root);
            BuildNotices(root);
            BuildBossBar(root);
            BuildInventory(root);

            ApplyTextScale();
        }

        private void BuildVitals(Transform root)
        {
            var panel = UiFactory.Panel(root, "Vitals", new Color(0.04f, 0.05f, 0.08f, 0.62f));
            UiFactory.Place(panel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                            new Vector2(24f, -24f), new Vector2(372f, 138f));

            _nameText = Track(UiFactory.Label(panel.transform, "Name", "Auralis", 24,
                                              TextAnchor.UpperLeft, Color.white));
            UiFactory.Place(_nameText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                            new Vector2(14f, -10f), new Vector2(240f, 30f));

            _levelText = Track(UiFactory.Label(panel.transform, "Level", "Lv 1", 22,
                                               TextAnchor.UpperRight, new Color(0.95f, 0.87f, 0.62f)));
            UiFactory.Place(_levelText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                            new Vector2(-14f, -10f), new Vector2(110f, 30f));

            _hpFill = MakeBar(panel.transform, "Health", new Color(0.80f, 0.24f, 0.30f), -50f, 20f);
            _staminaFill = MakeBar(panel.transform, "Stamina", new Color(0.32f, 0.78f, 0.46f), -76f, 15f);
            _energyFill = MakeBar(panel.transform, "Energy", new Color(0.36f, 0.62f, 0.98f), -96f, 15f);
        }

        private Image MakeBar(Transform parent, string name, Color color, float y, float height)
        {
            var background = UiFactory.Bar(parent, name, color, out var fill);
            UiFactory.Place(background.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                            new Vector2(14f, y), new Vector2(344f, height));
            return fill;
        }

        private void BuildXpBar(Transform root)
        {
            var background = UiFactory.Bar(root, "XpBar", new Color(0.88f, 0.74f, 0.36f), out _xpFill);
            UiFactory.Place(background.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                            new Vector2(0f, 26f), new Vector2(780f, 18f));

            _xpHint = Track(UiFactory.Label(root, "XpHint", "XP 0 / 100", 16,
                                            TextAnchor.LowerCenter, new Color(1f, 1f, 1f, 0.75f)));
            UiFactory.Place(_xpHint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                            new Vector2(0f, 46f), new Vector2(600f, 22f));

            _goldText = Track(UiFactory.Label(root, "Gold", "Gold 0", 20,
                                              TextAnchor.LowerLeft, new Color(0.95f, 0.87f, 0.62f)));
            UiFactory.Place(_goldText.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                            new Vector2(26f, 26f), new Vector2(260f, 26f));
        }

        private void BuildQuestTracker(Transform root)
        {
            var panel = UiFactory.Panel(root, "QuestTracker", new Color(0.04f, 0.05f, 0.08f, 0.55f));
            UiFactory.Place(panel.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                            new Vector2(-24f, -24f), new Vector2(380f, 170f));

            _questText = Track(UiFactory.Label(panel.transform, "Text", "No active quest", 19,
                                               TextAnchor.UpperLeft, Color.white));
            UiFactory.Place(_questText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                            new Vector2(14f, -12f), new Vector2(352f, 150f));
        }

        private void BuildAbilityBar(Transform root)
        {
            int count = _combat != null && _combat.abilities != null ? _combat.abilities.Length : 3;
            const float size = 86f;
            const float gap = 10f;

            for (int i = 0; i < count; i++)
            {
                int index = i;
                var button = UiFactory.Button(root, $"Ability{i}", _combat != null ? _combat.AbilityLabel(i) : "Ability",
                                              new Color(0.16f, 0.22f, 0.36f, 0.9f),
                                              () => { if (_combat != null) _combat.TryAbility(index); }, 17);
                UiFactory.Place(button.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f),
                                new Vector2(-24f - i * (size + gap), 96f), new Vector2(size, size));

                var cooldown = Track(UiFactory.Label(button.transform, "Cooldown", "", 26,
                                                     TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.5f)));
                UiFactory.Place(cooldown.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                Vector2.zero, new Vector2(size, size));

                _abilityCooldown.Add(cooldown);
                _abilityIcon.Add(button.GetComponent<Image>());
            }

            var ultimate = UiFactory.Button(root, "Ultimate", "Awakening\n(F)", new Color(0.42f, 0.18f, 0.36f, 0.92f),
                                           () => { if (_combat != null) _combat.TryUltimate(); }, 16);
            UiFactory.Place(ultimate.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f),
                            new Vector2(-24f - count * (size + gap), 96f), new Vector2(size, size));
            _abilityIcon.Add(ultimate.GetComponent<Image>());
            _abilityCooldown.Add(null);
        }

        private void BuildNotices(Transform root)
        {
            _noticeText = Track(UiFactory.Label(root, "Notices", "", 20, TextAnchor.UpperCenter,
                                                new Color(1f, 0.96f, 0.82f)));
            UiFactory.Place(_noticeText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            new Vector2(0f, -30f), new Vector2(900f, 120f));
        }

        private void BuildBossBar(Transform root)
        {
            var panel = UiFactory.Panel(root, "BossBar", new Color(0.08f, 0.02f, 0.03f, 0.7f));
            UiFactory.Place(panel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            new Vector2(0f, -170f), new Vector2(720f, 62f));
            _bossPanel = panel.rectTransform;

            _bossLabel = Track(UiFactory.Label(panel.transform, "BossName", "Boss", 22,
                                               TextAnchor.UpperCenter, new Color(1f, 0.85f, 0.85f)));
            UiFactory.Place(_bossLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            new Vector2(0f, -4f), new Vector2(700f, 26f));

            var bar = UiFactory.Bar(panel.transform, "BossHealth", new Color(0.85f, 0.3f, 0.25f), out _bossFill);
            UiFactory.Place(bar.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                            new Vector2(0f, 10f), new Vector2(690f, 20f));

            _bossPanel.gameObject.SetActive(false);
        }

        private void BuildInventory(Transform root)
        {
            var panel = UiFactory.Panel(root, "InventoryPanel", new Color(0.03f, 0.04f, 0.07f, 0.94f));
            UiFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                            Vector2.zero, new Vector2(620f, 470f));
            _inventoryPanel = panel.rectTransform;

            var title = Track(UiFactory.Label(panel.transform, "Title", "Satchel", 26,
                                              TextAnchor.UpperLeft, Color.white));
            UiFactory.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                            new Vector2(20f, -16f), new Vector2(400f, 34f));

            _inventoryText = Track(UiFactory.Label(panel.transform, "Items", "", 19,
                                                   TextAnchor.UpperLeft, new Color(0.9f, 0.92f, 1f)));
            UiFactory.Place(_inventoryText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                            new Vector2(20f, -60f), new Vector2(580f, 380f));

            var close = UiFactory.Button(panel.transform, "Close", "Close (Tab)", new Color(0.3f, 0.14f, 0.18f, 0.95f),
                                         () => ToggleInventory(false), 18);
            UiFactory.Place(close.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f),
                            new Vector2(-20f, 16f), new Vector2(150f, 42f));

            _inventoryPanel.gameObject.SetActive(false);
        }

        // ---------------------------------------------------------------- runtime

        private void Update()
        {
            if (_progression != null)
            {
                _levelText.text = $"Lv {_progression.Level}";
                long into = XpCurve.XpIntoLevel(_progression.TotalXp);
                long next = XpCurve.XpToNext(_progression.Level);
                UiFactory.SetFill(_xpFill, next <= 0 ? 1f : (float)into / next);
                _xpHint.text = next <= 0
                    ? $"MAX LEVEL   {_progression.TotalXp:N0} XP"
                    : $"XP {into:N0} / {next:N0}";
            }

            if (_health != null) UiFactory.SetFill(_hpFill, _health.Current / Mathf.Max(1f, _health.Max));

            if (_combat != null)
            {
                UiFactory.SetFill(_staminaFill, _combat.Stamina / Mathf.Max(1f, _combat.MaxStamina));
                UiFactory.SetFill(_energyFill, _combat.Energy / Mathf.Max(1f, _combat.MaxEnergy));

                for (int i = 0; i < _abilityCooldown.Count; i++)
                {
                    var label = _abilityCooldown[i];
                    if (label == null) continue;

                    float remaining = RemainingCooldown(i);
                    label.text = remaining > 0.05f ? Mathf.CeilToInt(remaining).ToString() : "";
                }
            }

            if (_inventory != null) _goldText.text = $"Gold {_inventory.Gold:N0}";

            if (GameManager.Instance != null && GameManager.Instance.Quests != null)
                RebuildQuestText();

            TickNotices();
            TickBoss();

            if (_inventoryOpen) RefreshInventory();

            var input = InputRouter.Instance;
            if (input != null && input.ToggleInventory.Consume()) ToggleInventory(!_inventoryOpen);
        }

        private float RemainingCooldown(int index)
        {
            if (_combat == null) return 0f;
            if (index >= _combat.abilities.Length) return 0f;
            var ability = _combat.abilities[index];
            return ability != null ? Mathf.Max(0f, ability.cooldownRemaining) : 0f;
        }

        private void TickNotices()
        {
            if (_notices.Count == 0)
            {
                if (_noticeText.text.Length > 0) _noticeText.text = "";
                return;
            }

            _noticeTimer -= Time.deltaTime;
            if (_noticeTimer <= 0f)
            {
                _notices.RemoveAt(0);
                _noticeTimer = _notices.Count > 0 ? noticeDuration : 0f;
            }

            _noticeText.text = string.Join("\n", _notices);
        }

        private void TickBoss()
        {
            if (_boss == null || _boss.Health == null)
            {
                if (_bossPanel.gameObject.activeSelf) _bossPanel.gameObject.SetActive(false);
                return;
            }

            bool engaged = _boss.Health.IsAlive && _boss.Health.Current < _boss.Health.Max;
            if (_bossPanel.gameObject.activeSelf != engaged) _bossPanel.gameObject.SetActive(engaged);
            if (!engaged) return;

            _bossLabel.text = $"{_boss.Title}   —   Phase {_boss.Phase}";
            UiFactory.SetFill(_bossFill, _boss.Health.Current / Mathf.Max(1f, _boss.Health.Max));
        }

        private void RebuildQuestText()
        {
            var quest = _quests != null ? _quests.Active : null;
            string signature;

            if (quest == null)
            {
                signature = "none";
                if (signature == _questSignature) return;
                _questSignature = signature;
                _questText.text = "No active quest";
                return;
            }

            var builder = new System.Text.StringBuilder();
            builder.Append("<b>").Append(quest.title).Append("</b>").Append('\n');
            if (quest.objectives != null)
            {
                foreach (var objective in quest.objectives)
                {
                    builder.Append(objective.IsComplete ? "[x] " : "[ ] ")
                           .Append(objective.ProgressLabel).Append('\n');
                }
            }

            signature = builder.ToString();
            if (signature == _questSignature) return;
            _questSignature = signature;
            _questText.text = signature;
        }

        private void RefreshInventory()
        {
            var model = _inventory != null ? _inventory.Model : null;
            if (model == null) return;

            var builder = new System.Text.StringBuilder();
            builder.Append("Gold: ").Append(_inventory.Gold).Append("      ")
                   .Append(model.UsedSlots).Append(" / ").Append(model.Capacity).Append(" slots\n\n");

            if (model.Slots.Count == 0) builder.Append("Empty");

            foreach (var slot in model.Slots)
            {
                var definition = _inventory.Database != null ? _inventory.Database.Get(slot.itemId) : null;
                string name = definition != null ? definition.displayName : slot.itemId;
                builder.Append(name).Append("  x").Append(slot.count);
                if (definition != null && definition.IsEquippable) builder.Append("   [equipment]");
                builder.Append('\n');
            }

            string text = builder.ToString();
            if (text == _inventorySignature) return;
            _inventorySignature = text;
            _inventoryText.text = text;
        }

        public void ToggleInventory(bool open)
        {
            _inventoryOpen = open;
            _inventoryPanel.gameObject.SetActive(open);
            _inventorySignature = "";
            if (_motor != null) _motor.ControlLocked = open;
        }

        private void PushNotice(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            _notices.Add(message);
            while (_notices.Count > 4) _notices.RemoveAt(0);
            _noticeTimer = noticeDuration;
            _noticeText.text = string.Join("\n", _notices);
        }

        // ---------------------------------------------------------------- accessibility

        private Text Track(Text text)
        {
            if (text != null) _scalable.Add((text, text.fontSize));
            return text;
        }

        public void SetTextScale(float scale)
        {
            textScale = Mathf.Clamp(scale, 0.8f, 1.6f);
            ApplyTextScale();
        }

        private void ApplyTextScale()
        {
            foreach (var entry in _scalable)
            {
                if (entry.text != null) entry.text.fontSize = Mathf.RoundToInt(entry.baseSize * textScale);
            }
        }
    }
}
