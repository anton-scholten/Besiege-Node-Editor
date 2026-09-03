using UnityEngine;

namespace TimerPlusMod
{
    /// <summary>
    /// What a row does with time: Besiege's own <c>TimerBlock</c> state machine,
    /// with the game taken out of it.
    ///
    /// It is here rather than in the behaviour for one reason: it is the part of
    /// this mod whose failure is silent. A row that fires forty milliseconds late,
    /// or loses a tick every time it loops, does not throw and does not log -- it
    /// just drifts away from the real timer sitting beside it on the same machine,
    /// which is exactly what a block claiming to be that timer must not do. Pure
    /// and free of Unity objects, so tools/tests/TableCheck.cs can run it against
    /// the tick counts read out of the game.
    ///
    /// Nothing here touches a key. The clock says what a row *wants*
    /// (<see cref="Row.Wants"/>) and the behaviour reconciles that against what is
    /// actually held, which is the rule in notes/08: check it, do not remember it.
    /// </summary>
    public static class Clock
    {
        /// <summary>
        /// Emulation ticks a second. <c>Machine.FixedUpdate</c> drives the
        /// emulation pass on every other 100 Hz fixed step, which is where
        /// Besiege's own timer gets the 50 in its own frame count.
        /// </summary>
        public const float TicksPerSecond = 50f;

        /// <summary>
        /// Seconds as emulation ticks, never fewer than one.
        ///
        /// `TimerBlock.TimeToFrameCount`, exactly: ceiling, floored at one. A press
        /// that lasts no ticks is a press nothing sees, and the game's own timer
        /// has the same floor for the same reason.
        /// </summary>
        public static int Ticks(float seconds)
        {
            int count = Mathf.CeilToInt(seconds * TicksPerSecond);
            return count < 1 ? 1 : count;
        }

        public static void Start(Row row)
        {
            if (row.Phase != Row.Idle)
            {
                return;
            }
            row.Phase = Row.Waiting;
            row.Entered = Row.Idle;
            row.Ticks = 0;
        }

        public static void Stop(Row row)
        {
            row.Phase = Row.Idle;
            row.Entered = Row.Idle;
            row.Ticks = 0;
            row.Wants = false;
        }

        /// <summary>
        /// One emulation tick.
        ///
        /// At most two phases advance in one tick. A wait that runs out hands the
        /// tick straight to the press, exactly as the game's own `ElapseDelay`
        /// falls into `ElapseEmulation`, and a press that ends into a loop hands it
        /// back to the wait the same way -- without that, a looping row loses a
        /// tick every cycle and drifts. Bounded rather than a `while`, so nothing
        /// can spin however the times are set.
        /// </summary>
        public static void Tick(Row row, float wait, float press, bool loop)
        {
            for (int step = 0; step < 2; step++)
            {
                if (row.Phase == Row.Idle || Advance(row, wait, press, loop))
                {
                    return;
                }
            }
        }

        /// <summary>Runs one phase for one tick. True when the tick was spent,
        /// false when the phase ended and the next one should have it.</summary>
        private static bool Advance(Row row, float wait, float press, bool loop)
        {
            if (row.Phase == Row.Waiting)
            {
                if (row.Entered != Row.Waiting)
                {
                    row.Length = Ticks(wait);
                    row.Entered = Row.Waiting;
                    row.Ticks = 0;
                }
                if (row.Ticks < row.Length)
                {
                    row.Ticks++;
                    return true;
                }
                row.Phase = Row.Pressing;
                row.Entered = Row.Idle;
                row.Ticks = 0;
                return false;
            }

            if (row.Entered != Row.Pressing)
            {
                row.Wants = true;
                row.Length = Ticks(press);
                row.Entered = Row.Pressing;
                row.Ticks = 0;
            }
            if (row.Ticks < row.Length)
            {
                row.Ticks++;
                return true;
            }

            row.Wants = false;
            row.Phase = loop ? Row.Waiting : Row.Idle;
            row.Entered = Row.Idle;
            row.Ticks = 0;
            return false;
        }

        /// <summary>
        /// What a press means to one row: Besiege's <c>TimerBlock.CheckKeys</c>,
        /// verbatim in structure. Hold-to-run makes the activation a switch;
        /// otherwise a press starts a row that is idle and stops a running one only
        /// where it is allowed to.
        /// </summary>
        public static void Pressed(Row row, bool pressed, bool held,
                                   bool holdToRun, bool canStop, bool loops)
        {
            if (holdToRun)
            {
                if (held)
                {
                    if (pressed)
                    {
                        Start(row);
                    }
                }
                else
                {
                    Stop(row);
                }
                return;
            }

            if (!pressed)
            {
                return;
            }
            if (row.Phase != Row.Idle)
            {
                if (canStop)
                {
                    Stop(row);
                }
            }
            else
            {
                Start(row);
            }
        }
    }
}
