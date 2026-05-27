using System.Numerics;

namespace UnoGallery.Models;

public readonly record struct ItemPlacement(
    int ItemId,
    Vector2 Center,
    Vector2 Size,
    float Rotation,
    float Z,
    float Opacity,
    float Sharpness);
