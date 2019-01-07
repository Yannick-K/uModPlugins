using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using UnityEngine;
using Random = System.Random;

namespace Oxide.Plugins
{
    [Info("Power Spawn", "Iv Misticos", "1.0.0")]
    [Description("Control players' spawning")]
    class PowerSpawn : RustPlugin
    {
        #region Variables

        private int _worldSize;

        private Random _random = new Random();
        
        #endregion
        
        #region Configuration

        private static Configuration _config;
        
        private class Configuration
        {
            [JsonProperty(PropertyName = "Minimal Distance To Building")]
            public int DistanceBuilding = 20;

            [JsonProperty(PropertyName = "Debug")]
            public bool Debug = false;
        }
        
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

        private void OnServerInitialized()
        {
            _worldSize = ConVar.Server.worldsize;
        }

        private object OnPlayerRespawn(BasePlayer player)
        {
            var position = FindPosition();
            PrintDebug($"Found position for {player.displayName}: {position}");
            
            return new BasePlayer.SpawnPoint
            {
                pos = position
            };
        }

        #endregion
        
        #region Helpers

        private Vector3 FindPosition()
        {
            Vector3? position;
            do
            {
                position = TryFindPosition();
            } while (position == null);

            return position.Value;
        }

        private Vector3? TryFindPosition()
        {
            var position = new Vector3(GetRandomPosition(), 0, GetRandomPosition());
            var height = TerrainMeta.HeightMap.GetHeight(position);
            if (height > 0)
                position.y = height;
            else
                return null;

            var buildings = new List<BuildingBlock>();
            Vis.Entities(position, _config.DistanceBuilding, buildings);

            return buildings.Count > 0 ? (Vector3?) null : position;
        }

        private int GetRandomPosition() => _random.Next(_worldSize / -2, _worldSize / 2);

        private void PrintDebug(string message)
        {
            if (_config.Debug)
                Interface.Oxide.LogDebug($"{Name} > " + message);
        }

        #endregion
    }
}