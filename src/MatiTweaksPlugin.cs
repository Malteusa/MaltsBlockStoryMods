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
    [BepInPlugin("com.malts.blockstory.matitweaks", "MatiTweaks", "4.1.0")]
    [BepInDependency(Core.Guid)]
    public class MatiFixPlugin : BaseUnityPlugin
    {
        private Harmony _harmony;

        public static bool Enabled = PlayerPrefs.GetInt("MatiFix_Enabled", 1) != 0;
        public static bool UnmountedAIBoost = PlayerPrefs.GetInt("MatiFix_UnmountedAIBoost", 1) != 0;
        public static bool WallSensing = PlayerPrefs.GetInt("MatiFix_WallSensing", 1) != 0;
        public static bool EnhancedRange = PlayerPrefs.GetInt("MatiFix_EnhancedRange", 1) != 0;
        public static bool PrioritizeHighHealth = PlayerPrefs.GetInt("MatiFix_PrioritizeHighHealth", 1) != 0;
        public static bool LingeringDarkDamage = PlayerPrefs.GetInt("MatiFix_LingeringDarkDamage", 1) != 0;
        public static bool TeleportDelay = PlayerPrefs.GetInt("MatiFix_TeleportDelay", 1) != 0;

        public static float TeleportAttackCooldown = PlayerPrefs.GetFloat("MatiFix_TeleportAttackCooldown", 3.0f);
        public static float DoTDamage = PlayerPrefs.GetFloat("MatiFix_DoTDamage", 10.0f);
        public static float DoTDuration = PlayerPrefs.GetFloat("MatiFix_DoTDuration", 30.0f);

        private float _aiScanTimer = 0f;
        private readonly Dictionary<int, float> _attackTimers = new Dictionary<int, float>();
        private readonly HashSet<int> _activeTeleports = new HashSet<int>();

        private static readonly FieldInfo BlastEffectField = typeof(BehaviourController).GetField("blastEffect", BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo EffectOffsetField = typeof(BehaviourController).GetField("effectOffset", BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo AnimField = typeof(BehaviourController).GetField("animation", BindingFlags.Public | BindingFlags.Instance);
        private static MethodInfo _crossFadeMethod;

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
            if (!Enabled || IsGamePaused()) return;

            _aiScanTimer += Time.deltaTime;
            if (_aiScanTimer >= 0.25f)
            {
                _aiScanTimer = 0f;
                FixMatiAI();
            }
        }

        private void FixMatiAI()
        {
            if (IsGamePaused()) return;

#pragma warning disable CS0618
            BehaviourController[] controllers = UnityEngine.Object.FindObjectsOfType<BehaviourController>();
#pragma warning restore CS0618

            foreach (var controller in controllers)
            {
                if (!IsMati(controller)) continue;

                if (!UnmountedAIBoost) continue;

                int id = controller.GetInstanceID();

                if (_activeTeleports.Contains(id)) continue;

                if (!_attackTimers.ContainsKey(id))
                {
                    _attackTimers[id] = Time.time;
                }

                GameObject target = GetMatiTarget(controller);

                if (target != null && !IsGamePaused())
                {
                    float dist = Vector3.Distance(controller.transform.position, target.transform.position);
                    float maxCastDist = EnhancedRange ? 60f : 30f;

                    if (dist <= maxCastDist && Time.time >= _attackTimers[id] + TeleportAttackCooldown)
                    {
                        StartCoroutine(ExecuteTeleportCombo(controller, target, id));
                    }
                }
            }

            if (_attackTimers.Count > 32)
            {
                _attackTimers.Clear();
            }
        }

        private IEnumerator ExecuteTeleportCombo(BehaviourController controller, GameObject target, int id)
        {
            if (controller == null || target == null || IsGamePaused()) yield break;

            _activeTeleports.Add(id);

            try
            {
                controller.SpawnTeleportEffect();

                Vector3 targetPos = target.transform.position + Vector3.up * 1.2f + UnityEngine.Random.insideUnitSphere * 1.5f;
                controller.transform.position = targetPos;
                controller.SpawnTeleportEffect();

                TryPlayAnimation(controller, "attack");

                yield return new WaitForSeconds(0.15f);

                if (IsGamePaused()) yield break;

                ExecuteFixedMagicBlast(controller);

                float delay = TeleportDelay ? 1.0f : 0.35f;
                yield return new WaitForSeconds(delay);

                if (IsGamePaused()) yield break;

                if (controller != null && controller.player != null)
                {
                    controller.SpawnTeleportEffect();
                    Vector3 safePos = controller.player.transform.position + UnityEngine.Random.onUnitSphere * 4f;
                    safePos.y = controller.player.transform.position.y + 1.5f;
                    controller.transform.position = safePos;
                    controller.SpawnTeleportEffect();
                }
            }
            finally
            {
                _activeTeleports.Remove(id);
                _attackTimers[id] = Time.time;
            }
        }

        public static void ExecuteFixedMagicBlast(BehaviourController controller)
        {
            if (controller == null || IsGamePaused()) return;

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
            GameObject darkFxPrefab = GetDarkDamageParticlePrefab();

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

                health.Attacked(finalDamage, userHealth, null, InvEffect.Identifier.NormalDamage, false);

                if (LingeringDarkDamage)
                {
                    DarkDoTTracker tracker = rootGo.GetComponent<DarkDoTTracker>();
                    if (tracker == null)
                    {
                        tracker = rootGo.AddComponent<DarkDoTTracker>();
                    }
                    tracker.Init(userHealth, darkFxPrefab, DoTDamage, DoTDuration);
                }
            }
        }

        public static bool IsGamePaused()
        {
            if (Inventory.isPaused) return true;
            if (Inventory.Instance != null && Inventory.Instance.isWindowOpen()) return true;
            return false;
        }

        private static GameObject GetDarkDamageParticlePrefab()
        {
            GameObject managerGo = GameObject.FindGameObjectWithTag("MOBManager");
            if (managerGo != null)
            {
                MOBManagement manager = managerGo.GetComponent<MOBManagement>();
                if (manager != null)
                {
                    return manager.darkDamagePrefab;
                }
            }
            return null;
        }

        public static bool IsNonAngryNPC(Health h)
        {
            if (h == null || h.angry) return false;
            return h.CompareTag("NPC");
        }

        private GameObject GetMatiTarget(BehaviourController controller)
        {
            if (controller == null) return null;

            Vector3 pos = controller.transform.position;
            float maxDist = EnhancedRange ? 60f : 30f;
            float sqrMax = maxDist * maxDist;

#pragma warning disable CS0618
            GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
#pragma warning restore CS0618

            GameObject bestTarget = null;
            float highestMaxHealth = -1f;
            float closestSqrForHighest = float.MaxValue;

            foreach (GameObject enemy in enemies)
            {
                if (enemy == null || !IsHostileTarget(enemy)) continue;

                float sqr = (enemy.transform.position - pos).sqrMagnitude;
                if (sqr > sqrMax) continue;

                Health h = enemy.GetComponent<Health>();
                if (h == null || h.IsDead()) continue;

                if (PrioritizeHighHealth)
                {
                    if (h.maxHealth > highestMaxHealth)
                    {
                        highestMaxHealth = h.maxHealth;
                        closestSqrForHighest = sqr;
                        bestTarget = enemy;
                    }
                    else if (Mathf.Approximately(h.maxHealth, highestMaxHealth))
                    {
                        if (sqr < closestSqrForHighest)
                        {
                            closestSqrForHighest = sqr;
                            bestTarget = enemy;
                        }
                    }
                }
                else
                {
                    if (sqr < closestSqrForHighest)
                    {
                        closestSqrForHighest = sqr;
                        bestTarget = enemy;
                    }
                }
            }

            if (bestTarget != null)
            {
                controller.target = bestTarget;
            }

            return bestTarget;
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

        private static void TryPlayAnimation(BehaviourController controller, string animName)
        {
            try
            {
                object animObj = AnimField?.GetValue(controller);
                if (animObj != null)
                {
                    if (_crossFadeMethod == null)
                    {
                        _crossFadeMethod = animObj.GetType().GetMethod("CrossFade", new Type[] { typeof(string), typeof(float) });
                    }
                    _crossFadeMethod?.Invoke(animObj, new object[] { animName, 0.2f });
                }
            }
            catch { }
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

    public class DarkDoTTracker : MonoBehaviour
    {
        private float _durationRemaining = 30f;
        private float _tickTimer = 0f;
        private HealthBase _health;
        private HealthBase _userHealth;
        private GameObject _activeVisual;
        private GameObject _fxPrefab;
        private float _damagePerSecond = 10f;

        public void Init(HealthBase userHealth, GameObject darkFxPrefab, float damagePerSecond, float duration)
        {
            _userHealth = userHealth;
            _health = GetComponent<HealthBase>();
            _durationRemaining = duration;
            _damagePerSecond = damagePerSecond;
            _fxPrefab = darkFxPrefab;

            EnsureVisual();
        }

        private void EnsureVisual()
        {
            if (_activeVisual == null && _fxPrefab != null && transform != null)
            {
                Vector3 fxPos = transform.position + Vector3.up * 1.5f;
                _activeVisual = Instantiate(_fxPrefab, fxPos, Quaternion.identity);
                _activeVisual.name = "MatiDarkDoTVisual";
                _activeVisual.transform.parent = transform;
            }
        }

        private void Update()
        {
            if (MatiFixPlugin.IsGamePaused()) return;

            if (_health == null || _health.IsDead() || _durationRemaining <= 0f)
            {
                CleanupAndDestroy();
                return;
            }

            _durationRemaining -= Time.deltaTime;
            _tickTimer += Time.deltaTime;

            EnsureVisual();

            if (_tickTimer >= 1.0f)
            {
                _tickTimer = 0f;
                _health.Attacked(-_damagePerSecond, _userHealth, null, (InvEffect.Identifier)6, false);
            }
        }

        private void CleanupAndDestroy()
        {
            if (_activeVisual != null)
            {
                Destroy(_activeVisual);
            }
            Destroy(this);
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
        private static GUIStyle _rowText;
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
            _rowText = new GUIStyle(Theme.LabelGold)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
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

            float width = Mathf.Min(580f, Screen.width * 0.85f);
            float height = Mathf.Min(670f, Screen.height * 0.96f);
            float x = (Screen.width - width) / 2f;
            float y = (Screen.height - height) / 2f;

            GUI.Box(new Rect(x, y, width, height), GUIContent.none, Theme.Window);
            GUI.Label(new Rect(x, y + 14f, width, 30f), "Mati Tweaks Settings", _title);

            float contentY = y + 44f;
            float contentWidth = width - 48f;
            float btnHeight = 28f;
            float btnSpacing = 31f;

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
                hoveredDesc = "Adds a configurable delay after teleporting onto an enemy before teleporting back.";
            }
            if (DrawToggleButton(r2, "Teleport Attack Delay", MatiFixPlugin.TeleportDelay, enabled: MatiFixPlugin.UnmountedAIBoost))
            {
                MatiFixPlugin.TeleportDelay = !MatiFixPlugin.TeleportDelay;
                PlayerPrefs.SetInt("MatiFix_TeleportDelay", MatiFixPlugin.TeleportDelay ? 1 : 0);
                PlayerPrefs.Save();
            }

            contentY += btnSpacing;

            bool cooldownEnabled = MatiFixPlugin.UnmountedAIBoost && MatiFixPlugin.TeleportDelay;
            Rect r3 = new Rect(x + 24f, contentY, contentWidth, btnHeight);
            if (r3.Contains(mousePos))
            {
                hoveredDesc = "Adjusts the delay in seconds between Mati's teleport attacks.";
            }
            MatiFixPlugin.TeleportAttackCooldown = DrawSlider(r3, "Teleport Cooldown", MatiFixPlugin.TeleportAttackCooldown, 1f, 10f, "s", step: 0.5f, enabled: cooldownEnabled);
            PlayerPrefs.SetFloat("MatiFix_TeleportAttackCooldown", MatiFixPlugin.TeleportAttackCooldown);

            contentY += btnSpacing;

            Rect r4 = new Rect(x + 24f, contentY, contentWidth, btnHeight);
            if (r4.Contains(mousePos))
            {
                hoveredDesc = "Allows Mati to detect and hunt enemies through blocks.";
            }
            if (DrawToggleButton(r4, "Target Through Walls", MatiFixPlugin.WallSensing))
            {
                MatiFixPlugin.WallSensing = !MatiFixPlugin.WallSensing;
                PlayerPrefs.SetInt("MatiFix_WallSensing", MatiFixPlugin.WallSensing ? 1 : 0);
                PlayerPrefs.Save();
            }

            contentY += btnSpacing;

            Rect r5 = new Rect(x + 24f, contentY, contentWidth, btnHeight);
            if (r5.Contains(mousePos))
            {
                hoveredDesc = "Increases Mati's attack radius and expands the teleport range greatly. Disable this if you don't want random animals dying.";
            }
            if (DrawToggleButton(r5, "Increased Attack & Detection Range", MatiFixPlugin.EnhancedRange))
            {
                MatiFixPlugin.EnhancedRange = !MatiFixPlugin.EnhancedRange;
                PlayerPrefs.SetInt("MatiFix_EnhancedRange", MatiFixPlugin.EnhancedRange ? 1 : 0);
                PlayerPrefs.Save();
            }

            contentY += btnSpacing;

            Rect r6 = new Rect(x + 24f, contentY, contentWidth, btnHeight);
            if (r6.Contains(mousePos))
            {
                hoveredDesc = "Makes Mati prioritize targeting bosses and high health enemies.";
            }
            if (DrawToggleButton(r6, "Boss & High Health Priority", MatiFixPlugin.PrioritizeHighHealth))
            {
                MatiFixPlugin.PrioritizeHighHealth = !MatiFixPlugin.PrioritizeHighHealth;
                PlayerPrefs.SetInt("MatiFix_PrioritizeHighHealth", MatiFixPlugin.PrioritizeHighHealth ? 1 : 0);
                PlayerPrefs.Save();
            }

            contentY += btnSpacing;

            Rect r7 = new Rect(x + 24f, contentY, contentWidth, btnHeight);
            if (r7.Contains(mousePos))
            {
                hoveredDesc = "Adds overtime dark damage on enemies hit by Mati's attack.";
            }
            if (DrawToggleButton(r7, "Dark Damage Overtime", MatiFixPlugin.LingeringDarkDamage))
            {
                MatiFixPlugin.LingeringDarkDamage = !MatiFixPlugin.LingeringDarkDamage;
                PlayerPrefs.SetInt("MatiFix_LingeringDarkDamage", MatiFixPlugin.LingeringDarkDamage ? 1 : 0);
                PlayerPrefs.Save();
            }

            contentY += btnSpacing;

            Rect r8 = new Rect(x + 24f, contentY, contentWidth, btnHeight);
            if (r8.Contains(mousePos))
            {
                hoveredDesc = "Adjusts the damage dealt per second by the dark damage.";
            }
            MatiFixPlugin.DoTDamage = DrawSlider(r8, "Damage / sec", MatiFixPlugin.DoTDamage, 1f, 100f, " Damage", step: 1.0f, enabled: MatiFixPlugin.LingeringDarkDamage);
            PlayerPrefs.SetFloat("MatiFix_DoTDamage", MatiFixPlugin.DoTDamage);

            contentY += btnSpacing;

            Rect r9 = new Rect(x + 24f, contentY, contentWidth, btnHeight);
            if (r9.Contains(mousePos))
            {
                hoveredDesc = "Adjusts how many seconds the dark damage lasts.";
            }
            MatiFixPlugin.DoTDuration = DrawSlider(r9, "Duration", MatiFixPlugin.DoTDuration, 5f, 60f, "s", step: 0.5f, enabled: MatiFixPlugin.LingeringDarkDamage);
            PlayerPrefs.SetFloat("MatiFix_DoTDuration", MatiFixPlugin.DoTDuration);

            PlayerPrefs.Save();

            float descY = y + 335f;
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

        private static float DrawSlider(Rect rect, string label, float value, float min, float max, string unit, float step = 0.5f, bool enabled = true)
        {
            bool prevEnabled = GUI.enabled;
            GUI.enabled = enabled;

            GUI.Label(new Rect(rect.x, rect.y, rect.width * 0.58f, rect.height), label + ": " + value.ToString(step >= 1.0f ? "F0" : "F1") + unit, _rowText);
            float val = GUI.HorizontalSlider(new Rect(rect.x + rect.width * 0.60f, rect.y + 6f, rect.width * 0.40f, rect.height - 12f), value, min, max);

            float rounded = Mathf.Round(val / step) * step;

            GUI.enabled = prevEnabled;
            return rounded;
        }
    }
}
