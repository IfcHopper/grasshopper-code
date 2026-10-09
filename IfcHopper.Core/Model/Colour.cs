using System;

namespace IfcHopper.Core.Model
{
    /// <summary>Surface colour of a mesh (IfcSurfaceStyleShading): red, green, blue and transparency, each from 0 to 1.</summary>
    public sealed class Colour : IEquatable<Colour>
    {
        private const double Tolerance = 1e-3;

        public double Red { get; }
        public double Green { get; }
        public double Blue { get; }

        /// <summary>0 is opaque, 1 is fully transparent.</summary>
        public double Transparency { get; }

        public Colour(double red, double green, double blue, double transparency = 0)
        {
            Red = Clamp(red);
            Green = Clamp(green);
            Blue = Clamp(blue);
            Transparency = Clamp(transparency);
        }

        /// <summary>Equal within the precision of 8-bit colour channels.</summary>
        public bool Equals(Colour other) =>
            other != null && Math.Abs(Red - other.Red) < Tolerance && Math.Abs(Green - other.Green) < Tolerance &&
            Math.Abs(Blue - other.Blue) < Tolerance && Math.Abs(Transparency - other.Transparency) < Tolerance;

        public override bool Equals(object obj) => Equals(obj as Colour);

        public override int GetHashCode() => (Round(Red), Round(Green), Round(Blue), Round(Transparency)).GetHashCode();

        public override string ToString() => $"RGB({Round(Red)}, {Round(Green)}, {Round(Blue)})" + (Transparency > 0 ? $" transparency {Transparency:0.##}" : "");

        private static int Round(double value) => (int)Math.Round(value * 255);

        private static double Clamp(double value) => double.IsNaN(value) ? 0 : Math.Max(0, Math.Min(1, value));
    }
}
