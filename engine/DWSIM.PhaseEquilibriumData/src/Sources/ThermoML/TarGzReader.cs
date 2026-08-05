using System;
using System.Collections.Generic;
using System.IO;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;

namespace DWSIM.PhaseEquilibriumData.Sources.ThermoML
{
    public sealed class TarGzReader : IDisposable
    {
        private readonly Stream _input;
        private readonly bool _leaveOpen;
        private GZipInputStream? _gz;
        private TarInputStream? _tar;

        public TarGzReader(Stream input, bool leaveOpen = false)
        {
            _input = input ?? throw new ArgumentNullException(nameof(input));
            _leaveOpen = leaveOpen;
        }

        public static TarGzReader FromFile(string path)
            => new TarGzReader(File.OpenRead(path), leaveOpen: false);

        public IEnumerable<(string Name, Stream Content)> ReadEntries()
        {
            _gz = new GZipInputStream(_input);
            _tar = new TarInputStream(_gz, System.Text.Encoding.UTF8);
            TarEntry? entry;
            while ((entry = _tar.GetNextEntry()) != null)
            {
                if (entry.IsDirectory) continue;
                if (entry.Size == 0) continue;
                var name = entry.Name;
                if (!name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) &&
                    !name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;

                var buffer = new MemoryStream(checked((int)entry.Size));
                _tar.CopyEntryContents(buffer);
                buffer.Position = 0;
                yield return (name, buffer);
            }
        }

        public void Dispose()
        {
            _tar?.Dispose();
            _gz?.Dispose();
            if (!_leaveOpen) _input.Dispose();
        }
    }
}
