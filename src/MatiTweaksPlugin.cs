using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using BlockStoryCore;

namespace BlockStoryMod
{
    [BepInPlugin("com.malts.blockstory.matitweaks", "MatiTweaks", "2.2.0")]
    [BepInDependency(Core.Guid)]
    public class MatiFixPlugin : BaseUnityPlugin
    {
        private Harmony _harmony;

        public static bool Enabled = PlayerPrefs.GetInt("MatiFix_Enabled", 1) != 0;
        public static bool UnmountedAIBoost = PlayerPrefs.GetInt("MatiFix_UnmountedAIBoost", 1) != 0;
        public static bool WallSensing = PlayerPrefs.GetInt("MatiFix_WallSensing", 1) != 0;
        public static bool EnhancedRange = PlayerPrefs.GetInt("MatiFix_EnhancedRange", 1) != 0;
        public static bool DarkDamage = PlayerPrefs.GetInt("MatiFix_DarkDamage", 1) != 0;

        private float _aiScanTimer = 0f;
        private readonly Dictionary<int, float> _attackTimers = new Dictionary<int, float>();

        private static readonly FieldInfo BlastEffectField = typeof(BehaviourController).GetField("blastEffect", BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo EffectOffsetField = typeof(BehaviourController).GetField("effectOffset", BindingFlags.Public | BindingFlags.Instance);

        private void Awake()
        {
            _harmony = new Harmony("com.malts.blockstory.matitweaks");
            _harmony.PatchAll();

            ModInfo modInfo = new ModInfo
            {
                Name = "Mati Tweaks",
                Description = "Major overhauls to the Mati pet to make it not a waste of time.",
                GetEnabled = () => Enabled,
                SetEnabled = on => { Enabled = on; PlayerPrefs.SetInt("MatiFix_Enabled", on ? 1 : 0); PlayerPrefs.Save(); },
                HasConfig = true,
            };

            Action configAction = MatiFixConfig.OpenFromMenu;
            string[] possibleNames = new string[] { "OpenConfig", "OnOpenConfig", "OnConfig", "Config", "OpenMenu", "OnConfigOpen", "ConfigAction" };

            foreach (string name in possibleNames)
            {
                PropertyInfo prop = typeof(ModInfo).GetProperty(name);
                if (prop != null && prop.PropertyType == typeof(Action))
                {
                    prop.SetValue(modInfo, configAction, null);
                    break;
                }
                FieldInfo field = typeof(ModInfo).GetField(name);
                if (field != null && field.FieldType == typeof(Action))
                {
                    field.SetValue(modInfo, configAction);
                    break;
                }
            }

            ModRegistry.Register(modInfo);
            Core.Log?.LogInfo("[MatiTweaks]: Loaded successfully.");
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }

        private void OnGUI()
        {
            MatiFixConfig.Draw();
        }

        private void Update()
        {
            if (!Enabled) return;

            _aiScanTimer += Time.deltaTime;
            if (_aiScanTimer >= 0.25f)
            {
                _aiScanTimer = 0f;
                FixMatiAI();
            }
        }

        private void FixMatiAI()
        {
#pragma warning disable CS0618
            BehaviourController[] controllers = UnityEngine.Object.FindObjectsOfType<BehaviourController>();
#pragma warning restore CS0618

            foreach (var controller in controllers)
            {
                if (!IsMati(controller)) continue;

                if (!UnmountedAIBoost) continue;

                int id = controller.GetInstanceID();
                if (!_attackTimers.ContainsKey(id))
                {
                    _attackTimers[id] = Time.time;
                }

                GameObject target = GetMatiTarget(controller);

                if (target != null && !Inventory.isPaused)
                {
                    float dist = Vector3.Distance(controller.transform.position, target.transform.position);
                    float maxCastDist = EnhancedRange ? 60f : 30f;

                    if (dist <= maxCastDist && Time.time >= _attackTimers[id] + 1.0f)
                    {
                        _attackTimers[id] = Time.time;
                        StartCoroutine(ExecuteTeleportCombo(controller, target));
                    }
                }
            }

            if (_attackTimers.Count > 32)
            {
                _attackTimers.Clear();
            }
        }

        private IEnumerator ExecuteTeleportCombo(BehaviourController controller, GameObject target)
        {
            if (controller == null || target == null) yield break;

            controller.SpawnTeleportEffect();

            Vector3 targetPos = target.transform.position + Vector3.up * 1.2f + UnityEngine.Random.insideUnitSphere * 1.5f;
            controller.transform.position = targetPos;
            controller.SpawnTeleportEffect();

            yield return new WaitForSeconds(0.05f);

            ExecuteFixedMagicBlast(controller);

            yield return new WaitForSeconds(0.35f);

            if (controller != null && controller.player != null)
            {
                controller.SpawnTeleportEffect();
                Vector3 safePos = controller.player.transform.position + UnityEngine.Random.onUnitSphere * 4f;
                safePos.y = controller.player.transform.position.y + 1.5f;
                controller.transform.position = safePos;
                controller.SpawnTeleportEffect();
            }
        }

        public static void ExecuteFixedMagicBlast(BehaviourController controller)
        {
            if (controller == null) return;

            GameObject blastFx = BlastEffectField?.GetValue(controller) as GameObject;
            float offset = EffectOffsetField != null ? (float)EffectOffsetField.GetValue(controller) : 0f;

            if (blastFx != null)
            {
                Vector3 fxPos = new Vector3(controller.transform.position.x, controller.transform.position.y + offset, controller.transform.position.z);
                GameObject spawnedFx = Instantiate(blastFx, fxPos, controller.transform.rotation);
                spawnedFx.transform.parent = controller.gameObject.transform;
            }

            float radius = EnhancedRange ? 18f : 10f;
            float baseDamage = controller.attackDamage > 0 ? controller.attackDamage : 150f;

            Vector3 position = controller.transform.position;
            Collider[] colliders = Physics.OverlapSphere(position, radius);
            HashSet<GameObject> processed = new HashSet<GameObject>();

            HealthBase userHealth = controller.GetComponent<HealthBase>();
            InvEffect.Identifier damageType = DarkDamage ? (InvEffect.Identifier)6 : InvEffect.Identifier.NormalDamage;

            foreach (Collider col in colliders)
            {
                if (col == null) continue;

                HealthBase health = col.GetComponent<HealthBase>() ?? col.GetComponentInParent<HealthBase>();
                PlayerHealth playerHealth = col.GetComponent<PlayerHealth>() ?? col.GetComponentInParent<PlayerHealth>();

                if (playerHealth != null) continue;

                if (health == null || health.IsDead()) continue;

                GameObject rootGo = health.gameObject;

                if (rootGo.CompareTag("Player") || rootGo.CompareTag("Pet")) continue;
                if (rootGo == controller.gameObject || rootGo == controller.player) continue;

                if (PetList.IsPet(rootGo) || IsMati(rootGo.GetComponent<BehaviourController>())) continue;

                Health h = health as Health;
                if (h != null && IsNonAngryNPC(h)) continue;

                if (processed.Contains(rootGo)) continue;
                processed.Add(rootGo);

                float dist = Vector3.Distance(position, col.ClosestPoint(position));
                float falloff = Mathf.Clamp01(1f - (dist / radius));
                float finalDamage = -Mathf.Max(25f, baseDamage * falloff);

                health.Attacked(finalDamage, userHealth, null, damageType, false);
            }
        }

        public static bool IsNonAngryNPC(Health h)
        {
            if (h == null || h.angry) return false;
            return h.CompareTag("NPC");
        }

        private GameObject GetMatiTarget(BehaviourController controller)
        {
            if (controller == null) return null;

            if (controller.target != null && IsHostileTarget(controller.target))
            {
                return controller.target;
            }

            Vector3 pos = controller.transform.position;
            float maxDist = EnhancedRange ? 60f : 30f;
            float closestSqr = maxDist * maxDist;
            GameObject closest = null;

#pragma warning disable CS0618
            GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
#pragma warning restore CS0618

            foreach (GameObject enemy in enemies)
            {
                if (enemy == null || !IsHostileTarget(enemy)) continue;

                float sqr = (enemy.transform.position - pos).sqrMagnitude;
                if (sqr < closestSqr)
                {
                    closestSqr = sqr;
                    closest = enemy;
                }
            }

            if (closest != null)
            {
                controller.target = closest;
            }

            return closest;
        }

        private bool IsHostileTarget(GameObject go)
        {
            if (go == null) return false;

            if (go.CompareTag("Player") || go.CompareTag("Pet")) return false;

            if (IsMati(go.GetComponent<BehaviourController>())) return false;

            Health h = go.GetComponent<Health>();
            if (h == null || h.IsDead()) return false;

            if (IsNonAngryNPC(h))
            {
                return false;
            }

            return true;
        }

        public static bool IsMati(BehaviourController controller)
        {
            if (controller == null) return false;

            bool isPet = controller.mobType == BehaviourController.MobType.Pet
                         || controller.libraryType == BehaviourController.LibraryType.Pets
                         || controller.CompareTag("Pet")
                         || controller.GetComponent<PetList>() != null;

            if (!isPet) return false;

            bool nameMatches = controller.gameObject.name.IndexOf("Mati", StringComparison.OrdinalIgnoreCase) >= 0;
            bool treeMatches = !string.IsNullOrEmpty(controller.tree) && controller.tree.IndexOf("MATI", StringComparison.OrdinalIgnoreCase) >= 0;

            return nameMatches || treeMatches;
        }
    }

    [HarmonyPatch(typeof(BehaviourController), "MagicBlast")]
    public static class MatiVanillaMagicBlastPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(BehaviourController __instance, bool cantHitPlayer)
        {
            if (MatiFixPlugin.IsMati(__instance))
            {
                MatiFixPlugin.ExecuteFixedMagicBlast(__instance);
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(Health), "Attacked")]
    public static class MatiNPCDamageProtectionPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(Health __instance, float adj, HealthBase from)
        {
            if (__instance == null) return true;

            if (MatiFixPlugin.IsNonAngryNPC(__instance))
            {
                if (from != null && IsMatiObject(from.gameObject))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsMatiObject(GameObject go)
        {
            if (go == null) return false;

            BehaviourController c = go.GetComponent<BehaviourController>();
            if (c != null)
            {
                return MatiFixPlugin.IsMati(c);
            }

            return false;
        }
    }

    public static class MatiFixConfig
    {
        public static bool Open;

        private static Texture2D _dim;
        private static bool _styles;
        private static int _builtVer = -1;

        private static GUIStyle _title;
        private static GUIStyle _row;
        private static GUIStyle _back;
        private static GUIStyle _desc;

        public static void OpenFromMenu()
        {
            ModsPage.Close();
            MatiFixConfig.Open = true;
            Overlay.ConfigOpen = true;
        }

        public static void Close()
        {
            MatiFixConfig.Open = false;
            Overlay.ConfigOpen = false;
            ModsPage.Open = true;
        }

        private static void BuildStyles()
        {
            Theme.Build();
            if (_styles && _builtVer == Theme.Version)
            {
                return;
            }

            _title = new GUIStyle(Theme.LabelGold)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _row = new GUIStyle(Theme.Button)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            _back = new GUIStyle(Theme.Button)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            _desc = new GUIStyle(Theme.LabelGold)
            {
                fontSize = 13,
                alignment = TextAnchor.UpperCenter,
                normal = { textColor = Color.white },
                wordWrap = true
            };

            _styles = true;
            _builtVer = Theme.Version;
        }

        public static void Draw()
        {
            if (!Open) return;

            BuildStyles();
            GUI.depth = 10;
            GUI.color = Color.white;

            if (_dim == null)
            {
                _dim = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                _dim.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.72f));
                _dim.Apply();
            }

            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _dim);

            float width = Mathf.Min(570f, Screen.width * 0.82f);
            float height = Mathf.Min(480f, Screen.height * 0.85f);
            float x = (Screen.width - width) / 2f;
            float y = (Screen.height - height) / 2f;

            GUI.Box(new Rect(x, y, width, height), GUIContent.none, Theme.Window);
            GUI.Label(new Rect(x, y + 14f, width, 30f), "Mati Tweaks Settings", _title);

            float contentY = y + 44f;
            float contentWidth = width - 48f;
            float btnHeight = 30f;
            float btnSpacing = 34f;

            string hoveredDesc = "Hover over any option to see what it does.";
            Vector2 mousePos = Event.current.mousePosition;

            Rect r1 = new Rect(x + 24f, contentY, contentWidth, btnHeight);
            if (r1.Contains(mousePos))
            {
                hoveredDesc = "Overhauls Mati into a Teleport Assassin: Mati teleports directly onto enemies, attacks, then teleports back to safety.";
            }
            if (DrawToggleButton(r1, "Teleport Overhaul", MatiFixPlugin.UnmountedAIBoost))
            {
                MatiFixPlugin.UnmountedAIBoost = !MatiFixPlugin.UnmountedAIBoost;
                PlayerPrefs.SetInt("MatiFix_UnmountedAIBoost", MatiFixPlugin.UnmountedAIBoost ? 1 : 0);
                PlayerPrefs.Save();
            }

            contentY += btnSpacing;

            Rect r2 = new Rect(x + 24f, contentY, contentWidth, btnHeight);
            if (r2.Contains(mousePos))
            {
                hoveredDesc = "Allows Mati to detect and hunt enemies through blocks.";
            }
            if (DrawToggleButton(r2, "Target Through Walls", MatiFixPlugin.WallSensing))
            {
                MatiFixPlugin.WallSensing = !MatiFixPlugin.WallSensing;
                PlayerPrefs.SetInt("MatiFix_WallSensing", MatiFixPlugin.WallSensing ? 1 : 0);
                PlayerPrefs.Save();
            }

            contentY += btnSpacing;

            Rect r3 = new Rect(x + 24f, contentY, contentWidth, btnHeight);
            if (r3.Contains(mousePos))
            {
                hoveredDesc = "Increases Mati's attack radius and expands the teleport range greatly.";
            }
            if (DrawToggleButton(r3, "Increased Attack & Detection Range", MatiFixPlugin.EnhancedRange))
            {
                MatiFixPlugin.EnhancedRange = !MatiFixPlugin.EnhancedRange;
                PlayerPrefs.SetInt("MatiFix_EnhancedRange", MatiFixPlugin.EnhancedRange ? 1 : 0);
                PlayerPrefs.Save();
            }

            contentY += btnSpacing;

            Rect r4 = new Rect(x + 24f, contentY, contentWidth, btnHeight);
            if (r4.Contains(mousePos))
            {
                hoveredDesc = "When enabled, Mati's attack deals Dark damage.";
            }
            if (DrawToggleButton(r4, "Dark Magic Damage", MatiFixPlugin.DarkDamage))
            {
                MatiFixPlugin.DarkDamage = !MatiFixPlugin.DarkDamage;
                PlayerPrefs.SetInt("MatiFix_DarkDamage", MatiFixPlugin.DarkDamage ? 1 : 0);
                PlayerPrefs.Save();
            }

            float descY = y + 195f;
            float descHeight = 160f;
            GUI.Label(new Rect(x + 24f, descY, contentWidth, descHeight), hoveredDesc, _desc);

            Rect backRect = new Rect(x + 24f, y + height - 14f - 36f, width - 48f, 36f);
            if (GUI.Button(backRect, "Back", _back))
            {
                Close();
            }
        }

        private static bool DrawToggleButton(Rect rect, string label, bool state, bool enabled = true)
        {
            bool prevEnabled = GUI.enabled;
            GUI.enabled = enabled;

            Color prevBg = GUI.backgroundColor;
            if (enabled)
            {
                GUI.backgroundColor = state ? new Color(0.18f, 0.65f, 0.22f, 1f) : new Color(0.72f, 0.2f, 0.2f, 1f);
            }

            string symbol = state ? "● " : "○ ";
            bool clicked = GUI.Button(rect, symbol + label, _row);

            GUI.backgroundColor = prevBg;
            GUI.enabled = prevEnabled;

            return clicked;
        }
    }
}