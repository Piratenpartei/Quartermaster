using System;
using System.Collections.Generic;

namespace Quartermaster.Api.Dashboard;

public class DashboardDTO {
    public DashboardSection<DashboardApplicationDTO>? PendingApplications { get; set; }
    public DashboardSection<DashboardDueSelectionDTO>? PendingDueSelections { get; set; }
    public DashboardSection<DashboardMotionDTO>? OpenMotions { get; set; }
    public List<DashboardEventDTO>? UpcomingEvents { get; set; }
}

public class DashboardSection<T> {
    public int TotalCount { get; set; }
    public List<T> Items { get; set; } = new();
}

public class DashboardApplicationDTO {
    public Guid Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string ChapterName { get; set; } = "";
    public DateTime SubmittedAt { get; set; }
}

public class DashboardDueSelectionDTO {
    public Guid Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public decimal SelectedDue { get; set; }
}

public class DashboardMotionDTO {
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public string ChapterName { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public class DashboardEventDTO {
    public Guid Id { get; set; }
    public string PublicName { get; set; } = "";
    public string ChapterName { get; set; } = "";
    public DateTime? EventDate { get; set; }
}
