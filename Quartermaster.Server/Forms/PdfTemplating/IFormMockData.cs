using System.Collections.Generic;

namespace Quartermaster.Server.Forms.PdfTemplating;

/// <summary>Supplies a representative sample model for a form key so the template editor can render a live preview without real data. One implementation per form, resolved by <see cref="FormKey"/>.</summary>
public interface IFormMockData {
    string FormKey { get; }
    IDictionary<string, object> BuildModel();
}
