using System.Collections.Generic;
using ProjectInfinity.Animation;
using ProjectInfinity.Audio;
using ProjectInfinity.Combat;
using ProjectInfinity.Core;
using ProjectInfinity.Enemies;
using ProjectInfinity.Inventory;
using ProjectInfinity.Player;
using ProjectInfinity.Progression;
using ProjectInfinity.Quests;
using ProjectInfinity.UI;
using ProjectInfinity.World;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace ProjectInfinity.EditorTools
{
    /// <summary>
    /// Builds the whole playable prototype scene from code: the Verdant Hollow region, an original
    /// humanoid character, the camera rig, three enemy species, the Rootwarden boss, pickups, the
    /// quest giver, HUD, touch controls and a baked NavMesh.
    /// Everything is deterministic (fixed seeds) so the scene is reproducible and reviewable in Git.
    /// </summary>
    public static class PrototypeSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Prototype_Auralis.unity";
        private const string RegionName = "Verdant Hollow";

        private static readonly Dictionary<string, Material> Mats = new Dictionary<string, Material>();

        [MenuItem("Project Infinity/2. Build Prototype Scene", priority = 2)]
        public static void BuildFromMenu() => Build();

        /// <summary>Batch-mode entry point: <c>-executeMethod ProjectInfinity.EditorTools.PrototypeSceneBuilder.BuildFromCommandLine</c></summary>
        public static void BuildFromCommandLine()
        {
            bool ok = Build();
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        public static bool Build()
        {
            try
            {
                ContentSeeder.EnsureFolder("Assets/Scenes");
                ContentSeeder.EnsureFolder("Assets/Settings");

                var db = ContentSeeder.CreateAll(out var mobs, out var bossDefinition, out var quest);

                Mats.Clear();
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                ConfigureLighting();
                BuildEnvironment();
                var player = BuildPlayer(out var animator, out var router, out var motor,
                                         out var combat, out var playerHealth,
                                         out var progression, out var inventory, out var questLog);
                var cameraRig = BuildCamera(player.transform, router);
                motor.Bind(router, cameraRig);

                var game = CreateGameManager();
                BuildEnemies(mobs, playerHealth, game);
                BuildBoss(bossDefinition, playerHealth, game);
                BuildPickups();
                BuildQuestGiver(quest);
                BindGameManager(game, progression, inventory, questLog, playerHealth, motor);
                BuildInterface(router);
                BakeNavMesh();

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene, ScenePath);
                AddSceneToBuildSettings();

                Debug.Log($"[Project Infinity] '{ScenePath}' built: {RegionName}, " +
                          $"{mobs.Count} enemy types, boss '{bossDefinition.displayName}'.");
                Selection.activeGameObject = player;
                return true;
            }
            catch (System.Exception exception)
            {
                Debug.LogError("[Project Infinity] Prototype scene build failed:\n" + exception);
                return false;
            }
        }

        // ---------------------------------------------------------------- lighting

        private static void ConfigureLighting()
        {
            var sunGo = new GameObject("Sun", typeof(Light));
            var sun = sunGo.GetComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.15f;
            sun.color = new Color(1f, 0.96f, 0.9f);
            sun.shadows = LightShadows.Soft;
            sunGo.transform.rotation = Quaternion.Euler(48f, 35f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.5f, 0.6f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 60f;
            RenderSettings.fogEndDistance = 190f;
            RenderSettings.fogColor = new Color(0.62f, 0.74f, 0.82f);

            var atmosphereGo = new GameObject("Atmosphere", typeof(WorldAtmosphere));
            var atmosphere = atmosphereGo.GetComponent<WorldAtmosphere>();
            atmosphere.sun = sun;
            atmosphere.startHour = 9.5f;

            // A second "floating island" silhouette sells the wider world beyond the playable region.
            var island = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            island.name = "Distant_FloatingIsland";
            Object.DestroyImmediate(island.GetComponent<Collider>());
            island.transform.position = new Vector3(-90f, 46f, 140f);
            island.transform.localScale = new Vector3(46f, 9f, 34f);
            island.GetComponent<Renderer>().sharedMaterial = Mat("island", new Color(0.45f, 0.55f, 0.6f));
        }

        // ---------------------------------------------------------------- world

        private static void BuildEnvironment()
        {
            var root = new GameObject("Region_" + RegionName).transform;

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(root, false);
            ground.transform.localScale = new Vector3(24f, 1f, 24f); // 240 x 240 m
            ground.GetComponent<Renderer>().sharedMaterial = Mat("grass", new Color(0.30f, 0.46f, 0.28f));

            // Rolling hills ringing the playable bowl.
            var rng = new System.Random(20261119);
            for (int i = 0; i < 14; i++)
            {
                float angle = (float)(rng.NextDouble() * Mathf.PI * 2.0);
                float radius = 70f + (float)rng.NextDouble() * 40f;
                var hill = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                hill.name = "Hill_" + i;
                Object.DestroyImmediate(hill.GetComponent<Collider>());
                hill.transform.SetParent(root, false);
                hill.transform.position = new Vector3(Mathf.Cos(angle) * radius, -6f, Mathf.Sin(angle) * radius);
                hill.transform.localScale = new Vector3(38f + (float)rng.NextDouble() * 22f, 16f,
                                                        38f + (float)rng.NextDouble() * 22f);
                hill.GetComponent<Renderer>().sharedMaterial = Mat("hill", new Color(0.24f, 0.38f, 0.26f));
            }

            // Pond
            var pond = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pond.name = "Pond";
            Object.DestroyImmediate(pond.GetComponent<Collider>());
            pond.transform.SetParent(root, false);
            pond.transform.position = new Vector3(-24f, 0.02f, -18f);
            pond.transform.localScale = new Vector3(18f, 0.02f, 18f);
            pond.GetComponent<Renderer>().sharedMaterial = Mat("water", new Color(0.18f, 0.42f, 0.55f));

            // Old stone path from the shrine towards the boss clearing.
            var path = GameObject.CreatePrimitive(PrimitiveType.Cube);
            path.name = "Path";
            Object.DestroyImmediate(path.GetComponent<Collider>());
            path.transform.SetParent(root, false);
            path.transform.position = new Vector3(0f, 0.03f, 18f);
            path.transform.localScale = new Vector3(4.5f, 0.06f, 46f);
            path.GetComponent<Renderer>().sharedMaterial = Mat("stone", new Color(0.55f, 0.53f, 0.48f));

            BuildTrees(root, rng);
            BuildRuins(root);

            var shrine = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shrine.name = "Shrine";
            shrine.transform.SetParent(root, false);
            shrine.transform.position = new Vector3(0f, 0.15f, -4f);
            shrine.transform.localScale = new Vector3(5f, 0.15f, 5f);
            shrine.GetComponent<Renderer>().sharedMaterial = Mat("shrine", new Color(0.72f, 0.68f, 0.52f));

            var shrineTrigger = new GameObject("ShrineTrigger", typeof(SphereCollider), typeof(Shrine)).GetComponent<Shrine>();
            shrineTrigger.transform.SetParent(root, false);
            shrineTrigger.transform.position = new Vector3(0f, 1f, -4f);
            var sphere = shrineTrigger.GetComponent<SphereCollider>();
            sphere.radius = 4.5f;
            sphere.isTrigger = true;
            shrineTrigger.locationId = "shrine.verdant";

            var travel = new GameObject("FastTravel_Verdant", typeof(FastTravelPoint));
            travel.transform.SetParent(root, false);
            travel.transform.position = new Vector3(7.5f, 0.1f, -4f);
            var travelPoint = travel.GetComponent<FastTravelPoint>();
            travelPoint.locationId = "travel.verdant_shrine";
            travelPoint.displayName = "Verdant Shrine";
            var pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pad.name = "TravelPad";
            Object.DestroyImmediate(pad.GetComponent<Collider>());
            pad.transform.SetParent(travel.transform, false);
            pad.transform.localScale = new Vector3(2.4f, 0.06f, 2.4f);
            pad.GetComponent<Renderer>().sharedMaterial = Mat("travel", new Color(0.35f, 0.78f, 0.9f));

            var lightOrb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lightOrb.name = "ShrineOrb";
            Object.DestroyImmediate(lightOrb.GetComponent<Collider>());
            lightOrb.transform.SetParent(root, false);
            lightOrb.transform.position = new Vector3(0f, 3.2f, -4f);
            lightOrb.transform.localScale = Vector3.one * 0.9f;
            lightOrb.GetComponent<Renderer>().sharedMaterial = Mat("orb", new Color(0.55f, 0.95f, 0.85f));
        }

        private static void BuildTrees(Transform root, System.Random rng)
        {
            for (int i = 0; i < 46; i++)
            {
                float angle = (float)(rng.NextDouble() * Mathf.PI * 2.0);
                float radius = 22f + (float)rng.NextDouble() * 46f;

                var tree = new GameObject("Tree_" + i).transform;
                tree.SetParent(root, false);
                tree.position = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

                var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Object.DestroyImmediate(trunk.GetComponent<Collider>());
                trunk.transform.SetParent(tree, false);
                trunk.transform.localPosition = new Vector3(0f, 2f, 0f);
                trunk.transform.localScale = new Vector3(0.55f, 2f, 0.55f);
                trunk.GetComponent<Renderer>().sharedMaterial = Mat("bark", new Color(0.28f, 0.22f, 0.16f));

                var canopy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.DestroyImmediate(canopy.GetComponent<Collider>());
                canopy.transform.SetParent(tree, false);
                canopy.transform.localPosition = new Vector3(0f, 5.4f, 0f);
                float size = 2.6f + (float)rng.NextDouble() * 1.8f;
                canopy.transform.localScale = new Vector3(size, size * 0.85f, size);
                canopy.GetComponent<Renderer>().sharedMaterial = Mat("canopy", new Color(0.22f, 0.44f, 0.26f));
            }
        }

        private static void BuildRuins(Transform root)
        {
            var rng = new System.Random(551);
            for (int i = 0; i < 7; i++)
            {
                float angle = i / 7f * Mathf.PI * 2f;
                var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pillar.name = "Pillar_" + i;
                pillar.transform.SetParent(root, false);
                pillar.transform.position = new Vector3(Mathf.Cos(angle) * 16f, 2.2f, Mathf.Sin(angle) * 16f + 8f);
                float height = 2.4f + (float)rng.NextDouble() * 2.2f;
                pillar.transform.localScale = new Vector3(0.9f, height * 0.5f, 0.9f);
                pillar.GetComponent<Renderer>().sharedMaterial = Mat("ruin", new Color(0.6f, 0.58f, 0.52f));
            }

            var arch = GameObject.CreatePrimitive(PrimitiveType.Cube);
            arch.name = "RuinedArch";
            arch.transform.SetParent(root, false);
            arch.transform.position = new Vector3(16f, 4f, -16f);
            arch.transform.localScale = new Vector3(6f, 0.7f, 1.2f);
            arch.GetComponent<Renderer>().sharedMaterial = Mat("ruin", new Color(0.6f, 0.58f, 0.52f));
        }

        // ---------------------------------------------------------------- player

        private static GameObject BuildPlayer(out ProceduralAnimator animator, out InputRouter router,
                                              out PlayerMotor motor, out PlayerCombat combat,
                                              out PlayerHealth health, out PlayerProgression progression,
                                              out InventoryComponent inventory, out QuestLog questLog)
        {
            var root = new GameObject("Player_Auralis");

            var rig = BuildHumanoid(root.transform, "Auralis", new Color(0.20f, 0.52f, 0.55f),
                                    new Color(0.94f, 0.82f, 0.68f), 1f, out animator);
            rig.name = "Rig";

            var controller = root.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.34f;
            controller.center = new Vector3(0f, 0.92f, 0f);
            controller.slopeLimit = 50f;
            controller.stepOffset = 0.4f;

            router = root.AddComponent<InputRouter>();
            var reader = root.AddComponent<PlayerInputReader>();
            reader.Bind(router);

            motor = root.AddComponent<PlayerMotor>();
            combat = root.AddComponent<PlayerCombat>();
            health = root.AddComponent<PlayerHealth>();

            progression = root.AddComponent<PlayerProgression>();
            inventory = root.AddComponent<InventoryComponent>();
            var loadout = root.AddComponent<EquipmentLoadout>();
            loadout.Bind(progression);

            questLog = root.AddComponent<QuestLog>();

            combat.progression = progression;
            combat.motor = motor;
            combat.health = health;
            combat.animator = animator;
            combat.input = router;
            combat.abilities = new[]
            {
                new PlayerCombat.AbilitySlot
                {
                    abilityName = "Verdant Lash", keyLabel = "Q", power = 2.6f, radius = 3.2f,
                    reach = 2.4f, cooldown = 5f, energyCost = 18f
                },
                new PlayerCombat.AbilitySlot
                {
                    abilityName = "Ember Nova", keyLabel = "R", power = 3.5f, radius = 5.2f,
                    reach = 0f, cooldown = 9f, energyCost = 26f
                },
                new PlayerCombat.AbilitySlot
                {
                    abilityName = "Gale Step", keyLabel = "T", power = 2.2f, radius = 4.2f,
                    reach = 3.4f, cooldown = 7f, energyCost = 21f
                }
            };

            animator.motor = motor;
            health.Bind(combat);
            root.transform.position = new Vector3(0f, 0.15f, -8f);
            root.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            return root;
        }

        private static GameObject BuildHumanoid(Transform parent, string name, Color clothing, Color skin,
                                                float scale, out ProceduralAnimator animator)
        {
            var root = new GameObject(name + "_Rig").transform;
            root.SetParent(parent, false);
            root.localScale = Vector3.one * scale;

            var clothingMat = Mat("cloth_" + name, clothing);
            var skinMat = Mat("skin_" + name, skin);
            var accentMat = Mat("accent_" + name, Color.Lerp(clothing, Color.black, 0.35f));

            var hips = Pivot(root, "Hips", new Vector3(0f, 0.95f, 0f));
            var torso = Pivot(hips, "Torso", new Vector3(0f, 0.08f, 0f));

            Part(torso, "TorsoMesh", PrimitiveType.Capsule, new Vector3(0f, 0.24f, 0f),
                 new Vector3(0.62f, 0.30f, 0.46f), clothingMat);
            Part(torso, "Cape", PrimitiveType.Cube, new Vector3(0f, 0.16f, -0.20f),
                 new Vector3(0.52f, 0.66f, 0.06f), accentMat);

            var head = Pivot(torso, "Head", new Vector3(0f, 0.52f, 0f));
            Part(head, "HeadMesh", PrimitiveType.Sphere, new Vector3(0f, 0.15f, 0f),
                 Vector3.one * 0.34f, skinMat);
            Part(head, "Hair", PrimitiveType.Sphere, new Vector3(0f, 0.24f, -0.02f),
                 new Vector3(0.37f, 0.28f, 0.37f), Mat("hair_" + name, new Color(0.16f, 0.2f, 0.28f)));

            var armLeft = Limb(torso, "ArmLeft", new Vector3(-0.34f, 0.34f, 0f), 0.62f, 0.15f, clothingMat, skinMat);
            var armRight = Limb(torso, "ArmRight", new Vector3(0.34f, 0.34f, 0f), 0.62f, 0.15f, clothingMat, skinMat);

            var weapon = Part(armRight, "Weapon", PrimitiveType.Cube, new Vector3(0f, -0.72f, 0f),
                              new Vector3(0.07f, 0.9f, 0.07f), Mat("blade_" + name, new Color(0.78f, 0.82f, 0.86f)));
            weapon.localRotation = Quaternion.Euler(12f, 0f, 0f);

            var legLeft = Limb(hips, "LegLeft", new Vector3(-0.17f, -0.04f, 0f), 0.82f, 0.17f, accentMat, accentMat);
            var legRight = Limb(hips, "LegRight", new Vector3(0.17f, -0.04f, 0f), 0.82f, 0.17f, accentMat, accentMat);

            animator = root.gameObject.AddComponent<ProceduralAnimator>();
            animator.torso = torso;
            animator.head = head;
            animator.armLeft = armLeft;
            animator.armRight = armRight;
            animator.legLeft = legLeft;
            animator.legRight = legRight;
            animator.weapon = weapon;

            return root.gameObject;
        }

        private static Transform Pivot(Transform parent, string name, Vector3 localPosition)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = localPosition;
            return pivot;
        }

        private static Transform Part(Transform parent, string name, PrimitiveType type, Vector3 localPos,
                                      Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            var collider = go.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);

            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go.transform;
        }

        private static Transform Limb(Transform parent, string name, Vector3 shoulder, float length,
                                      float radius, Material upper, Material lower)
        {
            var pivot = Pivot(parent, name, shoulder);
            Part(pivot, name + "_Upper", PrimitiveType.Capsule, new Vector3(0f, -length * 0.5f, 0f),
                 new Vector3(radius * 2f, length * 0.5f, radius * 2f), upper);
            Part(pivot, name + "_Lower", PrimitiveType.Capsule, new Vector3(0f, -length * 1.15f, 0f),
                 new Vector3(radius * 1.7f, length * 0.35f, radius * 1.7f), lower);
            return pivot;
        }

        private static ThirdPersonCamera BuildCamera(Transform target, InputRouter router)
        {
            var cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.GetComponent<Camera>();
            camera.fieldOfView = 56f;
            camera.nearClipPlane = 0.12f;
            camera.farClipPlane = 400f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.7f, 0.82f);

            var rig = cameraGo.AddComponent<ThirdPersonCamera>();
            rig.Bind(target, router);
            rig.SetOrbit(0f, 16f);

            cameraGo.transform.position = target.position + new Vector3(0f, 2.4f, -5.5f);
            return rig;
        }

        // ---------------------------------------------------------------- enemies

        private static void BuildEnemies(List<EnemyDefinition> definitions, Health playerHealth, Core.GameManager game)
        {
            var root = new GameObject("Enemies").transform;
            var rng = new System.Random(4242);

            for (int i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                for (int copy = 0; copy < 2; copy++)
                {
                    float angle = (float)(rng.NextDouble() * Mathf.PI * 2.0);
                    float radius = 12f + (float)rng.NextDouble() * 18f;
                    var position = new Vector3(Mathf.Cos(angle) * radius, 0.1f, 6f + Mathf.Sin(angle) * radius);

                    var enemy = BuildEnemy(definition, position, playerHealth, game);
                    enemy.transform.SetParent(root, true);
                }
            }
        }

        private static GameObject BuildEnemy(EnemyDefinition definition, Vector3 position, Health playerHealth,
                                             Core.GameManager game)
        {
            var root = new GameObject("Enemy_" + definition.enemyId);
            root.transform.position = position;

            var rigRoot = BuildHumanoid(root.transform, definition.enemyId, definition.tint,
                                        Color.Lerp(definition.tint, Color.white, 0.35f),
                                        definition.scale, out var animator);

            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.height = 1.8f * definition.scale;
            capsule.radius = 0.36f * definition.scale;
            capsule.center = new Vector3(0f, 0.9f * definition.scale, 0f);

            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = capsule.radius;
            agent.height = capsule.height;
            agent.baseOffset = 0f;

            animator.agent = agent;

            root.AddComponent<Health>();
            var brain = root.AddComponent<EnemyBrain>();
            brain.Configure(definition, playerHealth, animator);
            if (game != null) brain.Died += game.RegisterKill;

            return root;
        }

        private static void BuildBoss(EnemyDefinition definition, Health playerHealth, Core.GameManager game)
        {
            var root = new GameObject("Boss_" + definition.enemyId);
            root.transform.position = new Vector3(0f, 0.1f, 40f);

            BuildHumanoid(root.transform, definition.enemyId, definition.tint,
                          Color.Lerp(definition.tint, Color.white, 0.3f), definition.scale, out var animator);

            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.height = 1.9f * definition.scale;
            capsule.radius = 0.45f * definition.scale;
            capsule.center = new Vector3(0f, 0.95f * definition.scale, 0f);

            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = capsule.radius;
            agent.height = capsule.height;

            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "TelegraphRing";
            Object.DestroyImmediate(ring.GetComponent<Collider>());
            ring.transform.SetParent(root, false);
            ring.transform.localPosition = new Vector3(0f, 0.06f, 0f);
            ring.transform.localScale = new Vector3(5f, 0.04f, 5f);
            ring.GetComponent<Renderer>().sharedMaterial = Mat("telegraph", new Color(0.95f, 0.35f, 0.25f));

            animator.agent = agent;

            root.AddComponent<Health>();
            var boss = root.AddComponent<BossBrain>();
            boss.Configure(definition, playerHealth, animator, ring.transform);
            if (game != null) boss.Defeated += game.RegisterBossKill;
        }

        // ---------------------------------------------------------------- props

        private static void BuildPickups()
        {
            var root = new GameObject("Pickups").transform;
            var rng = new System.Random(9090);
            var material = Mat("shard", new Color(0.98f, 0.62f, 0.22f));

            for (int i = 0; i < 10; i++)
            {
                float angle = (float)(rng.NextDouble() * Mathf.PI * 2.0);
                float radius = 8f + (float)rng.NextDouble() * 22f;

                var pickupGo = new GameObject("Pickup_EmberShard_" + i);
                pickupGo.transform.SetParent(root, false);
                pickupGo.transform.position = new Vector3(Mathf.Cos(angle) * radius, 1.1f,
                                                          Mathf.Sin(angle) * radius + 4f);

                var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(visual.GetComponent<Collider>());
                visual.name = "Visual";
                visual.transform.SetParent(pickupGo.transform, false);
                visual.transform.localScale = Vector3.one * 0.45f;
                visual.transform.localRotation = Quaternion.Euler(35f, 20f, 15f);
                visual.GetComponent<Renderer>().sharedMaterial = material;

                var trigger = pickupGo.AddComponent<SphereCollider>();
                trigger.radius = 1.4f;
                trigger.isTrigger = true;

                var pickup = pickupGo.AddComponent<Pickup>();
                pickup.itemId = ContentSeeder.ShardId;
                pickup.count = 1;
            }
        }

        private static void BuildQuestGiver(QuestDefinition quest)
        {
            var root = new GameObject("NPC_WardenSylva");
            root.transform.position = new Vector3(-6f, 0.1f, -2f);

            BuildHumanoid(root.transform, "sylva", new Color(0.52f, 0.32f, 0.55f),
                          new Color(0.9f, 0.78f, 0.66f), 1.02f, out _);

            var trigger = root.AddComponent<CapsuleCollider>();
            trigger.radius = 2.6f;
            trigger.height = 2f;
            trigger.center = new Vector3(0f, 1f, 0f);
            trigger.isTrigger = true;

            var giver = root.AddComponent<QuestGiver>();
            giver.quest = quest;
        }

        private static Core.GameManager CreateGameManager()
        {
            var managerGo = new GameObject("Game");
            var manager = managerGo.AddComponent<Core.GameManager>();

            var audioGo = new GameObject("Audio", typeof(AudioSource), typeof(ProceduralSfx));
            audioGo.transform.position = Vector3.zero;
            audioGo.GetComponent<AudioSource>().spatialBlend = 0f;

            return manager;
        }

        private static void BindGameManager(Core.GameManager manager, PlayerProgression progression,
                                            InventoryComponent inventory, QuestLog questLog,
                                            PlayerHealth health, PlayerMotor motor)
        {
            if (manager == null) return;

            inventory.Bind(AssetDatabase.LoadAssetAtPath<ItemDatabase>(ContentSeeder.ItemDbPath));

            var shrine = Object.FindFirstObjectByType<Shrine>();
            manager.Bind(progression, inventory, questLog, health, motor,
                         shrine != null ? shrine.transform : null);
        }

        private static void BuildInterface(InputRouter router)
        {
            var uiGo = new GameObject("UI", typeof(HudController), typeof(TouchControls));
            var touch = uiGo.GetComponent<TouchControls>();
            if (router != null) touch.Bind(router);
        }

        private static void BakeNavMesh()
        {
            var surfaceGo = new GameObject("NavMesh");
            var surface = surfaceGo.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;

            try
            {
                surface.BuildNavMesh();
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("[Project Infinity] NavMesh bake failed; enemies will use direct steering. " +
                                 exception.Message);
            }
        }

        private static void AddSceneToBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var scene in scenes)
            {
                if (scene.path == ScenePath) return;
            }
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ---------------------------------------------------------------- materials

        private static Material Mat(string key, Color color)
        {
            if (Mats.TryGetValue(key, out var existing) && existing != null) return existing;

            const string folder = "Assets/Settings/Materials";
            ContentSeeder.EnsureFolder(folder);

            string safe = Sanitize(key);
            string path = folder + "/MI_" + safe + ".mat";

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                if (shader == null) shader = Shader.Find("Sprites/Default");

                material = new Material(shader) { name = "MI_" + safe };
                AssetDatabase.CreateAsset(material, path);
            }

            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.18f);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0.05f);
            EditorUtility.SetDirty(material);

            Mats[key] = material;
            return material;
        }

        private static string Sanitize(string key)
        {
            var builder = new System.Text.StringBuilder(key.Length);
            foreach (char character in key) builder.Append(char.IsLetterOrDigit(character) ? character : '_');
            return builder.ToString();
        }
    }
}
