using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;


public class CircleIndicator : MonoBehaviour
{
    [FormerlySerializedAs("stickVis")] public Transform cursorVis;
    public GameObject cursorClone;
    
    public InputActionReference trickAction;
    public int trickTickRate = 8;
    public int rollingWindowSize = 15;
    public Queue<GameObject> rollingWindow;
    
    public Vector2 trickRaw;
    
    public Vector2 xBounds;
    public Vector2 yBounds;
    public float radius;
    private TrickAnalyzer trickAnalyzer;
    
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        trickAnalyzer = GetComponent<TrickAnalyzer>();
        Image image = GetComponent<Image>();
        Vector3[] corners = new Vector3[4];;
        image.rectTransform.GetWorldCorners(corners);
        xBounds = new Vector2(corners[0].x, corners[2].x);
        yBounds = new Vector2(corners[0].y, corners[2].y);
        radius = (xBounds.y - xBounds.x) /2;
        rollingWindow = new Queue<GameObject>(rollingWindowSize);
        InvokeRepeating("RollingWindow", 1, (float)1/trickTickRate);
    }

    private void Awake()
    {
        if (trickAction != null) trickAction.action.Enable();
    }

    private void OnDisable()
    {
        if (trickAction != null) trickAction.action.Disable();
    }
    // Update is called once per frame
    void FixedUpdate()
    {
        if (trickAction)
        {
            Vector2 trickVec = trickAction.action.ReadValue<Vector2>();
            //trickVec = trickVec.normalized;
            trickRaw = trickVec;
            float posX = Utils.Remap(trickVec.x, -1, 1, xBounds.x, xBounds.y);
            float posY = Utils.Remap(trickVec.y, -1, 1, yBounds.x, yBounds.y);
            cursorVis.position = new Vector2(posX, posY);
            if (Vector2.Distance(cursorVis.position, transform.position) > radius)
            {
                Vector2 center = transform.position;
                Vector2 delta  = (Vector2)cursorVis.position - center;
                cursorVis.position = center + delta.normalized * radius;
                rollingWindow.Enqueue(Instantiate(cursorClone, cursorVis.position, cursorVis.rotation, transform));
                if(rollingWindow.Count > rollingWindowSize) Destroy(rollingWindow.Dequeue());
            }

            var snapshot = rollingWindow.ToArray();
            trickAnalyzer.AnalyzeSnapshot(snapshot);
            
            
            
        }
        else cursorVis.position = transform.position;
    }


    void RollingWindow()
    {
        rollingWindow.Enqueue(Instantiate(cursorClone, cursorVis.position, cursorVis.rotation, transform));
        if(rollingWindow.Count > rollingWindowSize) Destroy(rollingWindow.Dequeue());
        Debug.Log(rollingWindow.Count);
    }
    
}
