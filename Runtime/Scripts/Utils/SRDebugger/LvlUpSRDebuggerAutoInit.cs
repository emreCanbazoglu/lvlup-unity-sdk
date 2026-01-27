#if lvlup_srdebugger_enabled
using UnityEngine;

namespace LvlUp.Utils
{
    /// <summary>
    /// Auto-initializes LvlUp SR Debugger integration
    /// </summary>
    public class LvlUpSRDebuggerAutoInit
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            try
            {
                LvlUpSRDebuggerIntegration.EnsureInitialized();
                Debug.Log("[LvlUp] SR Debugger integration initialized");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[LvlUp] SR Debugger integration failed: {ex.Message}");
            }
        }
    }
}
#endif

