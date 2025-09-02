using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

public class Grinder : MonoBehaviour
{
    private Rigidbody rb;
    [HideInInspector]
    public Vector3 grindDir;
    public Collider currentGrind;
    public Spline currentSpline;
    private Vector3 contactPoint;
    public float grindEntranceVel;


    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void OnCollisionEnter(Collision other)
    {
        //Debug.Log(other.gameObject.name);
        if (other.gameObject.CompareTag("Grindable"))
        {
            Debug.Log("passed");
            currentGrind = other.gameObject.GetComponent<Collider>();
            currentSpline = other.gameObject.GetComponentInChildren<SplineContainer>().Spline; 
            Vector3 velocityDir = rb.linearVelocity.normalized;
            contactPoint = other.contacts[0].point;
            var spline = currentSpline; // type: Spline
            float t;
            float3 nearestLocal;
            var localPos = currentGrind.transform.InverseTransformPoint(transform.position);
            SplineUtility.GetNearestPoint(spline, (float3)localPos, out nearestLocal, out t, 5);
            float angle1 = Vector3.SignedAngle(rb.linearVelocity.normalized, currentSpline.EvaluateTangent(t), Vector3.forward);
            float angle2 = Vector3.SignedAngle(-rb.linearVelocity.normalized, currentSpline.EvaluateTangent(t), Vector3.forward);
            grindEntranceVel = rb.linearVelocity.magnitude;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            if (Mathf.Abs(angle1) > Mathf.Abs(angle2)) grindDir = -currentSpline.EvaluateTangent(t);
            else grindDir = currentSpline.EvaluateTangent(t);
            
            
                


        }
    }
    void OnCollisionExit(Collision other)
    {
        //Debug.Log(other.gameObject.name);
        if (other.gameObject.CompareTag("Grindable") && currentGrind == other.gameObject.GetComponent<Collider>())
        {
            currentGrind = null; 
        }
    }

    void OnDrawGizmos()
    {
        if (grindDir != Vector3.zero)
        {
            Debug.DrawRay(contactPoint, grindDir, Color.red);

        }
    }
}
