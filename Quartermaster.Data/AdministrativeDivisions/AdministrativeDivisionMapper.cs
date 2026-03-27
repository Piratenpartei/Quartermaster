using Quartermaster.Api.AdministrativeDivisions;
using Riok.Mapperly.Abstractions;

namespace Quartermaster.Data.AdministrativeDivisions;

[Mapper]
public static partial class AdministrativeDivisionMapper {
    public static partial AdministrativeDivisionDTO ToDto(this AdministrativeDivision division);
}
