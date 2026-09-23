using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Fluid;
using Fluid.Values;
using Quartermaster.Server.Banking;

namespace Quartermaster.Server.Forms.PdfTemplating;

public static class PdfTemplateFilters {
    private static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");

    public static void Register(TemplateOptions options, BankCodeBicLookup? bicLookup) {
        options.Filters.AddFilter("money", (input, args, ctx) => {
            var d = ToDecimal(input);
            return new StringValue(d.ToString("N2", De) + " €");
        });

        options.Filters.AddFilter("amount", (input, args, ctx) => {
            var d = ToDecimal(input);
            return new StringValue(d.ToString("N2", De));
        });

        options.Filters.AddFilter("german_date", (input, args, ctx) => {
            var format = args.At(0).Or(new StringValue("dd.MM.yyyy")).ToStringValue();
            var raw = input.ToObjectValue();
            // Fluid wraps DateTime values as DateTimeOffset at the configured zone; use the wall-clock
            // component (o.DateTime), not UtcDateTime, so a midnight date doesn't shift back a day.
            DateTime? dt = raw switch {
                DateTime d when d != default => d,
                DateTimeOffset o => o.DateTime,
                string s when DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) => parsed,
                _ => null
            };
            return dt.HasValue ? new StringValue(dt.Value.ToString(format, De)) : StringValue.Empty;
        });

        options.Filters.AddFilter("sum_by", (input, args, ctx) => {
            var key = args.At(0).ToStringValue();
            decimal total = 0m;
            if (input is ArrayValue arr) {
                foreach (var v in arr.Values) {
                    var obj = v.ToObjectValue();
                    if (obj == null) continue;
                    var prop = obj.GetType().GetProperty(key);
                    if (prop != null) {
                        total += ToDecimal(prop.GetValue(obj));
                    }
                }
            }
            return NumberValue.Create(total);
        });

        options.Filters.AddFilter("bic_lookup", (input, args, ctx) => {
            if (bicLookup == null) {
                return StringValue.Empty;
            }
            var iban = input.ToStringValue();
            return new StringValue(bicLookup.LookupByIban(iban) ?? "");
        });
    }

    private static decimal ToDecimal(object? value) {
        return value switch {
            null => 0m,
            decimal d => d,
            double dbl => (decimal)dbl,
            float f => (decimal)f,
            int i => i,
            long l => l,
            FluidValue fv => ToDecimal(fv.ToObjectValue()),
            string s when decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => 0m
        };
    }

    private static decimal ToDecimal(FluidValue value) => ToDecimal(value.ToObjectValue());
}
