using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IHealth {
    float MaxHP { get; }
    float HP { get; }

    delegate void HealthReachedMax();
    event HealthReachedMax OnHealthReachedMax;

    delegate void HealthReachedEmpty();
    event HealthReachedEmpty OnHealthReachedEmpty;

    float IncreaseHP(float amount);
    float DecreaseHP(float amount);
}
