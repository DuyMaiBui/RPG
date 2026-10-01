using AuraEngine.Core;
using AuraEngine.Demo;
using AuraEngine.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace AuraEngine.EditorTools
{
    public static class AuraDemoSceneBuilder
    {
        private const string DemoRoot = "Assets/AuraEngine.Demo";
        private const string SceneFolder = "Assets/AuraEngine.Demo/Scenes";

        [MenuItem("AuraEngine/Build Demo Scenes")]
        public static void BuildAll()
        {
            EnsureFolders();
            Build3D();
            Build2D();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("AuraEngine: demo scenes generated under " + SceneFolder);
        }

        private static void Build3D()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ConfigureLighting();
            var simulation = CreateSimulation(AuraPhysicsMode.Full3D, new Vector3(0f, -9.81f, 0f), AuraBackendKind.Native);
            var camera = CreateCamera(simulation, new Vector3(0f, 7f, -18f), new Vector3(20f, 0f, 0f), false, 0f);
            CreateLight();
            CreateGround(simulation, new Vector3(0f, -0.5f, 0f), new Vector3(30f, 1f, 30f));

            for (var index = 0; index < 12; index++)
            {
                var column = index % 4;
                var row = index / 4;
                var x = -4.5f + column * 3f;
                var z = -2f + row * 2.5f;
                var y = 3.5f + column * 0.6f;
                CreateBall(simulation, new Vector3(x, y, z), 0.5f);
            }

            CreateCapsule(simulation, new Vector3(-1.5f, 4f, 0f), 0.5f, 2f);
            CreateCylinder(simulation, new Vector3(1.5f, 4f, 0f), 0.5f, 2f);

            for (var index = 0; index < 3; index++)
                CreateBox(simulation, new Vector3(0f, 0.5f + index, 3f), 0.5f);

            CreateTaperedCapsule(simulation, new Vector3(-3f, 4f, -3f), 0.5f, 0.2f, 2f);
            CreateTaperedCylinder(simulation, new Vector3(3f, 4f, -3f), 0.5f, 0.15f, 2f);

            CreateTrigger(simulation, new Vector3(0f, 2f, 0f), new Vector3(5f, 0.6f, 5f));
            var platform = CreatePlatform(simulation, new Vector3(-7f, 1.5f, 0f), new Vector3(3f, 0.5f, 3f));
            CreateMover(platform, simulation, new Vector3(4f, 0f, 0f));
            CreateHud(simulation, camera);

            EditorSceneManager.SaveScene(scene, SceneFolder + "/AuraDemo3D.unity");
        }

        private static void Build2D()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ConfigureLighting();
            var simulation = CreateSimulation(AuraPhysicsMode.Plane2D, new Vector3(0f, -9.81f, 0f), AuraBackendKind.Native);
            var camera = CreateCamera(simulation, new Vector3(0f, 0.5f, -12f), Vector3.zero, true, 6f);
            CreateLight();
            CreateGround(simulation, new Vector3(0f, -0.5f, 0f), new Vector3(20f, 1f, 1f));

            for (var index = 0; index < 7; index++)
            {
                var x = -4.8f + index * 1.6f;
                CreateBall(simulation, new Vector3(x, 3f + index % 3 * 0.8f, 0f), 0.5f);
            }

            CreateTrigger(simulation, new Vector3(0f, 2.5f, 0f), new Vector3(4f, 0.5f, 1f));
            var platform = CreatePlatform(simulation, new Vector3(-6f, 1.5f, 0f), new Vector3(2.5f, 0.5f, 1f));
            CreateMover(platform, simulation, new Vector3(4.5f, 0f, 0f));
            CreateHud(simulation, camera);

            EditorSceneManager.SaveScene(scene, SceneFolder + "/AuraDemo2D.unity");
        }

        private static GameObject CreateSimulation(AuraPhysicsMode mode, Vector3 gravity, AuraBackendKind backend)
        {
            var go = new GameObject("AuraSimulation");
            var instance = go.AddComponent<AuraSimulationInstance>();
            var serialized = new SerializedObject(instance);
            serialized.FindProperty("_mode").enumValueIndex = (int)mode;
            serialized.FindProperty("_gravity").vector3Value = gravity;
            serialized.FindProperty("_tickRate").intValue = 60;
            serialized.FindProperty("_initialBodyCapacity").intValue = 64;
            serialized.FindProperty("_autoTick").boolValue = true;
            serialized.FindProperty("_autoCreateOnStart").boolValue = true;
            serialized.FindProperty("_backendKind").enumValueIndex = (int)backend;
            serialized.FindProperty("_layers").objectReferenceValue = GetOrCreateLayers();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return go;
        }

        private static AuraPhysicsLayers GetOrCreateLayers()
        {
            var path = DemoRoot + "/AuraPhysicsLayers.asset";
            var existing = AssetDatabase.LoadAssetAtPath<AuraPhysicsLayers>(path);
            if (existing != null)
                return existing;

            var asset = ScriptableObject.CreateInstance<AuraPhysicsLayers>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static GameObject CreateCamera(GameObject simulation, Vector3 position, Vector3 euler, bool orthographic, float size)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(euler);
            var camera = go.AddComponent<Camera>();
            camera.orthographic = orthographic;
            if (orthographic)
                camera.orthographicSize = size;
            else
                camera.fieldOfView = 60f;

            go.AddComponent<AudioListener>();

            var probe = go.AddComponent<AuraDemoRayProbe>();
            var serialized = new SerializedObject(probe);
            serialized.FindProperty("_instance").objectReferenceValue = simulation.GetComponent<AuraSimulationInstance>();
            serialized.FindProperty("_camera").objectReferenceValue = camera;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return go;
        }

        private static void CreateLight()
        {
            var go = new GameObject("Directional Light");
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
            light.color = new Color(1f, 0.97f, 0.92f);
            light.shadows = LightShadows.Soft;
        }

        private static void CreateGround(GameObject simulation, Vector3 position, Vector3 size)
        {
            var go = CreatePrimitive("Ground", PrimitiveType.Cube, position, size, simulation);
            AddBody(go, AuraBodyType.Static);
            var box = go.AddComponent<AuraBoxColliderAuthoring>();
            SetVector(box, "_size", size);
        }

        private static void CreateBall(GameObject simulation, Vector3 position, float radius)
        {
            var go = CreatePrimitive("Ball", PrimitiveType.Sphere, position, Vector3.one * (radius * 2f), simulation);
            AddBody(go, AuraBodyType.Dynamic);
            var sphere = go.AddComponent<AuraSphereColliderAuthoring>();
            SetFloat(sphere, "_radius", radius);
        }

        private static void CreateCapsule(GameObject simulation, Vector3 position, float radius, float height)
        {
            var go = CreatePrimitive("Capsule", PrimitiveType.Capsule, position, new Vector3(radius * 2f, height * 0.5f, radius * 2f), simulation);
            AddBody(go, AuraBodyType.Dynamic);
            var capsule = go.AddComponent<AuraCapsuleColliderAuthoring>();
            SetFloat(capsule, "_radius", radius);
            SetFloat(capsule, "_height", height);
        }

        private static void CreateCylinder(GameObject simulation, Vector3 position, float radius, float height)
        {
            var go = CreatePrimitive("Cylinder", PrimitiveType.Cylinder, position, new Vector3(radius * 2f, height * 0.5f, radius * 2f), simulation);
            AddBody(go, AuraBodyType.Dynamic);
            var cylinder = go.AddComponent<AuraCylinderColliderAuthoring>();
            SetFloat(cylinder, "_radius", radius);
            SetFloat(cylinder, "_height", height);
        }

        private static void CreateBox(GameObject simulation, Vector3 position, float half)
        {
            var go = CreatePrimitive("Box", PrimitiveType.Cube, position, Vector3.one * (half * 2f), simulation);
            AddBody(go, AuraBodyType.Dynamic);
            var box = go.AddComponent<AuraBoxColliderAuthoring>();
            SetVector(box, "_size", Vector3.one * (half * 2f));
        }

        private static void CreateTaperedCapsule(GameObject simulation, Vector3 position, float radius, float topRadius, float height)
        {
            var go = CreatePrimitive("TaperedCapsule", PrimitiveType.Capsule, position, new Vector3(radius * 2f, height * 0.5f, radius * 2f), simulation);
            AddBody(go, AuraBodyType.Dynamic);
            var shape = go.AddComponent<AuraTaperedCapsuleColliderAuthoring>();
            SetFloat(shape, "_radius", radius);
            SetFloat(shape, "_topRadius", topRadius);
            SetFloat(shape, "_height", height);
        }

        private static void CreateTaperedCylinder(GameObject simulation, Vector3 position, float radius, float topRadius, float height)
        {
            var go = CreatePrimitive("TaperedCylinder", PrimitiveType.Cylinder, position, new Vector3(radius * 2f, height * 0.5f, radius * 2f), simulation);
            AddBody(go, AuraBodyType.Dynamic);
            var shape = go.AddComponent<AuraTaperedCylinderColliderAuthoring>();
            SetFloat(shape, "_radius", radius);
            SetFloat(shape, "_topRadius", topRadius);
            SetFloat(shape, "_height", height);
        }

        private static void CreateTrigger(GameObject simulation, Vector3 position, Vector3 size)
        {
            var go = CreatePrimitive("Trigger", PrimitiveType.Cube, position, size, simulation);
            AddBody(go, AuraBodyType.Static);
            var box = go.AddComponent<AuraBoxColliderAuthoring>();
            SetVector(box, "_size", size);
            SetBool(box, "_isTrigger", true);
        }

        private static GameObject CreatePlatform(GameObject simulation, Vector3 position, Vector3 size)
        {
            var go = CreatePrimitive("Platform", PrimitiveType.Cube, position, size, simulation);
            AddBody(go, AuraBodyType.Kinematic);
            var box = go.AddComponent<AuraBoxColliderAuthoring>();
            SetVector(box, "_size", size);
            return go;
        }

        private static void CreateMover(GameObject platform, GameObject simulation, Vector3 amplitude)
        {
            var mover = platform.AddComponent<AuraDemoKinematicMover>();
            var serialized = new SerializedObject(mover);
            serialized.FindProperty("_instance").objectReferenceValue = simulation.GetComponent<AuraSimulationInstance>();
            serialized.FindProperty("_body").objectReferenceValue = platform.GetComponent<AuraPhysicsBodyAuthoring>();
            serialized.FindProperty("_amplitude").vector3Value = amplitude;
            serialized.FindProperty("_speed").floatValue = 1.2f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateHud(GameObject simulation, GameObject camera)
        {
            var go = new GameObject("DemoHud");
            var hud = go.AddComponent<AuraDemoHud>();
            var serialized = new SerializedObject(hud);
            serialized.FindProperty("_instance").objectReferenceValue = simulation.GetComponent<AuraSimulationInstance>();
            serialized.FindProperty("_rayProbe").objectReferenceValue = camera.GetComponent<AuraDemoRayProbe>();
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject CreatePrimitive(string name, PrimitiveType primitive, Vector3 position, Vector3 scale, GameObject simulation)
        {
            var go = GameObject.CreatePrimitive(primitive);
            go.name = name;
            go.transform.SetParent(simulation.transform, true);
            go.transform.position = position;
            go.transform.localScale = scale;

            var collider = go.GetComponent<Collider>();
            if (collider != null)
                Object.DestroyImmediate(collider);

            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.sharedMaterial = CreateMaterial(name, renderer.sharedMaterial);

            go.AddComponent<AuraPhysicsView>();
            return go;
        }

        private static Material CreateMaterial(string name, Material fallback)
        {
            var isGround = name == "Ground";
            var path = DemoRoot + "/Materials/" + (isGround ? "Ground.mat" : "Actor.mat");
            var shader = ResolveLitShader();
            var color = isGround ? new Color(0.25f, 0.28f, 0.35f) : new Color(0.95f, 0.55f, 0.15f);

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", 0.2f);
            if (material.HasProperty("_Glossiness"))
                material.SetFloat("_Glossiness", 0.2f);
            if (material.HasProperty("_Metallic"))
                material.SetFloat("_Metallic", 0f);

            EditorUtility.SetDirty(material);
            return material;
        }

        private static Shader ResolveLitShader()
        {
            if (GraphicsSettings.currentRenderPipeline != null)
            {
                var urp = Shader.Find("Universal Render Pipeline/Lit");
                if (urp != null)
                    return urp;
            }

            return Shader.Find("Standard");
        }

        private static void ConfigureLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.36f, 0.38f, 0.44f);
            RenderSettings.fog = false;
        }

        private static AuraPhysicsBodyAuthoring AddBody(GameObject go, AuraBodyType type)
        {
            var body = go.AddComponent<AuraPhysicsBodyAuthoring>();
            SetEnum(body, "_type", (int)type);
            return body;
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder(DemoRoot))
                AssetDatabase.CreateFolder("Assets", "AuraEngine.Demo");
            if (!AssetDatabase.IsValidFolder(SceneFolder))
                AssetDatabase.CreateFolder(DemoRoot, "Scenes");
            if (!AssetDatabase.IsValidFolder(DemoRoot + "/Materials"))
                AssetDatabase.CreateFolder(DemoRoot, "Materials");
        }

        private static void SetEnum(Object target, string field, int value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).enumValueIndex = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(Object target, string field, float value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBool(Object target, string field, bool value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetVector(Object target, string field, Vector3 value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).vector3Value = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
