using System;
using System.Collections.Generic;
using System.IO;

namespace RFramework
{
    /// <summary>
    /// 框架通用工具入口。
    /// </summary>
    public static partial class Utility
    {
        /// <summary>
        /// 提供跨平台路径与目录处理功能。
        /// </summary>
        public static class Path
        {
            /// <summary>
            /// 将路径分隔符统一为正斜杠。
            /// </summary>
            /// <param name="path">待规范化的路径。</param>
            /// <returns>规范化后的路径；输入为 null 时返回 null。</returns>
            public static string GetRegularPath(string path)
            {
                return path?.Replace('\\', '/');
            }

            /// <summary>
            /// 将本地路径转换为标准 file URI；已有 URI 保持其协议。
            /// </summary>
            /// <param name="path">本地路径或已有 URI。</param>
            /// <returns>可用于远程加载接口的 URI；输入为 null 时返回 null。</returns>
            public static string GetRemotePath(string path)
            {
                if (path == null)
                {
                    return null;
                }

                string regularPath = GetRegularPath(path);
                if (regularPath.IndexOf("://", StringComparison.Ordinal) >= 0)
                {
                    return Uri.TryCreate(regularPath, UriKind.Absolute, out Uri remoteUri)
                        ? remoteUri.AbsoluteUri
                        : regularPath;
                }

                try
                {
                    return new Uri(System.IO.Path.GetFullPath(path)).AbsoluteUri;
                }
                catch (Exception exception)
                {
                    throw new RFrameworkException(
                        $"Path '{path}' can not be converted to a file URI.", exception);
                }
            }

            /// <summary>
            /// 递归删除指定目录下的空目录；遇到文件、链接或访问失败时保留目录。
            /// </summary>
            /// <param name="directoryName">待检查的根目录。</param>
            /// <returns>根目录是否已被删除。</returns>
            public static bool RemoveEmptyDirectory(string directoryName)
            {
                if (string.IsNullOrWhiteSpace(directoryName))
                {
                    throw new RFrameworkException("Directory name is invalid.");
                }

                return RemoveEmptyDirectoryCore(directoryName);
            }

            private static bool RemoveEmptyDirectoryCore(string directoryName)
            {
                try
                {
                    if (!Directory.Exists(directoryName))
                    {
                        return false;
                    }

                    FileAttributes attributes = File.GetAttributes(directoryName);
                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        return false;
                    }

                    foreach (string childDirectory in Directory.EnumerateDirectories(directoryName))
                    {
                        RemoveEmptyDirectoryCore(childDirectory);
                    }

                    using (IEnumerator<string> entries =
                           Directory.EnumerateFileSystemEntries(directoryName).GetEnumerator())
                    {
                        if (entries.MoveNext())
                        {
                            return false;
                        }
                    }

                    Directory.Delete(directoryName, false);
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }
    }
}
