using RPG.Simulation.Contracts;

namespace RPG.Core.Actors
{
    public sealed class AnyAliveEnemyTargetSelector
    {
        public bool TrySelect(Actor attacker, ActorRegistry actors, out EntityId targetId)
        {
            targetId = EntityId.None;
            foreach (var snapshot in actors.CreateSnapshot())
            {
                if (snapshot.Faction == attacker.Components.Get<FactionComponent>().Faction || snapshot.VisualState == ActorVisualState.Dead)
                    continue;

                targetId = snapshot.Entity;
                return true;
            }

            return false;
        }
    }
}
