using LiteOrm.Demo.Models;
using LiteOrm.Service;

namespace LiteOrm.Demo.Services
{
    public interface ISalesService : 
        IEntityService<SalesRecord>, IEntityServiceAsync<SalesRecord>, 
        IEntityViewService<SalesRecordView>, IEntityViewServiceAsync<SalesRecordView>
    {
    }
}
