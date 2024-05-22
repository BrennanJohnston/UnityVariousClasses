using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface ILookAt {
    bool LookingAt { get; }
    Vector3 LookingAtPosition { get; }
    void LookAt(Vector3 position);
}