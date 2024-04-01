﻿using System;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.ModAPI;
using VRage.ObjectBuilders;

namespace MoA_Fusion_Systems.Data.Scripts.ModularAssemblies.
    FusionParts.FusionReactor
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Reactor), false, "Caster_Reactor")]
    public class FusionReactorLogic : FusionPart<IMyReactor>
    {
        private const float MaxPowerPerReactor = 2000;

        private float BufferReactorOutput;


        internal override string BlockSubtype => "Caster_Reactor";
        internal override string ReadableName => "Reactor";


        public void UpdatePower(float PowerGeneration, float MegawattsPerFusionPower)
        {
            BufferPowerGeneration = PowerGeneration;

            var reactorConsumptionMultiplier =
                OverrideEnabled.Value
                    ? OverridePowerUsageSync
                    : PowerUsageSync.Value; // This is ugly, let's make it better.
            // Power generation consumed (per second)
            var powerConsumption = PowerGeneration * 60 * reactorConsumptionMultiplier;


            var reactorEfficiencyMultiplier = 1 / (0.5f + reactorConsumptionMultiplier);
            // Power generated (per second)
            var reactorOutput = reactorEfficiencyMultiplier * powerConsumption * MegawattsPerFusionPower;

            if (reactorOutput > MaxPowerPerReactor)
            {
                reactorOutput = MaxPowerPerReactor;
                powerConsumption = GetConsumptionFromPower(reactorOutput, MegawattsPerFusionPower);
            }

            BufferReactorOutput = reactorOutput;
            MaxPowerConsumption = powerConsumption / 60;

            InfoText.Clear();
            InfoText.AppendLine(
                $"\nOutput: {Math.Round(reactorOutput, 1)}/{Math.Round(PowerGeneration * 60 * MegawattsPerFusionPower, 1)}");
            InfoText.AppendLine($"Input: {Math.Round(powerConsumption, 1)}/{Math.Round(PowerGeneration * 60, 1)}");
            InfoText.AppendLine($"Efficiency: {Math.Round(reactorEfficiencyMultiplier * 100)}%");

            // Convert back into power per tick
            SyncMultipliers.ReactorOutput(Block, BufferReactorOutput);
        }

        private float GetConsumptionFromPower(float reactorOutput, float MegawattsPerFusionPower)
        {
            return reactorOutput / MegawattsPerFusionPower;
        }

        //private float GetEfficiencyFromPower(float reactorConsumption)
        //{
        //    var a = (1 / (0.5f + reactorConsumptionMultiplier)) * (PowerGeneration * 60 * reactorConsumptionMultiplier);
        //
        //}

        public void SetPowerBoost(bool value)
        {
            if (OverrideEnabled.Value == value)
                return;

            OverrideEnabled.Value = value;
            UpdatePower(BufferPowerGeneration, S_FusionSystem.MegawattsPerFusionPower);
        }

        #region Base Methods

        public override void Init(MyObjectBuilder_EntityBase definition)
        {
            base.Init(definition);
            Block = (IMyReactor)Entity;
            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;

            // Trigger power update is only needed when OverrideEnabled is false
            PowerUsageSync.ValueChanged += value =>
            {
                if (!OverrideEnabled.Value)
                    UpdatePower(BufferPowerGeneration, S_FusionSystem.MegawattsPerFusionPower);
            };

            // Trigger power update is only needed when OverrideEnabled is true
            OverridePowerUsageSync.ValueChanged += value =>
            {
                if (OverrideEnabled.Value)
                    UpdatePower(BufferPowerGeneration, S_FusionSystem.MegawattsPerFusionPower);
            };

            // Trigger power update if boostEnabled is changed
            OverrideEnabled.ValueChanged += value =>
                UpdatePower(BufferPowerGeneration, S_FusionSystem.MegawattsPerFusionPower);
        }

        public override void UpdateAfterSimulation()
        {
            base.UpdateAfterSimulation();

            // If boost is unsustainable, disable it.
            // If power draw exceeds power available, disable self until available.
            if (MemberSystem?.PowerStored <= PowerConsumption * 120 || !Block.IsWorking)
            {
                SetPowerBoost(false);
                PowerConsumption = 0;
                SyncMultipliers.ReactorOutput(Block, 0);
            }
            else
            {
                SyncMultipliers.ReactorOutput(Block, BufferReactorOutput);
                PowerConsumption = MaxPowerConsumption * Block.CurrentOutputRatio;
            }
        }

        #endregion
    }
}