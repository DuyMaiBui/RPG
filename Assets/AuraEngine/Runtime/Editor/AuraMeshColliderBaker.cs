using System.IO;
using AuraEngine.Unity;
using UnityEditor;
using UnityEngine;

namespace AuraEngine.EditorTools
{
    public static class AuraMeshColliderBaker
    {
        private const string BakedFolder = "Assets/AuraEngine.Demo/Baked";

        [MenuItem("AuraEngine/Bake Mesh Collider")]
        public static void Bake()
        {
            var target = Selection.activeGameObject;
            if (target == null)
            {
                Debug.LogError("AuraEngine: select a GameObject with a MeshFilter to bake.");
                return;
            }

            var filter = target.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
            {
                Debug.LogError("AuraEngine: the selected GameObject has no MeshFilter with a mesh.");
                return;
            }

            var mesh = filter.sharedMesh;
            var sourcePath = AssetDatabase.GetAssetPath(mesh);
            var sourceGuid = string.IsNullOrEmpty(sourcePath) ? mesh.name : AssetDatabase.AssetPathToGUID(sourcePath);
            var assetId = StableHash(sourceGuid);

            EnsureFolder();
            var assetPath = Path.Combine(BakedFolder, mesh.name + "_physics.asset").Replace('\\', '/');
            var data = AssetDatabase.LoadAssetAtPath<AuraPhysicsMeshData>(assetPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<AuraPhysicsMeshData>();
                AssetDatabase.CreateAsset(data, assetPath);
            }

            data.Initialize(mesh.vertices, mesh.triangles, assetId, sourceGuid);
            EditorUtility.SetDirty(data);

            var convex = target.GetComponent<AuraConvexColliderAuthoring>();
            if (convex != null)
            {
                convex.ApplyBakedMesh(data);
                EditorUtility.SetDirty(convex);
            }

            var meshCollider = target.GetComponent<AuraMeshColliderAuthoring>();
            if (meshCollider != null)
            {
                meshCollider.ApplyBakedMesh(data);
                EditorUtility.SetDirty(meshCollider);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"AuraEngine: baked '{mesh.name}' to {assetPath} (assetId={assetId}).");
        }

        private static int StableHash(string value)
        {
            unchecked
            {
                var hash = 2166136261u;
                foreach (var character in value)
                {
                    hash ^= character;
                    hash *= 16777619u;
                }

                return (int)(hash & 0x7FFFFFFF);
            }
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/AuraEngine.Demo"))
                AssetDatabase.CreateFolder("Assets", "AuraEngine.Demo");
            if (!AssetDatabase.IsValidFolder(BakedFolder))
                AssetDatabase.CreateFolder("Assets/AuraEngine.Demo", "Baked");
        }
    }
}
