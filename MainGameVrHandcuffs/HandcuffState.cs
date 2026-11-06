using System;

namespace MainGameVrHandcuffs
{
    // Pure interaction state; engine-side transactions commit only after successful acquisition.
    internal sealed class HandcuffState
    {
        internal int Held { get; private set; } = -1;
        private readonly int[] _wrists = { -1, -1 };
        internal int Wrist(int cuff) => _wrists[cuff];
        internal bool TryGrab(int cuff)
        {
            if (cuff<0 || cuff>1 || Held!=-1) return false;
            Held=cuff; return true;
        }
        internal void Release() { Held=-1; }
        internal bool CanBind(int cuff,int wrist) => cuff>=0 && cuff<2 && Held==cuff
            && wrist>=0 && wrist<2 && _wrists[cuff]<0 && _wrists[1-cuff]!=wrist;
        internal void CommitBinding(int cuff,int wrist)
        {
            if (!CanBind(cuff,wrist)) throw new InvalidOperationException("Invalid handcuff binding transaction");
            _wrists[cuff]=wrist;
        }
        internal void ClearBinding(int cuff) { _wrists[cuff]=-1; }
        internal void Reset() { Held=-1; _wrists[0]=_wrists[1]=-1; }
    }
}
