using System;
using System.Text.Json;
using LinqToDB.Mapping;
using Quartermaster.Api.TravelExpenses;

namespace Quartermaster.Data.TravelExpenses;

public enum TravelExpenseStatus {
    Draft = 0,
    Submitted = 1
}

/// <summary>
/// A logged-in member's travel expense application. The whole input is kept as JSON because the trip timeline is a
/// free list of module kinds; the columns beside it only serve the listing.
/// </summary>
[Table(TableName, IsColumnAttributeRequired = false)]
public class TravelExpenseReimbursement {
    public const string TableName = "TravelExpenseReimbursements";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OwnerUserId { get; set; }
    public TravelExpenseStatus Status { get; set; } = TravelExpenseStatus.Draft;
    public Guid? TravelChapterId { get; set; }
    public string ChapterName { get; set; } = "";
    public string Purpose { get; set; } = "";
    public DateTime? StartAt { get; set; }
    public decimal Total { get; set; }
    public string InputJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }

    public TravelExpenseInputDTO ReadInput() {
        return JsonSerializer.Deserialize<TravelExpenseInputDTO>(InputJson) ?? new TravelExpenseInputDTO();
    }

    /// <summary>Stores the input and the listing columns derived from it.</summary>
    public void WriteInput(TravelExpenseInputDTO input, decimal total) {
        InputJson = JsonSerializer.Serialize(input);
        TravelChapterId = input.TravelChapterId;
        ChapterName = Truncate(input.TravelChapterName, 256);
        Purpose = Truncate(input.Purpose, 256);
        StartAt = input.Start.At;
        Total = total;
    }

    public TravelExpenseListItemDTO ToListItem() {
        return new TravelExpenseListItemDTO {
            Id = Id,
            Status = (TravelExpenseStatusDTO)(int)Status,
            ChapterName = ChapterName,
            Purpose = Purpose,
            StartAt = StartAt,
            Total = Total,
            UpdatedAt = AsUtc(UpdatedAt),
            SubmittedAt = SubmittedAt == null ? null : AsUtc(SubmittedAt.Value)
        };
    }

    public TravelExpenseResponseDTO ToResponse() {
        return new TravelExpenseResponseDTO {
            Id = Id,
            Status = (TravelExpenseStatusDTO)(int)Status,
            CreatedAt = AsUtc(CreatedAt),
            UpdatedAt = AsUtc(UpdatedAt),
            SubmittedAt = SubmittedAt == null ? null : AsUtc(SubmittedAt.Value),
            Input = ReadInput()
        };
    }

    private static DateTimeOffset AsUtc(DateTime value) {
        return new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    }

    private static string Truncate(string value, int maxLength) {
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
