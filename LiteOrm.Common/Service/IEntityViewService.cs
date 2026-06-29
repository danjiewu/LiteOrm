using LiteOrm.Common;
using System;
using System.Collections;
using System.Collections.Generic;

namespace LiteOrm.Service
{

    /// <summary>
    /// 实体类查询接口
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    [Service]
    [ServicePermission(true)]
    [ServiceLog(LogLevel = ServiceLogLevel.Debug)]
    public interface IEntityViewService<T> : IEntityViewService
    {
        /// <summary>
        /// 获取实体
        /// </summary>
        /// <param name="id">实体主键</param>
        /// <param name="tableArgs">表名参数</param>
        /// <returns>id对应实体，若不存在则返回null</returns>
        [ServiceMethod]
        new T GetObject(object id, params string[] tableArgs);
        /// <summary>
        /// 根据条件获取单个实体
        /// </summary>
        /// <param name="expr">查询条件，若为null则表示没有条件</param>
        /// <param name="tableArgs">表名参数</param>
        /// <returns>第一个符合条件的实体，若不存在则返回null</returns>
        [ServiceMethod]
        new T SearchOne(Expr expr, params string[] tableArgs);
        /// <summary>
        /// 根据条件遍历对象
        /// </summary>
        /// <param name="expr">查询条件，若为null则表示没有条件</param>
        /// <param name="func">调用的函数委托</param>
        /// <param name="tableArgs">表名参数</param>
        [ServiceMethod(false)]
        void ForEach(Expr expr, Action<T> func, params string[] tableArgs);
        /// <summary>
        /// 根据条件获取实体列表
        /// </summary>
        /// <param name="expr">查询条件，若为null则表示没有条件</param>
        /// <param name="tableArgs">表名参数</param>
        /// <returns>符合条件的实体列表</returns>
        [ServiceMethod]
        new List<T> Search(Expr expr = null, params string[] tableArgs);

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="selectExpr"></param>
        /// <param name="tableArgs"></param>
        /// <returns></returns>
        [ServiceMethod]
        List<TResult> SearchAs<TResult>(SelectExpr selectExpr, params string[] tableArgs);

        /// <summary>
        /// 根据条件获取单个符合条件的实体，并将结果转换为指定类型的对象。
        /// </summary>
        /// <typeparam name="TResult">结果类型</typeparam>
        /// <param name="selectExpr">完整查询表达式</param>
        /// <param name="tableArgs">表名参数</param>
        /// <returns></returns>
        [ServiceMethod]
        TResult SearchOneAs<TResult>(SelectExpr selectExpr, params string[] tableArgs);
    }

    /// <summary>
    /// 提供对实体视图（只读或关联视图）进行查询操作的非泛型接口。
    /// </summary>  
    [AutoRegister(false)]
    [ServicePermission(true)]
    [ServiceLog(LogLevel = ServiceLogLevel.Debug)]
    public interface IEntityViewService
    {
        /// <summary>
        /// 获取实体
        /// </summary>
        /// <param name="id">实体主键</param>
        /// <param name="tableArgs">表名参数</param>
        /// <returns>id对应实体，若不存在则返回null</returns>
        object GetObject(object id, params string[] tableArgs);
        /// <summary>
        /// 检测ID是否存在
        /// </summary>
        /// <param name="id">实体主键</param>
        /// <param name="tableArgs">表名参数</param>
        /// <returns>是否存在记录</returns>
        [ServiceMethod]
        bool ExistsID(object id, params string[] tableArgs);
        /// <summary>
        /// 根据条件检查是否存在记录
        /// </summary>
        /// <param name="expr">查询条件，若为null则表示没有条件</param>
        /// <param name="tableArgs">表名参数</param>
        /// <returns>是否存在记录</returns>
        [ServiceMethod]
        bool Exists(Expr expr, params string[] tableArgs);
        /// <summary>
        /// 根据条件获取记录总数
        /// </summary>
        /// <param name="expr">查询条件，若为null则表示没有条件</param>
        /// <param name="tableArgs">表名参数</param>
        /// <returns>符合条件的记录总数</returns>
        [ServiceMethod]
        int Count(Expr expr = null, params string[] tableArgs);
        /// <summary>
        /// 根据条件获取单个实体
        /// </summary>
        /// <param name="expr">查询条件，若为null则表示没有条件</param>
        /// <param name="tableArgs">表名参数</param>
        /// <returns>第一个符合条件的实体，若不存在则返回null</returns>
        object SearchOne(Expr expr, params string[] tableArgs);
        /// <summary>
        /// 根据条件获取实体列表
        /// </summary>
        /// <param name="expr">查询条件，若为null则表示没有条件</param>
        /// <param name="tableArgs">表名参数</param>
        /// <returns>符合条件的实体列表</returns>
        IList Search(Expr expr = null, params string[] tableArgs);
    }
}
