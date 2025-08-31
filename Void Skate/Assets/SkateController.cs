using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Diagnostics;
using UnityEngine.InputSystem;
public class SkateController : MonoBehaviour
{
    public CinemachineThirdPersonFollow cam;
    public float camDistance = 3.6f;
    public static SkateController instance;
    public Animator animator;
    public float pushForce;
    private Controls _controls;
    private Rigidbody rb;
    private float _lastPushTime;
    public bool grounded = true;
    public float groundCheckRadius = 0.2f;
    public Transform groundRayPos;
    public LayerMask groundMask;
    private Vector3 _groundNormal;
    public float rollingResistance = 0.2f;
    public float lateralDamp = 2.5f;   
    [Header("Push Tuning")]
    public float baseImpulse = 180f;     // N·s-ish feeling; scale to your RB mass
    
    public float cadenceSeconds = 0.42f; // time between effective pushes
    public float perfectBeatSeconds = 0.84f; // every 2 beats (example)
    public float perfectWindow = 0.08f;  // ± window for perfect
    public float perfectBonus = 1.25f;   // multiplier
    public float uphillBonus = 1.1f;     // tiny boost pushing uphill
    public float downhillPenalty = 0.9f;
    public AnimationCurve impulseBySpeed;
    
    
    float lastPushTime = -999f;
    float beatClock = 0f; // track time for perfect windows
    Vector3 groundNormal = Vector3.up;
    float steerFiltered = 0f;
    [Header("Input (New Input System)")]
    public InputActionReference steerAction;   // value in [-1, +1]
    [Range(0f, 0.2f)] public float deadzone = 0.08f;
    [Range(0.0f, 1.0f)] public float steerExpo = 0.35f; // 0=linear, 0.3..0.5 smooth
    [Range(0.0f, 20f)] public float steerSmoothing = 10f; // exp smoothing rate (1/s)
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    
    
    
    Vector3 up = Vector3.up;
    Vector3 fwd = Vector3.forward;
    Vector3 right = Vector3.right;
    float speed = 0f;
    Vector3 planarV = Vector3.zero;
    
    
    
    [Header("Curvature Limits")]
    [Tooltip("Global ceiling on curvature (1/m). Sets tightest possible radius regardless of grip.")]
    public float kappaMaxBase = 0.9f;  // 1/m → ~1.1 m min radius at low speeds
    [Tooltip("Low-speed minimum curvature so you can still turn while crawling.")]
    public float kappaLow = 0.35f;     // 1/m
    [Tooltip("Below this speed, add pivot yaw so you can rotate in place.")]
    public float pivotAssistFadeSpeed = 2.0f; // m/s
    [Tooltip("Max yaw rate (rad/s) when stationary with full steer.")]
    public float pivotYawRateMax = 3.5f;      // ~200 deg/s

    [Header("Yaw Controller (about ground up)")]
    public float yawKp = 6.0f;     // proportional on (ω_target - ω_now)
    public float yawKd = 1.5f;     // damping on ω_now
    
    [Header("Bank / Lean")]
    [Range(0f, 25f)] public float maxLeanDegrees = 10f;
    [Tooltip("Fraction of the 'ideal' bank angle to apply (0..1).")]
    [Range(0f, 1f)] public float bankStrength = 0.8f;
    public float bankKp = 10f;
    public float bankKd = 2f;
    private float impulse;
    void Awake()
    {
        instance = this;
        _controls = new Controls();
        rb = GetComponent<Rigidbody>();
        if (steerAction != null) steerAction.action.Enable();
        rb.maxAngularVelocity = 50f;
    }

    private void OnEnable()
    {
        _controls.Enable();
        _controls.Player.PushFFoot.performed += FrontPush;
    }

    private void OnDisable()
    {
        _controls.Disable();
        _controls.Player.PushFFoot.performed -= FrontPush;
        if (steerAction != null) steerAction.action.Disable();
    }

    void Update()
    {
    }

    float desiredDistance;
    public float cameraSpeed = 2;
    void FixedUpdate()
    {
        if (Physics.Raycast(groundRayPos.position, Vector3.down, out RaycastHit hitInfo, groundCheckRadius, groundMask))
        {
            grounded = true;
            _groundNormal = hitInfo.normal;
            ApplyRollingResistance(hitInfo);
            KillLateralSlide(hitInfo);
            ApplySteering(hitInfo);

            desiredDistance = Utils.Remap(rb.linearVelocity.magnitude, 0f, 20, 0, camDistance);
            desiredDistance += 2;
            if(Vector3.Angle(rb.linearVelocity, fwd) > 90f) desiredDistance *= -1;
            float camDist = cam.CameraDistance;
            //Debug.Log(desiredDistance);
            float distance = Mathf.Lerp(camDist, desiredDistance, cameraSpeed * Time.deltaTime);
            cam.CameraDistance = distance;
        }
        else
        {
            _groundNormal = Vector3.up;
            ApplySteering(hitInfo);
            grounded = false;
        }
    }

    void FrontPush(InputAction.CallbackContext context)
    {
        Debug.Log("Front Push");
        if (!grounded) return;
        if (Time.time - _lastPushTime < cadenceSeconds) return; 
        
        animator.SetTrigger("Push");
        // Compute push direction on slope
         fwd = Vector3.ProjectOnPlane(transform.forward, groundNormal).normalized;

        // Current planar speed
        Vector3 planar = Vector3.ProjectOnPlane(rb.linearVelocity, Vector3.up);
        float speed = planar.magnitude;

        // Diminishing returns
        float mul = impulseBySpeed.Evaluate(speed);

        // Perfect timing bonus (hit near beat multiples)
        float mod = Mathf.Repeat(beatClock, perfectBeatSeconds);
        bool isPerfect = (mod <= perfectWindow || mod >= perfectBeatSeconds - perfectWindow);
        if (isPerfect) mul *= perfectBonus;

        // Slope modifier
        float slope = Vector3.SignedAngle(fwd, Vector3.ProjectOnPlane(Vector3.down, groundNormal), Vector3.Cross(fwd, groundNormal));
        // simpler: use dot with gravity
        float downhill = Vector3.Dot(fwd, Physics.gravity.normalized); // >0 means facing down
        float slopeMul = downhill > 0f ? downhillPenalty : uphillBonus;

        float surfaceMul = .5f;

        // Final impulse
        impulse = baseImpulse * mul * slopeMul * surfaceMul;
        Invoke("FRPush",.6f);
        rb.AddForce(fwd * impulse, ForceMode.Impulse);
        lastPushTime = Time.time;
    }

    void ApplySteering(RaycastHit hit)
    {
        if (!grounded) up = Vector3.up;
        // Build slope-aware basis no matter what; if airborne, up≈world up.
        fwd = Vector3.ProjectOnPlane(transform.forward, up).normalized;
        if (fwd.sqrMagnitude < 1e-4f)
            fwd = Vector3.ProjectOnPlane(transform.right, up).normalized;
        right = Vector3.Cross(up, fwd);

        planarV = Vector3.ProjectOnPlane(rb.linearVelocity, up);
        speed   = planarV.magnitude;
        
        float forwardDot = (speed > 1e-3f) ? Vector3.Dot(planarV.normalized, fwd) : 1f; // -1..+1
        float align = Mathf.Clamp01((forwardDot + 1f) * 0.5f);   // 0 when backwards, 0.5 at 90°, 1 when forward
        float misalign = 1f - align;                              // 1 at 90°/backwards


        // --- 2) Read & filter steer ------------------------------------------
        float steerRaw = 0f;
        
        if (steerAction)
        {
            Vector2 steerVec = steerAction.action.ReadValue<Vector2>();
            steerRaw = Mathf.Clamp(steerVec.x, -1f, 1f);
        }
        steerRaw = Utils.ApplyDeadzone(steerRaw, deadzone);
        steerRaw = Utils.ApplyExpo(steerRaw, steerExpo);
        steerFiltered = Utils.ExpSmooth(steerFiltered, steerRaw, steerSmoothing, Time.fixedDeltaTime);

        // --- 3) Grip and curvature limits ------------------------------------
        float mu = 0.5f;
        float gEff = Mathf.Abs(Vector3.Dot(Physics.gravity, up)); // = g * cos(theta) on slopes

        // Traction-limited curvature cap (a_lat = v^2 * κ <= μ gEff  => κ <= μ gEff / v^2)
        float kappaGrip = (speed > 0.15f) ? (mu * gEff) / (speed * speed) : 999f;

        // Final κ_max: respect global base cap, but never below kappaLow
        float kappaMax = Mathf.Min(kappaMaxBase, kappaGrip);
        kappaMax = Mathf.Max(kappaMax, kappaLow);

        float airMult = 1;
        if (!grounded)
        {
            
            airMult = 2f;
            kappaMax *= airMult;
        }

        float kappaTarget = steerFiltered * kappaMax;

        // --- 4) Target yaw rate & low-speed pivot assist ----------------------
        float signedSpeed = Vector3.Dot(planarV, fwd);   // <0 when traveling backwards
        // ground-driven yaw when aligned with travel
        float omegaGround = signedSpeed * kappaTarget;

// input-driven yaw when misaligned / in-air (doesn't vanish at 90°)
        float airYawRateMax = pivotYawRateMax * airMult;     // reuse your numbers
        float omegaAir = steerFiltered * airYawRateMax;

// blend by misalignment; weight it more strongly in-air
        float airBias = grounded ? 0.6f : 1.0f;              // tune: how much we trust air control
        float omegaTarget = Mathf.Lerp(omegaGround, omegaAir, misalign * airBias);
        
        if (speed < pivotAssistFadeSpeed)
        {
            float t = 1f - Mathf.Clamp01(speed / Mathf.Max(0.0001f, pivotAssistFadeSpeed));
            omegaTarget = Mathf.Lerp(omegaTarget, steerFiltered * pivotYawRateMax * airMult, t);
        }



        // --- 5) Apply lateral (centripetal) force to carve --------------------
        if (grounded && speed > 0.01f)
        {
            float aLatCmd = speed * speed * kappaTarget;                 // desired
            float aLatCap = mu * gEff;                                   // traction budget
            float aLat = Mathf.Clamp(aLatCmd, -aLatCap, aLatCap);        // grip-limited
            Vector3 aLatVec = right * (Mathf.Sign(kappaTarget) * Mathf.Abs(aLat)); // toward center of turn

            rb.AddForce(aLatVec, ForceMode.Acceleration);

            // optional gentle sideways bleed to prevent crab
            Vector3 lateral = planarV - Vector3.Project(planarV, fwd);
            if (lateralDamp > 0f && lateral.sqrMagnitude > 1e-4f)
                rb.AddForce(-lateral * lateralDamp, ForceMode.Acceleration);
        }

        // --- 6) Yaw PD to track omegaTarget ----------------------------------
        // current yaw rate is the component of angular velocity about 'up'
        float omegaNow = Vector3.Dot(rb.angularVelocity, up);
        float yawTorque = yawKp * (omegaTarget - omegaNow) - yawKd * omegaNow;
        rb.AddTorque(up * yawTorque, ForceMode.Acceleration);

        // Small alignment torque so board faces travel (reduces crab at high v)

    // only help-face the travel if we’re generally moving forward
        if (speed > 0.2f && forwardDot > 0.2f)
        {
            Vector3 travelDir = planarV.normalized;
            float alignSign  = Mathf.Sign(Vector3.SignedAngle(fwd, travelDir, up));
            float alignAngle = Vector3.Angle(fwd, travelDir) * Mathf.Deg2Rad;
            rb.AddTorque(up * (alignSign * alignAngle * 2.0f), ForceMode.Acceleration);
        }

        // --- 7) Bank / lean toward inside of turn ----------------------------
        // Ideal bank: phi = atan(a_lat / gEff). Use commanded aLat (grip-limited) & scale.
        float aLatIdeal = Mathf.Clamp(speed * speed * Mathf.Abs(kappaTarget), 0f, mu * gEff);
        float phiTarget = Mathf.Atan2(aLatIdeal, Mathf.Max(0.001f, gEff)) * bankStrength; // radians
        phiTarget = Mathf.Min(phiTarget, maxLeanDegrees * Mathf.Deg2Rad);

        // Determine which side we’re banking toward (left/right turn)
        float turnSign = Mathf.Sign(kappaTarget); // + left, - right (given right = up×fwd)
        // target "up" vector is current up rotated around 'forward' by -turnSign*phi
        Quaternion bankRot = Quaternion.AngleAxis(-turnSign * phiTarget * Mathf.Rad2Deg, fwd);
        Vector3 targetUp = (bankRot * up).normalized;

        // PD torque to align current up to targetUp, around axis perpendicular to both
        Vector3 curUp = transform.rotation * Vector3.up;
        Vector3 axis = Vector3.Cross(curUp, targetUp);
        float ang = Mathf.Asin(Mathf.Clamp(axis.magnitude, -1f, 1f)); // radians (small-angle)
        Vector3 axisN = (axis.sqrMagnitude > 1e-8f) ? axis.normalized : Vector3.zero;

        // Project angular velocity onto that axis for damping
        float angVelAlongAxis = Vector3.Dot(rb.angularVelocity, axisN);
        Vector3 bankTorque = axisN * (ang * bankKp - angVelAlongAxis * bankKd);
        if (axisN != Vector3.zero)
        {
            rb.AddTorque(bankTorque, ForceMode.Acceleration);
            
        }
            
        
    } 
    void FRPush() =>rb.AddForce(fwd * impulse, ForceMode.Impulse);
    
    
    void ApplyRollingResistance(RaycastHit hit)
    {
        // Constant planar slowdown approximating bearing + wheel loss.
        Vector3 planar = Vector3.ProjectOnPlane(rb.linearVelocity, Vector3.up);
        if (planar.sqrMagnitude < 0.0001f) return;

        float surfaceMul = .5f;
        
        float decel = rollingResistance / Mathf.Max(surfaceMul, 0.25f); // grass slows more
        rb.AddForce(-planar.normalized * (decel * rb.mass), ForceMode.Force);
    }
    
    void KillLateralSlide(RaycastHit hit)
    {
        // Bleed sideways motion relative to board forward
        Vector3 fwd    = Vector3.ProjectOnPlane(transform.forward, groundNormal).normalized;
        Vector3 planar = Vector3.ProjectOnPlane(rb.linearVelocity, Vector3.up);
        Vector3 lateral = planar - Vector3.Project(planar, fwd);
        rb.AddForce(-lateral * lateralDamp, ForceMode.Force);
    }

    public void PopUp(float force)
    {
        rb.AddForce(groundNormal * force, ForceMode.Impulse);
    }


    void OnDrawGizmosSelected()
    {
        if(!Application.isPlaying) return;
        if(grounded) Gizmos.color = Color.green;
        else Gizmos.color = Color.red;
        Gizmos.DrawRay(groundRayPos.position, Vector3.down * groundCheckRadius);
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(groundRayPos.position, groundRayPos.position + _groundNormal * 2);
        Gizmos.DrawSphere(transform.position + rb.centerOfMass.y * Vector3.up, groundCheckRadius);
    }

}
