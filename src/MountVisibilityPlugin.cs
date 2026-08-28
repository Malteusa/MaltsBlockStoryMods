using BepInEx;
using UnityEngine;
using System.Collections.Generic;
using BlockStoryCore;
using ISRef = UnityEngine.InputSystem.InputActionReference;

namespace BlockStoryMod
{
    [BepInPlugin("com.malts.blockstory.mountvisibility", "MountVisibility", "3.0.0")]
    [BepInDependency(Core.Guid)] 
    public class MountVisibilityPlugin : BaseUnityPlugin
    {
        public static bool Enabled = PlayerPrefs.GetInt("MountVisibility_Enabled", 1) != 0;
        private ISRef _key;
        private bool _isMountHidden;

        private GameObject _lastMounted;
        private bool _wasSuitActive;

        private readonly HashSet<GameObject> _hiddenObjects = new HashSet<GameObject>();
        private readonly HashSet<Behaviour> _hiddenUIWidgets = new HashSet<Behaviour>();

        private readonly HashSet<GameObject> _currentTargets = new HashSet<GameObject>();
        private readonly HashSet<Behaviour> _currentUIWidgets = new HashSet<Behaviour>();

        private readonly List<GameObject> _toRemoveObjects = new List<GameObject>();
        private readonly List<Behaviour> _toRemoveWidgets = new List<Behaviour>();

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

            GameObject currentMount = PlayerMounted.mounted;
            if (currentMount != _lastMounted)
            {
                _isMountHidden = false;
                _lastMounted = currentMount;
            }

            bool isSuitActive = IsAnySuitActive();
            if (!isSuitActive && _wasSuitActive)
            {
                _isMountHidden = false;
            }
            _wasSuitActive = isSuitActive;

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
            _currentTargets.Clear();
            _currentUIWidgets.Clear();

            if (_isMountHidden)
            {
                if (PlayerMounted.mounted != null)
                {
                    GameObject mount = PlayerMounted.mounted;
                    _currentTargets.Add(mount);

                    Car car = mount.GetComponent<Car>();
                    if (car != null)
                    {
                        if (car.countLabel != null)
                        {
                            _currentUIWidgets.Add(car.countLabel);
                            if (car.countLabel.transform.parent != null)
                            {
                                _currentTargets.Add(car.countLabel.transform.parent.gameObject);
                            }
                        }
                        if (car.filledBar != null)
                        {
                            _currentUIWidgets.Add(car.filledBar);
                            if (car.filledBar.transform.parent != null)
                            {
                                _currentTargets.Add(car.filledBar.transform.parent.gameObject);
                            }
                        }
                        if (car.speedLabel != null)
                        {
                            _currentUIWidgets.Add(car.speedLabel);
                            if (car.speedLabel.transform.parent != null)
                            {
                                _currentTargets.Add(car.speedLabel.transform.parent.gameObject);
                            }
                        }

                        if (car.wheels != null)
                        {
                            for (int w = 0; w < car.wheels.Length; w++)
                            {
                                if (car.wheels[w] != null) _currentTargets.Add(car.wheels[w]);
                            }
                        }
                    }
                }

                JetPackTool[] jetpacks = Object.FindObjectsByType<JetPackTool>(FindObjectsSortMode.None);
                for (int i = 0; i < jetpacks.Length; i++)
                {
                    JetPackTool jp = jetpacks[i];
                    if (jp != null && jp.enabled)
                    {
                        if (jp.model != null)
                        {
                            _currentTargets.Add(jp.model);

                            JetpackPanel panel = jp.model.GetComponentInChildren<JetpackPanel>(true);
                            if (panel != null)
                            {
                                _currentTargets.Add(panel.gameObject);
                                if (panel.countLabel != null) _currentUIWidgets.Add(panel.countLabel);
                            }
                        }
                    }
                }

                SubmarineTool[] submarines = Object.FindObjectsByType<SubmarineTool>(FindObjectsSortMode.None);
                for (int i = 0; i < submarines.Length; i++)
                {
                    SubmarineTool sub = submarines[i];
                    if (sub != null && sub.enabled)
                    {
                        if (sub.model != null) _currentTargets.Add(sub.model);
                        if (sub.coalCountObj != null) _currentTargets.Add(sub.coalCountObj);
                        if (sub.label != null) _currentUIWidgets.Add(sub.label);
                    }
                }
            }

            _toRemoveObjects.Clear();
            foreach (GameObject obj in _hiddenObjects)
            {
                if (obj == null || !_currentTargets.Contains(obj))
                {
                    if (obj != null)
                    {
                        SetRenderersAndUIVisible(obj, true);
                    }
                    _toRemoveObjects.Add(obj);
                }
            }
            for (int i = 0; i < _toRemoveObjects.Count; i++)
            {
                _hiddenObjects.Remove(_toRemoveObjects[i]);
            }

            _toRemoveWidgets.Clear();
            foreach (Behaviour widget in _hiddenUIWidgets)
            {
                if (widget == null || !_currentUIWidgets.Contains(widget))
                {
                    if (widget != null)
                    {
                        widget.enabled = true;
                    }
                    _toRemoveWidgets.Add(widget);
                }
            }
            for (int i = 0; i < _toRemoveWidgets.Count; i++)
            {
                _hiddenUIWidgets.Remove(_toRemoveWidgets[i]);
            }

            foreach (GameObject obj in _currentTargets)
            {
                if (obj != null)
                {
                    SetRenderersAndUIVisible(obj, false);
                    _hiddenObjects.Add(obj);
                }
            }

            foreach (Behaviour widget in _currentUIWidgets)
            {
                if (widget != null)
                {
                    if (widget.enabled != false)
                    {
                        widget.enabled = false;
                    }
                    _hiddenUIWidgets.Add(widget);
                }
            }
        }

        private bool IsAnySuitActive()
        {
            JetPackTool[] jetpacks = Object.FindObjectsByType<JetPackTool>(FindObjectsSortMode.None);
            for (int i = 0; i < jetpacks.Length; i++)
            {
                if (jetpacks[i] != null && jetpacks[i].enabled) return true;
            }

            SubmarineTool[] submarines = Object.FindObjectsByType<SubmarineTool>(FindObjectsSortMode.None);
            for (int i = 0; i < submarines.Length; i++)
            {
                if (submarines[i] != null && submarines[i].enabled) return true;
            }

            return false;
        }

        private static void SetRenderersAndUIVisible(GameObject obj, bool visible)
        {
            if (obj == null) return;

            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r != null && r.enabled != visible)
                {
                    r.enabled = visible;
                }
            }

            MonoBehaviour[] uiComponents = obj.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < uiComponents.Length; i++)
            {
                MonoBehaviour comp = uiComponents[i];
                if (comp == null || comp == obj.transform) continue;

                string typeName = comp.GetType().Name;
                if (typeName.Contains("UI") || typeName.Contains("Label") || typeName.Contains("Sprite") || typeName.Contains("Widget") || typeName.Contains("Panel"))
                {
                    if (comp.enabled != visible)
                    {
                        comp.enabled = visible;
                    }
                }
            }
        }
    }
}
