using System;

namespace RPG.Core.Physics
{
    public readonly struct ColliderFilter
    {
        public ColliderFilter(int layer, int mask)
        {
            if (layer < 0 || layer > 31) throw new ArgumentOutOfRangeException(nameof(layer));
            Layer = layer;
            Mask = mask;
        }

        public int Layer { get; }
        public int Mask { get; }

        public bool CanInteractWith(ColliderFilter other) =>
            (Mask & (1 << other.Layer)) != 0 &&
            (other.Mask & (1 << Layer)) != 0;
    }
}
