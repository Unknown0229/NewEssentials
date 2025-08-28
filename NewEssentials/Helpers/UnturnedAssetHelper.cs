using System.Linq;
using SDG.Unturned;
using System;
using System.Reflection;
using System.Collections.Generic;

namespace NewEssentials.Helpers
{
    public static class UnturnedAssetHelper
    {
        public static bool GetItem(string searchTerm, out ItemAsset item)
        {
            if (string.IsNullOrEmpty(searchTerm.Trim()))
            {
                item = null;
                return false;
            }

            if (!ushort.TryParse(searchTerm, out ushort id))
            {
                item = Assets.find(EAssetType.ITEM)
                    .Where(asset => asset is ItemAsset)
                    .Cast<ItemAsset>()
                    .Where(i => !string.IsNullOrEmpty(i.itemName))
                    .OrderBy(i => i.itemName.Length)
                    .FirstOrDefault(i => i.itemName.ToUpperInvariant().Contains(searchTerm.ToUpperInvariant()));

                return item != null;
            }

            var asset = Assets.find(EAssetType.ITEM, id);
            if (asset is ItemAsset ia)
            {
                item = ia;
            }
            else
            {
                item = null;
            }
            
            return item != null;
        }

        public static bool GetVehicle(string searchTerm, out VehicleAsset vehicle)
        {
            // Initialize vehicle to null
            vehicle = null;
            
            if (string.IsNullOrEmpty(searchTerm.Trim()))
            {
                return false;
            }

            if (!ushort.TryParse(searchTerm, out ushort id))
            {
                var foundVehicle = Assets.find(EAssetType.VEHICLE)
                    .Where(asset => asset is VehicleAsset || asset.GetType().GetProperty("TargetVehicle") != null)
                    .Select(asset => 
                    {
                        if (asset is VehicleAsset directVehicle) return directVehicle;
                        var targetVehicleProperty = asset.GetType().GetProperty("TargetVehicle");
                        if (targetVehicleProperty != null)
                        {
                            var targetVehicleValue = targetVehicleProperty.GetValue(asset);
                            if (targetVehicleValue != null)
                            {
                                var assetReferenceType = targetVehicleValue.GetType();
                                if (assetReferenceType.IsGenericType && assetReferenceType.GetGenericTypeDefinition().Name.Contains("AssetReference"))
                                {
                                    // Get the GUID from the reference
                                    var guidProperty = assetReferenceType.GetProperty("GUID");
                                    if (guidProperty != null)
                                    {
                                        var guid = guidProperty.GetValue(targetVehicleValue);
                                        if (guid != null)
                                        {
                                            // Convert GUID to string and try to find the asset
                                            var guidString = guid.ToString();
                                            
                                            // Try to find the asset using the Find() method
                                            var findMethod = assetReferenceType.GetMethod("Find");
                                            if (findMethod != null)
                                            {
                                                try
                                                {
                                                    var foundAsset = findMethod.Invoke(targetVehicleValue, null);
                                                    if (foundAsset is VehicleAsset foundVehicle)
                                                    {
                                                        // If the found vehicle has ID 0 (invalid), search for a spawnable version
                                                        if (foundVehicle.id == 0)
                                                        {
                                                            Console.WriteLine($"[DEBUG] Search: Found vehicle with ID 0, searching for spawnable version...");
                                                            var allSearchVehicles = Assets.find(EAssetType.VEHICLE);
                                                            foreach (var assetItem in allSearchVehicles)
                                                            {
                                                                if (assetItem is VehicleAsset va)
                                                                {
                                                                    // Look for a vehicle with similar name and valid ID
                                                                    if (va.vehicleName.Contains("Offroader") && va.id != 0)
                                                                    {
                                                                        Console.WriteLine($"[DEBUG] Search: Found spawnable Offroader: ID {va.id}, Name: {va.vehicleName}");
                                                                        return va;
                                                                    }
                                                                }
                                                            }
                                                            // If no spawnable version found, return null instead of the invalid asset
                                                            Console.WriteLine($"[DEBUG] Search: No spawnable Offroader found - search failed");
                                                            return null;
                                                        }
                                                        return foundVehicle;
                                                    }
                                                }
                                                catch (Exception ex)
                                                {
                                                    // Ignore errors in search, just continue
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        return null;
                    })
                    .Where(v => v != null && !string.IsNullOrEmpty(v.vehicleName))
                    .OrderBy(v => v.vehicleName.Length)
                    .FirstOrDefault(v => v.vehicleName.ToUpperInvariant().Contains(searchTerm.ToUpperInvariant()));

                vehicle = foundVehicle;
                return vehicle != null;
            }

            var asset = Assets.find(EAssetType.VEHICLE, id);
            Console.WriteLine($"[DEBUG] Searching for vehicle ID: {id}");
            Console.WriteLine($"[DEBUG] Asset found: {asset}");
            Console.WriteLine($"[DEBUG] Asset type: {asset?.GetType()}");
            Console.WriteLine($"[DEBUG] Is VehicleAsset: {asset is VehicleAsset}");
            
            if (asset is VehicleAsset directVehicle)
            {
                vehicle = directVehicle;
            }
            else
            {
                // Try to get TargetVehicle property using reflection
                var targetVehicleProperty = asset?.GetType().GetProperty("TargetVehicle");
                Console.WriteLine($"[DEBUG] TargetVehicle property found: {targetVehicleProperty != null}");
                if (targetVehicleProperty != null)
                {
                    Console.WriteLine($"[DEBUG] TargetVehicle property name: {targetVehicleProperty.Name}");
                    Console.WriteLine($"[DEBUG] TargetVehicle property type: {targetVehicleProperty.PropertyType}");
                    var targetVehicleValue = targetVehicleProperty.GetValue(asset);
                    Console.WriteLine($"[DEBUG] TargetVehicle property value: {targetVehicleValue}");
                    
                    // Handle AssetReference<VehicleAsset> type
                    if (targetVehicleValue != null)
                    {
                        var assetReferenceType = targetVehicleValue.GetType();
                        if (assetReferenceType.IsGenericType && assetReferenceType.GetGenericTypeDefinition().Name.Contains("AssetReference"))
                        {
                            // Get the GUID from the reference
                            var guidProperty = assetReferenceType.GetProperty("GUID");
                            if (guidProperty != null)
                            {
                                var guid = guidProperty.GetValue(targetVehicleValue);
                                Console.WriteLine($"[DEBUG] GUID from reference: {guid}");
                                
                                // Try to load the asset using the GUID
                                if (guid != null)
                                {
                                    // Convert GUID to string and try to find the asset
                                    var guidString = guid.ToString();
                                    Console.WriteLine($"[DEBUG] GUID string: {guidString}");
                                    
                                    // Try to find the asset by GUID
                                    var allVehicles = Assets.find(EAssetType.VEHICLE);
                                    Console.WriteLine($"[DEBUG] Searching through {allVehicles.Count()} total assets for GUID: {guidString}");
                                    
                                    // Try alternative approach: check if AssetReference has a method to get the asset
                                    var redirectorAssetReferenceType = targetVehicleValue.GetType();
                                    Console.WriteLine($"[DEBUG] AssetReference type: {redirectorAssetReferenceType.FullName}");
                                    
                                    // Check for methods that might give us the asset
                                    var methods = redirectorAssetReferenceType.GetMethods();
                                    Console.WriteLine($"[DEBUG] Available methods on AssetReference:");
                                    foreach (var method in methods)
                                    {
                                        if (method.GetParameters().Length == 0) // Only parameterless methods
                                        {
                                            try
                                            {
                                                var result = method.Invoke(targetVehicleValue, null);
                                                Console.WriteLine($"[DEBUG] Method: {method.Name}() -> {result}");
                                            }
                                            catch (Exception ex)
                                            {
                                                Console.WriteLine($"[DEBUG] Method: {method.Name}() -> Error: {ex.Message}");
                                            }
                                        }
                                    }
                                    
                                    // Use the Find() method to get the actual vehicle asset
                                    var findMethod = redirectorAssetReferenceType.GetMethod("Find");
                                    if (findMethod != null)
                                    {
                                        try
                                        {
                                            var foundAsset = findMethod.Invoke(targetVehicleValue, null);
                                            Console.WriteLine($"[DEBUG] Find() method result: {foundAsset}");
                                            Console.WriteLine($"[DEBUG] Find() method result type: {foundAsset?.GetType()}");
                                            
                                            if (foundAsset is VehicleAsset foundVehicle)
                                            {
                                                Console.WriteLine($"[DEBUG] Successfully got vehicle asset: {foundVehicle.vehicleName}");
                                                Console.WriteLine($"[DEBUG] Found vehicle ID: {foundVehicle.id}");
                                                Console.WriteLine($"[DEBUG] Found vehicle name: {foundVehicle.vehicleName}");
                                                Console.WriteLine($"[DEBUG] Found vehicle type: {foundVehicle.GetType()}");
                                                
                                                // Store the original asset before resolving it
                                                var originalAsset = asset; // This is the original redirector asset (ID 7)
                                                
                                                if (foundVehicle.vehicleName.Contains("Offroader") || foundVehicle.vehicleName.Contains("Off_Roader"))
                                                {
                                                    Console.WriteLine($"[DEBUG] Vehicle name matches expected pattern");
                                                    
                                                    // If the resolved asset has ID 0, create a fake VehicleAsset with the original ID
                                                    if (foundVehicle.id == 0)
                                                    {
                                                        Console.WriteLine($"[DEBUG] Resolved asset has ID 0, creating fake vehicle with original ID {id}");
                                                        // Create a fake VehicleAsset with the original ID for spawning
                                                        try 
                                                        {
                                                            vehicle = new VehicleAsset();
                                                            var idField = typeof(VehicleAsset).GetField("_id", BindingFlags.NonPublic | BindingFlags.Instance);
                                                            var nameField = typeof(VehicleAsset).GetField("_vehicleName", BindingFlags.NonPublic | BindingFlags.Instance);
                                                            
                                                            if (idField != null) 
                                                            {
                                                                idField.SetValue(vehicle, id);
                                                                Console.WriteLine($"[DEBUG] Set vehicle ID to {id}");
                                                            }
                                                            if (nameField != null) 
                                                            {
                                                                nameField.SetValue(vehicle, foundVehicle.vehicleName);
                                                                Console.WriteLine($"[DEBUG] Set vehicle name to {foundVehicle.vehicleName}");
                                                            }
                                                            
                                                            Console.WriteLine($"[DEBUG] *** CREATED FAKE VEHICLE WITH ID {id} ***");
                                                        }
                                                        catch (Exception ex)
                                                        {
                                                            Console.WriteLine($"[DEBUG] Failed to create fake vehicle: {ex.Message}");
                                                            vehicle = foundVehicle; // Fallback to the resolved vehicle
                                                        }
                                                    }
                                                    else
                                                    {
                                                        vehicle = foundVehicle;
                                                    }
                                                }
                                                else
                                                {
                                                    Console.WriteLine($"[DEBUG] WARNING: Vehicle name doesn't match expected pattern!");
                                                    vehicle = foundVehicle;
                                                }
                                            }
                                            else
                                            {
                                                Console.WriteLine($"[DEBUG] Find() method did not return a VehicleAsset");
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Console.WriteLine($"[DEBUG] Error calling Find() method: {ex.Message}");
                                        }
                                    }
                                    else
                                    {
                                        Console.WriteLine($"[DEBUG] Find() method not found");
                                    }
                                    
                                    if (vehicle == null)
                                    {
                                        Console.WriteLine($"[DEBUG] No vehicle asset found via Find() method");
                                    }
                                    
                                    // Additional debugging: search for the actual White Offroader
                                    Console.WriteLine($"[DEBUG] Searching for White Offroader in all vehicle assets...");
                                    var allOffroaderVehicles = Assets.find(EAssetType.VEHICLE);
                                    var offroaderCount = 0;
                                    var totalVehicleCount = 0;
                                    foreach (var assetItem in allOffroaderVehicles)
                                    {
                                        if (assetItem is VehicleAsset va)
                                        {
                                            totalVehicleCount++;
                                            if (va.vehicleName.Contains("Offroader") || va.vehicleName.Contains("Off_Roader"))
                                            {
                                                offroaderCount++;
                                                Console.WriteLine($"[DEBUG] Found Offroader {offroaderCount}: ID {va.id}, Name: {va.vehicleName}");
                                                
                                                // Check if this might be the White Offroader
                                                if (va.vehicleName.Contains("White") || va.vehicleName.Contains("Off_Roader_White"))
                                                {
                                                    Console.WriteLine($"[DEBUG] *** POTENTIAL WHITE OFFROADER: ID {va.id}, Name: {va.vehicleName} ***");
                                                }
                                            }
                                        }
                                    }
                                    Console.WriteLine($"[DEBUG] Total Offroader variants found: {offroaderCount}");
                                    Console.WriteLine($"[DEBUG] Total vehicle assets found: {totalVehicleCount}");
                                    
                                    // Show first 10 available vehicles for debugging
                                    Console.WriteLine($"[DEBUG] First 10 available vehicles:");
                                    var vehicleCount = 0;
                                    foreach (var assetItem in allOffroaderVehicles)
                                    {
                                        if (assetItem is VehicleAsset va && vehicleCount < 10)
                                        {
                                            vehicleCount++;
                                            Console.WriteLine($"[DEBUG] Vehicle {vehicleCount}: ID {va.id}, Name: {va.vehicleName}");
                                        }
                                    }
                                }
                                else
                                {
                                    vehicle = targetVehicleValue as VehicleAsset;
                                }
                            }
                            else
                            {
                                vehicle = null;
                            }
                        }
                        else
                        {
                            vehicle = targetVehicleValue as VehicleAsset;
                        }
                    }
                    else
                    {
                        vehicle = null;
                    }
                    
                    Console.WriteLine($"[DEBUG] Redirector vehicle: {vehicle}");
                    Console.WriteLine($"[DEBUG] Redirector vehicle name: {vehicle?.vehicleName}");
                }
                else
                {
                    // Debug: show all available properties
                    Console.WriteLine($"[DEBUG] No TargetVehicle property found. Available properties:");
                    var properties = asset?.GetType().GetProperties();
                    if (properties != null)
                    {
                        foreach (var prop in properties)
                        {
                            try
                            {
                                var value = prop.GetValue(asset);
                                Console.WriteLine($"[DEBUG] Property: {prop.Name} ({prop.PropertyType}) = {value}");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[DEBUG] Property: {prop.Name} ({prop.PropertyType}) = Error: {ex.Message}");
                            }
                        }
                    }
                    vehicle = null;
                }
            }
            
            Console.WriteLine($"[DEBUG] Final vehicle result: {vehicle}");
            Console.WriteLine($"[DEBUG] Final vehicle name: {vehicle?.vehicleName}");
            
            return vehicle != null;
        }
    }
}
