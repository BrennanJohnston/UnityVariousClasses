using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Allows for this object to catch on fire and take damage over time.
/// Ignite() and UnIgnite() can be called externally to manually control this behaviour.
/// </summary>
[RequireComponent(typeof(ADamageable))]
public class Flammable : NetworkBehaviour {
    [Tooltip("How much damage per second this object should take on it's ADamageable component while ignited.")]
    [SerializeField] private float _dps;
    [Tooltip("True if the object should ignite upon taking any damage. False otherwise.")]
    [SerializeField] private bool _igniteOnDamaged;
    [Tooltip("Particle effects on this object that represent the object being on fire.")]
    [SerializeField] private ParticleSystem[] _flameEffectParticles;

    NetworkVariable<FlammableState> flameState = new NetworkVariable<FlammableState>(FlammableState.NotIgnited);
    private ADamageable damageableComponent;

    public enum FlammableState {
        NotIgnited,
        Ignited
    }

    void Awake() {
        damageableComponent = GetComponent<ADamageable>();
        damageableComponent.DamageTaken += OnDamageTaken;
        damageableComponent.HPReachedEmpty += OnHPReachedEmpty;
        EnableFlameEffectParticles(false);
    }

    public override void OnNetworkSpawn() {
        base.OnNetworkSpawn();

        flameState.OnValueChanged += OnFlameStateChanged;
        SyncFlameState();
    }

    public override void OnNetworkDespawn() {
        base.OnNetworkDespawn();

        flameState.OnValueChanged -= OnFlameStateChanged;
    }

    void Update() {
        if ((!IsServer && !IsHost) || damageableComponent == null || flameState.Value != FlammableState.Ignited) return;

        float damageToApply = _dps * Time.deltaTime;
        damageableComponent.TakeDamage(damageToApply, null);
    }

    public override void OnDestroy() {
        base.OnDestroy();

        damageableComponent.DamageTaken -= OnDamageTaken;
        damageableComponent.HPReachedEmpty -= OnHPReachedEmpty;
    }

    public void UnIgnite() {
        if ((!IsServer && !IsHost) || flameState.Value == FlammableState.NotIgnited) return;
        flameState.Value = FlammableState.NotIgnited;
    }

    public void Ignite() {
        if ((!IsServer && !IsHost) || flameState.Value == FlammableState.Ignited) return;
        flameState.Value = FlammableState.Ignited;
    }

    private void OnFlameStateChanged(FlammableState oldState, FlammableState newState) {
        SyncFlameState();
    }

    private void SyncFlameState() {
        switch (flameState.Value) {
            case FlammableState.Ignited:
            EnableFlameEffectParticles(true);
            break;

            case FlammableState.NotIgnited:
            EnableFlameEffectParticles(false);
            break;
        }
    }

    private void EnableFlameEffectParticles(bool enable) {
        for(int i = 0; i < _flameEffectParticles.Length; i++) {
            ParticleSystem particles = _flameEffectParticles[i];
            ParticleSystem.EmissionModule emissionModule = particles.emission;
            emissionModule.enabled = enable;
        }
    }

    private void OnDamageTaken() {
        if (_igniteOnDamaged) Ignite();
    }

    private void OnHPReachedEmpty() {
        UnIgnite();
    }
}