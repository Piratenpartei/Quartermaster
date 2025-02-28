using Quartermaster.Data.Abstract;

namespace Quartermaster.Data.DueSelector; 

public class DueSelectionRepository : RepositoryBase<DueSelection> {
    private readonly SqlContext _context;

    public DueSelectionRepository(SqlContext context) {
        _context = context;
    }

    //public DueSelection Create(DueSelection dueSelection) {
    //    EnsureSetGuid(dueSelection, s => s.Id);

    //    using var con = _context.GetConnection();

    //}
}