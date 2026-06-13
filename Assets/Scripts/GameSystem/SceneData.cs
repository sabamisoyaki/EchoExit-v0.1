using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class PlacedItem
{
    public string prefabName;
    public Vector3 position;
    public Vector3 rotation;
    public string tag;
}

[Serializable]
public class SceneRecord
{
    public int sceneId;
    public string AnomaryHouse;
    public List<PlacedItem> items;
}

[Serializable]
public class AllScenesData
{
    public List<SceneRecord> scenes = new List<SceneRecord>();
}
