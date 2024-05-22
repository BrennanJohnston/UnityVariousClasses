using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Allows for props that can be interacted with and destroyed without having to spawn and despawn objects.
/// Usable with DoExplosion, Flammable, and CrushableByTank.
/// </summary>
[RequireComponent(typeof(PropDamageable))]
public class DestructibleProp : NetworkBehaviour {
    [SerializeField] private GameObject _explosionEffectPrefab;
    [SerializeField] private GameObject[] _meshGameObjects;
    [SerializeField] private Collider _collider;
    [SerializeField] private float _resetTime;

    private PropDamageable propDamageable;
    private Explode explodableComponent;
    private Flammable flammableComponent;
    private CrushableByTank crushableComponent;
    private bool resetable = false;
    private float resetTimer = 0f;
    private NetworkVariable<DestructablePropState> propState = new NetworkVariable<DestructablePropState>(DestructablePropState.NotDestroyed);

    public enum DestructablePropState {
        NotDestroyed,
        Damaged,
        Crushed,
        Destroyed
    }

    void Awake() {
        propDamageable = GetComponent<PropDamageable>();
        explodableComponent = GetComponent<Explode>();
        flammableComponent = GetComponent<Flammable>();
        crushableComponent = GetComponent<CrushableByTank>();

        if (crushableComponent != null) {
            crushableComponent.GotCrushed += OnGotCrushed;
            crushableComponent.GotUnCrushed += OnGotUnCrushed;
        }
    }

    public override void OnNetworkSpawn() {
        base.OnNetworkSpawn();

        propDamageable.HPReachedEmpty += OnHPReachedEmpty;
        propDamageable.DamageTaken += OnDamageTaken;
        propState.OnValueChanged += OnPropStateChanged;
        if (_resetTime > 0f) resetable = true;

        SyncPropState();
    }

    public override void OnNetworkDespawn() {
        base.OnNetworkDespawn();

        propDamageable.HPReachedEmpty -= OnHPReachedEmpty;
        propDamageable.DamageTaken -= OnDamageTaken;
        propState.OnValueChanged -= OnPropStateChanged;
    }

    void Update() {
        if ((!IsServer && !IsHost) || !resetable || propState.Value != DestructablePropState.Destroyed) return;

        resetTimer -= Time.deltaTime;
        if (resetTimer <= 0f) UnDestroyProp();
    }

    public override void OnDestroy() {
        base.OnDestroy();

        if (crushableComponent != null) {
            crushableComponent.GotCrushed -= OnGotCrushed;
            crushableComponent.GotUnCrushed -= OnGotUnCrushed;
        }
    }

    private void DestroyProp() {
        if ((!IsServer && !IsHost) || propState.Value == DestructablePropState.Destroyed) return;

        propState.Value = DestructablePropState.Destroyed;
        if (explodableComponent != null) explodableComponent.DoExplosion();
        if (flammableComponent != null) flammableComponent.UnIgnite();
    }

    private void UnDestroyProp() {
        if ((!IsServer && !IsHost) || propState.Value == DestructablePropState.NotDestroyed) return;

        propState.Value = DestructablePropState.NotDestroyed;
        propDamageable.ResetHP();
        if (explodableComponent != null) explodableComponent.UndoExplosion();
        if (flammableComponent != null) flammableComponent.UnIgnite();
        if (crushableComponent != null) crushableComponent.UnCrush();
    }

    private void CrushProp() {
        if ((!IsServer && !IsHost) || propState.Value == DestructablePropState.Crushed) return;

        propState.Value = DestructablePropState.Crushed;
        if (flammableComponent != null) flammableComponent.UnIgnite();
    }

    private void OnPropStateChanged(DestructablePropState oldState, DestructablePropState newState) {
        SyncPropState();
    }

    private void SyncPropState() {
        switch (propState.Value) {
            case DestructablePropState.Destroyed:
            EnableMeshes(false);
            EnableCollider(false);
            InstantiateExplosionPrefab();
            if (resetable) resetTimer = _resetTime;
            break;

            case DestructablePropState.Damaged:

            break;

            case DestructablePropState.Crushed:
            EnableCollider(false);
            break;

            case DestructablePropState.NotDestroyed:
            EnableMeshes(true);
            EnableCollider(true);
            break;


        }
    }

    private void OnGotCrushed() {
        CrushProp();
    }

    private void OnGotUnCrushed() {
        UnDestroyProp();
    }

    private void OnDamageTaken() {
        if ((!IsServer && !IsHost) || propState.Value == DestructablePropState.Damaged) return;

        propState.Value = DestructablePropState.Damaged;
    }

    private void OnHPReachedEmpty() {
        if (!IsServer && !IsHost) return;

        DestroyProp();
    }

    private void EnableMeshes(bool enable) {
        for(int i = 0; i < _meshGameObjects.Length; i++) {
            GameObject meshGO = _meshGameObjects[i];
            meshGO.SetActive(enable);
        }
    }

    private void EnableCollider(bool enable) {
        if (_collider == null) return;

        _collider.enabled = enable;
    }

    private void InstantiateExplosionPrefab() {
        if (_explosionEffectPrefab == null) return;

        Instantiate(_explosionEffectPrefab, transform.position, transform.rotation);
    }
}