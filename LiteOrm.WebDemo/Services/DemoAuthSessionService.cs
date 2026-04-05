using LiteOrm.Common;
using LiteOrm.Service;
using LiteOrm.WebDemo.Models;

namespace LiteOrm.WebDemo.Services;

public interface IDemoAuthSessionService :
    IEntityServiceAsync<DemoAuthSession>,
    IEntityViewServiceAsync<DemoAuthSession>
{
}

public class DemoAuthSessionService : EntityService<DemoAuthSession>, IDemoAuthSessionService
{
    public DemoAuthSessionService(ObjectDAO<DemoAuthSession> objectDAO, ObjectViewDAO<DemoAuthSession> objectViewDAO)
        : base(objectDAO, objectViewDAO)
    {
    }
}
