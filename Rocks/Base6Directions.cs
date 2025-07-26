using g4;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SpaceEditor.Rocks;

public static class Base6Directions
{
    public const int Forward = 0;
    public const int Backward = 1;
    public const int Left = 2;
    public const int Right = 3;
    public const int Up = 4;
    public const int Down = 5;

    public static readonly Vector3i[] Vectors =
    [
        new( 0,  0, -1), //Forward 0
        new( 0,  0,  1), //Backward 1
        new(-1,  0,  0), //Left 2
        new( 1,  0,  0), //Right 3
        new( 0,  1,  0), //Up 4
        new( 0, -1,  0) //Down 5
    ];

    public static int Invert(int direction)
    {
        return direction switch
        {
            Forward => Backward,
            Backward => Forward,
            Left => Right,
            Right => Left,
            Up => Down,
            Down => Up,
        };
    }

    public static Matrix3d MakeRotationMatrix(int up, int right)
    {
        return MakeRotationMatrix(Vectors[up], Vectors[right]);
    }

    public static Matrix3d MakeRotationMatrix(Vector3i up, Vector3i right)
    {
        return MakeRotationMatrix(up.ToVector3d(), right.ToVector3d());
    }

    public static Matrix3d MakeRotationMatrix(Vector3d up, Vector3d right)
    {
        return new
        (
            right,
            up,
            right.Cross(up),
            bRows: false
        );
    }
}