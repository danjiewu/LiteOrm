using LiteOrm.AotDemo.Models;
using LiteOrm.Service;

namespace LiteOrm.AotDemo.Services
{
    /// <summary>
    /// 订单业务接口。
    /// </summary>
    public interface IAotOrderService :
        IEntityService<AotOrder>, IEntityServiceAsync<AotOrder>,
        IEntityViewService<AotOrderView>, IEntityViewServiceAsync<AotOrderView>
    {
    }
}
