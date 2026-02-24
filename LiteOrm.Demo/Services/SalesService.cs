using LiteOrm.Common;
using LiteOrm.Demo.Models;
using LiteOrm.Service;
using Microsoft.Extensions.DependencyInjection;

namespace LiteOrm.Demo.Services
{
    /// <summary>
    /// 销售服务实现
    /// </summary>
    [AutoRegister(Lifetime = ServiceLifetime.Scoped)]
    public class SalesService : EntityService<SalesRecord, SalesRecordView>, ISalesService
    {
    }
}
