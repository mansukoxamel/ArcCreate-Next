using System;
using System.Collections.Generic;

namespace ArcCreate.Compose.MissLog
{
    /// <summary>
    /// Kind of a recorded miss.
    /// </summary>
    public enum MissKind
    {
        /// <summary>
        /// The note (or hold / arc tick) was not judged in time.
        /// </summary>
        Lost,

        /// <summary>
        /// The note was hit inside the FAR window.
        /// </summary>
        Far,
    }

    /// <summary>
    /// Note type recorded in the miss log.
    /// </summary>
    public enum MissNoteType
    {
        /// <summary>
        /// A tap note on the ground lanes.
        /// </summary>
        Tap,

        /// <summary>
        /// A hold note on the ground lanes.
        /// </summary>
        Hold,

        /// <summary>
        /// An arc tap note.
        /// </summary>
        ArcTap,

        /// <summary>
        /// An arc note.
        /// </summary>
        Arc,
    }

    /// <summary>
    /// One row of a miss log written by Xynapse.
    /// </summary>
    public sealed class MissRecord
    {
        /// <summary>
        /// Gets or sets the chart time of the note, in milliseconds.
        /// </summary>
        public int NoteMs { get; set; }

        /// <summary>
        /// Gets or sets the miss kind.
        /// </summary>
        public MissKind Kind { get; set; }

        /// <summary>
        /// Gets or sets the note type.
        /// </summary>
        public MissNoteType NoteType { get; set; }

        /// <summary>
        /// Gets or sets the lane (1 to 4) of a tap or hold, or 0 when the log has no lane for this row.
        /// </summary>
        public int Lane { get; set; }

        /// <summary>
        /// Gets or sets the chart time at which the game judged this row, in milliseconds.
        /// For a lost hold or arc this is the time of the missed tick.
        /// </summary>
        public int JudgedMs { get; set; }

        /// <summary>
        /// Gets or sets the "chart_info" column: lane, hold end, arc colour and position, or the arc an arc tap lies on.
        /// </summary>
        public string ChartInfo { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the 1-based line number in the file, used in error messages.
        /// </summary>
        public int LineNumber { get; set; }
    }

    /// <summary>
    /// A parsed miss log file written by Xynapse (the "Download/Xynapse/miss_*.txt" files).
    /// </summary>
    public sealed class MissLogFile
    {
        /// <summary>
        /// Gets or sets the format version (the number after "miss log v").
        /// </summary>
        public int Version { get; set; }

        /// <summary>
        /// Gets or sets the song id (the song folder name).
        /// </summary>
        public string SongId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the song title.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the difficulty text, for example "FUTURE 7".
        /// </summary>
        public string Difficulty { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the play date text.
        /// </summary>
        public string Date { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the practice speed text, for example "100%".
        /// </summary>
        public string Speed { get; set; } = string.Empty;

        /// <summary>
        /// Gets the rows of the log, in file order.
        /// </summary>
        public List<MissRecord> Records { get; } = new List<MissRecord>();
    }

    /// <summary>
    /// Thrown when a miss log cannot be read. The message names the line and the reason.
    /// </summary>
    public sealed class MissLogFormatException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MissLogFormatException"/> class.
        /// </summary>
        /// <param name="message">The reason, in Japanese, shown to the user.</param>
        public MissLogFormatException(string message)
            : base(message)
        {
        }
    }
}
