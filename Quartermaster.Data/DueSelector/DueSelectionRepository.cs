using LinqToDB;
using Quartermaster.Data.Abstract;

namespace Quartermaster.Data.DueSelector; 

public class DueSelectionRepository : RepositoryBase<DueSelection> {
    private readonly DbContext _context;

    public DueSelectionRepository(DbContext context) {
        _context = context;
    }

    public void Create(DueSelection selection) => _context.Insert(selection);
}