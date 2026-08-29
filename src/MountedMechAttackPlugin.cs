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
    [BepInPlugin("com.malts.blockstory.mountedmechattack", "MountedMechAttack", "2.7.0")]
    [BepInDependency(Core.Guid)]
    public class MountedMechAttackPlugin : BaseUnityPlugin
    {
        private Harmony _harmony;

        public static bool Enabled = PlayerPrefs.GetInt("MountedMechAttack_Enabled", 1) != 0;
        public static bool HoldToAttack = PlayerPrefs.GetInt("MountedMechAttack_HoldToAttack", 0) != 0;
        public static bool VanillaRockets = PlayerPrefs.GetInt("MountedMechAttack_VanillaRockets", 0) != 0;
        public static bool DestroyBlocks = PlayerPrefs.GetInt("MountedMechAttack_DestroyBlocks", 0) != 0;
        public static bool CleanGhostProjectiles = PlayerPrefs.GetInt("MountedMechAttack_CleanGhostProjectiles", 0) != 0;

        private ISRef _key;

        private float _lastAttackTime = 0f;
        public float AttackCooldown = 1.0f;

        private static readonly FieldInfo AnimField = typeof(BehaviourController).GetField("animation", BindingFlags.Public | BindingFlags.Instance);
        private static MethodInfo _crossFadeMethod;

        private void Awake()
        {
            _harmony = new Harmony("com.malts.blockstory.mountedmechattack");
            _harmony.PatchAll();

            _key = BSKeybinds.Register("MountedMechAttack", "Shoot Mech Rocket", "<Mouse>/rightButton");

            ModInfo modInfo = new ModInfo
            {
                Name = "Mounted Mech Attack",
                Description = "Shoot rockets from the mech pet while mounted. Still requires rockets in the Mech's inventory.",
                GetEnabled = () => Enabled,
                SetEnabled = on => { Enabled = on; PlayerPrefs.SetInt("MountedMechAttack_Enabled", on ? 1 : 0); PlayerPrefs.Save(); },
                HasConfig = true,
            };

            Action configAction = MountedMechAttackConfig.OpenFromMenu;
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
            Core.Log?.LogInfo("[MountedMechAttack]: Loaded successfully.");
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }

        private void OnGUI()
        {
            MountedMechAttackConfig.Draw();
        }

        private void Update()
        {
            if (!Enabled) return;

            HandleMountedMech();
        }

        private void HandleMountedMech()
        {
            GameObject mountedGo = PlayerMounted.mounted;
            if (mountedGo == null) return;

            MechMount mount = mountedGo.GetComponent<MechMount>();
            if (mount == null || !mount.mounted) return;

            bool isHeld = _key != null && _key.action != null && _key.action.IsPressed();
            bool inputTriggered = HoldToAttack ? (BSKeybinds.Pressed(_key) || isHeld) : BSKeybinds.Pressed(_key);

            if (inputTriggered && Time.time >= _lastAttackTime + AttackCooldown)
            {
                _lastAttackTime = Time.time;

                GameObject target = GetBestTarget(mountedGo);

                StartCoroutine(LaunchRocketVanilla(mount, target));
            }
        }

        private IEnumerator LaunchRocketVanilla(MechMount mount, GameObject targetGo)
        {
            if (mount == null || mount.inventory == null) yield break;

            int rocketsCount = mount.inventory.Count("Rockets", 0);
            if (rocketsCount <= 0 || mount.fireball == null || mount.fireball.fireBalls == null || mount.fireball.fireBalls.Length == 0) yield break;

            if (mount.rocketModel != null)
            {
                mount.rocketModel.SetActive(false);
            }

            GameObject rocket = Instantiate(
                mount.fireball.fireBalls[UnityEngine.Random.Range(0, mount.fireball.fireBalls.Length)],
                mount.fireball.spawnPoint != null ? mount.fireball.spawnPoint.position : mount.transform.position + mount.transform.forward * 2f,
                mount.transform.rotation
            );

            InvBaseItem invBaseItem = InvDatabase.FindByName(mount.fireball.itemName ?? "Rockets");

            Transform targetTransform = targetGo != null ? targetGo.transform : null;
            Health targetHealth = targetGo != null ? targetGo.GetComponent<Health>() : null;

            if (CleanGhostProjectiles)
            {
                MechRocketCleaner cleaner = rocket.AddComponent<MechRocketCleaner>();
                cleaner.Init(targetTransform, targetHealth, maxLifetime: 5.0f);
            }

            Homing homing = rocket.GetComponent<Homing>();
            if (homing != null)
            {
                float rocketSpeed = VanillaRockets ? mount.fireball.speed : 30f;
                float rotSpeed = VanillaRockets ? mount.fireball.rotationSpeed : 150f;
                float rocketDmg = mount.fireball.demage;

                homing.InitSettings(
                    targetTransform,
                    mount.gameObject,
                    rocketDmg,
                    rocketSpeed,
                    rotSpeed,
                    mount.fireball.cantHitPlayer,
                    false,
                    invBaseItem,
                    10f,
                    targetTransform != null
                );
            }

            mount.inventory.Consume("Rockets", 0, 1);

            yield return new WaitForSeconds(1f);
            if (mount.rocketModel != null)
            {
                mount.rocketModel.SetActive(true);
            }
        }

        private GameObject GetBestTarget(GameObject mechGo)
        {
            Camera cam = Camera.main;

            if (cam != null)
            {
                Ray ray = new Ray(cam.transform.position, cam.transform.forward);
                RaycastHit[] hits = Physics.RaycastAll(ray, 70f);
                Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                foreach (RaycastHit hit in hits)
                {
                    GameObject hitGo = hit.collider.gameObject;
                    if (IsValidTarget(hitGo, mechGo))
                    {
                        return hitGo;
                    }

                    Transform current = hitGo.transform.parent;
                    while (current != null)
                    {
                        if (IsValidTarget(current.gameObject, mechGo))
                        {
                            return current.gameObject;
                        }
                        current = current.parent;
                    }
                }
            }

            Vector3 pos = mechGo.transform.position;
            float closestSqr = 40f * 40f;
            GameObject closest = null;

#pragma warning disable CS0618
            GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
#pragma warning restore CS0618

            foreach (GameObject enemy in enemies)
            {
                if (enemy == null || !IsValidTarget(enemy, mechGo)) continue;

                float sqr = (enemy.transform.position - pos).sqrMagnitude;
                if (sqr < closestSqr)
                {
                    closestSqr = sqr;
                    closest = enemy;
                }
            }

            return closest;
        }

        private bool IsValidTarget(GameObject go, GameObject mechGo)
        {
            if (go == null) return false;
            if (go.CompareTag("Player") || go.CompareTag("Pet")) return false;
            if (go == mechGo) return false;

            Health h = go.GetComponent<Health>();
            if (h == null || h.IsDead()) return false;

            if (go.CompareTag("Enemy"))
            {
                return true;
            }

            if (go.CompareTag("Animal") || go.CompareTag("NPC"))
            {
                return h.angry;
            }

            return false;
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

        public static bool IsMech(GameObject go)
        {
            if (go == null) return false;
            if (go.GetComponent<MechMount>() != null) return true;
            if (go.name.IndexOf("Mech", StringComparison.OrdinalIgnoreCase) >= 0) return true;

            BehaviourController c = go.GetComponent<BehaviourController>();
            if (c != null && !string.IsNullOrEmpty(c.tree) && c.tree.IndexOf("MECH", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(Health), "Attacked")]
    public static class MechNPCDamageProtectionPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(Health __instance, float adj, HealthBase from)
        {
            if (__instance == null) return true;

            if (__instance.CompareTag("NPC") && !__instance.angry)
            {
                if (from != null && MountedMechAttackPlugin.IsMech(from.gameObject))
                {
                    return false;
                }
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(Homing), "StartHit")]
    public static class MechRocketStartHitPatch
    {
        private static readonly FieldInfo UserField = typeof(Homing).GetField("user", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo DamageField = typeof(Homing).GetField("damage", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo CantHitPlayerField = typeof(Homing).GetField("cantHitPlayer", BindingFlags.NonPublic | BindingFlags.Instance);

        [HarmonyPrefix]
        public static bool Prefix(Homing __instance)
        {
            if (MountedMechAttackPlugin.DestroyBlocks)
            {
                return true;
            }

            GameObject user = UserField?.GetValue(__instance) as GameObject;

            if (user == null || !MountedMechAttackPlugin.IsMech(user))
            {
                return true;
            }

            float damage = DamageField != null ? (float)DamageField.GetValue(__instance) : 150f;
            bool cantHitPlayer = CantHitPlayerField != null && (bool)CantHitPlayerField.GetValue(__instance);

            Vector3 epicenter = __instance.transform.position;
            float radius = 4.5f;

            Collider[] colliders = Physics.OverlapSphere(epicenter, radius);
            HashSet<GameObject> hitObjects = new HashSet<GameObject>();

            HealthBase userHealth = user.GetComponent<HealthBase>();

            foreach (Collider col in colliders)
            {
                if (col == null) continue;
                GameObject go = col.gameObject;

                if (cantHitPlayer && (go.CompareTag("Player") || go.CompareTag("Pet"))) continue;
                if (go == user) continue;

                HealthBase health = go.GetComponent<HealthBase>() ?? go.GetComponentInParent<HealthBase>();
                if (health == null || health.IsDead()) continue;

                GameObject rootGo = health.gameObject;
                if (hitObjects.Contains(rootGo)) continue;
                hitObjects.Add(rootGo);

                float dist = Vector3.Distance(epicenter, col.ClosestPoint(epicenter));
                float falloff = Mathf.Clamp01(1f - (dist / radius));
                float finalDamage = -Mathf.Max(10f, damage * falloff);

                health.Attacked(finalDamage, userHealth, null, InvEffect.Identifier.NormalDamage, false);
            }

            return false;
        }
    }

    public static class MountedMechAttackConfig
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
            MountedMechAttackConfig.Open = true;
            Overlay.ConfigOpen = true;
        }

        public static void Close()
        {
            MountedMechAttackConfig.Open = false;
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

            float width = Mathf.Min(540f, Screen.width * 0.8f);
            float height = Mathf.Min(480f, Screen.height * 0.88f);
            float x = (Screen.width - width) / 2f;
            float y = (Screen.height - height) / 2f;

            GUI.Box(new Rect(x, y, width, height), GUIContent.none, Theme.Window);
            GUI.Label(new Rect(x, y + 14f, width, 30f), "Mounted Mech Attack Settings", _title);

            float contentY = y + 44f;
            float contentWidth = width - 48f;
            float btnHeight = 30f;
            float btnSpacing = 38f;

            string hoveredDesc = "Hover over any option to see what it does.";
            Vector2 mousePos = Event.current.mousePosition;

            Rect r1 = new Rect(x + 24f, contentY, contentWidth, btnHeight);
            if (r1.Contains(mousePos))
            {
                hoveredDesc = "The Mounted Attack key can be held down to continuously fire rockets as long as there are rockets in the Mech's inventory.";
            }
            if (DrawToggleButton(r1, "Hold To Fire", MountedMechAttackPlugin.HoldToAttack))
            {
                MountedMechAttackPlugin.HoldToAttack = !MountedMechAttackPlugin.HoldToAttack;
                PlayerPrefs.SetInt("MountedMechAttack_HoldToAttack", MountedMechAttackPlugin.HoldToAttack ? 1 : 0);
                PlayerPrefs.Save();
            }

            contentY += btnSpacing;

            Rect r2 = new Rect(x + 24f, contentY, contentWidth, btnHeight);
            if (r2.Contains(mousePos))
            {
                hoveredDesc = "Enables vanilla rocket speed and homing when mounted. Otherwise mounted rockets are faster and home in better.";
            }
            if (DrawToggleButton(r2, "Vanilla Rocket Speed", MountedMechAttackPlugin.VanillaRockets))
            {
                MountedMechAttackPlugin.VanillaRockets = !MountedMechAttackPlugin.VanillaRockets;
                PlayerPrefs.SetInt("MountedMechAttack_VanillaRockets", MountedMechAttackPlugin.VanillaRockets ? 1 : 0);
                PlayerPrefs.Save();
            }

            contentY += btnSpacing;

            Rect r3 = new Rect(x + 24f, contentY, contentWidth, btnHeight);
            if (r3.Contains(mousePos))
            {
                hoveredDesc = "When enabled, rockets break surrounding terrain upon exploding (Like Vanilla). When disabled, rockets only damage mobs without destroying blocks.";
            }
            if (DrawToggleButton(r3, "Terrain Destruction", MountedMechAttackPlugin.DestroyBlocks))
            {
                MountedMechAttackPlugin.DestroyBlocks = !MountedMechAttackPlugin.DestroyBlocks;
                PlayerPrefs.SetInt("MountedMechAttack_DestroyBlocks", MountedMechAttackPlugin.DestroyBlocks ? 1 : 0);
                PlayerPrefs.Save();
            }

            contentY += btnSpacing;

            Rect r4 = new Rect(x + 24f, contentY, contentWidth, btnHeight);
            if (r4.Contains(mousePos))
            {
                hoveredDesc = "Automatically removes rockets if the target dies or if the rockets gets stuck somewhere.";
            }
            if (DrawToggleButton(r4, "Projectile Cleanup", MountedMechAttackPlugin.CleanGhostProjectiles))
            {
                MountedMechAttackPlugin.CleanGhostProjectiles = !MountedMechAttackPlugin.CleanGhostProjectiles;
                PlayerPrefs.SetInt("MountedMechAttack_CleanGhostProjectiles", MountedMechAttackPlugin.CleanGhostProjectiles ? 1 : 0);
                PlayerPrefs.Save();
            }

            float descY = y + 205f;
            float descHeight = 140f;
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

    public class MechRocketCleaner : MonoBehaviour
    {
        public Transform TargetTransform;
        public Health TargetHealth;
        public float MaxLifetime = 5.0f;

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