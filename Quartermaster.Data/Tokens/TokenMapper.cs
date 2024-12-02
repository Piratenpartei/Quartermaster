using Quartermaster.Api.Tokens;
using Riok.Mapperly.Abstractions;

namespace Quartermaster.Data.Tokens;

[Mapper]
public static partial class TokenMapper {
    public static partial TokenDTO ToDto(this Token token);
}