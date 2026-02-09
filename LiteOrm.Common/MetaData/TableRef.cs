using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Linq;

namespace LiteOrm.Common
{
    /// <summary>
    /// Êý¾Ý¿â±íµÄÒýÓÃ
    /// </summary>
    public abstract class TableRef : SqlObject
    {
        /// <summary>
        /// ´´½¨Êý¾Ý¿â±íµÄÒýÓÃ
        /// </summary>
        /// <param name="table">ÒýÓÃµÄÊý¾Ý¿â±í¶¨Òå</param>
        public TableRef(TableDefinition table)
        {
            _tableDefinition = table;
            Name = table.Name;
            _columns = table.Columns.Select(column => new ColumnRef(this, column)).ToList().AsReadOnly();
        }

        private TableDefinition _tableDefinition;
        private ReadOnlyCollection<ColumnRef> _columns;
        private ConcurrentDictionary<string, ColumnRef> _namedColumnCache = new ConcurrentDictionary<string, ColumnRef>();

        /// <summary>
        /// ¶ÔÓ¦Êý¾Ý¿â±íµÄ¶¨Òå
        /// </summary>
        public TableDefinition TableDefinition
        {
            get { return _tableDefinition; }
        }

        /// <summary>
        /// Êý¾Ý¿â±íµÄÁÐÐÅÏ¢
        /// </summary>
        public ReadOnlyCollection<ColumnRef> Columns
        {
            get { return _columns; }
        }

        /// <summary>
        /// ÊôÐÔÃû¶ÔÓ¦ÁÐµÄ»º´æ
        /// </summary>
        protected ConcurrentDictionary<string, ColumnRef> NamedColumnCache
        {
            get
            {
                if (_namedColumnCache.Count == 0)
                {
                    foreach (ColumnRef column in Columns)
                        _namedColumnCache[column.Column.PropertyName] = column;
                }
                return _namedColumnCache;
            }
        }

        /// <summary>
        /// ¸ù¾ÝÊôÐÔÃû»ñµÃÁÐ¶¨Òå£¬ºöÂÔ´óÐ¡Ð´
        /// </summary>
        /// <param name="propertyName">ÊôÐÔÃû</param>
        /// <returns>ÁÐ¶¨Òå£¬ÁÐÃû²»´æÔÚÔò·µ»Ønull</returns>
        public virtual ColumnRef GetColumn(string propertyName)
        {
            if (String.IsNullOrEmpty(propertyName)) return null;
            ColumnRef column;
            NamedColumnCache.TryGetValue(propertyName, out column);
            return column;
        }

        /// <summary>
        /// È·¶¨Ö¸¶¨µÄ¶ÔÏóÊÇ·ñµÈÓÚµ±Ç°¶ÔÏó¡£
        /// </summary>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj)) return true;
            if (obj == null || obj.GetType() != GetType()) return false;
            TableRef other = (TableRef)obj;
            return Equals(TableDefinition, other.TableDefinition) && Name == other.Name;
        }

        /// <summary>
        /// »ñÈ¡¹þÏ£Âë¡£
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                return ((TableDefinition?.GetHashCode() ?? 0) * 31) ^ (Name?.GetHashCode() ?? 0);
            }
        }
    }
}
