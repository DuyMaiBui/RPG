using System;
using AuraEngine.Core;
using AuraEngine.Physics;
using AuraEngine.Simulation;
using AuraEngine.Unity;
using UnityEngine;

namespace AuraEngine.Tests
{
    /* One test scene hierarchy: a root GameObject holding an AuraSimulationInstance. The world is created explicitly
       with the public AuraSimulationInstance.CreateWorld() (Start/_autoCreateOnStart does not run in EditMode); the mode,
       gravity and backend go through SerializedObject / the public SetPhysicsBackend. Dispose destroys the hierarchy and
       the native world (OnDestroy is not sent in EditMode, so the world is disposed explicitly). */
    public sealed class AuraAuthoringHarness : IDisposable
    {
        private const float StepSeconds = 1f / 60f;
        private uint _tick;

        private AuraAuthoringHarness(GameObject root, AuraSimulationInstance instance)
        {
            Root = root;
            Instance = instance;
        }

        public GameObject Root { get; }

        public AuraSimulationInstance Instance { get; }

        public AuraSimulationWorld World => Instance.World;

        public static AuraAuthoringHarness Create(AuraPhysicsMode mode, IPhysicsBackend backend = null, bool zeroGravity = false)
        {
            var root = new GameObject("AuraTestRoot");
            var instance = root.AddComponent<AuraSimulationInstance>();
            AuraSerializedFields.SetEnum(instance, "_mode", mode);
            if (zeroGravity)
                AuraSerializedFields.SetVector3(instance, "_gravity", Vector3.zero);

            if (backend != null)
                instance.SetPhysicsBackend(backend);

            return new AuraAuthoringHarness(root, instance);
        }

        public void CreateWorld() => Instance.CreateWorld();

        public GameObject NewChild(string name, Transform parent, Vector3 position, Vector3 euler, Vector3 scale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : Root.transform, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            return go;
        }

        public AuraPhysicsBodyAuthoring AddBody3D(GameObject go, AuraBodyType type = AuraBodyType.Dynamic)
        {
            go.AddComponent<AuraBoxColliderAuthoring>();
            var body = go.AddComponent<AuraPhysicsBodyAuthoring>();
            AuraSerializedFields.SetEnum(body, "_type", type);
            return body;
        }

        public AuraPhysicsBody2DAuthoring AddBody2D(GameObject go, AuraBodyType type = AuraBodyType.Dynamic)
        {
            go.AddComponent<AuraBoxCollider2DAuthoring>();
            var body = go.AddComponent<AuraPhysicsBody2DAuthoring>();
            AuraSerializedFields.SetEnum(body, "_type", type);
            return body;
        }

        public void Enable(params Component[] components)
        {
            for (var index = 0; index < components.Length; index++)
                AuraAuthoringLifecycle.Enable(components[index]);
        }

        public void Step(int count)
        {
            for (var index = 0; index < count; index++)
                World.Step(new SimulationStep(new SimulationTick(++_tick), StepSeconds));
        }

        public void Dispose()
        {
            if (Instance != null && Instance.IsCreated)
                ((IDisposable)Instance.World).Dispose();

            if (Root != null)
                UnityEngine.Object.DestroyImmediate(Root);
        }
    }
}
