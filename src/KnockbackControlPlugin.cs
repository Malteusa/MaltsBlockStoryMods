using System; 
using BepInEx; 
using HarmonyLib; 
using UnityEngine; 
using BlockStoryCore; 
using ISRef = UnityEngine.InputSystem.InputActionReference;

namespace BlockStoryMod 
{
    [BepInPlugin("com.malts.blockstory.knockbacktweaks", "KnockbackTweaks", "2.2.0")] 
    [BepInDependency(Core.Guid)] 
    public class KnockbackTweaksPlugin : BaseUnityPlugin 
    { 
        public static bool Enabled = PlayerPrefs.GetInt("KB_Enabled", 1) != 0; 
        public static bool SavePersistent = PlayerPrefs.GetInt("KB_SavePersistent", 0) != 0; 
        public static int KnockbackMultiplier = SavePersistent ? PlayerPrefs.GetInt("KB_Multiplier", 1) : 1;

        private ISRef _key;
        private bool _open;
        private bool _wasOpen;
        private Rect _win = new Rect(60, 60, 360, 230);
        private Harmony _harmony;

        private static GUIStyle _hdr;
        private static GUIStyle _row;
        private static GUIStyle _back;
        private static bool _styles;
        private static int _builtVer;

        private void Awake()
        {
            _key = BSKeybinds.Register("KnockbackTweaks", "Open Knockback Menu", "<Keyboard>/period");

            if (!SavePersistent)
            {
                KnockbackMultiplier = 1;
                PlayerPrefs.SetInt("KB_Multiplier", 1);
                PlayerPrefs.Save();
            }

            ModRegistry.Register(new ModInfo
            {
                Name = "Knockback Control",
                Description = "Adjust Knockback",
                GetEnabled = () => Enabled,
                SetEnabled = on => 
                { 
                    Enabled = on; 
                    PlayerPrefs.SetInt("KB_Enabled", on ? 1 : 0); 
                    PlayerPrefs.Save(); 
                    if (!on) _open = false; 
                },
                HasConfig = false,
            });

            _harmony = new Harmony("com.malts.blockstory.knockbacktweaks");
            _harmony.PatchAll();

            Core.Log?.LogInfo("[KnockbackTweaks]: Loaded successfully.");
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }

        private void OnApplicationQuit()
        {
            if (!SavePersistent)
            {
                PlayerPrefs.SetInt("KB_Multiplier", 1);
                PlayerPrefs.Save();
            }
        }

        private void Update()
        {
            if (Enabled && BSKeybinds.Pressed(_key))
            {
                _open = !_open;
            }

            if (_open != _wasOpen)
            {
                _wasOpen = _open;
                Cursor.visible = _open;
                Cursor.lockState = _open ? CursorLockMode.None : CursorLockMode.Locked;
            }
        }

        private static void BuildStyles()
        {
            Theme.Build();
            if (_styles && _builtVer == Theme.Version)
            {
                return;
            }

            _hdr = new GUIStyle(Theme.LabelGold)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white }
            };
            _row = new GUIStyle(Theme.Button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white },
                padding = new RectOffset(2, 2, 2, 2),
                margin = new RectOffset(2, 2, 2, 2)
            };
            _back = new GUIStyle(Theme.Button)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white },
                padding = new RectOffset(4, 4, 4, 4),
                margin = new RectOffset(2, 2, 2, 2)
            };

            _styles = true;
            _builtVer = Theme.Version;
        }

        private void OnGUI()
        {
            if (!Enabled || !_open) return;

            BuildStyles();
            _win = GUILayout.Window(GetInstanceID(), _win, DrawWindow, "Knockback Control", Theme.Window);
        }

        private void DrawWindow(int id)
        {
            GUILayout.Label("Knockback Settings:", _hdr);
            GUILayout.Space(4);

            GUILayout.Label($"Knockback Multiplier: {KnockbackMultiplier}x", _hdr);
            GUILayout.Space(2);

            int newMultiplier = (int)GUILayout.HorizontalSlider(KnockbackMultiplier, 1f, 1000f);
            if (newMultiplier != KnockbackMultiplier)
            {
                KnockbackMultiplier = newMultiplier;
                if (SavePersistent)
                {
                    PlayerPrefs.SetInt("KB_Multiplier", KnockbackMultiplier);
                    PlayerPrefs.Save();
                }
            }

            GUILayout.Space(10);

            if (DrawToggleButton("Persistent Knockback Value", SavePersistent))
            {
                SavePersistent = !SavePersistent;
                PlayerPrefs.SetInt("KB_SavePersistent", SavePersistent ? 1 : 0);
                if (SavePersistent)
                {
                    PlayerPrefs.SetInt("KB_Multiplier", KnockbackMultiplier);
                }
                else
                {
                    PlayerPrefs.SetInt("KB_Multiplier", 1);
                }
                PlayerPrefs.Save();
            }

            GUILayout.Space(12);

            if (GUILayout.Button("Close", _back, GUILayout.Height(28f)))
            {
                _open = false;
            }

            GUILayout.Space(2);
            GUI.DragWindow(new Rect(0, 0, 100000, 26));
        }

        private static bool DrawToggleButton(string label, bool state, bool enabled = true)
        {
            bool prevEnabled = GUI.enabled;
            GUI.enabled = enabled;

            Color prevBg = GUI.backgroundColor;
            if (enabled)
            {
                GUI.backgroundColor = state ? new Color(0.18f, 0.65f, 0.22f, 1f) : new Color(0.72f, 0.2f, 0.2f, 1f);
            }

            string symbol = state ? "● " : "○ ";
            bool clicked = GUILayout.Button(symbol + label, _row, GUILayout.Height(28f));

            GUI.backgroundColor = prevBg;
            GUI.enabled = prevEnabled;

            return clicked;
        }
    }

    [HarmonyPatch(typeof(Health))]
    public static class KnockbackPatches
    {
        private static readonly Action<HealthBase, HealthBase, InvEffect.Identifier> ApplyImpactDelegate =
            AccessTools.MethodDelegate<Action<HealthBase, HealthBase, InvEffect.Identifier>>(
                AccessTools.Method(typeof(HealthBase), "ApplyImpactToMotor")
            );

        [HarmonyPatch(nameof(Health.Attacked))]
        [HarmonyPostfix]
        public static void Attacked_Postfix(Health __instance, float adj, HealthBase from, InvEffect.Identifier type, bool timed)
        {
            if (!KnockbackTweaksPlugin.Enabled || __instance == null || adj >= 0f || timed) return;

            int extraImpulses = KnockbackTweaksPlugin.KnockbackMultiplier - 1;

            if (extraImpulses > 0 && ApplyImpactDelegate != null)
            {
                for (int i = 0; i < extraImpulses; i++)
                {
                    ApplyImpactDelegate(__instance, from, type);
                }
            }
        }
    }
}
