using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class TrickAnalyzer : MonoBehaviour
{
    private SkateController skateController;
    public TrickBook tricks;
    
    public TextMeshProUGUI debugText;

    public float forgiveness = 0.1f;
    public Vector2[] trickVec;
    public float trickDelay;
    public bool lockTricks = false;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        skateController = SkateController.instance;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void AnalyzeSnapshot(GameObject[] snapshotObjs, Vector2[] trickVecs)
    {
        trickVec = trickVecs;
        if(lockTricks) return;
        if(trickVecs.All(p => p == new Vector2(0, 0))) return;
        else if (!WithinRange(trickVecs[trickVecs.Length-1], new Vector2(0, 0))) return;

        for(int i = 0; i < tricks.tricks.Length; i++)
        {
            Trick trick = tricks.tricks[i].trick;
            Dictionary<Vector2,int> requiredPasses = new Dictionary<Vector2,int>((trick.positions.Length + 1));
            int passes = 0;
            List<Vector2> allChecks = new List<Vector2>(trick.positions.Length + 1);
            allChecks.Add(trick.startPosition);
            allChecks.AddRange(trick.positions);
            
            for (int j = 0; j < allChecks.Count; j++)
            {
                requiredPasses.Add(allChecks[j], -1);
            }
            
            
            for(int j = 0; j < allChecks.Count; j++)
            {
                for (int k = 0; k < trickVecs.Length; k++)
                {
                    if (WithinRange(allChecks[j], trickVecs[k]))
                    {
                        requiredPasses[allChecks[j]] = k;
                        break;
                    }
                }
                
            }
            
            
            var hitIndex = new int[allChecks.Count];
            for (int j = 0; j < hitIndex.Length; j++) hitIndex[j] = -1;

            for (int j = 0; j < allChecks.Count; j++)
            {
                Vector2 target = allChecks[j];
                for (int k = 0; k < trickVecs.Length; k++)
                {
                    if (WithinRange(target, trickVecs[k])) { hitIndex[j] = k; break; }
                }
            }

            // Validate: every step hit, and indices strictly increasing
            int last = -1;
            bool passed = true;
            for (int j = 0; j < hitIndex.Length; j++)
            {
                int idx = hitIndex[j];
                if (idx < 0 || idx <= last) { passed = false; break; }
                last = idx;
            }

            if (passed)
            {
                if(trick.needsGround && !skateController.grounded) continue;
                TryWrite(trick.trickName);
                Debug.Log(trick.trickName);
                skateController.PopUp(trick.popForce);
                lockTricks = true;
                Invoke("ReleaseTricks", trickDelay);
            }
            
        }
        
        
    }
    
    void ReleaseTricks() => lockTricks = false;

    bool WithinRange(Vector2 input, Vector2 comparison) 
    {
        if(Vector2.Distance(input, comparison) > forgiveness) return false;
        return true;
    }

    void TryWrite(string data)
    {
        if (debugText != null)
        {
            debugText.text = data;
        }
    }
}
