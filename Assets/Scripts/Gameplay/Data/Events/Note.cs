using System.Collections.Generic;
using UnityEngine;

namespace ArcCreate.Gameplay.Data
{
    public abstract class Note : ArcEvent
    {
        /// <summary>
        /// Gets or sets a value indicating whether or not the note is selected.
        /// </summary>
        public bool IsSelected { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the note is drawn in <see cref="MissTint"/>, used to mark the
        /// notes that a Xynapse miss log says were missed.
        /// </summary>
        public bool HasMissTint { get; set; }

        /// <summary>
        /// Gets or sets the colour the note is drawn in while <see cref="HasMissTint"/> is true.
        /// </summary>
        public Color MissTint { get; set; }

        /// <summary>
        /// Applies the miss tint to a colour, keeping its alpha.
        /// </summary>
        /// <param name="color">The colour the note would be drawn in.</param>
        /// <returns>The miss tint with the same alpha, or the colour unchanged when the note is not marked.</returns>
        protected Color ApplyMissTint(Color color)
        {
            return HasMissTint ? new Color(MissTint.r, MissTint.g, MissTint.b, color.a) : color;
        }

        public double FloorPosition { get; set; }

        public virtual int TotalCombo { get; protected set; } = 1;

        public virtual int ComboAt(int timing) => (timing >= Timing) ? 1 : 0;

        public virtual void RecalculateFloorPosition()
        {
            FloorPosition = TimingGroupInstance.GetFloorPosition(Timing);
        }

        public float ZPos(double floorPosition)
            => ArcFormula.FloorPositionToZ(FloorPosition - floorPosition, TimingGroup);

        public abstract void GenerateColliderTriangles(int timing, List<Vector3> vertices, List<int> triangles);
    }
}
