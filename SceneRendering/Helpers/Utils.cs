using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace SceneRendering.Helpers;

public unsafe partial class Utils
{
    [GeneratedRegex("([A-Z])", RegexOptions.Compiled)]
    private static partial Regex CamelCase();

    public static string SplitCamelCase(string str)
    {
        return CamelCase().Replace(str, " $1").Trim();
    }

    /// <summary>
    /// 字符串转指针。
    /// </summary>
    /// <param name="str">str</param>
    /// <returns></returns>
    public static byte* StringToPointer(string str)
    {
        return (byte*)Marshal.StringToHGlobalAnsi(str);
    }

    /// <summary>
    /// 指针转字符串。
    /// </summary>
    /// <param name="ptr">ptr</param>
    /// <returns></returns>
    public static string PointerToString(byte* ptr)
    {
        return Marshal.PtrToStringAnsi((nint)ptr)!;
    }

    /// <summary>
    /// 字符串数组转指针数组。
    /// </summary>
    /// <param name="strs"></param>
    /// <returns></returns>
    public static byte** GetPointerArray(string[] strs)
    {
        byte** ptrs = (byte**)Marshal.AllocHGlobal(sizeof(byte*) * strs.Length);

        for (int i = 0; i < strs.Length; i++)
        {
            ptrs[i] = StringToPointer(strs[i]);
        }

        return ptrs;
    }

    /// <summary>
    /// 指针数组转字符串数组。
    /// </summary>
    /// <param name="ptrs"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static string[] GetStringArray(byte** ptrs, uint length)
    {
        string[] strs = new string[length];

        for (int i = 0; i < length; i++)
        {
            strs[i] = PointerToString(ptrs[i]);
        }

        return strs;
    }
}
