using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Allows this GameObject to cause damage to ADamageable's in a given radius to other GameObject's when DoExplosion() is called.
/// </summary>
public class Explode : NetworkBehaviour {
    [SerializeField] private float _explosionDamageMin;
    [SerializeField] private float _explosionDamageMax;
    [SerializeField] private float _explosionRadius;
    [Tooltip("True if this component should handle ADie/Despawn of this object upon exploding.")]
    [SerializeField] private bool _destroyOnExplode = false;
    [Tooltip("Layers that should be checked when determining what to cause explosion damage to.")]
    [SerializeField] private LayerMask _explodeLayers;

    bool isExploded = false;
    private TankTeamAssignment tankTeamAssignmentComponent;

    void Awake() {
        tankTeamAssignmentComponent = GetComponent<TankTeamAssignment>();
    }

    public void UndoExplosion() {
        isExploded = false;
    }

    public void DoExplosion() {
        if ((!IsServer && !IsHost) || isExploded) return;
        isExploded = true;

        Collider[] collidersHit = Physics.OverlapSphere(transform.position, _explosionRadius, _explodeLayers);
        HashSet<Transform> rootTransformSet = new HashSet<Transform>();
        rootTransformSet.Add(transform.root); // add self so we don't explode ourselves
        for(int i = 0; i < collidersHit.Length; i++) {
            Collider collider = collidersHit[i];

            Transform rootTransform = collider.transform.root;
            if (rootTransformSet.Contains(rootTransform)) continue;

            rootTransformSet.Add(rootTransform);
            ADamageable damageable = rootTransform.GetComponent<ADamageable>();
            if (damageable == null) continue;

            // check if damage should be done to the object
            bool doDamage = ShouldDoDamage(damageable.gameObject);

            if(doDamage)
                damageable.TakeDamage(Random.Range(_explosionDamageMin, _explosionDamageMax), null);
        }

        if (!_destroyOnExplode) return;

        ANetworkDie networkDie = GetComponent<ANetworkDie>();
        if(networkDie != null) {
            networkDie.NetworkDie(new DeathInfo());
        } else {
            if(NetworkObject.IsSpawned) NetworkObject.Despawn();
        }
    }

    private bool ShouldDoDamage(GameObject collidedObject) {
        // Friendly fire processing
        bool doDamage = true;
        AMatchLogic matchLogic = TankRelay.Singleton?.MatchLogic;
        if (matchLogic == null || matchLogic.FriendlyFireEnabled == true) return true;

        // Since friendlyfire is not enabled, make sure we aren't shooting a teammate
        //TankTeam projectileTeam = matchLogic.TeamManager.GetTeamByClientID(OwnerClientId);
        if (tankTeamAssignmentComponent == null || tankTeamAssignmentComponent.AssignedTankTeam == null) return true;

        int projectileTeamID = tankTeamAssignmentComponent.TankTeamID;
        NetworkObject collidedNetworkObject = collidedObject.GetComponentInParent<NetworkObject>();
        if (collidedNetworkObject == null) return true;

        //TankTeam collidedTeam = matchLogic.TeamManager.GetTeamByClientID(collidedNetworkObject.OwnerClientId);
        TankTeamAssignment tankTeamAssignmentCollidedWith = collidedObject.GetComponentInParent<TankTeamAssignment>();
        if (tankTeamAssignmentCollidedWith == null) return true;
        //if (collidedObject.TryGetComponent(out tankTeamAssignmentCollidedWith) == false) return true;

        int collidedTeamID = tankTeamAssignmentCollidedWith.TankTeamID;
        if (projectileTeamID == collidedTeamID) doDamage = false;

        return doDamage;
    }
}