// Copyright (c) Wojciech Figat. All rights reserved.

#include "ColoredBoxCollider.h"

ColoredBoxCollider::ColoredBoxCollider(const SpawnParams& params)
    : BoxCollider(params)
{
}

#if USE_EDITOR

#include "Engine/Debug/DebugDraw.h"
#include "Engine/Graphics/RenderView.h"

void ColoredBoxCollider::DrawPhysicsDebug(RenderView& view)
{
    const BoundingSphere sphere(_sphere.Center - view.Origin, _sphere.Radius);
    if (!view.CullingFrustum.Intersects(sphere))
        return;
    const OrientedBoundingBox bounds = GetOrientedBox();
    if (view.Mode == ViewMode::PhysicsColliders && !GetIsTrigger())
        DEBUG_DRAW_BOX(bounds, DebugColor, 0, true);
    else
        DEBUG_DRAW_WIRE_BOX(bounds, DebugColor * 0.8f, 0, true);
}

void ColoredBoxCollider::OnDebugDraw()
{
    if (GetIsTrigger())
    {
        DEBUG_DRAW_WIRE_BOX(GetOrientedBox(), DebugColor, 0, true);
    }

    // Skip BoxCollider implementation to avoid drawing with the default color
    Collider::OnDebugDraw();
}

namespace
{
    OrientedBoundingBox GetColoredWireBox(const Vector3& min, const Vector3& max, const float margin)
    {
        OrientedBoundingBox box;
        const Vector3 vec = max - min;
        const Vector3 dir = Float3::Normalize(vec);
        Quaternion orientation;
        if (Vector3::Dot(dir, Float3::Up) >= 0.999f)
            Quaternion::RotationAxis(Float3::Left, PI_HALF, orientation);
        else
            Quaternion::LookRotation(dir, Float3::Cross(Float3::Cross(dir, Float3::Up), dir), orientation);
        const Vector3 up = orientation * Vector3::Up;
        Matrix world;
        Matrix::CreateWorld(min + vec * 0.5f, dir, up, world);
        world.Decompose(box.Transformation);
        Matrix invWorld;
        Matrix::Invert(world, invWorld);
        Vector3 vecLocal;
        Vector3::TransformNormal(vec * 0.5f, invWorld, vecLocal);
        box.Extents.X = margin;
        box.Extents.Y = margin;
        box.Extents.Z = vecLocal.Z;
        return box;
    }
}

void ColoredBoxCollider::OnDebugDrawSelf()
{
    const Color color = DebugColor;
    const OrientedBoundingBox bounds = GetOrientedBox();
    DEBUG_DRAW_WIRE_BOX(bounds, color * 0.3f, 0, false);

    Vector3 corners[8];
    bounds.GetCorners(corners);
    const float margin = Math::Min(1.0f, (float)bounds.GetSize().MinValue() * 0.01f);
    const Color wiresColor = color.AlphaMultiplied(0.6f);
    if (margin > 0.05f)
    {
        DEBUG_DRAW_BOX(GetColoredWireBox(corners[0], corners[1], margin), wiresColor, 0, true);
        DEBUG_DRAW_BOX(GetColoredWireBox(corners[0], corners[3], margin), wiresColor, 0, true);
        DEBUG_DRAW_BOX(GetColoredWireBox(corners[0], corners[4], margin), wiresColor, 0, true);
        DEBUG_DRAW_BOX(GetColoredWireBox(corners[1], corners[2], margin), wiresColor, 0, true);
        DEBUG_DRAW_BOX(GetColoredWireBox(corners[1], corners[5], margin), wiresColor, 0, true);
        DEBUG_DRAW_BOX(GetColoredWireBox(corners[2], corners[3], margin), wiresColor, 0, true);
        DEBUG_DRAW_BOX(GetColoredWireBox(corners[2], corners[6], margin), wiresColor, 0, true);
        DEBUG_DRAW_BOX(GetColoredWireBox(corners[3], corners[7], margin), wiresColor, 0, true);
        DEBUG_DRAW_BOX(GetColoredWireBox(corners[4], corners[5], margin), wiresColor, 0, true);
        DEBUG_DRAW_BOX(GetColoredWireBox(corners[4], corners[7], margin), wiresColor, 0, true);
        DEBUG_DRAW_BOX(GetColoredWireBox(corners[5], corners[6], margin), wiresColor, 0, true);
        DEBUG_DRAW_BOX(GetColoredWireBox(corners[6], corners[7], margin), wiresColor, 0, true);
    }
    else
    {
        DEBUG_DRAW_WIRE_BOX(bounds, wiresColor, 0, true);
    }
}

void ColoredBoxCollider::OnDebugDrawSelected()
{
    OnDebugDrawSelf();

    if (_contactOffset > 0)
    {
        OrientedBoundingBox contactBounds = GetOrientedBox();
        contactBounds.Extents += Vector3(_contactOffset) / contactBounds.Transformation.Scale;
        DEBUG_DRAW_WIRE_BOX(contactBounds, DebugColor.AlphaMultiplied(0.2f), 0, false);
    }

    // Skip BoxCollider implementation to avoid drawing with the default color
    Collider::OnDebugDrawSelected();
}

#endif
