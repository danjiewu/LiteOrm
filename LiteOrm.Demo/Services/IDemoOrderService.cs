using LiteOrm.Common;
using LiteOrm.Service;
using LiteOrm.Demo.Models;
using System.Threading;

namespace LiteOrm.Demo.Services;

public interface IDemoOrderService :
    IEntityServiceAsync<DemoOrder>,
    IEntityViewServiceAsync<DemoOrderView>
{
}
