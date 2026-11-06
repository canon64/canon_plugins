using System;

namespace MainGameVrNeckGrab
{
    // The transition model is independent of Unity, wall-clock frames and saved settings.
    internal sealed class NeckState
    {
        internal const double HoldSeconds = 30;
        internal int Owner { get; private set; } = -1;
        internal long Ticket { get; private set; }
        internal bool Doll { get; private set; }
        internal bool HandPlaced { get; private set; }
        internal float Awakening { get; private set; }
        internal double Started { get; private set; }
        internal bool Muted => Owner >= 0;

        internal bool Begin(int hand, double now)
        {
            if (hand < 0 || hand > 1 || Owner >= 0 || double.IsNaN(now) || double.IsInfinity(now)) return false;
            Owner = hand; Started = now; HandPlaced = true; ++Ticket;
            return true;
        }
        internal bool End(int hand)
        {
            if (hand < 0 || Owner != hand) return false;
            Owner = -1; ++Ticket; return true;
        }
        internal bool CanEnter(long ticket, double now)
            => Owner >= 0 && !Doll && Ticket == ticket && now - Started >= HoldSeconds;
        internal void EnterDoll()
        {
            if (Doll || Owner < 0) throw new InvalidOperationException("No active neck hold");
            Doll = true; Awakening = 0;
        }
        internal bool AddAwakening(float amount)
        {
            if (!Doll || amount <= 0 || float.IsNaN(amount) || float.IsInfinity(amount)) return false;
            Awakening = Math.Min(100, Awakening + amount);
            if (Awakening < 100) return false;
            Doll = false; Owner = -1; ++Ticket; return true;
        }
        internal void RemoveHand() { Owner = -1; HandPlaced = false; ++Ticket; }
        internal void CancelDoll() { Owner = -1; Doll = false; Awakening = 0; ++Ticket; }
        internal void Clear()
        {
            Owner = -1; HandPlaced = Doll = false; Awakening = 0; ++Ticket;
        }
    }
}
