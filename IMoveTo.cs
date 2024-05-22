using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IMoveTo {
    bool MovingTo { get; }
    Vector3 MovingToPosition { get; }
    void MoveTo(Vector3 position);
}
