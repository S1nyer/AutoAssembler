using System.Text.RegularExpressions;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace AutoAssembler
{
    public class AutoAssembler
    {
        public AutoAssembler([In]MemoryAPI API)
        {
            Memory = API;
            RegisteredSymbols = new List<RegisterSymbol>();
            AllocedMemorys = new List<AllocedMemory>();
            OK = true;
        }
        public AutoAssembler(string ProcessName)
        {
            OK = true;
            Memory = new MemoryAPI(ProcessName);
            RegisteredSymbols = new List<RegisterSymbol>();
            AllocedMemorys = new List<AllocedMemory>();
            if (!Memory.ok) OK = false;
            ErrorState = "OpenProcessFailed";
            AutoAssemble_Error = "Open process " + ProcessName + " failed!";
        }
        public List<RegisterSymbol> RegisteredSymbols;
        public List<AllocedMemory> AllocedMemorys;
        public string AutoAssemble_Error;
        public string ErrorState;
        public bool OK;
        private readonly MemoryAPI Memory;
        public struct Assembled
        {
            public byte[] AssembledBytes;
            public long CurrentAddress;
        }
        public struct AobScan_args
        {
            public string DefineName;
            public string Module;
            public string AobString;
        }
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
        public string GetErrorInfo()
        {
            return AutoAssemble_Error;
        }
        public string GetErrorState()
        {
            return ErrorState;
        }
        public bool AutoAssemble(string Code,bool Enable)
        {
            string[] codes = GetExecuteMode(Code, Enable);
            return AutoAssemble(codes);
        }
        public bool AutoAssemble(string[] Codes)
        {
            //初始化标签数组(Labels),定义数组(defines),汇编字节集(assembleds),释放内存数组(Deallocs),重汇编指令数组(reassembles),创建线程数组(Threads)及标签(Label),定义(define),分配的内存(alloced),XEDParse汇编指令解析器参数(Parameters)结构体和相关变量
            List<string> AssembleCode = new List<string>();
            List<string> Threads = new List<string>();
            List<string> Deallocs = new List<string>();
            AutoAssemble_Error = "";
            Reassemble reassemble;
            List<Reassemble> reassembles = new List<Reassemble>();
            MemoryAPI.Assembler_Parameter Parameters = new MemoryAPI.Assembler_Parameter();
            List<RegisterSymbol> Symbols = new List<RegisterSymbol>();
            AllocedMemory alloc = new AllocedMemory();
            List<AllocedMemory> allocs = new List<AllocedMemory>();
            Define define;
            List<Define> defines = new List<Define>();
            Label label;
            List<Label> labels = new List<Label>();
            Assembled assembled = new Assembled();
            Address address = new Address();
            List<Assembled> assembleds = new List<Assembled>();
            List<string> registers = new List<string>();
            string Currentline,Currentline2="", s = "";
            int TotalLine,i, j, x;
            Regex regex;
            int seek, size;//用于截取字符
            long CurrentAddress = 0;
            bool mustbefar;
            TotalLine = Codes.Length;
            string InstrPrefix; //命令前缀,减少ToUpper函数的使用来提升效率
            //将全局符号分配给脚本内部标签
            for (i = 0;i < RegisteredSymbols.Count; ++i)
            {
                label.LabelName = RegisteredSymbols[i].SymbolName;
                label.Address = RegisteredSymbols[i].Address;
                label.Define = true;
                labels.Add(label);
            }
            //首先处理AOBScanModule命令
            for(i = 0;i < TotalLine; ++i)
            {
                Currentline = Codes[i].Trim();
                InstrPrefix = Currentline.ToUpper();
                if (Substring(InstrPrefix,0,14) == "AOBSCANMODULE(")
                {
                    regex = new Regex(@",");
                    seek = 14;
                    size = Currentline.Length - seek - 1;
                    string[] args = regex.Split(Substring(Currentline,seek,size));
                    TrimArgs(ref args);
                    if (args.Length != 3)
                    {
                        AutoAssemble_Error = ("AobScan parameters Error!Line number:" + i.ToString() + ",Code:" + Codes[i]);
                        ErrorState = "LackParameters";
                        return false;
                    }
                    define.DefineName = args[0];
                    if (DefineExist(define.DefineName, defines))
                    {
                        ErrorState = "DefineAlreadyExist";
                        AutoAssemble_Error = ("Symbol: " + args[0] + " already exist!");
                        return false;
                    }
                    define.Value = Memory.AobScanModule(args[1], args[2]).ToString("x");
                    if (define.Value == "0")
                    {//未找到特征码
                        ErrorState = "AOBScanFailed";
                        AutoAssemble_Error = "Cannot find AOB's address!Line number:" + i.ToString() + ",Code:" + Codes[i];
                        return false;
                    }
                    defines.Add(define);
                    //清除AOBScan命令，为第二次循环
                    Codes[i] = "";
                }
            }
            //开始处理其它命令并将不是自动汇编引擎命令的汇编指令加入到汇编指令数组中
            for(i = 0;i < TotalLine; ++i)
            {
                Currentline = Codes[i].Trim();
                if (String.IsNullOrEmpty(Codes[i]))
                {
                    continue;
                }
                if (Substring(Currentline, 0, 2) == "//")
                {
                    continue;
                }
                if (Codes[i].IndexOf("//") > 0)
                {
                    Currentline = Codes[i].Substring(0, Codes[i].Length - Codes[i].IndexOf("//"));
                }
                regex = new Regex(@",");
                string[] args;
                InstrPrefix = Currentline.ToUpper();
                if (Substring(InstrPrefix, 0, 6) == "ALLOC(")
                {
                    Currentline = Currentline.Replace("\"", "");
                    seek = Currentline.IndexOf("(") + 1;
                    size = Currentline.Length - seek - 1;
                    args = regex.Split(Substring(Currentline,seek,size));
                    TrimArgs(ref args);
                    if (args.Length > 3) 
                    { 
                        AutoAssemble_Error = "Alloc parameters overload!Line number:" + i.ToString() + ",Code:" + Codes[i];
                        ErrorState = "ParametersOverload";
                        return false;
                    }
                    if(args.Length == 3)
                    {
                        if (AllocExist(args[0], AllocedMemorys))
                        {
                            ErrorState = "AllocAlreadyExist";
                            AutoAssemble_Error = "Symbol: " + args[0] + " already alloc!";
                            return false;
                        }
                        alloc.AllocName = args[0];
                        try
                        {
                            alloc.size = StrToInt(args[1]);
                        }
                        catch(FormatException)
                        {
                            AutoAssemble_Error = ("Is not a valid integer!Line number:" + i.ToString() + ",Code:" + Codes[i]);
                            ErrorState = "NotValidInteger";
                            return false;
                        }
                        long NearAddress = GetAddress(args[2]);
                        if(NearAddress == 0)
                        {
                            AutoAssemble_Error = ("Parameter 3 gives an unknown module!Line number:" + i.ToString() + ",Code:" + Codes[i]);
                            ErrorState = "UnknownModule";
                            return false;
                        }
                        alloc.Zero = false;
                        alloc.Address = Memory.FindNearFreeBlock(NearAddress,alloc.size);
                    }
                    if(args.Length == 2)
                    {
                        if (AllocExist(args[0], AllocedMemorys))
                        {
                            AutoAssemble_Error = ("Symbol: " + args[0] + " already alloc!");
                            ErrorState = "AllocAlreadyExist";
                            return false ;
                        }
                        alloc.AllocName = args[0];
                        alloc.Address = Memory.AllocMemory(0, StrToInt(args[1]));
                        alloc.Zero = true;
                        goto end;
                    }
                    if(args.Length < 2)
                    {
                        AutoAssemble_Error = ("Alloc parameters Error!Line number:" + i.ToString() + ",Code:" + Codes[i]);
                        return false;
                    }
                    if(alloc.Address == 0)
                    {
                        AutoAssemble_Error = "Alloc memory " + allocs[i].AllocName + "failed!";
                        ErrorState = "MemoryAllocFailed";
                        return false;
                    }
                    end:
                    label.Define = true;
                    label.LabelName = alloc.AllocName;
                    label.Address = alloc.Address;
                    labels.Add(label);
                    allocs.Add(alloc);
                    continue;
                }
                if (Substring(InstrPrefix, 0, 8) == "DEALLOC(")
                {
                    seek = Currentline.IndexOf("(") + 1;
                    size = Currentline.Length - seek - 1;
                    s = Currentline.Substring(seek, size).Trim();
                    Deallocs.Add(s);//将其加入到Deallocs数组,在最后处理
                    continue;
                }
                if (Substring(InstrPrefix, 0, 6) == "LABEL(")
                {
                    seek = Currentline.IndexOf("(") + 1;
                    size = Currentline.Length - seek - 1;
                    s = Currentline.Substring(seek, size).Trim();
                    if (!LabelExist(s, labels))
                    {
                        label.LabelName = s;
                        label.Define = false;
                        label.Address = 0;
                    }
                    else
                    {
                        ErrorState = "LabelAlreadyExist";
                        AutoAssemble_Error = ("Label: "+s +" already exist!" + "Line number: " + i.ToString() + ", Code: " + Codes[i]);
                        return false;
                    }
                    labels.Add(label);
                    continue;
                }
                if(Substring(InstrPrefix, 0,13) == "CREATETHREAD(")
                {
                    seek = Currentline.IndexOf("(") + 1;
                    size = Currentline.Length - seek - 1;
                    s = Currentline.Substring(seek, size).Trim();
                    Threads.Add(s);
                    continue;
                }
                if (Substring(InstrPrefix, 0, 15) == "REGISTERSYMBOL(")
                {
                    seek = Currentline.IndexOf("(") + 1;
                    size = Currentline.Length - seek - 1;
                    s = Currentline.Substring(seek, size).Trim();
                    registers.Add(s);
                    continue;
                }
                if(Substring(InstrPrefix, 0, 17) == "UNREGISTERSYMBOL(")
                {
                    s = Currentline.Substring(17, Currentline.Length - 18).Trim();
                    for(j = 0;j < RegisteredSymbols.Count; ++j)
                    {
                        if(s == RegisteredSymbols[j].SymbolName)
                        {
                            RegisteredSymbols.RemoveAt(j);
                            break;
                        }
                    }
                    continue;
                }
                //不是自动汇编引擎命令,应为汇编指令,加入到汇编指令数组
                AssembleCode.Add(Currentline);
            }

            //开始处理汇编指令集
            Codes = AssembleCode.ToArray();
            TotalLine = Codes.Length;
            for(i = 0;i < TotalLine; i++)
            {
                Currentline = ReplaceWithDefines(Codes[i], defines);
                InstrPrefix = Currentline.ToUpper();
                if (Currentline[Currentline.Length - 1] != ':')//处理汇编指令
                {
                    if (Substring(InstrPrefix, 0, 3) == "DB ")
                    {
                        s = Currentline.Substring(3, Currentline.Length - 3);
                        assembled.AssembledBytes = Memory.HexToBytes(s);
                        assembled.CurrentAddress = CurrentAddress;
                        assembleds.Add(assembled);
                        CurrentAddress += assembled.AssembledBytes.Length;
                        continue;
                    }
                    if (Substring(InstrPrefix, 0, 4) == "NOP ")
                    {
                        s = Currentline.Substring(4, Currentline.Length - 4);
                        try
                        {
                            x = Convert.ToInt32(s,16);
                        }
                        catch (FormatException)
                        {
                            AutoAssemble_Error = ("Is not a valid integer!" + " Code: " + Codes[i]);
                            ErrorState = "InvalidValue";
                            return false;
                        }
                        assembled.AssembledBytes = new byte[x];
                        assembled.CurrentAddress = CurrentAddress;
                        for (j = 0; j < x; ++j)
                        {
                            assembled.AssembledBytes[j] = 0x90;
                        }
                        assembleds.Add(assembled);
                        CurrentAddress += x;
                        continue;
                    }
                    seek = Currentline.IndexOf("(");
                    if(seek != -1)//有涉及到类型转换
                    {
                        seek++;
                        size = Currentline.IndexOf(")", seek) - seek;
                        string type = Currentline.Substring(seek, size).ToUpper();
                        size++;
                        string value = Substring(Currentline, seek + size, Currentline.Length);
                        string Prefix = Currentline.Substring(0,Currentline.IndexOf(",") + 1);
                        try
                        {
                            if (type == "FLOAT")
                            {
                                value = BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(value)), 0).ToString("x");
                                Currentline = Prefix + value;
                            }
                            if (type == "DOUBLE")
                            {
                                value = BitConverter.ToInt64(BitConverter.GetBytes(Convert.ToDouble(value)), 0).ToString("x");
                                Currentline = Prefix + value;
                            }
                        }
                        catch (FormatException)
                        {
                            ErrorState = "InvalidValue";
                            AutoAssemble_Error = "Is not a valid value!" + " Code: " + Codes[i];
                            return false;
                        }
                        Codes[i] = Currentline;
                    }
                    x = labels.Count;
                    Currentline = ReplaceWithDefineLabels(Currentline, labels);
                    s = Currentline;
                    for(j = 0; j < x; ++j)//寻找指令有关联的非定义标签
                    {
                        if (Currentline.IndexOf(labels[j].LabelName) != -1)
                        {
                            long LabelParentAddress = CurrentAddress;
                            mustbefar = false;
                            long diff = 0;
                            for (int y = i + 1; y < TotalLine; ++y)
                            {
                                Currentline2 = ReplaceWithDefines(Codes[y],defines);
                                if(Currentline2[Currentline2.Length -1] == ':')//Currentline2是一个被使用的地址标签
                                {
                                    s = Currentline2.Substring(0, Currentline2.Length - 1);
                                    if (Currentline2 == (labels[j].LabelName + ":"))//找到了非定义标签
                                    {
                                        if (CurrentAddress > LabelParentAddress)
                                            diff = CurrentAddress - LabelParentAddress;
                                        else
                                            diff = LabelParentAddress - CurrentAddress;
                                        if (diff > 0x80000000)
                                            mustbefar = true;
                                        else
                                            mustbefar = false;
                                        //最后的清尾工作,将当前汇编指令索引,地址和汇编指令字节集索引储存到reassembles数组
                                        reassemble.CurrentAddress = CurrentAddress;
                                        reassemble.Reflect = i;
                                        reassemble.Reflect2 = assembleds.Count;
                                        reassembles.Add(reassemble);
                                        break;
                                    }
                                    else
                                    {
                                        if (DefineLabelExist(s, labels))
                                        {
                                            LabelParentAddress = GetAddressByLabelName(s, labels);
                                            continue;
                                        }
                                        LabelParentAddress = AddressParse(s,ref labels, LabelParentAddress).address;
                                    }
                                }
                            }
                            if (diff < 128)
                            {
                                s = Currentline.Replace(labels[j].LabelName, (CurrentAddress + 127).ToString("x"));
                                continue;
                            }
                            if (mustbefar)
                                s = Currentline.Replace(labels[j].LabelName, (CurrentAddress + 0x2000FFFFF).ToString("x"));
                            else
                                s = Currentline.Replace(labels[j].LabelName, (CurrentAddress + 0xFFFFF).ToString("x"));
                        }
                    }
                    Currentline = s;
                    Parameters = Memory.Assemble(s, CurrentAddress, Memory.is64bit);
                    if (Parameters.AsmLength == 0)
                    {
                        AutoAssemble_Error = ("Unknown Asm instruction: " + Codes[i] + ".Error info:" + Parameters.error);
                        ErrorState = "UnknownAsmInstruction";
                        return false;
                    }
                    assembled.CurrentAddress = CurrentAddress;
                    assembled.AssembledBytes = new byte[Parameters.AsmLength];
                    ByteCopy(Parameters.Bytes, assembled.AssembledBytes, Parameters.AsmLength);
                    assembleds.Add(assembled);
                    CurrentAddress += assembled.AssembledBytes.Length;
                    continue;
                }
                /*---------------------------------------分割线-------------------------------------*/
                if (Currentline[Currentline.Length - 1] == ':')//处理定义标签
                {
                    s = Currentline.Substring(0, Currentline.IndexOf(":")).Trim();
                    address = AddressParse(s, ref labels, CurrentAddress);
                    if (address.isVirtualLabel && address.Multiple)
                    {
                        AutoAssemble_Error = ("Virtual label cannot add offset before have valid value: " + address.VirtualLabel + " !" + " Code: " + Codes[i]);
                        ErrorState = "InvalidCode";
                        return false;
                    }
                    if (address.isVirtualLabel)
                    {
                        if (!RemoveLabelByName(address.VirtualLabel, labels))
                        {
                            AutoAssemble_Error = ("Undefined label: " + address.VirtualLabel + " !" + " Code: " + Codes[i]);
                            ErrorState = "UndefinedLabel";
                            return false;
                        }
                        //初始化一个新Label结构，用于清空结构原有数据
                        label = new Label
                        {
                            LabelName = address.VirtualLabel,
                            Address = CurrentAddress,
                            Define = true
                        };
                        labels.Add(label);
                        continue;
                    }
                    CurrentAddress = address.address;
                }
            }
            //大部分汇编指令已被处理完毕,现在处理需要被重汇编的指令(reassembles)
            Assembled[] assembledArray = assembleds.ToArray();
            x = reassembles.Count;
            for(i = 0; i < x; ++i)
            {
                Currentline = ReplaceWithDefineLabels(Codes[reassembles[i].Reflect],labels);
                Parameters = Memory.Assemble(Currentline, reassembles[i].CurrentAddress, Memory.is64bit);
                if(Parameters.AsmLength == 0)
                {
                    AutoAssemble_Error = ("Unknown Asm instruction: " + Codes[reassembles[i].Reflect] + ".Error info:" + Parameters.error);
                    ErrorState = "UnknownAsmInstruction";
                    return false;
                }
                ByteCopy(Parameters.Bytes, assembledArray[reassembles[i].Reflect2].AssembledBytes, Parameters.AsmLength);
            }
            //开始分配内存
            for(i = 0;i < allocs.Count; ++i)
            {
                if (allocs[i].Zero == true)
                {
                    AllocedMemorys.Add(allocs[i]);
                    continue;
                }
                if (Memory.AllocMemory(allocs[i].Address, allocs[i].size) == 0)
                {
                    AutoAssemble_Error = "Alloc memory " + allocs[i].AllocName + "failed!Near Address:"+allocs[i].Address.ToString("x");
                    ErrorState = "MemoryAllocFailed";
                    return false;
                }
                AllocedMemorys.Add(allocs[i]);
            }
            //重汇编指令已处理完毕!开始执行写入内存操作
            assembledArray = MergeAssembles(assembledArray);//将分散的汇编指令以区块为标准合并
            for(i = 0;i < assembledArray.Length; ++i)
            {
                if (!Memory.WriteMemoryByteSet(assembledArray[i].CurrentAddress, assembledArray[i].AssembledBytes))
                {
                    AutoAssemble_Error = ("Write process memory error!");
                    ErrorState = "WriteMemoryError";
                    return false;
                } 
            }
            //执行释放内存操作
            for(i = 0; i < Deallocs.Count;++i)
            {
                s = Deallocs[i];
                if (!FreeAllocMemory(s))
                {
                    ErrorState = "FreeMemoryError";
                    AutoAssemble_Error = "Free memory " + s + " error!";
                    return false;
                }
            }
            //将defines数组的成员加入到label数组
            for(i = 0; i < defines.Count; ++i)
            {
                label.LabelName = defines[i].DefineName;
                label.Address = Convert.ToInt64(defines[i].Value,16);
                label.Define = true;
                labels.Add(label);
            }
            //执行创建线程操作
            bool ok = true;
            for(i = 0; i < Threads.Count; ++i)
            {
                if(Memory.CreateThread(GetAddressByLabelName(Threads[i],labels)) == null)//线程创建失败不会退出当前函数
                {
                    AutoAssemble_Error = "Create thread failed!";
                    ErrorState = "CreateThreadFailed";
                    ok = false;
                    continue;
                }
            }
            //现在将脚本注册的全局符号赋值,汇编脚本处理完毕
            if (!RegisterSymbols(registers, labels))
                ok = false;
            return ok;
        }
        private bool RemoveLabelByName(string name,List<Label> labels)
        {
            int len = labels.Count;
            for(int i = 0; i < len; ++i)
            {
                if(labels[i].LabelName == name)
                {
                    labels.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }
        private void ByteCopy(byte[] src,byte[] dest,int Lenth)
        {
            for(int i = 0; i < Lenth; ++i)
            {
                dest[i] = src[i];
            }
        }
        public long StrToLong(string s)
        {
            string number;
            if(s[0] == '$')
            {
                number = Substring(s, 1, s.Length - 1);
                return Convert.ToInt64(number, 16);
            }
            else
            {
                return Convert.ToInt64(s);
            }
        }
        public int StrToInt(string s)
        {
            string number;
            if (s[0] == '$')
            {
                number = Substring(s, 1, s.Length - 1);
                return Convert.ToInt32(number, 16);
            }
            else
            {
                return Convert.ToInt32(s);
            }
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
                MemoryAPI.Number[] numbers = Memory.OperationParse(SplitExps[i]);
                int x = 0;
                while (x < numbers.Length)
                {
                    temp = 0;
                    //寻找全局符号数组
                    foreach (RegisterSymbol symbol in RegisteredSymbols)
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
                        temp = Memory.GetModuleBaseaddress(numbers[x].Value);
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
                if (Memory.is64bit)
                {
                    if (!Memory.ReadMemoryInt64(Address, ref Address))
                    {
                        return 0;
                    }
                    continue;
                }
                else
                {
                    int tAddress = (int)Address;
                    if (!Memory.ReadMemoryInt32(Address, ref tAddress))
                    {
                        return 0;
                    }
                    Address = tAddress;
                }
            }
            return Address;
        }
        public string[] GetExecuteMode(string Code,bool Enable)
        {
            Regex regex = new Regex(@"\r\n");
            string[] AllCode = regex.Split(Code);
            int i, x;
            int seek,size;
            int Lines = AllCode.Length;
            string Currentline = "";
            List<string> ReturnList = new List<string>();
            if (Enable)
            {
                for(i = 0; i < Lines; ++i)
                {
                    Currentline = AllCode[i];
                    if (Substring(Currentline, 0, 2) == "//")
                    {
                        continue;
                    }
                    if (AllCode[i].IndexOf("//") > 0)
                    {
                        seek = AllCode[i].IndexOf("//");
                        size = AllCode[i].Length - (AllCode[i].Length - seek);
                        Currentline = AllCode[i].Substring(0, size);
                    }
                    if (Currentline.Trim().ToUpper() == "[ENABLE]")//找到了启用开头
                    {
                        x = i + 1;
                        for(; x < Lines; ++x)
                        {
                            Currentline = AllCode[x];
                            if (Substring(Currentline, 0, 2) == "//")
                            {
                                continue;
                            }
                            if (AllCode[x].IndexOf("//") > 0)
                            {
                                seek = AllCode[x].IndexOf("//");
                                size = AllCode[x].Length - (AllCode[x].Length - seek);
                                Currentline = AllCode[x].Substring(0, size);
                            }
                            if (Currentline.Trim().ToUpper() != "[DISABLE]")
                                ReturnList.Add(Currentline);
                            else
                                return ReturnList.ToArray();
                        }
                        return ReturnList.ToArray();
                    }
                }
            }
            else
            {
                for (i = 0; i < Lines; ++i)
                {
                    Currentline = AllCode[i];
                    if (Substring(Currentline, 0, 2) == "//")
                    {
                        continue;
                    }
                    if (AllCode[i].IndexOf("//") > 0)
                    {
                        seek = AllCode[i].IndexOf("//");
                        size = AllCode[i].Length - (AllCode[i].Length - seek);
                        Currentline = AllCode[i].Substring(0, size);
                    }
                    if (Currentline.Trim().ToUpper() == "[DISABLE]")//找到了启用开头
                    {
                        x = i + 1;
                        for (; x < Lines; ++x)
                        {
                            Currentline = AllCode[x];
                            if (Substring(Currentline, 0, 2) == "//")
                            {
                                continue;
                            }
                            if (AllCode[x].IndexOf("//") > 0)
                            {
                                seek = AllCode[x].IndexOf("//");
                                size = AllCode[x].Length - (AllCode[x].Length - seek);
                                Currentline = AllCode[x].Substring(0, size);
                            }
                            if (Currentline.Trim().ToUpper() != "[ENABLE]")
                                ReturnList.Add(Currentline);
                            else
                                return ReturnList.ToArray();
                        }
                        return ReturnList.ToArray();
                    }
                }
            }
            return null;
        }
        private bool RegisterSymbols(List<string> registerSymbols,List<Label> labels)
        {
            int SymbolCount = registerSymbols.Count;
            int LabelCount = labels.Count;
            int i = 0;
            string[] symbols = registerSymbols.ToArray();
            RegisterSymbol symbol;
            for(i = 0; i < SymbolCount; ++i)
            {
                symbol = new RegisterSymbol();
                if (SymbolExist(registerSymbols[i], RegisteredSymbols))
                {
                    int index = FindSymbolIndex(registerSymbols[i]);
                    RegisteredSymbols.RemoveAt(index);
                }
                for (int x = 0; x < LabelCount; ++x)
                {
                    if (symbols[i] == labels[x].LabelName)
                    {
                        symbol.SymbolName = labels[x].LabelName;
                        symbol.Address = labels[x].Address;
                        RegisteredSymbols.Add(symbol);
                        break;
                    }
                }
                if(symbol.Address == 0)
                {
                    ErrorState = "InvalidSymbol";
                    AutoAssemble_Error = "Cannot find symbol " + registerSymbols[i] + " in this script!";
                    return false;
                }
            }
            return true;
        }
        private void TrimArgs(ref string[] args)
        {
            int x = args.Length;
            for(int i = 0; i < x; ++i)
            {
                args[i] = args[i].Trim();
            }
        }
        private string Substring(string str,int start, int length)
        {
            int Len = str.Length;
            if((start + length) > Len)
            {
                length = Len - start;
                return str.Substring(start, length);
            }
            return str.Substring(start, length);
        }
        private Address AddressParse(string expression,ref List<Label> labels, long CurrentAddress)
        {
            long reserved = CurrentAddress;
            Address address = new Address();
            address.isVirtualLabel = false;
            MemoryAPI.Number[] numbers = Memory.OperationParse(expression);
            long temp = 0;
            if (numbers.Length == 1) goto label;
            for (int i = 0; i < numbers.Length; ++i)
            {
                temp = 0;
                reserved = CurrentAddress;
                CurrentAddress = GetAddressByLabelName(numbers[i].Value, labels);
                if (CurrentAddress == 0)
                {
                    CurrentAddress = Memory.GetModuleBaseaddress(numbers[i].Value);
                    if(CurrentAddress == 0)
                    {
                        try
                        {
                            CurrentAddress = reserved;
                            temp = Convert.ToInt64(numbers[i].Value, 16);
                        }
                        catch (FormatException)
                        {//不是数字和模块,应为未定义标签
                            CurrentAddress = reserved;
                            if (numbers.Length > 1)
                            {
                                address.VirtualLabel = numbers[i].Value;
                                address.isVirtualLabel = true;
                                address.Multiple = true;
                                return address;
                            }
                        }
                    }
                }
                switch (numbers[i].Type)
                {
                    case MemoryAPI.OperationType.Add:
                        CurrentAddress += temp;
                        break;
                    case MemoryAPI.OperationType.Sub:
                        CurrentAddress -= temp;
                        break;
                    case MemoryAPI.OperationType.Mul:
                        CurrentAddress *= temp;
                        break;
                }
            }
            address.address = CurrentAddress;
            address.Multiple = true;
            return address;
        //不涉及到多重运算
        label:
            CurrentAddress = GetAddressByLabelName(numbers[0].Value, labels);
            if (CurrentAddress == 0)
            {
                CurrentAddress = Memory.GetModuleBaseaddress(numbers[0].Value);
                if(CurrentAddress == 0)
                {
                    try
                    {
                        CurrentAddress = Convert.ToInt64(expression, 16);
                    }
                    catch (FormatException)
                    {//不是数字和模块,应为未定义标签
                        address.VirtualLabel = numbers[0].Value;
                        address.address = reserved;
                        address.isVirtualLabel = true;
                        address.Multiple = false;
                        return address;
                    }
                }
            }
            address.isVirtualLabel = false;
            address.Multiple = false;
            address.address = CurrentAddress;
            return address;
        }
        struct Address
        {
            public long address;
            public string VirtualLabel;
            public bool isVirtualLabel;
            public bool Multiple;
        }
        private Assembled[] MergeAssembles(Assembled[] assembleds)
        {
            byte[] Bytes;
            long Address = 0;
            Assembled assembled = new Assembled();
            List<Assembled> assembledBlock = new List<Assembled>();
            int BlockSize = 0;
            int loop = assembleds.Length;
            int times = 0;
            for(int i = 0;i < loop; ++i)
            {
                if(i+1 == loop)
                {
                    if(loop == 1)
                    {
                        assembled.AssembledBytes = assembleds[0].AssembledBytes;
                        assembled.CurrentAddress = assembleds[0].CurrentAddress;
                        assembledBlock.Add(assembled);
                        return assembledBlock.ToArray();
                    }
                    long x = assembleds[i - 1].CurrentAddress + assembleds[i - 1].AssembledBytes.Length;
                    if (x == assembleds[i].CurrentAddress)
                    {//处理到数组最后一个元素
                        BlockSize += assembleds[i].AssembledBytes.Length;
                        assembled.CurrentAddress = assembleds[i - times].CurrentAddress;
                        int index = 0;
                        Bytes = new byte[BlockSize];
                        while (times >= 0)
                        {
                            assembleds[i - times].AssembledBytes.CopyTo(Bytes, index);
                            index += assembleds[i - times].AssembledBytes.Length;
                            times--;
                        }
                        assembled.AssembledBytes = Bytes;
                        assembledBlock.Add(assembled);
                        break;
                    }
                    else
                    {
                        assembled.AssembledBytes = assembleds[i].AssembledBytes;
                        assembled.CurrentAddress = assembleds[i].CurrentAddress;
                        assembledBlock.Add(assembled);
                        break;
                    }
                }
                Address = assembleds[i].CurrentAddress + assembleds[i].AssembledBytes.Length;
                if(Address == assembleds[i + 1].CurrentAddress)
                {
                    times++;
                    BlockSize += assembleds[i].AssembledBytes.Length;
                    continue;
                }
                else
                {
                    BlockSize += assembleds[i].AssembledBytes.Length;
                    assembled.CurrentAddress = assembleds[i - times].CurrentAddress;//取首汇编指令的地址
                    int index = 0;
                    Bytes = new byte[BlockSize];
                    while(times >= 0)
                    {
                        assembleds[i - times].AssembledBytes.CopyTo(Bytes, index);
                        index += assembleds[i - times].AssembledBytes.Length;
                        times--;
                    }
                    times = 0;
                    BlockSize = 0;
                    assembled.AssembledBytes = Bytes;
                    assembledBlock.Add(assembled);
                }
            }
            return assembledBlock.ToArray();
        }
        private bool DefineExist(string name,List<Define> defines)
        {
            int x = defines.Count;
            for(int i = 0; i < x; ++i)
            {
                if (defines[i].DefineName == name)
                    return true;
            }
            return false;
        }
        private int FindSymbolIndex(string name)
        {
            int count = RegisteredSymbols.Count;
            for(int i = 0; i < count; ++i)
            {
                if(RegisteredSymbols[i].SymbolName == name)
                {
                    return i;
                }
            }
            return -1;
        }
        private string ReplaceWithDefines(string code,List<Define> defines)
        {
            for(int i = 0; i < defines.Count; ++i)
            {
                if(code.IndexOf(defines[i].DefineName) != -1)
                {
                    code = code.Replace(defines[i].DefineName, defines[i].Value);
                }
            }
            return code;
        }
        private string ReplaceWithDefineLabels(string code, List<Label> labels)
        {
            for (int i = 0; i < labels.Count; ++i)
            {
                if (code.IndexOf(labels[i].LabelName) != -1 && labels[i].Define)
                {
                    code = code.Replace(labels[i].LabelName, labels[i].Address.ToString("x"));
                }
            }
            return code;
        }
        public bool FreeAllocMemory(string AllocName)
        {
            for (int i = 0; i < AllocedMemorys.Count; ++i)
            {
                if (AllocName == AllocedMemorys[i].AllocName)
                {
                    bool ok = MemoryAPI.VirtualFreeEx(Memory.ProcessHandle,AllocedMemorys[i].Address, AllocedMemorys[i].size,MEM_DECOMMIT);
                    AllocedMemorys.RemoveAt(i);
                    return ok;
                }
            }
            return false;
        }
        private long GetAddressByLabelName(string LabelName,List<Label> Labels)
        {
            int x = Labels.Count;
            for(int i = 0; i < x; ++i)
            {
                if(LabelName == Labels[i].LabelName && Labels[i].Define)
                {
                    return Labels[i].Address;
                }
            }
            return 0;
        }
        private bool LabelExist(string Labelname,List<Label> Labels)
        {
            for(int i = 0;i < Labels.Count; ++i)
            {
                if(Labelname == Labels[i].LabelName)
                {
                    return true;
                }
            }
            return false;
        }
        private bool DefineLabelExist(string Labelname, List<Label> Labels)
        {
            for (int i = 0; i < Labels.Count; ++i)
            {
                if (Labelname == Labels[i].LabelName && Labels[i].Define)
                {
                    return true;
                }
            }
            return false;
        }
        private bool AllocExist(string AllocName,List<AllocedMemory> alloceds)
        {
            for (int i = 0; i < alloceds.Count; ++i)
            {
                if (AllocName == alloceds[i].AllocName)
                {
                    return true;
                }
            }
            return false;
        }
        private bool SymbolExist(string SymbolName,List<RegisterSymbol> symbols)
        {
            for (int i = 0; i < symbols.Count; ++i)
            {
                if (SymbolName == symbols[i].SymbolName)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
