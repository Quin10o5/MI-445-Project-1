using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "Trick", menuName = "Trick")]
public class Trick : ScriptableObject
{
    
    [FormerlySerializedAs("name")] public string trickName;
    public bool needsGround = false;
    public float popForce = 1;
    public Vector2 startPosition = new Vector2(0f, -1);
    public Vector2[] positions;
    public AnimationClip animation; 
}


