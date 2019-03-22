using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using UnityEngine;
using Random = System.Random;
//using Oxide.Extensions;

namespace Oxide.Plugins
{
    [Info("Loot Plus", "Iv Misticos", "2.0.0")]
    [Description("Modify loot on your server.")]
    public class LootPlus : RustPlugin
    {
        #region Variables

        public static LootPlus Ins;

        public Random Random = new Random();

        // ReSharper disable once RedundantDefaultMemberInitializer
        private bool _initialized = false;
        
        #endregion
        
        #region Configuration

        private static Configuration _config;

        private class Configuration
        {
            [JsonProperty(PropertyName = "Plugin Enabled")]
            public bool Enabled = false;
            
            [JsonProperty(PropertyName = "Loot Skins", NullValueHandling = NullValueHandling.Ignore)]
            public Dictionary<string, Dictionary<string, ulong>> Skins = null; // OLD
            
            [JsonProperty(PropertyName = "Containers", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<ContainerData> Containers = new List<ContainerData> {new ContainerData()};

            [JsonProperty(PropertyName = "Shuffle Items")]
            public bool ShuffleItems = true;

            [JsonProperty(PropertyName = "Allow Duplicate Items")]
            public bool DuplicateItems = false;

            [JsonProperty(PropertyName = "Allow Duplicate Items With Different Skins")]
            public bool DuplicateItemsDifferentSkins = true;

            [JsonProperty(PropertyName = "Debug")]
            public bool Debug = false;
        }

        private class ContainerData
        {
            [JsonProperty(PropertyName = "Entity Shortname")]
            public string Shortname = "entity.shortname";

            [JsonProperty(PropertyName = "Replace Items")]
            public bool ReplaceItems = true;

            [JsonProperty(PropertyName = "Add Items")]
            public bool AddItems = false;

            [JsonProperty(PropertyName = "Modify Items")]
            public bool ModifyItems = false;

            [JsonProperty(PropertyName = "Maximal Failures To Add An Item")]
            public int MaxRetries = 5;
            
            [JsonProperty(PropertyName = "Capacity", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<CapacityData> Capacity = new List<CapacityData> {new CapacityData()};
            
            [JsonProperty(PropertyName = "Items", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<ItemData> Items = new List<ItemData> {new ItemData()};
        }

        private class ItemData : ChanceData
        {
            [JsonProperty(PropertyName = "Item Shortname")]
            public string Shortname = "item.shortname";

            [JsonProperty(PropertyName = "Is Blueprint")]
            public bool IsBlueprint = false;

            [JsonProperty(PropertyName = "Allow Stacking")]
            public bool AllowStacking = true;

            [JsonProperty(PropertyName = "Conditions", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<ConditionData> Conditions = new List<ConditionData> {new ConditionData()};

            [JsonProperty(PropertyName = "Skins", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<SkinData> Skins = new List<SkinData> {new SkinData()};
            
            [JsonProperty(PropertyName = "Amount", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<AmountData> Amount = new List<AmountData> {new AmountData()};
        }
        
        #region Additional

        private class ConditionData : ChanceData
        {
            [JsonProperty(PropertyName = "Condition")]
            public float Condition = 100f;
        }

        private class SkinData : ChanceData
        {
            [JsonProperty(PropertyName = "Skin")]
            // ReSharper disable once RedundantDefaultMemberInitializer
            public ulong Skin = 0;
        }

        private class AmountData : ChanceData
        {
            [JsonProperty(PropertyName = "Amount")]
            public int Amount = 3;
        }

        private class CapacityData : ChanceData
        {
            [JsonProperty(PropertyName = "Capacity")]
            public int Capacity = 3;
        }

        public class ChanceData
        {
            [JsonProperty(PropertyName = "Chance")]
            // ReSharper disable once MemberCanBePrivate.Global
            public int Chance = 1;
            
            public static T Select<T>(IReadOnlyList<T> data) where T : ChanceData
            {
                // xD

                if (data == null)
                {
                    PrintDebug("Data is null");
                    return null;
                }

                if (data.Count == 0)
                {
                    PrintDebug("Data is empty");
                    return null;
                }

                var sum1 = 0;
                for (var i = 0; i < data.Count; i++)
                {
                    var entry = data[i];
                    sum1 += entry?.Chance ?? 0;
                }

                PrintDebug($"Sum: {sum1}");
                if (sum1 < 1)
                {
                    PrintDebug("Sum is less than 1");
                    return null;
                }

                var random = Ins?.Random?.Next(1, sum1 + 1); // include the sum1 number itself and exclude the 0
                if (random == null)
                {
                    PrintDebug("Random is null");
                    return null;
                }
                
                PrintDebug($"Selected random: {random}");
                
                var sum2 = 0;
                for (var i = 0; i < data.Count; i++)
                {
                    var entry = data[i];
                    sum2 += entry?.Chance ?? 0;
                    PrintDebug($"Current sum: {sum2}, random: {random}");
                    if (random <= sum2)
                        return entry;
                }
                
                return null;
            }
        }
        
        #endregion

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
            }
            catch
            {
                Config.WriteObject(_config, false, $"{Interface.Oxide.ConfigDirectory}/{Name}.jsonError");
                PrintError("The configuration file contains an error and has been replaced with a default config.\n" +
                           "The error configuration file was saved in the .jsonError extension");
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig() => _config = new Configuration();

        protected override void SaveConfig() => Config.WriteObject(_config);

        #endregion
        
        #region Hooks

        private void Init()
        {
            Ins = this;
            new GameObject().AddComponent<LootPlusController>();
        }

        private void OnServerInitialized()
        {
            Ins = this;

            if (_config.Skins != null)
            {
                foreach (var kvp in _config.Skins)
                {
                    var container = kvp.Key;
                    var dataContainer = new ContainerData
                    {
                        Shortname = container,
                        Items = new List<ItemData>()
                    };
                    
                    foreach (var item in kvp.Value)
                    {
                        var shortname = item.Key;
                        var skin = item.Value;

                        var dataItem = new ItemData
                        {
                            Shortname = shortname,
                            Skins = new List<SkinData>
                            {
                                new SkinData
                                {
                                    Skin = skin
                                }
                            }
                        };
                        
                        dataContainer.Items.Add(dataItem);
                    }
                    
                    _config.Containers.Add(dataContainer);
                }
            }

            if (!_config.Enabled)
            {
                PrintWarning("WARNING! Plugin is disabled in configuration");
                Interface.Oxide.UnloadPlugin(Name);
                return;
            }

            _initialized = true;

            NextFrame(() =>
            {
                var containers = UnityEngine.Object.FindObjectsOfType<LootContainer>();
                var containersCount = containers.Length;
                for (var i = 0; i < containersCount; i++)
                {
                    var container = containers[i];
                    LootPlusController.Instance.StartCoroutine(LootHandler(container));
                }
            });
        }

        private void Unload()
        {
            UnityEngine.Object.Destroy(LootPlusController.Instance);
            
            // LOOT IS BACK
            var containers = UnityEngine.Object.FindObjectsOfType<LootContainer>();
            var containersCount = containers.Length;
            for (var i = 0; i < containersCount; i++)
            {
                var container = containers[i];
                container.CreateInventory(true);
                container.SpawnLoot();
            }
        }

        private void OnLootSpawn(StorageContainer container)
        {
            if (!_initialized)
                return;
            
            NextFrame(() => LootPlusController.Instance.StartCoroutine(LootHandler(container)));
        }

        #endregion
        
        #region Controller
        
        private class LootPlusController : FacepunchBehaviour
        {
            public static LootPlusController Instance;

            private void Awake()
            {
                if (Instance != null)
                    Destroy(Instance.gameObject);
                
                Instance = this;
            }
        }
        
        #endregion
        
        #region Helpers

        private IEnumerator LootHandler(StorageContainer entity)
        {
            if (entity == null)
                yield break;

            for (var i = 0; i < _config.Containers.Count; i++)
            {
                var container = _config.Containers[i];
                if (container.Shortname != entity.ShortPrefabName)
                    continue;

                HandleContainer(entity, container);
            }
        }

        private void HandleContainer(StorageContainer entity, ContainerData container)
        {
            PrintDebug(
                $"Handling container {entity.ShortPrefabName} ({entity.net.ID} @ {entity.transform.position})");

            if (_config.ShuffleItems && !container.ModifyItems) // No need to shuffle for items modification
                container.Items?.Shuffle();

            entity.inventory.capacity = entity.inventory.itemList.Count;
            HandleInventory(entity.inventory, container);
        }

        private void HandleInventory(ItemContainer inventory, ContainerData container)
        {
            var dataCapacity = ChanceData.Select(container.Capacity);
            if (dataCapacity == null)
            {
                PrintDebug("Could not select a correct capacity");
                return;
            }
            
            PrintDebug($"Items: {inventory.itemList.Count} / {inventory.capacity}");

            if (!((container.AddItems || container.ReplaceItems) ^ container.ModifyItems))
            {
                PrintWarning("Multiple options (Add / Replace / Modify) are selected");
                return;
            }

            if (container.ReplaceItems)
            {
                inventory.Clear();
                ItemManager.DoRemoves();
                inventory.capacity = dataCapacity.Capacity;
                HandleInventoryAddReplace(inventory, container);
                return;
            }
            
            if (container.AddItems)
            {
                inventory.capacity += dataCapacity.Capacity;
                HandleInventoryAddReplace(inventory, container);
                return;
            }

            if (container.ModifyItems)
            {
                HandleInventoryModify(inventory, container);
            }
        }

        private static void HandleInventoryAddReplace(ItemContainer inventory, ContainerData container)
        {
            PrintDebug("Using add or replace");
            
            var failures = 0;
            while (inventory.itemList.Count < inventory.capacity)
            {
                PrintDebug($"Count: {inventory.itemList.Count} / {inventory.capacity}");

                var dataItem = ChanceData.Select(container.Items);
                if (dataItem == null)
                {
                    PrintDebug("Could not select a correct item");
                    continue;
                }

                PrintDebug($"Handling item {dataItem.Shortname} (Blueprint: {dataItem.IsBlueprint} / Stacking: {dataItem.AllowStacking})");

                var skin = ChanceData.Select(dataItem.Skins)?.Skin ?? 0UL;

                if (!_config.DuplicateItems) // Duplicate items are not allowed
                {
                    PrintDebug("Searching for duplicates..");

                    if (IsDuplicate(inventory.itemList, dataItem, skin))
                    {
                        if (++failures > container.MaxRetries)
                        {
                            PrintDebug("Too many failures");
                            break;
                        }

                        continue;
                    }

                    PrintDebug("No duplicates");
                }

                var dataAmount = ChanceData.Select(dataItem.Amount);
                if (dataAmount == null)
                {
                    PrintDebug("Could not select a correct amount");
                    continue;
                }

                PrintDebug($"Selected amount: {dataAmount.Amount}");

                var definition =
                    ItemManager.FindItemDefinition(dataItem.IsBlueprint ? "blueprintbase" : dataItem.Shortname);
                if (definition == null)
                {
                    PrintDebug("Could not find an item definition");
                    continue;
                }

                var createdItem = ItemManager.Create(definition, dataAmount.Amount, skin);
                if (createdItem == null)
                {
                    PrintDebug("Could not create an item");
                    continue;
                }

                if (dataItem.IsBlueprint)
                {
                    createdItem.blueprintTarget = ItemManager.FindItemDefinition(dataItem.Shortname).itemid;
                }
                else
                {
                    PrintDebug("Setting up condition..");

                    var dataCondition = ChanceData.Select(dataItem.Conditions);
                    if (createdItem.hasCondition)
                    {
                        if (dataCondition == null)
                        {
                            PrintDebug("Could not select a correct condition");
                        }
                        else
                        {
                            PrintDebug($"Selected condition: {dataCondition.Condition}");
                            createdItem.condition = dataCondition.Condition;
                        }
                    }
                    else if (dataCondition != null)
                    {
                        PrintDebug("Configurated item has a condition but item doesn't have condition");
                    }
                }

                PrintDebug("Moving item to container..");

                var moved = createdItem.MoveToContainer(inventory, allowStack: dataItem.AllowStacking);
                if (moved) continue;

                PrintDebug("Could not move item to a container");
            }
        }

        private static void HandleInventoryModify(ItemContainer inventory, ContainerData container)
        {
            PrintDebug("Using modify");
            
            for (var i = 0; i < inventory.itemList.Count; i++)
            {
                var item = inventory.itemList[i];
                for (var j = 0; j < container.Items.Count; j++)
                {
                    var dataItem = container.Items[j];
                    if (dataItem.Shortname != item.info.shortname)
                        continue;

                    PrintDebug(
                        $"Handling item {dataItem.Shortname} (Blueprint: {dataItem.IsBlueprint} / Stacking: {dataItem.AllowStacking})");

                    var skin = ChanceData.Select(dataItem.Skins)?.Skin;
                    if (skin.HasValue)
                        item.skin = skin.Value;

                    var dataAmount = ChanceData.Select(dataItem.Amount);
                    if (dataAmount == null)
                    {
                        PrintDebug("Could not select a correct amount");
                        continue;
                    }

                    PrintDebug($"Selected amount: {dataAmount.Amount}");

                    item.amount = dataAmount.Amount;
                    
                    PrintDebug("Setting up condition..");

                    var dataCondition = ChanceData.Select(dataItem.Conditions);
                    if (item.hasCondition)
                    {
                        if (dataCondition == null)
                        {
                            PrintDebug("Could not select a correct condition");
                        }
                        else
                        {
                            PrintDebug($"Selected condition: {dataCondition.Condition}");
                            item.condition = dataCondition.Condition;
                        }
                    }
                    else if (dataCondition != null)
                    {
                        PrintDebug("Configurated item has a condition but item doesn't have condition");
                    }
                }
            }
        }

        private static bool IsDuplicate(IReadOnlyList<Item> list, ItemData dataItem, ulong skin)
        {
            for (var j = 0; j < list.Count; j++)
            {
                var item = list[j];
                if (dataItem.IsBlueprint)
                {
                    if (!item.IsBlueprint() || item.blueprintTargetDef.shortname != dataItem.Shortname) continue;

                    PrintDebug("Found a duplicate blueprint");
                    return true;
                }

                if (item.info.shortname != dataItem.Shortname) continue;
                if (_config.DuplicateItemsDifferentSkins && item.skin != skin)
                    continue;

                PrintDebug("Found a duplicate");
                return true;
            }

            return false;
        }

        private static void PrintDebug(string message)
        {
            if (_config.Debug)
                Interface.Oxide.LogDebug(message);
        }
        
        #endregion
    }
    
    public static class Extensions
    {
        public static void Shuffle<T>(this IList<T> list)
        {
            var count = list.Count;
            while (count > 1)
            {
                count--;
                var index = LootPlus.Ins.Random.Next(count + 1);
                var value = list[index];
                list[index] = list[count];
                list[count] = value;
            }
        }
    }
}