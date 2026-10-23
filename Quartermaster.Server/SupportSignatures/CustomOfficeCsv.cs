using System.Collections.Generic;
using Quartermaster.Api.SupportSignatures;

namespace Quartermaster.Server.SupportSignatures;

public record CustomOfficeRow(int Line, string MunicipalityKey, string MunicipalityName, string Name, string? Addition, string Street, string PostCode, string City);

/// <summary>The readable rows of an office import, the rows that could not be read, or a problem with the whole file in <see cref="ErrorKey"/>.</summary>
public record CustomOfficeCsv(IReadOnlyList<CustomOfficeRow> Rows, IReadOnlyList<OfficeImportProblemDTO> Problems, int RowCount, string? ErrorKey);
