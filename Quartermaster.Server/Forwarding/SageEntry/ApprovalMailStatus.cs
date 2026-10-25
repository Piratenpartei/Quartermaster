using System;
using Quartermaster.Api.MembershipApplications;

namespace Quartermaster.Server.Forwarding.SageEntry;

public record ApprovalMailStatus(ApprovalMailState State, DateTime? SentAt, string? FederalTicketUrl);
