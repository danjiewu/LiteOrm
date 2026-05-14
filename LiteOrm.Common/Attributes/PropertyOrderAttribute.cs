using System;
using System.Collections.Generic;
using System.Text;

namespace LiteOrm.Common
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class PropertyOrderAttribute : Attribute
    {
        public PropertyOrderAttribute(int order=0)
        {
            Order = order;
        }

        public int Order { get; }
        public string After { get; set; }
        public string Before { get; set; }
    }
}
