using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DAL;
using MyOrm.Service;

namespace MyOrm.Test
{
    public interface ICOMMUNITYService:IEntityService<COMMUNITY>,IEntityViewService<COMMUNITY> { }
    public class COMMUNITYService:EntityService<COMMUNITY>, ICOMMUNITYService
    {
    }
}
