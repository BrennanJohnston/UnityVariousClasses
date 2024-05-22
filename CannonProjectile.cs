using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CannonProjectile : AProjectile {
    [Header("Required")]
    [Range(0f, 500f)]
    [SerializeField] float _speed;

    // Start is called before the first frame update
    void Start() {
        ProjectileRigidbody.velocity = transform.forward * _speed;
    }

    // Update is called once per frame
    /*
    void FixedUpdate() {
        //ProjectileRigidbody.position += transform.forward * _speed * Time.fixedDeltaTime;
    }
    */
}