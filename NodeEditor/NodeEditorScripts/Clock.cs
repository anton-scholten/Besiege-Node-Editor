using UnityEngine;

namespace NodeEditorMod
{
    /// <summary>
    /// Besiege's <c>TimerBlock</c> state machine, free of Unity so TableCheck can
    /// test it: a timer that drifts fails silently. It only says what a row wants
    /// (<see cref="Row.Wants"/>); the behaviour presses the keys.
    /// </summary>
    public static class Clock
    {
        /// <summary>Emulation ticks a second: the pass runs on every other 100 Hz
        /// step.
        /// </summary>
        public const float TicksPerSecond = 50f;

        /// <summary>Seconds as ticks: `TimerBlock.TimeToFrameCount`, a ceiling
        /// floored at one.</summary>
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

        /// <summary>One tick. A phase that ends hands the tick on, as the game's
        /// `ElapseDelay` does, so a loop loses no tick. At most two
        /// phases.</summary>
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

        /// <summary>A press, as Besiege's <c>TimerBlock.CheckKeys</c>: hold-to-run
        /// makes it a switch; otherwise it starts an idle row and stops a running
        /// one if allowed.
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
