using System;
using System.Collections.Generic;
using BepInEx;
using BlockStoryCore;
using HarmonyLib;
using UnityEngine;

namespace BlockStoryMod
{
    [BepInPlugin("com.malts.blockstory.doubledoors", "DoubleDoors", "1.2.0")]
    [BepInDependency(Core.Guid)]
    public class DoubleDoorsPlugin : BaseUnityPlugin
    {
        private Harmony harmony;

        private void Awake()
        {
            ModRegistry.Register(new ModInfo
            {
                Name = "Double Doors",
                Description = "Opening one door opens the other one next to it.",
                GetEnabled = () => true,
                SetEnabled = _ => { },
                HasConfig = false,
            });

            harmony = new Harmony("com.malts.blockstory.doubledoors");
            harmony.PatchAll();

            Core.Log?.LogInfo("[DoubleDoors]: Loaded successfully.");
        }

        private void OnDestroy()
        {
            harmony?.UnpatchSelf();
        }
    }

    public static class DoorTracker
    {
        public static readonly HashSet<Doors> ActiveDoors = new HashSet<Doors>();

        public static readonly AccessTools.FieldRef<Doors, int> FieldX = AccessTools.FieldRefAccess<Doors, int>("x");
        public static readonly AccessTools.FieldRef<Doors, int> FieldY = AccessTools.FieldRefAccess<Doors, int>("y");
        public static readonly AccessTools.FieldRef<Doors, int> FieldZ = AccessTools.FieldRefAccess<Doors, int>("z");
        public static readonly AccessTools.FieldRef<Doors, int> FieldFace = AccessTools.FieldRefAccess<Doors, int>("face");
        public static readonly AccessTools.FieldRef<Doors, UIButtonTween> FieldDoorButton = AccessTools.FieldRefAccess<Doors, UIButtonTween>("doorButton");
    }

    [HarmonyPatch(typeof(Doors), "Start")]
    static class Doors_Start_Patch
    {
        static void Postfix(Doors __instance)
        {
            DoorTracker.ActiveDoors.Add(__instance);
        }
    }

    [HarmonyPatch(typeof(Doors), "OnDisable")]
    static class Doors_OnDisable_Patch
    {
        static void Prefix(Doors __instance)
        {
            DoorTracker.ActiveDoors.Remove(__instance);
        }
    }

    [HarmonyPatch(typeof(UIButtonTween), "OnClick")]
    static class UIButtonTween_OnClick_Patch
    {
        private static bool isSyncing;

        static void Postfix(UIButtonTween __instance)
        {
            if (isSyncing) return;

            Doors door = __instance.GetComponentInParent<Doors>() ?? __instance.GetComponent<Doors>();
            if (door == null) return;

            isSyncing = true;
            try
            {
                Doors neighbor = FindNeighborDoor(door);
                if (neighbor != null)
                {
                    UIButtonTween neighborBtn = DoorTracker.FieldDoorButton(neighbor);
                    neighborBtn?.OnClick();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DoubleDoors]: Error syncing door: {ex}");
            }
            finally
            {
                isSyncing = false;
            }
        }

        private static Doors FindNeighborDoor(Doors source)
        {
            int x = DoorTracker.FieldX(source);
            int y = DoorTracker.FieldY(source);
            int z = DoorTracker.FieldZ(source);
            int face = DoorTracker.FieldFace(source);

            (int dx1, int dz1, int dx2, int dz2) = GetNeighborOffsets(face);

            foreach (Doors other in DoorTracker.ActiveDoors)
            {
                if (other == null || other == source) continue;

                if (DoorTracker.FieldY(other) != y) continue;
                if (DoorTracker.FieldFace(other) != face) continue;

                int ox = DoorTracker.FieldX(other);
                int oz = DoorTracker.FieldZ(other);

                if ((ox == x + dx1 && oz == z + dz1) || (ox == x + dx2 && oz == z + dz2))
                {
                    return other;
                }
            }
            return null;
        }

        private static (int dx1, int dz1, int dx2, int dz2) GetNeighborOffsets(int face)
        {
            switch (face)
            {
                case 4: return (-1, 0, 1, 0);
                case 3: return (0, -1, 0, 1);
                case 2: return (0, 1, 0, -1);
                case 5: return (1, 0, -1, 0);
                default: return (0, 0, 0, 0);
            }
        }
    }
}