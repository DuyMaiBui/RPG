using RPG.Simulation.Contracts;

namespace RPG.Core.Projectiles
{
    public sealed class Projectile
    {
        public Projectile(EntityId id, EntityId source, EntityId target, SimulationVector2 position, int damage, float speed, float radius, float lifetime)
        {
            Reset(id, source, target, position, damage, speed, radius, lifetime);
        }

        public EntityId Id { get; private set; }
        public EntityId Source { get; private set; }
        public EntityId Target { get; private set; }
        public SimulationVector2 Position { get; set; }
        public int Damage { get; private set; }
        public float Speed { get; private set; }
        public float Radius { get; private set; }
        public float RemainingLifetime { get; private set; }

        public void Reset(EntityId id, EntityId source, EntityId target, SimulationVector2 position, int damage, float speed, float radius, float lifetime)
        {
            Id = id;
            Source = source;
            Target = target;
            Position = position;
            Damage = damage;
            Speed = speed;
            Radius = radius;
            RemainingLifetime = lifetime;
        }

        public void Tick(float deltaTime) => RemainingLifetime -= deltaTime;
    }
}
