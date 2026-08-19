using LiteOrm;
using LiteOrm.Common;
using LiteOrm.Demo.Models;
using LiteOrm.DependencyInjection;
using LiteOrm.Service;
using Microsoft.Extensions.DependencyInjection;

namespace LiteOrm.Demo.Services
{
    /// <summary>
    /// 销售服务实现
    /// </summary>
    [AutoRegister(RegisterPolicy.Interface, Lifetime = Lifetime.Scoped)]
    public class SalesService : EntityService<SalesRecord, SalesRecordView>, ISalesService
    {
        public SalesService(IServiceProvider serviceProvider)
            : base(serviceProvider)
        {
        }
    }
}
