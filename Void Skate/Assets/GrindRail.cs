using UnityEngine;
using UnityEngine.Splines;

public class GrindRail : MonoBehaviour
{
    public Spline spline;


    void Awake()
    {
        spline = transform.parent.GetComponentInChildren<SplineContainer>().Spline;
    }
    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Board"))
        { 
            Grinder g = other.GetComponentInParent<Grinder>();
            if (g.currentSpline == spline)
            {
                g.currentSpline = null;
                other.GetComponentInParent<Rigidbody>().constraints = RigidbodyConstraints.None;
            }
        }
    }
}
