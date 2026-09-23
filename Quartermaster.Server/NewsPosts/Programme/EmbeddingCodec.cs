using System;
using System.Runtime.InteropServices;

namespace Quartermaster.Server.NewsPosts.Programme;

public static class EmbeddingCodec {
    public static byte[] Encode(float[] vector) => MemoryMarshal.AsBytes(vector.AsSpan()).ToArray();

    public static float[] Decode(byte[] bytes) => MemoryMarshal.Cast<byte, float>(bytes.AsSpan()).ToArray();
}
