using LinqToDB;
using LinqToDB.Data;
using Quartermaster.Data.Tokens;

namespace Quartermaster.Data; 

public class DbContext : DataConnection {
    public ITable<Token> Tokens => this.GetTable<Token>();
}