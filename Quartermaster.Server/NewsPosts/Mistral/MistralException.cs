using System;

namespace Quartermaster.Server.NewsPosts.Mistral;

public class MistralException : Exception {
    public MistralException(string message) : base(message) { }
}
