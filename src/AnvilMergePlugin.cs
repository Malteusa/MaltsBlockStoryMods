using System;
using BepInEx;
using BlockStoryCore;
using HarmonyLib;
using UnityEngine;

namespace BlockStoryMod
{
    [BepInPlugin("com.malts.blockstory.anvilmerge", "AnvilMerge", "3.0.2")]
    [BepInDependency(Core.Guid)]
    public class AnvilMergePlugin : BaseUnityPlugin
    {
        private Harmony _harmony;

        private void Awake()
        {
            _harmony = new Harmony("com.malts.blockstory.anvilmerge");
            _harmony.PatchAll();

            ModRegistry.Register(new ModInfo
            {
                Name = "Anvil Merge",
                Description = "Allows merging two of the same item in the anvil to combine durability.",
                GetEnabled = () => true,
                SetEnabled = _ => { },
                HasConfig = false,
            });

            Core.Log?.LogInfo("[AnvilMerge]: Loaded successfully.");
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }
    }

    [HarmonyPatch(typeof(RepairSlot))]
    public static class RepairSlotPatches
    {
        private static bool _isMerging = false;

        [HarmonyPatch("CheckConsume")]
        [HarmonyPostfix]
        public static void CheckConsume_Postfix(RepairSlot __instance, InvGameItem item)
        {
            if (_isMerging) return;

            RepairSlot.ConsumableItem currentConsume = Traverse.Create(__instance).Field("consume").GetValue<RepairSlot.ConsumableItem>();
            if (currentConsume != null) return;

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

                    try
                    {
                        _isMerging = true;

                        int repairAmount = Mathf.Min(sacrificeItem.count, targetItem.damage);
                        if (repairAmount > 0)
                        {
                            __instance.Repair(repairAmount);
                        }
                    }
                    finally
                    {
                        _isMerging = false;
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