using System.Numerics;

namespace BattleRunner.Core.Gestures
{
    /// <summary>
    /// One touch sample fed to the classifier. Positions are in centimeters
    /// (density-normalized by the caller) so thresholds hold across the Android
    /// device spread; NormalizedX is the screen-relative [0..1] X used for lane targeting.
    /// </summary>
    public readonly struct TouchSample
    {
        public readonly Vector2 PositionCm;
        public readonly float NormalizedX;
        public readonly float Time;

        public TouchSample(Vector2 positionCm, float normalizedX, float time)
        {
            PositionCm = positionCm;
            NormalizedX = normalizedX;
            Time = time;
        }
    }
}
