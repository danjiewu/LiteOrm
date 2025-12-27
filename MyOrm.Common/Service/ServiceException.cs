using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace MyOrm.Service
{
    [Serializable]
    public class ServiceException : Exception
    {
        public ServiceException() : base() { }
        public ServiceException(string message) : base(message) { }
        public ServiceException(string message, Exception inner) : base(message, inner) { }
    }
}
