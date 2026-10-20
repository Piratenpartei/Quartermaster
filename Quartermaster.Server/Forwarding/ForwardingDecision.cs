using System;
using Quartermaster.Api.Motions;

namespace Quartermaster.Server.Forwarding;

/// <summary>How a case was decided in Quartermaster: by a board vote or directly on the record. <see cref="Path"/> links what was decided.</summary>
public record ForwardingDecision(MotionApprovalStatus Result, Guid ChapterId, DateTime DecidedAt, string Path);
