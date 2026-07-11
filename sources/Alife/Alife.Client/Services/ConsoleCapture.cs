using System.IO;
using System.Text;

namespace Alife.Components.Services;

public static class ConsoleCapture
{
    const int Capacity = 5000;
    static readonly LogEntry[] buffer = new LogEntry[Capacity];
    static int head;
    static int count;
    static readonly object syncLock = new();

    static TextWriter? originalOut;
    static TextWriter? originalError;

    public static void Install()
    {
        originalOut = Console.Out;
        originalError = Console.Error;
        Console.SetOut(new CapturingWriter("INF", originalOut));
        Console.SetError(new CapturingWriter("ERR", originalError));
    }

    internal static void Push(LogEntry entry)
    {
        string message = entry.Message.Trim();
        if (string.IsNullOrEmpty(message)) return;

        entry = entry with { Message = message };
        lock (syncLock)
        {
            buffer[head] = entry;
            head = (head + 1) % Capacity;
            if (count < Capacity) count++;
        }
    }

    public static IReadOnlyList<LogEntry> GetBuffer()
    {
        lock (syncLock)
        {
            LogEntry[] result = new LogEntry[count];
            int start = (head - count + Capacity) % Capacity;
            for (int i = 0; i < count; i++)
                result[i] = buffer[(start + i) % Capacity];
            return result;
        }
    }

    public static void Clear()
    {
        lock (syncLock)
        {
            head = 0;
            count = 0;
        }
    }

    sealed class CapturingWriter : TextWriter
    {
        readonly string level;
        readonly TextWriter inner;
        readonly StringBuilder line = new();

        public CapturingWriter(string level, TextWriter inner)
        {
            this.level = level;
            this.inner = inner;
        }

        public override Encoding Encoding => inner.Encoding;

        void FlushLine()
        {
            if (line.Length == 0) return;
            string msg = line.ToString().TrimEnd('\r');
            line.Clear();
            Push(new LogEntry(DateTime.Now, level, msg));
        }

        void Append(char c)
        {
            if (c == '\n') FlushLine();
            else line.Append(c);
        }

        public override void Write(char value)
        {
            inner.Write(value);
            Append(value);
        }

        public override void Write(char[] buffer, int index, int count)
        {
            inner.Write(buffer, index, count);
            for (int i = 0; i < count; i++)
                Append(buffer[index + i]);
        }

        public override void Write(string? value)
        {
            if (value == null) return;
            inner.Write(value);
            foreach (char c in value)
                Append(c);
        }

        public override void WriteLine(string? value)
        {
            inner.WriteLine(value);
            if (value != null) line.Append(value);
            FlushLine();
        }

        public override void WriteLine()
        {
            inner.WriteLine();
            FlushLine();
        }
    }
}

public readonly record struct LogEntry(DateTime Timestamp, string Level, string Message);