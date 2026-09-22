namespace RPG.Core.Actors
{
    public sealed class AttackCooldownComponent : IActorComponent
    {
        public AttackCooldownComponent(float cooldown)
        {
            Cooldown = cooldown < 0f ? 0f : cooldown;
        }

        public float Cooldown { get; }
        public float Remaining { get; private set; }

        public bool IsReady => Remaining <= 0f;

        public void Consume() => Remaining = Cooldown;

        public void Tick(float deltaTime)
        {
            Remaining -= deltaTime;
            if (Remaining < 0f) Remaining = 0f;
        }
    }
}
