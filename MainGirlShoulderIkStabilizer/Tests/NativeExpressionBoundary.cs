using U = UnityEngine;

// Test boundary for the real ExpressionBone dictionary and its four private switches.
public sealed class ExpressionBone
{
    public enum BoneName {
        cf_j_forearm01_L=4,cf_j_forearm01_R=7,cf_j_hand_L=20,cf_j_hand_R=25,
        cf_j_arm00_L=28,cf_d_shoulder02_L,cf_d_arm01_L,cf_d_arm02_L,cf_d_arm03_L,n_goal_forearm_L,
        cf_j_arm00_R,cf_d_shoulder02_R,cf_d_arm01_R,cf_d_arm02_R,cf_d_arm03_R,n_goal_forearm_R,
        cf_j_spine03=60
    }
    public bool enable=true;
    public int GetInstanceID()=>1;
    public Action NativeCall;
    public void LateUpdate() { NativeCall?.Invoke(); }
    private bool enableShoulderL=true,enableShoulderR=true,enableUpperarmL=true,enableUpperarmR=true;
    private bool enableBust=true,enableElboL=true;
    public int DictionaryReads;
    public readonly Dictionary<int,U.Transform> Bones=new();
    public Dictionary<int,U.Transform> dictBone {get {DictionaryReads++;return Bones;}}
    public void DisableShoulder() {enableShoulderL=enableShoulderR=enableUpperarmL=enableUpperarmR=false;}
}
