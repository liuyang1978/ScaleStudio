using System.Text;

namespace ScaleStudio.Weighing;

/// <summary>Assembles ASCII frames across arbitrary read boundaries. Oversize frames are discarded until the next delimiter.</summary>
public sealed class FrameDecoder
{
    private readonly string _delimiter;
    private readonly int _max;
    private readonly StringBuilder _buffer = new();
    private bool _discard;
    public FrameDecoder(string delimiter, int maxFrameLength = 4096)
    {
        if (string.IsNullOrEmpty(delimiter) || delimiter.Any(c=>c > 127)) throw new ArgumentException("ASCII delimiter required.");
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxFrameLength);
        _delimiter = delimiter;
        _max = maxFrameLength;
    }
    public IReadOnlyList<string> Push(ReadOnlySpan<byte> bytes)
    {
        var frames = new List<string>();
        foreach (var b in bytes)
        {
            _buffer.Append((char)b);
            if (_buffer.Length >= _delimiter.Length &&
                _buffer.ToString(_buffer.Length-_delimiter.Length,_delimiter.Length) == _delimiter)
            {
                var n = _buffer.Length-_delimiter.Length;
                if (!_discard && n > 0 && n <= _max) frames.Add(_buffer.ToString(0,n));
                _buffer.Clear(); _discard = false;
            }
            else if (_buffer.Length > (long)_max + _delimiter.Length - 1)
            {
                _discard = true;
                _buffer.Remove(0,_buffer.Length-Math.Min(_delimiter.Length-1,_buffer.Length));
            }
        }
        return frames;
    }
}
