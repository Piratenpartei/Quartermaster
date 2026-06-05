using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.Forms.ExpenseReimbursements;

[Table(TableName, IsColumnAttributeRequired = false)]
public class ExpenseReimbursementLineItem {
    public const string TableName = "ExpenseReimbursementLineItems";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ExpenseReimbursementId { get; set; }
    public int SortOrder { get; set; }
    public string Description { get; set; } = "";
    public decimal Value { get; set; }
}
