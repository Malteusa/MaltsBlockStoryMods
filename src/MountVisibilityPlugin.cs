using BepInEx;
using UnityEngine;
using System.Reflection;
using System.Collections.Generic;
using BlockStoryCore;
using ISRef = UnityEngine.InputSystem.InputActionReference;

namespace BlockStoryMod
{
    [BepInPlugin("com.malts.blockstory.mountvisibility", "MountVisibility", "2.0.0")]
    [BepInDependency(Core.Guid)] 
    public class MountVisibilityPlugin : BaseUnityPlugin
    {
        public static bool Enabled = PlayerPrefs.GetInt("MountVisibility_Enabled", 1) != 0;
        private ISRef _key;
        private bool _isMountHidden;

        private readonly HashSet<GameObject> _hiddenObjects = new HashSet<GameObject>();

        private void Awake()
        {
            _key = BSKeybinds.Register("MountVisibility", "Hide Mount/Vehicle", "<Keyboard>/h");
            ModRegistry.Register(new ModInfo
            {
                Name = "Hide Mounts",
                Description = "Hide mounts and vehicles with a keybind",
                GetEnabled = () => Enabled,
                SetEnabled = on => 
                { 
                    Enabled = on; 
                    PlayerPrefs.SetInt("MountVisibility_Enabled", on ? 1 : 0); 
                    PlayerPrefs.Save(); 
                    if (!on) 
                    {
                        _isMountHidden = false;
                        UpdateVisibility();
                    }
                },
                HasConfig = false,
            });
        }

        private void Update()
        {
            if (!Enabled) return;

            if (BSKeybinds.Pressed(_key))
            {
                ToggleVisibility();
            }

            UpdateVisibility();
        }

        private void ToggleVisibility()
        {
            _isMountHidden = !_isMountHidden;
            UpdateVisibility();
        }

        private void UpdateVisibility()
        {
            HashSet<GameObject> currentTargets = new HashSet<GameObject>();

            if (_isMountHidden)
            {
                if (PlayerMounted.mounted != null)
                {
                    currentTargets.Add(PlayerMounted.mounted);
                }

                MonoBehaviour[] activeScripts = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
                foreach (MonoBehaviour script in activeScripts)
                {
                    if (script == null || !script.enabled) continue;

                    if (IsWearableVehicle(script))
                    {
                        GameObject modelObj = GetToolModelObject(script);
                        if (modelObj != null)
                        {
                            currentTargets.Add(modelObj);
                        }
                    }
                }
            }

            List<GameObject> toRemove = new List<GameObject>();
            foreach (GameObject obj in _hiddenObjects)
            {
                if (obj == null || !currentTargets.Contains(obj))
                {
                    if (obj != null)
                    {
                        SetRenderersVisible(obj, true);
                    }
                    toRemove.Add(obj);
                }
            }

            foreach (GameObject obj in toRemove)
            {
                _hiddenObjects.Remove(obj);
            }

            foreach (GameObject obj in currentTargets)
            {
                if (obj != null)
                {
                    SetRenderersVisible(obj, false);
                    _hiddenObjects.Add(obj);
                }
            }
        }

        private bool IsWearableVehicle(MonoBehaviour script)
        {
            if (script is JetPackTool) return true;

            string typeName = script.GetType().Name;
            return typeName.IndexOf("JetPack", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   typeName.IndexOf("Jetpack", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   typeName.IndexOf("Submarine", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   typeName.IndexOf("Diving", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private GameObject GetToolModelObject(MonoBehaviour tool)
        {
            string[] possibleFields = { "model", "modelObject", "mesh", "visual", "suit" };
            foreach (string fieldName in possibleFields)
            {
                FieldInfo field = tool.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    GameObject model = field.GetValue(tool) as GameObject;
                    if (model != null) return model;
                }
            }

            return tool.gameObject;
        }

        private void SetRenderersVisible(GameObject obj, bool visible)
        {
            if (obj == null) return;

            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in renderers)
            {
                if (r.enabled != visible)
                {
                    r.enabled = visible;
                }
            }
        }
    }
}