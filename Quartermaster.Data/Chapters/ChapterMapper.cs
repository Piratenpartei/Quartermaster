using Quartermaster.Api.Chapters;
using Riok.Mapperly.Abstractions;

namespace Quartermaster.Data.Chapters;

[Mapper]
public static partial class ChapterMapper {
    public static partial ChapterDTO ToDto(this Chapter chapter);
}
