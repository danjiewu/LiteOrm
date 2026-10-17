using LiteOrm.AotDemo.Models;
using LiteOrm.Service;

namespace LiteOrm.AotDemo.Services
{
    /// <summary>
    /// 工单业务接口。
    /// </summary>
    public interface IAotTicketService :
        IEntityService<AotTicket>, IEntityServiceAsync<AotTicket>,
        IEntityViewService<AotTicket>, IEntityViewServiceAsync<AotTicket>
    {
    }
}
