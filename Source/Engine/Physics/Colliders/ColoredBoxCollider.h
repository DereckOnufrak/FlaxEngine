// Copyright (c) Wojciech Figat. All rights reserved.

#pragma once

#include "BoxCollider.h"
#include "Engine/Core/Math/Color.h"

/// <summary>
/// A box-shaped primitive collider with a customizable debug draw color.
/// </summary>
/// <seealso cref="BoxCollider" />
API_CLASS(Attributes="ActorContextMenu(\"New/Physics/Colliders/Colored Box Collider\"), ActorToolbox(\"Physics\")")
class FLAXENGINE_API ColoredBoxCollider : public BoxCollider
{
    API_AUTO_SERIALIZATION();
    DECLARE_SCENE_OBJECT(ColoredBoxCollider);

public:
    /// <summary>
    /// The color used to draw the collider shape in the editor viewport (debug draw).
    /// </summary>
    API_FIELD(Attributes="EditorOrder(110), DefaultValue(typeof(Color), \"0.6784314,1,0.1843137,1\"), EditorDisplay(\"Collider\")")
    Color DebugColor = Color::GreenYellow;

public:
    // [BoxCollider]
#if USE_EDITOR
    void OnDebugDraw() override;
    void OnDebugDrawSelf() override;
    void OnDebugDrawSelected() override;
#endif

protected:
    // [BoxCollider]
    ImplementPhysicsDebug;
};
