// Copyright (c) Wojciech Figat. All rights reserved.

#include "WedgeBrush.h"
#include "Engine/Core/Math/Matrix.h"
#include "Engine/Content/Content.h"
#include "Engine/Serialization/Serialization.h"
#include "Engine/Level/Scene/Scene.h"

void WedgeSurface::Serialize(SerializeStream& stream, const void* otherObj)
{
    SERIALIZE_GET_OTHER_OBJ(WedgeSurface);

    SERIALIZE(Material);
    SERIALIZE_MEMBER(Offset, TexCoordOffset);
    SERIALIZE_MEMBER(Scale, TexCoordScale);
    SERIALIZE_MEMBER(Rotation, TexCoordRotation);
    SERIALIZE_MEMBER(ScaleInLightmap, ScaleInLightmap);
}

void WedgeSurface::Deserialize(DeserializeStream& stream, ISerializeModifier* modifier)
{
    DESERIALIZE(Material);
    DESERIALIZE_MEMBER(Offset, TexCoordOffset);
    DESERIALIZE_MEMBER(Scale, TexCoordScale);
    DESERIALIZE_MEMBER(Rotation, TexCoordRotation);
    DESERIALIZE_MEMBER(ScaleInLightmap, ScaleInLightmap);
}

WedgeBrush::WedgeBrush(const SpawnParams& params)
    : Actor(params)
    , _center(Vector3::Zero)
    , _size(100.0f)
    , _slope(1.0f)
    , _mode(CSG::Mode::Additive)
{
    for (uint32 i = 0; i < ARRAY_COUNT(Surfaces); i++)
    {
        auto& surface = Surfaces[i];
        surface.Brush = this;
        surface.Index = i;
    }
}

Array<WedgeSurface> WedgeBrush::GetSurfaces() const
{
    Array<WedgeSurface> value;
    value.Set(Surfaces, ARRAY_COUNT(Surfaces));
    return value;
}

void WedgeBrush::SetSurfaces(const Array<WedgeSurface>& value)
{
    CHECK(value.Count() == ARRAY_COUNT(Surfaces));
    Platform::MemoryCopy(Surfaces, value.Get(), sizeof(Surfaces));
    OnBrushModified();
}

void WedgeBrush::SetMode(BrushMode value)
{
    if (_mode != value)
    {
        _mode = value;
        OnBrushModified();
    }
}

void WedgeBrush::SetCenter(const Vector3& value)
{
    if (value == _center)
        return;

    _center = value;

    // Fire events
    UpdateBounds();
    OnBrushModified();
}

void WedgeBrush::SetSize(const Vector3& value)
{
    if (value == _size)
        return;

    _size = value;

    // Fire events
    UpdateBounds();
    OnBrushModified();
}

void WedgeBrush::SetSlope(float value)
{
    if (Math::NearEqual(value, _slope))
        return;

    _slope = value;

    // Fire events (bounds are unaffected - the slope only cuts into the existing box)
    OnBrushModified();
}

void WedgeBrush::GetSurfaces(CSG::Surface surfaces[6])
{
    // Init normals (matches BoxBrush's face layout: 0=+X, 1=-X, 2=+Y (sloped), 3=-Y, 4=+Z (front, shrinks with slope), 5=-Z (back, stays full height))
    surfaces[0].Normal = Vector3::Right;
    surfaces[1].Normal = Vector3::Left;
    surfaces[2].Normal = Vector3::Up;
    surfaces[3].Normal = Vector3::Down;
    surfaces[4].Normal = Vector3::Forward;
    surfaces[5].Normal = Vector3::Backward;

    // Calculate final transformation
    const auto transform = _transform.LocalToWorld(Transform(_center, Quaternion::Identity, _size));

    // Set size and scale
    surfaces[0].D = surfaces[1].D = transform.Scale.X / 2;
    surfaces[2].D = surfaces[3].D = transform.Scale.Y / 2;
    surfaces[4].D = surfaces[5].D = transform.Scale.Z / 2;

    // Add rotation
    Matrix rotation;
    Matrix::RotationQuaternion(transform.Orientation, rotation);
    for (int32 i = 0; i < 6; i++)
        Vector3::TransformNormal(surfaces[i].Normal, rotation, surfaces[i].Normal);

    // Add translation
    for (int32 i = 0; i < 6; i++)
        surfaces[i].Translate(transform.Translation);

    // Replace the top surface with the sloped one. Built from 3 world-space points rather than by
    // analytically transforming the plane, since that stays correct under non-uniform actor scale
    // (which the axis-aligned faces above can shortcut, but a tilted normal cannot).
    {
        const Vector3 backLeftTop(-_size.X * 0.5f, _size.Y * 0.5f, -_size.Z * 0.5f);
        const Vector3 backRightTop(_size.X * 0.5f, _size.Y * 0.5f, -_size.Z * 0.5f);
        const Vector3 frontRightTop(_size.X * 0.5f, _size.Y * 0.5f - _slope * _size.Y, _size.Z * 0.5f);
        const Vector3 p1 = _transform.LocalToWorld(_center + backLeftTop);
        const Vector3 p2 = _transform.LocalToWorld(_center + backRightTop);
        const Vector3 p3 = _transform.LocalToWorld(_center + frontRightTop);

        Vector3 normal = Vector3::Cross(p3 - p1, p2 - p1);
        const Real normalLength = normal.Length();
        if (normalLength > ZeroTolerance)
            normal /= normalLength;
        else
            normal = Vector3::Up;

        surfaces[2].Normal = normal;
        surfaces[2].D = Vector3::Dot(normal, p1);
    }

    // Copy per surface properties
    for (int32 i = 0; i < 6; i++)
    {
        auto& dst = surfaces[i];
        auto& src = Surfaces[i];

        dst.Material = src.Material.GetID();
        dst.TexCoordScale = src.TexCoordScale;
        dst.TexCoordOffset = src.TexCoordOffset;
        dst.TexCoordRotation = src.TexCoordRotation;
        dst.ScaleInLightmap = src.ScaleInLightmap;
    }
}

void WedgeBrush::SetMaterial(int32 surfaceIndex, MaterialBase* material)
{
    CHECK(Math::IsInRange(surfaceIndex, 0, 5));
    Surfaces[surfaceIndex].Material = material;
    OnBrushModified();
}

bool WedgeBrush::Intersects(int32 surfaceIndex, const Ray& ray, Real& distance, Vector3& normal) const
{
    distance = MAX_Real;
    normal = Vector3::Up;
    auto scene = GetScene();
    CHECK_RETURN(scene, false);

    // Get surface data handle
    CSG::SceneCSGData::SurfaceData surfaceData;
    if (scene->CSGData.TryGetSurfaceData(GetBrushID(), surfaceIndex, surfaceData))
    {
        return surfaceData.Intersects(ray, distance, normal);
    }
    return false;
}

void WedgeBrush::GetVertices(int32 surfaceIndex, Array<Vector3>& outputData) const
{
    auto scene = GetScene();
    CHECK(scene);
    CSG::SceneCSGData::SurfaceData surfaceData;
    if (scene->CSGData.TryGetSurfaceData(GetBrushID(), surfaceIndex, surfaceData))
    {
        outputData.Add((Vector3*)surfaceData.Triangles.Get(), 3 * surfaceData.Triangles.Count());
    }
}

void WedgeBrush::Serialize(SerializeStream& stream, const void* otherObj)
{
    // Base
    Actor::Serialize(stream, otherObj);

    SERIALIZE_GET_OTHER_OBJ(WedgeBrush);

    SERIALIZE_MEMBER(Mode, _mode);
    SERIALIZE_MEMBER(Center, _center);
    SERIALIZE_MEMBER(Size, _size);
    SERIALIZE_MEMBER(Slope, _slope);
    SERIALIZE(ScaleInLightmap);

    stream.JKEY("Surfaces");
    stream.StartArray();
    for (int32 i = 0; i < 6; i++)
    {
        stream.Object(&Surfaces[i], other ? &other->Surfaces[i] : nullptr);
    }
    stream.EndArray();
}

void WedgeBrush::Deserialize(DeserializeStream& stream, ISerializeModifier* modifier)
{
    // Base
    Actor::Deserialize(stream, modifier);

    DESERIALIZE_MEMBER(Mode, _mode);
    DESERIALIZE_MEMBER(Center, _center);
    DESERIALIZE_MEMBER(Size, _size);
    DESERIALIZE_MEMBER(Slope, _slope);
    DESERIALIZE(ScaleInLightmap);

    const DeserializeStream& surfaces = stream["Surfaces"];
    ASSERT(surfaces.IsArray() && surfaces.Size() == ARRAY_COUNT(Surfaces));
    for (rapidjson::SizeType i = 0; i < surfaces.Size(); i++)
    {
        Surfaces[i].Deserialize((DeserializeStream&)surfaces[i], modifier);
    }
}

bool WedgeBrush::IntersectsItself(const Ray& ray, Real& distance, Vector3& normal)
{
    bool result = false;
    Real minDistance = MAX_float;
    Vector3 minDistanceNormal = Vector3::Up;
    if (_bounds.Intersects(ray, distance))
    {
        for (int32 surfaceIndex = 0; surfaceIndex < 6; surfaceIndex++)
        {
            if (Intersects(surfaceIndex, ray, distance, normal) && distance < minDistance)
            {
                minDistance = distance;
                minDistanceNormal = normal;
                result = true;
            }
        }
    }
    distance = minDistance;
    normal = minDistanceNormal;
    return result;
}

#if USE_EDITOR

#include "Engine/Debug/DebugDraw.h"

void WedgeBrush::OnDebugDrawSelected()
{
    DEBUG_DRAW_WIRE_BOX(_bounds, Color::Yellow, 0, false);

    // Base
    Actor::OnDebugDrawSelected();
}

#endif

Scene* WedgeBrush::GetBrushScene() const
{
    return GetScene();
}

Guid WedgeBrush::GetBrushID() const
{
    return GetID();
}

bool WedgeBrush::CanUseCSG() const
{
    return IsActiveInHierarchy();
}

CSG::Mode WedgeBrush::GetBrushMode() const
{
    return _mode;
}

void WedgeBrush::GetSurfaces(Array<CSG::Surface>& surfaces)
{
    surfaces.Clear();
    surfaces.Resize(6, false);

    GetSurfaces(surfaces.Get());
}

int32 WedgeBrush::GetSurfacesCount()
{
    return 6;
}

void WedgeBrush::OnTransformChanged()
{
    // Base
    Actor::OnTransformChanged();

    // Fire events
    UpdateBounds();
    OnBrushModified();
}

void WedgeBrush::OnActiveInTreeChanged()
{
    // Base
    Actor::OnActiveInTreeChanged();

    // Fire event
    OnBrushModified();
}

void WedgeBrush::OnOrderInParentChanged()
{
    // Base
    Actor::OnOrderInParentChanged();

    // Fire event
    OnBrushModified();
}

void WedgeBrush::OnParentChanged()
{
    // Base
    Actor::OnParentChanged();

    if (!IsDuringPlay())
        return;

    // Fire event
    OnBrushModified();
}
