using UnityEngine;


public enum SkaterState {walking, riding}
public class Skater : MonoBehaviour
{
    public static Skater instance;
    public SkaterState state;
    public float crouchAmt;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        instance = this;
    }

    public void Crouch(float amt)
    {
        if (state != SkaterState.riding) return;
        
        
    }
}
