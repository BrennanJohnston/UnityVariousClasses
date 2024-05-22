using UnityEngine;
using Unity.Netcode;

public class FauxSuspension : MonoBehaviour {
    [Tooltip("Transforms that will be effected by the Faux Suspension.  IMPORTANT: Only input transforms that do not directly have movement-related scripts attached.  " +
        "This script is meant to be a visual effect, and not directly effect gameplay.  If you have meshes you want to cause wobble to, simply child them to " +
        "your GameObject and put references to them into this parameter.")]
    [SerializeField] private Transform[] _transforms;
    [Tooltip("The maximum degree rotation that can occur to the X-Axis (forward/back rotation).  " +
        "Not certain what usecase there is for up to 100 degrees, let alone more, but increasing this threshold won't break anything if you want to.")]
    [Range(0f, 100f)]
    [SerializeField] private float _xDegreesMax;
    [Tooltip("The maximum degree rotation that can occur to the Z-Axis (left/right rotation).  " +
        "Not certain what usecase there is for up to 100 degrees, let alone more, but increasing this threshold won't break anything if you want to.")]
    [Range(0f, 100f)]
    [SerializeField] private float _zDegreesMax;
    [Tooltip("Stiffness for the X-Axis (forward/back rotation). Experiment with this value to get desired results.")]
    [Range(0f, 800f)]
    [SerializeField] private float _xStiffness;
    [Tooltip("Stiffness for the Z-Axis (left/right rotation).  Experiment with this value to get desired results.")]
    [Range(0f, 800f)]
    [SerializeField] private float _zStiffness;
    [Tooltip("Dampening for the Faux Suspension.  Lesser value makes wobbling last longer and more pronounced, higher value reduces wobble time and makes it less pronounced.  " +
        "Experiment with this value to get desired results.")]
    [Range(0f, 50f)]
    [SerializeField] private float _dampening;
    [Tooltip("The maximum force that is applied to the Faux Suspension in ApplyForce() calls.  ApplyForce() 'force' parameter is directly effected by this.  " +
        "Inputting 0f 'force' into ApplyForce() will apply 0 _maxForce, while inputting 1f will apply _maxForce amount.  " +
        "Experiment with this value to get desired results.")]
    [Range(0f, 200f)]
    [SerializeField] private float _maxForce;
    [Tooltip("The maximum force that is applied to the Faux Suspension in ApplyImpulse() calls. ApplyImpulse() 'force' parameter is directly effected by this.  " +
        "Inputting 0f 'force' into ApplyImpulse() will apply 0 _maxForce, while inputting 1f will apply _maxForce amount.  " +
        "Experiment with this value to get desired results.")]
    [Range(0f, 200f)]
    [SerializeField] private float _maxImpulseForce;

    private Vector2[] wobblePoints;
    private Vector2[] wobbleVelocities;
    private Vector3[] originalRotationEulers;

    void Awake() {
        originalRotationEulers = new Vector3[_transforms.Length];
        wobblePoints = new Vector2[_transforms.Length];
        wobbleVelocities = new Vector2[_transforms.Length];
        for(int i = 0; i < _transforms.Length; i++) {
            originalRotationEulers[i] = _transforms[i].localRotation.eulerAngles;
            wobblePoints[i] = new Vector2();
            wobbleVelocities[i] = new Vector2();
        }
    }

    void FixedUpdate() {
        // Having a "DoXAndY()" function is bad practice (functions should do one thing), but otherwise we'd have to iterate the array twice,
        // which sucks worse since this runs in FixedUpdate.
        ApplyGravityAndDampening();
        UpdateWobblePoints();
        UpdateTransformRotations();
    }

    // IF YOU WANT TO USE THESE TWO RPC'S, EXTEND NetworkBehaviour INSTEAD OF MonoBehaviour IN THE CLASS DEFINITION ABOVE.

    /*
    /// <summary>
    /// RPC to make clients ApplyImpulse locally.  Use this instead of making your FauxSuspension transforms be networked because it's WAY more efficient
    /// than using a networked transform (also looks better).
    /// I highly recommend only using this RPC if you 100% do not need to do anything else at the same time as the RPC.
    /// Example: In the game I made this for, Clients can fire their weapons, which sends an RPC to the Server that they are firing a weapon.
    /// If the server determines they can fire their weapon, the Server spawns projectile(s) and sends an RPC to all Clients that that specific player
    /// fired that specific weapon.  The Client RPC calls ApplyImpulse() (NOT ApplyImpulseClientRpc()) within it, since that RPC also handles triggering
    /// sound effects, particles, etc.
    /// </summary>
    /// <param name="globalDirection"></param>
    /// <param name="force"></param>
    [ClientRpc]
    public void ApplyImpulseClientRpc(Vector3 globalDirection, float force) {
        ApplyImpulse(globalDirection, force);
    }

    /// IMPORTANT: For ApplyForce(), since it's something done every frame, I HIGHLY HIGHLY RECOMMEND you do NOT use this RPC.
    /// This script is inherently a visual effect and shouldn't be used for significant gameplay impact, thus, it does not need to be network syncronized.
    /// That being said, determine ApplyForce() parameters on local clients based on velocities or whatever.  Just use ApplyForce() locally, don't use this RPC.
    [ClientRpc]
    public void ApplyForceClientRpc(Vector3 globalDirection, float force) {
        ApplyForce(globalDirection, force);
    }
    */

    /// <summary>
    /// Apply an instanteous force to the Faux Suspension.  Direction magnitude is not relevant as long as it is > 0.  Force is clamped between 0f and 1f.
    /// Adjust Max Impulse Force to change strength.
    /// </summary>
    /// <param name="globalDirection"></param>
    /// <param name="force"></param>
    public void ApplyImpulse(Vector3 globalDirection, float force) {
        for (int i = 0; i < wobbleVelocities.Length; i++) {
            Vector2 wobbleVelocity = wobbleVelocities[i];

            // change direction of globalDirection based on the transform associated with this wobbleVelocity
            Vector2 localizedDirection = GlobalToLocalVector(globalDirection, _transforms[i].forward);

            wobbleVelocity = ApplyImpulseToVelocity(wobbleVelocity, localizedDirection, force);

            wobbleVelocities[i] = wobbleVelocity;
        }
    }

    /// <summary>
    /// Apply force over time.  Call this consistently in FixedUpdate() as desired.  Direction magnitude is not relevant as long as it is > 0.  Force is clamped between 0f and 1f.
    /// Useful for movements of vehicle etc.  Adjust Max Force to change strength.
    /// </summary>
    /// <param name="globalDirection"></param>
    /// <param name="force"></param>
    public void ApplyForce(Vector3 globalDirection, float force) {
        for (int i = 0; i < wobbleVelocities.Length; i++) {
            Vector2 wobbleVelocity = wobbleVelocities[i];

            // change direction of globalDirection based on the transform associated with this wobbleVelocity
            Vector2 localizedDirection = GlobalToLocalVector(globalDirection, _transforms[i].forward);

            wobbleVelocity = ApplyForceToVelocity(wobbleVelocity, localizedDirection, force);

            wobbleVelocities[i] = wobbleVelocity;
        }
    }

    /// <summary>
    /// Applies gravity and dampening in one function.  Typically, you would split the two into separate functions, but that would mean we iterate the array twice.  No thanks.
    /// </summary>
    private void ApplyGravityAndDampening() {
        for (int i = 0; i < wobblePoints.Length; i++) {
            Vector2 wobblePoint = wobblePoints[i];
            Vector2 wobbleVelocity = wobbleVelocities[i];

            // Apply consistent gravity to push x toward 0, using _xStiffness scalar
            Vector2 xGravityDirection = new Vector2(-wobblePoint.x, 0f);
            wobbleVelocity = ApplyForceAdvanced(wobbleVelocity, xGravityDirection, Mathf.Abs(wobblePoint.x), _xStiffness);

            // Apply consistent gravity to push y toward 0, using _zStiffness scalar
            Vector2 yGravityDirection = new Vector2(0f, -wobblePoint.y);
            wobbleVelocity = ApplyForceAdvanced(wobbleVelocity, yGravityDirection, Mathf.Abs(wobblePoint.y), _zStiffness);

            // Apply consistent force directly against the velocity vector for dampening, using _dampening scalar
            Vector2 dampeningDirection = wobbleVelocity * -1;
            wobbleVelocity = ApplyForceAdvanced(wobbleVelocity, dampeningDirection, 1f, wobbleVelocity.magnitude * _dampening);

            wobbleVelocities[i] = wobbleVelocity;
        }
    }

    /// <summary>
    /// Apply an instanteous force to the provided wobbleVelocity.  Direction magnitude is not relevant as long as > 0, and force is clamped between 0f and 1f.
    /// Adjust Max Impulse Force to change strength.
    /// </summary>
    /// <param name="direction"></param>
    /// <param name="force"></param>
    private Vector2 ApplyImpulseToVelocity(Vector2 wobbleVelocity, Vector2 direction, float force) {
        force = ClampForceAmount(force);
        wobbleVelocity += direction.normalized * _maxImpulseForce * force;
        return wobbleVelocity;
    }

    /// <summary>
    /// Apply force over time.  Call in FixedUpdate().  Direction magnitude is not relevant as long as it is > 0, and force is clamped between 0f and 1f.
    /// Useful for movements of vehicle etc.  Adjust Max Force to change strength.
    /// </summary>
    /// <param name="direction"></param>
    /// <param name="force"></param>
    private Vector2 ApplyForceToVelocity(Vector2 wobbleVelocity, Vector2 direction, float force) {
        force = ClampForceAmount(force);
        wobbleVelocity += direction.normalized * _maxForce * force * Time.fixedDeltaTime;
        return wobbleVelocity;
    }

    /// <summary>
    /// Does what the name suggests. Comments provided to try and make sense of it.
    /// </summary>
    /// <param name="globalDirection"></param>
    /// <param name="globalFacingDirection"></param>
    /// <returns>Localized Vector2 of the globalDirection</returns>
    private Vector2 GlobalToLocalVector(Vector3 globalDirection, Vector3 globalFacingDirection) {
        // Taking the globalFacingDirection and extracting the X and Z values, we don't care about Y.
        Vector2 globalFacingDirectionV2 = new Vector2(globalFacingDirection.x, globalFacingDirection.z);

        // Getting the angle offset from the globalFacingDirection Vector2 from facing directly "forward" (Vector2.up).
        // The wobblePoints used to determine transform rotation are all considered always facing locally "forward", so we need to determine the offset
        // that the input globalFacingDirection is from local "forward" to correctly effect the transform.
        float angleOffset = Vector2.SignedAngle(globalFacingDirectionV2, Vector2.up);

        // We don't care about Y in the globalDirection Vector3 either.
        globalDirection.y = 0f;

        // Using the angleOffset we calculated, we create a Quaternion rotation based on it.
        // We then multiply globalDirection by this Quaternion, which rotates the globalDirection vector by that Quaternion rotation.
        // This readjusts the globalDirection vector to be the direction we desire.
        globalDirection = Quaternion.AngleAxis(-angleOffset, Vector3.up) * globalDirection;

        // Return the Vector2 form of the adjusted globalDirection vector.
        return new Vector2(globalDirection.x, globalDirection.z);
    }

    /// <summary>
    /// Updates the rotation of the provided Transforms based on the wobblePoints.  Gets called in Update() of this script.
    /// </summary>
    private void UpdateTransformRotations() {
        for(int i = 0; i < _transforms.Length; i++) {
            Vector2 wobblePoint = wobblePoints[i];

            Vector3 originalEulers = originalRotationEulers[i];
            Vector3 eulerAngles = new Vector3(originalEulers.x + (wobblePoint.y * _xDegreesMax), 0f, originalEulers.z + (-wobblePoint.x * _zDegreesMax));
            Quaternion newRotation = Quaternion.Euler(eulerAngles);
            _transforms[i].localRotation = newRotation;
        }
    }

    /// <summary>
    /// Unused, but is simply ApplyImpulse with a custom maxForce parameter available.  If you want to modify this script, this is available.
    /// </summary>
    /// <param name="wobbleVelocity"></param>
    /// <param name="direction"></param>
    /// <param name="force"></param>
    /// <param name="maxForce"></param>
    /// <returns></returns>
    private Vector2 ApplyImpulseAdvanced(Vector2 wobbleVelocity, Vector2 direction, float force, float maxForce) {
        wobbleVelocity += direction.normalized * maxForce * force;
        return wobbleVelocity;
    }

    /// <summary>
    /// ApplyForce(), but with custom maxForce applied.  Not public because provided editor sliders give the range of force that is within reason.
    /// </summary>
    /// <param name="direction"></param>
    /// <param name="force"></param>
    /// <param name="maxForce"></param>
    private Vector2 ApplyForceAdvanced(Vector2 wobbleVelocity, Vector2 direction, float force, float maxForce) {
        wobbleVelocity += direction.normalized * maxForce * force * Time.fixedDeltaTime;
        return wobbleVelocity;
    }

    /// <summary>
    /// Apply the current wobbleVelocity to the wobblePoint.  Gets called in FixedUpdate() of this script.
    /// </summary>
    private void UpdateWobblePoints() {
        for (int i = 0; i < wobblePoints.Length; i++) {
            Vector2 wobblePoint = wobblePoints[i];
            Vector2 wobbleVelocity = wobbleVelocities[i];
            wobblePoint += wobbleVelocity * Time.fixedDeltaTime;
            wobblePoint = ClampWobblePoint(wobblePoint);
            wobblePoints[i] = wobblePoint;
        }
    }

    /// <summary>
    /// Clamp the wobblePoint magnitude to make it a unit scalar.
    /// </summary>
    private Vector2 ClampWobblePoint(Vector2 wobblePoint) {
        wobblePoint = Vector2.ClampMagnitude(wobblePoint, 1f);
        return wobblePoint;
    }

    /// <summary>
    /// Clamp the provided force amount to be a unit scalar.
    /// </summary>
    /// <param name="forceAmount"></param>
    /// <returns></returns>
    private float ClampForceAmount(float forceAmount) {
        return Mathf.Clamp(forceAmount, 0.0f, 1.0f);
    }
}