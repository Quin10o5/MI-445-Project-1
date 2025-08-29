using UnityEngine;

public class Utils : MonoBehaviour
{
    /// <summary>
    /// Remaps a value from one range to another.
    /// </summary>
    public static float Remap(float value, float fromMin, float fromMax, float toMin, float toMax)
    {
        float t = Mathf.InverseLerp(fromMin, fromMax, value);
        return Mathf.Lerp(toMin, toMax, t);
    }

    /// <summary>
    /// Remaps a value from one range to another using Vector2s for min/max.
    /// from.x = min, from.y = max. to.x = min, to.y = max.
    /// </summary>
    public static float Remap(float value, Vector2 from, Vector2 to)
    {
        float t = Mathf.InverseLerp(from.x, from.y, value);
        return Mathf.Lerp(to.x, to.y, t);
    }
    
    public static bool IsFinite(Vector2 v) =>
        !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsInfinity(v.x) || float.IsInfinity(v.y));
    
    
    public static float ApplyDeadzone(float x, float dz)
    {
        float ax = Mathf.Abs(x);
        if (ax <= dz) return 0f;
        // re-scale so post-deadzone maps to full range
        return Mathf.Sign(x) * Mathf.InverseLerp(dz, 1f, ax);
    }

    // simple exponential curve: keep fine control near center
    public static float ApplyExpo(float x, float expo)
    {
        if (expo <= 0f) return x;
        float sign = Mathf.Sign(x);
        float ax = Mathf.Abs(x);
        float curved = (1f - expo) * ax + expo * ax * ax * ax; // cubic expo
        return sign * curved;
    }

    public static float ExpSmooth(float y, float target, float ratePerSec, float dt)
    {
        if (ratePerSec <= 0f) return target;
        float a = 1f - Mathf.Exp(-ratePerSec * dt);
        return Mathf.Lerp(y, target, a);
    }

    
}
