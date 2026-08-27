using System; 
using BepInEx; 
using HarmonyLib; 
using UnityEngine; 
using BlockStoryCore; 
using ISRef = UnityEngine.InputSystem.InputActionReference;

namespace BlockStoryMod 
{
    [BepInPlugin("com.malts.blockstory.knockbacktweaks", "KnockbackTweaks", "2.0.0")] 
    [BepInDependency(Core.Guid)] 
    public class KnockbackTweaksPlugin : BaseUnityPlugin 
    { 
        public static bool Enabled = PlayerPrefs.GetInt("KB_Enabled", 1) != 0; 
        public static int KnockbackMultiplier = PlayerPrefs.GetInt("KB_Multiplier", 1);

        private ISRef _key;
        private bool _open;
        private bool _wasOpen;
        private Rect _win = new Rect(60, 60, 400, 220);
        private Harmony _harmony;

        private void Awake()
        {
            _key = BSKeybinds.Register("KnockbackTweaks", "Open Knockback Menu", "<Keyboard>/period");

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

        private void OnGUI()
        {
            if (!Enabled || !_open) return;
            Theme.Build();
            _win = GUILayout.Window(GetInstanceID(), _win, DrawWindow, "Knockback Control", Theme.Window);
        }

        private void DrawWindow(int id)
        {
            GUILayout.Label("Knockback Multiplier", Theme.Label);
            GUILayout.Label($"{KnockbackMultiplier}x", Theme.Label);

            int newMultiplier = (int)GUILayout.HorizontalSlider(KnockbackMultiplier, 1f, 1000f);
            if (newMultiplier != KnockbackMultiplier)
            {
                KnockbackMultiplier = newMultiplier;
                PlayerPrefs.SetInt("KB_Multiplier", KnockbackMultiplier);
                PlayerPrefs.Save();
            }

            GUILayout.Space(15);

            if (GUILayout.Button("Close", Theme.Button))
            {
                _open = false;
            }

            GUILayout.Space(5);
            GUI.DragWindow(new Rect(0, 0, 100000, 26));
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