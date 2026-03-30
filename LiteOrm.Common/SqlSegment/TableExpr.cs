using System;
using System.Linq;
using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    [JsonConverter(typeof(ExprJsonConverterFactory))]
    public sealed class TableExpr : Expr, ISqlSegment
    {
        public TableExpr() { }

        public TableExpr(Type objectType)
        {
            ObjectType = objectType;
        }

        private string _alias;
        public string Alias
        {
            get => _alias;
            set
            {
                ThrowIfInvalidSqlName(nameof(Alias), value);
                _alias = value;
            }
        }

        ISqlSegment ISqlSegment.Source { get => null; set => _ = value; }

        public Type ObjectType { get; set; }

        private string[] _tableArgs;
        public string[] TableArgs
        {
            get => _tableArgs;
            set
            {
                if (value != null)
                {
                    foreach (var arg in value)
                    {
                        ThrowIfInvalidSqlName(nameof(TableArgs), arg);
                    }
                }
                _tableArgs = value;
            }
        }

        public override ExprType ExprType => ExprType.Table;

        public override bool Equals(object obj)
        {
            if (obj is TableExpr other)
            {
                if (!Equals(ObjectType, other.ObjectType)) return false;
                if (Alias != other.Alias) return false;
                if (!ArrayEquals(TableArgs, other.TableArgs)) return false;
                return true;
            }
            return false;
        }

        private static bool ArrayEquals(string[] a, string[] b)
        {
            if (a == null || a.Length == 0) return b == null || b.Length == 0;
            if (b == null || b.Length == 0) return false;
            return a.SequenceEqual(b);
        }

        public override int GetHashCode()
        {
            return OrderedHashCodes(
                typeof(TableExpr).GetHashCode(),
                ObjectType?.GetHashCode() ?? 0,
                Alias?.GetHashCode() ?? 0,
                (TableArgs == null || TableArgs.Length == 0) ? 0 : SequenceHash(TableArgs));
        }

        public override string ToString()
        {
            return ObjectType?.Name ?? string.Empty;
        }

        public override Expr Clone()
        {
            var t = new TableExpr();
            t.ObjectType = this.ObjectType;
            t.Alias = this.Alias;
            t.TableArgs = this.TableArgs == null ? null : (string[])this.TableArgs.Clone();
            return t;
        }
    }
}

