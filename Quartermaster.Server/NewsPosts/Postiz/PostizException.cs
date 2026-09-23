using System;

namespace Quartermaster.Server.NewsPosts.Postiz;

public class PostizException : Exception {
    public PostizException(string message) : base(message) { }
}
