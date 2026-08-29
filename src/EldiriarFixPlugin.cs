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
    [BepInPlugin("com.malts.blockstory.eldiriarfix", "EldriarTweaks", "9.6.0")]
    [BepInDependency(Core.Guid)]
    public class EldiriarFixPlugin : BaseUnityPlugin
    {
        private Harmony _harmony;

        public static bool Enabled = PlayerPrefs.GetInt("EldiriarFix_Enabled", 1) != 0;
        public static bool MountedAbility = PlayerPrefs.GetInt("EldiriarFix_MountedAbility", 1) != 0;
        public static bool HoldToAttack = PlayerPrefs.GetInt("EldiriarFix_HoldToAttack", 0) != 0;
        public static bool UnmountedAIBoost = PlayerPrefs.GetInt("EldiriarFix_UnmountedAIBoost", 1) != 0;
        public static bool HomingProjectiles = PlayerPrefs.GetInt("EldiriarFix_HomingProjectiles", 1) != 0;
        public static bool EnhancedRange = PlayerPrefs.GetInt("EldiriarFix_EnhancedRange", 1) != 0;
        public static bool CleanGhostProjectiles = PlayerPrefs.GetInt("EldiriarFix_CleanGhostProjectiles", 1) != 0;
        public static bool FilterNeutralTargets = PlayerPrefs.GetInt("EldiriarFix_FilterNeutralTargets", 1) != 0;
        public static bool AlternateProjectiles = PlayerPrefs.GetInt("EldiriarFix_AlternateProjectiles", 1) != 0;

        private ISRef _key;

        private float _lastMountedAttackTime = 0f;
        public float MountedAttackCooldown = 2.0f;
        private float _aiScanTimer = 0f;

        private readonly Dictionary<int, float> _unmountedAttackTimers = new Dictionary<int, float>();

        private static readonly FieldInfo AnimField = typeof(BehaviourController).GetField("animation", BindingFlags.Public | BindingFlags.Instance);
        private static MethodInfo _crossFadeMethod;

        private void Awake()
        {
            _harmony = new Harmony("com.malts.blockstory.eldiriarfix");
            _harmony.PatchAll();

            _key = BSKeybinds.Register("EldiriarFix", "Mounted Attack", "<Mouse>/rightButton");

            ModInfo modInfo = new ModInfo
            {
                Name = "Eldriar Tweaks",
                Description = "Major configurable modifications to Eldriar to make him suck less.",
                GetEnabled = () => Enabled,
                SetEnabled = on => { Enabled = on; PlayerPrefs.SetInt("EldiriarFix_Enabled", on ? 1 : 0); PlayerPrefs.Save(); },
                HasConfig = true,
            };

            Action configAction = EldiriarFixConfig.OpenFromMenu;
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
            Core.Log?.LogInfo("[EldriarTweaks]: Loaded successfully.");
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }

        private void OnGUI()
        {
            EldiriarFixConfig.Draw();
        }

        private void Update()
        {
            if (!Enabled) return;

            HandleMountedEldriar();

            _aiScanTimer += Time.deltaTime;
            if (_aiScanTimer >= 0.25f)
            {
                _aiScanTimer = 0f;
                FixUnmountedEldriarAI();
            }
        }

        private void HandleMountedEldriar()
        {
            if (!MountedAbility) return;

            BehaviourController mountedEldriar = GetMountedEldriar();
            if (mountedEldriar == null) return;

            mountedEldriar.target = null;

            bool isHeld = _key != null && _key.action != null && _key.action.IsPressed();
            bool inputTriggered = HoldToAttack ? (BSKeybinds.Pressed(_key) || isHeld) : BSKeybinds.Pressed(_key);

            if (inputTriggered && Time.time >= _lastMountedAttackTime + MountedAttackCooldown)
            {
                _lastMountedAttackTime = Time.time;

                TryPlayAnimation(mountedEldriar, "attack");

                List<GameObject> targets = GetHostileTargets(mountedEldriar, maxTargets: 5, maxDist: 40f, requireOnScreen: true);

                StartCoroutine(LaunchHomingAbilities(mountedEldriar, targets, isMounted: true));
            }
        }

        private void FixUnmountedEldriarAI()
        {
#pragma warning disable CS0618
            BehaviourController[] controllers = UnityEngine.Object.FindObjectsOfType<BehaviourController>();
#pragma warning restore CS0618

            foreach (var controller in controllers)
            {
                if (!IsEldriar(controller)) continue;

                AICharacterMotor motor = controller.GetComponent<AICharacterMotor>();
                bool isFlying = motor != null && motor.flying != null && motor.flying.flying;

                if (isFlying)
                {
                    controller.seekUpDown = true;
                }
                else
                {
                    controller.seekUpDown = false;

                    Vector3 euler = controller.transform.eulerAngles;
                    if (Mathf.Abs(Mathf.DeltaAngle(0f, euler.x)) > 0.1f || Mathf.Abs(Mathf.DeltaAngle(0f, euler.z)) > 0.1f)
                    {
                        controller.transform.eulerAngles = new Vector3(0f, euler.y, 0f);
                    }

                    if (motor != null)
                    {
                        Vector3 lookFwd = motor.lookat * Vector3.forward;
                        lookFwd.y = 0f;
                        if (lookFwd.sqrMagnitude > 0.001f)
                        {
                            motor.lookat = Quaternion.LookRotation(lookFwd, Vector3.up);
                        }
                    }
                }

                if (IsRidingThisEldriar(controller))
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
                        StartCoroutine(LaunchHomingAbilities(controller, targets, isMounted: false));
                    }
                }
            }

            if (_unmountedAttackTimers.Count > 32)
            {
                _unmountedAttackTimers.Clear();
            }
        }

        private IEnumerator LaunchHomingAbilities(BehaviourController controller, List<GameObject> targets, bool isMounted)
        {
            if (controller == null) yield break;

            PetExperience exp = controller.GetComponent<PetExperience>();
            int level = exp != null ? exp.curLvl : 50;

            var fireballSettings = controller.fireball;
            if (fireballSettings == null || fireballSettings.fireballs == null || fireballSettings.fireballs.Length == 0)
            {
                yield break;
            }

            InvBaseItem fireballItem = InvDatabase.FindByName(fireballSettings.itemName ?? "Fireball");
            InvBaseItem meteorItem = InvDatabase.FindByName("Dragon Meteor") ?? fireballItem;

            float damage = fireballSettings.demage > 0 ? fireballSettings.demage : 120f;
            float speed = fireballSettings.speed > 0 ? fireballSettings.speed : 14f;

            int targetCount = targets != null ? targets.Count : 0;

            var fireballData = fireballSettings.fireballs[0];
            int fireballCount = 5;
            float fbSpeed = AlternateProjectiles ? speed * 2.2f : speed * 1.2f;

            for (int i = 0; i < fireballCount; i++)
            {
                if (controller == null) yield break;

                GameObject assignedTarget = targetCount > 0 ? targets[i % targetCount] : null;

                if (!IsHostileTarget(assignedTarget, controller))
                {
                    assignedTarget = null;
                }

                Transform targetTransform = assignedTarget != null ? assignedTarget.transform : null;
                Health targetHealth = assignedTarget != null ? assignedTarget.GetComponent<Health>() : null;

                Vector3 spawnPos = fireballData.spawnPoint != null ? fireballData.spawnPoint.position : controller.transform.position + controller.transform.forward * 2f;
                Quaternion spawnRot = controller.transform.rotation;

                GameObject fb = Instantiate(fireballData.fireBall, spawnPos, spawnRot);
                fb.transform.Rotate(0f, UnityEngine.Random.Range(-8f, 8f), 0f);

                if (CleanGhostProjectiles)
                {
                    ProjectileCleaner cleaner = fb.AddComponent<ProjectileCleaner>();
                    cleaner.Init(targetTransform, targetHealth, maxLifetime: 3.0f);
                }

                Homing homing = fb.GetComponent<Homing>();
                if (homing != null)
                {
                    bool useHoming = HomingProjectiles && (targetTransform != null);

                    if (isMounted && useHoming)
                    {
                        homing.InitSettings(null, controller.gameObject, damage, fbSpeed, 0f, true, false, fireballItem, 3.0f, false);
                        StartCoroutine(EnableHomingDelayed(fb, homing, targetTransform, controller.gameObject, damage, fbSpeed, rotSpeed: 180f, fireballItem, delay: 0.4f));
                    }
                    else
                    {
                        homing.InitSettings(useHoming ? targetTransform : null, controller.gameObject, damage, fbSpeed, useHoming ? 180f : 0f, true, false, fireballItem, 3.0f, useHoming);
                    }
                }

                yield return new WaitForSeconds(0.08f);
            }

            if (level >= 39)
            {
                int meteorCount = 1;
                if (level >= 49) meteorCount = 3;
                else if (level >= 44) meteorCount = 2;
                else meteorCount = 1;

                var meteorData = fireballSettings.fireballs.Length > 1 ? fireballSettings.fireballs[1] : fireballData;

                for (int m = 0; m < meteorCount; m++)
                {
                    if (controller == null) yield break;

                    GameObject assignedTarget = targetCount > 0 ? targets[m % targetCount] : null;

                    if (!IsHostileTarget(assignedTarget, controller))
                    {
                        assignedTarget = null;
                    }

                    Transform targetTransform = assignedTarget != null ? assignedTarget.transform : null;
                    Health targetHealth = assignedTarget != null ? assignedTarget.GetComponent<Health>() : null;

                    Vector3 meteorSpawnPos;
                    if (targetTransform != null)
                    {
                        Vector3 offset = new Vector3(UnityEngine.Random.Range(-2.5f, 2.5f), 28f + (m * 5f), UnityEngine.Random.Range(-2.5f, 2.5f));
                        meteorSpawnPos = targetTransform.position + offset;
                    }
                    else
                    {
                        Vector3 offset = controller.transform.forward * (12f + m * 5f) + new Vector3(UnityEngine.Random.Range(-2f, 2f), 28f, UnityEngine.Random.Range(-2f, 2f));
                        meteorSpawnPos = controller.transform.position + offset;
                    }

                    Vector3 dirToTarget = targetTransform != null ? (targetTransform.position - meteorSpawnPos).normalized : Vector3.down;
                    Quaternion meteorRot = Quaternion.LookRotation(dirToTarget);

                    if (controller.childEffect != null)
                    {
                        Instantiate(controller.childEffect, meteorSpawnPos, meteorRot);
                    }

                    yield return new WaitForSeconds(0.2f);

                    GameObject meteor = Instantiate(meteorData.fireBall, meteorSpawnPos, meteorRot);
                    meteor.transform.localScale = new Vector3(3.5f, 3.5f, 3.5f);

                    float meteorLifetime = AlternateProjectiles ? 2.5f : 4.5f;

                    if (CleanGhostProjectiles)
                    {
                        ProjectileCleaner cleaner = meteor.AddComponent<ProjectileCleaner>();
                        cleaner.Init(targetTransform, targetHealth, maxLifetime: meteorLifetime);
                    }

                    Homing homing = meteor.GetComponent<Homing>();
                    if (homing != null)
                    {
                        if (AlternateProjectiles)
                        {
                            bool useHoming = HomingProjectiles && (targetTransform != null);
                            homing.InitSettings(
                                useHoming ? targetTransform : null,
                                controller.gameObject,
                                damage * 1.8f,
                                55f,
                                useHoming ? 350f : 0f,
                                true,
                                false,
                                meteorItem,
                                meteorLifetime,
                                useHoming
                            );
                        }
                        else
                        {
                            bool useHoming = HomingProjectiles && (targetTransform != null);
                            homing.InitSettings(
                                useHoming ? targetTransform : null,
                                controller.gameObject,
                                damage * 1.8f,
                                18f,
                                useHoming ? 4f : 0f,
                                true,
                                false,
                                meteorItem,
                                meteorLifetime,
                                useHoming
                            );
                        }
                    }
                }
            }
        }

        private IEnumerator EnableHomingDelayed(GameObject projectile, Homing homing, Transform targetTransform, GameObject user, float damage, float speed, float rotSpeed, InvBaseItem item, float delay)
        {
            yield return new WaitForSeconds(delay);

            if (projectile == null || homing == null || targetTransform == null) yield break;

            Health h = targetTransform.GetComponent<Health>();
            if (h != null && h.IsDead()) yield break;

            homing.InitSettings(targetTransform, user, damage, speed, rotSpeed, true, false, item, 3.0f, true);
        }

        private BehaviourController GetMountedEldriar()
        {
            GameObject mountedGo = PlayerMounted.mounted;
            if (mountedGo != null)
            {
                BehaviourController c = mountedGo.GetComponent<BehaviourController>();
                if (c != null && IsEldriar(c) && IsRidingThisEldriar(c))
                {
                    return c;
                }
            }
            return null;
        }

        private bool IsRidingThisEldriar(BehaviourController controller)
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
                    if (IsHostileTarget(hitGo, controller) && !controller.TroughtWall(hitGo))
                    {
                        list.Add(hitGo);
                        break;
                    }

                    Transform current = hitGo.transform.parent;
                    bool foundParent = false;
                    while (current != null)
                    {
                        if (IsHostileTarget(current.gameObject, controller) && !controller.TroughtWall(current.gameObject))
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

            if (controller.target != null && IsHostileTarget(controller.target, controller) && !controller.TroughtWall(controller.target))
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
                if (enemy == null || list.Contains(enemy) || !IsHostileTarget(enemy, controller) || controller.TroughtWall(enemy)) continue;

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

            if (IsEldriar(go.GetComponent<BehaviourController>())) return false;

            Health h = go.GetComponent<Health>();
            if (h == null || h.IsDead()) return false;

            if (FilterNeutralTargets)
            {
                if (go.CompareTag("Animal") || go.CompareTag("NPC"))
                {
                    return h.angry;
                }
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

        public static bool IsEldriar(BehaviourController controller)
        {
            if (controller == null) return false;
            bool nameMatches = controller.gameObject.name.IndexOf("Eldriar", StringComparison.OrdinalIgnoreCase) >= 0;
            bool treeMatches = !string.IsNullOrEmpty(controller.tree) && controller.tree.IndexOf("ELDRIAR", StringComparison.OrdinalIgnoreCase) >= 0;
            return nameMatches || treeMatches;
        }
    }

    [HarmonyPatch(typeof(Health), "Attacked")]
    public static class EldriarNPCDamageProtectionPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(Health __instance, float adj, HealthBase from)
        {
            if (!EldiriarFixPlugin.FilterNeutralTargets) return true;

            if (__instance == null) return true;

            if (IsNonAngryNPC(__instance))
            {
                if (from != null && IsEldriarObject(from.gameObject))
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

        private static bool IsEldriarObject(GameObject go)
        {
            if (go == null) return false;

            if (go.name.IndexOf("Eldriar", StringComparison.OrdinalIgnoreCase) >= 0) return true;

            BehaviourController c = go.GetComponent<BehaviourController>();
            if (c != null && EldiriarFixPlugin.IsEldriar(c)) return true;

            return false;
        }
    }

    public static class EldiriarFixConfig
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
            EldiriarFixConfig.Open = true;
            Overlay.ConfigOpen = true;
        }

        public static void Close()
        {
            EldiriarFixConfig.Open = false;
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
            float height = Mathf.Min(650f, Screen.height * 0.96f);
            float x = (Screen.width - width) / 2f;
            float y = (Screen.height - height) / 2f;

            GUI.Box(new Rect(x, y, width, height), GUIContent.none, Theme.Window);
            GUI.Label(new Rect(x, y + 14f, width, 30f), "Eldriar Tweaks Settings", _title);

            float contentY = y + 44f;
            float contentWidth = width - 48f;
            float btnHeight = 30f;
            float btnSpacing = 34f;

            string hoveredDesc = "Hover over any option to see what it does.";
            Vector2 mousePos = Event.current.mousePosition;

            Rect r1 = new Rect(x + 24f, contentY, contentWidth, btnHeight);
            if (r1.Contains(mousePos))
            {
                hoveredDesc = "Manually shoot Eldriar's Fireballs/Meteors when mounted on Right-Click (By Default). Keybind can be changed in the Key Mapping section in Controls.";
            }
            if (DrawToggleButton(r1, "Mounted Special Attack", EldiriarFixPlugin.MountedAbility))
            {
                EldiriarFixPlugin.MountedAbility = !EldiriarFixPlugin.MountedAbility;
                PlayerPrefs.SetInt("EldiriarFix_MountedAbility", EldiriarFixPlugin.MountedAbility ? 1 : 0);
                PlayerPrefs.Save();
            }

            contentY += btnSpacing;

            Rect r2 = new Rect(x + 24f, contentY, contentWidth, btnHeight);
            if (r2.Contains(mousePos))
            {
                hoveredDesc = "The Mounted Attack key can be held to shoot rapidly instead of clicking it lot of times. The projectiles will still shoot at their regular intervals.";
            }
            if (DrawToggleButton(r2, "Hold To Attack", EldiriarFixPlugin.HoldToAttack, enabled: EldiriarFixPlugin.MountedAbility))
            {
                EldiriarFixPlugin.HoldToAttack = !EldiriarFixPlugin.HoldToAttack;
                PlayerPrefs.SetInt("EldiriarFix_HoldToAttack", EldiriarFixPlugin.HoldToAttack ? 1 : 0);
                PlayerPrefs.Save();
            }

            contentY += btnSpacing;

            Rect r3 = new Rect(x + 24f, contentY, contentWidth, btnHeight);
            if (r3.Contains(mousePos))
            {
                hoveredDesc = "Tweaks Eldriar's AI so he automatically casts Fireballs/Meteors every 3 seconds in combat when dismounted.";
            }
            if (DrawToggleButton(r3, "AI Tweaks", EldiriarFixPlugin.UnmountedAIBoost))
            {
                EldiriarFixPlugin.UnmountedAIBoost = !EldiriarFixPlugin.UnmountedAIBoost;
                PlayerPrefs.SetInt("EldiriarFix_UnmountedAIBoost", EldiriarFixPlugin.UnmountedAIBoost ? 1 : 0);
                PlayerPrefs.Save();
            }

            contentY += btnSpacing;

            Rect r4 = new Rect(x + 24f, contentY, contentWidth, btnHeight);
            if (r4.Contains(mousePos))
            {
                hoveredDesc = "Makes Fireballs/Meteors actively steer and home in on targets instead of whatever it does in vanilla. Does not home in on Animals or Neutral mobs.";
            }
            if (DrawToggleButton(r4, "Homing Projectiles", EldiriarFixPlugin.HomingProjectiles))
            {
                EldiriarFixPlugin.HomingProjectiles = !EldiriarFixPlugin.HomingProjectiles;
                PlayerPrefs.SetInt("EldiriarFix_HomingProjectiles", EldiriarFixPlugin.HomingProjectiles ? 1 : 0);
                PlayerPrefs.Save();
            }

            contentY += btnSpacing;

            Rect r5 = new Rect(x + 24f, contentY, contentWidth, btnHeight);
            if (r5.Contains(mousePos))
            {
                hoveredDesc = "Increases Eldriar's ranged fireball/meteor engagement distance to 35m without affecting his melee bite range.";
            }
            if (DrawToggleButton(r5, "Increased Attack Range", EldiriarFixPlugin.EnhancedRange))
            {
                EldiriarFixPlugin.EnhancedRange = !EldiriarFixPlugin.EnhancedRange;
                PlayerPrefs.SetInt("EldiriarFix_EnhancedRange", EldiriarFixPlugin.EnhancedRange ? 1 : 0);
                PlayerPrefs.Save();
            }

            contentY += btnSpacing;

            Rect r6 = new Rect(x + 24f, contentY, contentWidth, btnHeight);
            if (r6.Contains(mousePos))
            {
                hoveredDesc = "WARNING: RECOMMENDED TO BE ON BY DEFAULT! Automatically removes fireballs/meteors if the target dies or if the projectile get stuck on terrain or fluids and never explode or despawn.";
            }
            if (DrawToggleButton(r6, "Projectile Cleanup", EldiriarFixPlugin.CleanGhostProjectiles))
            {
                EldiriarFixPlugin.CleanGhostProjectiles = !EldiriarFixPlugin.CleanGhostProjectiles;
                PlayerPrefs.SetInt("EldiriarFix_CleanGhostProjectiles", EldiriarFixPlugin.CleanGhostProjectiles ? 1 : 0);
                PlayerPrefs.Save();
            }

            contentY += btnSpacing;

            Rect r7 = new Rect(x + 24f, contentY, contentWidth, btnHeight);
            if (r7.Contains(mousePos))
            {
                hoveredDesc = "Prevents Eldriar's fireballs/meteors from damaging NPCs. Passive animals and angry NPCs can still be damaged.";
            }
            if (DrawToggleButton(r7, "NPC Protection", EldiriarFixPlugin.FilterNeutralTargets))
            {
                EldiriarFixPlugin.FilterNeutralTargets = !EldiriarFixPlugin.FilterNeutralTargets;
                PlayerPrefs.SetInt("EldiriarFix_FilterNeutralTargets", EldiriarFixPlugin.FilterNeutralTargets ? 1 : 0);
                PlayerPrefs.Save();
            }

            contentY += btnSpacing;

            Rect r8 = new Rect(x + 24f, contentY, contentWidth, btnHeight);
            if (r8.Contains(mousePos))
            {
                hoveredDesc = "Speeds up fireballs and meteors. Fireballs move much faster and meteors fall down faster in straight lines.";
            }
            if (DrawToggleButton(r8, "Faster Projectiles", EldiriarFixPlugin.AlternateProjectiles))
            {
                EldiriarFixPlugin.AlternateProjectiles = !EldiriarFixPlugin.AlternateProjectiles;
                PlayerPrefs.SetInt("EldiriarFix_AlternateProjectiles", EldiriarFixPlugin.AlternateProjectiles ? 1 : 0);
                PlayerPrefs.Save();
            }

            float descY = y + 322f;
            float descHeight = 180f;
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

    public class ProjectileCleaner : MonoBehaviour
    {
        public Transform TargetTransform;
        public Health TargetHealth;
        public float MaxLifetime = 4.0f;

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
