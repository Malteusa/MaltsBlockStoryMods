using BepInEx;
using HarmonyLib;
using UnityEngine;
using BlockStoryCore;
using ISRef = UnityEngine.InputSystem.InputActionReference;

namespace BlockStoryMod
{
    [BepInPlugin("com.malts.blockstory.hidediamonds", "Hide Diamonds", "1.2.0")]
    [BepInDependency(Core.Guid)]
    public class HideDiamondsPlugin : BaseUnityPlugin
    {
        public static bool ModEnabled = PlayerPrefs.GetInt("HideDiamonds_ModEnabled", 1) != 0;
        public static bool DiamondsHidden = PlayerPrefs.GetInt("HideDiamonds_IsHidden", 0) != 0;

        private ISRef _toggleKey;

        private void Awake()
        {
            _toggleKey = BSKeybinds.Register("Hide Diamonds", "Hide Diamonds", "<Keyboard>/o");

            ModRegistry.Register(new ModInfo
            {
                Name = "Hide Diamond Display",
                Description = "Adds a keybind to hide the diamonds display.",
                GetEnabled = () => ModEnabled,
                SetEnabled = on => 
                { 
                    ModEnabled = on; 
                    PlayerPrefs.SetInt("HideDiamonds_ModEnabled", on ? 1 : 0); 
                    PlayerPrefs.Save(); 
                    
                    if (!on) SetDiamondsVisibility(true);
                    else SetDiamondsVisibility(!DiamondsHidden);
                },
                HasConfig = false,
            });

            Harmony.CreateAndPatchAll(typeof(HideDiamondsPlugin).Assembly);

            Core.Log?.LogInfo("[HideDiamonds]: Loaded successfully.");
        }

        private void Update()
        {
            if (!ModEnabled) return;

            if (BSKeybinds.Pressed(_toggleKey))
            {
                DiamondsHidden = !DiamondsHidden;
                PlayerPrefs.SetInt("HideDiamonds_IsHidden", DiamondsHidden ? 1 : 0);
                PlayerPrefs.Save();

                SetDiamondsVisibility(!DiamondsHidden);

                Core.Log?.LogInfo($"[HideDiamonds]: Diamond display visible = {!DiamondsHidden}");
            }
        }

        public static void SetDiamondsVisibility(bool visible)
        {
            DisplayCurrency[] displays = Resources.FindObjectsOfTypeAll<DisplayCurrency>();
            foreach (var display in displays)
            {
                ApplyToInstance(display, visible);
            }
        }

        public static void ApplyToInstance(DisplayCurrency display, bool visible)
        {
            if (display == null) return;

            if (display.label != null)
            {
                display.label.enabled = visible;
            }

            if (display.tween != null)
            {
                display.tween.gameObject.SetActive(visible);
            }

            if (display.particles != null)
            {
                display.particles.SetActive(visible);
            }

            var renderers = display.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                r.enabled = visible;
            }
        }
    }

    [HarmonyPatch(typeof(DisplayCurrency), "OnEnable")]
    public static class DisplayCurrency_OnEnable_Patch
    {
        public static void Postfix(DisplayCurrency __instance)
        {
            if (!HideDiamondsPlugin.ModEnabled) return;
            HideDiamondsPlugin.ApplyToInstance(__instance, !HideDiamondsPlugin.DiamondsHidden);
        }
    }
}