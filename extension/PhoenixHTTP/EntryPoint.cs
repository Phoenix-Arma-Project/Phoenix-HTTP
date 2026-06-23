using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace PhoenixHttp;

/// <summary>
/// Native entry surface that the Arma 3 engine binds to when it loads the extension.
/// Every exported method follows the engine's <c>callExtension</c> ABI: the engine owns the
/// output buffer and the argument pointers, so this class is purely about marshalling raw
/// pointers to and from managed strings and never contains business logic of its own.
/// </summary>
public static unsafe class EntryPoint
{
    /// <summary>Resolve the module handle from an address that lives inside this module.</summary>
    private const uint GetModuleHandleFromAddress = 0x00000004;

    /// <summary>Resolve the handle without touching the module's reference count.</summary>
    private const uint GetModuleHandleUnchangedReferenceCount = 0x00000002;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetModuleHandleEx(uint flags, IntPtr moduleNameOrAddress, out IntPtr module);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetModuleFileName(IntPtr module, char* fileName, uint size);

    /// <summary>
    /// Finds the absolute path of this DLL by asking Windows for the module that owns one of our
    /// own functions. Arma never tells the extension where it lives, so this is the only reliable
    /// way to locate <c>config.json</c> and the log directory next to the binary.
    /// </summary>
    /// <returns>The full path of the loaded DLL, or an empty string if it could not be resolved.</returns>
    private static string ResolveDllPath()
    {
        IntPtr addressInsideThisModule = (IntPtr)(delegate* unmanaged[Stdcall]<byte*, int, void>)(&RVExtensionVersion);

        if (!GetModuleHandleEx(
                GetModuleHandleFromAddress | GetModuleHandleUnchangedReferenceCount,
                addressInsideThisModule,
                out IntPtr module))
        {
            return string.Empty;
        }

        // GetModuleFileNameW writes UTF-16 and measures its buffer in characters, not bytes, so the
        // buffer is a char[] and the returned length is already a character count we can hand to string.
        char* buffer = stackalloc char[260];
        uint length = GetModuleFileName(module, buffer, 260);
        return new string(buffer, 0, (int)length);
    }

    /// <summary>
    /// Engine hook called once when the extension is loaded. We use it to perform one-time
    /// initialization and to report the version string back to the engine.
    /// </summary>
    /// <param name="output">Engine-owned buffer that receives the version string.</param>
    /// <param name="outputSize">Capacity of <paramref name="output"/> in bytes, including the terminator.</param>
    [UnmanagedCallersOnly(EntryPoint = "RVExtensionVersion", CallConvs = new[] { typeof(CallConvStdcall) })]
    public static void RVExtensionVersion(byte* output, int outputSize)
    {
        try
        {
            Extension.Initialize(ResolveDllPath());
            WriteOutput(output, outputSize, Extension.Version);
        }
        catch
        {
            WriteOutput(output, outputSize, string.Empty);
        }
    }

    /// <summary>
    /// Engine hook for <c>"extension" callExtension "command"</c>, the single-argument form.
    /// </summary>
    /// <param name="output">Engine-owned buffer that receives the reply.</param>
    /// <param name="outputSize">Capacity of <paramref name="output"/> in bytes, including the terminator.</param>
    /// <param name="function">Null-terminated command string supplied by the script.</param>
    [UnmanagedCallersOnly(EntryPoint = "RVExtension", CallConvs = new[] { typeof(CallConvStdcall) })]
    public static void RVExtension(byte* output, int outputSize, sbyte* function)
    {
        try
        {
            string command = ReadString(function);
            WriteOutput(output, outputSize, Extension.Call(command));
        }
        catch (Exception exception)
        {
            WriteOutput(output, outputSize, Extension.DescribeFailure(exception));
        }
    }

    /// <summary>
    /// Engine hook for <c>"extension" callExtension ["command", [args]]</c>, the array form that
    /// the SQF wrapper uses for every request verb.
    /// </summary>
    /// <param name="output">Engine-owned buffer that receives the reply.</param>
    /// <param name="outputSize">Capacity of <paramref name="output"/> in bytes, including the terminator.</param>
    /// <param name="function">Null-terminated command string supplied by the script.</param>
    /// <param name="arguments">Array of null-terminated, engine-quoted argument strings.</param>
    /// <param name="argumentCount">Number of entries in <paramref name="arguments"/>.</param>
    /// <returns>Always zero; the meaningful result is written to <paramref name="output"/>.</returns>
    [UnmanagedCallersOnly(EntryPoint = "RVExtensionArgs", CallConvs = new[] { typeof(CallConvStdcall) })]
    public static int RVExtensionArgs(byte* output, int outputSize, sbyte* function, sbyte** arguments, int argumentCount)
    {
        try
        {
            string command = ReadString(function);
            string[] parsedArguments = ReadArguments(arguments, argumentCount);
            WriteOutput(output, outputSize, Extension.Call(command, parsedArguments));
        }
        catch (Exception exception)
        {
            WriteOutput(output, outputSize, Extension.DescribeFailure(exception));
        }

        return 0;
    }

    /// <summary>
    /// Engine hook that hands us the callback pointer used to push asynchronous results back into
    /// the game through the <c>ExtensionCallback</c> mission event handler.
    /// </summary>
    /// <param name="callback">Function pointer owned by the engine for the lifetime of the mission.</param>
    [UnmanagedCallersOnly(EntryPoint = "RVExtensionRegisterCallback", CallConvs = new[] { typeof(CallConvStdcall) })]
    public static void RVExtensionRegisterCallback(IntPtr callback)
    {
        try
        {
            Extension.RegisterCallback(callback);
        }
        catch
        {
        }
    }

    /// <summary>Reads a null-terminated UTF-8 string the engine passed by pointer.</summary>
    /// <param name="value">Pointer to the first byte, or <see langword="null"/>.</param>
    /// <returns>The decoded string, or an empty string when the pointer is null.</returns>
    private static string ReadString(sbyte* value) =>
        value is null ? string.Empty : new string(value, 0, ByteLength(value), Encoding.UTF8);

    /// <summary>Decodes and unquotes every argument the engine passed in the array form.</summary>
    /// <param name="arguments">Array of argument pointers, or <see langword="null"/>.</param>
    /// <param name="count">Number of arguments to read.</param>
    /// <returns>The decoded, unescaped arguments.</returns>
    private static string[] ReadArguments(sbyte** arguments, int count)
    {
        if (arguments is null || count <= 0)
        {
            return [];
        }

        string[] result = new string[count];
        for (int index = 0; index < count; index++)
        {
            result[index] = Unescape(ReadString(arguments[index]));
        }

        return result;
    }

    /// <summary>
    /// Strips the surrounding quotes and collapses doubled quotes that the engine adds around
    /// every string argument, turning <c>"he said ""hi"""</c> back into <c>he said "hi"</c>.
    /// </summary>
    /// <param name="value">The raw, engine-quoted argument.</param>
    /// <returns>The argument as the script author wrote it.</returns>
    private static string Unescape(string value)
    {
        if (value.Length < 2 || value[0] != '"' || value[^1] != '"')
        {
            return value;
        }

        return value[1..^1].Replace("\"\"", "\"");
    }

    /// <summary>Counts the bytes up to the null terminator of a C string.</summary>
    /// <param name="value">Pointer to the first byte of a null-terminated string.</param>
    /// <returns>The length in bytes, excluding the terminator.</returns>
    private static int ByteLength(sbyte* value)
    {
        int length = 0;
        while (value[length] != 0)
        {
            length++;
        }

        return length;
    }

    /// <summary>
    /// Writes a managed string into the engine-owned buffer as null-terminated UTF-8, truncating
    /// to fit. When truncation would land in the middle of a multi-byte sequence the cut is moved
    /// back to the previous character boundary so the engine never sees a malformed code point.
    /// </summary>
    /// <param name="output">Destination buffer owned by the engine.</param>
    /// <param name="outputSize">Capacity of the buffer in bytes, including the terminator.</param>
    /// <param name="value">The string to write.</param>
    private static void WriteOutput(byte* output, int outputSize, string value)
    {
        if (output is null || outputSize <= 0)
        {
            return;
        }

        byte* destination = output;
        int capacity = outputSize - 1;
        byte[] encoded = Encoding.UTF8.GetBytes(value);
        int length = Math.Min(encoded.Length, capacity);

        // Never split a multi-byte UTF-8 sequence: if the first dropped byte is a continuation
        // byte (0b10xxxxxx) the boundary is inside a code point, so back off to its start.
        while (length < encoded.Length && length > 0 && (encoded[length] & 0xC0) == 0x80)
        {
            length--;
        }

        for (int index = 0; index < length; index++)
        {
            destination[index] = encoded[index];
        }

        destination[length] = 0;
    }
}
