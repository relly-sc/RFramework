using System;


/// <summary>
/// 提供不依赖 Unity 的字符串扩展方法。
/// </summary>
public static class StringExtension
{
    private static readonly char[] LineEndings = { '\r', '\n' };

    /// <summary>
    /// 移除路径末尾的文件扩展名。
    /// </summary>
    /// <param name="path">待处理的路径。</param>
    /// <returns>不含扩展名的路径；输入为 null 时返回 null。</returns>
    public static string StripExtension(this string path)
    {
        if (path == null)
        {
            return null;
        }

        string extension = System.IO.Path.GetExtension(path);
        return string.IsNullOrEmpty(extension)
            ? path
            : path.Substring(0, path.Length - extension.Length);
    }

    /// <summary>
    /// 从当前位置读取一行，并将位置推进到下一行开头。
    /// </summary>
    /// <param name="source">待读取的字符串。</param>
    /// <param name="position">读取起点；返回时指向下一行开头。</param>
    /// <returns>读取到的行；到达末尾或位置无效时返回 null。</returns>
    /// <remarks>CRLF 作为一个换行符处理，空行返回空字符串。</remarks>
    public static string ReadLine(this string source, ref int position)
    {
        if (source == null)
        {
            throw new Exception("Source string is invalid.");
        }

        if (position < 0 || position >= source.Length)
        {
            return null;
        }

        int lineEnd = source.IndexOfAny(LineEndings, position);
        if (lineEnd < 0)
        {
            string finalLine = source.Substring(position);
            position = source.Length;
            return finalLine;
        }

        string line = source.Substring(position, lineEnd - position);
        position = lineEnd + 1;
        if (source[lineEnd] == '\r'
            && position < source.Length
            && source[position] == '\n')
        {
            position++;
        }

        return line;
    }
}
