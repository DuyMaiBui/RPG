namespace RPG.Core.Actors
{
    public sealed class ProjectileWeaponComponent : IActorComponent
    {
        public ProjectileWeaponComponent(float speed, float radius, float lifetime)
        {
            Speed = speed;
            Radius = radius;
            Lifetime = lifetime;
        }

        public float Speed { get; }
        public float Radius { get; }
        public float Lifetime { get; }
    }
}
