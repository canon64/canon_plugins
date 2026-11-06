namespace MainGameAiShoujoH.Progression
{
    /// <summary>Actual session observations; null denotes a native branch that is not yet connected.</summary>
    public sealed class NativeEndingState
    {
        public bool IsFaintness, IsForce, HadControlAction, Pain, Constraint, SleepStart;
        public bool PainSelected, ConstraintSelected, PainAction, ConstraintAction;
        public int FemaleOrgasmTotal, MaleOrgasmTotal, InvitePattern;
        public int? FemalePlayerOrgasmTotal;
        public bool HadAibu, HadHoushi, HadSonyu;
        public bool? HadAnal;
        public float YobaiBareRate, Skill2;
        public string Evidence;
    }
}
