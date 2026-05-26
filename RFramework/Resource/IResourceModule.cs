
using RFramework.ObjectPool;
using System;
using System.Collections.Generic;
using YooAsset;

namespace RFramework.Resource
{
    /// <summary>
    /// 资源管理器接口。
    /// </summary>
    public interface IResourceModule
    {


        /// <summary>
        /// 获取资源模式。
        /// </summary>
        EPlayMode ResourceMode
        {
            get;
        }






    }
}
