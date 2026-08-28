using System;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using BlockStoryCore;
using ISRef = UnityEngine.InputSystem.InputActionReference;

namespace BlockStoryMod
{
    [BepInPlugin("com.malts.blockstory.toolstacking", "ToolStacking", "3.2.0")]
    [BepInDependency(Core.Guid)]
    public class ToolStackingPlugin : BaseUnityPlugin
    {
        public static bool Enabled = PlayerPrefs.GetInt("TS_Enabled", 1) != 0;
        public static bool PickupStackEnabled = PlayerPrefs.GetInt("TS_PickupStack", 1) != 0;
        public static bool InventoryMergeEnabled = PlayerPrefs.GetInt("TS_InventoryMerge", 1) != 0;

        private ISRef _key;
        private bool _open;
        private bool _wasOpen;
        private Rect _win = new Rect(60, 60, 380, 215);
        private Harmony _harmony;

        private static GUIStyle _hdr;
        private static GUIStyle _row;
        private static GUIStyle _back;
        private static GUIStyle _desc;
        private static bool _styles;
        private static int _builtVer;

        private void Awake()
        {
            _key = BSKeybinds.Register("ToolStacking", "Open Tool Stacking Menu", "<Keyboard>/comma");

            ModRegistry.Register(new ModInfo
            {
                Name = "Tool Stacking",
                Description = "Damaged Tools, Weapon and Armor can now stack when picked up or in the inventory. Now can be toggled.",
                GetEnabled = () => Enabled,
                SetEnabled = on => 
                { 
                    Enabled = on; 
                    PlayerPrefs.SetInt("TS_Enabled", on ? 1 : 0); 
                    PlayerPrefs.Save(); 
                    if (!on) _open = false; 
                },
                HasConfig = false,
            });

            _harmony = new Harmony("com.malts.blockstory.toolstacking");
            _harmony.PatchAll();

            Core.Log?.LogInfo("[ToolStacking]: Loaded successfully.");
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
            _desc = new GUIStyle(Theme.LabelGold)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.85f, 0.85f, 0.85f, 1f) },
                wordWrap = true
            };

            _styles = true;
            _builtVer = Theme.Version;
        }

        private void OnGUI()
        {
            if (!Enabled || !_open) return;

            BuildStyles();
            _win = GUILayout.Window(GetInstanceID(), _win, DrawWindow, "Tool Stacking Settings", Theme.Window);
        }

        private void DrawWindow(int id)
        {
            string currentHover = "";

            if (DrawToggleButton("Stack on Pickup", PickupStackEnabled, "Damaged tools, weapons, and armor will automatically stack and merge when picked up.", ref currentHover))
            {
                PickupStackEnabled = !PickupStackEnabled;
                SavePref("TS_PickupStack", PickupStackEnabled);
            }

            GUILayout.Space(4);

            if (DrawToggleButton("Inventory Merging", InventoryMergeEnabled, "Allows dragging damaged tools, weapons, and armor in the inventory to merge their durability with another same item.", ref currentHover))
            {
                InventoryMergeEnabled = !InventoryMergeEnabled;
                SavePref("TS_InventoryMerge", InventoryMergeEnabled);
            }

            GUILayout.Space(6);

            string descText = string.IsNullOrEmpty(currentHover) ? "Hover over any option to see what it does." : currentHover;
            GUILayout.Label(descText, _desc, GUILayout.Height(50f));

            GUILayout.Space(6);

            if (GUILayout.Button("Close", _back, GUILayout.Height(28f)))
            {
                _open = false;
            }

            GUILayout.Space(2);
            GUI.DragWindow(new Rect(0, 0, 100000, 26));
        }

        private static bool DrawToggleButton(string label, bool state, string description, ref string currentHover)
        {
            bool prevEnabled = GUI.enabled;
            GUI.enabled = true;

            Color prevBg = GUI.backgroundColor;
            GUI.backgroundColor = state ? new Color(0.18f, 0.65f, 0.22f, 1f) : new Color(0.72f, 0.2f, 0.2f, 1f);

            string symbol = state ? "● " : "○ ";
            bool clicked = GUILayout.Button(symbol + label, _row, GUILayout.Height(26f));

            if (GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
            {
                currentHover = description;
            }

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

    [HarmonyPatch(typeof(InvGameItem), "alwaysCombine", MethodType.Getter)]
    public static class InvGameItem_AlwaysCombine_Patch
    {
        static void Postfix(InvGameItem __instance, ref bool __result)
        {
            if (!ToolStackingPlugin.Enabled || !ToolStackingPlugin.PickupStackEnabled) return;

            if (__instance != null && __instance.durability > 1)
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(InvBaseItem), "alwaysCombine", MethodType.Getter)]
    public static class InvBaseItem_AlwaysCombine_Patch
    {
        static void Postfix(InvBaseItem __instance, ref bool __result)
        {
            if (!ToolStackingPlugin.Enabled || !ToolStackingPlugin.PickupStackEnabled) return;

            if (__instance != null && __instance.durability > 1)
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(UIItemSlot), nameof(UIItemSlot.TakeItem))]
    public static class UIItemSlot_TakeItem_Patch
    {
        static bool Prefix(UIItemSlot __instance, InvGameItem item, ref bool __result)
        {
            if (!ToolStackingPlugin.Enabled || !ToolStackingPlugin.InventoryMergeEnabled) return true;

            if (item == null)
            {
                __result = false;
                return false;
            }

            if (!__instance.CanTake(item))
            {
                __result = false;
                return false;
            }

            if (__instance.IsEmpty())
            {
                __instance.Replace(item);
                __result = true;
                return false;
            }

            InvGameItem observedItem = __instance.observedItem;
            if (observedItem != null &&
                observedItem.baseItemID == item.baseItemID &&
                observedItem.data == item.data &&
                observedItem.paintData == item.paintData)
            {
                observedItem.count += item.count;
                __result = true;
                return false;
            }

            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(UIItemSlot), nameof(UIItemSlot.CanAccept))]
    public static class UIItemSlot_CanAccept_Patch
    {
        static bool Prefix(UIItemSlot __instance, InvGameItem item, ref bool __result)
        {
            if (!ToolStackingPlugin.Enabled || !ToolStackingPlugin.InventoryMergeEnabled) return true;

            if (item == null)
            {
                __result = true;
                return false;
            }

            if (__instance.observedItem != null)
            {
                InvGameItem observedItem = __instance.observedItem;
                if (observedItem.baseItemID == item.baseItemID &&
                    observedItem.data == item.data &&
                    observedItem.paintData == item.paintData)
                {
                    __result = true;
                    return false;
                }
            }
            return true;
        }
    }
}
