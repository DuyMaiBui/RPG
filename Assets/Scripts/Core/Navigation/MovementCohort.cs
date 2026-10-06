using RPG.Core.Actors;
using RPG.Simulation.Contracts;

namespace RPG.Core.Navigation
{
    public sealed class MovementCohort
    {
        private const float WaypointReachDistance = 0.35f;
        private const float SlotSpacing = 0.9f;

        public MovementCohort(int id, int routeCapacity)
        {
            Id = id;
            _members = new EntityId[8];
            Route = new SimulationVector2[routeCapacity];
            RouteLength = 0;
        }

        public int Id { get; }
        public EntityId[] Members => _members;
        public int MemberCount { get; private set; }
        public int ObjectiveKey { get; private set; }
        public FactionId Faction { get; private set; }
        public SimulationVector2 Destination { get; private set; }
        public SimulationVector2 Anchor { get; private set; }
        public SimulationVector2 SharedDirection { get; private set; }
        public SimulationVector2[] Route { get; }
        public int RouteLength { get; private set; }
        public int NextWaypoint { get; private set; }
        public int RouteRevision { get; private set; }
        public bool UsesFlowField { get; private set; }
        public bool HasRoute => RouteLength > 0;

        public void Reset(int objectiveKey, FactionId faction, SimulationVector2 destination)
        {
            Prepare(objectiveKey, faction, destination, false);
        }

        public void Prepare(int objectiveKey, FactionId faction, SimulationVector2 destination, bool preserveRoute)
        {
            ObjectiveKey = objectiveKey;
            Faction = faction;
            Destination = destination;
            Anchor = SimulationVector2.Zero;
            SharedDirection = SimulationVector2.Zero;
            MemberCount = 0;
            if (!preserveRoute)
            {
                RouteLength = 0;
                NextWaypoint = 0;
                RouteRevision = -1;
                UsesFlowField = false;
            }
        }

        public void AddMember(EntityId id)
        {
            if (MemberCount == _members.Length)
            {
                var expanded = new EntityId[_members.Length * 2];
                System.Array.Copy(_members, expanded, _members.Length);
                _members = expanded;
            }

            _members[MemberCount++] = id;
        }

        public bool ContainsMember(EntityId id)
        {
            for (var index = 0; index < MemberCount; index++)
            {
                if (_members[index] == id)
                    return true;
            }

            return false;
        }

        public void SetAnchor(SimulationVector2 anchor) => Anchor = anchor;

        public void SetDestination(SimulationVector2 destination) => Destination = destination;

        public void SetRoute(SimulationVector2 destination, int routeLength, int revision, float radius)
        {
            Destination = destination;
            RouteLength = routeLength;
            RouteRevision = revision;
            NextWaypoint = routeLength > 1 ? 1 : 0;
            RouteRadius = radius;
            UsesFlowField = false;
        }

        public void SetDirectRoute(SimulationVector2 start, SimulationVector2 destination, int revision, float radius)
        {
            Destination = destination;
            Route[0] = start;
            Route[1] = destination;
            RouteLength = 2;
            RouteRevision = revision;
            NextWaypoint = 1;
            RouteRadius = radius;
            UsesFlowField = false;
        }

        public void SetFlowFieldRoute(SimulationVector2 destination, int revision, float radius)
        {
            Destination = destination;
            RouteLength = 0;
            RouteRevision = revision;
            NextWaypoint = 0;
            RouteRadius = radius;
            UsesFlowField = true;
        }

        public void UpdateAnchor(ActorRegistry actors)
        {
            if (MemberCount > 0 && actors.TryGet(Members[0], out var leader) &&
                !leader.Components.Get<HealthComponent>().IsDead)
            {
                Anchor = leader.Components.Get<PositionComponent>().Position;
                AdvanceWaypoint();
                return;
            }

            var sum = SimulationVector2.Zero;
            var count = 0;
            for (var index = 0; index < MemberCount; index++)
            {
                if (!actors.TryGet(Members[index], out var actor) ||
                    actor.Components.Get<HealthComponent>().IsDead)
                    continue;

                sum += actor.Components.Get<PositionComponent>().Position;
                count++;
            }

            if (count > 0)
                Anchor = sum / count;

            AdvanceWaypoint();
        }

        public SimulationVector2 GetDirection(Actor actor, int slotIndex)
        {
            if (!HasRoute && !UsesFlowField)
                return SimulationVector2.Zero;

            var routeDirection = SharedDirection;
            var position = actor.Components.Get<PositionComponent>().Position;
            var columns = System.Math.Max(1, System.Math.Min(4, (int)SimulationMath.Ceiling(SimulationMath.Sqrt(MemberCount))));
            var row = slotIndex / columns;
            var column = slotIndex % columns;
            var lateral = (column - (columns - 1) * 0.5f) * SlotSpacing;
            var backward = row * SlotSpacing;
            var side = new SimulationVector2(-routeDirection.Y, routeDirection.X);
            var slotPosition = Anchor + side * lateral - routeDirection * backward;
            var slotCorrection = slotPosition - position;
            if (slotCorrection.LengthSquared <= 0.04f)
                return routeDirection;

            var correction = slotCorrection.Normalized();
            return (routeDirection * 0.75f + correction * 0.25f).Normalized();
        }

        private void AdvanceWaypoint()
        {
            if (UsesFlowField)
                return;

            while (NextWaypoint < RouteLength - 1 &&
                   (Route[NextWaypoint] - Anchor).LengthSquared <= WaypointReachDistance * WaypointReachDistance)
                NextWaypoint++;

            if (NextWaypoint >= RouteLength)
                NextWaypoint = RouteLength - 1;

            SharedDirection = NextWaypoint >= 0 && NextWaypoint < RouteLength
                ? (Route[NextWaypoint] - Anchor).Normalized()
                : SimulationVector2.Zero;
        }

        public void RefreshDirection(NavigationFlowFieldCache flowFields)
        {
            if (UsesFlowField)
            {
                if (!flowFields.TryGetDirection(Anchor, Destination, RouteRadius, out var direction))
                    SharedDirection = SimulationVector2.Zero;
                else
                    SharedDirection = direction;
                return;
            }

            AdvanceWaypoint();
        }

        public bool IsNextSegmentBlocked(DynamicOccupancyGrid occupancy)
        {
            if (UsesFlowField)
                return occupancy.IsStationaryPathBlocked(Anchor, Destination);
            if (!HasRoute || NextWaypoint >= RouteLength)
                return false;
            return occupancy.IsStationaryPathBlocked(Anchor, Route[NextWaypoint]);
        }

        private EntityId[] _members;
        private float RouteRadius;
    }
}
