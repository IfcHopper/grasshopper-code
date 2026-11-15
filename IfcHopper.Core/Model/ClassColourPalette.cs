using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using IfcHopper.Core.IO;

namespace IfcHopper.Core.Model
{
    /// <summary>Read-only display palette with stable pseudo-random colours per IFC class and optional exact-class overrides.</summary>
    /// <remarks>Does not read geometry or mutate model styles. Colours are independent of object order, process and current culture.
    /// No growing cache is retained. Automatic colours are opaque; manual colours retain transparency. Instances support concurrent reads.</remarks>
    public sealed class ClassColourPalette
    {
        private readonly Dictionary<string, Colour> _overrides = new Dictionary<string, Colour>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Creates a palette, copying the paired override lists.</summary>
        /// <param name="classes">Exact IFC class names to override, ignoring case and surrounding whitespace. * sets the fallback for all other classes. Null means no overrides.</param>
        /// <param name="colours">Colours paired by index with classes. Null means no overrides.</param>
        /// <exception cref="ArgumentException">List lengths differ, or an override has a blank, unknown or duplicate class or a null colour.</exception>
        public ClassColourPalette(IReadOnlyList<string> classes = null, IReadOnlyList<Colour> colours = null)
        {
            if ((classes?.Count ?? 0) != (colours?.Count ?? 0))
                throw new ArgumentException($"Give one Colour per override Class ({classes?.Count ?? 0} classes, {colours?.Count ?? 0} colours).");
            for (int i = 0; i < (classes?.Count ?? 0); i++)
            {
                var name = classes[i]?.Trim() == "*" ? "*" : CanonicalClass(classes[i]);
                if (colours[i] == null) throw new ArgumentException($"Override Colour {i + 1} for {name} is empty. Supply a colour or remove the pair.");
                if (!_overrides.TryAdd(name, colours[i]))
                    throw new ArgumentException($"Override Class {i + 1} repeats {name}. Give each class only once, ignoring case.");
            }
        }

        /// <summary>Gets an exact-class override, then the * fallback if supplied, else a stable pseudo-random colour.</summary>
        /// <param name="ifcClass">IFC class name, ignoring case and surrounding whitespace.</param>
        /// <returns>An immutable colour; overrides do not apply to subclasses.</returns>
        /// <exception cref="ArgumentException">The name is blank or is not an IFC class known to the active backend.</exception>
        public Colour GetColour(string ifcClass)
        {
            var name = CanonicalClass(ifcClass);
            if (_overrides.TryGetValue(name, out var colour)) return colour;
            if (_overrides.TryGetValue("*", out var fallback)) return fallback;
            // A fixed digest avoids Random/string.GetHashCode process seeds and unbounded per-class
            // state. Restrict channels to a readable midrange; hash collisions are possible.
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(name.ToUpperInvariant()));
            return new Colour((64 + hash[0] % 160) / 255.0, (64 + hash[1] % 160) / 255.0, (64 + hash[2] % 160) / 255.0);
        }

        private static string CanonicalClass(string text) =>
            !string.IsNullOrWhiteSpace(text) && IfcBackend.Schema.FindClass(text.Trim()) is string name ? name
                : throw new ArgumentException($"'{text}' is not an IFC class. Give a class name such as IfcWall or IfcSlab.");
    }
}
