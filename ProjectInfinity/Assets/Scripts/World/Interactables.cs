using System.Collections.Generic;
using ProjectInfinity.Core;
using ProjectInfinity.Player;
using UnityEngine;

namespace ProjectInfinity.World
{
    /// <summary>Loot dropped in the world. Collected on touch, so it also works on mobile.</summary>
    [RequireComponent(typeof(Collider))]
    public class Pickup : MonoBehaviour
    {
        public string itemId = "material.ember_shard";
        public int count = 1;
        public float spinSpeed = 90f;

        private bool _taken;

        private void Update()
        {
            transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_taken) return;
            if (other.GetComponentInParent<PlayerMotor>() == null) return;

            var gm = GameManager.Instance;
            if (gm?.Inventory == null) return;

            if (!gm.Inventory.TryGive(itemId, count))
            {
                gm.PushNotice("Inventory full");
                return;
            }

            _taken = true;
            gm.Quests?.ReportCollect(itemId, count);
            Destroy(gameObject);
        }
    }

    /// <summary>Respawn point and healing shrine.</summary>
    [RequireComponent(typeof(Collider))]
    public class Shrine : MonoBehaviour
    {
        public string locationId = "shrine.verdant";
        [SerializeField] private float healAmount = 200f;

        private void OnTriggerEnter(Collider other)
        {
            var gm = GameManager.Instance;
            if (gm?.PlayerHealth == null) return;
            if (other.GetComponentInParent<PlayerMotor>() == null) return;

            gm.PlayerHealth.Heal(healAmount);
            gm.Quests?.ReportReach(locationId);
        }
    }

    /// <summary>Gives the player a quest when they walk up to it (the region's first NPC).</summary>
    [RequireComponent(typeof(Collider))]
    public class QuestGiver : MonoBehaviour
    {
        public string npcName = "Warden Sylva";
        public ProjectInfinity.Quests.QuestDefinition quest;
        public string greeting = "The Hollow is sick. Cut the blight back and I will open the sealed path.";

        private bool _given;

        private void OnTriggerEnter(Collider other)
        {
            if (_given || other.GetComponentInParent<PlayerMotor>() == null) return;

            var gm = GameManager.Instance;
            if (gm?.Quests == null || quest == null) return;

            if (gm.Quests.Accept(quest))
            {
                _given = true;
                gm.PushNotice($"{npcName}: {greeting}");
            }
        }
    }

    /// <summary>Fast travel destination. Registered statically for the map panel.</summary>
    public class FastTravelPoint : MonoBehaviour
    {
        public static readonly List<FastTravelPoint> All = new List<FastTravelPoint>();

        public string locationId = "travel.point";
        public string displayName = "Waypoint";

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        public static bool TravelTo(string id)
        {
            var gm = GameManager.Instance;
            if (gm?.Motor == null) return false;

            foreach (var point in All)
            {
                if (point.locationId != id) continue;
                gm.Motor.Teleport(point.transform.position + Vector3.up * 1.2f);
                gm.PushNotice($"Travelled to {point.displayName}");
                return true;
            }
            return false;
        }
    }
}
