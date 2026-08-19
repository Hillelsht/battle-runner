using System.Collections.Generic;
using System.Numerics;
using BattleRunner.Core.Gestures;
using NUnit.Framework;

namespace BattleRunner.Tests
{
    [TestFixture]
    public class GestureClassifierTests
    {
        private GestureClassifier _classifier;

        [SetUp]
        public void SetUp() => _classifier = new GestureClassifier(GestureSettings.Default);

        /// <summary>Feeds a straight-line trace and returns every non-None event.</summary>
        private List<GestureEvent> Trace(Vector2 from, Vector2 to, float durationSec, int steps = 8)
        {
            var events = new List<GestureEvent>();
            void Collect(GestureEvent e)
            {
                if (e.Type != GestureEventType.None) events.Add(e);
            }

            Collect(_classifier.OnTouchDown(new TouchSample(from, from.X / 10f, 0f)));
            for (int i = 1; i <= steps; i++)
            {
                float t = i / (float)steps;
                Vector2 p = Vector2.Lerp(from, to, t);
                Collect(_classifier.OnTouchMove(new TouchSample(p, p.X / 10f, t * durationSec)));
            }
            Collect(_classifier.OnTouchUp(new TouchSample(to, to.X / 10f, durationSec)));
            return events;
        }

        [Test]
        public void CleanHorizontalDrag_EmitsOnlyLaneTargets()
        {
            var events = Trace(new Vector2(2f, 5f), new Vector2(7f, 5f), 0.5f);
            Assert.IsNotEmpty(events);
            foreach (GestureEvent e in events)
                Assert.AreEqual(GestureEventType.LaneTarget, e.Type);
        }

        [Test]
        public void LaneTarget_TracksFingerX()
        {
            var events = Trace(new Vector2(2f, 5f), new Vector2(8f, 5f), 0.5f);
            Assert.AreEqual(0.8f, events[events.Count - 1].LaneNormalizedX, 1e-3f);
        }

        [Test]
        public void FastUpwardFlick_EmitsFlickUp()
        {
            var events = Trace(new Vector2(5f, 4f), new Vector2(5f, 7f), 0.08f);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(GestureEventType.FlickUp, events[0].Type);
        }

        [Test]
        public void FastDownwardFlick_EmitsFlickDown()
        {
            var events = Trace(new Vector2(5f, 7f), new Vector2(5f, 4f), 0.08f);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(GestureEventType.FlickDown, events[0].Type);
        }

        [Test]
        public void SloppyDiagonalFlick_StillClassifiesVertical()
        {
            // 1 unit sideways drift over 3 units of vertical travel, fast.
            var events = Trace(new Vector2(5f, 4f), new Vector2(6f, 7f), 0.08f);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(GestureEventType.FlickUp, events[0].Type);
        }

        [Test]
        public void ShallowDiagonalDrag_ClassifiesAsLaneDrag()
        {
            // More sideways than up: must be lane control, never a spell.
            var events = Trace(new Vector2(2f, 5f), new Vector2(7f, 6.5f), 0.4f);
            Assert.IsNotEmpty(events);
            foreach (GestureEvent e in events)
                Assert.AreEqual(GestureEventType.LaneTarget, e.Type);
        }

        [Test]
        public void SlowVerticalWander_DemotesToLaneDrag()
        {
            // Vertical direction but far below flick velocity, long contact.
            var events = Trace(new Vector2(5f, 4f), new Vector2(5f, 6f), 1.2f, steps: 24);
            Assert.IsNotEmpty(events);
            foreach (GestureEvent e in events)
                Assert.AreEqual(GestureEventType.LaneTarget, e.Type, "slow wander must never cast");
        }

        [Test]
        public void OneGesturePerContact_DragNeverEmitsFlick()
        {
            var events = new List<GestureEvent>();
            void Collect(GestureEvent e)
            {
                if (e.Type != GestureEventType.None) events.Add(e);
            }

            // Commit to a horizontal drag, then whip violently upward without lifting.
            Collect(_classifier.OnTouchDown(new TouchSample(new Vector2(2f, 5f), 0.2f, 0f)));
            Collect(_classifier.OnTouchMove(new TouchSample(new Vector2(4f, 5f), 0.4f, 0.15f)));
            Collect(_classifier.OnTouchMove(new TouchSample(new Vector2(4f, 11f), 0.4f, 0.2f)));
            Collect(_classifier.OnTouchUp(new TouchSample(new Vector2(4f, 12f), 0.4f, 0.22f)));

            Assert.IsNotEmpty(events);
            foreach (GestureEvent e in events)
                Assert.AreEqual(GestureEventType.LaneTarget, e.Type,
                    "a contact that classified as LaneDrag must never emit a flick");
        }

        [Test]
        public void MicroMovement_EmitsNothing()
        {
            var events = Trace(new Vector2(5f, 5f), new Vector2(5.2f, 5.1f), 0.1f);
            Assert.IsEmpty(events, "movement under the commit distance must stay silent");
        }

        [Test]
        public void FlickConsumesGesture_NoEventsUntilNextTouchDown()
        {
            var events = new List<GestureEvent>();
            void Collect(GestureEvent e)
            {
                if (e.Type != GestureEventType.None) events.Add(e);
            }

            Collect(_classifier.OnTouchDown(new TouchSample(new Vector2(5f, 4f), 0.5f, 0f)));
            Collect(_classifier.OnTouchMove(new TouchSample(new Vector2(5f, 7f), 0.5f, 0.06f)));
            // Finger keeps moving after the flick fired — must be ignored.
            Collect(_classifier.OnTouchMove(new TouchSample(new Vector2(5f, 9f), 0.5f, 0.12f)));
            Collect(_classifier.OnTouchUp(new TouchSample(new Vector2(5f, 10f), 0.5f, 0.2f)));

            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(GestureEventType.FlickUp, events[0].Type);
        }

        [Test]
        public void SecondContact_ClassifiesIndependently()
        {
            Trace(new Vector2(2f, 5f), new Vector2(7f, 5f), 0.4f); // drag
            var events = Trace(new Vector2(5f, 4f), new Vector2(5f, 7f), 0.08f); // then flick
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(GestureEventType.FlickUp, events[0].Type);
        }
    }
}
