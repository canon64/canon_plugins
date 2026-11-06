namespace MainGameVoiceFaceEventBridge
{
    internal sealed partial class Plugin
    {
        private bool PrepareActorContactVoice(ChaControl actor)
        {
            if(SyringeDialogueActive||!SessionGate.Allowed||actor==null)return false;
            int index=-1;
            for(int i=0;i<SessionGate.Females.Count;i++)if(SessionGate.Females[i]==actor){index=i;break;}
            if(index<0)return false;
            var flags=SessionGate.Flags;
            if(flags!=null&&flags.transVoiceMouth!=null&&index<flags.transVoiceMouth.Length&&flags.transVoiceMouth[index]!=null)
                Manager.Voice.Stop(flags.transVoiceMouth[index]);
            Manager.Voice.Stop(actor.transform);
            return true;
        }
    }
}
