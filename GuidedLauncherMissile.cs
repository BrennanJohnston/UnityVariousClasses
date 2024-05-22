using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(Rigidbody))]
public class GuidedLauncherMissile : AProjectile {
    [SerializeField] private GameObject[] _flameGameObjects;
    [Range(0.1f, 1000f)]
    [SerializeField] private float _rotationDegreesPerSecond;
    [Range(0.1f, 500f)]
    [SerializeField] private float _accelerationSpeed;
    [Range(0.1f, 1000f)]
    [SerializeField] private float _maxVelocity;
    [Range(0.1f, 200f)]
    [SerializeField] private float _launchVelocity;

    private GameObject targetGO = null;
    private float currentVelocity = 0f;
    private float ignitionTime = 0.5f; // seconds
    private const float preIgnitionRotationSpeed = 60f; // degrees per second
    private const float preIgnitionDesiredAngle = 60f;
    private MissileState State = MissileState.NotIgnited;
    private Quaternion preIgnitionDesiredRotation;

    private NetworkObject missileNO;
    private TrailRenderer trailRenderer;

    public enum MissileState {
        NotIgnited,
        Ignited
    }

    protected override void Awake() {
        base.Awake();

        missileNO = GetComponent<NetworkObject>();
        trailRenderer = GetComponentInChildren<TrailRenderer>();
        if (trailRenderer != null) trailRenderer.enabled = false;

        for(int i = 0; i < _flameGameObjects.Length; i++) {
            GameObject flame = _flameGameObjects[i];
            flame.SetActive(false);
        }
        //rigidBody.isKinematic = true;
    }

    public override void OnNetworkSpawn() {
        base.OnNetworkSpawn();
    }

    void Start() {
        if (!IsOwner) return;
        Vector3 launchVector = transform.forward * _launchVelocity;
        ProjectileRigidbody.AddForce(launchVector, ForceMode.Impulse);
        //preIgnitionDesiredRotation = ProjectileRigidbody.rotation * Quaternion.Euler(preIgnitionDesiredAngle, 0f, 0f);
        preIgnitionDesiredRotation = transform.rotation * Quaternion.Euler(preIgnitionDesiredAngle, 0f, 0f);
    }

    void FixedUpdate() {
        if (IsOwner) {
            if (State == MissileState.NotIgnited) {
                ignitionTime -= Time.fixedDeltaTime;

                float rotateStep = preIgnitionRotationSpeed * Time.fixedDeltaTime;
                //ProjectileRigidbody.rotation = Quaternion.RotateTowards(ProjectileRigidbody.rotation, preIgnitionDesiredRotation, rotateStep);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, preIgnitionDesiredRotation, rotateStep);

                if (ignitionTime < 0f) {
                    State = MissileState.Ignited;
                    currentVelocity = ProjectileRigidbody.velocity.magnitude * Time.fixedDeltaTime;
                    ProjectileRigidbody.isKinematic = true;
                    EnableRocketFlameEffectRpc();
                }

            } else if (State == MissileState.Ignited) {
                currentVelocity += _accelerationSpeed * Time.fixedDeltaTime;
                currentVelocity = Mathf.Min(currentVelocity, _maxVelocity * Time.fixedDeltaTime);
                Vector3 velocityVector = transform.forward * currentVelocity;
                //ProjectileRigidbody.MovePosition(ProjectileRigidbody.position + velocityVector);
                transform.position += velocityVector;

                if (targetGO == null) return;
                Vector3 toTarget = targetGO.transform.position - transform.position;
                Quaternion desiredRotation = Quaternion.LookRotation(toTarget);
                //ProjectileRigidbody.rotation = Quaternion.RotateTowards(ProjectileRigidbody.rotation, desiredRotation, _rotationDegreesPerSecond * Time.fixedDeltaTime);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, desiredRotation, _rotationDegreesPerSecond * Time.fixedDeltaTime);
            }
        }
    }

    [Rpc(SendTo.Everyone)]
    private void EnableRocketFlameEffectRpc() {
        if (trailRenderer != null) trailRenderer.enabled = true;
        for (int i = 0; i < _flameGameObjects.Length; i++) {
            GameObject flame = _flameGameObjects[i];
            flame.SetActive(true);
        }
    }

    /// <summary>
    /// <para>Functional on: Owner</para>
    /// </summary>
    /// <param name="targetNO"></param>
    public void SetTarget(NetworkObject targetNO) {
        if (!IsOwner || targetNO == null) return;
        targetGO = targetNO.gameObject;
        //SetTargetAllRpc(targetNO);
    }

    [Rpc(SendTo.NotOwner)]
    private void SetTargetAllRpc(NetworkObjectReference targetNOR) {
        NetworkObject targetNO;
        if (!targetNOR.TryGet(out targetNO)) return;

        targetGO = targetNO.gameObject;
    }
}