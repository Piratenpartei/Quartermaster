using System;

namespace Quartermaster.Server.AssemblyMotions.OpenSlides;

public class OpenSlidesException : Exception {
    public string? Code { get; }

    public OpenSlidesException(string message, string? code = null, Exception? inner = null)
        : base(message, inner) {
        Code = code;
    }
}
