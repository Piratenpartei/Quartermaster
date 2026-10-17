using System;

namespace Quartermaster.Server.AssemblyAssistant.Mumble;

/// <summary>
/// Mumble's prefix-coded variable-length integer from the voice packet format (not protobuf LEB128).
/// Mirrors <c>PacketDataStream::operator&gt;&gt;(quint64&amp;)</c> in the Mumble sources.
/// </summary>
public static class MumbleVarint {
    public static bool TryRead(ReadOnlySpan<byte> data, ref int offset, out ulong value) {
        value = 0;
        if (offset >= data.Length)
            return false;

        var first = data[offset];
        var position = offset + 1;

        if ((first & 0x80) == 0x00) {
            value = (ulong)(first & 0x7F);
        } else if ((first & 0xC0) == 0x80) {
            if (!TryReadBytes(data, ref position, 1, out var rest))
                return false;
            value = ((ulong)(first & 0x3F) << 8) | rest;
        } else if ((first & 0xE0) == 0xC0) {
            if (!TryReadBytes(data, ref position, 2, out var rest))
                return false;
            value = ((ulong)(first & 0x1F) << 16) | rest;
        } else if ((first & 0xF0) == 0xE0) {
            if (!TryReadBytes(data, ref position, 3, out var rest))
                return false;
            value = ((ulong)(first & 0x0F) << 24) | rest;
        } else if (!TryReadLongForm(data, first, ref position, out value)) {
            return false;
        }

        offset = position;
        return true;
    }

    private static bool TryReadLongForm(ReadOnlySpan<byte> data, byte first, ref int position, out ulong value) {
        value = 0;
        switch (first & 0xFC) {
            case 0xF0:
                return TryReadBytes(data, ref position, 4, out value);
            case 0xF4:
                return TryReadBytes(data, ref position, 8, out value);
            case 0xF8:
                if (!TryRead(data, ref position, out var inner))
                    return false;
                value = ~inner;
                return true;
            case 0xFC:
                value = ~(ulong)(first & 0x03);
                return true;
            default:
                return false;
        }
    }

    private static bool TryReadBytes(ReadOnlySpan<byte> data, ref int position, int count, out ulong value) {
        value = 0;
        if (position + count > data.Length)
            return false;

        for (var i = 0; i < count; i++)
            value = (value << 8) | data[position + i];

        position += count;
        return true;
    }
}
