using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;
using BlockStoryCore;
using ISRef = UnityEngine.InputSystem.InputActionReference;

namespace BlockStoryMod
{
    [BepInPlugin("com.malts.blockstory.zoomkey", "Zoom Key", "1.4.0")]
    [BepInDependency(Core.Guid)]
    public class ZoomKeyPlugin : BaseUnityPlugin
    {
        public static bool Enabled = PlayerPrefs.GetInt("ZoomKey_Enabled", 1) != 0;
        public static bool IsZooming { get; private set; }

        private ISRef _key;

        private static Camera _targetCamera;
        private static float _originalFov = -1f;
        private float _currentZoomFov = BaseZoomFov;

        private const float BaseZoomFov = 20f;
        private const float MinZoomFov = 2f;
        private const float MaxZoomFov = 60f;
        private const float ScrollStep = 4f;
        private const float ZoomSpeed = 16f;

        public static float SensitivityMultiplier
        {
            get
            {
                if (!IsZooming || _targetCamera == null || _originalFov <= 0f)
                    return 1f;

                float ratio = _targetCamera.fieldOfView / _originalFov;
                return Mathf.Clamp(ratio, 0.01f, 1f);
            }
        }

        private void Awake()
        {
            _key = BSKeybinds.Register("Zoom Key", "Zoom", "<Keyboard>/z");
            
            ModRegistry.Register(new ModInfo
            {
                Name = "Zoom Key",
                Description = "Zoom with a keybind, scroll to change the zoom. Also Supports screenshots",
                GetEnabled = () => Enabled,
                SetEnabled = on => { Enabled = on; PlayerPrefs.SetInt("ZoomKey_Enabled", on ? 1 : 0); PlayerPrefs.Save(); },
                HasConfig = false,
            });

            Harmony.CreateAndPatchAll(typeof(ZoomKeyPlugin), "com.malts.blockstory.zoomkey");

            Core.Log?.LogInfo("[Zoomkey]: Loaded successfully.");
        }

        private void Update()
        {
            if (!Enabled)
            {
                IsZooming = false;
                return;
            }

            if (_targetCamera == null)
            {
                _targetCamera = Camera.main;
                if (_targetCamera == null) return;
            }

            bool isHoldingKey = _key != null && _key.action != null && _key.action.IsPressed();

            if (Inventory.Instance != null && Inventory.Instance.isWindowOpen())
            {
                isHoldingKey = false;
            }

            IsZooming = isHoldingKey;

            if (IsZooming)
            {
                if (_originalFov < 0f)
                {
                    _originalFov = _targetCamera.fieldOfView;
                    _currentZoomFov = BaseZoomFov;
                }

                float scrollDelta = GetScrollDelta();
                if (Mathf.Abs(scrollDelta) > 0.01f)
                {
                    _currentZoomFov -= Mathf.Sign(scrollDelta) * ScrollStep;
                    _currentZoomFov = Mathf.Clamp(_currentZoomFov, MinZoomFov, MaxZoomFov);
                }

                _targetCamera.fieldOfView = Mathf.Lerp(_targetCamera.fieldOfView, _currentZoomFov, Time.deltaTime * ZoomSpeed);
            }
            else if (_originalFov > 0f)
            {
                _targetCamera.fieldOfView = Mathf.Lerp(_targetCamera.fieldOfView, _originalFov, Time.deltaTime * ZoomSpeed);

                if (Mathf.Abs(_targetCamera.fieldOfView - _originalFov) < 0.05f)
                {
                    _targetCamera.fieldOfView = _originalFov;
                    _originalFov = -1f;
                    _currentZoomFov = BaseZoomFov;
                }
            }
        }

        private float GetScrollDelta()
        {
            if (Mouse.current != null)
            {
                float val = Mouse.current.scroll.ReadValue().y;
                if (Mathf.Abs(val) > 0.01f) return val;
            }
            return Input.GetAxis("Mouse ScrollWheel");
        }

        private void OnDisable()
        {
            IsZooming = false;
            if (_targetCamera != null && _originalFov > 0f)
            {
                _targetCamera.fieldOfView = _originalFov;
                _originalFov = -1f;
            }
        }

        [HarmonyPatch(typeof(Inventory), "SetQuickSlot")]
        [HarmonyPrefix]
        private static bool PreventQuickSlotChangeOnZoom()
        {
            return !IsZooming;
        }

        [HarmonyPatch(typeof(HiResScreenShots), "MakeScreenshot")]
        [HarmonyPostfix]
        private static void ApplyZoomToScreenshot(HiResScreenShots __instance)
        {
            Camera mainCam = Camera.main;
            if (mainCam == null) return;

            Camera photoCam = Traverse.Create(__instance).Field("photoCamera").GetValue<Camera>();
            if (photoCam != null)
            {
                photoCam.fieldOfView = mainCam.fieldOfView;
            }
        }

        [HarmonyPatch(typeof(Input), nameof(Input.GetAxis))]
        [HarmonyPostfix]
        private static void ScaleGetAxisSensitivity(string axisName, ref float __result)
        {
            if (IsZooming && (axisName == "Mouse X" || axisName == "Mouse Y"))
            {
                __result *= SensitivityMultiplier;
            }
        }

        [HarmonyPatch(typeof(Input), nameof(Input.GetAxisRaw))]
        [HarmonyPostfix]
        private static void ScaleGetAxisRawSensitivity(string axisName, ref float __result)
        {
            if (IsZooming && (axisName == "Mouse X" || axisName == "Mouse Y"))
            {
                __result *= SensitivityMultiplier;
            }
        }
    }
}