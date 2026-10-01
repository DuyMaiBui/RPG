using System;

namespace AuraEngine.Core
{
    public sealed class AuraCollisionMatrix
    {
        private readonly ulong[] _masks = new ulong[AuraPhysicsLayer.MaxLayers];

        public AuraCollisionMatrix()
        {
            for (var layer = 0; layer < AuraPhysicsLayer.MaxLayers; layer++)
                _masks[layer] = ~0UL;
        }

        public static AuraCollisionMatrix CreateAllCollide() => new AuraCollisionMatrix();

        public static AuraCollisionMatrix CreateNoneCollide()
        {
            var matrix = new AuraCollisionMatrix();
            for (var layer = 0; layer < AuraPhysicsLayer.MaxLayers; layer++)
                matrix._masks[layer] = 0UL;
            return matrix;
        }

        public ulong GetMask(AuraPhysicsLayer layer) => _masks[layer.Value];

        public AuraCollisionMatrix SetMask(AuraPhysicsLayer layer, ulong mask)
        {
            _masks[layer.Value] = mask;
            return this;
        }

        public bool CanCollide(AuraPhysicsLayer a, AuraPhysicsLayer b) =>
            ((GetMask(a) >> b.Value) & 1UL) != 0UL &&
            ((GetMask(b) >> a.Value) & 1UL) != 0UL;

        public AuraCollisionMatrix SetCollision(AuraPhysicsLayer a, AuraPhysicsLayer b, bool enabled)
        {
            if (enabled)
            {
                _masks[a.Value] |= 1UL << b.Value;
                _masks[b.Value] |= 1UL << a.Value;
            }
            else
            {
                _masks[a.Value] &= ~(1UL << b.Value);
                _masks[b.Value] &= ~(1UL << a.Value);
            }

            return this;
        }

        public AuraCollisionMatrix SetSelfCollision(AuraPhysicsLayer layer, bool enabled) =>
            SetCollision(layer, layer, enabled);
    }
}
