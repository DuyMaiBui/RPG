using System.Collections.Generic;
using UnityEngine;

namespace RPG.Unity
{
    public sealed class FloatingCombatTextPool : MonoBehaviour
    {
        [SerializeField] private int initialSize = 12;
        private readonly List<FloatingCombatTextView> _items = new();

        private void Awake()
        {
            for (var index = 0; index < initialSize; index++)
                CreateItem();
        }

        public FloatingCombatTextView Rent(Vector3 position, int damage, Color color)
        {
            foreach (var item in _items)
            {
                if (!item.IsAvailable) continue;
                item.Show(position, damage, color);
                return item;
            }

            var created = CreateItem();
            created.Show(position, damage, color);
            return created;
        }

        public void Return(FloatingCombatTextView item) => item.Release();

        private FloatingCombatTextView CreateItem()
        {
            var itemObject = new GameObject("FloatingCombatText");
            itemObject.transform.SetParent(transform, false);
            var item = itemObject.AddComponent<FloatingCombatTextView>();
            item.Initialize(this);
            _items.Add(item);
            return item;
        }
    }
}
