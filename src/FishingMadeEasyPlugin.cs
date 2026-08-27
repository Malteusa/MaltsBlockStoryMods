using System;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using BlockStoryCore;
using ISRef = UnityEngine.InputSystem.InputActionReference;

namespace BlockStoryMod
{
    [BepInPlugin("com.malts.blockstory.fishingmadeeasy", "FishingMadeEasy", "5.2.0")]
    [BepInDependency(Core.Guid)]
    public class FishingMadeEasyPlugin : BaseUnityPlugin
    {
        public static bool Enabled = PlayerPrefs.GetInt("FME_Enabled", 1) != 0;
        public static bool AutoReel = PlayerPrefs.GetInt("FME_AutoReel", 1) != 0;
        public static bool GuaranteedCatch = PlayerPrefs.GetInt("FME_GuaranteedCatch", 1) != 0;
        public static bool FailCatchLoot = PlayerPrefs.GetInt("FME_FailCatchLoot", 1) != 0;
        public static int LootMultiplier = PlayerPrefs.GetInt("FME_LootMultiplier", 1);

        private ISRef _key;
        private bool _open;
        private bool _wasOpen;
        private Rect _win = new Rect(60, 60, 370, 265);
        private Harmony _harmony;

        private static readonly int[] Multipliers = { 1, 2, 5, 10, 25, 50, 100 };

        private static GUIStyle _hdr;
        private static GUIStyle _row;
        private static GUIStyle _back;
        private static bool _styles;
        private static int _builtVer;

        private void Awake()
        {
            _key = BSKeybinds.Register("FishingMadeEasy", "Open Fishing Menu", "<Keyboard>/leftBracket");

            ModRegistry.Register(new ModInfo
            {
                Name = "Fishing Made Easy",
                Description = "Tweaks to the Fishing System",
                GetEnabled = () => Enabled,
                SetEnabled = on => 
                { 
                    Enabled = on; 
                    PlayerPrefs.SetInt("FME_Enabled", on ? 1 : 0); 
                    PlayerPrefs.Save(); 
                    if (!on) _open = false; 
                },
                HasConfig = false,
            });

            _harmony = new Harmony("com.malts.blockstory.fishingmadeeasy");
            _harmony.PatchAll();

            Core.Log?.LogInfo("[FishingMadeEasy]: Loaded successfully.");
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
            _win = GUILayout.Window(GetInstanceID(), _win, DrawWindow, "Fishing Made Easy", Theme.Window);
        }

        private void DrawWindow(int id)
        {
            GUILayout.Label("Fishing Settings:", _hdr);
            GUILayout.Space(2);

            if (DrawToggleButton("Auto Catch on Bite", AutoReel))
            {
                AutoReel = !AutoReel;
                SavePref("FME_AutoReel", AutoReel);
            }

            if (DrawToggleButton("Disable 50% Fail Rate", GuaranteedCatch))
            {
                GuaranteedCatch = !GuaranteedCatch;
                SavePref("FME_GuaranteedCatch", GuaranteedCatch);
            }

            if (DrawToggleButton("Enable Loot on Failed Catch", FailCatchLoot))
            {
                FailCatchLoot = !FailCatchLoot;
                SavePref("FME_FailCatchLoot", FailCatchLoot);
            }

            GUILayout.Space(6);

            GUILayout.Label($"Secondary Loot Rolls: {LootMultiplier}x", _hdr);
            GUILayout.Space(2);

            GUILayout.BeginHorizontal();
            foreach (int mult in Multipliers)
            {
                bool isSelected = (mult == LootMultiplier);
                Color prevBg = GUI.backgroundColor;
                if (isSelected) GUI.backgroundColor = new Color(0.18f, 0.65f, 0.22f, 1f);

                if (GUILayout.Button($"{mult}x", _row, GUILayout.Height(26f)))
                {
                    LootMultiplier = mult;
                    PlayerPrefs.SetInt("FME_LootMultiplier", LootMultiplier);
                    PlayerPrefs.Save();
                }

                GUI.backgroundColor = prevBg;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(8);

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

        private static void SavePref(string key, bool val)
        {
            PlayerPrefs.SetInt(key, val ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    [HarmonyPatch(typeof(FishRodInput))]
    public static class FishRodInputPatches
    {
        [HarmonyPatch("Bite")]
        [HarmonyPostfix]
        public static void Bite_Postfix(FishRodInput __instance)
        {
            if (!FishingMadeEasyPlugin.Enabled || !FishingMadeEasyPlugin.AutoReel) return;

            var traverse = Traverse.Create(__instance);
            traverse.Field("isFloatBounce").SetValue(true);
            traverse.Method("Take").GetValue();
        }

        [HarmonyPatch("DropLoot")]
        [HarmonyPrefix]
        public static bool DropLoot_Prefix(FishRodInput __instance)
        {
            if (!FishingMadeEasyPlugin.Enabled) return true;

            var traverse = Traverse.Create(__instance);
            Health lastAttackedHealth = traverse.Field("lastAttackedHealth").GetValue<Health>();
            Transform rodEndPoint = traverse.Field("rodEndPoint").GetValue<Transform>();
            InventoryCollector inventory = __instance.inventory;

            Vector3 spawnPos = (rodEndPoint != null) 
                ? rodEndPoint.position + UnityEngine.Random.insideUnitSphere * 0.3f 
                : __instance.transform.position;

            int roll = UnityEngine.Random.Range(0, 100);
            bool catchSuccess = FishingMadeEasyPlugin.GuaranteedCatch || (roll <= __instance.chance);

            if (catchSuccess)
            {
                if (lastAttackedHealth != null)
                {
                    lastAttackedHealth.Dispawn();
                    if (lastAttackedHealth.fishingLoot != null)
                    {
                        GameObject fishItem = UnityEngine.Object.Instantiate(lastAttackedHealth.fishingLoot, spawnPos, Quaternion.identity);
                        fishItem.SendMessage("SetCount", 1, SendMessageOptions.DontRequireReceiver);
                        fishItem.SendMessage("SetPickup", true, SendMessageOptions.DontRequireReceiver);
                        
                        if (inventory != null)
                            fishItem.SendMessage("AutoCollect", inventory.transform, SendMessageOptions.DontRequireReceiver);
                    }
                }
            }
            else
            {
                if (!FishingMadeEasyPlugin.FailCatchLoot)
                {
                    return false;
                }
            }

            if (__instance.loot != null && __instance.loot.Length > 0)
            {
                int totalRolls = Mathf.Max(1, FishingMadeEasyPlugin.LootMultiplier);

                for (int i = 0; i < totalRolls; i++)
                {
                    foreach (LootDrop lootDrop in __instance.loot)
                    {
                        if (lootDrop.prefab != null && UnityEngine.Random.value < lootDrop.probability)
                        {
                            int count = UnityEngine.Random.Range(1, Mathf.Max(2, lootDrop.count));

                            GameObject extraLoot = UnityEngine.Object.Instantiate(lootDrop.prefab, spawnPos, Quaternion.identity);
                            extraLoot.SendMessage("SetCount", count, SendMessageOptions.DontRequireReceiver);
                            extraLoot.SendMessage("SetPickup", true, SendMessageOptions.DontRequireReceiver);
                            
                            if (inventory != null)
                                extraLoot.SendMessage("AutoCollect", inventory.transform, SendMessageOptions.DontRequireReceiver);

                            break;
                        }
                    }
                }
            }

            return false;
        }
    }
}