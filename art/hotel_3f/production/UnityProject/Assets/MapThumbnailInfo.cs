using System;
using System.Collections.Generic;
using UnityEngine;
public class MapThumbnailInfo : ScriptableObject {
 [Serializable] public class Param { public string Name;public int ID;public string Bundle,Asset; }
 public List<Param> param=new List<Param>();
}
