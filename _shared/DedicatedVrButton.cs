using System;
using System.Linq;
using Valve.VR;

namespace CanonPlugins.Shared
{
    internal sealed class DedicatedVrButton : IDisposable
    {
        private SteamVR_ActionSet _set;
        internal SteamVR_Action_Boolean Action { get; private set; }
        internal DedicatedVrButton(string name)
        {
            try
            {
                _set=SteamVR_ActionSet.Create<SteamVR_ActionSet>("/actions/"+name);
                SteamVR_Input.actionSets=Add(SteamVR_Input.actionSets,_set);
                SteamVR_Input.PreinitializeActionSetDictionaries();
                Action=SteamVR_Action.Create<SteamVR_Action_Boolean>("/actions/"+name+"/in/b");
                SteamVR_Input.actions=Add<SteamVR_Action>(SteamVR_Input.actions,Action);
                SteamVR_Input.actionsIn=Add<ISteamVR_Action_In>(SteamVR_Input.actionsIn,Action);
                SteamVR_Input.actionsBoolean=Add(SteamVR_Input.actionsBoolean,Action);
                SteamVR_Input.actionsNonPoseNonSkeletonIn=Add<ISteamVR_Action_In>(SteamVR_Input.actionsNonPoseNonSkeletonIn,Action);
                SteamVR_Input.PreinitializeActionDictionaries();_set.FinishPreInitialize();
                _set.Initialize(true);Action.Initialize(true);
                if (_set.handle==0 || Action.handle==0) throw new InvalidOperationException("Dedicated VR B binding missing: "+name);
                _set.Activate(SteamVR_Input_Sources.LeftHand,0,false);
                _set.Activate(SteamVR_Input_Sources.RightHand,0,false);
            }
            catch { Dispose();throw; }
        }
        private static T[] Add<T>(T[] values,T value)=>(values??new T[0]).Concat(new[]{value}).ToArray();
        private T[] Remove<T>(T[] values)=>(values??new T[0]).Where(x=>!ReferenceEquals(x,Action)&&!ReferenceEquals(x,_set)).ToArray();
        public void Dispose()
        {
            if (_set==null) return;
            try
            {
                _set.Deactivate(SteamVR_Input_Sources.LeftHand);
                _set.Deactivate(SteamVR_Input_Sources.RightHand);
            }
            finally
            {
                SteamVR_Input.actions=Remove(SteamVR_Input.actions);
                SteamVR_Input.actionsIn=Remove(SteamVR_Input.actionsIn);
                SteamVR_Input.actionsBoolean=Remove(SteamVR_Input.actionsBoolean);
                SteamVR_Input.actionsNonPoseNonSkeletonIn=Remove(SteamVR_Input.actionsNonPoseNonSkeletonIn);
                SteamVR_Input.actionSets=Remove(SteamVR_Input.actionSets);
                SteamVR_Input.PreinitializeActionDictionaries();SteamVR_Input.PreinitializeActionSetDictionaries();
                _set=null;Action=null;
            }
        }
    }
}
