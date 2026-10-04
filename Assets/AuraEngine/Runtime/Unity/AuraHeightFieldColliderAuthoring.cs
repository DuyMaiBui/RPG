using AuraEngine.Core;
using UnityEngine;

namespace AuraEngine.Unity
{
    public sealed class AuraHeightFieldColliderAuthoring : AuraColliderAuthoring
    {
        [SerializeField]
        [Min(2)]
        private int _resolution = 4;

        [SerializeField]
        private Vector3 _size = new Vector3(10f, 1f, 10f);

        [SerializeField]
        private float[] _heights;

        public override AuraShapeType ShapeType => AuraShapeType.HeightField;

        protected override AuraShapeGeometry Geometry
        {
            get
            {
                var resolution = Mathf.Max(2, _resolution);
                var samples = BuildSamples(resolution);
                var stepX = _size.x / (resolution - 1);
                var stepZ = _size.z / (resolution - 1);
                var scale = new AuraVector3(stepX, _size.y, stepZ);
                return AuraShapeGeometry.HeightField(samples, resolution, scale);
            }
        }

        private float[] BuildSamples(int resolution)
        {
            var count = resolution * resolution;
            var samples = new float[count];
            if (_heights != null && _heights.Length == count)
                System.Array.Copy(_heights, samples, count);

            return samples;
        }
    }
}
