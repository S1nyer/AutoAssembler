using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;


namespace AutoAssembler
{
    public class MemoryAPI
    {
        public enum Assembler_Status
        {
            //XED汇编引擎状态
            Assembler_Error = 0, Assembler_OK = 1
        };
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate bool CBXEDPARSE_UNKNOWN([MarshalAs(UnmanagedType.LPWStr)]StringBuilder text, long value);
        [StructLayout(LayoutKind.Sequential)]
        public struct Assembler_Parameter
        {
            public bool x64; //逻辑值，0为假1为真
            public Int64 CurrentAddress;//汇编指令当前地址(用于运算jmp和call等)
            public Int16 AsmLength;//指令字节集长度
            public CBXEDPARSE_UNKNOWN cbUnkonwn;//无法识别的指令
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public byte[] Bytes;//汇编指令字节集
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string Asm;//汇编指令
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string error;//错误字符
        };
        public struct Label
        {
            public string LabelName;
            public long Address;
            public bool Define;
        }
        public struct Define
        {
            public string DefineName;
            public string Value;
        }
        public struct Reassemble
        {
            public long CurrentAddress;
            public int Reflect;//汇编代码数组索引
            public int Reflect2;//汇编字节集数组索引
        }
        public struct AllocedMemory
        {
            public string AllocName;
            public long Address;
            public int size;
            public bool Zero;
        }
        public struct RegisterSymbol
        {
            public string SymbolName;
            public long Address;
        }
        public struct MEMORY_BASIC_INFORMATION
        {
            public long BaseAddress;
            public long AllocationBase;
            public int AllocationProtect;
            public long RegionSize;
            public int State;
            public int Protect;
            public int lType;
        }
        const int PROCESS_POWER_MAX = 2035711;
        const int ASCII = 0;
        const int Unicode = 1;
        const int AllocationGranularity = 65536;
        const long x32MaxAddress = 0xfffffffff;
        const long x32MinAddress = 0x10000;
        const long x64MaxAddress = 0x00007FFFFFFEFFFF;
        const long x64MinAddress = 0x10000;
        const int PAGE_READONLY = 2;
        const int PAGE_READWRITE = 4;
        const int PAGE_WRITECOPY = 8;
        const int PAGE_EXECUTE = 16;
        const int PAGE_EXECUTE_READ = 32;
        const int PAGE_EXECUTE_READWRITE = 64;
        const int PAGE_EXECUTE_WRITECOPY = 128;
        const int PAGE_GUARD = 256;
        const int PAGE_NOACCESS = 1;
        const int PAGE_NOCACHE = 512;
        const int MEM_COMMIT = 4096;
        const int MEM_FREE = 65536;
        const int MEM_RESERVE = 8192;
        const int MEM_IMAGE = 16777216;
        const int MEM_MAPPED = 262144;
        const int MEM_PRIVATE = 131072;
        const int MEM_DECOMMIT = 16384;
        const int MEM_RELEASE = 32768;
        const int MEM_TOP_DOWN = 1048576;
        const int MEM_RESET = 0x80000;
        const int MEM_WRITE_WATCH = 0x200000;
        const int MEM_PHYSICAL = 0x400000;
        const int MEM_LARGE_PAGES = 0x20000000;
        [DllImport("XEDParse.dll", EntryPoint = "XEDParseAssemble")]
        private static extern Assembler_Status Assemble(ref Assembler_Parameter Parameter);
        [DllImport("kernel32.dll", EntryPoint = "CloseHandle")]
        private static extern bool CloseHandle(IntPtr Handle);
        [DllImport("kernel32.dll", EntryPoint = "WriteProcessMemory")]
        private static extern bool WriteMemoryByteSet(IntPtr hProcess, long lpBaseAddress, byte[] lpBuffer, long nSize, int lpNumberOfBytesWritten);
        [DllImport("kernel32.dll", EntryPoint = "CreateRemoteThread")]
        private static extern IntPtr CreateRemoteThread(IntPtr Handle, int ThreadAttributes, int Stacksize, long Address, int par, int flags, int filewriten);//ThreadAttributes =0;Stacksize=0;
        [DllImport("kernel32.dll", EntryPoint = "ReadProcessMemory")]
        private static extern bool ReadMemoryByteSet(IntPtr hProcess, long lpBaseAddress, byte[] lpBuffer, long nSize, int lpNumberOfBytesRead);
        [DllImport("kernel32.dll", EntryPoint = "OpenProcess")]
        private static extern IntPtr OpenProcess(int dwDesiredAccess, int bInheritHandle, int dwProcessId);
        [DllImport("kernel32.dll", EntryPoint = "VirtualFreeEx")]
        private static extern bool VirtualFreeEx(IntPtr Handle, long Address, int size, int FreeType);
        [DllImport("kernel32.dll", EntryPoint = "VirtualQueryEx")]
        private static extern int VirtualQueryEx(IntPtr handle, long QueryAddress, out MEMORY_BASIC_INFORMATION lpBuffer, int dwLength);
        [DllImport("kernel32.dll", EntryPoint = "VirtualAllocEx")]
        private static extern long VirtualAllocEx(IntPtr process, long pAddress, int size, int AllocType, int protect);
        public Assembler_Parameter Assemble(string Asm,long CurrentAddress,bool x64)
        {
            Assembler_Parameter parameter = new Assembler_Parameter();
            parameter.x64 = x64;
            parameter.Asm = Asm;
            parameter.CurrentAddress = CurrentAddress;
            Assemble(ref parameter);
            return parameter;
        }
        public enum OperationType
        {
            Add = 0,
            Sub = 1,
            Mul = 2
        };
        public struct Number
        {
            public string Value;
            public OperationType Type;
        }
        public Number[] OperationParse(string Exp)
        {
            Number number;
            int[] pos = new int[3];
            bool flag;
            int current = 0, lenth, min;
            pos[0] = Exp.IndexOf('+');
            pos[1] = Exp.IndexOf('-');
            pos[2] = Exp.IndexOf('*');
            flag = pos[0] != -1 || pos[1] != -1 || pos[2] != -1;
            if (!flag)
            {
                number.Value = Exp.Trim();
                number.Type = OperationType.Add;
                Number[] temp = { number };
                return temp;
            }
            List<Number> numbers = new List<Number>();
            min = MinIndex(pos);
            if (pos[min] == 0) goto label;
            number.Value = Exp.Substring(0, pos[min]).Trim();
            number.Type = OperationType.Add;
            numbers.Add(number);
            current = pos[min] + 1;
        label:
            do
            {
                pos[0] = Exp.IndexOf('+', current);
                pos[1] = Exp.IndexOf('-', current);
                pos[2] = Exp.IndexOf('*', current);
                flag = pos[0] != -1 || pos[1] != -1 || pos[2] != -1;
                if (!flag) goto end;
                number.Type = (OperationType)min;
                min = MinIndex(pos);
                lenth = pos[min] - current;
                number.Value = Exp.Substring(current, lenth).Trim();
                numbers.Add(number);
                current = pos[min] + 1;
            } while (flag);
        end:
            if (current != Exp.Length)
            {
                lenth = Exp.Length - current;
                number.Type = (OperationType)min;
                number.Value = Exp.Substring(current, lenth).Trim();
                numbers.Add(number);
                return numbers.ToArray();
            }
            return numbers.ToArray();
        }
        private int MinIndex(int[] array)
        {
            int index = 0;
            for (int i = 0; i < array.Length; ++i)
            {
                if (array[index] == -1 && array[i] > array[index])
                {
                    index = i;
                    continue;
                }
                if (array[i] > 0 && array[index] > array[i])
                {
                    index = i;
                    continue;
                }
            }
            return index;
        }
        public IntPtr CreateThread(long address)
        {
            int dw = 0;
            return CreateRemoteThread(Var.ProcessHandle, 0, 0, address, 0, 0, dw);
        }
        public IntPtr getHandleByProcessName(string processName)
        {
            int pid;
            Process[] ArrayProcess = Process.GetProcessesByName(processName);
            foreach (Process pro in ArrayProcess)
            {
                pid = pro.Id;
                return OpenProcess(PROCESS_POWER_MAX, 0, pid);
            }
            return (IntPtr)0;
        }
        public long AllocMemory(long address,int size)
        {
            long value = VirtualAllocEx(Var.ProcessHandle, address, size, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);
            if (value != 0)
            {
                return value;
            }
            return value;
        }
        public bool FreeAllocMemory(string AllocName)
        {
            for(int i = 0;i < Var.AllocedMemorys.Count; ++i)
            {
                if(AllocName == Var.AllocedMemorys[i].AllocName)
                {
                    bool ok = VirtualFreeEx(Var.ProcessHandle, Var.AllocedMemorys[i].Address, Var.AllocedMemorys[i].size, MEM_DECOMMIT);
                    Var.AllocedMemorys.RemoveAt(i);
                    return ok;
                }
            }
            return false;
        }
        public bool GetProcessModuleInfo(string processName)
        {
            foreach(Process p in Process.GetProcessesByName(processName))
            {
                Var.ProcessModuleInfo = p.Modules;
                return true;
            }
            return false;
        }
        public long GetModuleBaseaddress(string DLLname)
        {
            DLLname = DLLname.ToUpper();
            DLLname = DLLname.Replace("\"", "");
            foreach (ProcessModule m in Var.ProcessModuleInfo)
            {
                if (m.ModuleName.ToUpper() == DLLname)
                    return (long)m.BaseAddress;
            }
            return 0;
        }
        public long GetModuleSize(string DLLname)
        {
            DLLname = DLLname.ToUpper();
            DLLname = DLLname.Replace("\"", "");
            foreach (ProcessModule m in Var.ProcessModuleInfo)
            {
                if (m.ModuleName.ToUpper().Equals(DLLname))
                    return (long)m.ModuleMemorySize;
            }
            return 0;
        }
        public long FindNearFreeBlock(long Address, int size)
        {
            MEMORY_BASIC_INFORMATION mbi = new MEMORY_BASIC_INFORMATION();
            if (Address == 0)
            {
                return VirtualAllocEx(Var.ProcessHandle, 0, size, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);
            }
            long minAddress = Address - 0x70000000;
            long maxAddress = Address + 0x70000000;
            if (Var.is64bit == true)
            {
                if (minAddress > x64MaxAddress || minAddress < x64MinAddress)
                    minAddress = x64MinAddress;
                if (maxAddress > x64MaxAddress || maxAddress < x64MinAddress)
                    maxAddress = x64MaxAddress;
            }
            else
            {
                minAddress = x32MinAddress;
                maxAddress = x32MaxAddress;
            }
            long x;
            long oldb;
            long b = minAddress;
            long result = 0;
            int v;
            long offset;
            while (true)
            {
                v = VirtualQueryEx(Var.ProcessHandle, b, out mbi, Marshal.SizeOf(mbi));
                if (v != Marshal.SizeOf(mbi))
                    return 0;
                if (mbi.BaseAddress > maxAddress)
                    return 0;
                if (mbi.State == MEM_FREE && mbi.RegionSize > size)
                {
                    if ((mbi.BaseAddress % AllocationGranularity) > 0)
                    {
                        x = mbi.BaseAddress;
                        offset = AllocationGranularity - (x % AllocationGranularity);
                        if ((mbi.RegionSize - offset) >= size)
                        {
                            x = x + offset;
                            if (x < Address)
                            {
                                x = x + (mbi.RegionSize - offset) - size;
                                if (x > Address)
                                    x = Address;
                                x = x - (x % AllocationGranularity);
                            }
                            if (Math.Abs((x - Address)) < Math.Abs(result - Address))
                                result = x;
                        }
                    }
                    else
                    {
                        x = mbi.BaseAddress;
                        if (x < Address)
                        {
                            x = (x + mbi.RegionSize) - size;
                            if (x > Address)
                                x = Address;
                            x = x - (x % AllocationGranularity);
                        }
                        if (Math.Abs((x - Address)) < Math.Abs(result - Address))
                            result = x;
                    }
                }
                if ((mbi.RegionSize % AllocationGranularity) > 0)
                    mbi.RegionSize = mbi.RegionSize + (AllocationGranularity - (mbi.RegionSize % AllocationGranularity));
                oldb = b;
                b = mbi.BaseAddress + mbi.RegionSize;
                if (b > maxAddress)
                    break;
                if (oldb > b)
                    return 0;
            }
            return result;
        }
        public long AobScanModule(string Module,string MarkCode)
        {
            if (String.IsNullOrEmpty(MarkCode))
                return 0;
            if (MarkCode.Replace(" ", "").Length % 2 != 0)
                return 0;
            //定义Sunday匹配算法所需的变量
            short[] CodeArray = HexStringToIntArray(MarkCode);
            int i, j, k;
            i = j = 0;
            int BufferLen, CodeLen;
            int Offest;
            long index = 0;
            CodeLen = CodeArray.Length;
            //定义模块信息及有关变量
            long BeginAddress, EndAddress,CurrentAddress;
            byte[] Buffer;
            BeginAddress = GetAddress(Module);
            if (BeginAddress == 0)
                return 0;
            EndAddress = GetModuleBaseaddress(Module) + GetModuleSize(Module);
            CurrentAddress = BeginAddress;
            MEMORY_BASIC_INFORMATION mbi = new MEMORY_BASIC_INFORMATION();
            while(CurrentAddress < EndAddress)
            {
                if (VirtualQueryEx(Var.ProcessHandle, CurrentAddress, out mbi, Marshal.SizeOf(mbi)) == 0)
                    return 0;
                Buffer = new byte[mbi.RegionSize];
                if (!ReadMemoryByteSet(Var.ProcessHandle, CurrentAddress, Buffer, mbi.RegionSize, 0))
                    return 0;
                BufferLen = Buffer.Length;
                while(i < BufferLen && j < CodeLen)
                {
                    if(Buffer[i] == CodeArray[j] || CodeArray[j] == 256)
                    {
                        ++i;
                        ++j;
                        if (j == CodeLen)
                            return CurrentAddress + (index + i - CodeLen);
                        continue;
                    }
                    Offest = i + CodeLen;
                    if (Offest >= mbi.RegionSize)
                    {
                        CurrentAddress += mbi.RegionSize;
                        j = 0;
                        break;
                    }
                    for (k = CodeLen - 1; k >= 0 && Buffer[Offest] != CodeArray[k]; --k) ;
                    i = i + (CodeLen - k);
                    j = 0;
                }
                i = 0;
            }
            return 0;
        }
        public byte[] HexToBytes(string HexString)
        {
            HexString = HexString.Replace(" ", "");
            if (HexString.Length % 2 != 0)
                return null;
            int times = HexString.Length / 2;
            byte[] CopyBytes = new byte[times];
            string[] HexArray = new string[times];
            int cur = 0;
            for (int i = 0; i < times; i++)
            {
                HexArray[i] = HexString.Substring(cur, 2);
                CopyBytes[i] = (byte)Convert.ToInt16(HexArray[i], 16);
                cur += 2;
            }
            return CopyBytes;
        }
        public void ByteCopy(byte[] src, byte[] dest, int Lenth)
        {
            for (int i = 0; i < Lenth; ++i)
            {
                dest[i] = src[i];
            }
        }
        private short[] HexStringToIntArray(string HexString)
        {
            HexString = HexString.Replace(" ", "");
            if (HexString.Length % 2 != 0)
                return null;
            int times = HexString.Length / 2;
            short[] CopyBytes = new short[times];
            string[] HexArray = new string[times];
            int cur = 0;
            for (int i = 0; i < times; i++)
            {
                HexArray[i] = HexString.Substring(cur, 2);
                if (HexArray[i] == "??" || HexArray[i] == "**")
                {
                    CopyBytes[i] = 256;
                    cur += 2;
                    continue;
                }
                CopyBytes[i] = Convert.ToByte(HexArray[i], 16);
                cur += 2;
            }
            return CopyBytes;
        }
        public long GetAddress(string Expression)
        {
            Expression = Expression.Replace("[", "");
            char[] a = { ']' };
            string[] SplitExps = Expression.Split(a, StringSplitOptions.RemoveEmptyEntries);
            long Address = 0;
            long temp = 0;
            for (int i = 0; i < SplitExps.Length; i++)
            {
                Number[] numbers = OperationParse(SplitExps[i]);
                int x = 0;
                while (x < numbers.Length)
                {
                    temp = 0;
                    //寻找全局符号数组
                    foreach (RegisterSymbol symbol in Var.RegisteredSymbols)
                    {
                        if (symbol.SymbolName == numbers[x].Value)
                        {
                            temp = symbol.Address;
                            break;
                        }
                    }
                    if (temp == 0)
                    {
                        //未找到到指定符号,判断其是否为模块
                        temp = GetModuleBaseaddress(numbers[x].Value);
                        if (temp == 0)
                        {
                            //未找到模块,判断是否为静态地址
                            try
                            {
                                temp = Convert.ToInt64(numbers[x].Value, 16);
                            }
                            catch (FormatException)
                            {
                                //非全局符号,模块及静态地址
                                return 0;
                            }
                        }
                    }
                    switch (numbers[x].Type)
                    {
                        case MemoryAPI.OperationType.Add:
                            Address += temp;
                            break;
                        case MemoryAPI.OperationType.Sub:
                            Address -= temp;
                            break;
                        case MemoryAPI.OperationType.Mul:
                            Address *= temp;
                            break;
                    }
                    ++x;
                }
                if (SplitExps.Length == 1)
                {
                    return Address;
                }
                if (Var.is64bit)
                {
                    if (!ReadMemoryInt64(Address, ref Address))
                    {
                        Var.ErrorState = "ReadMemoryError";
                        return 0;
                    }
                    continue;
                }
                else
                {
                    int tAddress = (int)Address;
                    if (!ReadMemoryInt32(Address, ref tAddress))
                    {
                        Var.ErrorState = "ReadMemoryError";
                        return 0;
                    }
                    Address = tAddress;
                }
            }
            return Address;
        }
        public byte[] ReadMemoryByteSet(long address,int size)
        {
            byte[] Buffer = new byte[size];
            if (ReadMemoryByteSet(Var.ProcessHandle, address, Buffer, size, 0))
                return Buffer;
            return null;
        }
        public bool ReadMemoryByte(long address,ref byte buffer)
        {
            byte[] Bytes = new byte[1];
            if (ReadMemoryByteSet(Var.ProcessHandle, address, Bytes, 1, 0))
            {
                buffer = Bytes[0];
                return true;
            }
            return false;
        }
        public bool ReadMemoryInt16(long address, ref short buffer)
        {
            byte[] bytes = new byte[4];
            if (ReadMemoryByteSet(Var.ProcessHandle, address, bytes, 2, 0))
            {
                buffer = BitConverter.ToInt16(bytes, 0);
                return true;
            }
            return false;
        }
        public bool ReadMemoryInt32(long address,ref int buffer)
        {
            byte[] bytes = new byte[4];
            if (ReadMemoryByteSet(Var.ProcessHandle, address, bytes, 4, 0))
            {
                buffer = BitConverter.ToInt32(bytes, 0);
                return true;
            }
            return false;
        }
        public bool ReadMemoryInt64(long address,ref long buffer)
        {
            byte[] bytes = new byte[8];
            if (ReadMemoryByteSet(Var.ProcessHandle, address, bytes, 8, 0))
            {
                buffer = BitConverter.ToInt64(bytes, 0);
                return true;
            }
            return false;
        }
        public bool ReadMemoryFloat(long address,ref float buffer)
        {
            byte[] bytes = new byte[4];
            if (ReadMemoryByteSet(Var.ProcessHandle, address, bytes, 4, 0))
            {
                buffer = BitConverter.ToSingle(bytes, 0);
                return true;
            }
            return false;
        }
        public bool ReadMemoryDouble(long address,ref double buffer)
        {
            byte[] bytes = new byte[8];
            if (ReadMemoryByteSet(Var.ProcessHandle, address, bytes, 8, 0))
            {
                buffer = BitConverter.ToDouble(bytes, 0);
                return true;
            }
            return false;
        }
        /// <summary> 
        ///  向给出的地址读取文本,字节长度若设定为0,则自动读取到字符串结尾(自动读取最多读取1024个字节)
        /// </summary> 
        /// <param name="encoding">读取文本的编码格式</param>
        public string ReadMemoryString(long address,int Byteslength,Encoding encoding)
        {
            byte[] bytes;
            if(Byteslength == 0)
            {
                bytes = new byte[1024];
                if (!ReadMemoryByteSet(Var.ProcessHandle, address, bytes, 1024, 0))
                {
                    return "";
                }
                for (int i = 0; i < 1024; ++i)
                {
                    if (bytes[i] == 0 && bytes[i + 1] == 0)
                    {
                        byte[] Copy = new byte[i + 1];
                        ByteCopy(bytes, Copy, i + 1);
                        return encoding.GetString(Copy);
                    }
                }
            }
            bytes = new byte[Byteslength];
            if (!ReadMemoryByteSet(Var.ProcessHandle, address, bytes, Byteslength, 0))
            {
                return "";
            }
            return encoding.GetString(bytes);
        }
        public bool WriteMemoryByteSet(long address,byte[] Bytes)
        {
            return WriteMemoryByteSet(Var.ProcessHandle, address, Bytes, Bytes.Length, 0);
        }
        public bool WriteMemoryByte(long address,byte Byte)
        {
            byte[] b = { Byte };
            return WriteMemoryByteSet(Var.ProcessHandle, address,b, 1, 0);
        }
        public bool WriteMemoryInt16(long address,short value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            return WriteMemoryByteSet(Var.ProcessHandle, address, bytes, 2, 0);
        }
        public bool WriteMemoryInt32(long address,int value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            return WriteMemoryByteSet(Var.ProcessHandle, address, bytes, 4, 0);

        }
        public bool WriteMemoryInt64(long address,long value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            return WriteMemoryByteSet(Var.ProcessHandle, address, bytes, 8, 0);
        }
        public bool WriteMemoryFloat(long address,float value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            return WriteMemoryByteSet(Var.ProcessHandle, address, bytes, 4, 0);
        }
        public bool WriteMemoryDouble(long address,double value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            return WriteMemoryByteSet(Var.ProcessHandle, address, bytes, 8, 0);
        }
        /// <summary> 
        ///  向给出的地址写入文本，会自动添加字串结尾
        /// </summary> 
        /// <param name="encoding">写入字符的编码格式</param>
        public bool WriteMemoryString(long address,string text, Encoding encoding)
        {
            byte[] Copy = encoding.GetBytes(text);
            int size = Copy.Length + 2;
            byte[] Bytes = new byte[size];
            Copy.CopyTo(Bytes, 0);
            if (!WriteMemoryByteSet(Var.ProcessHandle, address, Bytes, Bytes.Length, 0))
            {
                return false;
            }
            return true;
        }
    }
    public static class Var
    {
        public static bool is64bit;
        public static IntPtr ProcessHandle;
        public static ProcessModuleCollection ProcessModuleInfo;
        public static List<MemoryAPI.RegisterSymbol> RegisteredSymbols;
        public static List<MemoryAPI.AllocedMemory> AllocedMemorys;
        public static string AutoAssemble_Error;
        public static string ErrorState;
        public const byte True = 1;
        public const byte False = 0;
    }
}
