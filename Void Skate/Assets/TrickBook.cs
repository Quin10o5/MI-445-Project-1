using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "TrickBook", menuName = "TrickBook")]
public class TrickBook : ScriptableObject
{
    public TrickInfo[] tricks;

    public Trick FindTrick(string key)
    {
        for (int i = 0; i < tricks.Length; i++)
        {
            if(tricks[i].key == key) return tricks[i].trick;
        }

        return null;
    }
    
}




[System.Serializable]
public class TrickInfo
{
    public string key;
    public Trick trick;
}