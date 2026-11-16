using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xbim.Common;
using Xbim.Ifc4.Interfaces;

namespace IfcHopper.Core.Backends.Xbim
{
    internal static class XbimAbstractProductChecks
    {
        // xBIM discards abstract entity instances during STEP parsing. Inspect entity
        // declarations in the source too, using this file's schema metadata, not IFC4X3
        // metadata for every schema. This is a diagnostic, not a repair or full validator.
        public static IEnumerable<string> Warnings(IModel model, string path)
        {
            var abstractProducts = model.Metadata.Types()
                .Where(t => t.Type.IsAbstract && typeof(IIfcProduct).IsAssignableFrom(t.Type))
                .ToDictionary(t => t.Name.ToUpperInvariant(), t => t.Name, StringComparer.Ordinal);
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            using (var reader = File.OpenText(path))
            {
                foreach (var name in EntityNames(reader))
                    if (abstractProducts.TryGetValue(name, out var canonical))
                        counts[canonical] = counts.TryGetValue(canonical, out var count) ? count + 1 : 1;
            }
            return counts.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p =>
                $"{p.Key}: {p.Value} invalid abstract product instance(s) omitted by the IFC parser. " +
                "Abstract IFC classes cannot be instantiated. Correct the classification to an appropriate concrete IFC class " +
                "in the authoring application and re-export; these products are unavailable in the IfcHopper model.").ToArray();
        }

        // Stream tokens so multiline declarations work without loading geometry text into
        // memory. Quoted strings and comments cannot introduce entity declarations.
        private static IEnumerable<string> EntityNames(TextReader reader)
        {
            var state = 0;
            int value;
            while ((value = reader.Read()) >= 0)
            {
                var c = (char)value;
                if (c == '\'')
                {
                    while ((value = reader.Read()) >= 0)
                        if (value == '\'')
                        {
                            if (reader.Peek() != '\'') break;
                            reader.Read();
                        }
                    state = 0;
                    continue;
                }
                if (c == '/' && reader.Peek() == '*')
                {
                    reader.Read();
                    while ((value = reader.Read()) >= 0)
                        if (value == '*' && reader.Peek() == '/') { reader.Read(); break; }
                    continue;
                }
                if (char.IsWhiteSpace(c)) continue;
                if (c == '#') { state = 1; continue; }
                if (state == 1 && c >= '0' && c <= '9') { state = 2; continue; }
                if (state == 2 && c >= '0' && c <= '9') continue;
                if (state == 2 && c == '=') { state = 3; continue; }
                if (state == 3 && char.IsLetter(c))
                {
                    var name = new StringBuilder();
                    name.Append(char.ToUpperInvariant(c));
                    while (reader.Peek() >= 0 && (char.IsLetterOrDigit((char)reader.Peek()) || reader.Peek() == '_'))
                    {
                        var next = (char)reader.Read();
                        if (name.Length < 128) name.Append(char.ToUpperInvariant(next));
                    }
                    yield return name.ToString();
                }
                state = 0;
            }
        }
    }
}
