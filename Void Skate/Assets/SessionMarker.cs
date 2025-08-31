using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Interactions;

public class SessionMarker : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("Reference the 'Session / D-Pad/Up' action (Action Type = Button).")]
    public InputActionReference dpadUp;

    [Header("Behavior")]
    [Min(0f)] public float holdToSaveSeconds = 1.0f; // just for your reference
    public Vector3 markerOffset = new Vector3(0f, 0.5f, 0f);

    private Vector3 savedMarker;
    private Vector3 savedRotation;
    private bool hasMarker;

    void OnEnable()
    {
        if (dpadUp == null || dpadUp.action == null)
        {
            Debug.LogError("[SessionMarker] No InputActionReference assigned.");
            enabled = false;
            return;
        }

        var a = dpadUp.action;
        a.started   += OnStarted;   // optional, for debug
        a.performed += OnPerformed; // Tap/Hold complete
        a.canceled  += OnCanceled;  // release (not required here)
        a.Enable();
    }

    void OnDisable()
    {
        if (dpadUp?.action == null) return;
        var a = dpadUp.action;
        a.started   -= OnStarted;
        a.performed -= OnPerformed;
        a.canceled  -= OnCanceled;
        a.Disable();
    }

    private void OnStarted(InputAction.CallbackContext ctx)
    {
        // Debug.Log("DPAD Up started");
    }

    private void OnPerformed(InputAction.CallbackContext ctx)
    {
        // Distinguish which interaction completed
        if (ctx.interaction is HoldInteraction)
        {
            SaveMarker();
        }
        else if (ctx.interaction is TapInteraction)
        {
            LoadMarker();
        }
        else
        {
            // If interactions weren’t set, you’ll land here.
            // Add Tap + Hold on the binding (like in your screenshot).
            Debug.LogWarning("[SessionMarker] Performed without interaction; check binding Interactions.");
        }
    }

    private void OnCanceled(InputAction.CallbackContext ctx)
    {
        // Not used, but handy for debugging releases:
        // Debug.Log("DPAD Up canceled");
    }

    private void SaveMarker()
    {
        savedMarker = transform.position + markerOffset;
        hasMarker = true;
        savedRotation = transform.rotation.eulerAngles;
    }

    private void LoadMarker()
    {
        if (!hasMarker) return;
        transform.position = savedMarker;
        transform.eulerAngles = savedRotation;
        GetComponent<Rigidbody>().linearVelocity = Vector3.zero;
    }
}
