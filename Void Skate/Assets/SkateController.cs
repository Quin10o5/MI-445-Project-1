using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Diagnostics;
using UnityEngine.InputSystem;
using UnityEngine.Splines;

public class SkateController : MonoBehaviour
{
    public CinemachineThirdPersonFollow cam;
    public float camDistance = 3.6f;
    public static SkateController instance;
    public Animator animator;
    private Controls _controls;
    private Rigidbody rb;
    private float _lastPushTime;
    public bool grounded = true;
    public float groundCheckRadius = 0.2f;
    public Transform groundRayPos;
    public Transform groundRayPos2;
    public LayerMask groundMask;
    private Vector3 _groundNormal;
    public float rollingResistance = 0.2f;
    public float lateralDamp = 2.5f;   
    [Header("Push Tuning")]
    public float baseImpulse = 180f;     // N·s-ish feeling; scale to your RB mass
    
    public float cadenceSeconds = 0.42f; // time between effective pushes

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
    public float backsideTorque = 5;
    
    
    
    [Header("Curvature Limits")]
    public Vector2 torqueMinMax = new Vector2(0.1f, 0.2f);
    public Vector2 speedMinMax = new Vector2(0.0f, 30f); 
    public float airTurnMult = 1.6f;

    public float grindForce = 10;
    private Grinder grinder;
    
  
    private float impulse;
    void Awake()
    {
        instance = this;
        _controls = new Controls();
        rb = GetComponent<Rigidbody>();
        if (steerAction != null) steerAction.action.Enable();
        rb.maxAngularVelocity = 50f;
        grinder = GetComponent<Grinder>();
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
        if (rb.linearVelocity.magnitude > speedMinMax.y) rb.linearVelocity = rb.linearVelocity.normalized * speedMinMax.y;
        //Debug.Log(rb.linearVelocity.magnitude);
        if (Physics.Raycast(groundRayPos.position, Vector3.down, out RaycastHit hitInfo, groundCheckRadius, groundMask))
        {
            OnRay(hitInfo);
        }
        else if (Physics.Raycast(groundRayPos2.position, Vector3.down, out RaycastHit hitInfo2, groundCheckRadius, groundMask))
        {
            OnRay(hitInfo2);
        }
        else
        { 
            _groundNormal = Vector3.up;
            if (grinder.currentSpline == null|| grinder.currentSpline.Count == 0)
            {
                ApplySteering(hitInfo);
            }
            else
            {
                Grind(grinder.currentSpline, grinder.grindDir);
            }
            grounded = false;
        }
    }


    void OnRay(RaycastHit hitInfo)
    {
        grounded = true;
        _groundNormal = hitInfo.normal;

        if (grinder.currentSpline == null || grinder.currentSpline.Count == 0)
        {
            ApplyRollingResistance(hitInfo);
            KillLateralSlide(hitInfo);
            ApplySteering(hitInfo);
        }
        else
        {
           Grind(grinder.currentSpline, grinder.grindDir);
        }
        

        desiredDistance = Utils.Remap(rb.linearVelocity.magnitude, 0f, 20, 0, camDistance);
        desiredDistance += 2;
        if(Vector3.Angle(rb.linearVelocity, fwd) > 90f) desiredDistance *= -1;
        float camDist = cam.CameraDistance;
        //Debug.Log(desiredDistance);
        float distance = Mathf.Lerp(camDist, desiredDistance, cameraSpeed * Time.deltaTime);
        cam.CameraDistance = distance;
    }


    void Grind(Spline spline, Vector3 dir)
    {
        Vector3 fwd    = dir.normalized;
        Vector3 planar = Vector3.ProjectOnPlane(dir, Vector3.up);
        rb.AddForce(planar * grinder.grindEntranceVel, ForceMode.Force);
    }
    

    void FrontPush(InputAction.CallbackContext context)
    {
        //Debug.Log("Front Push");
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

        Vector3 kindaDown = -transform.up + fwd;
        float downhill = Vector3.Dot(kindaDown.normalized, Physics.gravity.normalized); // >0 = facing down
        //Debug.Log(downhill);
        float slopeMul = downhill > .71f ? downhillPenalty : uphillBonus;

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
        else up = _groundNormal;
        // Build slope-aware basis no matter what; if airborne, up≈world up.
        fwd = Vector3.ProjectOnPlane(transform.forward, up).normalized;
        right = Vector3.Cross(up, fwd);
        Debug.DrawLine(transform.position, transform.position + fwd * 2, Color.green);
        Debug.DrawLine(transform.position, transform.position + right * 2, Color.red);

        planarV = Vector3.ProjectOnPlane(rb.linearVelocity, up);
        speed   = planarV.magnitude;
        


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
        
        float desiredTorque = Utils.Remap(steerFiltered, -1f, 1, -torqueMinMax.y, torqueMinMax.y);
        float speedMult;
        if (!grounded) speedMult = Utils.Remap(0, speedMinMax.x, speedMinMax.y, 1, 0);
            else speedMult = Utils.Remap(rb.linearVelocity.magnitude, speedMinMax.x, speedMinMax.y, 1, 0 );
        
        if (!grounded) speedMult *= airTurnMult;
        rb.AddTorque(up * desiredTorque * speedMult, ForceMode.Impulse);

        
            
        
    }

    public void BacksideTurnCheck()
    {
        if (Mathf.Abs(steerFiltered) > .4f)
        {
            float desiredTorque = Utils.Remap(steerFiltered, -1f, 1, -torqueMinMax.y, torqueMinMax.y);
            rb.AddTorque(up * desiredTorque * backsideTorque, ForceMode.Impulse);
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


    void OnDrawGizmos()
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
