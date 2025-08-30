using Steamworks;
using NewEssentials.API.Players;

namespace NewEssentials.Players
{
    /// <summary>
    /// Static manager for the auto-refuel service to avoid dependency injection issues
    /// </summary>
    public static class AutoRefuelServiceManager
    {
        private static IAutoRefuelService s_Service;

        public static void SetService(IAutoRefuelService service)
        {
            s_Service = service;
        }

        public static IAutoRefuelService GetService()
        {
            return s_Service;
        }

        public static bool IsServiceAvailable()
        {
            return s_Service != null;
        }
    }
}
