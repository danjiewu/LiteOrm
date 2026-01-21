using Autofac.Extras.DynamicProxy;
using LiteOrm.Common;
using LiteOrm.Demo.DAO;
using LiteOrm.Service;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiteOrm.Demo.Services
{
    public interface ServiceFactory
    {
        IUserService UserService { get; }
        ISalesService SalesService { get; }
        IBusinessService BusinessService { get; }
        IDepartmentService DepartmentService { get; }

        IUserCustomDAO UserCustomDAO { get; }
    }
}
