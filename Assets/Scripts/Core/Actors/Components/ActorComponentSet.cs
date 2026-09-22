using System;
using System.Collections.Generic;

namespace RPG.Core.Actors
{
    public sealed class ActorComponentSet
    {
        private readonly Dictionary<Type, IActorComponent> _components;

        public ActorComponentSet(int capacity = 4)
        {
            _components = new Dictionary<Type, IActorComponent>(capacity);
        }

        public void Add<T>(T component) where T : class, IActorComponent
        {
            if (component == null) throw new ArgumentNullException(nameof(component));
            _components.Add(typeof(T), component);
        }

        public bool Contains<T>() where T : class, IActorComponent =>
            _components.ContainsKey(typeof(T));

        public T Get<T>() where T : class, IActorComponent =>
            (T)_components[typeof(T)];

        public bool TryGet<T>(out T component) where T : class, IActorComponent
        {
            if (_components.TryGetValue(typeof(T), out var value))
            {
                component = (T)value;
                return true;
            }

            component = null;
            return false;
        }

        public bool Remove<T>() where T : class, IActorComponent =>
            _components.Remove(typeof(T));
    }
}
