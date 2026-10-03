#include "aura_jolt_internal.h"

namespace aura
{

/* Shape construction for the Jolt backend. Kept in its own translation unit so
   new shapes/wrappers (height field, offset-center-of-mass, scaled, ...) can be
   added without touching the world, query or contact units. */
JPH::RefConst<JPH::Shape> MakeShape(const AuraShapeDesc& shape, bool& sensor)
{
    sensor = shape.isTrigger != 0;
    switch (shape.type)
    {
        case AURA_SHAPE_SPHERE:
            return new JPH::SphereShape(shape.radius);
        case AURA_SHAPE_CAPSULE:
        {
            const float halfHeight = std::max(0.0f, shape.height * 0.5f - shape.radius);
            return new JPH::CapsuleShape(halfHeight, shape.radius);
        }
        case AURA_SHAPE_CYLINDER:
            return new JPH::CylinderShape(std::max(0.0f, shape.height * 0.5f), shape.radius);
        case AURA_SHAPE_PLANE:
        {
            JPH::Vec3 normal(shape.planeNormal.x, shape.planeNormal.y, shape.planeNormal.z);
            if (normal.LengthSq() <= 1e-12f)
                return JPH::RefConst<JPH::Shape>();
            return new JPH::PlaneShape(JPH::Plane(normal.Normalized(), 0.0f));
        }
        case AURA_SHAPE_TAPERED_CAPSULE:
        {
            const float half = std::max(0.0f, (shape.height - shape.topRadius - shape.radius) * 0.5f);
            JPH::TaperedCapsuleShapeSettings settings(half, shape.topRadius, shape.radius);
            JPH::ShapeSettings::ShapeResult result = settings.Create();
            return result.IsValid() ? result.Get() : JPH::RefConst<JPH::Shape>();
        }
        case AURA_SHAPE_TAPERED_CYLINDER:
        {
            const float half = std::max(0.0f, shape.height * 0.5f);
            JPH::TaperedCylinderShapeSettings settings(half, shape.topRadius, shape.radius);
            JPH::ShapeSettings::ShapeResult result = settings.Create();
            return result.IsValid() ? result.Get() : JPH::RefConst<JPH::Shape>();
        }
        case AURA_SHAPE_CONVEX_MESH:
        {
            if (shape.vertices == nullptr || shape.vertexCount < 4)
                return JPH::RefConst<JPH::Shape>();

            JPH::Array<JPH::Vec3> points;
            points.reserve(shape.vertexCount);
            for (uint32_t i = 0; i < shape.vertexCount; ++i)
                points.push_back(JPH::Vec3(shape.vertices[i * 3], shape.vertices[i * 3 + 1], shape.vertices[i * 3 + 2]));

            JPH::ConvexHullShapeSettings settings(points);
            JPH::ShapeSettings::ShapeResult result = settings.Create();
            return result.IsValid() ? result.Get() : JPH::RefConst<JPH::Shape>();
        }
        case AURA_SHAPE_HEIGHT_FIELD:
        {
            if (shape.vertices == nullptr)
                return JPH::RefConst<JPH::Shape>();

            uint32_t sampleCount = 0;
            while ((sampleCount + 1) * (sampleCount + 1) <= shape.vertexCount)
                ++sampleCount;
            if (sampleCount < 2 || sampleCount * sampleCount != shape.vertexCount)
                return JPH::RefConst<JPH::Shape>();

            const auto* samples = reinterpret_cast<const float*>(shape.vertices);
            const JPH::Vec3 scale(shape.halfExtents.x, shape.halfExtents.y, shape.halfExtents.z);
            std::vector<uint8_t> materialIndices;
            const uint32_t squareCount = (sampleCount - 1) * (sampleCount - 1);
            if (shape.materialIndices != nullptr && shape.materialIndexCount >= squareCount)
            {
                materialIndices.reserve(squareCount);
                for (uint32_t i = 0; i < squareCount; ++i)
                    materialIndices.push_back(static_cast<uint8_t>(std::min(shape.materialIndices[i], 255u)));
            }
            JPH::PhysicsMaterialList materials;
            uint32_t maxMaterial = 0;
            for (uint32_t i = 0; i < materialIndices.size(); ++i)
                maxMaterial = std::max(maxMaterial, static_cast<uint32_t>(materialIndices[i]));
            for (uint32_t i = 0; i <= maxMaterial; ++i)
                materials.push_back(JPH::PhysicsMaterial::sDefault);
            JPH::HeightFieldShapeSettings settings = materialIndices.empty()
                ? JPH::HeightFieldShapeSettings(samples, JPH::Vec3::sZero(), scale, sampleCount)
                : JPH::HeightFieldShapeSettings(samples, JPH::Vec3::sZero(), scale, sampleCount, materialIndices.data(), materials);
            if (shape.activeEdgeCosThresholdAngle > 0.0f)
                settings.mActiveEdgeCosThresholdAngle = shape.activeEdgeCosThresholdAngle;
            JPH::ShapeSettings::ShapeResult result = settings.Create();
            return result.IsValid() ? result.Get() : JPH::RefConst<JPH::Shape>();
        }
        case AURA_SHAPE_TRIANGLE_MESH:
        {
            if (shape.vertices == nullptr || shape.vertexCount < 3 || shape.indices == nullptr || shape.indexCount < 3)
                return JPH::RefConst<JPH::Shape>();

            JPH::VertexList vertices;
            vertices.reserve(shape.vertexCount);
            for (uint32_t i = 0; i < shape.vertexCount; ++i)
                vertices.push_back(JPH::Float3(shape.vertices[i * 3], shape.vertices[i * 3 + 1], shape.vertices[i * 3 + 2]));

            JPH::IndexedTriangleList triangles;
            triangles.reserve(shape.indexCount / 3);
            for (uint32_t i = 0; i + 2 < shape.indexCount; i += 3)
            {
                const uint32_t triangle = i / 3;
                const uint32_t material = shape.materialIndices != nullptr && triangle < shape.materialIndexCount
                    ? shape.materialIndices[triangle] : 0u;
                triangles.push_back(JPH::IndexedTriangle(shape.indices[i], shape.indices[i + 1], shape.indices[i + 2], material));
            }

            JPH::PhysicsMaterialList materials;
            uint32_t maxMaterial = 0;
            for (const JPH::IndexedTriangle& triangle : triangles)
                maxMaterial = std::max(maxMaterial, triangle.mMaterialIndex);
            for (uint32_t i = 0; i <= maxMaterial; ++i)
                materials.push_back(JPH::PhysicsMaterial::sDefault);
            JPH::MeshShapeSettings settings(vertices, triangles, materials);
            if (shape.activeEdgeCosThresholdAngle > 0.0f)
                settings.mActiveEdgeCosThresholdAngle = shape.activeEdgeCosThresholdAngle;
            JPH::ShapeSettings::ShapeResult result = settings.Create();
            return result.IsValid() ? result.Get() : JPH::RefConst<JPH::Shape>();
        }
        default:
            return new JPH::BoxShape(JPH::Vec3(shape.halfExtents.x, shape.halfExtents.y, shape.halfExtents.z));
    }
}

JPH::RefConst<JPH::Shape> JoltWorld::Impl::BuildShape(const AuraBodyDesc& desc, bool& anySensor)
{
    anySensor = false;

    if (desc.shapeCount == 1)
    {
        bool sensor = false;
        JPH::RefConst<JPH::Shape> shape = MakeShape(desc.shapes[0], sensor);
        anySensor = sensor;
        return shape;
    }

    JPH::StaticCompoundShapeSettings settings;
    for (uint32_t i = 0; i < desc.shapeCount; ++i)
    {
        const AuraShapeDesc& shape = desc.shapes[i];
        bool sensor = false;
        JPH::RefConst<JPH::Shape> subShape = MakeShape(shape, sensor);
        if (sensor)
            anySensor = true;
        if (subShape == nullptr)
            continue;

        settings.AddShape(ToVec3(shape.localPose.position), ToQuat(shape.localPose.rotation), subShape.GetPtr());
    }

    JPH::ShapeSettings::ShapeResult result = settings.Create();
    return result.IsValid() ? result.Get() : JPH::RefConst<JPH::Shape>();
}

} // namespace aura
