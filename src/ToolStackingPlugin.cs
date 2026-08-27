using System;
using BepInEx;
using HarmonyLib;
using BlockStoryCore;
using UnityEngine;

namespace BlockStoryMod
{
    [BepInPlugin("com.malts.blockstory.toolstacking", "ToolStacking", "3.0.0")]
    [BepInDependency(Core.Guid)]
    public class ToolStackingPlugin : BaseUnityPlugin
    {
        private void Awake()
        {
            ModRegistry.Register(new ModInfo
            {
                Name = "ToolStacking",
                Description = "Damaged Tools, Weapon and Armor can now stack when picked up or in the inventory.",
                GetEnabled = () => true,
                SetEnabled = _ => { },
                HasConfig = false,
            });

            Harmony harmony = new Harmony("com.malts.blockstory.toolstacking");
            harmony.PatchAll();

            Core.Log?.LogInfo("[ToolStacking]: Loaded successfully.");
        }
    }

    [HarmonyPatch(typeof(InvGameItem), "alwaysCombine", MethodType.Getter)]
    public static class InvGameItem_AlwaysCombine_Patch
    {
        static void Postfix(InvGameItem __instance, ref bool __result)
        {
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