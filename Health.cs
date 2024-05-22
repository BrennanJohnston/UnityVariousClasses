using UnityEngine;

public class Health : IHealth {
    private float _maxHP;
    private float _hp;

    public float MaxHP {
        get { return _maxHP; }
        private set {
            _maxHP = Mathf.Max(0.0f, value);
        }
    }

    public float HP {
        get { return _hp; }
        private set {
            _hp = Mathf.Clamp(value, 0.0f, _maxHP);
        }
    }

    public event IHealth.HealthReachedMax OnHealthReachedMax;
    public event IHealth.HealthReachedEmpty OnHealthReachedEmpty;

    public Health(float maxHP) {
        MaxHP = maxHP;
        HP = MaxHP;
    }
    
    public float DecreaseHP(float amount) {
        amount = Mathf.Max(0.0f, amount);
        float hpAfterDecrease = HP - amount;
        float amountNotApplied = Mathf.Min(hpAfterDecrease, 0.0f);
        if (amountNotApplied < 0.0f) {
            amountNotApplied *= -1;
            if (amountNotApplied != amount && OnHealthReachedEmpty != null)
                OnHealthReachedEmpty();
        }
        HP -= amount;
        return amountNotApplied;
    }

    public float IncreaseHP(float amount) {
        amount = Mathf.Max(0.0f, amount);
        float hpAfterIncrease = HP + amount;
        float amountNotApplied = hpAfterIncrease > MaxHP ? hpAfterIncrease - MaxHP : 0.0f;
        if(amountNotApplied > 0.0f) {
            if (OnHealthReachedMax != null)
                OnHealthReachedMax();
        }
        HP += amount;
        return amountNotApplied;
    }
}