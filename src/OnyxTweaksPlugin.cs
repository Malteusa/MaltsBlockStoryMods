using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using BlockStoryCore;
using ISRef = UnityEngine.InputSystem.InputActionReference;

namespace BlockStoryMod
{
    [BepInPlugin("com.malts.blockstory.onyxtweaks", "OnyxTweaks", "2.3.0")]
    [BepInDependency(Core.Guid)]
    public class OnyxFixPlugin : BaseUnityPlugin
    {
        private Harmony _harmony;

        public static bool Enabled = PlayerPrefs.GetInt("OnyxFix_Enabled", 1) != 0;
        public static bool MountedAbility = PlayerPrefs.GetInt("OnyxFix_MountedAbility", 1) != 0;
        public static bool HoldToAttack = PlayerPrefs.GetInt("OnyxFix_HoldToAttack", 1) != 0;
        public static bool UnmountedAIBoost = PlayerPrefs.GetInt("OnyxFix_UnmountedAIBoost", 1) != 0;
        public static bool EnhancedRange = PlayerPrefs.GetInt("OnyxFix_EnhancedRange", 1) != 0;
        public static bool CleanGhostProjectiles = PlayerPrefs.GetInt("OnyxFix_CleanGhostProjectiles", 0) != 0;
        public static bool FilterNeutralTargets = PlayerPrefs.GetInt("OnyxFix_FilterNeutralTargets", 1) != 0;
        public static bool AlternateProjectiles = PlayerPrefs.GetInt("OnyxFix_AlternateProjectiles", 1) != 0;

        private ISRef _key;

        private float _lastMountedAttackTime = 0f;
        public float MountedAttackCooldown = 1.8f;
        private float _aiScanTimer = 0f;

        private readonly Dictionary<int, float> _unmountedAttackTimers = new Dictionary<int, float>();

        private static readonly FieldInfo AnimField = typeof(BehaviourController).GetField("animation", BindingFlags.Public | BindingFlags.Instance);
        private static MethodInfo _crossFadeMethod;

        private void Awake()
        {
            _harmony = new Harmony("com.malts.blockstory.onyxtweaks");
            _harmony.PatchAll();

            _key = BSKeybinds.Register("OnyxFix", "Mounted Attack", "<Mouse>/rightButton");

            ModInfo modInfo = new ModInfo
            {
                Name = "Onyx Tweaks",
                Description = "Major configurable modifications to Onyx to make him worth the hassle.",
                GetEnabled = () => Enabled,
                SetEnabled = on => { Enabled = on; PlayerPrefs.SetInt("OnyxFix_Enabled", on ? 1 : 0); PlayerPrefs.Save(); },
                HasConfig = true,
            };

            Action configAction = OnyxFixConfig.OpenFromMenu;
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
            Core.Log?.LogInfo("[OnyxTweaks]: Loaded successfully.");
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }

        private void OnGUI()
        {
            OnyxFixConfig.Draw();
        }

        private void Update()
        {
            if (!Enabled) return;

            HandleMountedOnyx();

            _aiScanTimer += Time.deltaTime;
            if (_aiScanTimer >= 0.25f)
            {
                _aiScanTimer = 0f;
                FixUnmountedOnyxAI();
            }
        }

        private void HandleMountedOnyx()
        {
            if (!MountedAbility) return;

            BehaviourController mountedOnyx = GetMountedOnyx();
            if (mountedOnyx == null) return;

            mountedOnyx.target = null;

            bool isHeld = _key != null && _key.action != null && _key.action.IsPressed();
            bool inputTriggered = HoldToAttack ? (BSKeybinds.Pressed(_key) || isHeld) : BSKeybinds.Pressed(_key);

            if (inputTriggered && Time.time >= _lastMountedAttackTime + MountedAttackCooldown)
            {
                _lastMountedAttackTime = Time.time;

                TryPlayAnimation(mountedOnyx, "attack");

                List<GameObject> targets = GetHostileTargets(mountedOnyx, maxTargets: 5, maxDist: 40f, requireOnScreen: true);

                StartCoroutine(LaunchOnyxAbilities(mountedOnyx, targets, isMounted: true));
            }
        }

        private void FixUnmountedOnyxAI()
        {
#pragma warning disable CS0618
            BehaviourController[] controllers = UnityEngine.Object.FindObjectsOfType<BehaviourController>();
#pragma warning restore CS0618

            foreach (var controller in controllers)
            {
                if (!IsOnyx(controller)) continue;

                if (IsRidingThisOnyx(controller))
                {
                    controller.target = null;
                    continue;
                }

                if (!UnmountedAIBoost) continue;

                int id = controller.GetInstanceID();
                if (!_unmountedAttackTimers.ContainsKey(id))
                {
                    _unmountedAttackTimers[id] = Time.time;
                }

                if (controller.target != null && IsHostileTarget(controller.target, controller) && !Inventory.isPaused)
                {
                    float dist = Vector3.Distance(controller.transform.position, controller.target.transform.position);
                    float maxCastDist = EnhancedRange ? 35f : 15f;

                    if (dist <= maxCastDist && Time.time >= _unmountedAttackTimers[id] + 3.0f)
                    {
                        _unmountedAttackTimers[id] = Time.time;

                        List<GameObject> targets = GetHostileTargets(controller, maxTargets: 5, maxDist: maxCastDist, requireOnScreen: false);
                        StartCoroutine(LaunchOnyxAbilities(controller, targets, isMounted: false));
                    }
                }
            }

            if (_unmountedAttackTimers.Count > 32)
            {
                _unmountedAttackTimers.Clear();
            }
        }

        private IEnumerator LaunchOnyxAbilities(BehaviourController controller, List<GameObject> targets, bool isMounted)
        {
            if (controller == null) yield break;

            var fireballSettings = controller.fireball;
            if (fireballSettings == null || fireballSettings.fireballs == null || fireballSettings.fireballs.Length == 0)
            {
                yield break;
            }

            InvBaseItem fireballItem = InvDatabase.FindByName(fireballSettings.itemName ?? "Fireball");

            float damage = fireballSettings.demage > 0 ? fireballSettings.demage : 100f;
            float speed = fireballSettings.speed > 0 ? fireballSettings.speed : 12f;

            float fbSpeed = AlternateProjectiles ? 45f : speed;
            float rotSpd = AlternateProjectiles ? 400f : (fireballSettings.rotationSpeed > 0f ? fireballSettings.rotationSpeed : 12f);

            int targetCount = targets != null ? targets.Count : 0;
            int count = fireballSettings.fireballs.Length;

            for (int i = 0; i < count; i++)
            {
                var fbData = fireballSettings.fireballs[i];
                if (fbData.fireBall == null) continue;

                GameObject assignedTarget = targetCount > 0 ? targets[i % targetCount] : null;

                if (!IsHostileTarget(assignedTarget, controller))
                {
                    assignedTarget = null;
                }

                Transform targetTransform = assignedTarget != null ? assignedTarget.transform : null;
                Health targetHealth = assignedTarget != null ? assignedTarget.GetComponent<Health>() : null;

                Vector3 spawnPos = fbData.spawnPoint != null ? fbData.spawnPoint.position : controller.transform.position + Quaternion.Euler(0f, i * 90f, 0f) * Vector3.forward * 2f;
                Quaternion spawnRot = controller.transform.rotation;

                GameObject fb = Instantiate(fbData.fireBall, spawnPos, spawnRot);

                if (CleanGhostProjectiles)
                {
                    OnyxProjectileCleaner cleaner = fb.AddComponent<OnyxProjectileCleaner>();
                    cleaner.Init(targetTransform, targetHealth, maxLifetime: 3.5f);
                }

                Homing homing = fb.GetComponent<Homing>();
                if (homing != null)
                {
                    bool useHoming = targetTransform != null;

                    if (isMounted && useHoming)
                    {
                        homing.InitSettings(null, controller.gameObject, damage, fbSpeed, 0f, fireballSettings.cantHitPlayer, false, fireballItem, 3.5f, false);
                        StartCoroutine(EnableHomingDelayed(fb, homing, targetTransform, controller.gameObject, damage, fbSpeed, rotSpd, fireballItem, delay: 0.12f));
                    }
                    else
                    {
                        homing.InitSettings(useHoming ? targetTransform : null, controller.gameObject, damage, fbSpeed, useHoming ? rotSpd : 0f, fireballSettings.cantHitPlayer, false, fireballItem, 3.5f, useHoming);
                    }
                }
            }

            yield break;
        }

        private IEnumerator EnableHomingDelayed(GameObject projectile, Homing homing, Transform targetTransform, GameObject user, float damage, float speed, float rotSpeed, InvBaseItem item, float delay)
        {
            yield return new WaitForSeconds(delay);

            if (projectile == null || homing == null || targetTransform == null) yield break;

            Health h = targetTransform.GetComponent<Health>();
            if (h != null && h.IsDead()) yield break;

            homing.InitSettings(targetTransform, user, damage, speed, rotSpeed, true, false, item, 3.5f, true);
        }

        private BehaviourController GetMountedOnyx()
        {
            GameObject mountedGo = PlayerMounted.mounted;
            if (mountedGo != null)
            {
                BehaviourController c = mountedGo.GetComponent<BehaviourController>();
                if (c != null && IsOnyx(c) && IsRidingThisOnyx(c))
                {
                    return c;
                }
            }
            return null;
        }

        private bool IsRidingThisOnyx(BehaviourController controller)
        {
            if (controller == null) return false;
            Mount mount = controller.GetComponent<Mount>();
            if (mount != null && mount.mounted) return true;
            return PlayerMounted.mounted == controller.gameObject;
        }

        private List<GameObject> GetHostileTargets(BehaviourController controller, int maxTargets, float maxDist, bool requireOnScreen)
        {
            List<GameObject> list = new List<GameObject>();
            Camera cam = Camera.main;

            if (cam != null)
            {
                Ray ray = new Ray(cam.transform.position, cam.transform.forward);
                RaycastHit[] hits = Physics.RaycastAll(ray, 70f);
                Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                foreach (RaycastHit hit in hits)
                {
                    GameObject hitGo = hit.collider.gameObject;
                    if (IsHostileTarget(hitGo, controller))
                    {
                        list.Add(hitGo);
                        break;
                    }

                    Transform current = hitGo.transform.parent;
                    bool foundParent = false;
                    while (current != null)
                    {
                        if (IsHostileTarget(current.gameObject, controller))
                        {
                            list.Add(current.gameObject);
                            foundParent = true;
                            break;
                        }
                        current = current.parent;
                    }
                    if (foundParent) break;
                }
            }

            if (controller.target != null && IsHostileTarget(controller.target, controller))
            {
                if (!requireOnScreen || IsOnScreen(controller.target, cam))
                {
                    if (!list.Contains(controller.target))
                    {
                        list.Add(controller.target);
                    }
                }
            }

            Vector3 pos = controller.transform.position;
            float sqrMax = maxDist * maxDist;

#pragma warning disable CS0618
            GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
#pragma warning restore CS0618

            foreach (GameObject enemy in enemies)
            {
                if (enemy == null || list.Contains(enemy) || !IsHostileTarget(enemy, controller)) continue;

                if (requireOnScreen && !IsOnScreen(enemy, cam)) continue;

                float sqr = (enemy.transform.position - pos).sqrMagnitude;
                if (sqr <= sqrMax)
                {
                    list.Add(enemy);
                    if (list.Count >= maxTargets) break;
                }
            }

            return list;
        }

        private bool IsOnScreen(GameObject go, Camera cam)
        {
            if (go == null || cam == null) return false;
            Vector3 vp = cam.WorldToViewportPoint(go.transform.position);
            return vp.z > 0f && vp.x >= 0.05f && vp.x <= 0.95f && vp.y >= 0.05f && vp.y <= 0.95f;
        }

        private bool IsHostileTarget(GameObject go, BehaviourController controller = null)
        {
            if (go == null) return false;

            if (go.CompareTag("Player") || go.CompareTag("Pet")) return false;

            if (controller != null)
            {
                if (go == controller.gameObject || go == controller.player) return false;
            }

            if (IsOnyx(go.GetComponent<BehaviourController>())) return false;

            Health h = go.GetComponent<Health>();
            if (h == null || h.IsDead()) return false;

            if (FilterNeutralTargets && OnyxNPCDamageProtectionPatch.IsNonAngryNPC(h))
            {
                return false;
            }

            return true;
        }

        private void TryPlayAnimation(BehaviourController controller, string animName)
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

        public static bool IsOnyx(BehaviourController controller)
        {
            if (controller == null) return false;

            bool isPet = controller.mobType == BehaviourController.MobType.Pet
                         || controller.libraryType == BehaviourController.LibraryType.Pets
                         || controller.CompareTag("Pet")
                         || controller.GetComponent<PetList>() != null;

            if (!isPet) return false;

            bool nameMatches = controller.gameObject.name.IndexOf("Phoenix", StringComparison.OrdinalIgnoreCase) >= 0 || controller.gameObject.name.IndexOf("Onyx", StringComparison.OrdinalIgnoreCase) >= 0;
            bool treeMatches = !string.IsNullOrEmpty(controller.tree) && controller.tree.IndexOf("PHOENIX", StringComparison.OrdinalIgnoreCase) >= 0;

            return nameMatches || treeMatches;
        }
    }

    [HarmonyPatch(typeof(Homing), "InitSettings")]
    public static class OnyxVanillaFireballSpeedPatch
    {
        [HarmonyPrefix]
        public static void Prefix(Homing __instance, GameObject user, ref float speed, ref float rotationSpeed)
        {
            if (!OnyxFixPlugin.AlternateProjectiles) return;

            if (user != null && OnyxFixPlugin.IsOnyx(user.GetComponent<BehaviourController>()))
            {
                speed = 45f;
                rotationSpeed = 400f;
            }
        }
    }

    [HarmonyPatch(typeof(Health), "Attacked")]
    public static class OnyxNPCDamageProtectionPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(Health __instance, float adj, HealthBase from)
        {
            if (!OnyxFixPlugin.FilterNeutralTargets) return true;

            if (__instance == null) return true;

            if (IsNonAngryNPC(__instance))
            {
                if (from != null && IsOnyxObject(from.gameObject))
                {
                    return false;
                }
            }

            return true;
        }

        public static bool IsNonAngryNPC(Health h)
        {
            if (h == null || h.angry) return false;
            return h.CompareTag("NPC");
        }

        private static bool IsOnyxObject(GameObject go)
        {
            if (go == null) return false;

            BehaviourController c = go.GetComponent<BehaviourController>();
            if (c != null)
            {
                return OnyxFixPlugin.IsOnyx(c);
            }

            return false;
        }
    }

    public static class OnyxFixConfig
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
            OnyxFixConfig.Open = true;
            Overlay.ConfigOpen = true;
        }

        public static void Close()
        {
            OnyxFixConfig.Open = false;
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
            float height = Mathf.Min(610f, Screen.height * 0.95f);
            float x = (Screen.width - width) / 2f;
            float y = (Screen.height - height) / 2f;

            GUI.Box(new Rect(x, y, width, height), GUIContent.none, Theme.Window);
            GUI.Label(new Rect(x, y + 14f, width, 30f), "Onyx Tweaks Settings", _title);

            float contentY = y + 44f;
            float contentWidth = width - 48f;
            float btnHeight = 30f;
            float btnSpacing = 34f;

            string hoveredDesc = "Hover over any option to see what it does.";
            Vector2 mousePos = Event.current.mousePosition;

            Rect r1 = new Rect(x + 24f, contentY, contentWidth, btnHeight);
            if (r1.Contains(mousePos))
            {
                hoveredDesc = "Manually shoot Onyx's Fireballs when mounted on Right-Click (By Default). Keybind can be changed in Controls.";
            }
            if (DrawToggleButton(r1, "Mounted Special Attack", OnyxFixPlugin.MountedAbility))
            {
                OnyxFixPlugin.MountedAbility = !OnyxFixPlugin.MountedAbility;
                PlayerPrefs.SetInt("OnyxFix_MountedAbility", OnyxFixPlugin.MountedAbility ? 1 : 0);
                PlayerPrefs.Save();
            }

            contentY += btnSpacing;

            Rect r2 = new Rect(x + 24f, contentY, contentWidth, btnHeight);
            if (r2.Contains(mousePos))
            {
                hoveredDesc = "The Mounted Attack key can be held to shoot rapidly instead of clicking it lot of times.";
            }
            if (DrawToggleButton(r2, "Hold To Attack", OnyxFixPlugin.HoldToAttack, enabled: OnyxFixPlugin.MountedAbility))
            {
                OnyxFixPlugin.HoldToAttack = !OnyxFixPlugin.HoldToAttack;
                PlayerPrefs.SetInt("OnyxFix_HoldToAttack", OnyxFixPlugin.HoldToAttack ? 1 : 0);
                PlayerPrefs.Save();
            }

            contentY += btnSpacing;

            Rect r3 = new Rect(x + 24f, contentY, contentWidth, btnHeight);
            if (r3.Contains(mousePos))
            {
                hoveredDesc = "Tweaks Onyx's AI so he automatically shoots Fireballs every 3 seconds in combat when dismounted.";
            }
            if (DrawToggleButton(r3, "AI Tweaks", OnyxFixPlugin.UnmountedAIBoost))
            {
                OnyxFixPlugin.UnmountedAIBoost = !OnyxFixPlugin.UnmountedAIBoost;
                PlayerPrefs.SetInt("OnyxFix_UnmountedAIBoost", OnyxFixPlugin.UnmountedAIBoost ? 1 : 0);
                PlayerPrefs.Save();
            }

            contentY += btnSpacing;

            Rect r4 = new Rect(x + 24f, contentY, contentWidth, btnHeight);
            if (r4.Contains(mousePos))
            {
                hoveredDesc = "Increases Onyx's fireball detection range.";
            }
            if (DrawToggleButton(r4, "Increased Projectile Detection Range", OnyxFixPlugin.EnhancedRange))
            {
                OnyxFixPlugin.EnhancedRange = !OnyxFixPlugin.EnhancedRange;
                PlayerPrefs.SetInt("OnyxFix_EnhancedRange", OnyxFixPlugin.EnhancedRange ? 1 : 0);
                PlayerPrefs.Save();
            }

            contentY += btnSpacing;

            Rect r5 = new Rect(x + 24f, contentY, contentWidth, btnHeight);
            if (r5.Contains(mousePos))
            {
                hoveredDesc = "Automatically removes fireballs if the target dies or if the projectile gets stuck on terrain.";
            }
            if (DrawToggleButton(r5, "Projectile Cleanup", OnyxFixPlugin.CleanGhostProjectiles))
            {
                OnyxFixPlugin.CleanGhostProjectiles = !OnyxFixPlugin.CleanGhostProjectiles;
                PlayerPrefs.SetInt("OnyxFix_CleanGhostProjectiles", OnyxFixPlugin.CleanGhostProjectiles ? 1 : 0);
                PlayerPrefs.Save();
            }

            contentY += btnSpacing;

            Rect r6 = new Rect(x + 24f, contentY, contentWidth, btnHeight);
            if (r6.Contains(mousePos))
            {
                hoveredDesc = "Prevents Onyx's fireballs from damaging non-angry NPCs.";
            }
            if (DrawToggleButton(r6, "NPC Protection", OnyxFixPlugin.FilterNeutralTargets))
            {
                OnyxFixPlugin.FilterNeutralTargets = !OnyxFixPlugin.FilterNeutralTargets;
                PlayerPrefs.SetInt("OnyxFix_FilterNeutralTargets", OnyxFixPlugin.FilterNeutralTargets ? 1 : 0);
                PlayerPrefs.Save();
            }

            contentY += btnSpacing;

            Rect r7 = new Rect(x + 24f, contentY, contentWidth, btnHeight);
            if (r7.Contains(mousePos))
            {
                hoveredDesc = "Speeds up Onyx's fireballs greatly.";
            }
            if (DrawToggleButton(r7, "Faster Fireballs", OnyxFixPlugin.AlternateProjectiles))
            {
                OnyxFixPlugin.AlternateProjectiles = !OnyxFixPlugin.AlternateProjectiles;
                PlayerPrefs.SetInt("OnyxFix_AlternateProjectiles", OnyxFixPlugin.AlternateProjectiles ? 1 : 0);
                PlayerPrefs.Save();
            }

            float descY = y + 295f;
            float descHeight = 170f;
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

    public class OnyxProjectileCleaner : MonoBehaviour
    {
        public Transform TargetTransform;
        public Health TargetHealth;
        public float MaxLifetime = 3.0f;

        private float _spawnTime;
        private Vector3 _lastPos;
        private float _stuckTimer;

        public void Init(Transform targetTransform, Health targetHealth, float maxLifetime)
        {
            TargetTransform = targetTransform;
            TargetHealth = targetHealth;
            MaxLifetime = maxLifetime;
        }

        private void Start()
        {
            _spawnTime = Time.time;
            _lastPos = transform.position;
        }

        private void Update()
        {
            if (Time.time - _spawnTime >= MaxLifetime)
            {
                SelfDestruct();
                return;
            }

            if (TargetTransform != null)
            {
                if (!TargetTransform.gameObject.activeInHierarchy)
                {
                    SelfDestruct();
                    return;
                }

                if (TargetHealth != null && TargetHealth.IsDead())
                {
                    SelfDestruct();
                    return;
                }
            }

            float movedDistSqr = (transform.position - _lastPos).sqrMagnitude;
            _lastPos = transform.position;

            if (movedDistSqr < 0.0025f)
            {
                _stuckTimer += Time.deltaTime;
                if (_stuckTimer >= 0.5f)
                {
                    SelfDestruct();
                    return;
                }
            }
            else
            {
                _stuckTimer = 0f;
            }
        }

        private void SelfDestruct()
        {
            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                col.enabled = false;
            }
            Destroy(gameObject);
        }
    }
}