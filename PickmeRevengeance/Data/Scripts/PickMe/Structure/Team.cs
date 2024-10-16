﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.ModAPI;
using VRage;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using Sandbox.Common.ObjectBuilders;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using SpaceEngineers.Game.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Utils;
using VRageMath;
using ProtoBuf;

namespace PickMe.Structure
{
    class Team
    {
        public IMyFaction Faction;
        public List<Ship> Ships;
        public List<long> Players;
        public float Value = 0;
        public float Total = 0;
        Vector3D Spawn = Vector3D.Zero;
        public List<IMyPlayer> tempPlayers;
        public string Name = "";
        public List<long> processedGrids;
        List<IMyEntity> topmostEntities;
        public TeamLog ThisTeamLog;


        public Team(long factionId, Vector3D spawnCenter)
        {
            Faction = MyAPIGateway.Session.Factions.TryGetFactionById(factionId);
            Spawn = spawnCenter;
            Ships = new List<Ship>();
            Name = Faction.Name;
        }

        public void AddPlayer(long newPlayer)
        {
            if(Players == null || Players.Count == 0)
            {
                Players = new List<long>();
            }
            Players.Add(newPlayer);
        }

        public void AddGrid(Ship newGrid)
        {
            Session.Instance.networking.RelayToClients(new Networking.StatePacket("Added Grid: " + newGrid.Name));
            Ships.Add(newGrid);
            Value += newGrid.Value;
            Total += newGrid.Total;
        }

        public void Teleport()
        {
            if (Ships != null && Ships.Count > 0)
            {
                Vector3D arenaCenter = new Vector3D(/* center coordinates */); // Define the center of the arena

                int count = 0;
                foreach (var grid in Ships)
                {
                    Vector3D directionToCenter = Vector3D.Normalize(Spawn - arenaCenter);
                    Vector3D shipTangent = Vector3D.Cross(directionToCenter, Vector3D.Up); // Calculate the tangent vector
                    Vector3D shipSpawn = Spawn + shipTangent * 200 * count; // Adjust spawn position

                    MatrixD shipWorldMat = new MatrixD
                    {
                        Translation = shipSpawn,
                        Forward = shipTangent,
                        Up = Vector3D.Up
                    };

                    grid.construct.First().Teleport(shipWorldMat);

                    // Kick from neutral faction and join team faction
                    MyAPIGateway.Session.Factions.KickMember(Session.Instance.factionControl.neutralFactionID, grid.Owner);
                    MyAPIGateway.Session.Factions.SendJoinRequest(Faction.FactionId, grid.Owner);

                    tempPlayers = new List<IMyPlayer>();
                    MyAPIGateway.Players.GetPlayers(tempPlayers);
                    foreach (var player in tempPlayers)
                    {
                        if (player.IdentityId == grid.Owner)
                        {
                            Vector3D playerSpawn = shipSpawn + new Vector3D(0, 0, 200); // Adjust player spawn position
                            MatrixD playerWorldMat = new MatrixD
                            {
                                Translation = playerSpawn,
                                Forward = shipTangent,
                                Up = Vector3D.Up
                            };

                            player.Character.SetPosition(playerSpawn);
                            player.Character.Teleport(playerWorldMat);
                        }
                    }

                    tempPlayers.Clear();
                    count++;
                }
            }
            else
            {
                MyAPIGateway.Utilities.ShowNotification("No ships found");
            }
        }

        public void Recount(List<IMyPlayer> players)
        {
            //if any changes were made to the teams before start, capture them before running the match
            Ships?.Clear();
            Ships = new List<Ship>();
            Players?.Clear();
            Players = new List<long>();
            processedGrids?.Clear();
            processedGrids = new List<long>();

            Value = 0;

            BoundingSphereD arena = new BoundingSphereD(Vector3D.Zero, 200000);
            topmostEntities = MyAPIGateway.Entities.GetTopMostEntitiesInSphere(ref arena);
            foreach(var entity in topmostEntities)
            {
                if(entity is MyCubeGrid)
                {
                    MyCubeGrid thisGrid = entity as MyCubeGrid;
                    foreach (var player in players)
                        if (player.IdentityId == thisGrid.BigOwners.First())
                            player.AddGrid(thisGrid.EntityId);
                }
            }
            topmostEntities.Clear();

            Players?.Clear();
            Players = new List<long>();
            foreach (var player in players) Players.Add(player.IdentityId);

            foreach (var player in players)
            {
                if (!Players.Contains(player.IdentityId)) continue;
                if (MyAPIGateway.Session.Factions.TryGetPlayerFaction(player.IdentityId).FactionId != Faction.FactionId) continue;

                foreach (var grid in player.Grids)
                {
                    if (!processedGrids.Contains(grid))
                    {
                        MyCubeGrid newGrid = MyAPIGateway.Entities.GetEntityById(grid) as MyCubeGrid;
                        Ship newShip = new Ship(newGrid);
                        Ships.Add(newShip);
                        Value += newShip.Value;
                        foreach(var subgrid in newShip.construct)
                        {
                            processedGrids.Add(subgrid.EntityId);
                        }
                    }
                }
            }
        }

        public void Echo()
        {
            if (Ships == null)
            {
                MyAPIGateway.Utilities.ShowNotification("No Grids in Team");
                return;
            }
            if (Ships.Count == 0)
            {
                MyAPIGateway.Utilities.ShowNotification("No Grids in Team");
                return;
            }
            MyAPIGateway.Utilities.ShowNotification("Team:   " + Faction.Name);
            foreach(var grid in Ships)
            {
                MyAPIGateway.Utilities.ShowNotification(grid.Name);
            }
        }

        public void PreMatch()
        {
            ThisTeamLog = new TeamLog(this);
            ThisTeamLog.PreMatch(this);
        }

        public void PostMatch()
        {
            ThisTeamLog.PostMatch(this);
        }

        public string Log()
        {
            return ThisTeamLog.Log(this);
        }

        public void Close()
        {
            foreach (var ship in Ships) ship.Close();
            Ships?.Clear();
            Players?.Clear();
            tempPlayers?.Clear();
            processedGrids?.Clear();
            topmostEntities?.Clear();
        }
    }
}
