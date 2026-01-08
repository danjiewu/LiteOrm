using Microsoft.Extensions.DependencyInjection;
using MyOrm.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyOrm.Service
{
    public interface IServiceFactory
    {
        T GetService<T>() where T : class;
        object GetService(Type serviceType);
    }
}
