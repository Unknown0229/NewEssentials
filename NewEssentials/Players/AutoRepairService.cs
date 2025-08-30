using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NewEssentials.API.Players;
using OpenMod.API.Users;
using OpenMod.Core.Users;
using OpenMod.Unturned.Users;
using SDG.Unturned;
using Steamworks;
using UnityEngine;

namespace NewEssentials.Players
{
    public class AutoRepairService : IAutoRepairService, IDisposable
    {
        private readonly IConfiguration m_Configuration;
        private readonly ILogger<AutoRepairService> m_Logger;
        private readonly Dictionary<CSteamID, bool> m_AutoRepairEnabled;
        private bool m_IsRunning;
        private UniTask m_MonitorTask;

        public AutoRepairService(IConfiguration configuration, ILogger<AutoRepairService> logger)
        {
            m_Configuration = configuration;
            m_Logger = logger;
            m_AutoRepairEnabled = new Dictionary<CSteamID, bool>();
            m_IsRunning = false;
        }

        public void Start()
        {
            if (m_IsRunning) return;
            
            m_IsRunning = true;
            m_MonitorTask = MonitorVehiclesAsync();
            m_Logger.LogInformation("Auto-repair service started");
        }

        public void Stop()
        {
            if (!m_IsRunning) return;
            
            m_IsRunning = false;
            m_Logger.LogInformation("Auto-repair service stopped");
        }

        public void EnableForPlayer(CSteamID steamID)
        {
            m_AutoRepairEnabled[steamID] = true;
            m_Logger.LogDebug($"Auto-repair enabled for player {steamID}");
        }

        public void DisableForPlayer(CSteamID steamID)
        {
            m_AutoRepairEnabled[steamID] = false;
            m_Logger.LogDebug($"Auto-repair disabled for player {steamID}");
        }

        public bool IsEnabledForPlayer(CSteamID steamID)
        {
            return m_AutoRepairEnabled.ContainsKey(steamID) && m_AutoRepairEnabled[steamID];
        }

        private async UniTask MonitorVehiclesAsync()
        {
            while (m_IsRunning)
            {
                try
                {
                    await UniTask.SwitchToMainThread();
                    
                    var players = Provider.clients;
                    foreach (var client in players)
                    {
                        if (client?.player == null) continue;
                        
                        var steamID = client.playerID.steamID;
                        if (!IsEnabledForPlayer(steamID)) continue;

                        var vehicle = client.player.movement.getVehicle();
                        if (vehicle == null) continue;

                        // Check if vehicle needs repair
                        if (ShouldRepairVehicle(vehicle))
                        {
                            RepairVehicle(vehicle);
                            m_Logger.LogDebug($"Auto-repaired vehicle for player {steamID}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    m_Logger.LogError(ex, "Error in auto-repair monitor loop");
                }

                // Wait before next check
                await UniTask.Delay(GetCheckInterval());
            }
        }

        private bool ShouldRepairVehicle(InteractableVehicle vehicle)
        {
            if (vehicle.isExploded) return false;
            
            var threshold = m_Configuration.GetValue<int>("autorepair:threshold", 90);
            var currentHealth = vehicle.health;
            var maxHealth = vehicle.asset.health;
            var currentPercentage = (currentHealth / (float)maxHealth) * 100;
            
            return currentPercentage <= threshold;
        }

        private void RepairVehicle(InteractableVehicle vehicle)
        {
            if (vehicle.isExploded) return;
            
            vehicle.health = vehicle.asset.health;
            VehicleManager.sendVehicleHealth(vehicle, vehicle.health);
        }

        private int GetCheckInterval()
        {
            return m_Configuration.GetValue<int>("autorepair:checkInterval", 5000); // Default 5 seconds
        }

        public void Dispose()
        {
            Stop();
        }
    }
}


