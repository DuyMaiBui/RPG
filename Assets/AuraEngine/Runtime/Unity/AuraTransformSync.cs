using AuraEngine.Core;
using UnityEngine;

namespace AuraEngine.Unity
{
    public static class AuraTransformSync
    {
        public static void Apply(Transform target, in AuraPose pose)
        {
            target.SetPositionAndRotation(
                new Vector3(pose.Position.X, pose.Position.Y, pose.Position.Z),
                new Quaternion(pose.Rotation.X, pose.Rotation.Y, pose.Rotation.Z, pose.Rotation.W));
        }

        public static AuraPose Capture(Transform source) =>
            new AuraPose(
                new AuraVector3(source.position.x, source.position.y, source.position.z),
                new AuraQuaternion(source.rotation.x, source.rotation.y, source.rotation.z, source.rotation.w));
    }
}
