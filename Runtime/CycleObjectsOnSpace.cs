using UnityEngine;
namespace OzhogiTools
{
    public class CycleObjectsOnSpace : MonoBehaviour
    {
        [Tooltip("Объекты, которые будут переключаться по кругу.")]
        [SerializeField] private GameObject[] objects;
        private int currentIndex = -1;
        private void Start()
        {
            DeactivateAll();
            if (objects == null || objects.Length == 0) return;
            currentIndex = 0;
            if (objects[0] != null)
            {
                objects[0].SetActive(true);
            }
        }
        private void Update()
        {
            if (objects == null || objects.Length == 0) return;
            if (!SpacePressedThisFrame()) return;
            if (currentIndex >= 0 && currentIndex < objects.Length && objects[currentIndex] != null)
            {
                objects[currentIndex].SetActive(false);
            }
            currentIndex = (currentIndex + 1) % objects.Length;
            if (objects[currentIndex] != null)
            {
                objects[currentIndex].SetActive(true);
            }
        }
        private void DeactivateAll()
        {
            if (objects == null) return;

            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i] != null)
                {
                    objects[i].SetActive(false);
                }
            }
        }
        private static bool SpacePressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            // New Input System (пакет com.unity.inputsystem).
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            return keyboard != null && keyboard.spaceKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            // Input Manager (Old).
            return Input.GetKeyDown(KeyCode.Space);
#else
            return WindowsSpacePressed();
#endif
        }
#if !ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        private const int VkSpace = 0x20;
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);
        private static bool spaceWasDown;
        private static bool warnedPlatform;
        private static bool WindowsSpacePressed()
        {
            bool isWindows =
                Application.platform == RuntimePlatform.WindowsEditor ||
                Application.platform == RuntimePlatform.WindowsPlayer;
            if (!isWindows)
            {
                if (!warnedPlatform)
                {
                    warnedPlatform = true;
                    Debug.LogWarning("[OzhogiTools] CycleObjectsOnSpace: нет активной системы ввода, "
                                   + "а WinAPI-фолбэк доступен только на Windows. Клавиша Space не будет читаться.");
                }
                return false;
            }
            bool down = (GetAsyncKeyState(VkSpace) & 0x8000) != 0;
            bool pressed = down && !spaceWasDown;
            spaceWasDown = down;
            return pressed;
        }
#endif
    }
}