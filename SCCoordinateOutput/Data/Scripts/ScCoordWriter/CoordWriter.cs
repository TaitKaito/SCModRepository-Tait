﻿using Sandbox.Game;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.IO;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace YourName.ModName.Data.Scripts.ScCoordWriter
{
    internal class CoordWriter
    {
        IMyCubeGrid grid;
        TextWriter writer;
        Vector3 oldPos;
        Quaternion oldRot;
        bool isStatic; // Add isStatic as a class-level field

        // Add this property with a public setter
        public string FileName { get; private set; }

        // Add this property with a public setter
        public bool HasStartedData { get; set; }

        public CoordWriter(IMyCubeGrid grid, string fileExtension = ".scc", string factionName = "Unowned", bool isStatic = false)
        {
            this.grid = grid;
            this.isStatic = isStatic; // Initialize the class-level field
            FileName = $"{DateTime.Now:dd-MM-yyyy HHmm} , {grid.EntityId}{fileExtension}";

            try
            {
                // Use the Space Engineers modding API to open the file for writing
                writer = MyAPIGateway.Utilities.WriteFileInWorldStorage(FileName, typeof(CoordWriter));
                MyVisualScriptLogicProvider.SendChatMessage($"File created for grid {grid.CustomName}");
            }
            catch (Exception ex)
            {
                // Handle the exception (e.g., log an error message)
                MyLog.Default.WriteLine($"Error creating file for grid {grid.CustomName}: {ex.Message}");
                MyVisualScriptLogicProvider.SendChatMessage($"Error creating file for grid {grid.CustomName}: {ex.Message}");
                writer = null; // Set writer to null to prevent further writing attempts
            }

            // Now include the 'isStatic' information in the data being written
            // writer.WriteLine($"{DateTime.Now},{factionName},{grid.CustomName},{isStatic}");
            HasStartedData = false; // Initialize the flag to indicate that starting data has not been written yet
        }


        public void WriteStartingData(string factionName)
        {
            if (writer == null) return; // Check if writer is null

            string owner = "";

            if (grid.BigOwners.Count > 0)
            {
                var identities = new List<IMyIdentity>();
                MyAPIGateway.Players.GetAllIdentites(identities, id => id.IdentityId == grid.BigOwners[0]);
                if (identities.Count > 0)
                {
                    owner = identities[0].DisplayName;
                }
            }

            writer.WriteLine($"{DateTime.Now},{factionName},{grid.CustomName},{owner},{isStatic}");
            var size = Vector3I.Abs(grid.Min) + Vector3I.Abs(grid.Max);
            writer.WriteLine($"{grid.GridSize},{size.X},{size.Y},{size.Z}");
            writer.Flush();
        }

        public void WriteNextTick(int currentTick, bool isAlive, float healthPercent, Vector3D forwardDirection)
        {
            // Check if writer is null or if the grid no longer exists
            if (writer == null || grid == null || grid.MarkedForClose || !grid.InScene)
            {
                Close(); // Close the writer if the grid is no longer valid
                return;
            }

            var position = grid.GetPosition();
            var rotation = Quaternion.CreateFromForwardUp(forwardDirection, grid.WorldMatrix.Up);

            if (position == oldPos && rotation == oldRot)
                return;

            oldPos = position;
            oldRot = rotation;

            writer.WriteLine($"{currentTick},{isAlive},{Math.Round(healthPercent, 2)},{position.X},{position.Y},{position.Z},{rotation.X},{rotation.Y},{rotation.Z},{rotation.W}");
            writer.Flush();
        }

        public void Close()
        {
            if (writer != null)
            {
                writer.Flush();
                writer.Close();

                // Dispose of the writer to release its resources
                ((IDisposable)writer).Dispose();

                writer = null; // Set the writer to null to prevent further writing attempts
            }
        }
    }
}
