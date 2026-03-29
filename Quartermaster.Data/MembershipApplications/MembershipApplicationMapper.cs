using Quartermaster.Api.MembershipApplications;
using Riok.Mapperly.Abstractions;

namespace Quartermaster.Data.MembershipApplications;

[Mapper]
public static partial class MembershipApplicationMapper {
    [MapperIgnoreSource(nameof(MembershipApplicationDTO.DueSelection))]
    public static partial MembershipApplication FromDto(MembershipApplicationDTO dto);
}
