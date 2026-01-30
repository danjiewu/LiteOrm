using LiteOrm.Demo.DAO;

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
