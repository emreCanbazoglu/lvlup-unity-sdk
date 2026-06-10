#if lvlup_srdebugger_enabled
using System.Collections;
using SRDebugger.Services;
using SRF.Service;
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

        // How long to wait for the host app to create the SR Debugger service before giving up.
        private const float ServiceWaitTimeoutSeconds = 30f;

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
            StartCoroutine(RegisterWhenServiceAvailable());
        }

        /// <summary>
        /// Waits until the SR Debugger service has been created by the host app before registering
        /// our option container. We deliberately avoid touching <see cref="SRDebug.Instance"/> here,
        /// because accessing it auto-creates the debug service via SRServiceManager, which would force
        /// SR Debugger to spin up even when the host app never intended to enable it (e.g. for end users).
        /// Instead we poll <see cref="SRServiceManager.HasService{T}"/>, which is a non-creating check.
        /// If the service never appears within <see cref="ServiceWaitTimeoutSeconds"/>, we give up so
        /// the coroutine doesn't poll indefinitely on builds where SR Debugger is never enabled.
        /// </summary>
        private IEnumerator RegisterWhenServiceAvailable()
        {
            // Wait (without forcing creation) until the host app initializes the SR Debugger service.
            float elapsed = 0f;
            while (!SRServiceManager.HasService<IDebugService>())
            {
                if (elapsed >= ServiceWaitTimeoutSeconds)
                {
                    Debug.Log($"[LvlUp] SR Debugger service not available after {ServiceWaitTimeoutSeconds}s; " +
                              "skipping integration registration.");
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            RegisterSRDebuggerMenu();
        }

        private void RegisterSRDebuggerMenu()
        {
            try
            {
                // Safe to access SRDebug.Instance now: the service already exists, so this resolves
                // the existing instance rather than creating a new one.
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

