using System.Collections.Generic;
using RPG.Simulation.Contracts;

namespace RPG.Core.Actors
{
    public sealed class TurnState
    {
        private readonly List<EntityId> _order = new();
        private int _currentIndex;

        public int Round { get; private set; } = 1;
        public int TurnNumber { get; private set; }
        public EntityId ActiveActorId { get; private set; } = EntityId.None;
        public bool IsInitialized => _order.Count > 0;

        public void Initialize(IReadOnlyList<EntityId> orderedActors)
        {
            _order.Clear();
            for (var index = 0; index < orderedActors.Count; index++)
                _order.Add(orderedActors[index]);

            _currentIndex = 0;
            Round = 1;
            TurnNumber = 0;
            ActiveActorId = _order.Count == 0 ? EntityId.None : _order[0];
        }

        public bool Advance(ActorRegistry actors)
        {
            RemoveInvalidActors(actors);
            if (_order.Count == 0)
            {
                ActiveActorId = EntityId.None;
                return false;
            }

            _currentIndex = (_currentIndex + 1) % _order.Count;
            if (_currentIndex == 0)
                Round++;

            TurnNumber++;
            ActiveActorId = _order[_currentIndex];
            return true;
        }

        public BattleResult GetResult(ActorRegistry actors)
        {
            var redAlive = false;
            var blueAlive = false;
            foreach (var snapshot in actors.CreateSnapshot())
            {
                if (snapshot.Faction == FactionId.Red) redAlive = true;
                if (snapshot.Faction == FactionId.Blue) blueAlive = true;
            }

            if (redAlive && blueAlive) return BattleResult.Ongoing;
            if (redAlive) return BattleResult.RedWon;
            if (blueAlive) return BattleResult.BlueWon;
            return BattleResult.Draw;
        }

        private void RemoveInvalidActors(ActorRegistry actors)
        {
            for (var index = _order.Count - 1; index >= 0; index--)
            {
                if (actors.TryGet(_order[index], out _)) continue;
                _order.RemoveAt(index);
                if (index < _currentIndex) _currentIndex--;
            }

            if (_order.Count == 0)
            {
                _currentIndex = 0;
                return;
            }

            if (_currentIndex >= _order.Count)
                _currentIndex = 0;
        }
    }
}
