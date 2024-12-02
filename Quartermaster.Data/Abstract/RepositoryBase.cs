using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Quartermaster.Data.Abstract;

public class RepositoryBase<T> {
    protected void EnsureSetGuid(T model, Expression<Func<T, Guid>> expr) {
        var propInfo = (PropertyInfo)((MemberExpression)expr.Body).Member;
        var currGuid = (Guid)propInfo.GetValue(model)!;

        if (currGuid != Guid.Empty)
            return;

        propInfo.SetValue(model, Guid.NewGuid());
    }

    protected void ThrowOnEmptyGuid(T model, Expression<Func<T, Guid>> expr) {
        var propInfo = (PropertyInfo)((MemberExpression)expr.Body).Member;
        var currGuid = (Guid)propInfo.GetValue(model)!;

        if (currGuid == Guid.Empty)
            throw new ArgumentNullException(propInfo.Name);
    }
}