using System.Collections.Generic;
using UnityEngine;

public class FauxPhysicsAntenna : MonoBehaviour {
    [Tooltip("The prefab to be used for each antenna segment. Make sure the Transform of this prefab is at the base of the segment, as " +
        "they will spawn assuming the Transform of the object is the bottom of the segment. Childing your Mesh to an empty Transform is an easy " +
        "way to accomplish this.")]
    [SerializeField] private GameObject _antennaSegmentPrefab;
    [Tooltip("OPTIONAL: In case you want something at the top of the Antenna.")]
    [SerializeField] private GameObject _antennaTopperPrefab;
    [Tooltip("The Transform that exists in this GameObject that you want the base of the Antenna to be at.")]
    [SerializeField] private Transform _antennaBase;
    [Tooltip("The number of AntennaSegmentPrefab's to spawn in order to create the Antenna. You can modify this range without causing any problems. " +
        "However, whatever this number is dictates how many iterations occur per FixedUpdate to update the Antenna Transforms.")]
    [Range(1, 20)]
    [SerializeField] private int _segmentCount;
    [Tooltip("The maximum amount of force applied when using ApplyForce each FixedUpdate to push the Antenna around. " +
        "At an ApplyForce of 1, you will be applying this MaxForce amount.")]
    [Range(0f, 200f)]
    [SerializeField] private float _maxForce;
    [Tooltip("The maximum amount of force applied when using ApplyImpulse to push the Antenna instantaneously. " +
        "At an ApplyImpulse of 1, you will be applying this MaxImpulseForce amount.")]
    [Range(0f, 200f)]
    [SerializeField] private float _maxImpulseForce;
    [Tooltip("The maximum number of degrees the Antenna should bend. Experiment with this to achieve desired look. Results are visible during runtime.")]
    [Range(1, 90)]
    [SerializeField] private float _maxDegrees;
    [Tooltip("The stiffness of the Antenna. Experiment with this to achieve desired look. Results are visible during runtime.")]
    [Range(0f, 800f)]
    [SerializeField] private float _stiffness;
    [Tooltip("The dampening of the Antenna. Lower dampening means the Antenna will take longer to return to stationary after movement. " +
        "Experiment with this to achieve desired look. Results are visible during runtime.")]
    [Range(0f, 50f)]
    [SerializeField] private float _dampening;
    [Tooltip("This is just a unique string to assign each individual AntennaSegmentPrefab so the internal Object Pool can retrieve the correct " +
        "pooled GameObject. Leaving this blank means the Object Pool will not be used. It's recommended you put some string value here, as " +
        "the Object Pool will significantly reduce Instantiation calls. (Simply put the same string " +
        "here on all your objects that use the same AntennaSegmentPrefab, it's just a Dictionary internally, don't overthink it.)")]
    [SerializeField] private string _segmentIdentifier;

    private Transform[] antennaTransforms;
    private Transform topperTransform;
    private Vector2 wobblePoint = new Vector2();
    private Vector2 wobbleVelocity = new Vector2();
    private float segmentLength = 0f;
    private const string TOPPER_IDENTIFIER_SUFFIX = "Topper";
    private string topperIdentifier;

    private static Dictionary<string, List<GameObject>> antennaPool = new Dictionary<string, List<GameObject>>();

    void Awake() {
        if (_antennaBase == null || _antennaSegmentPrefab == null) return;

        if (string.IsNullOrEmpty(_segmentIdentifier) || string.IsNullOrWhiteSpace(_segmentIdentifier)) _segmentIdentifier = null;

        Renderer segmentRenderer = _antennaSegmentPrefab.GetComponentInChildren<Renderer>();
        Transform segmentTransform = segmentRenderer.GetComponent<Transform>();
        if(segmentRenderer == null || segmentTransform == null) {
            Debug.Log("No Renderer and/or Transform in Antenna Segment prefab");
            return;
        }
        Bounds segmentBounds = segmentRenderer.localBounds;
        segmentLength = segmentBounds.size.y * segmentTransform.localScale.y;
        antennaTransforms = new Transform[_segmentCount];

        for (int i = 0; i < antennaTransforms.Length; i++) {
            GameObject segmentGO = RetrievePooledGameObject(_segmentIdentifier);
            if (segmentGO == null) segmentGO = Instantiate(_antennaSegmentPrefab);
            Vector3 segmentPosition = _antennaBase.position;
            segmentPosition.y += i * segmentLength;
            segmentGO.transform.position = segmentPosition;
            antennaTransforms[i] = segmentGO.transform;
            segmentGO.transform.SetParent(_antennaBase);
        }

        if (_antennaTopperPrefab == null || _segmentIdentifier == null) return;

        topperIdentifier = _segmentIdentifier + TOPPER_IDENTIFIER_SUFFIX;
        GameObject topperGO = RetrievePooledGameObject(topperIdentifier);
        if (topperGO == null) topperGO = Instantiate(_antennaTopperPrefab);
        topperTransform = topperGO.transform;
        topperTransform.SetParent(_antennaBase);
    }


    void FixedUpdate() {
        ApplyGravityAndDampening();
        UpdateWobblePoint();
        UpdateTransforms();
    }

    public void OnDestroy() {
        if (_segmentIdentifier == null) return;
        for(int i = 0; i < antennaTransforms.Length; i++) {
            Transform current = antennaTransforms[i];
            current.SetParent(null);
        }
        PoolGameObjects(_segmentIdentifier, antennaTransforms);
        if (topperTransform == null) return;
        PoolGameObject(topperIdentifier, topperTransform);
    }

    /// <summary>
    /// This is one of two functions that will make the Antenna actually move.
    /// Applies an instantaneous force to the Antenna. <paramref name="globalDirection"/> is the direction of the force and <paramref name="force"/>
    /// is the amount of force (between 0f and 1f).
    /// </summary>
    /// <param name="globalDirection"></param>
    /// <param name="force"></param>
    public void ApplyImpulse(Vector3 globalDirection, float force) {
        Vector2 localizedDirection = GlobalToLocalVector(globalDirection, _antennaBase.forward);
        wobbleVelocity = ApplyImpulseToVelocity(wobbleVelocity, localizedDirection, force);
    }

    /// <summary>
    /// This is one of two functions that will make the Antenna actually move.
    /// Applies a force over time to the Antenna. <paramref name="globalDirection"/> is the direction of the force and <paramref name="force"/>
    /// is the amount of force (between 0f and 1f).
    /// </summary>
    /// <param name="globalDirection"></param>
    /// <param name="force"></param>
    public void ApplyForce(Vector3 globalDirection, float force) {
        Vector2 localizedDirection = GlobalToLocalVector(globalDirection, _antennaBase.forward);
        wobbleVelocity = ApplyForceToVelocity(wobbleVelocity, localizedDirection, force);
    }

    private Vector2 ApplyImpulseToVelocity(Vector2 wobbleVelocity, Vector2 direction, float force) {
        force = ClampForceAmount(force);
        wobbleVelocity += direction.normalized * _maxImpulseForce * force;
        return wobbleVelocity;
    }

    private Vector2 ApplyForceToVelocity(Vector2 wobbleVelocity, Vector2 direction, float force) {
        force = ClampForceAmount(force);
        wobbleVelocity += direction.normalized * _maxForce * force * Time.fixedDeltaTime;
        return wobbleVelocity;
    }

    private Vector2 GlobalToLocalVector(Vector3 globalDirection, Vector3 globalFacingDirection) {
        Vector2 globalFacingDirectionV2 = new Vector2(globalFacingDirection.x, globalFacingDirection.z);
        float angleOffset = Vector2.SignedAngle(globalFacingDirectionV2, Vector2.up);
        globalDirection.y = 0f;
        globalDirection = Quaternion.AngleAxis(-angleOffset, Vector3.up) * globalDirection;
        return new Vector2(globalDirection.x, globalDirection.z);
    }

    private void ApplyGravityAndDampening() {
        Vector2 xGravityDirection = new Vector2(-wobblePoint.x, 0f);
        wobbleVelocity = ApplyForceAdvanced(wobbleVelocity, xGravityDirection, Mathf.Abs(wobblePoint.x), _stiffness);

        // Apply consistent gravity to push y toward 0, using _zStiffness scalar
        Vector2 yGravityDirection = new Vector2(0f, -wobblePoint.y);
        wobbleVelocity = ApplyForceAdvanced(wobbleVelocity, yGravityDirection, Mathf.Abs(wobblePoint.y), _stiffness);

        // Apply consistent force directly against the velocity vector for dampening, using _dampening scalar
        Vector2 dampeningDirection = wobbleVelocity * -1;
        wobbleVelocity = ApplyForceAdvanced(wobbleVelocity, dampeningDirection, 1f, wobbleVelocity.magnitude * _dampening);
    }

    private void UpdateWobblePoint() {
        wobblePoint += wobbleVelocity * Time.fixedDeltaTime;
        wobblePoint = ClampWobblePoint(wobblePoint);
    }

    private void UpdateTransforms() {
        float degreeTotal = _maxDegrees * wobblePoint.magnitude;
        float degreesPerSegment = degreeTotal / antennaTransforms.Length;
        float currentSegmentDegrees = degreesPerSegment;
        // rotate wobblePoint 90 degrees CCW
        Vector3 rotationVector = new Vector3(-wobblePoint.y, 0f, wobblePoint.x);
        Vector3 transformOffset = new Vector3();
        for(int i = 0; i < antennaTransforms.Length; i++) {
            Transform antennaTransform = antennaTransforms[i];
            antennaTransform.localRotation = Quaternion.AngleAxis(currentSegmentDegrees, rotationVector);
            antennaTransform.position = _antennaBase.position + transformOffset;
            transformOffset += antennaTransform.up * segmentLength;
            currentSegmentDegrees += degreesPerSegment;
        }

        if (topperTransform == null) return;
        topperTransform.localRotation = Quaternion.AngleAxis(currentSegmentDegrees, rotationVector);
        topperTransform.position = _antennaBase.position + transformOffset;
    }

    private Vector2 ApplyForceAdvanced(Vector2 wobbleVelocity, Vector2 direction, float force, float maxForce) {
        wobbleVelocity += direction.normalized * maxForce * force * Time.fixedDeltaTime;
        return wobbleVelocity;
    }

    private Vector2 ClampWobblePoint(Vector2 wobblePoint) {
        wobblePoint = Vector2.ClampMagnitude(wobblePoint, 1f);
        return wobblePoint;
    }

    private float ClampForceAmount(float forceAmount) {
        return Mathf.Clamp(forceAmount, 0.0f, 1.0f);
    }

    // POOL FUNCTIONS ========================================
    private void PoolGameObject(string identifier, Transform goTransform) {
        List<GameObject> pool;
        if(!antennaPool.TryGetValue(identifier, out pool)) {
            pool = new List<GameObject>();
            antennaPool.Add(identifier, pool);
        }

        GameObject gObject = goTransform.gameObject;
        gObject.SetActive(false);
        pool.Add(gObject);
    }

    private void PoolGameObjects(string identifier, Transform[] transforms) {
        List<GameObject> pool;
        if (!antennaPool.TryGetValue(identifier, out pool)) {
            pool = new List<GameObject>();
            antennaPool.Add(identifier, pool);
        }

        for (int i = 0; i < transforms.Length; i++) {
            GameObject gObject = transforms[i].gameObject;
            gObject.SetActive(false);
            pool.Add(gObject);
        }
    }

    private GameObject RetrievePooledGameObject(string identifier) {
        if (identifier == null) return null;
        List<GameObject> pool;
        if (!antennaPool.TryGetValue(identifier, out pool)) return null;
        if (pool.Count < 1) return null;

        GameObject GO = null;
        while(GO == null && pool.Count > 0) {
            GO = pool[pool.Count - 1];
            pool.RemoveAt(pool.Count - 1);
        }

        if (GO == null) return null;

        GO.SetActive(true);
        return GO;
    }
    // =======================================================
}