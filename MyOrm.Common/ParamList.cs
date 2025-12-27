using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyOrm.Common
{
    [Serializable]
    public class ParamList: List<KeyValuePair<string, object>> 
    {
        public ParamList() { }  
        public ParamList(int capacity):base(capacity){ }
        public ParamList(IEnumerable<KeyValuePair<string, object>> collection) : base(collection) { }
        public void Add(string key, object value)
        {   
            base.Add(new KeyValuePair<string, object>(key, value));
        }
    }
}
