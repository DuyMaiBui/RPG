using RPG.Core.Actors;
using RPG.Simulation.Contracts;

namespace RPG.Core.Tests
{
    /// <summary>Per-unit bookkeeping of one headless Moba scenario run: how far a unit travelled, how long it stood
    /// still, whether it ever closed on its target and how much it attacked.</summary>
    internal sealed class MobaScenarioUnit
    {
        public EntityId Id;
        public FactionId Faction;
        public bool Ranged;
        public int SpawnTick;
        public SimulationVector2 SpawnPosition;
        public SimulationVector2 LastPosition;
        public int LastProgressTick;
        public int CurrentStall;
        public int MaxStall;
        public int StallTicks;
        public int CurrentNoClose;
        public int MaxNoClose;
        public float PreviousTargetDistance = -1f;
        public float Travelled;
        public float NetDisplacement;
        public float Reach;
        public float AttackDistance;
        public float MinTargetDistance = float.MaxValue;
        public int InRangeTicks;
        public bool WantsToMove;
        public SimulationVector2 LastHeading;
        public int CurrentDipTicks;
        public int DipEpisodes;
        public int CurrentContactDipTicks;
        public int ContactDipEpisodes;
        public int CurrentTerrainDipTicks;
        public int TerrainDipEpisodes;
        public int CurrentCrowdDipTicks;
        public int CrowdDipEpisodes;
        public float CurrentTargetDistance = -1f;
        public bool CrossedMidline;
        public bool Died;
        public int DeathTick = -1;
        public int Attacks;
        public int DamageDealt;
        public int DamageTaken;
        public int Kills;
        public int Casts;
    }
}
