﻿using Sandbox.Common.ObjectBuilders;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Network;
using VRage.Game.ObjectBuilders.ComponentSystem;
using VRage.ModAPI;
using VRage.Network;
using VRage.ObjectBuilders;
using VRage.Sync;
using VRage.Utils;
using VRageMath;
using System.Collections.Generic;
using System.Text;
using Sandbox.Definitions;
using System.Linq;

namespace Invalid.MetalFoam
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Decoy), false, "LargeDecoy_MetalFoam")]
    public class MetalFoamGenerator : MyGameLogicComponent, IMyEventProxy
    {
        private IMyCubeBlock block;
        //private const int sphereRadius = 7;
        private int nextLayerTick = 0;
        private HashSet<Vector3I> currentFoamPositions = new HashSet<Vector3I>();
        private Vector3I center;  // Added this line
        private MySync<bool, SyncDirection.BothWays> foammeup;
        static bool m_controlsCreated = false;

        private int totalFoamBlocksAdded = 0;  // Counter for the number of foam blocks added
        private int maxFoamBlocks;  // Maximum number of foam blocks allowed based on Steel Plates in its block, pretty neat. is that bad for performance? uhhhhhh


        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;

            foammeup.ValueChanged += foammeup_ValueChanged;

            block = (IMyCubeBlock)Entity;
            block.SlimBlock.ComponentStack.IsFunctionalChanged += OnBlockDamaged;

            maxFoamBlocks = CalculateMaxFoamBlocks(block);
        }

        private int CalculateMaxFoamBlocks(IMyCubeBlock block)                           // its not quite exact sometimes it cuts off 1 or 2 blocks but maybe it got converted into waste heat or something
        {                                                                                // no wait its only reading the first entry of steel plates. We'll just call the second entry (after computers) its casing or something
            var definition = block.SlimBlock.BlockDefinition as MyCubeBlockDefinition;   // lmao it generates one extra plate on the outside of the formation because of the space at the center being taken up by the decoy
            if (definition != null)
            {
                // Assuming 'SteelPlate' is the subtypeId for steel plates
                var steelPlateComponent = definition.Components.FirstOrDefault(c => c.Definition.Id.SubtypeName == "SteelPlate");
                if (steelPlateComponent != null)
                {
                    return steelPlateComponent.Count / 25;    //hmm i bet i could make a selectable dropdown that lets you choose if you want to fill in with heavy armor or light armor
                }
            }
            return 100; // Default value if steel plates are not found or block definition is null
        }

        private void foammeup_ValueChanged(MySync<bool, SyncDirection.BothWays> obj)
        {
            if (obj.Value)
            {
                StartFoamGeneration();
            }
            else
            {
                StopFoamGeneration();
            }
        }

        private void StartFoamGeneration()
        {
            center = block.Position;
            currentFoamPositions.Clear();
            AddFoamBlock("LargeBlockArmorBlock", block.Position); // Start foam at the block's position
            currentFoamPositions.Add(block.Position);
            NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;

            NotifyPlayers("Foam generation started!", MyFontEnum.Green);
            CreateEffects();
        }
        private void StopFoamGeneration()
        {
            NeedsUpdate &= ~MyEntityUpdateEnum.EACH_100TH_FRAME;
            NotifyPlayers("Foam generation stopped!", MyFontEnum.Red);
        }

        private void NotifyPlayers(string message, string font)
        {
            Vector3D position = block.GetPosition();
            NotifyPlayersInRange(message, position, 100, font);
        }

        private void CreateEffects()
        {
            Vector3D position = block.GetPosition();
            MyVisualScriptLogicProvider.CreateParticleEffectAtPosition("MetalFoamSmoke", position);
            MyVisualScriptLogicProvider.PlaySingleSoundAtPosition("MetalFoamSound", position);
        }

        private void NotifyPlayersInRange(string text, Vector3D position, double radius, string font)
        {
            BoundingSphereD bound = new BoundingSphereD(position, radius);
            List<IMyEntity> nearEntities = MyAPIGateway.Entities.GetEntitiesInSphere(ref bound);

            foreach (var entity in nearEntities)
            {
                IMyCharacter character = entity as IMyCharacter;
                if (character != null && character.IsPlayer && bound.Contains(character.GetPosition()) != ContainmentType.Disjoint)
                {
                    var notification = MyAPIGateway.Utilities.CreateNotification(text, 1600, font);
                    notification.Show();
                }
            }
        }

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();
            CreateTerminalControls();
        }

        public override void UpdateBeforeSimulation100()
        {
            if (MyAPIGateway.Session.GameplayFrameCounter >= nextLayerTick && foammeup.Value)
            {
                SpreadFoam();
                nextLayerTick = MyAPIGateway.Session.GameplayFrameCounter + 180; // Adjust timing as needed
            }
        }

        private void SpreadFoam()
        {
            var grid = block.CubeGrid;
            HashSet<Vector3I> newFoamPositions = new HashSet<Vector3I>();
            bool blockAdded = false; // Flag to track if any foam block was added

            foreach (var foamBlock in currentFoamPositions)
            {
                if (totalFoamBlocksAdded >= maxFoamBlocks)
                {
                    break; // Exit if max blocks already added
                }

                foreach (var neighbor in GetNeighboringBlocks(foamBlock))
                {
                    if (CanPlaceFoam(neighbor) && !currentFoamPositions.Contains(neighbor) && !newFoamPositions.Contains(neighbor))
                    {
                        newFoamPositions.Add(neighbor);
                        blockAdded = true;
                        totalFoamBlocksAdded++;

                        if (totalFoamBlocksAdded >= maxFoamBlocks)
                        {
                            break; // Exit if max blocks reached during this iteration
                        }
                    }
                }

                // Early exit if max blocks reached to prevent further iterations
                if (totalFoamBlocksAdded >= maxFoamBlocks)
                {
                    break;
                }
            }

            // Add foam blocks at new positions
            foreach (var newPos in newFoamPositions)
            {
                AddFoamBlock("LargeBlockArmorBlock", newPos);
            }

            // Update current foam positions
            currentFoamPositions.UnionWith(newFoamPositions);

            // Play sound effect once if a block was added this tick
            if (blockAdded)
            {
                MyVisualScriptLogicProvider.PlaySingleSoundAtPosition("MetalFoamSound", block.GetPosition());
            }
        }




        private IEnumerable<Vector3I> GetNeighboringBlocks(Vector3I position)
        {
            return new List<Vector3I>
            {
                position + new Vector3I(1, 0, 0),
                position + new Vector3I(-1, 0, 0),
                position + new Vector3I(0, 1, 0),
                position + new Vector3I(0, -1, 0),
                position + new Vector3I(0, 0, 1),
                position + new Vector3I(0, 0, -1)
            };
        }

        private bool CanPlaceFoam(Vector3I position)
        {
            var grid = block.CubeGrid;
            var blockAtPosition = grid.GetCubeBlock(position);

            // Foam can be placed if the position is an empty block space (no block present)
            return blockAtPosition == null;
        }

        private void AddFoamBlock(string subtypeName, Vector3I position)
        {
            var grid = block.CubeGrid; // Get the grid the block is part of

            var armorBlockBuilder = new MyObjectBuilder_CubeBlock
            {
                SubtypeName = subtypeName, // Foam block subtype
                Min = position, // Position where the block will be placed
                ColorMaskHSV = new SerializableVector3(0, -1, 0) // Foam color
            };

            armorBlockBuilder.BlockOrientation = new MyBlockOrientation(
                Base6Directions.Direction.Forward,
                Base6Directions.Direction.Up);

            grid.AddBlock(armorBlockBuilder, false);
        }


        private void OnBlockDamaged()
        {
            // Check if the block has become non-functional
            if (!block.IsFunctional)
            {
                // Play particle and sound effects at the block's position
                MyVisualScriptLogicProvider.CreateParticleEffectAtPosition("MetalFoamSmoke", block.GetPosition());
                MyVisualScriptLogicProvider.PlaySingleSoundAtPosition("MetalFoamSound", block.GetPosition());

                // Automatically set the foammeup sync variable to true
                foammeup.Value = true;
            }
        }



        static void CreateTerminalControls()
        {
            if (!m_controlsCreated)
            {
                m_controlsCreated = true;

                var startgenerationOnOff = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlOnOffSwitch, IMyDecoy>("MetalFoam_Terminal_StartGeneration");
                startgenerationOnOff.Enabled = (b) => b.GameLogic is MetalFoamGenerator;
                startgenerationOnOff.Visible = (b) => b.GameLogic is MetalFoamGenerator;
                startgenerationOnOff.Title = MyStringId.GetOrCompute("RELEASE THE FOAM");
                startgenerationOnOff.Getter = (b) => (b.GameLogic.GetAs<MetalFoamGenerator>()?.foammeup.Value) ?? false;
                startgenerationOnOff.Setter = (b, v) =>
                {
                    var generator = b.GameLogic.GetAs<MetalFoamGenerator>();
                    if (generator != null)
                    {
                        generator.foammeup.Value = v; // Syncs the value
                    }
                };
                startgenerationOnOff.OnText = MyStringId.GetOrCompute("On");
                startgenerationOnOff.OffText = MyStringId.GetOrCompute("Off");
                MyAPIGateway.TerminalControls.AddControl<IMyDecoy>(startgenerationOnOff);

                // Add an action for cockpits/control stations
                var action = MyAPIGateway.TerminalControls.CreateAction<IMyDecoy>("MetalFoam_Action_StartGeneration");
                action.Icon = @"Textures\GUI\Icons\Actions\Toggle.dds"; // Path to your icon or a default icon
                action.Name = new StringBuilder("RELEASE THE FOAM");
                action.Writer = (block, stringBuilder) =>
                {
                    var generator = block.GameLogic.GetAs<MetalFoamGenerator>();
                    if (generator != null)
                    {
                        stringBuilder.Append("Foam: ").Append(generator.foammeup.Value ? "On" : "Off");
                    }
                };
                action.Action = (block) =>
                {
                    var generator = block.GameLogic.GetAs<MetalFoamGenerator>();
                    if (generator != null)
                    {
                        generator.foammeup.Value = !generator.foammeup.Value; // Syncs the value
                    }
                };
                action.Enabled = (block) => block.GameLogic is MetalFoamGenerator;
                action.ValidForGroups = true; // Set true if you want this action to be available when selecting a group of blocks

                MyAPIGateway.TerminalControls.AddAction<IMyDecoy>(action);
            }
        }

        public override void Close()
        {
            base.Close();

            // Unsubscribe from the foammeup ValueChanged event
            if (foammeup != null)
            {
                foammeup.ValueChanged -= foammeup_ValueChanged;
            }

            // Unsubscribe from the IsFunctionalChanged event
            if (block != null)
            {
                block.SlimBlock.ComponentStack.IsFunctionalChanged -= OnBlockDamaged;
            }
        }



    }
}
