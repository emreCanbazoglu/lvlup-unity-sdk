#if lvlup_srdebugger_enabled
using UnityEngine;

namespace LvlUp.Utils
{
    /// <summary>
    /// SR Debugger integration for LvlUp mobile debugging
    /// Registers debug options in SR Debugger menu
    /// </summary>
    public class LvlUpSRDebuggerIntegration : MonoBehaviour
    {
        private static LvlUpSRDebuggerIntegration _instance;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            RegisterSRDebuggerMenu();
        }

        private void RegisterSRDebuggerMenu()
        {
            try
            {
                // Register LvlUp options with SR Debugger using the correct API
                var debugService = SRDebug.Instance;
                if (debugService != null)
                {
                    debugService.AddOptionContainer(new LvlUpDebugContainer());
                    Debug.Log("[LvlUp] SR Debugger integration successfully registered");
                }
                else
                {
                    Debug.LogWarning("[LvlUp] SRDebug instance not available");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[LvlUp] Failed to register SR Debugger integration: {ex.Message}");
            }
        }

        public static void EnsureInitialized()
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("LvlUpSRDebuggerIntegration");
                go.AddComponent<LvlUpSRDebuggerIntegration>();
            }
        }
    }
}
#endif

