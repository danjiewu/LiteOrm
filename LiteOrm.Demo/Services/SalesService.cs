using LiteOrm;
using LiteOrm.Common;
using LiteOrm.Demo.Models;
using LiteOrm.Framework;
using LiteOrm.Service;
using Microsoft.Extensions.DependencyInjection;

namespace LiteOrm.Demo.Services
{
    /// <summary>
    /// 销售服务实现
    /// </summary>
    [AutoRegister(Lifetime.Scoped, typeof(ISalesService))]
    public class SalesService : EntityService<SalesRecord, SalesRecordView>, ISalesService
    {
        public SalesService(ObjectDAO<SalesRecord> objectDAO, ObjectViewDAO<SalesRecordView> objectViewDAO)
            : base(objectDAO, objectViewDAO)
        {
        }
    }
}
