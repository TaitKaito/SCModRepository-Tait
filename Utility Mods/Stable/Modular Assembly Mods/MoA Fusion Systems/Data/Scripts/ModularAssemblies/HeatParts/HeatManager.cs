﻿using System.Collections.Generic;
using System.Reflection;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace FusionSystems.HeatParts
{
    internal class HeatManager
    {
        public static HeatManager I = new HeatManager();
        private readonly Dictionary<IMyCubeGrid, GridHeatManager> _heatSystems = new Dictionary<IMyCubeGrid, GridHeatManager>();

        public void Load()
        {
            I = this;
            MyAPIGateway.Entities.OnEntityAdd += OnEntityAdd;
            MyAPIGateway.Entities.OnEntityRemove += OnEntityRemove;
        }

        public void Unload()
        {
            MyAPIGateway.Entities.OnEntityAdd -= OnEntityAdd;
            MyAPIGateway.Entities.OnEntityRemove -= OnEntityRemove;
            foreach (var system in _heatSystems.Values)
                system.Unload();
            I = null;
        }

        public void UpdateTick()
        {
            foreach (var system in _heatSystems.Values)
                system.UpdateTick();
        }

        public float GetGridHeatLevel(IMyCubeGrid grid)
        {
            return _heatSystems[grid].HeatRatio;
        }

        private void OnEntityAdd(IMyEntity entity)
        {
            if (!(entity is IMyCubeGrid) || entity.Physics == null)
                return;
            var grid = (IMyCubeGrid) entity;

            _heatSystems[grid] = new GridHeatManager(grid);
        }

        private void OnEntityRemove(IMyEntity entity)
        {
            if (!(entity is IMyCubeGrid) || entity.Physics == null)
                return;
            var grid = (IMyCubeGrid) entity;

            _heatSystems[grid].Unload();
            _heatSystems.Remove(grid);
        }

        public void OnPartAdd(int assemblyId, IMyCubeBlock block, bool isBaseBlock)
        {
            _heatSystems[block.CubeGrid].OnPartAdd(assemblyId, block, isBaseBlock);
        }

        public void OnPartRemove(int assemblyId, IMyCubeBlock block, bool isBaseBlock)
        {
            _heatSystems[block.CubeGrid].OnPartRemove(assemblyId, block, isBaseBlock);
        }
    }
}
