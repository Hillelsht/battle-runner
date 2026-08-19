using System;

namespace BattleRunner.Core.Gestures
{
    public enum GestureEventType
    {
        None = 0,
        LaneTarget = 1,
        FlickUp = 2,
        FlickDown = 3
    }

    public readonly struct GestureEvent
    {
        public readonly GestureEventType Type;
        /// <summary>Screen-relative [0..1] X for LaneTarget events.</summary>
        public readonly float LaneNormalizedX;

        public GestureEvent(GestureEventType type, float laneNormalizedX = 0f)
        {
            Type = type;
            LaneNormalizedX = laneNormalizedX;
        }

        public static readonly GestureEvent None = new GestureEvent(GestureEventType.None);
    }

    /// <summary>
    /// Pure-C# gesture state machine (doc 02):
    ///   Idle -> TouchActive -> (LaneDrag | VerticalCandidate) -> Idle on release.
    ///
    /// Lane control is a positional horizontal drag (continuous LaneTarget events);
    /// spells are high-velocity vertical flicks. One gesture per contact: a touch that
    /// classified as LaneDrag can never emit a flick, eliminating the
    /// "dodge accidentally casts" bug class. A vertical candidate that never reaches
    /// flick velocity within the flick window demotes to LaneDrag.
    /// </summary>
    public sealed class GestureClassifier
    {
        private enum State
        {
            Idle,
            TouchActive,
            LaneDrag,
            VerticalCandidate
        }

        private readonly GestureSettings _settings;
        private State _state = State.Idle;
        private TouchSample _down;
        private TouchSample _previous;

        public GestureClassifier(GestureSettings settings)
        {
            if (settings.CommitDistanceCm <= 0f || settings.AxisRatio <= 0f ||
                settings.MinFlickVelocityCmPerSec <= 0f || settings.FlickMaxDurationSec <= 0f)
                throw new ArgumentException("All gesture thresholds must be positive.", nameof(settings));
            _settings = settings;
        }

        public GestureEvent OnTouchDown(TouchSample sample)
        {
            _state = State.TouchActive;
            _down = sample;
            _previous = sample;
            return GestureEvent.None;
        }

        public GestureEvent OnTouchMove(TouchSample sample)
        {
            switch (_state)
            {
                case State.TouchActive:
                    return ClassifyOrWait(sample);
                case State.LaneDrag:
                    _previous = sample;
                    return new GestureEvent(GestureEventType.LaneTarget, sample.NormalizedX);
                case State.VerticalCandidate:
                    return UpdateVerticalCandidate(sample);
                default:
                    return GestureEvent.None;
            }
        }

        public GestureEvent OnTouchUp(TouchSample sample)
        {
            State state = _state;
            _state = State.Idle;

            if (state == State.VerticalCandidate)
            {
                // Release confirms the flick if it was fast enough overall.
                float dy = sample.PositionCm.Y - _down.PositionCm.Y;
                float duration = Math.Max(1e-4f, sample.Time - _down.Time);
                float velocity = Math.Abs(dy) / duration;
                if (velocity >= _settings.MinFlickVelocityCmPerSec)
                    return Flick(dy);
            }

            return GestureEvent.None;
        }

        private GestureEvent ClassifyOrWait(TouchSample sample)
        {
            float dx = sample.PositionCm.X - _down.PositionCm.X;
            float dy = sample.PositionCm.Y - _down.PositionCm.Y;
            float travelSq = dx * dx + dy * dy;
            float commit = _settings.CommitDistanceCm;
            if (travelSq < commit * commit)
            {
                _previous = sample;
                return GestureEvent.None;
            }

            if (Math.Abs(dy) > _settings.AxisRatio * Math.Abs(dx))
            {
                _state = State.VerticalCandidate;
                return UpdateVerticalCandidate(sample);
            }

            _state = State.LaneDrag;
            _previous = sample;
            return new GestureEvent(GestureEventType.LaneTarget, sample.NormalizedX);
        }

        private GestureEvent UpdateVerticalCandidate(TouchSample sample)
        {
            float dt = Math.Max(1e-4f, sample.Time - _previous.Time);
            float instantVelocity = Math.Abs(sample.PositionCm.Y - _previous.PositionCm.Y) / dt;
            _previous = sample;

            if (instantVelocity >= _settings.MinFlickVelocityCmPerSec)
            {
                _state = State.Idle; // gesture consumed; ignore until next touch down
                return Flick(sample.PositionCm.Y - _down.PositionCm.Y);
            }

            if (sample.Time - _down.Time > _settings.FlickMaxDurationSec)
            {
                // Too slow for too long: it was a sloppy drag start.
                _state = State.LaneDrag;
                return new GestureEvent(GestureEventType.LaneTarget, sample.NormalizedX);
            }

            return GestureEvent.None;
        }

        private static GestureEvent Flick(float dy) =>
            new GestureEvent(dy > 0f ? GestureEventType.FlickUp : GestureEventType.FlickDown);
    }
}
