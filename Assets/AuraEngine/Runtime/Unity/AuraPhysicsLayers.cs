using System.Collections.Generic;
using AuraEngine.Core;
using UnityEngine;

namespace AuraEngine.Unity
{
    [CreateAssetMenu(menuName = "AuraEngine/Physics Layers", fileName = "AuraPhysicsLayers")]
    public sealed class AuraPhysicsLayers : ScriptableObject
    {
        public const int MaxLayers = AuraPhysicsLayer.MaxLayers;
        public const string DefaultLayerName = "Default";

        [SerializeField, HideInInspector]
        private string[] _names = new string[MaxLayers];

        [SerializeField, HideInInspector]
        private ulong[] _masks = new ulong[MaxLayers];

        private void OnEnable() => Ensure();

        public bool HasLayer(int index)
        {
            Ensure();
            return index >= 0 && index < MaxLayers && !string.IsNullOrEmpty(_names[index]);
        }

        public string GetName(int index)
        {
            Ensure();
            return index >= 0 && index < MaxLayers ? _names[index] ?? string.Empty : string.Empty;
        }

        public int[] GetNamedIndices()
        {
            Ensure();
            var list = new List<int>(MaxLayers);
            for (var index = 0; index < MaxLayers; index++)
                if (!string.IsNullOrEmpty(_names[index]))
                    list.Add(index);
            return list.ToArray();
        }

        public int NamedLayerCount => GetNamedIndices().Length;

        public void SetName(int index, string name)
        {
            Ensure();
            if (index <= 0 || index >= MaxLayers)
                return;

            var wasNamed = !string.IsNullOrEmpty(_names[index]);
            _names[index] = string.IsNullOrEmpty(name) ? string.Empty : name;
            if (!wasNamed && !string.IsNullOrEmpty(_names[index]))
                GrantDefaultInteractions(index);

            Sanitize();
        }

        public int AddLayer(string name)
        {
            Ensure();
            if (string.IsNullOrEmpty(name))
                return -1;

            for (var index = 1; index < MaxLayers; index++)
            {
                if (!string.IsNullOrEmpty(_names[index]))
                    continue;

                _names[index] = name;
                GrantDefaultInteractions(index);
                Sanitize();
                return index;
            }

            return -1;
        }

        public ulong GetMask(int index)
        {
            Ensure();
            if (index < 0 || index >= MaxLayers || string.IsNullOrEmpty(_names[index]))
                return 0ul;
            return _masks[index];
        }

        public bool CanCollide(int a, int b)
        {
            Ensure();
            if (!HasLayer(a) || !HasLayer(b))
                return false;
            return ((_masks[a] >> b) & 1ul) != 0ul && ((_masks[b] >> a) & 1ul) != 0ul;
        }

        public void SetCollision(int a, int b, bool enabled)
        {
            Ensure();
            if (!HasLayer(a) || !HasLayer(b))
                return;

            if (enabled)
            {
                _masks[a] |= 1ul << b;
                _masks[b] |= 1ul << a;
            }
            else
            {
                _masks[a] &= ~(1ul << b);
                _masks[b] &= ~(1ul << a);
            }
        }

        public void SetAll(bool enabled)
        {
            Ensure();
            var indices = GetNamedIndices();
            var value = enabled ? NamedMask() : 0ul;
            for (var i = 0; i < indices.Length; i++)
                _masks[indices[i]] = value;
        }

        public AuraCollisionMatrix ToCollisionMatrix()
        {
            Ensure();
            var matrix = AuraCollisionMatrix.CreateNoneCollide();
            for (var index = 0; index < MaxLayers; index++)
                matrix.SetMask(new AuraPhysicsLayer(index), string.IsNullOrEmpty(_names[index]) ? 0ul : _masks[index]);
            return matrix;
        }

        private void GrantDefaultInteractions(int index)
        {
            var named = NamedMask();
            _masks[index] = named;
            for (var other = 0; other < MaxLayers; other++)
                if (other != index && !string.IsNullOrEmpty(_names[other]))
                    _masks[other] |= 1ul << index;
        }

        private ulong NamedMask()
        {
            var mask = 0ul;
            for (var index = 0; index < MaxLayers; index++)
                if (!string.IsNullOrEmpty(_names[index]))
                    mask |= 1ul << index;
            return mask;
        }

        private void Ensure()
        {
            if (_names == null || _names.Length != MaxLayers)
            {
                _names = new string[MaxLayers];
                _names[0] = DefaultLayerName;
            }

            if (string.IsNullOrEmpty(_names[0]))
                _names[0] = DefaultLayerName;

            if (_masks == null || _masks.Length != MaxLayers)
            {
                _masks = new ulong[MaxLayers];
                for (var index = 0; index < MaxLayers; index++)
                    _masks[index] = ~0ul;
            }

            Sanitize();
        }

        private void Sanitize()
        {
            var named = NamedMask();
            for (var index = 0; index < MaxLayers; index++)
            {
                if (string.IsNullOrEmpty(_names[index]))
                    _masks[index] = 0ul;
                else
                    _masks[index] &= named;
            }
        }
    }
}
