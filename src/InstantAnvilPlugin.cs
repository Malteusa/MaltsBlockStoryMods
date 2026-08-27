using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BlockStoryCore;
using HarmonyLib;
using UnityEngine;

namespace BlockStoryMod
{
    [BepInPlugin("com.malts.blockstory.instantanvil", "InstantAnvil", "2.2.1")]
    [BepInDependency(Core.Guid)]
    public class InstantAnvilPlugin : BaseUnityPlugin
    {
        private Harmony _harmony;

        public static bool InstantRepair = PlayerPrefs.GetInt("InstantAnvil_InstantRepair", 0) != 0;
        public static bool AnvilMerging = PlayerPrefs.GetInt("InstantAnvil_AnvilMerging", 1) != 0;
        public static bool InstantAnvilMerging = PlayerPrefs.GetInt("InstantAnvil_InstantAnvilMerging", 0) != 0;
        public static bool HammerRepairing = PlayerPrefs.GetInt("InstantAnvil_HammerRepairing", 1) != 0;

        public static bool AnvilMergeDetected { get; private set; } = false;

        private void Awake()
        {
            if (Chainloader.PluginInfos.ContainsKey("com.malts.blockstory.anvilmerge"))
            {
                AnvilMergeDetected = true;
                Core.Log?.LogWarning("[InstantAnvil]: WARNING! [AnvilMerge] (com.malts.blockstory.anvilmerge) is also loaded! " +
                                     "InstantAnvil already includes AnvilMerge, disable anvilmerge to prevent lag!");
            }

            _harmony = new Harmony("com.malts.blockstory.instantanvil");
            _harmony.PatchAll();

            ModInfo modInfo = new ModInfo
            {
                Name = "Instant Anvil",
                Description = "Configurable instant repairs, item merging, and hammer repairs.",
                GetEnabled = () => true,
                SetEnabled = _ => { },
                HasConfig = true,
            };

            Action configAction = InstantAnvilConfig.OpenFromMenu;
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
            Core.Log?.LogInfo("[InstantAnvil]: Loaded successfully.");
        }

        private void OnGUI()
        {
            InstantAnvilConfig.Draw();
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }

        public static bool IsHammer(InvGameItem item)
        {
            if (item == null) return false;
            if (item.name != null && item.name.ToLower().Contains("hammer")) return true;
            if (item.baseItem != null && item.baseItem.name != null && item.baseItem.name.ToLower().Contains("hammer")) return true;
            return false;
        }
    }

    public static class InstantAnvilConfig
    {
        public static bool Open;

        private static Texture2D _dim;
        private static bool _styles;
        private static int _builtVer = -1;

        private static GUIStyle _title;
        private static GUIStyle _hdr;
        private static GUIStyle _row;
        private static GUIStyle _back;
        private static GUIStyle _desc;

        public static void OpenFromMenu()
        {
            ModsPage.Close();
            InstantAnvilConfig.Open = true;
            Overlay.ConfigOpen = true;
        }

        public static void Close()
        {
            InstantAnvilConfig.Open = false;
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
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _hdr = new GUIStyle(Theme.LabelGold)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white }
            };
            _row = new GUIStyle(Theme.Button)
            {
                fontSize = 16,
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
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter,
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

            float width = Mathf.Min(540f, Screen.width * 0.75f);
            float height = Mathf.Min(480f, Screen.height * 0.88f);
            float x = (Screen.width - width) / 2f;
            float y = (Screen.height - height) / 2f;

            GUI.Box(new Rect(x, y, width, height), GUIContent.none, Theme.Window);
            
            GUI.Label(new Rect(x, y + 16f, width, 34f), "Instant Anvil Settings", _title);

            float contentY = y + 65f;
            float contentWidth = width - 48f;
            float btnHeight = 40f;
            float btnSpacing = 48f;

            if (DrawToggleButton(new Rect(x + 24f, contentY, contentWidth, btnHeight), "Instant Tool Repair", InstantAnvilPlugin.InstantRepair))
            {
                InstantAnvilPlugin.InstantRepair = !InstantAnvilPlugin.InstantRepair;
                PlayerPrefs.SetInt("InstantAnvil_InstantRepair", InstantAnvilPlugin.InstantRepair ? 1 : 0);
                PlayerPrefs.Save();
            }

            contentY += btnSpacing;

            if (DrawToggleButton(new Rect(x + 24f, contentY, contentWidth, btnHeight), "Anvil Item Merging", InstantAnvilPlugin.AnvilMerging))
            {
                InstantAnvilPlugin.AnvilMerging = !InstantAnvilPlugin.AnvilMerging;
                PlayerPrefs.SetInt("InstantAnvil_AnvilMerging", InstantAnvilPlugin.AnvilMerging ? 1 : 0);
                PlayerPrefs.Save();
            }

            contentY += btnSpacing;

            if (DrawToggleButton(new Rect(x + 24f, contentY, contentWidth, btnHeight), "Instant Item Merging", InstantAnvilPlugin.InstantAnvilMerging, InstantAnvilPlugin.AnvilMerging))
            {
                InstantAnvilPlugin.InstantAnvilMerging = !InstantAnvilPlugin.InstantAnvilMerging;
                PlayerPrefs.SetInt("InstantAnvil_InstantAnvilMerging", InstantAnvilPlugin.InstantAnvilMerging ? 1 : 0);
                PlayerPrefs.Save();
            }

            contentY += btnSpacing;

            if (DrawToggleButton(new Rect(x + 24f, contentY, contentWidth, btnHeight), "Hammer Repairing", InstantAnvilPlugin.HammerRepairing))
            {
                InstantAnvilPlugin.HammerRepairing = !InstantAnvilPlugin.HammerRepairing;
                PlayerPrefs.SetInt("InstantAnvil_HammerRepairing", InstantAnvilPlugin.HammerRepairing ? 1 : 0);
                PlayerPrefs.Save();
            }

            contentY += btnSpacing + 6f;

            string descText = "Configure instant repairs, item merging, and hammer repairing options.";
            GUI.Label(new Rect(x + 24f, contentY, contentWidth, 34f), descText, _desc);

            Rect backRect = new Rect(x + 24f, y + height - 18f - 42f, width - 48f, 42f);
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

    [HarmonyPatch(typeof(RepairSlot))]
    public static class RepairSlotPatches
    {
        private static bool _isProcessing = false;

        [HarmonyPatch("Awake")]
        [HarmonyPostfix]
        public static void Awake_Postfix(RepairSlot __instance)
        {
            if (InstantAnvilPlugin.HammerRepairing)
            {
                HashSet<InvBaseItem> ignore = Traverse.Create(__instance).Field("ignore").GetValue<HashSet<InvBaseItem>>();
                if (ignore != null)
                {
                    ignore.Clear();
                }
            }
        }

        [HarmonyPatch("CheckConsume")]
        [HarmonyPostfix]
        public static void CheckConsume_Postfix(RepairSlot __instance, InvGameItem item)
        {
            if (_isProcessing) return;
            if (!InstantAnvilPlugin.AnvilMerging) return;
            if (InstantAnvilPlugin.AnvilMergeDetected) return;

            if (InstantAnvilPlugin.IsHammer(__instance.repairItem) && !InstantAnvilPlugin.HammerRepairing) return;

            RepairSlot.ConsumableItem currentConsume = Traverse.Create(__instance).Field("consume").GetValue<RepairSlot.ConsumableItem>();
            
            if (currentConsume == null)
            {
                InvGameItem sacrificeItem = item;
                InvGameItem targetItem = __instance.repairItem;

                if (sacrificeItem != null && targetItem != null && sacrificeItem != targetItem)
                {
                    bool isSameItem = (sacrificeItem.baseItem != null && targetItem.baseItem != null && sacrificeItem.baseItem == targetItem.baseItem) 
                                   || (sacrificeItem.name == targetItem.name);

                    if (isSameItem && targetItem.damage > 0 && sacrificeItem.count > 0)
                    {
                        RepairSlot.ConsumableItem mergeConsumable = new RepairSlot.ConsumableItem
                        {
                            name = sacrificeItem.name,
                            _minutesPerTick = 0.00 
                        };

                        Traverse.Create(__instance).Field("consume").SetValue(mergeConsumable);

                        if (InstantAnvilPlugin.InstantAnvilMerging)
                        {
                            try
                            {
                                _isProcessing = true;

                                int repairAmount = Mathf.Min(sacrificeItem.count, targetItem.damage);
                                if (repairAmount > 0)
                                {
                                    __instance.Repair(repairAmount);
                                }
                            }
                            finally
                            {
                                _isProcessing = false;
                            }
                        }
                    }
                }
            }
        }

        [HarmonyPatch("ProcessRepair")]
        [HarmonyPostfix]
        public static void ProcessRepair_Postfix(RepairSlot __instance)
        {
            if (_isProcessing) return;
            if (!InstantAnvilPlugin.InstantRepair) return;

            if (InstantAnvilPlugin.IsHammer(__instance.repairItem) && !InstantAnvilPlugin.HammerRepairing) return;

            if (__instance.repairItem != null && __instance.repairItem.damage > 0 && __instance.hammer != null && __instance.hammer.count > 0)
            {
                RepairSlot.ConsumableItem currentConsume = Traverse.Create(__instance).Field("consume").GetValue<RepairSlot.ConsumableItem>();
                if (currentConsume != null)
                {
                    try
                    {
                        _isProcessing = true;

                        int repairAmount = Mathf.Min(__instance.hammer.count, __instance.repairItem.damage);
                        if (repairAmount > 0)
                        {
                            __instance.Repair(repairAmount);
                        }
                    }
                    finally
                    {
                        _isProcessing = false;
                    }
                }
            }
        }

        [HarmonyPatch("Repair")]
        [HarmonyPostfix]
        public static void Repair_Postfix(RepairSlot __instance)
        {
            InvGameItem hammer = __instance.hammer;

            if (hammer != null && hammer.count <= 0)
            {
                if (__instance.storage != null && __instance.storage[0] != null)
                {
                    __instance.storage[0].item = null;
                }
            }
        }
    }
}