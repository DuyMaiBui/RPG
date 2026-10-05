using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace AuraEngine.Tests
{
    /* The authoring components are plain MonoBehaviours, so Unity does not send Awake/OnEnable/OnDisable to them in
       EditMode. This helper invokes the private OnEnable/OnDisable messages (declared on the type or a base class)
       exactly as Unity would in Play Mode, so registration and release run the real production code. */
    public static class AuraAuthoringLifecycle
    {
        public static void Enable(Component component) => Invoke(component, "OnEnable");

        public static void Disable(Component component) => Invoke(component, "OnDisable");

        private static void Invoke(Component component, string message)
        {
            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            for (var type = component.GetType(); type != null; type = type.BaseType)
            {
                var method = type.GetMethod(message, Flags, null, Type.EmptyTypes, null);
                if (method == null)
                    continue;

                method.Invoke(component, null);
                return;
            }

            Assert.Fail($"{component.GetType().Name} has no {message} message.");
        }
    }
}
