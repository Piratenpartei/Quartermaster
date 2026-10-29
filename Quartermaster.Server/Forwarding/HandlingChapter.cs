using System;
using Quartermaster.Api.Forwarding;
using Quartermaster.Data.Chapters;

namespace Quartermaster.Server.Forwarding;

/// <param name="Skip">The first marked chapter passed over although it had an application setting, or null.</param>
public record HandlingChapter(Guid? ChapterId, ForwardingMode? Mode, ChapterMarkSkip? Skip);
