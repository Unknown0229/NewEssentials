using System;
using System.Collections.Generic;
using System.Linq;
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
    public class AutoAmmoService : IAutoAmmoService, IDisposable
    {
        private readonly IConfiguration m_Configuration;
        private readonly ILogger<AutoAmmoService> m_Logger;
        private readonly Dictionary<CSteamID, bool> m_AutoAmmoEnabled;
        private bool m_IsRunning;
        private UniTask m_MonitorTask;

        public AutoAmmoService(IConfiguration configuration, ILogger<AutoAmmoService> logger)
        {
            m_Configuration = configuration;
            m_Logger = logger;
            m_AutoAmmoEnabled = new Dictionary<CSteamID, bool>();
            m_IsRunning = false;
        }

        public void Start()
        {
            if (m_IsRunning) return;
            
            m_IsRunning = true;
            m_MonitorTask = MonitorAmmoAsync();
            
            m_Logger.LogInformation("Auto-ammo service started");
        }

        public void Stop()
        {
            if (!m_IsRunning) return;
            
            m_IsRunning = false;
            m_Logger.LogInformation("Auto-ammo service stopped");
        }

        public void EnableForPlayer(CSteamID steamID)
        {
            m_Logger.LogInformation($"Auto-ammo enabled for player {steamID}");
            m_AutoAmmoEnabled[steamID] = true;
        }

        public void DisableForPlayer(CSteamID steamID)
        {
            m_Logger.LogInformation($"Auto-ammo disabled for player {steamID}");
            m_AutoAmmoEnabled[steamID] = false;
        }

        public bool IsEnabledForPlayer(CSteamID steamID)
        {
            return m_AutoAmmoEnabled.ContainsKey(steamID) && m_AutoAmmoEnabled[steamID];
        }

        public void TestService()
        {
            m_Logger.LogInformation($"Auto-ammo service test - Running: {m_IsRunning}, Enabled players: {m_AutoAmmoEnabled.Count}");
            foreach (var kvp in m_AutoAmmoEnabled)
            {
                m_Logger.LogInformation($"Player {kvp.Key}: {kvp.Value}");
            }
        }

        private async UniTask MonitorAmmoAsync()
        {
            m_Logger.LogInformation("Auto-ammo monitor loop started");
            
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

                        var inventory = client.player.inventory;
                        if (inventory == null) continue;

                        // Check if ammo needs refilling
                        if (ShouldRefillAmmo(inventory))
                        {
                            m_Logger.LogInformation($"Refilling ammo for player {steamID}");
                            RefillAmmo(inventory);
                        }
                    }
                }
                catch (Exception ex)
                {
                    m_Logger.LogError(ex, "Error in auto-ammo monitor loop");
                }

                // Wait before next check
                await UniTask.Delay(GetCheckInterval());
            }
            
            m_Logger.LogInformation("Auto-ammo monitor loop stopped");
        }

        private bool ShouldRefillAmmo(PlayerInventory inventory)
        {
            if (inventory.items == null) return false;
            
            var threshold = 80; // m_Configuration.GetValue<int>("autoammo:threshold", 30);
            
            foreach (SDG.Unturned.Items itemContainer in inventory.items)
            {
                if (itemContainer == null) continue;
                
                foreach (var itemJar in itemContainer.items)
                {
                    if (itemJar.item == null) continue;
                    
                    var item = itemJar.item;
                    var itemAsset = (ItemAsset)Assets.find(EAssetType.ITEM, item.id);
                    
                    if (itemAsset == null) continue;

                    // Check if it's ammo
                    if (IsAmmoItem(itemAsset))
                    {
                        if (item.amount < itemAsset.amount)
                            return true;
                    }
                    
                    // Check if it's a magazine item
                    if (IsMagazineItem(itemAsset))
                    {
                        if (item.state != null && item.state.Length > 0)
                        {
                            int maxAmmo = itemAsset.amount;
                            byte currentAmmo = (byte)item.amount;
                            
                            int thresholdAmount = (maxAmmo * threshold) / 100;
                            
                            if (currentAmmo < thresholdAmount)
                                return true;
                        }
                        else
                        {
                            // Magazines without state data need refilling
                            return true;
                        }
                    }
                    
                    // Check if it's a gun with low ammo
                    if (IsGunItem(itemAsset))
                    {
                        int estimatedMagazineSize = 30; // Default magazine size
                        
                        var itemGunAsset = (ItemGunAsset)itemAsset;
                        if (itemGunAsset.hasBarrel)
                        {
                            estimatedMagazineSize = 50; // Guns with barrels typically have larger magazines
                        }
                        
                        if (item.state != null && item.state.Length > 0)
                        {
                            byte gunAmmo = item.state[0];
                            int gunThreshold = m_Configuration.GetValue<int>("autoammo:gunThreshold", 80);
                            int gunThresholdAmount = (estimatedMagazineSize * gunThreshold) / 100;
                            
                            if (gunAmmo < gunThresholdAmount)
                                return true;
                        }
                    }
                    
                    // Final fallback: check if any item with 0 amount might be ammo-related
                    if (item.amount == 0 && itemAsset.amount > 0)
                    {
                        string itemName = itemAsset.name.ToLower();
                        if (itemName.Contains("ammo") || itemName.Contains("mag") || itemAsset.type == EItemType.SUPPLY)
                            return true;
                    }
                }
            }
            
            return false;
        }

        private void RefillAmmo(PlayerInventory inventory)
        {
            if (inventory.items == null) return;
            
            bool refilledSomething = false;
            
            foreach (SDG.Unturned.Items itemContainer in inventory.items)
            {
                if (itemContainer == null) continue;
                
                foreach (var itemJar in itemContainer.items)
                {
                    if (itemJar.item == null) continue;
                    
                    var item = itemJar.item;
                    var itemAsset = (ItemAsset)Assets.find(EAssetType.ITEM, item.id);
                    
                    if (itemAsset == null) continue;

                    // Refill ammo items to max capacity
                    if (IsAmmoItem(itemAsset))
                    {
                        if (item.amount < itemAsset.amount)
                        {
    
                            m_Logger.LogDebug($"Refilling ammo item: {itemAsset.name} from {item.amount} to {itemAsset.amount}");
                            item.amount = itemAsset.amount;
                            inventory.sendUpdateAmount(itemContainer.page, itemJar.x, itemJar.y, item.amount);
                            refilledSomething = true;
                        }
                    }
                    
                    // CRITICAL FIX: Refill magazine ammo count, not stack size!
                    if (IsMagazineItem(itemAsset))
                    {
                        if (item.state != null && item.state.Length > 0)
                        {
                            // FIXED: Use display amount (item.amount) instead of broken state data!
                            byte currentAmmo = (byte)item.amount;  // Use 19 instead of 75
                            int maxAmmo = itemAsset.amount;
                            

                            
                            if (currentAmmo < maxAmmo)
                            {
        
                                m_Logger.LogDebug($"Refilling magazine ammo: {itemAsset.name} from {currentAmmo} to {maxAmmo}");
                                
                                // FIXED: Update BOTH the state data AND the display amount!
                                // Update both the state data AND the display amount
                                item.state[0] = (byte)maxAmmo;
                                item.amount = (byte)maxAmmo;
                                
                                inventory.sendUpdateInvState(itemContainer.page, itemJar.x, itemJar.y, item.state);
                                inventory.sendUpdateAmount(itemContainer.page, itemJar.x, itemJar.y, item.amount);
                                
                                refilledSomething = true;
                            }
                        }
                        else
                        {
                            // Create state data for the magazine
                            byte[] newState = new byte[18]; // Standard Unturned item state size
                            newState[0] = (byte)itemAsset.amount; // Set ammo to max
                            
                            // Update the item with new state
                            item.state = newState;
                            inventory.sendUpdateInvState(itemContainer.page, itemJar.x, itemJar.y, item.state);
                            refilledSomething = true;
                        }
                    }
                    
                    // Refill gun ammo
                    if (IsGunItem(itemAsset))
                    {
                        if (RefillGunAmmo(inventory, itemContainer, itemJar, item))
                        {
                            refilledSomething = true;
                        }
                    }
                    
                    // Final fallback: refill any empty ammo-related items
                    if (item.amount == 0 && itemAsset.amount > 0)
                    {
                        string itemName = itemAsset.name.ToLower();
                        if (itemName.Contains("ammo") || itemName.Contains("mag") || itemName.Contains("bullet") || 
                            itemName.Contains("shell") || itemName.Contains("round") || itemName.Contains("clip"))
                        {
                            m_Logger.LogDebug($"Refilling empty ammo item: {itemAsset.name} from 0 to {itemAsset.amount}");
                            item.amount = itemAsset.amount;
                            inventory.sendUpdateAmount(itemContainer.page, itemJar.x, itemJar.y, item.amount);
                            refilledSomething = true;
                        }
                    }
                }
            }
            
            if (refilledSomething)
            {
                m_Logger.LogDebug("Auto-ammo refill completed successfully");
            }
        }

        private bool RefillGunAmmo(PlayerInventory inventory, SDG.Unturned.Items itemContainer, ItemJar itemJar, Item item)
        {
            if (item.state == null || item.state.Length < 18) return false;
            
            var itemGunAsset = (SDG.Unturned.ItemGunAsset)Assets.find(EAssetType.ITEM, item.id);
            if (itemGunAsset == null) return false;

            // Refill current magazine - use a reasonable default if magazine size isn't available
            int magazineSize = 30; // Default magazine size
            
            // Try to get magazine size from the asset if possible
            if (itemGunAsset.hasBarrel)
            {
                // For guns with barrels, we can estimate magazine size
                magazineSize = Math.Max(20, Math.Min(100, magazineSize)); // Reasonable range
            }
            
            if (item.state[0] < magazineSize)
            {
                item.state[0] = (byte)magazineSize;
                inventory.sendUpdateInvState(itemContainer.page, itemJar.x, itemJar.y, item.state);
                return true;
            }
            return false;
        }

        private bool IsAmmoItem(ItemAsset itemAsset)
        {
            // Check item type first - SUPPLY items are typically ammo
            if (itemAsset.type == EItemType.SUPPLY)
                return true;
                 
            // Guns are NOT ammo items - they are weapons that USE ammo
            // Remove this incorrect logic:
            // if (itemAsset.type == EItemType.GUN)
            //     return true;
                 
            // Check item name for ammo-related keywords
            string itemName = itemAsset.name.ToLower();
            if (itemName.Contains("ammo") || 
                itemName.Contains("magazine") || 
                itemName.Contains("clip") ||
                itemName.Contains("bullet") ||
                itemName.Contains("shell") ||
                itemName.Contains("arrow") ||
                itemName.Contains("bolt") ||
                itemName.Contains("dart") ||
                itemName.Contains("pellet"))
                return true;
                 
            return false;
        }

        private bool IsMagazineItem(ItemAsset itemAsset)
        {
            // CRITICAL FIX: In Unturned, magazines can be SUPPLY OR MAGAZINE types!
            // Check both types to catch all magazines
            if (itemAsset.type == EItemType.SUPPLY || itemAsset.type == EItemType.MAGAZINE)
            {
                // A magazine is an item that can hold ammo (has capacity > 0)
                // This will detect ALL magazines automatically without manual name guessing!
                if (itemAsset.amount > 0)
                {
                    return true;
                }
            }
                  
            return false;
        }

        private bool IsGunItem(ItemAsset itemAsset)
        {
            return itemAsset.type == EItemType.GUN;
        }

        private bool HasLowAmmo(Item item)
        {
            if (item.state == null || item.state.Length < 18) return false;
            
            var itemGunAsset = (SDG.Unturned.ItemGunAsset)Assets.find(EAssetType.ITEM, item.id);
            if (itemGunAsset == null) return false;

            var threshold = m_Configuration.GetValue<int>("autoammo:gunThreshold", 80); // Default: refill when below 80% (more aggressive)
            
            // CRITICAL FIX: Estimate magazine size since Unturned API doesn't expose it directly
            int estimatedMagazineSize = 30; // Default magazine size
            
            // Try to estimate magazine size based on gun type
            if (itemGunAsset.hasBarrel)
            {
                estimatedMagazineSize = 50; // Guns with barrels typically have larger magazines
            }
            
            // CRITICAL FIX: For guns, we need to check if they have a magazine loaded
            // The ammo count is usually in state[1] or we need to check the actual loaded magazine
            byte currentAmmo = 0;
            
            // Try different state positions for ammo count
            if (item.state.Length > 1)
            {
                currentAmmo = item.state[1]; // Try state[1] for ammo
            }
            
            // If still 0, try state[0] as fallback
            if (currentAmmo == 0 && item.state.Length > 0)
            {
                currentAmmo = item.state[0];
            }
            
            // Calculate threshold amount
            int thresholdAmount = (estimatedMagazineSize * threshold) / 100;
            
            return currentAmmo < thresholdAmount;
        }

        private int GetCheckInterval()
        {
            return m_Configuration.GetValue<int>("autoammo:checkInterval", 5000); // Default 5 seconds to reduce spam and give time to read
        }

        public void Dispose()
        {
            Stop();
        }
    }
}

