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

void ColoredBoxCollider::OnDebugDrawSelf()
{
    DrawDebugBoxSelf(DebugColor);
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
