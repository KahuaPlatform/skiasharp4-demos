using System.Numerics;

namespace UnoGallery.Models;

/// <summary>
/// The computed on-screen placement of one gallery tile for the current frame.
/// Layouts produce these; the scene/effects pipeline consumes them. <see cref="Z"/>
/// drives depth sorting and <see cref="Sharpness"/> drives the depth-of-field blur.
/// </summary>
/// <param name="ItemId">Stable id of the gallery item this placement is for.</param>
/// <param name="Center">Screen-space center, in pixels.</param>
/// <param name="Size">Tile size, in pixels.</param>
/// <param name="Rotation">Rotation in radians.</param>
/// <param name="Z">Depth (painter's-order + DoF key).</param>
/// <param name="Opacity">Alpha in [0,1].</param>
/// <param name="Sharpness">1 = fully sharp, lower = more blurred.</param>
public readonly record struct ItemPlacement(
    int ItemId,
    Vector2 Center,
    Vector2 Size,
    float Rotation,
    float Z,
    float Opacity,
    float Sharpness);
