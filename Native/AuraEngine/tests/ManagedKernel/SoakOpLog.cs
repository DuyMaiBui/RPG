using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AuraEngine.KernelTests
{
    /* Every operation of an episode, kept in memory for failure reports and mirrored unbuffered to a file so the
       last line names the call that crashed the process. */
    internal sealed class SoakOpLog : IDisposable
    {
        private readonly List<string> _lines = new List<string>();
        private readonly FileStream _file;

        public SoakOpLog(string path, string header)
        {
            if (string.IsNullOrEmpty(path))
                return;
            _file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 1);
            var bytes = Encoding.UTF8.GetBytes("# " + header + "\n");
            _file.Write(bytes, 0, bytes.Length);
        }

        public int Count => _lines.Count;

        public string this[int index] => _lines[index];

        public void Add(string line)
        {
            _lines.Add(line);
            if (_file != null)
            {
                var bytes = Encoding.UTF8.GetBytes(line + "\n");
                _file.Write(bytes, 0, bytes.Length);
            }
        }

        public void Append(string suffix)
        {
            if (_lines.Count > 0)
                _lines[_lines.Count - 1] += suffix;
            if (_file != null)
            {
                var bytes = Encoding.UTF8.GetBytes("   " + suffix + "\n");
                _file.Write(bytes, 0, bytes.Length);
            }
        }

        public string Tail(int count)
        {
            var builder = new StringBuilder();
            for (var i = Math.Max(0, _lines.Count - count); i < _lines.Count; i++)
                builder.AppendLine(_lines[i]);
            return builder.ToString();
        }

        public string Window(int center, int radius)
        {
            var builder = new StringBuilder();
            for (var i = Math.Max(0, center - radius); i < Math.Min(_lines.Count, center + radius + 1); i++)
                builder.AppendLine(_lines[i]);
            return builder.ToString();
        }

        void IDisposable.Dispose() => _file?.Dispose();
    }
}
