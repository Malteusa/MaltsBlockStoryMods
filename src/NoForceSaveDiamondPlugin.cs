using System.Reflection;
using BepInEx;
using BlockStoryCore;
using HarmonyLib;
using UnityEngine;

namespace BlockStoryMod
{
    [BepInPlugin("com.malts.blockstory.noforcesavediamond", "NoForceSaveDiamond", "1.0.0")]
    [BepInDependency(Core.Guid)]
    public class NoForceSaveDiamondPlugin : BaseUnityPlugin
    {
        private Harmony harmony;

        private void Awake()
        {
            ModRegistry.Register(new ModInfo
            {
                Name = "No Force Save on Diamond Change",
                Description = "Disables the forced saves when diamonds are changed",
                GetEnabled = () => true,
                SetEnabled = _ => { },
                HasConfig = false,
            });

            harmony = new Harmony("com.malts.blockstory.noforcesavediamond");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            Core.Log?.LogInfo("[NoForceSaveDiamond]: Loaded successfully, Game won't force a save on diamond change.");
        }

        private void OnDestroy()
        {
            harmony?.UnpatchSelf();
        }
    }
	
    [HarmonyPatch(typeof(DiamondManager), "Save")]
    public static class DisableDiamondSavePatch
    {

        [HarmonyPrefix]
        public static bool Prefix()
        {
            return false;
        }
    }
}