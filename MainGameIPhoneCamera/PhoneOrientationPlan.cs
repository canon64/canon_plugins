namespace MainGameIPhoneCamera
{
    internal static class PhoneOrientationPlan
    {
        internal const float Duration = .32f;
        internal static float TargetAngle(float start, int turns)
        {
            float delta = (-90 * (turns & 3) - start) % 360;
            if (delta > 180) delta -= 360;
            if (delta < -180) delta += 360;
            return start + delta;
        }
        internal static int Columns(int turns) => (turns & 1) == 0 ? 3 : 5;
    }
}
