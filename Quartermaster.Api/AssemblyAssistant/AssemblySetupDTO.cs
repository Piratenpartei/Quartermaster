using System;
using System.Collections.Generic;

namespace Quartermaster.Api.AssemblyAssistant;

public class AssemblySetupStatusDTO {
    public string Host { get; set; } = "";
    public int Port { get; set; }
    public string Username { get; set; } = "";
    public bool CertificatePresent { get; set; }
    public bool CertificateUnreadable { get; set; }
    public string? CertificateHash { get; set; }
    public DateTime? CertificateNotAfter { get; set; }
    public Dictionary<ChannelRole, uint> Channels { get; set; } = new();
}

public class AssemblySetupGenerateCertificateRequest {
    public bool Replace { get; set; }
}

public class AssemblySetupChannelDTO {
    public uint Id { get; set; }
    public uint? ParentId { get; set; }
    public string Name { get; set; } = "";
    public List<uint> Links { get; set; } = [];
    public int UserCount { get; set; }
}

public class AssemblySetupProblemDTO {
    public ChannelRole? Role { get; set; }
    public string MessageKey { get; set; } = "";
}

public class AssemblySetupCheckDTO {
    public bool Connected { get; set; }
    public string? ErrorKey { get; set; }
    public string? ErrorDetail { get; set; }
    public string? ServerVersion { get; set; }
    public string? ServerCertificateSha256 { get; set; }
    public bool Registered { get; set; }
    public List<AssemblySetupChannelDTO> Channels { get; set; } = [];
    public List<AssemblySetupProblemDTO> Problems { get; set; } = [];
}

public class AssemblySetupRegisterDTO {
    public bool Registered { get; set; }
    public string? ErrorKey { get; set; }
    public string? ErrorDetail { get; set; }
}

public class AssemblySetupSaveChannelsRequest {
    public Dictionary<ChannelRole, uint> Channels { get; set; } = new();
}
