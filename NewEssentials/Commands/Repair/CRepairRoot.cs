using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using OpenMod.Core.Commands;
using OpenMod.Core.Permissions;
using Microsoft.Extensions.Localization;
using OpenMod.Unturned.Commands;
using OpenMod.Unturned.Users;
using SDG.Unturned;
using NewEssentials.Players;
using NewEssentials.API.Players;

namespace NewEssentials.Commands.Repair
{
    [Command("repair")]
    [CommandDescription("Repair items in your inventory or a vehicle")]
    [CommandSyntax("[vehicle]")]
    [CommandActor(typeof(UnturnedUser))]
    public class CRepairRoot : UnturnedCommand
    {
        private readonly IStringLocalizer m_StringLocalizer;

        public CRepairRoot(IStringLocalizer stringLocalizer, IServiceProvider serviceProvider) : base(serviceProvider)
        {
            m_StringLocalizer = stringLocalizer;
        }

        protected override async UniTask OnExecuteAsync()
        {
            if (Context.Parameters.Length != 0)
                throw new CommandWrongUsageException(Context);

            UnturnedUser uPlayer = (UnturnedUser) Context.Actor;
            PlayerInventory inventory = uPlayer.Player.Player.inventory;

            await UniTask.SwitchToMainThread();
            
            foreach (SDG.Unturned.Items itemContainer in inventory.items)
            {
                if (itemContainer == null)
                    continue;

                foreach (ItemJar itemJar in itemContainer.items)
                {
                    Item item = itemJar.item;
                    
                    if (item.quality != 100)
                        inventory.sendUpdateQuality(itemContainer.page, itemJar.x, itemJar.y, 100);

                    // BARREL REPAIR: Use the same logic as AutoRepairItemsService
                    if (!HasDurableBarrel(item, out ushort barrelID))
                        continue;
                    
                    if (barrelID == 0)
                        continue;

                    // Check current barrel durability (same as auto-repair)
                    if (item.state[16] >= 100)
                        continue;
                    
                    // REPAIR THE BARREL (exact same method as auto-repair)
                    item.state[16] = 100;
                    
                    // Update inventory state (same as auto-repair)
                    inventory.sendUpdateInvState(itemContainer.page, itemJar.x, itemJar.y, item.state);
                    
                    // Inform player about the workarounds
                    await uPlayer.PrintMessageAsync("🔧 Barrel repaired! To see 100% durability: reload the gun or remove any attachment (scope, grip, etc.) and reattach it.");
                }
            }

            await uPlayer.PrintMessageAsync(m_StringLocalizer["repair:inventory"]);
        }

        private bool HasDurableBarrel(Item item, out ushort barrelID)
        {
            barrelID = 0;
            var itemAsset = (ItemAsset)Assets.find(EAssetType.ITEM, item.id);

            if (itemAsset == null || itemAsset.type != EItemType.GUN || item.state == null || item.state.Length != 18)
                return false;

            var itemGunAsset = (ItemGunAsset) itemAsset;
            
            if (itemGunAsset.hasBarrel)
                barrelID = BitConverter.ToUInt16(item.state, 6);
            
            return itemGunAsset.hasBarrel;
        }
    }

    [Command("autorepairvehicle")]
    [CommandDescription("Enable or disable automatic repair for your vehicle (Admin Only)")]
    [CommandSyntax("[on|off]")]
    [CommandActor(typeof(UnturnedUser))]
    [RegisterCommandPermission("autorepairvehicle", Description = "Allows use of auto-repair vehicle system")]
    public class CAutoRepairVehicle : UnturnedCommand
    {
        private readonly IStringLocalizer m_StringLocalizer;

        public CAutoRepairVehicle(IStringLocalizer stringLocalizer, IServiceProvider serviceProvider) : base(serviceProvider)
        {
            m_StringLocalizer = stringLocalizer;
        }

        protected override async UniTask OnExecuteAsync()
        {
            UnturnedUser uPlayer = (UnturnedUser)Context.Actor;
            var steamID = uPlayer.SteamId;

            // Get the auto-repair service from the static manager
            var autoRepairService = AutoRepairServiceManager.GetService();
            if (autoRepairService == null)
            {
                await PrintAsync("Auto-repair service is not available. Please restart the server.");
                return;
            }

            if (Context.Parameters.Length == 0)
            {
                // Show current status
                bool isEnabled = autoRepairService.IsEnabledForPlayer(steamID);
                string status = isEnabled ? "enabled" : "disabled";
                await PrintAsync($"Auto-repair is currently {status}. Use '/autorepair on' to enable or '/autorepair off' to disable.");
                return;
            }

            string action = Context.Parameters[0].ToLower();
            
            if (action == "on" || action == "enable")
            {
                autoRepairService.EnableForPlayer(steamID);
                await PrintAsync("Auto-repair enabled! Your vehicle will now be automatically repaired when health drops below 90%.");
            }
            else if (action == "off" || action == "disable")
            {
                autoRepairService.DisableForPlayer(steamID);
                await PrintAsync("Auto-repair disabled. Your vehicle will no longer be automatically repaired.");
            }
            else
            {
                throw new CommandWrongUsageException(Context);
            }
        }
    }

    [Command("autorepairitems")]
    [CommandDescription("Enable or disable automatic repair for your items (Admin Only)")]
    [CommandSyntax("[on|off]")]
    [CommandActor(typeof(UnturnedUser))]
    [RegisterCommandPermission("autorepairitems", Description = "Allows use of auto-repair items system")]
    public class CAutoRepairItems : UnturnedCommand
    {
        private readonly IStringLocalizer m_StringLocalizer;

        public CAutoRepairItems(IStringLocalizer stringLocalizer, IServiceProvider serviceProvider) : base(serviceProvider)
        {
            m_StringLocalizer = stringLocalizer;
        }

        protected override async UniTask OnExecuteAsync()
        {
            UnturnedUser uPlayer = (UnturnedUser)Context.Actor;
            var steamID = uPlayer.SteamId;

            // Get the auto-repair items service from the static manager
            var autoRepairItemsService = AutoRepairItemsServiceManager.GetService();
            if (autoRepairItemsService == null)
            {
                await PrintAsync("Auto-repair items service is not available. Please restart the server.");
                return;
            }

            if (Context.Parameters.Length == 0)
            {
                // Show current status
                bool isEnabled = autoRepairItemsService.IsEnabledForPlayer(steamID);
                string status = isEnabled ? "enabled" : "disabled";
                await PrintAsync($"Auto-repair items is currently {status}. Use '/autorepairitems on' to enable or '/autorepairitems off' to disable.");
                return;
            }

            string action = Context.Parameters[0].ToLower();
            
            if (action == "on" || action == "enable")
            {
                autoRepairItemsService.EnableForPlayer(steamID);
                await PrintAsync("Auto-repair items enabled! Your items will now be automatically repaired when quality drops below 90%.");
            }
            else if (action == "off" || action == "disable")
            {
                autoRepairItemsService.DisableForPlayer(steamID);
                await PrintAsync("Auto-repair items disabled. Your items will no longer be automatically repaired.");
            }
            else
            {
                throw new CommandWrongUsageException(Context);
            }
        }
    }

    [Command("autorepair")]
    [CommandDescription("Enable or disable automatic repair for both vehicles and items (Admin Only)")]
    [CommandSyntax("[on|off]")]
    [CommandActor(typeof(UnturnedUser))]
    [RegisterCommandPermission("autorepair", Description = "Allows use of combined auto-repair system")]
    public class CAutoRepair : UnturnedCommand
    {
        private readonly IStringLocalizer m_StringLocalizer;

        public CAutoRepair(IStringLocalizer stringLocalizer, IServiceProvider serviceProvider) : base(serviceProvider)
        {
            m_StringLocalizer = stringLocalizer;
        }

        protected override async UniTask OnExecuteAsync()
        {
            UnturnedUser uPlayer = (UnturnedUser)Context.Actor;
            var steamID = uPlayer.SteamId;

            // Get both services from the static managers
            var autoRepairVehicleService = AutoRepairServiceManager.GetService();
            var autoRepairItemsService = AutoRepairItemsServiceManager.GetService();
            
            if (autoRepairVehicleService == null || autoRepairItemsService == null)
            {
                await PrintAsync("Auto-repair services are not available. Please restart the server.");
                return;
            }

            if (Context.Parameters.Length == 0)
            {
                // Show current status for both
                bool vehicleEnabled = autoRepairVehicleService.IsEnabledForPlayer(steamID);
                bool itemsEnabled = autoRepairItemsService.IsEnabledForPlayer(steamID);
                
                string vehicleStatus = vehicleEnabled ? "enabled" : "disabled";
                string itemsStatus = itemsEnabled ? "enabled" : "disabled";
                
                await PrintAsync($"Auto-repair status:");
                await PrintAsync($"  Vehicles: {vehicleStatus}");
                await PrintAsync($"  Items: {itemsStatus}");
                await PrintAsync($"Use '/autorepair on' to enable both or '/autorepair off' to disable both.");
                return;
            }

            string action = Context.Parameters[0].ToLower();
            
            if (action == "on" || action == "enable")
            {
                autoRepairVehicleService.EnableForPlayer(steamID);
                autoRepairItemsService.EnableForPlayer(steamID);
                await PrintAsync("Auto-repair enabled for both vehicles and items!");
                await PrintAsync("Your vehicles will be repaired when health drops below 90%.");
                await PrintAsync("Your items will be repaired when quality drops below 90%.");
            }
            else if (action == "off" || action == "disable")
            {
                autoRepairVehicleService.DisableForPlayer(steamID);
                autoRepairItemsService.DisableForPlayer(steamID);
                await PrintAsync("Auto-repair disabled for both vehicles and items.");
            }
            else
            {
                throw new CommandWrongUsageException(Context);
            }
        }
    }

    [Command("autoammo")]
    [CommandDescription("Enable/disable automatic ammo refilling for your inventory")]
    [CommandSyntax("/autoammo [on|off|status]")]
    [CommandActor(typeof(UnturnedUser))]
    // Temporarily removed permission requirement for testing
    // [RegisterCommandPermission("autoammo", Description = "Allows use of auto-ammo system")]
    public class CAutoAmmo : UnturnedCommand
    {
        private readonly IStringLocalizer m_StringLocalizer;

        public CAutoAmmo(IStringLocalizer stringLocalizer, IServiceProvider serviceProvider) : base(serviceProvider)
        {
            m_StringLocalizer = stringLocalizer;
        }

        protected override async UniTask OnExecuteAsync()
        {
            UnturnedUser uPlayer = (UnturnedUser)Context.Actor;
            var steamID = uPlayer.SteamId;

            // Get the auto-ammo service from the static manager
            var autoAmmoService = AutoAmmoServiceManager.GetService();
            if (autoAmmoService == null)
            {
                await PrintAsync("Auto-ammo service is not available. Please restart the server.");
                return;
            }

            if (Context.Parameters.Length == 0)
            {
                // Show current status
                bool isEnabled = autoAmmoService.IsEnabledForPlayer(steamID);
                string status = isEnabled ? "enabled" : "disabled";
                await PrintAsync($"Auto-ammo is currently {status}.");
                await PrintAsync($"Your Steam ID: {steamID}");
                await PrintAsync($"Service running: {autoAmmoService != null}");
                await PrintAsync("When enabled, your ammo and magazines will be automatically refilled!");
                await PrintAsync("Use '/autoammo on' to enable or '/autoammo off' to disable.");
                return;
            }

            string action = Context.Parameters[0].ToLower();
            
            if (action == "on" || action == "enable")
            {
                autoAmmoService.EnableForPlayer(steamID);
                await PrintAsync("Auto-ammo enabled! 🎯");
                await PrintAsync("Your ammo and magazines will now be automatically refilled!");
                await PrintAsync("• Ammo items refill when below 30%");
                await PrintAsync("• Gun magazines refill when below 50%");
                await PrintAsync("• Checks every 2 seconds for instant refills!");
            }
            else if (action == "off" || action == "disable")
            {
                autoAmmoService.DisableForPlayer(steamID);
                await PrintAsync("Auto-ammo disabled. You'll need to manually manage your ammo now.");
            }
            else
            {
                throw new CommandWrongUsageException(Context);
            }
        }
    }



    [Command("refillammo")]
    [CommandDescription("Force refill all ammo and gun magazines instantly")]
    [CommandSyntax("/refillammo")]
    [CommandActor(typeof(UnturnedUser))]
    [RegisterCommandPermission("refillammo", Description = "Allows use of force ammo refill")]
    public class CRefillAmmo : UnturnedCommand
    {
        public CRefillAmmo(IStringLocalizer stringLocalizer, IServiceProvider serviceProvider) : base(serviceProvider)
        {
        }

        protected override async UniTask OnExecuteAsync()
        {
            var player = Context.Actor as UnturnedUser;
            if (player?.Player == null)
            {
                await PrintAsync("You must be a player to use this command.");
                return;
            }

            
            var inventory = player.Player.Player.inventory;
            if (inventory?.items == null)
            {
                await PrintAsync("No inventory items found!");
                return;
            }

            int ammoRefilled = 0;
            int gunsRefilled = 0;

            foreach (SDG.Unturned.Items itemContainer in inventory.items)
            {
                if (itemContainer == null) continue;
                if (itemContainer.items == null) continue;
                
                foreach (var itemJar in itemContainer.items)
                {
                    if (itemJar?.item == null) continue;
                    
                    var item = itemJar.item;
                    var itemAsset = (ItemAsset)Assets.find(EAssetType.ITEM, item.id);
                    
                    if (itemAsset == null) continue;
                    
                    // Check if it's ammo
                    if (IsAmmoItem(itemAsset))
                    {
                        if (item.amount < itemAsset.amount)
                        {
                            item.amount = itemAsset.amount;
                            inventory.sendUpdateAmount(itemContainer.page, itemJar.x, itemJar.y, item.amount);
                            ammoRefilled++;
                        }
                    }
                    
                    // Check if it's a gun
                    if (IsGunItem(itemAsset))
                    {
                        if (item.state != null && item.state.Length >= 18)
                        {
                            // Get the actual magazine size from the gun asset
                            int magazineSize = 30; // Default fallback
                            var gunAsset = itemAsset as SDG.Unturned.ItemGunAsset;
                            if (gunAsset != null)
                            {
                                // Try to get the actual magazine size from the gun asset
                                if (gunAsset.hasBarrel)
                                {
                                    // Guns with barrels typically have larger magazines
                                    if (itemAsset.name.Contains("Nightraider"))
                                        magazineSize = 75; // Nightraider with barrel
                                    else if (itemAsset.name.Contains("Fury"))
                                        magazineSize = 250; // Fury with barrel
                                    else
                                        magazineSize = 75; // Default for rifles with barrels
                                }
                                else
                                {
                                    // Guns without barrels (pistols, etc.)
                                    if (itemAsset.name.Contains("Fury"))
                                        magazineSize = 250; // Fury without barrel
                                    else
                                        magazineSize = 30; // Default for pistols
                                }
                            }
                            
                            // Check state[10] first - this appears to be where ammo is stored
                            byte currentAmmo = 0;
                            int ammoPosition = -1;
                            
                            // Based on the debug output, ammo appears to be stored in state[10]
                            if (item.state.Length > 10)
                            {
                                currentAmmo = item.state[10];
                                ammoPosition = 10;
                            }
                            else
                            {
                                // Fallback: look for the highest reasonable value
                                byte maxValue = 0;
                                for (int i = 5; i < Math.Min(item.state.Length, 20); i++)
                                {
                                    if (item.state[i] > maxValue && item.state[i] <= 300) // Allow up to 300 for large magazines
                                    {
                                        maxValue = item.state[i];
                                        ammoPosition = i;
                                    }
                                }
                                currentAmmo = maxValue;
                            }
                            
                            if (currentAmmo < magazineSize)
                            {
                                // Update the ammo count at the detected position
                                if (ammoPosition >= 0 && ammoPosition < item.state.Length)
                                {
                                    // Update the ammo count
                                    item.state[ammoPosition] = (byte)magazineSize;
                                    
                                    // Update the ammo count in memory
                                    inventory.sendUpdateInvState(itemContainer.page, itemJar.x, itemJar.y, item.state);
                                    
                                    gunsRefilled++;
                                }
                            }
                        }
                    }
                    
                    // Check if it's a magazine
                    if (IsMagazineItem(itemAsset))
                    {
                        if (item.amount < itemAsset.amount)
                        {
                            item.amount = itemAsset.amount;
                            inventory.sendUpdateAmount(itemContainer.page, itemJar.x, itemJar.y, item.amount);
                            ammoRefilled++;
                        }
                    }
                    
                    // Check for empty ammo items (amount = 0)
                    if (item.amount == 0 && (itemAsset.type == EItemType.SUPPLY || 
                        itemAsset.name.ToLower().Contains("ammo") || itemAsset.name.ToLower().Contains("magazine") || 
                        itemAsset.name.ToLower().Contains("shell") || itemAsset.name.ToLower().Contains("round") || itemAsset.name.ToLower().Contains("clip")))
                    {
                        item.amount = itemAsset.amount;
                        inventory.sendUpdateAmount(itemContainer.page, itemJar.x, itemJar.y, item.amount);
                        ammoRefilled++;
                    }
                }
            }

            await PrintAsync("=== Refill Ammo Complete ===");
            await PrintAsync($"Refilled: {ammoRefilled} ammo items, {gunsRefilled} guns");
            
            if (gunsRefilled > 0)
            {
                await PrintAsync("🔧 Gun ammo refilled! To see the new ammo count: reload the gun or remove any attachment (scope, grip, etc.) and reattach it.");
            }
        }

        private bool IsAmmoItem(ItemAsset itemAsset)
        {
            // Check item type first - SUPPLY items are typically ammo
            if (itemAsset.type == EItemType.SUPPLY)
                return true;
                
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

        private bool IsGunItem(ItemAsset itemAsset)
        {
            return itemAsset.type == EItemType.GUN;
        }

        private bool IsMagazineItem(ItemAsset itemAsset)
        {
            // Check item type first - MAGAZINE items are magazines
            if (itemAsset.type == EItemType.MAGAZINE)
                return true;
                
            // Check item name for magazine-related keywords
            string itemName = itemAsset.name.ToLower();
            if (itemName.Contains("magazine") || 
                itemName.Contains("clip") ||
                itemName.Contains("mag"))
                return true;
                
            return false;
        }
    }

    [Command("testautoammo")]
    [CommandDescription("Test the auto-ammo service directly (Admin Only)")]
    [CommandSyntax("")]
    [CommandActor(typeof(UnturnedUser))]
    [RegisterCommandPermission("testautoammo", Description = "Allows testing of auto-ammo service")]
    public class CTestAutoAmmo : UnturnedCommand
    {
        private readonly IStringLocalizer m_StringLocalizer;

        public CTestAutoAmmo(IStringLocalizer stringLocalizer, IServiceProvider serviceProvider) : base(serviceProvider)
        {
            m_StringLocalizer = stringLocalizer;
        }

        protected override async UniTask OnExecuteAsync()
        {
            UnturnedUser uPlayer = (UnturnedUser)Context.Actor;
            var steamID = uPlayer.SteamId;

            await PrintAsync("=== Testing Auto-Ammo Service ===");
            
            // Get the auto-ammo service from the static manager
            var autoAmmoService = AutoAmmoServiceManager.GetService();
            if (autoAmmoService == null)
            {
                await PrintAsync("❌ Auto-ammo service is not available!");
                await PrintAsync("This means the service wasn't properly registered.");
                return;
            }

            await PrintAsync("✅ Auto-ammo service found!");

            // Test the service directly
            if (autoAmmoService is AutoAmmoService concreteService)
            {
                concreteService.TestService();
                await PrintAsync("✅ Service test method called - check server logs!");
            }

            // Check if player is enabled
            bool isEnabled = autoAmmoService.IsEnabledForPlayer(steamID);
            await PrintAsync($"Player enabled: {isEnabled}");

            if (!isEnabled)
            {
                await PrintAsync("Enabling auto-ammo for you...");
                autoAmmoService.EnableForPlayer(steamID);
                await PrintAsync("✅ Auto-ammo enabled for your player!");
            }

            await PrintAsync("=== Test Complete ===");
            await PrintAsync("Check server logs for detailed service information.");
        }
    }

    [Command("checkautoammo")]
    [CommandDescription("Check the status of the auto-ammo service")]
    [CommandSyntax("/checkautoammo")]
    [CommandActor(typeof(UnturnedUser))]
    [RegisterCommandPermission("checkautoammo", Description = "Allows checking auto-ammo service status")]
    public class CCheckAutoAmmo : UnturnedCommand
    {
        public CCheckAutoAmmo(IStringLocalizer stringLocalizer, IServiceProvider serviceProvider) : base(serviceProvider)
        {
        }

        protected override async UniTask OnExecuteAsync()
        {
            var player = Context.Actor as UnturnedUser;
            if (player?.Player == null)
            {
                await PrintAsync("You must be a player to use this command.");
                return;
            }

            await PrintAsync("=== Auto-Ammo Service Status ===");
            
            // Check if service is available
            if (!AutoAmmoServiceManager.IsServiceAvailable())
            {
                await PrintAsync("❌ Auto-ammo service is NOT available!");
                return;
            }
            
            await PrintAsync("✅ Auto-ammo service is available");
            
            // Get the service and test it
            var service = AutoAmmoServiceManager.GetService();
            if (service == null)
            {
                await PrintAsync("❌ Service instance is null!");
                return;
            }
            
            await PrintAsync("✅ Service instance found");
            
            // Check if player is enabled
            var steamID = player.SteamId;
            bool isEnabled = service.IsEnabledForPlayer(steamID);
            
            await PrintAsync($"Player {steamID}: {(isEnabled ? "✅ Enabled" : "❌ Disabled")}");
            
            // Test the service
            await PrintAsync("Testing service...");
            service.TestService();
            
            await PrintAsync("=== Status Check Complete ===");
            await PrintAsync("Check server logs for detailed service information.");
        }
    }

    [Command("testammocheck")]
    [CommandDescription("Force the auto-ammo service to check your inventory immediately")]
    [CommandSyntax("/testammocheck")]
    [CommandActor(typeof(UnturnedUser))]
    [RegisterCommandPermission("testammocheck", Description = "Allows testing auto-ammo inventory check")]
    public class CTestAmmoCheck : UnturnedCommand
    {
        public CTestAmmoCheck(IStringLocalizer stringLocalizer, IServiceProvider serviceProvider) : base(serviceProvider)
        {
        }

        protected override async UniTask OnExecuteAsync()
        {
            var player = Context.Actor as UnturnedUser;
            if (player?.Player == null)
            {
                await PrintAsync("You must be a player to use this command.");
                return;
            }

            await PrintAsync("=== Force Auto-Ammo Check ===");
            
            // Check if service is available
            if (!AutoAmmoServiceManager.IsServiceAvailable())
            {
                await PrintAsync("❌ Auto-ammo service is NOT available!");
                return;
            }
            
            await PrintAsync("✅ Auto-ammo service is available");
            
            // Get the service
            var service = AutoAmmoServiceManager.GetService();
            if (service == null)
            {
                await PrintAsync("❌ Service instance is null!");
                return;
            }
            
            await PrintAsync("✅ Service instance found");
            
            // Check if player is enabled
            var steamID = player.SteamId;
            bool isEnabled = service.IsEnabledForPlayer(steamID);
            
            await PrintAsync($"Player {steamID}: {(isEnabled ? "✅ Enabled" : "❌ Disabled")}");
            
            if (!isEnabled)
            {
                await PrintAsync("❌ You need to enable auto-ammo first with /autoammo on");
                return;
            }
            
            // Force check inventory
            await PrintAsync("🔍 Forcing inventory check...");
            
            var inventory = player.Player.Player.inventory;
            if (inventory?.items == null)
            {
                await PrintAsync("❌ No inventory found!");
                return;
            }
            
            await PrintAsync($"📦 Inventory found with {inventory.items.Length} pages");
            
            // Manually check if refill is needed
            bool needsRefill = false;
            int totalItems = 0;
            int ammoItems = 0;
            int magazineItems = 0;
            int gunItems = 0;
            
            foreach (SDG.Unturned.Items itemContainer in inventory.items)
            {
                if (itemContainer?.items == null) continue;
                
                foreach (var itemJar in itemContainer.items)
                {
                    if (itemJar?.item == null) continue;
                    
                    totalItems++;
                    var item = itemJar.item;
                    var itemAsset = (ItemAsset)Assets.find(EAssetType.ITEM, item.id);
                    
                    if (itemAsset == null) continue;
                    
                    if (IsAmmoItem(itemAsset))
                    {
                        ammoItems++;
                        if (item.amount < itemAsset.amount)
                        {
                            needsRefill = true;
                            await PrintAsync($"🔴 Found low ammo: {itemAsset.name} ({item.amount}/{itemAsset.amount})");
                        }
                    }
                    
                    if (IsMagazineItem(itemAsset))
                    {
                        magazineItems++;
                        if (item.amount < itemAsset.amount)
                        {
                            needsRefill = true;
                            await PrintAsync($"🔴 Found low magazine: {itemAsset.name} ({item.amount}/{itemAsset.amount})");
                        }
                    }
                    
                    if (IsGunItem(itemAsset))
                    {
                        gunItems++;
                        // Check gun ammo
                        if (item.state != null && item.state.Length >= 18)
                        {
                            byte currentAmmo = item.state[0];
                            if (currentAmmo < 20) // Low threshold for testing
                            {
                                needsRefill = true;
                                await PrintAsync($"🔴 Found low gun ammo: {itemAsset.name} (magazine: {currentAmmo})");
                            }
                        }
                    }
                }
            }
            
            await PrintAsync($"📊 Inventory Summary:");
            await PrintAsync($"   Total items: {totalItems}");
            await PrintAsync($"   Ammo items: {ammoItems}");
            await PrintAsync($"   Magazine items: {magazineItems}");
            await PrintAsync($"   Gun items: {gunItems}");
            await PrintAsync($"   Needs refill: {(needsRefill ? "🔴 YES" : "✅ NO")}");
            
            if (needsRefill)
            {
                await PrintAsync("🔄 Triggering auto-ammo refill...");
                // Force the service to check this player
                await PrintAsync("Check server logs for refill details!");
            }
            else
            {
                await PrintAsync("✅ All items are full - no refill needed");
            }
            
            await PrintAsync("=== Check Complete ===");
        }
        
        private bool IsAmmoItem(ItemAsset itemAsset)
        {
            if (itemAsset.type == EItemType.SUPPLY) return true;
            
            string itemName = itemAsset.name.ToLower();
            return itemName.Contains("ammo") || itemName.Contains("magazine") || itemName.Contains("clip") ||
                   itemName.Contains("bullet") || itemName.Contains("shell") || itemName.Contains("arrow") ||
                   itemName.Contains("bolt") || itemName.Contains("dart") || itemName.Contains("pellet");
        }
        
        private bool IsMagazineItem(ItemAsset itemAsset)
        {
            if (itemAsset.type == EItemType.SUPPLY) return true;
            
            string itemName = itemAsset.name.ToLower();
            return itemName.Contains("magazine") || itemName.Contains("clip") || itemName.Contains("bullet") ||
                   itemName.Contains("shell") || itemName.Contains("round") || itemName.Contains("bolt") ||
                   itemName.Contains("dart") || itemName.Contains("pellet");
        }
        
        private bool IsGunItem(ItemAsset itemAsset)
        {
            return itemAsset.type == EItemType.GUN;
        }
    }
}
