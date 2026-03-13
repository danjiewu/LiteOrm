using LiteOrm;
using LiteOrm.Demo.DAO;
using LiteOrm.Demo.Models;

namespace LiteOrm.Demo.Services
{
    public interface ServiceFactory
    {
        IUserService UserService { get; }
        ISalesService SalesService { get; }
        IBusinessService BusinessService { get; }
        IDepartmentService DepartmentService { get; }

        IUserCustomDAO UserCustomDAO { get; }
        ObjectViewDAO<SalesRecord> SalesDAO { get; }
    }
}
