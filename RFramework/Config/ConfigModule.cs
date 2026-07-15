using System;
using System.Collections;
using System.Collections.Generic;

namespace RFramework
{
    /// <summary>
    /// 配置模块核心实现。
    /// 负责配置表的内存缓存与查询，通过 IConfigHelper 与 JSON、二进制及自定义格式解耦。
    /// 不涉及资源加载——字节数据由 Runtime ConfigComponent 通过 ResourceModule 获取后传入。
    /// </summary>
    internal sealed class ConfigModule : RFrameworkModule, IConfigModule
    {
        /// <summary>
        /// 获取框架模块优先级。
        /// 低于 Resource(50)，确保资源模块先更新，关闭时配置先释放。
        /// </summary>
        internal override int Priority
        {
            get { return 30; }
        }

        // ==================== 依赖注入 ====================

        /// <summary>配置辅助器，封装具体格式解析和表对象查询。</summary>
        private IConfigHelper helper;

        /// <summary>事件模块引用，用于分发加载成功/失败事件。惰性获取，可能为 null。</summary>
        private IEventModule eventModule;

        // ==================== 配置缓存 ====================

        /// <summary>已加载配置表缓存：行类型 → Helper 返回的表对象。</summary>
        private readonly Dictionary<Type, object> configTables = new Dictionary<Type, object>();

        /// <summary>已加载配置的元数据缓存：行类型 → 行数量</summary>
        private readonly Dictionary<Type, int> configRowCounts = new Dictionary<Type, int>();

        // ==================== 配置方法 ====================

        /// <summary>
        /// 设置配置辅助器（必须在加载任何配置前调用）。
        /// </summary>
        /// <param name="helper">配置辅助器实例，由 Runtime 层创建并注入。</param>
        public void SetHelper(IConfigHelper helper)
        {
            if (helper == null)
            {
                throw new RFrameworkException("Config helper is invalid.");
            }

            if (configTables.Count > 0 && !ReferenceEquals(this.helper, helper))
            {
                throw new RFrameworkException(
                    "Config helper cannot be replaced while config tables are loaded. "
                    + "Unload all config tables first.");
            }

            this.helper = helper;
        }

        // ==================== 配置加载 ====================

        /// <summary>
        /// 从字节数据加载配置表。
        /// 映射行类型→表类型 → 解析字节 → 缓存表对象。
        /// 重复加载相同类型会覆盖旧数据。
        /// </summary>
        /// <typeparam name="T">配置行类型（如 ItemConfig）。</typeparam>
        /// <param name="configBytes">配置原始字节数据。</param>
        public void LoadConfig<T>(byte[] configBytes) where T : class
        {
            EnsureHelper();

            Type rowType = typeof(T);

            if (configBytes == null || configBytes.Length == 0)
            {
                string errorMsg = $"ConfigModule: LoadConfig<{rowType.Name}> failed: config bytes is empty.";
                FireLoadFailedEvent(rowType, errorMsg);
                throw new RFrameworkException(errorMsg);
            }

            // 旧表引用与旧行数（重复加载时需在覆盖前捕获，失败回滚用）
            bool hasOld = configTables.TryGetValue(rowType, out object oldTable);
            int oldRowCount = hasOld ? configRowCounts[rowType] : 0;
            object newTable = null;

            try
            {
                // 1. 映射行类型 → 表类型（如 ItemConfig → TbItem）
                Type tableType = helper.GetTableType(rowType);

                // 2. 解析字节为表对象
                object parsedTable = helper.ParseConfig(tableType, configBytes);
                if (parsedTable == null)
                {
                    throw new RFrameworkException(
                        $"ConfigModule: helper returned null while parsing '{rowType.Name}' from bytes.");
                }

                newTable = parsedTable;

                // 3. 缓存（覆盖旧表）
                configTables[rowType] = parsedTable;

                // 4. 记录行数（用于事件分发）
                int rowCount = 0;
                try
                {
                    IReadOnlyList<T> allRows = helper.GetAllConfigs<T>(parsedTable);
                    rowCount = allRows != null ? allRows.Count : 0;
                }
                catch
                {
                    // 行数统计失败不影响加载流程
                }

                configRowCounts[rowType] = rowCount;

                // 5. 分发成功事件（暂不释放旧表，待确认新表已安全写入并广播后再释放）
                FireLoadSuccessEvent(rowType, rowCount);
            }
            catch (Exception ex)
            {
                // 失败回滚：恢复旧表（无旧表则移除条目），保证旧表始终可访问且不被泄漏；
                // 同时释放已创建但加载失败的新表。
                if (hasOld)
                {
                    configTables[rowType] = oldTable;
                    configRowCounts[rowType] = oldRowCount;
                }
                else
                {
                    configTables.Remove(rowType);
                    configRowCounts.Remove(rowType);
                }

                if (newTable != null && newTable != oldTable)
                {
                    try { helper.ReleaseConfig(newTable); }
                    catch { /* best-effort */ }
                }

                FireLoadFailedEvent(rowType, ex.Message);
                throw;
            }

            // 新表已安全写入并广播，现在释放被覆盖的旧表（best-effort）
            if (hasOld && oldTable != newTable)
            {
                try { helper.ReleaseConfig(oldTable); }
                catch { /* best-effort */ }
            }
        }

        /// <summary>
        /// 从一个配置容器原子加载多张配置表。
        /// Helper 必须实现 IConfigBundleHelper，解析或提交任一步失败都会恢复旧缓存。
        /// </summary>
        public void LoadConfigBundle(byte[] configBytes)
        {
            EnsureHelper();
            if (!(helper is IConfigBundleHelper bundleHelper))
            {
                throw new RFrameworkException(
                    $"Config helper '{helper.GetType().FullName}' does not support config bundles.");
            }

            if (configBytes == null || configBytes.Length == 0)
            {
                throw new RFrameworkException("ConfigModule: config bundle bytes are empty.");
            }

            IReadOnlyDictionary<Type, object> parsedTables =
                bundleHelper.ParseConfigBundle(configBytes);
            if (parsedTables == null || parsedTables.Count == 0)
            {
                throw new RFrameworkException(
                    "ConfigModule: helper returned an empty config bundle.");
            }

            Dictionary<Type, object> oldTables = new Dictionary<Type, object>();
            Dictionary<Type, int> oldCounts = new Dictionary<Type, int>();
            List<Type> committedTypes = new List<Type>(parsedTables.Count);
            try
            {
                foreach (KeyValuePair<Type, object> pair in parsedTables)
                {
                    if (pair.Key == null || !pair.Key.IsClass || pair.Value == null)
                    {
                        throw new RFrameworkException(
                            "ConfigModule: config bundle contains an invalid table entry.");
                    }

                    if (configTables.TryGetValue(pair.Key, out object oldTable))
                    {
                        oldTables.Add(pair.Key, oldTable);
                        oldCounts.Add(pair.Key, configRowCounts[pair.Key]);
                    }
                }

                foreach (KeyValuePair<Type, object> pair in parsedTables)
                {
                    int rowCount = pair.Value is ICollection collection ? collection.Count : 0;
                    configTables[pair.Key] = pair.Value;
                    configRowCounts[pair.Key] = rowCount;
                    committedTypes.Add(pair.Key);
                }

                foreach (Type rowType in committedTypes)
                {
                    FireLoadSuccessEvent(rowType, configRowCounts[rowType]);
                }
            }
            catch (Exception ex)
            {
                for (int i = 0; i < committedTypes.Count; i++)
                {
                    Type rowType = committedTypes[i];
                    if (oldTables.TryGetValue(rowType, out object oldTable))
                    {
                        configTables[rowType] = oldTable;
                        configRowCounts[rowType] = oldCounts[rowType];
                    }
                    else
                    {
                        configTables.Remove(rowType);
                        configRowCounts.Remove(rowType);
                    }
                }

                foreach (KeyValuePair<Type, object> pair in parsedTables)
                {
                    if (!oldTables.TryGetValue(pair.Key, out object oldTable)
                        || !ReferenceEquals(oldTable, pair.Value))
                    {
                        try { helper.ReleaseConfig(pair.Value); }
                        catch { }
                    }

                    FireLoadFailedEvent(pair.Key, ex.Message);
                }

                throw;
            }

            foreach (KeyValuePair<Type, object> pair in oldTables)
            {
                if (!ReferenceEquals(pair.Value, parsedTables[pair.Key]))
                {
                    try { helper.ReleaseConfig(pair.Value); }
                    catch { }
                }
            }
        }

        // ==================== 配置卸载 ====================

        /// <summary>
        /// 从 JSON 字符串加载配置表。
        /// 跟随 LoadConfig 的完整流程：映射→解析→缓存→分发事件。
        /// 重复加载相同类型会覆盖旧数据。
        /// </summary>
        /// <typeparam name="T">配置行类型。</typeparam>
        /// <param name="json">JSON 字符串。</param>
        public void LoadConfigFromString<T>(string json) where T : class
        {
            EnsureHelper();

            Type rowType = typeof(T);

            if (string.IsNullOrEmpty(json))
            {
                string errorMsg = $"ConfigModule: LoadConfigFromString<{rowType.Name}> failed: json is empty.";
                FireLoadFailedEvent(rowType, errorMsg);
                throw new RFrameworkException(errorMsg);
            }

            // 旧表引用与旧行数（重复加载时需在覆盖前捕获，失败回滚用）
            bool hasOld = configTables.TryGetValue(rowType, out object oldTable);
            int oldRowCount = hasOld ? configRowCounts[rowType] : 0;
            object newTable = null;

            try
            {
                Type tableType = helper.GetTableType(rowType);
                object parsedTable = helper.ParseConfigFromString(tableType, json);
                if (parsedTable == null)
                {
                    throw new RFrameworkException(
                        $"ConfigModule: helper returned null while parsing '{rowType.Name}' from string.");
                }

                newTable = parsedTable;
                configTables[rowType] = parsedTable;

                int rowCount = 0;
                try
                {
                    IReadOnlyList<T> allRows = helper.GetAllConfigs<T>(parsedTable);
                    rowCount = allRows != null ? allRows.Count : 0;
                }
                catch
                {
                }

                configRowCounts[rowType] = rowCount;
                FireLoadSuccessEvent(rowType, rowCount);
            }
            catch (Exception ex)
            {
                // 失败回滚：恢复旧表（无旧表则移除条目），保证旧表始终可访问且不被泄漏；
                // 同时释放已创建但加载失败的新表。
                if (hasOld)
                {
                    configTables[rowType] = oldTable;
                    configRowCounts[rowType] = oldRowCount;
                }
                else
                {
                    configTables.Remove(rowType);
                    configRowCounts.Remove(rowType);
                }

                if (newTable != null && newTable != oldTable)
                {
                    try { helper.ReleaseConfig(newTable); }
                    catch { /* best-effort */ }
                }

                FireLoadFailedEvent(rowType, ex.Message);
                throw;
            }

            // 新表已安全写入并广播，现在释放被覆盖的旧表（best-effort）
            if (hasOld && oldTable != newTable)
            {
                try { helper.ReleaseConfig(oldTable); }
                catch { /* best-effort */ }
            }
        }

        // ==================== 配置卸载 ====================

        /// <summary>
        /// 卸载指定类型的配置表。
        /// </summary>
        /// <typeparam name="T">配置行类型。</typeparam>
        public void UnloadConfig<T>() where T : class
        {
            Type rowType = typeof(T);

            if (configTables.TryGetValue(rowType, out object parsedTable))
            {
                Exception releaseError = null;
                try
                {
                    helper.ReleaseConfig(parsedTable);
                }
                catch (Exception ex)
                {
                    releaseError = ex;
                }
                finally
                {
                    configTables.Remove(rowType);
                    configRowCounts.Remove(rowType);
                }

                if (releaseError != null)
                {
                    throw new RFrameworkException(
                        $"ConfigModule: Failed to release config '{rowType.Name}'.", releaseError);
                }
            }
        }

        /// <summary>
        /// 卸载所有已加载的配置表。
        /// </summary>
        public void UnloadAllConfigs()
        {
            Exception firstError = null;
            foreach (KeyValuePair<Type, object> kv in configTables)
            {
                try
                {
                    helper.ReleaseConfig(kv.Value);
                }
                catch (Exception ex)
                {
                    if (firstError == null)
                    {
                        firstError = ex;
                    }
                }
            }

            configTables.Clear();
            configRowCounts.Clear();

            if (firstError != null)
            {
                throw new RFrameworkException(
                    "ConfigModule: One or more config tables failed to release.", firstError);
            }
        }

        // ==================== 配置查询 ====================

        /// <summary>
        /// 检查指定类型的配置表是否已加载。
        /// </summary>
        /// <typeparam name="T">配置行类型。</typeparam>
        /// <returns>已加载返回 true，否则返回 false。</returns>
        public bool HasConfig<T>() where T : class
        {
            return configTables.ContainsKey(typeof(T));
        }

        /// <summary>
        /// 从已加载的配置表中获取指定 ID 的配置行。
        /// </summary>
        /// <typeparam name="T">配置行类型。</typeparam>
        /// <param name="id">配置行 ID。</param>
        /// <returns>配置行实例，不存在时返回 null。</returns>
        public T GetConfig<T>(int id) where T : class
        {
            Type rowType = typeof(T);

            if (!configTables.TryGetValue(rowType, out object parsedTable))
            {
                throw new RFrameworkException(
                    $"ConfigModule: Config<{rowType.Name}> not loaded. Call LoadConfig first.");
            }

            EnsureHelper();
            return helper.GetConfig<T>(parsedTable, id);
        }

        /// <summary>
        /// 检查已加载的配置表中是否包含指定 ID 的配置行。
        /// </summary>
        /// <typeparam name="T">配置行类型。</typeparam>
        /// <param name="id">配置行 ID。</param>
        /// <returns>存在返回 true，否则返回 false。</returns>
        public bool HasConfigRow<T>(int id) where T : class
        {
            Type rowType = typeof(T);

            if (!configTables.TryGetValue(rowType, out object parsedTable))
            {
                return false;
            }

            EnsureHelper();
            return helper.ContainsConfig(parsedTable, id);
        }

        /// <summary>
        /// 获取已加载配置表中的所有配置行。
        /// </summary>
        /// <typeparam name="T">配置行类型。</typeparam>
        /// <returns>配置行只读列表。</returns>
        public IReadOnlyList<T> GetAllConfigs<T>() where T : class
        {
            Type rowType = typeof(T);

            if (!configTables.TryGetValue(rowType, out object parsedTable))
            {
                throw new RFrameworkException(
                    $"ConfigModule: Config<{rowType.Name}> not loaded. Call LoadConfig first.");
            }

            EnsureHelper();
            return helper.GetAllConfigs<T>(parsedTable);
        }

        /// <summary>
        /// 获取当前已加载的配置表数量。
        /// </summary>
        public int ConfigCount
        {
            get { return configTables.Count; }
        }

        // ==================== RFrameworkModule 生命周期 ====================

        /// <summary>
        /// 每帧更新。当前无每帧操作，预留。
        /// </summary>
        /// <param name="elapseSeconds">逻辑流逝时间，以秒为单位。</param>
        /// <param name="realElapseSeconds">真实流逝时间，以秒为单位。</param>
        internal override void Update(float elapseSeconds, float realElapseSeconds)
        {
            // 当前无需每帧操作
        }

        /// <summary>
        /// 关闭并清理配置模块。释放所有缓存的配置表。
        /// </summary>
        internal override void Shutdown()
        {
            try
            {
                UnloadAllConfigs();
            }
            catch
            {
                // 关闭阶段已逐表完成尽力清理，释放异常不能阻断框架关闭。
            }

            helper = null;
            eventModule = null;
        }

        // ==================== 私有方法 ====================

        /// <summary>
        /// 检查辅助器是否已设置，未设置时抛出异常。
        /// </summary>
        private void EnsureHelper()
        {
            if (helper == null)
            {
                throw new RFrameworkException(
                    "ConfigModule: Helper not set. Call SetHelper() before loading config.");
            }
        }

        /// <summary>
        /// 惰性获取事件模块引用。
        /// 通过 RFrameworkModuleEntry.GetModule 获取，若事件模块尚未就绪则返回 null。
        /// </summary>
        /// <returns>事件模块实例，未就绪时为 null。</returns>
        private IEventModule GetEventModule()
        {
            if (eventModule == null)
            {
                try
                {
                    eventModule = RFrameworkModuleEntry.GetModule<IEventModule>();
                }
                catch
                {
                    // 事件模块未就绪时静默处理
                    return null;
                }
            }

            return eventModule;
        }

        /// <summary>
        /// 分发配置加载成功事件。
        /// 事件模块未就绪时静默跳过。
        /// </summary>
        /// <param name="configType">配置行类型。</param>
        /// <param name="rowCount">加载的配置行数量。</param>
        private void FireLoadSuccessEvent(Type configType, int rowCount)
        {
            IEventModule evt = GetEventModule();
            if (evt == null)
            {
                return;
            }

            ConfigLoadSuccessEvent successEvent = new ConfigLoadSuccessEvent(configType, rowCount);
            evt.FireSafely(successEvent);
        }

        /// <summary>
        /// 分发配置加载失败事件。
        /// 事件模块未就绪时静默跳过。
        /// </summary>
        /// <param name="configType">配置行类型。</param>
        /// <param name="errorMessage">失败原因。</param>
        private void FireLoadFailedEvent(Type configType, string errorMessage)
        {
            IEventModule evt = GetEventModule();
            if (evt == null)
            {
                return;
            }

            ConfigLoadFailedEvent failedEvent = new ConfigLoadFailedEvent(configType, errorMessage);
            evt.FireSafely(failedEvent);
        }
    }
}
