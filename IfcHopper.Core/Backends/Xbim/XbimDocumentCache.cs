using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using Xbim.IO.Memory;

namespace IfcHopper.Core.Backends.Xbim
{
    /// <summary>
    /// Keeps opened IFC files in memory, keyed by full path, and reopens a file only when it changes on disk.
    /// Replaced models are not disposed: lazily loaded objects may still reference them.
    /// </summary>
    internal static class XbimDocumentCache
    {
        private static readonly Dictionary<string, Entry> Cache = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        private static readonly object Sync = new object();

        // File each opened model came from; weak, so replaced models can still be collected.
        private static readonly ConditionalWeakTable<MemoryModel, string> Paths = new ConditionalWeakTable<MemoryModel, string>();

        public static MemoryModel Open(string path)
        {
            var info = new FileInfo(Path.GetFullPath(path));
            if (!info.Exists) throw new FileNotFoundException("IFC file not found.", info.FullName);

            lock (Sync)
            {
                if (Cache.TryGetValue(info.FullName, out var entry) && entry.LastWrite == info.LastWriteTimeUtc && entry.Length == info.Length)
                    return entry.Model;

                var model = MemoryModel.OpenRead(info.FullName);
                Paths.AddOrUpdate(model, info.FullName);
                Cache[info.FullName] = new Entry(info.LastWriteTimeUtc, info.Length, model);
                return model;
            }
        }

        /// <summary>The file a cached model was opened from, or null.</summary>
        public static string PathOf(MemoryModel model) => Paths.TryGetValue(model, out var path) ? path : null;

        private sealed class Entry
        {
            public DateTime LastWrite { get; }
            public long Length { get; }
            public MemoryModel Model { get; }

            public Entry(DateTime lastWrite, long length, MemoryModel model)
            {
                LastWrite = lastWrite;
                Length = length;
                Model = model;
            }
        }
    }
}
