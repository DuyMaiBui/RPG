using System;
using System.Collections.Generic;
using RPG.Simulation.Contracts;

namespace RPG.Core.Actors
{
    /// <summary>Which player owns which actors. One player owns many actors (an RTS selection) and an actor has at
    /// most one owner. This is the authority check for every player-issued command: an order for an actor the session
    /// does not own is rejected rather than applied.</summary>
    public sealed class PlayerRoster
    {
        private static readonly EntityId[] NoActors = Array.Empty<EntityId>();

        private readonly Dictionary<PlayerId, List<EntityId>> _actorsByPlayer = new();
        private readonly Dictionary<EntityId, PlayerId> _ownerByActor = new();

        public int PlayerCount => _actorsByPlayer.Count;

        public int ActorCount => _ownerByActor.Count;

        public void Assign(PlayerId player, EntityId actor)
        {
            if (_ownerByActor.TryGetValue(actor, out var previous))
            {
                if (previous.Equals(player))
                    return;

                Remove(actor);
            }

            if (!_actorsByPlayer.TryGetValue(player, out var actors))
            {
                actors = new List<EntityId>();
                _actorsByPlayer.Add(player, actors);
            }

            actors.Add(actor);
            _ownerByActor.Add(actor, player);
        }

        public bool Owns(PlayerId player, EntityId actor) =>
            _ownerByActor.TryGetValue(actor, out var owner) && owner.Equals(player);

        public bool TryGetOwner(EntityId actor, out PlayerId player) => _ownerByActor.TryGetValue(actor, out player);

        /// <summary>The actors a player owns, in assignment order. Read-only from the outside.</summary>
        public IReadOnlyList<EntityId> ActorsOf(PlayerId player) =>
            _actorsByPlayer.TryGetValue(player, out var actors) ? actors : NoActors;

        public bool Remove(EntityId actor)
        {
            if (!_ownerByActor.Remove(actor, out var owner))
                return false;

            if (!_actorsByPlayer.TryGetValue(owner, out var actors))
                return true;

            actors.Remove(actor);
            if (actors.Count == 0)
                _actorsByPlayer.Remove(owner);
            return true;
        }
    }
}
