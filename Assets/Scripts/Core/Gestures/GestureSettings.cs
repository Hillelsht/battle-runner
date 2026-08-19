using System;

namespace BattleRunner.Core.Gestures
{
    /// <summary>
    /// Tunable classifier thresholds (doc 02). Authored on an InputSettings
    /// ScriptableObject in the Data layer; plain data here so the classifier
    /// stays engine-free and unit-testable.
    /// </summary>
    [Serializable]
    public struct GestureSettings
    {
        /// <summary>Finger travel (cm) before the gesture is classified.</summary>
        public float CommitDistanceCm;

        /// <summary>|dy| must exceed AxisRatio * |dx| to classify as a vertical candidate.
        /// Biased toward LaneDrag: a mis-read drag self-corrects, a mis-fired spell wastes a cooldown.</summary>
        public float AxisRatio;

        /// <summary>Minimum vertical velocity (cm/s) for a vertical candidate to confirm as a flick.</summary>
        public float MinFlickVelocityCmPerSec;

        /// <summary>Vertical candidates still touching after this long (s) without reaching
        /// flick velocity demote to LaneDrag — it was a sloppy drag start.</summary>
        public float FlickMaxDurationSec;

        public static GestureSettings Default => new GestureSettings
        {
            CommitDistanceCm = 0.8f,
            AxisRatio = 1.5f,
            MinFlickVelocityCmPerSec = 25f,
            FlickMaxDurationSec = 0.2f
        };
    }
}
