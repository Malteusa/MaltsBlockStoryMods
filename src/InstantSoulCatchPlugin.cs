using System;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using BlockStoryCore;
using ISRef = UnityEngine.InputSystem.InputActionReference;

namespace BlockStoryMod
{
    [BepInPlugin("com.malts.blockstory.instantsoulcatch", "InstantSoulCatch", "2.0.0")]
    [BepInDependency(Core.Guid)]
    public class InstantSoulCatchPlugin : BaseUnityPlugin
    {
        public static bool Enabled = PlayerPrefs.GetInt("SC_Enabled", 1) != 0;
        public static bool InstantCastEnabled = PlayerPrefs.GetInt("SC_InstantCast", 1) != 0;
        public static bool FreeManaEnabled = PlayerPrefs.GetInt("SC_FreeMana", 1) != 0;

        private ISRef _key;
        private bool _open;
        private bool _wasOpen;
        private Rect _win = new Rect(60, 60, 320, 195);
        private Harmony _harmony;

        private static GUIStyle _hdr;
        private static GUIStyle _row;
        private static GUIStyle _back;
        private static bool _styles;
        private static int _builtVer;

        private void Awake()
        {
            _key = BSKeybinds.Register("InstantSoulCatch", "Open Soul Catch Menu", "<Keyboard>/i");

            ModRegistry.Register(new ModInfo
            {
                Name = "Instant Soul Catch",
                Description = "Instant mob soul catching and mana requirement toggles.",
                GetEnabled = () => Enabled,
                SetEnabled = on => 
                { 
                    Enabled = on; 
                    PlayerPrefs.SetInt("SC_Enabled", on ? 1 : 0); 
                    PlayerPrefs.Save(); 
                    if (!on) _open = false; 
                },
                HasConfig = false,
            });

            _harmony = new Harmony("com.malts.blockstory.instantsoulcatch");
            _harmony.PatchAll();

            Core.Log?.LogInfo("[InstantSoulCatch]: Loaded successfully.");
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
            _win = GUILayout.Window(GetInstanceID(), _win, DrawWindow, "Soul Catcher Tweaks", Theme.Window);
        }

        private void DrawWindow(int id)
        {
            GUILayout.Label("Soul Catcher Settings:", _hdr);
            GUILayout.Space(4);

            if (DrawToggleButton("Instant Catch", InstantCastEnabled))
            {
                InstantCastEnabled = !InstantCastEnabled;
                SavePref("SC_InstantCast", InstantCastEnabled);
            }

            GUILayout.Space(4);

            if (DrawToggleButton("No Mana Requirement", FreeManaEnabled))
            {
                FreeManaEnabled = !FreeManaEnabled;
                SavePref("SC_FreeMana", FreeManaEnabled);
            }

            GUILayout.Space(12);

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

    [HarmonyPatch(typeof(SoulCatcher))]
    public static class SoulCatcherPatches
    {
        [HarmonyPatch("Firing")]
        [HarmonyPrefix]
        public static void ModifySoulCatcherParams(SoulCatcher __instance)
        {
            if (!InstantSoulCatchPlugin.Enabled) return;

            if (InstantSoulCatchPlugin.InstantCastEnabled)
            {
                __instance.castTimeRatio = 0f;
            }
            else
            {
                __instance.castTimeRatio = 0.01f;
            }

            if (InstantSoulCatchPlugin.FreeManaEnabled)
            {
                __instance.manaCostRatio = 0f;
                __instance.needMana = 0f;
            }
            else
            {
                __instance.manaCostRatio = 0.1f;
            }
        }
    }
}