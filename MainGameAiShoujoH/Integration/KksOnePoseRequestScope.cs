using System;
namespace MainGameAiShoujoH.Integration
{
    internal sealed class KksOnePoseRequestScope
    {
        object owner;
        bool captured;
        internal bool Active => owner != null;
        internal void Run(object requestOwner, Action begin)
        {
            if (requestOwner == null || begin == null || Active) throw new InvalidOperationException("KKS一件試験scopeの要求が不正か再入しています。");
            owner = requestOwner; captured = false;
            try { begin(); if (!captured) throw new InvalidOperationException("既存の同期Begin要求がありませんでした。"); }
            finally { owner = null; captured = false; }
        }
        internal bool Capture(object requestOwner, string motionId)
        {
            if (!Active) return false;
            if (captured || !ReferenceEquals(owner, requestOwner) || motionId != "houshi:2")
                throw new InvalidOperationException("KKS一件試験は同所有者のhoushi:2要求一回だけです。");
            captured = true; return true;
        }
    }
}
