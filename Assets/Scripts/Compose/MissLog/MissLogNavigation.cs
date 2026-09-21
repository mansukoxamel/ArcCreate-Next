using ArcCreate.Compose.Navigation;

namespace ArcCreate.Compose.MissLog
{
    /// <summary>
    /// Keyboard actions that walk through the misses of the miss log that was dropped onto the editor.
    /// </summary>
    [EditorScope("MissLog")]
    public class MissLogNavigation
    {
        /// <summary>
        /// Moves the playback position to the next missed note.
        /// </summary>
        [EditorAction("Next", true, "<a-n>")]
        [KeybindHint(Priority = KeybindPriorities.Clipboard + 10)]
        public void Next()
        {
            MissLogSession.Step(1);
        }

        /// <summary>
        /// Moves the playback position to the previous missed note.
        /// </summary>
        [EditorAction("Previous", true, "<a-p>")]
        [KeybindHint(Priority = KeybindPriorities.Clipboard + 11)]
        public void Previous()
        {
            MissLogSession.Step(-1);
        }
    }
}
