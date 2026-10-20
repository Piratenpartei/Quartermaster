using Quartermaster.Api;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Data.AssemblyMotions;

namespace Quartermaster.Server.AssemblyMotions;

public static class AssemblyMotionPublicView {
    public static AssemblyMotionPublicListItemDTO BuildListItem(AssemblyMotion motion, AssemblyMotionType type) {
        return new() {
            Id = motion.Id,
            TypeId = type.Id,
            TypeCode = type.Code,
            TypeName = type.Name,
            DisplayNumber = motion.Number == null ? "" : AssemblyMotionNumber.Format(type.Code, motion.Number.Value),
            NumberLabel = AssemblyMotionNumber.Label(type.Code, motion.Number, motion.ReferenceNumber),
            Title = motion.Title,
            Status = motion.Status,
            IsInRevision = motion.IsInRevision,
            Result = motion.Result,
            SubmittedAt = motion.SubmittedAt.ToDtoUtc()
        };
    }

    public static AssemblyMotionPublicDTO Build(AssemblyMotion motion, AssemblyMotionType type) {
        return Fill(new AssemblyMotionPublicDTO(), motion, type);
    }

    public static T Fill<T>(T dto, AssemblyMotion motion, AssemblyMotionType type) where T : AssemblyMotionPublicDTO {
        var item = BuildListItem(motion, type);

        dto.Id = item.Id;
        dto.TypeId = item.TypeId;
        dto.TypeCode = item.TypeCode;
        dto.TypeName = item.TypeName;
        dto.DisplayNumber = item.DisplayNumber;
        dto.NumberLabel = item.NumberLabel;
        dto.Title = item.Title;
        dto.Status = item.Status;
        dto.IsInRevision = item.IsInRevision;
        dto.Result = item.Result;
        dto.SubmittedAt = item.SubmittedAt;
        dto.Section = motion.Section;
        dto.TextHtml = motion.TextHtml;
        dto.ReasonHtml = motion.ReasonHtml;
        dto.Summary = motion.Summary;
        dto.Keywords = motion.Keywords;
        dto.PriorDiscussion = motion.PriorDiscussion;
        dto.CheckNote = motion.CheckNote;
        dto.RequiredMajority = type.RequiredMajority;
        return dto;
    }
}
