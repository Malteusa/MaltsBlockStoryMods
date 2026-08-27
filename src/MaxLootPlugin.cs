using System;
using System.Collections;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using BlockStoryCore;
using Blocksters.MathLib;
using ISRef = UnityEngine.InputSystem.InputActionReference;

namespace BlockStoryMod
{
    [BepInPlugin("com.malts.blockstory.maxloot", "MaxLoot", "4.2.1")]
    [BepInDependency(Core.Guid)]
    public class MaxLootPlugin : BaseUnityPlugin
    {
        public static bool Enabled = PlayerPrefs.GetInt("LT_Enabled", 1) != 0;

        public static bool ChestLootEnabled = PlayerPrefs.GetInt("LT_ChestLoot", 1) != 0;

        public static bool MobLoot100Enabled = PlayerPrefs.GetInt("LT_MobLoot100", 1) != 0;
        public static bool MobLevelReqDisabled = PlayerPrefs.GetInt("LT_MobLevelReq", 1) != 0;
        public static bool MobDamageReqDisabled = PlayerPrefs.GetInt("LT_MobDamageReq", 1) != 0;

        public static bool MobCoinsDiamondsEnabled = PlayerPrefs.GetInt("LT_MobCoinsDiamonds", 0) != 0;
        public static bool BlockCoinsDiamondsEnabled = PlayerPrefs.GetInt("LT_BlockCoinsDiamonds", 0) != 0;

        private ISRef _key;
        private bool _open;
        private bool _wasOpen;
        private Rect _win = new Rect(60, 60, 390, 375);
        private Harmony _harmony;

        private static GUIStyle _hdr;
        private static GUIStyle _row;
        private static GUIStyle _back;
        private static GUIStyle _desc;
        private static bool _styles;
        private static int _builtVer;

        private void Awake()
        {
            _key = BSKeybinds.Register("MaxLoot", "Open Loot Menu", "<Keyboard>/backslash");

            ModRegistry.Register(new ModInfo
            {
                Name = "Loot Tweaks",
                Description = "Fully configurable loot from safeboxes, blocks and mobs.",
                GetEnabled = () => Enabled,
                SetEnabled = on => 
                { 
                    Enabled = on; 
                    PlayerPrefs.SetInt("LT_Enabled", on ? 1 : 0); 
                    PlayerPrefs.Save(); 
                    if (!on) _open = false; 
                },
                HasConfig = false,
            });

            _harmony = new Harmony("com.malts.blockstory.maxloot");
            _harmony.PatchAll();

            Core.Log?.LogInfo("[MaxLoot/LootTweaks]: Loaded successfully.");
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
            _win = GUILayout.Window(GetInstanceID(), _win, DrawWindow, "Loot Tweaks", Theme.Window);
        }

        private void DrawWindow(int id)
        {
            string currentHover = "";

            GUILayout.Label("Safebox Settings:", _hdr);
            GUILayout.Space(2);

            if (DrawToggleButton("100% Full Safeboxes", ChestLootEnabled, "All randomly generated safeboxes will have all 16 of their slots full of loot.", ref currentHover))
            {
                ChestLootEnabled = !ChestLootEnabled;
                SavePref("LT_ChestLoot", ChestLootEnabled);
            }

            GUILayout.Space(6);

            GUILayout.Label("Mob Settings:", _hdr);
            GUILayout.Space(2);

            if (DrawToggleButton("100% Mob Drops", MobLoot100Enabled, "All mob loot will always drop 100% of the time.", ref currentHover))
            {
                MobLoot100Enabled = !MobLoot100Enabled;
                SavePref("LT_MobLoot100", MobLoot100Enabled);
            }

            if (DrawToggleButton("Ignore Mob Level Requirements", MobLevelReqDisabled, "Mobs will drop their special loot regardless of the mob's level.", ref currentHover))
            {
                MobLevelReqDisabled = !MobLevelReqDisabled;
                SavePref("LT_MobLevelReq", MobLevelReqDisabled);
            }

            if (DrawToggleButton("Ignore Player Damage Requirement", MobDamageReqDisabled, "Mobs will drop loot without requiring player/pet damage, such as other NPCs or environment damage. Just like the old days.", ref currentHover))
            {
                MobDamageReqDisabled = !MobDamageReqDisabled;
                SavePref("LT_MobDamageReq", MobDamageReqDisabled);
            }

            GUILayout.Space(6);

            GUILayout.Label("Coins & Diamonds:", _hdr);
            GUILayout.Space(2);

            if (DrawToggleButton("Always Drop Coin & Diamond (Mob)", MobCoinsDiamondsEnabled, "Guarantees a coin and diamond drop on every mob kill. Including non-player kills and death if Ignore Player Damage Requirement is enabled.", ref currentHover))
            {
                MobCoinsDiamondsEnabled = !MobCoinsDiamondsEnabled;
                SavePref("LT_MobCoinsDiamonds", MobCoinsDiamondsEnabled);
            }

            if (DrawToggleButton("Always Drop Coin & Diamond (Block)", BlockCoinsDiamondsEnabled, "Guarantees a coin and diamond drop when breaking a block.", ref currentHover))
            {
                BlockCoinsDiamondsEnabled = !BlockCoinsDiamondsEnabled;
                SavePref("LT_BlockCoinsDiamonds", BlockCoinsDiamondsEnabled);
            }

            GUILayout.Space(6);

            string descText = string.IsNullOrEmpty(currentHover) ? "Hover over any option to see what it does." : currentHover;
            GUILayout.Label(descText, _desc, GUILayout.Height(45f));

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

    [HarmonyPatch]
    public static class MaxLootPatches
    {
        private static readonly AccessTools.FieldRef<Health, bool> ShouldDropLootRef = 
            AccessTools.FieldRefAccess<Health, bool>("shouldDropLoot");

        [HarmonyPatch(typeof(Health), nameof(Health.NotifyDeath))]
        [HarmonyPrefix]
        public static void ForceShouldDropLoot(Health __instance)
        {
            if (!MaxLootPlugin.Enabled || !MaxLootPlugin.MobDamageReqDisabled) return;
            if (__instance != null)
            {
                ShouldDropLootRef(__instance) = true;
            }
        }

        [HarmonyPatch(typeof(Health), nameof(Health.DropLoot))]
        [HarmonyPrefix]
        public static void GuaranteeMobLootDrops(Health __instance)
        {
            if (!MaxLootPlugin.Enabled || __instance == null) return;

            if (MaxLootPlugin.MobLoot100Enabled && __instance.loot != null)
            {
                foreach (LootDrop drop in __instance.loot)
                {
                    if (drop != null)
                    {
                        drop.probability = 2f;
                    }
                }
            }

            if (MaxLootPlugin.MobLevelReqDisabled)
            {
                __instance.specialLootMobLvl = 0;
            }
        }

        private static float _origMobInitialProb;
        private static float _origMobNormalProb;
        private static float _origMobDiamondProb;

        [HarmonyPatch(typeof(CoinLoot), nameof(CoinLoot.TryDropCoin))]
        [HarmonyPrefix]
        public static void CoinLoot_Prefix(CoinLoot __instance)
        {
            if (!MaxLootPlugin.Enabled || !MaxLootPlugin.MobCoinsDiamondsEnabled) return;
            if (__instance == null) return;

            if (__instance.coinLootInitial?.lootDrop != null)
            {
                _origMobInitialProb = __instance.coinLootInitial.lootDrop.probability;
                __instance.coinLootInitial.lootDrop.probability = 2f;
            }
            if (__instance.coinLootNormal?.lootDrop != null)
            {
                _origMobNormalProb = __instance.coinLootNormal.lootDrop.probability;
                __instance.coinLootNormal.lootDrop.probability = 2f;
            }
            if (__instance.diamondLoot?.lootDrop != null)
            {
                _origMobDiamondProb = __instance.diamondLoot.lootDrop.probability;
                __instance.diamondLoot.lootDrop.probability = 2f;
            }
        }

        [HarmonyPatch(typeof(CoinLoot), nameof(CoinLoot.TryDropCoin))]
        [HarmonyPostfix]
        public static void CoinLoot_Postfix(CoinLoot __instance)
        {
            if (!MaxLootPlugin.Enabled || !MaxLootPlugin.MobCoinsDiamondsEnabled) return;
            if (__instance == null) return;

            if (__instance.coinLootInitial?.lootDrop != null)
                __instance.coinLootInitial.lootDrop.probability = _origMobInitialProb;

            if (__instance.coinLootNormal?.lootDrop != null)
                __instance.coinLootNormal.lootDrop.probability = _origMobNormalProb;

            if (__instance.diamondLoot?.lootDrop != null)
                __instance.diamondLoot.lootDrop.probability = _origMobDiamondProb;
        }

        private static float _origBlockDiamondProb;
        private static float _origBlockCoinProb;

        [HarmonyPatch(typeof(Digger), "DestroyBlock")]
        [HarmonyPrefix]
        public static void Digger_DestroyBlock_Prefix()
        {
            if (!MaxLootPlugin.Enabled || !MaxLootPlugin.BlockCoinsDiamondsEnabled) return;

            if (Digger.dropDiamondChance?.lootDrop != null)
            {
                _origBlockDiamondProb = Digger.dropDiamondChance.lootDrop.probability;
                Digger.dropDiamondChance.lootDrop.probability = 2f;
            }

            if (Digger.dropCoinChance?.lootDrop != null)
            {
                _origBlockCoinProb = Digger.dropCoinChance.lootDrop.probability;
                Digger.dropCoinChance.lootDrop.probability = 2f;
            }
        }

        [HarmonyPatch(typeof(Digger), "DestroyBlock")]
        [HarmonyPostfix]
        public static void Digger_DestroyBlock_Postfix()
        {
            if (!MaxLootPlugin.Enabled || !MaxLootPlugin.BlockCoinsDiamondsEnabled) return;

            if (Digger.dropDiamondChance?.lootDrop != null)
            {
                Digger.dropDiamondChance.lootDrop.probability = _origBlockDiamondProb;
            }

            if (Digger.dropCoinChance?.lootDrop != null)
            {
                Digger.dropCoinChance.lootDrop.probability = _origBlockCoinProb;
            }
        }

        [HarmonyPatch(typeof(ChestStorage), "LoadWorldChest")]
        [HarmonyPrefix]
        public static bool LoadWorldChest_Prefix(ChestStorage __instance, Vector3i position)
        {
            if (!MaxLootPlugin.Enabled || !MaxLootPlugin.ChestLootEnabled) return true;

            var traverse = Traverse.Create(__instance);
            traverse.Field("loading").SetValue(true);
            traverse.Field("position").SetValue(position);

            TerrainLoader terrain = __instance.terrain;
            object rawLootContent = (terrain != null && terrain.world != null) 
                ? terrain.world.GetChestContent(position) 
                : null;

            if (rawLootContent != null)
            {
                traverse.Field("lootList").SetValue(rawLootContent);
            }

            ICollection lootCollection = rawLootContent as ICollection;

            if (lootCollection != null && lootCollection.Count > 0)
            {
                __instance.LoadChest(position);
                return false;
            }

            ChestStorage.RandomChestLoot[] randomLoots = __instance.randomLoots;

            int size = __instance.size;
            for (int i = 0; i < size; i++)
            {
                if (randomLoots == null || randomLoots.Length == 0)
                {
                    __instance[i].item = null;
                }
                else
                {
                    int index = __instance.GetRandomWeightedIndex(randomLoots);
                    if (index >= 0 && index < randomLoots.Length)
                    {
                        ChestStorage.RandomChestLoot lootDef = randomLoots[index];
                        int count = UnityEngine.Random.Range(lootDef.minCount, lootDef.maxCount + 1);

                        InvGameItem item = InvDatabase.CreateItem(lootDef.itemName, (ushort)lootDef.data, count);
                        __instance[i].item = item;
                    }
                    else
                    {
                        __instance[i].item = null;
                    }
                }
            }

            traverse.Field("loading").SetValue(false);
            traverse.Method("SaveChest").GetValue();

            return false;
        }
    }
}