﻿using System.Text.RegularExpressions;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Text;

namespace AutoAssembler
{
    public class AutoAssembler
    {
        private static AutoAssembler SymbolDecoder;
        public AutoAssembler([In]MemoryAPI API)
        {
            Memory = API;
            RegisteredSymbols = new List<RegisterSymbol>();
            Scripts = new List<Script>();
            TempScriptAlloceds = new List<AllocedMemory>();
            OK = true;
            SymbolDecoder = this;
        }
        public AutoAssembler(string ProcessName)
        {
            OK = true;
            Memory = new MemoryAPI(ProcessName);
            RegisteredSymbols = new List<RegisterSymbol>();
            Scripts = new List<Script>();
            TempScriptAlloceds = new List<AllocedMemory>();
            if (!Memory.ok)
            {
                OK = false;
                ErrorState = "OpenProcessFailed";
                ErrorInfo = "Open process " + ProcessName + " failed!";
                return;
            }
        }
        /// <summary>
        /// 重新初始化自动汇编引擎,包括使所有脚本回到初始状态(使所有脚本处于未启用状态，并为所有脚本分配一个全新alloceds成员),清除全局符号列表，清除匿名脚本内存分配堆.(但它不会撤销对之前附加的程序进行的任意更改)
        /// </summary>
        public void ResetEngine(string ProcessName)
        {
            OK = true;
            Memory = new MemoryAPI(ProcessName);
            RegisteredSymbols = new List<RegisterSymbol>();
            TempScriptAlloceds = new List<AllocedMemory>();
            if (!Memory.ok)
            {
                OK = false;
                ErrorState = "OpenProcessFailed";
                ErrorInfo = "Open process " + ProcessName + " failed!";
                return;
            }
            for(int i = 0; i < Scripts.Count; i++)
            {
                if(Scripts[i].GetStatus == Script.Status.Enabled)
                {
                    Scripts[i].Enable = !Scripts[i].Enable;
                    Scripts[i].alloceds = new List<AllocedMemory>();
                }
            }
            SymbolDecoder = this;
            GC.Collect(200,GCCollectionMode.Optimized);
        }
        public bool ChangeScript(string name, Script script)
        {
            for (int i = 0; i < Scripts.Count; i++)
            {
                if (Scripts[i].Name == name)
                {
                    Scripts.RemoveAt(i);
                    Scripts.Insert(i, script);
                    return true;
                }
            }
            return false;
        }
        public bool ProcessIsAlive()
        {
            long address = Memory.VirtualAlloc(0, 0x1000);
            if (address == 0)
            {
                return false;
            }
            MemoryAPI.VirtualFreeEx(Memory.ProcessHandle, address, 0x1000, MEM_DECOMMIT);
            return true;
        }
        public string[] Close()
        {
            List<string> UnClosedScripts = new List<string>();
            for(int i = 0;i < Scripts.Count; i++)
            {
                if(GetScriptStatus(Scripts[i].Name) == Script.Status.Enabled)
                {
                    if (RunScript(Scripts[i].Name) == false)
                    {
                        UnClosedScripts.Add(Scripts[i].Name);
                    }  
                }
            }
            Memory.Close();
            if (UnClosedScripts.Count == 0)
                return null;
            return UnClosedScripts.ToArray();
        }
        public List<RegisterSymbol> RegisteredSymbols;
        public List<Script> Scripts;
        public List<AllocedMemory> TempScriptAlloceds;
        public string ErrorInfo;
        public string ErrorState;
        public bool OK;
        public MemoryAPI Memory;
        private byte[] DataBuf = new byte[1024];
        private List<Label> SymbolsPointer;
        private int CodeNumber, AssembledsNumber;
        private long CurAddress;
        public enum Assembler_Status
        {
            //XED汇编引擎状态
            Assembler_Error = 0, Assembler_OK = 1
        };
        [DllImport("XEDParse.dll", EntryPoint = "XEDParseAssemble")]
        private static extern Assembler_Status Assemble(ref Assembler_Data Parameter);
        public delegate bool CBXEDPARSE_UNKNOWN(string Text, ref Int64 value);
        [StructLayout(LayoutKind.Sequential)]
        public struct Assembler_Data
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
        public enum ReassembleType
        {
            LabelCorrection,
            LastReassemble,
            AddressAligned,
        }
        public struct Define
        {
            public string Original;
            public string Replace;
        }
        public struct Assert
        {
            public string Address;
            public short[] ByteCodes;
        }
        public struct Assembled
        {
            public byte[] AssembledBytes;
            public long CurrentAddress;
        }
        public struct AobScan_args
        {
            public string OriginCode;
            public string LabelName;
            public string Module;
            public string AobString;
        }
        public struct Label
        {
            public string LabelName;
            public string ParentLabel;
            public long Address;
            public long GuessAddress;
            public int PositionInCodes;
            public bool VirtualLabel;
            public List<Reference> references;
        }
        public struct Reference
        {
            public long CurrentAddress;
            public short CodeReflect;
            public short AssembledsReflect;
        }
        public struct AllocedMemory
        {
            public string AllocName;
            public int RefHeap;
            public int MemNumber;
            public long Address;
            public int Size;
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
        public bool AddScript(string ScriptName,string ScriptCode)
        {
            for(int i = 0; i < Scripts.Count; i++)
            {
                if (ScriptName == Scripts[i].Name)
                {
                    ErrorInfo = "Script " + ScriptName + " already exist!";
                    return false;
                }
            }
            Script script = new Script
            {
                Name = ScriptName,
                ScriptCode = ScriptCode,
                alloceds = new List<AllocedMemory>()
            };
            Scripts.Add(script);
            return true;
        }
        public Script.Status GetScriptStatus(string ScriptName)
        {
            for (int i = 0; i < Scripts.Count; i++)
            {
                if (ScriptName == Scripts[i].Name)
                {
                    return Scripts[i].GetStatus;
                }
            }
            ErrorInfo = "Script " + ScriptName + " not exist!";
            return Script.Status.Inexistence;
        }
        public bool RemoveScript(string ScriptName)
        {
            for (int i = 0; i < Scripts.Count; i++)
            {
                if (ScriptName == Scripts[i].Name)
                {
                    if (Scripts[i].GetStatus == Script.Status.Enabled)
                    {
                        ErrorInfo = "Script " + ScriptName + " is running!You cannot remove it unless disable it.";
                        return false;
                    }
                    Scripts.RemoveAt(i);
                    return true;
                }
            }
            ErrorInfo = "Script " + ScriptName + " not exist!";
            return false;
        }
        public bool RunScript(string ScriptName)
        {
            for(int i = 0; i < Scripts.Count; i++)
            {
                if (ScriptName != Scripts[i].Name)
                    continue;
                string[] codes = GetExecuteMode(Scripts[i].ScriptCode, Scripts[i].Enable);
                if(AutoAssemble(codes,ref Scripts[i].alloceds))
                {
                    Scripts[i].Enable = !Scripts[i].Enable;
                    return true;
                }
                return false;
            }
            ErrorState = "InvalidScript";
            ErrorInfo = "Cannot find script " + ScriptName + " !";
            return false;
        }
        public string GetErrorInfo()
        {
            return ErrorInfo;
        }
        public string GetErrorState()
        {
            return ErrorState;
        }
        public bool AutoAssemble(string Code,bool Enable)
        {
            string[] codes = GetExecuteMode(Code, Enable);
            return AutoAssemble(codes,ref TempScriptAlloceds);
        }
        public bool AutoAssemble(string[] Codes,ref List<AllocedMemory> alloceds)
        {
            //初始化各种数据
            SymbolDecoder = this;
            List<string> AssembleCode = new List<string>();
            List<string> Threads = new List<string>();
            List<string> Deallocs = new List<string>();
            Assembler_Status status;
            AobScan_args scan_Args = new AobScan_args();
            List<AobScan_args> AobScans = new List<AobScan_args>();
            Reassemble_Args reassemble_args = new Reassemble_Args();
            IAssemble AsmData = new IAssemble();
            List<RegisterSymbol> Symbols = new List<RegisterSymbol>();
            AllocedMemory alloc = new AllocedMemory();
            List<AllocedMemory> allocs = new List<AllocedMemory>();
            Label label = new Label();
            Reference reference;
            List<Label> labels = new List<Label>();
            Assembled assembled = new Assembled();
            Define define;
            List<Define> defines = new List<Define>();
            AddressExpression address = new AddressExpression();
            List<Assembled> assembleds = new List<Assembled>();
            List<string> registers = new List<string>();
            string[] Instr_args;
            string Currentline, s = "";
            ErrorInfo = "";
            ErrorState = "";
            string CurrentParentLabel = null;
            long LastAllocedAddress = 0;
            bool LastReassemble = false;
            int TotalLine,i, j;
            int seek, size;//用于截取字符
            long CurrentAddress = 0;
            TotalLine = Codes.Length;
            string InstrPrefix; //命令前缀,减少ToUpper函数的使用来提升效率
            //将全局符号分配给脚本内部标签
            for (i = 0;i < RegisteredSymbols.Count; ++i)
            {
                label.LabelName = RegisteredSymbols[i].SymbolName;
                label.Address = RegisteredSymbols[i].Address;
                label.VirtualLabel = false;
                labels.Add(label);
            }
            //将分配的内存(alloceds)分配给脚本内部标签
            for(i = 0; i < alloceds.Count; i++)
            {
                label.LabelName = alloceds[i].AllocName;
                label.Address = alloceds[i].Address;
                label.VirtualLabel = false;
                labels.Add(label);
            }
            //首先处理特征码扫描命令和Define命令
            char[] Sep_comma = { ',' };
            char[] Sep_space = { ' ' };
            for (i = 0; i < TotalLine; i++)
            {
                Currentline = Codes[i].Trim();
                InstrPrefix = Currentline.ToUpper();
                if(Substring(InstrPrefix,0,8) == "AOBSCAN(")
                {
                    Instr_args = ArgsParse(Currentline, Sep_comma);
                    TrimArgs(ref Instr_args);
                    if(Instr_args.Length != 2)
                    {
                        ErrorInfo = ("AobScan parameters Error!Line number:" + i.ToString() + ",Code:" + Codes[i]);
                        ErrorState = "ParametersError";
                        return false;
                    }
                    if (LabelExist(Instr_args[0], ref labels))
                    {
                        ErrorState = "LabelAlreadyExist";
                        ErrorInfo = ("Symbol: " + Instr_args[0] + " already exist!");
                        return false;
                    }
                    scan_Args = new AobScan_args()
                    {
                        LabelName = Instr_args[0],
                        Module = null,
                        AobString = Instr_args[1],
                        OriginCode = Currentline
                    };
                    AobScans.Add(scan_Args);
                    Codes[i] = "";
                    continue;
                }
                if (Substring(InstrPrefix, 0, 14) == "AOBSCANMODULE(")
                {
                    Instr_args = ArgsParse(Currentline, Sep_comma);
                    TrimArgs(ref Instr_args);
                    if (Instr_args.Length != 3)
                    {
                        ErrorInfo = ("AobScan parameters Error!Line number:" + i.ToString() + ",Code:" + Codes[i]);
                        ErrorState = "LackParameters";
                        return false;
                    }
                    if (LabelExist(Instr_args[0], ref labels))
                    {
                        ErrorState = "LabelAlreadyExist";
                        ErrorInfo = ("Symbol: " + Instr_args[0] + " already exist!");
                        return false;
                    }
                    scan_Args = new AobScan_args()
                    {
                        LabelName = Instr_args[0],
                        Module = Instr_args[1],
                        AobString = Instr_args[2],
                        OriginCode = Currentline
                    };
                    AobScans.Add(scan_Args);
                    //清除AOBScan命令，为第二次循环
                    Codes[i] = "";
                    continue;
                }
                if (Substring(InstrPrefix, 0, 7) == "DEFINE(")
                {
                    s = Substring(Currentline, 8, Currentline.Length - 9);
                    Instr_args = s.Split(Sep_comma);
                    if(Instr_args.Length < 2 || Instr_args.Length > 2)
                    {
                        ErrorState = "InvalidCode";
                        ErrorInfo = "Syntax error: " + InstrPrefix + " !";
                        return false;
                    }
                    if(DefineExist(Instr_args[0],ref defines))
                    {
                        ErrorState = "DefineAlreadyExist";
                        ErrorInfo = "Define " + Instr_args[0] + " already exist!";
                        return false;
                    }
                    define = new Define
                    {
                        Original = Instr_args[0],
                        Replace = Instr_args[1]
                    };
                    defines.Add(define);
                    Codes[i] = "";
                    continue;
                }
            }
            //进行特征码扫描
            if(!this.AobScans(AobScans.ToArray(), ref labels))
            {
                goto failed;
            }
            //开始处理其它命令并将不是自动汇编引擎命令的汇编指令加入到汇编指令数组中
            for (i = 0;i < TotalLine; ++i)
            {
                Currentline = Codes[i].Trim();
                if (String.IsNullOrEmpty(Codes[i]))
                {
                    continue;
                }
                if (Codes[i].IndexOf("//") > 0)
                {
                    Currentline = Codes[i].Substring(0, Codes[i].Length - Codes[i].IndexOf("//"));
                }
                InstrPrefix = Currentline.ToUpper();
                if (Substring(InstrPrefix, 0, 6) == "ALLOC(")
                {
                    Instr_args = ArgsParse(Currentline,Sep_comma);
                    TrimArgs(ref Instr_args);
                    if (Instr_args.Length > 3) 
                    { 
                        ErrorInfo = "Alloc parameters overload!Line number:" + i.ToString() + ",Code:" + Codes[i];
                        ErrorState = "ParametersOverload";
                        goto failed;
                    }
                    if(Instr_args.Length == 3)
                    {
                        if (AllocExist(Instr_args[0], alloceds))
                        {
                            ErrorState = "AllocAlreadyExist";
                            ErrorInfo = "Symbol: " + Instr_args[0] + " already alloc!";
                            goto failed;
                        }
                        try
                        {
                            alloc.Size = StrToInt_10(Instr_args[1]);
                        }
                        catch(FormatException)
                        {
                            ErrorInfo = ("Is not a valid integer!Line number:" + i.ToString() + ",Code:" + Codes[i]);
                            ErrorState = "NotValidInteger";
                            goto failed;
                        }
                        long NearAddress = GetAddress(Instr_args[2]);
                        if(NearAddress == 0)
                        {
                            ErrorInfo = ("Parameter 3 gives an unknown module!Line number:" + i.ToString() + ",Code:" + Codes[i]);
                            ErrorState = "UnknownModule";
                            goto failed;
                        }
                        Memory.AllocMemory(Instr_args[0], NearAddress, alloc.Size,out alloc);
                    }
                    if(Instr_args.Length == 2)
                    {
                        if (AllocExist(Instr_args[0], alloceds))
                        {
                            ErrorInfo = ("Symbol: " + Instr_args[0] + " already alloc!");
                            ErrorState = "AllocAlreadyExist";
                            goto failed ;
                        }
                        Memory.AllocMemory(Instr_args[0], LastAllocedAddress, StrToInt_10(Instr_args[1]), out alloc);
                        goto end;
                    }
                    if(Instr_args.Length < 2)
                    {
                        ErrorInfo = ("Alloc parameters Error!Line number:" + i.ToString() + ",Code:" + Codes[i]);
                        goto failed;
                    }
                    if(alloc.Address == 0)
                    {
                        ErrorInfo = "Alloc memory " + alloc.AllocName + " failed!";
                        ErrorState = "MemoryAllocFailed";
                        goto failed;
                    }
                    end:
                    label.VirtualLabel = false;
                    label.LabelName = alloc.AllocName;
                    label.Address = alloc.Address;
                    LastAllocedAddress = alloc.Address;
                    label.references = new List<Reference>();
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
                if(Substring(InstrPrefix,0,7) == "ASSERT(")
                {
                    Instr_args = Instr_args = ArgsParse(Currentline, Sep_comma);
                    TrimArgs(ref Instr_args);
                    if (Instr_args.Length < 2 || Instr_args.Length > 2)
                    {
                        ErrorState = "InvalidCode";
                        ErrorInfo = "Syntax error: " + InstrPrefix + " !";
                        return false;
                    }
                    long CmpAddress = LabelParse(Instr_args[0], ref labels,0).Address;
                    if(CmpAddress == 0)
                    {
                        ErrorInfo = string.Format("Cannot find symbol:{0}!Error code:\r\n{1}", Instr_args[0], Currentline);
                        goto failed;
                    }
                    short[] ByteCodes = Memory.HexStringToIntArray(Instr_args[1]);
                    byte[] ByteBuffer = Memory.ReadMemoryByteSet(CmpAddress, ByteCodes.Length);
                    for(int k = 0; k < ByteCodes.Length; k++)
                    {
                        if(ByteCodes[k] == ByteBuffer[k] || ByteCodes[k] == 256)
                        {
                            continue;
                        }
                        else
                        {
                            ErrorInfo = string.Format("Assert-break occurred!Address:{0:X},code:\r\n{1}", CmpAddress, Currentline);
                            goto failed;
                        }
                    }
                    continue;
                }
                if (Substring(InstrPrefix, 0, 6) == "LABEL(")
                {
                    seek = Currentline.IndexOf("(") + 1;
                    size = Currentline.Length - seek - 1;
                    s = Currentline.Substring(seek, size).Trim();
                    if (!LabelExist(s,ref labels))
                    {
                        label.LabelName = s;
                        label.VirtualLabel = true;
                        label.Address = 0;
                        label.references = new List<Reference>();
                    }
                    else
                    {
                        ErrorState = "LabelAlreadyExist";
                        ErrorInfo = ("Label: "+s +" already exist!" + "Line number: " + i.ToString() + ", Code: " + Codes[i]);
                        goto failed;
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
            Codes = AssembleCode.ToArray();
            //将Define命令申请的字符替换
            for(i = 0; i < defines.Count; i++)
            {
                for (j = 0; j < Codes.Length; j++)
                    Codes[j] = Codes[j].Replace(defines[i].Original, defines[i].Replace);
            }
            //对虚拟标签进行估测,对TempLabels进行赋值
            if(!GuessVirtualLabel(ref labels,ref Codes))
            {
                ErrorState = "InvalidCode";
                goto failed;
            }
            SymbolsPointer = labels;
            //开始处理汇编指令集
            AsmData = new IAssemble
            {
                x64 = Memory.is64bit,
                cbUnkonwn = ParseCallBack,
            };
            TotalLine = Codes.Length;
            for(i = 0;i < TotalLine; i++)
            {
                CurAddress = CurrentAddress;
                CodeNumber = i;
                AssembledsNumber = assembleds.Count;
                if (string.IsNullOrEmpty(Codes[i]))
                    continue;
                Currentline = Codes[i];
                InstrPrefix = Currentline.ToUpper();
                if (Currentline[Currentline.Length - 1] != ':')//处理汇编指令
                {
                    AsmData.Asm = Currentline;
                    AsmData.CurrentAddress = CurrentAddress;
                    status = ICompile(ref AsmData);
                    if (status == Assembler_Status.Assembler_Error)
                    {
                        goto failed;
                    }
                    assembled.CurrentAddress = CurrentAddress;
                    assembled.AssembledBytes = new byte[AsmData.AsmLength];
                    ByteCopy(AsmData.Bytes, assembled.AssembledBytes, AsmData.AsmLength);
                    assembleds.Add(assembled);
                    CurrentAddress += assembled.AssembledBytes.Length;
                    continue;
                }
                /*---------------------------------------分割线-------------------------------------*/
                if (Currentline[Currentline.Length - 1] == ':')//处理定义标签
                {
                    s = Currentline.Substring(0, Currentline.IndexOf(":")).Trim();
                    address = LabelParse(s, ref labels, CurrentAddress);
                    if (address.isVirtualLabel && address.Multiple)
                    {
                        ErrorInfo = ("Virtual label cannot add offset before have valid value: " + address.Label + " !\r\n" + Codes[i]);
                        ErrorState = "InvalidCode";
                        goto failed;
                    }
                    if (address.isVirtualLabel)
                    {
                        int index = FindLabelIndex(address.Label, ref labels);
                        if (index == -1)
                        {
                            ErrorInfo = ("Undefined label: " + address.Label + " !" + " Code: " + Codes[i]);
                            ErrorState = "UndefinedLabel";
                            goto failed;
                        }
                        label = labels[index];
                        if (!RemoveLabelByName(address.Label,ref labels))
                        {
                            ErrorInfo = ("Undefined label: " + address.Label + " !" + " Code: " + Codes[i]);
                            ErrorState = "UndefinedLabel";
                            goto failed;
                        }
                        //初始化一个新Label结构，用于清空结构原有数据
                        label.ParentLabel = CurrentParentLabel;
                        label.Address = CurrentAddress;
                        label.VirtualLabel = false;
                        label.PositionInCodes = i;
                        labels.Add(label);
                        reassemble_args.LabelIndex = labels.Count - 1;
                        //标签位置已确定,对引用标签的代码进行校正
                        if (!Reassemble(Codes,ref labels,ref assembleds, ref CurrentAddress, reassemble_args,ref LastReassemble, ReassembleType.LabelCorrection))
                        {
                            goto failed;
                        }
                        continue;
                    }
                    else
                    {
                        CurrentParentLabel = address.Label;
                    }
                    CurrentAddress = address.Address;
                }
            }
            //汇编指令已处理完毕,检查是否需要重汇编
            if (LastReassemble)
            {
                if (!Reassemble(Codes,ref labels,ref assembleds, ref CurrentAddress, reassemble_args,ref LastReassemble, ReassembleType.LastReassemble))
                    goto failed;
            }
            //将分散的汇编指令以区块为标准合并
            Assembled[] assembledArray = assembleds.ToArray();
            assembledArray = MergeAssembles(assembledArray);
            //开始执行写入内存操作
            for (i = 0;i < assembledArray.Length; ++i)
            {
                if (!Memory.WriteMemoryByteSet(assembledArray[i].CurrentAddress, assembledArray[i].AssembledBytes))
                {
                    ErrorInfo = ("Write process memory error!");
                    ErrorState = "WriteMemoryError";
                    goto failed;
                } 
            }
            //执行创建线程操作
            bool ok = true;
            for(i = 0; i < Threads.Count; ++i)
            {
                if(Memory.CreateThread(GetAddressByLabelName(Threads[i],ref labels)) == null)//线程创建失败不会退出当前函数
                {
                    ErrorInfo = "Create thread failed!";
                    ErrorState = "CreateThreadFailed";
                    ok = false;
                    continue;
                }
            }
            //执行释放内存操作
            List<AllocedMemory> tempAllocs = new List<AllocedMemory>();
            for (i = 0; i < alloceds.Count; i++)
                tempAllocs.Add(alloceds[i]);
            for (i = 0; i < allocs.Count; i++)
                tempAllocs.Add(allocs[i]);
            for (i = 0; i < Deallocs.Count; ++i)
            {
                s = Deallocs[i];
                int index = 0;
                foreach (AllocedMemory temp in tempAllocs)
                {
                    if (temp.AllocName == s)
                    {
                        Memory.FreeMemory(tempAllocs[index]);
                        tempAllocs.RemoveAt(index);
                        break;
                    }
                    index++;
                }
                continue;
            }
            //将脚本注册的全局符号赋值,将分配的内存(alloceds)更新,汇编脚本处理完毕
            alloceds = tempAllocs;
            if (!RegisterSymbols(ref registers,ref labels))
                ok = false;
            return ok;
            failed:
            //释放分配的内存
            for (i = 0; i < allocs.Count; ++i)
            {
                Memory.FreeMemory(allocs[i]);
            }
            return false;
        }
        public struct Reassemble_Args
        {
            public int LabelIndex;
            public int ReferenceIndex;
            public int diff;
            public string ParentLabel;
        }
        private string FindParentLabelByPosition(ref List<Label> labels,string[] codes,int index)
        {
            string s;
            for(int i = index;i > 0; i--)
            {
                if(codes[i][codes[i].Length-1] == ':')
                {
                    s = codes[i].Substring(0, codes[i].IndexOf(":")).Trim();
                    AddressExpression adexp = LabelParse(s, ref labels, 0);
                    if (string.IsNullOrEmpty(adexp.Label))
                        continue;
                    int x = FindLabelIndex(adexp.Label, ref labels);
                    if (string.IsNullOrEmpty(labels[x].ParentLabel))
                    {
                        return labels[x].LabelName;
                    }
                    else
                    {
                        return labels[x].ParentLabel;
                    }
                }
            }
            return null;
        }
        private bool Reassemble(string[] codes,ref List<Label> labels,ref List<Assembled> assembleds, ref long CurrentAddress, Reassemble_Args args,ref bool LastReassemble, ReassembleType type)
        {
            switch (type)
            {
                case ReassembleType.LabelCorrection:
                    goto LabelCorrection;
                case ReassembleType.LastReassemble:
                    goto LastReassemble;
                case ReassembleType.AddressAligned:
                    goto AddressAligned;

            }
            int i, loop;
            Assembled assembled;
            IAssemble data;
        LabelCorrection:
            loop = labels[args.LabelIndex].references.Count;
            Reference reference;
            Label label;
            data = new IAssemble();
            data.cbUnkonwn = ParseCallBack;
            for (i = 0; i < loop; ++i)
            {
                reference = labels[args.LabelIndex].references[i];
                data.Asm = codes[reference.CodeReflect];
                data.CurrentAddress = reference.CurrentAddress;
                if (ICompile(ref data) == Assembler_Status.Assembler_Error)
                {
                    ErrorState = "InvalidCode";
                    ErrorInfo = "Reassemble code failed!Error info:" + data.error;
                    goto Failed;
                }
                if (data.AsmLength != assembleds[reference.AssembledsReflect].AssembledBytes.Length)
                {
                    LastReassemble = true;
                    args.diff = data.AsmLength - assembleds[reference.AssembledsReflect].AssembledBytes.Length;
                    assembled = assembleds[reference.AssembledsReflect];
                    assembled.AssembledBytes = new byte[data.AsmLength];
                    ByteCopy(data.Bytes, assembled.AssembledBytes, data.AsmLength);
                    assembleds.RemoveAt(reference.AssembledsReflect);
                    assembleds.Insert(reference.AssembledsReflect, assembled);
                    args.ReferenceIndex = i;
                    args.ParentLabel = FindParentLabelByPosition(ref labels, codes, labels[args.LabelIndex].references[i].CodeReflect);
                    if (!Reassemble(codes,ref labels,ref assembleds, ref CurrentAddress, args,ref LastReassemble, ReassembleType.AddressAligned))
                    {
                        goto Failed;
                    }
                    continue;
                }
                assembled = assembleds[reference.AssembledsReflect];
                ByteCopy(data.Bytes, assembled.AssembledBytes, data.AsmLength);
                assembleds.RemoveAt(reference.AssembledsReflect);
                assembleds.Insert(reference.AssembledsReflect, assembled);
                continue;
            }
            goto Success;
        LastReassemble:
            data = new IAssemble();
            List<Assembled> TempAssembleds = new List<Assembled>();
            string code;
            loop = codes.Length;
            data.cbUnkonwn = ParseCallBack;
            CurrentAddress = 0;
            for(i = 0; i < loop; ++i)
            {
                code = codes[i];
                if(code[code.Length-1] == ':')
                {
                    string s = code.Substring(0, code.IndexOf(":")).Trim();
                    AddressExpression adrex = LabelParse(s, ref labels, CurrentAddress);
                    if(adrex.Address != 0)
                    {
                        CurrentAddress = adrex.Address;
                    }
                    else
                    {
                        ErrorState = "InvalidLabel";
                        ErrorInfo = "Undefined Label!Code:" + code;
                        goto Failed;
                    }
                }
                else
                {
                    data.Asm = code;
                    data.CurrentAddress = CurrentAddress;
                    if (ICompile(ref data) == Assembler_Status.Assembler_OK)
                    {
                        assembled.AssembledBytes = new byte[data.AsmLength];
                        ByteCopy(data.Bytes, assembled.AssembledBytes, data.AsmLength);
                        assembled.CurrentAddress = CurrentAddress;
                        TempAssembleds.Add(assembled);
                        CurrentAddress += data.AsmLength;
                        continue;
                    }
                    else
                    {
                        ErrorState = "InvalidCode";
                        ErrorInfo = "Reassemble code failed!Error code:" + code+",Error info:"+data.error;
                        goto Failed;
                    }
                }
            }
            assembleds = TempAssembleds;
            goto Success;
        AddressAligned:
            loop = labels.Count;
            long address;
            int AssembledsPosition = labels[args.LabelIndex].references[args.ReferenceIndex].AssembledsReflect;
            int CodePosition = labels[args.LabelIndex].references[args.ReferenceIndex].CodeReflect;
            //先校正标签
            for (i = 0; i < loop; ++i)
            {
                if (labels[i].ParentLabel == args.ParentLabel && labels[i].PositionInCodes > labels[args.LabelIndex].references[args.ReferenceIndex].CodeReflect)
                {
                    label = labels[i];
                    label.Address += args.diff;
                    labels.RemoveAt(i);
                    labels.Insert(i, label);
                    args.LabelIndex = i;
                    if (!Reassemble(codes,ref labels,ref assembleds, ref CurrentAddress, args, ref LastReassemble,ReassembleType.LabelCorrection))
                        goto Failed;
                }
            }
            //其次修正地址
            loop = assembleds.Count;
            i = labels[args.LabelIndex].references[args.ReferenceIndex].AssembledsReflect;
            while (i < loop)
            {
                address = (assembleds[i].CurrentAddress + assembleds[i].AssembledBytes.Length)- args.diff;
                if (i == loop - 1)
                    break;
                if(address == assembleds[i + 1].CurrentAddress)
                {
                    assembled = assembleds[i+1];
                    assembled.CurrentAddress += args.diff;
                    assembleds.RemoveAt(i+1);
                    assembleds.Insert(i+1, assembled);
                }
                ++i;
            }
            goto Success;
        Failed:
            return false;
        Success:
            return true;
        }
        private bool DefineExist(string define,ref List<Define> defines)
        {
            for(int i = 0; i < defines.Count; i++)
            {
                if (define == defines[i].Original)
                    return true;
            }
            return false;
        }
        private string[] ArgsParse(string Expression,char[] Separator)
        {
            int seek, length;
            seek = Expression.IndexOf('(');
            length = Expression.LastIndexOf(')');
            if(seek == -1 || length == -1)
            {
                ErrorState = "InvalidCode";
                ErrorInfo = "Invalid expression: " + Expression + " !";
                return null;
            }
            length = length - seek - 1;
            string temp = Expression.Substring(seek + 1, length);
            string[] args = temp.Split(Separator);
            return args;
        }
        public struct IAssemble
        {
            public bool x64; //逻辑值，0为假1为真
            public Int64 CurrentAddress;//汇编指令当前地址(用于运算jmp和call等)
            public Int16 AsmLength;//指令字节集长度
            public CBXEDPARSE_UNKNOWN cbUnkonwn;//无法识别的指令
            public byte[] Bytes;//汇编指令字节集
            public string Asm;//汇编指令
            public string error;//错误字符
        }
        private enum DataType
        {
            Byte,
            Word,
            Dword,
            Qword
        }
        private byte[] DataParse(string DataValue, DataType type)
        {
            int Pre, Cur, DataIndex;
            string Instr = DataValue;
            DataValue = DataValue.Substring(3, DataValue.Length - 3);
            var ConvertedVar = 0L;
            byte[] Tempbuf, RetVar;
            Pre = Cur = 0;
            DataIndex = 0;
            while (Cur < DataValue.Length)
            {
                switch (DataValue[Cur])
                {
                    case ' ':
                    case ',':
                        if (Cur - Pre == 0)
                        {
                            Pre = Cur;
                            continue;
                        }
                        else
                        {
                            try
                            {
                                string text;
                                text = DataValue.Substring(Pre, Cur - Pre);
                                switch (type)
                                {
                                    case DataType.Byte:
                                        ConvertedVar = Convert.ToByte(text, 16);
                                        DataBuf[DataIndex] = (byte)ConvertedVar;
                                        DataIndex++;
                                        break;
                                    case DataType.Word:
                                        ConvertedVar = Convert.ToInt16(text, 16);
                                        Tempbuf = BitConverter.GetBytes((Int16)ConvertedVar);
                                        DataBuf[DataIndex] = Tempbuf[0]; DataBuf[DataIndex + 1] = Tempbuf[1];
                                        DataIndex += 2;
                                        break;
                                    case DataType.Dword:
                                        if (IsFloatVar(text))
                                        {
                                            Tempbuf = BitConverter.GetBytes(Convert.ToSingle(text));
                                            DataBuf[DataIndex] = Tempbuf[0]; DataBuf[DataIndex + 1] = Tempbuf[1]; DataBuf[DataIndex + 2] = Tempbuf[2]; DataBuf[DataIndex + 3] = Tempbuf[3];
                                            DataIndex += 4;
                                            break;
                                        }
                                        ConvertedVar = Convert.ToInt32(text, 16);
                                        Tempbuf = BitConverter.GetBytes((Int32)ConvertedVar);
                                        DataBuf[DataIndex] = Tempbuf[0]; DataBuf[DataIndex + 1] = Tempbuf[1]; DataBuf[DataIndex + 2] = Tempbuf[2]; DataBuf[DataIndex + 3] = Tempbuf[3];
                                        DataIndex += 4;
                                        break;
                                    case DataType.Qword:
                                        if (IsFloatVar(text))
                                        {
                                            Tempbuf = BitConverter.GetBytes(Convert.ToDouble(text));
                                            DataBuf[DataIndex] = Tempbuf[0]; DataBuf[DataIndex + 1] = Tempbuf[1]; DataBuf[DataIndex + 2] = Tempbuf[2]; DataBuf[DataIndex + 3] = Tempbuf[3]; DataBuf[DataIndex + 4] = Tempbuf[4]; DataBuf[DataIndex + 5] = Tempbuf[5]; DataBuf[DataIndex + 6] = Tempbuf[6]; DataBuf[DataIndex + 7] = Tempbuf[7];
                                            DataIndex += 8;
                                            break;
                                        }
                                        ConvertedVar = Convert.ToInt64(text, 16);
                                        Tempbuf = BitConverter.GetBytes((Int64)ConvertedVar);
                                        DataBuf[DataIndex] = Tempbuf[0]; DataBuf[DataIndex + 1] = Tempbuf[1]; DataBuf[DataIndex + 2] = Tempbuf[2]; DataBuf[DataIndex + 3] = Tempbuf[3]; DataBuf[DataIndex + 4] = Tempbuf[4]; DataBuf[DataIndex + 5] = Tempbuf[5]; DataBuf[DataIndex + 6] = Tempbuf[6]; DataBuf[DataIndex + 7] = Tempbuf[7];
                                        DataIndex += 8;
                                        break;
                                }
                                Pre = Cur + 1;
                                Cur++;
                                continue;
                            }
                            catch (FormatException)
                            {
                                ErrorState = "NumberFormatException";
                                ErrorInfo = string.Format("Invalid number:{0},In code:\r\n{1}", DataValue.Substring(Pre, Cur - Pre), Instr);
                                return null;
                            }
                            catch (OverflowException)
                            {
                                ErrorState = "NumberOverflow";
                                ErrorInfo = string.Format("Number overflow: {0},In code:\r\n{1}", DataValue.Substring(Pre, Cur - Pre), Instr);
                                return null;
                            }
                        }
                    case '\'':
                    case '"':
                        bool ended = false;
                        Cur++;
                        StringBuilder builder = new StringBuilder();
                        while (Cur < DataValue.Length)
                        {
                            if (ended)
                                break;
                            switch (DataValue[Cur])
                            {
                                case '\''://字串结尾
                                case '"':
                                    switch (type)
                                    {
                                        case DataType.Byte:
                                            Tempbuf = Encoding.ASCII.GetBytes(builder.ToString());
                                            Array.Copy(Tempbuf, 0, DataBuf, DataIndex, Tempbuf.Length);
                                            DataIndex += Tempbuf.Length;
                                            break;
                                        case DataType.Word:
                                            Tempbuf = Encoding.Unicode.GetBytes(builder.ToString());
                                            Array.Copy(Tempbuf, 0, DataBuf, DataIndex, Tempbuf.Length);
                                            DataIndex += Tempbuf.Length;
                                            break;
                                        default:
                                            ErrorState = "SyntaxError";
                                            ErrorInfo = string.Format("Define data instruction only support ASCII and Unicode!Please use db or dw to define string!In code:\r\n{0}", Instr);
                                            return null;
                                    }
                                    while (DataValue[Cur] != ' ' && DataValue[Cur] != ',')
                                    {
                                        Cur++;
                                    }
                                    Cur++;
                                    Pre = Cur;
                                    ended = true;
                                    break;
                                case '\\'://转义符
                                    switch (DataValue[Cur + 1])
                                    {
                                        case 'T':
                                        case 't':
                                            builder.Append('\t');
                                            break;
                                        case 'R':
                                        case 'r':
                                            builder.Append('\r');
                                            break;
                                        case 'N':
                                        case 'n':
                                            builder.Append('\n');
                                            break;
                                        default:
                                            builder.Append(DataValue[Cur + 1]);
                                            break;
                                    }
                                    Cur += 2;
                                    break;
                                default:
                                    builder.Append(DataValue[Cur]);
                                    Cur++;
                                    continue;
                            }
                        }
                        if (ended)
                        {
                            continue;
                        }
                        else
                        {
                            ErrorState = "SyntaxError";
                            ErrorInfo = string.Format("Cannot find String ending sign \" or \'!In code:\r\n{0}", Instr);
                            return null;
                        }
                    default:
                        Cur++;
                        continue;
                }
            }
            if (Cur - Pre == 0)
            {
                RetVar = new byte[DataIndex];
                Array.Copy(DataBuf, RetVar, DataIndex);
                return RetVar;
            }
            else
            {
                try
                {
                    switch (type)
                    {
                        case DataType.Byte:
                            ConvertedVar = Convert.ToByte(DataValue.Substring(Pre, Cur - Pre), 16);
                            DataBuf[DataIndex] = (byte)ConvertedVar;
                            DataIndex++;
                            break;
                        case DataType.Word:
                            ConvertedVar = Convert.ToInt16(DataValue.Substring(Pre, Cur - Pre), 16);
                            Tempbuf = BitConverter.GetBytes((Int16)ConvertedVar);
                            DataBuf[DataIndex] = Tempbuf[0]; DataBuf[DataIndex + 1] = Tempbuf[1];
                            DataIndex += 2;
                            break;
                        case DataType.Dword:
                            if (IsFloatVar(DataValue.Substring(Pre, Cur - Pre)))
                            {
                                Tempbuf = BitConverter.GetBytes(Convert.ToSingle(DataValue.Substring(Pre, Cur - Pre)));
                                DataBuf[DataIndex] = Tempbuf[0]; DataBuf[DataIndex + 1] = Tempbuf[1]; DataBuf[DataIndex + 2] = Tempbuf[2]; DataBuf[DataIndex + 3] = Tempbuf[3];
                                DataIndex += 4;
                                break;
                            }
                            ConvertedVar = Convert.ToInt32(DataValue.Substring(Pre, Cur - Pre), 16);
                            Tempbuf = BitConverter.GetBytes((Int32)ConvertedVar);
                            DataBuf[DataIndex] = Tempbuf[0]; DataBuf[DataIndex + 1] = Tempbuf[1]; DataBuf[DataIndex + 2] = Tempbuf[2]; DataBuf[DataIndex + 3] = Tempbuf[3];
                            DataIndex += 4;
                            break;
                        case DataType.Qword:
                            if (IsFloatVar(DataValue.Substring(Pre, Cur - Pre)))
                            {
                                Tempbuf = BitConverter.GetBytes(Convert.ToDouble(DataValue.Substring(Pre, Cur - Pre)));
                                DataBuf[DataIndex] = Tempbuf[0]; DataBuf[DataIndex + 1] = Tempbuf[1]; DataBuf[DataIndex + 2] = Tempbuf[2]; DataBuf[DataIndex + 3] = Tempbuf[3]; DataBuf[DataIndex + 4] = Tempbuf[4]; DataBuf[DataIndex + 5] = Tempbuf[5]; DataBuf[DataIndex + 6] = Tempbuf[6]; DataBuf[DataIndex + 7] = Tempbuf[7];
                                DataIndex += 8;
                                break;
                            }
                            ConvertedVar = Convert.ToInt64(DataValue.Substring(Pre, Cur - Pre), 16);
                            Tempbuf = BitConverter.GetBytes((Int64)ConvertedVar);
                            DataBuf[DataIndex] = Tempbuf[0]; DataBuf[DataIndex + 1] = Tempbuf[1]; DataBuf[DataIndex + 2] = Tempbuf[2]; DataBuf[DataIndex + 3] = Tempbuf[3]; DataBuf[DataIndex + 4] = Tempbuf[4]; DataBuf[DataIndex + 5] = Tempbuf[5]; DataBuf[DataIndex + 6] = Tempbuf[6]; DataBuf[DataIndex + 7] = Tempbuf[7];
                            break;
                    }
                }
                catch (FormatException)
                {
                    ErrorState = "NumberFormatException";
                    ErrorInfo = string.Format("Invalid value:{0},In code:\r\n{1}", DataValue.Substring(Pre, Cur - Pre), Instr);
                    return null;
                }
                catch (OverflowException)
                {
                    ErrorState = "NumberOverflow";
                    ErrorInfo = string.Format("Number overflow: {0},In code:\r\n{1}", DataValue.Substring(Pre, Cur - Pre), Instr);
                    return null;
                }
                RetVar = new byte[DataIndex];
                Array.Copy(DataBuf, RetVar, DataIndex);
                return RetVar;
            }
        }
        private Assembler_Status ICompile(ref IAssemble assembledata)
        {
            string InstrPrefix = assembledata.Asm.ToUpper();
            string s;
            int seek, size;
            int idata;
            if(InstrPrefix.Length > 4)
            {
                byte[] temp;
                if(InstrPrefix[0] == 'D' && InstrPrefix[2] == ' ')
                {
                    switch (InstrPrefix[1])
                    {
                        case 'B':
                            temp = DataParse(assembledata.Asm, DataType.Byte);
                            break;
                        case 'W':
                            temp = DataParse(assembledata.Asm, DataType.Word);
                            break;
                        case 'D':
                            temp = DataParse(assembledata.Asm, DataType.Dword);
                            break;
                        case 'Q':
                            temp = DataParse(assembledata.Asm, DataType.Qword);
                            break;
                        default:
                            goto Decode;
                    }
                    if (temp == null)
                    {
                        return Assembler_Status.Assembler_Error;
                    }
                    else
                    {
                        assembledata.Bytes = temp;
                        assembledata.AsmLength = (short)temp.Length;
                        return Assembler_Status.Assembler_OK;
                    }
                }
                if (Substring(InstrPrefix, 0, 4) == "NOP ")
                {
                    s = InstrPrefix.Substring(4, InstrPrefix.Length - 4);
                    try
                    {
                        idata = StrToInt_16(s);
                    }
                    catch (FormatException)
                    {
                        ErrorInfo = ("Is not a valid integer!" + " Code: " + assembledata.Asm);
                        ErrorState = "InvalidValue";
                        return Assembler_Status.Assembler_Error;
                    }
                    assembledata.Bytes = new byte[idata];
                    assembledata.AsmLength = (short)idata;
                    for (int i = 0; i < idata; ++i)
                    {
                        assembledata.Bytes[i] = 0x90;
                    }
                    return Assembler_Status.Assembler_OK;
                }
            }
          Decode:
            Assembler_Data data = new Assembler_Data()
            {
                Asm = assembledata.Asm,
                cbUnkonwn = assembledata.cbUnkonwn,
                CurrentAddress = assembledata.CurrentAddress,
                x64 = Memory.is64bit
            };
            if(Assemble(ref data) == Assembler_Status.Assembler_OK)
            {
                assembledata.Bytes = data.Bytes;
                assembledata.AsmLength = data.AsmLength;
                assembledata.error = data.error;
                return Assembler_Status.Assembler_OK;
            }
            else
            {
                assembledata.Bytes = data.Bytes;
                assembledata.AsmLength = data.AsmLength;
                assembledata.error = data.error;
                ErrorInfo = string.Format("Unknown asm instruction:{0}\terror:\r\n{1}", assembledata.Asm, assembledata.error);
                ErrorState = "UnknownAsmInstruction";
                return Assembler_Status.Assembler_Error;
            }
        }
        private bool RemoveLabelByName(string name,ref List<Label> labels)
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
        private void ByteCopy(byte[] src, byte[] dest, int start, int Lenth)
        {
            int accumulate = 0;
            for (int i = start; accumulate < Lenth; ++i)
            {
                dest[i] = src[accumulate];
                ++accumulate;
            }
        }
        public long StrToLong_10(string s)
        {
            string number;
            if (s[0] == '$')
            {
                number = Substring(s, 1, s.Length - 1);
                return Convert.ToInt64(number,16);
            }else if (s[0] == '#')
            {
                number = Substring(s, 1, s.Length - 1);
                return Convert.ToInt64(number);
            }
            return Convert.ToInt64(s);
        }
        public int StrToInt_10(string s)
        {
            string number;
            if (s[0] == '$')
            {
                number = Substring(s, 1, s.Length - 1);
                return Convert.ToInt32(number,16);
            }
            else if (s[0] == '#')
            {
                number = Substring(s, 1, s.Length - 1);
                return Convert.ToInt32(number);
            }
            return Convert.ToInt32(s);
        }
        public long StrToLong_16(string s)
        {
            string number;
            if (s[0] == '#')
            {
                number = Substring(s, 1, s.Length - 1);
                return Convert.ToInt64(number);
            }
            else if (s[0] == '$')
            {
                number = Substring(s, 1, s.Length - 1);
                return Convert.ToInt64(number,16);
            }
            return Convert.ToInt64(s,16);
        }
        public int StrToInt_16(string s)
        {
            string number;
            if(s[0] == '#')
            {
                number = Substring(s, 1, s.Length - 1);
                return Convert.ToInt32(number);
            }
            else if (s[0] == '$')
            {
                number = Substring(s, 1, s.Length - 1);
                return Convert.ToInt32(number, 16);
            }
            return Convert.ToInt32(s,16);
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
                MemoryAPI.Number[] numbers = MemoryAPI.OperationParse(SplitExps[i]);
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
                        if (IsHexadecimal(numbers[x].Value))
                        {
                            temp = StrToLong_16(numbers[x].Value);
                        }
                        else
                        {
                            temp = Memory.GetModuleBaseaddress(numbers[x].Value);
                            if (temp == 0)
                            {
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
                if (i == SplitExps.Length - 1)
                {
                    if (Expression[Expression.Length - 1] == ']')
                    {
                        if (!Memory.ReadMemoryInt64(Address, ref Address))
                        {
                            return 0;
                        }
                        return Address;
                    }
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
        public string GetAddressTest(string Expression)
        {
            string RetExpression = Expression + "\r\n->";
            string TempStr = Expression;
            Expression = Expression.Replace("[", "");
            char[] a = { ']' };
            string[] SplitExps = Expression.Split(a, StringSplitOptions.RemoveEmptyEntries);
            long Address = 0;
            long temp = 0;
            for (int i = 0; i < SplitExps.Length; i++)
            {
                MemoryAPI.Number[] numbers = MemoryAPI.OperationParse(SplitExps[i]);
                int x = 0;
                while (x < numbers.Length)
                {
                    temp = 0;
                    //寻找全局符号数组
                    foreach (RegisterSymbol symbol in RegisteredSymbols)
                    {
                        if (symbol.SymbolName == numbers[x].Value)
                        {
                            TempStr = TempStr.Replace(symbol.SymbolName, symbol.Address.ToString("X"));
                            temp = symbol.Address;
                            break;
                        }
                    }
                    if (temp == 0)
                    {
                        if (IsHexadecimal(numbers[x].Value))
                        {
                            temp = StrToLong_16(numbers[x].Value);
                        }
                        else
                        {
                            temp = Memory.GetModuleBaseaddress(numbers[x].Value);
                            if (temp == 0)
                            {
                                return "表达式错误!无法识别的符号:" + numbers[x].Value;
                            }
                            else
                            {
                                TempStr = TempStr.Replace(numbers[x].Value, temp.ToString("X"));
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
                    if (Expression[Expression.Length - 1] == ']')
                    {
                        if (!Memory.ReadMemoryInt64(Address, ref Address))
                        {
                            return "表达式错误:指向一个无效的内存地址!";
                        }
                    }
                    RetExpression += TempStr;
                    RetExpression += "\r\n->" + Address.ToString("X");
                    return RetExpression;
                }
                if (Memory.is64bit)
                {
                    if (!Memory.ReadMemoryInt64(Address, ref Address))
                    {
                        return "表达式错误:指向一个无效的内存地址!!";
                    }
                    continue;
                }
                else
                {
                    int tAddress = (int)Address;
                    if (!Memory.ReadMemoryInt32(Address, ref tAddress))
                    {
                        return "表达式错误:指向一个无效的内存地址!!";
                    }
                    Address = tAddress;
                }
            }
            RetExpression += TempStr;
            RetExpression += "\r\n->" + Address.ToString("X");
            return RetExpression;
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
        private bool AobScans(AobScan_args[] aobs, ref List<Label> labels)
        {
            bool Odd = false;
            Label MainLabel;
            long MainResult;
            string error = "";
            if(aobs.Length == 1)
            {
                if(aobs[0].Module == null)
                {
                    MainResult = Memory.AobScan(aobs[0].AobString);
                    goto Result;
                }
                MainResult = Memory.AobScanModule(aobs[0].Module, aobs[0].AobString,out error);
            Result:
                if (MainResult == 0)
                {
                    ErrorState = "AOBScanFailed";
                    ErrorInfo = string.Format("AOBScanFailed!In code:{0}\r\n{1}",aobs[0].OriginCode, error);
                    return false;
                }
                MainLabel = new Label()
                {
                    VirtualLabel = false,
                    LabelName = aobs[0].LabelName,
                    Address = MainResult
                };
                labels.Add(MainLabel);
                return true;
            }
            if (aobs.Length % 2 != 0)
                Odd = true;
            int CurrentIndex = 0;
            int TaskIndex = aobs.Length / 2;
            int MainMaxIndex = TaskIndex - 1;
            long TaskResult;
            Label TaskLabel;
            while (CurrentIndex <= MainMaxIndex)
            {
                Task<long> task;
                if (aobs[TaskIndex].Module == null)
                {
                    task = new Task<long>(() => Memory.AobScan(aobs[TaskIndex].AobString));
                }
                else
                {
                    task = new Task<long>(() => Memory.AobScanModule(aobs[TaskIndex].Module, aobs[TaskIndex].AobString, out error));
                }
                task.Start();
                if (aobs[CurrentIndex].Module == null)
                {
                    MainResult = Memory.AobScan(aobs[0].AobString);
                    goto Result;
                }
                MainResult = Memory.AobScanModule(aobs[CurrentIndex].Module, aobs[CurrentIndex].AobString, out error);
            Result:
                if (MainResult == 0)
                {
                    ErrorState = "AOBScanFailed";
                    ErrorInfo = string.Format("AOBScanFailed!In code:{0}\r\n{1}", aobs[0].OriginCode, error);
                    return false;
                }
                while (!task.IsCompleted)
                {
                    Thread.Sleep(1);
                }
                TaskResult = task.Result;
                if (TaskResult == 0)
                {
                    ErrorState = "AOBScanFailed";
                    ErrorInfo = string.Format("AOBScanFailed!In code:{0}\r\n{1}", aobs[0].OriginCode, error);
                    return false;
                }
                MainLabel = new Label()
                {
                    VirtualLabel = false,
                    LabelName = aobs[CurrentIndex].LabelName,
                    Address = MainResult
                };
                TaskLabel = new Label()
                {
                    VirtualLabel = false,
                    LabelName = aobs[TaskIndex].LabelName,
                    Address = TaskResult
                };
                labels.Add(MainLabel);
                labels.Add(TaskLabel);
                CurrentIndex++;
                TaskIndex++;
            }
            if (Odd)
            {
                MainMaxIndex = aobs.Length - 1;
                if(aobs[MainMaxIndex].Module == null)
                {
                    MainResult = Memory.AobScan(aobs[MainMaxIndex].AobString);
                    goto Result;
                }
                MainResult = Memory.AobScanModule(aobs[MainMaxIndex].Module, aobs[MainMaxIndex].AobString, out error);
            Result:
                if (MainResult == 0)
                {
                    ErrorState = "AOBScanFailed";
                    ErrorInfo = string.Format("AOBScanFailed!In code:{0}\r\n{1}", aobs[0].OriginCode, error);
                    return false;
                }
                MainLabel = new Label()
                {
                    VirtualLabel = false,
                    LabelName = aobs[MainMaxIndex].LabelName,
                    Address = MainResult
                };
                labels.Add(MainLabel);
            }
            return true;
        }
        private bool RegisterSymbols(ref List<string> registerSymbols,ref List<Label> labels)
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
                    ErrorInfo = "Cannot find symbol " + registerSymbols[i] + " in this script!";
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
        private bool ParseCallBack(string text,ref Int64 retvalue)
        {
            if (SymbolDecoder.SymbolParse(text, out retvalue))
                return true;
            else
                return false;
        }
        public static bool IsHexadecimal(string str)
        {
            if (str[0] == '$' || str[0] == '#')
            {
                str = str.Remove(0, 1);
            }
            foreach (char word in str)
            {
                if ((word >= 0x30 && word <= 0x39) ||
                    (word >= 0x41 && word <= 0x46) ||
                    (word >= 0x61 && word <= 0x66))
                {
                    continue;
                }
                if (word == '+' || word == '-')
                    continue;
                return false;
            }
            return true;
        }
        public static bool IsFloatVar(string str)
        {
            bool Dot = false;
            char word;
            for (int i = 0; i < str.Length; i++)
            {
                word = str[i];
                if (word >= 0x30 && word <= 0x39)
                    continue;
                if (word == '.')
                {
                    if (Dot)
                    {
                        return false;
                    }
                    else
                    {
                        Dot = true;
                        continue;
                    }
                }
                if (word == 'f' || word == 'F')
                {
                    if (str.Length - 1 == i)
                        return true;
                    return false;
                }
                if (word == '+' || word == '-')
                    continue;
                return false;
            }
            return Dot;
        }
        private bool SymbolParse(string exp, out long value)
        {
            long reserved = 0, CurrentAddress = 0;
            MemoryAPI.Number[] numbers = MemoryAPI.OperationParse(exp);
            string tempstr = numbers[0].Value;
            if (numbers.Length == 1)
            {
                if (IsFloatVar(tempstr))
                {
                    if (tempstr[tempstr.Length - 1] == 'f' || tempstr[tempstr.Length - 1] == 'F')
                    {
                        float singlef = Convert.ToSingle(tempstr.Substring(0, tempstr.Length - 1));
                        value = BitConverter.ToInt32(BitConverter.GetBytes(singlef), 0);
                        return true;
                    }
                    double doublef = Convert.ToDouble(tempstr.Substring(0, tempstr.Length - 1));
                    value = BitConverter.ToInt64(BitConverter.GetBytes(doublef), 0);
                    return true;
                }
                else
                {
                    goto label;
                }
            }
            for (int i = 0; i < numbers.Length; ++i)
            {
                long temp = 0;
                reserved = CurrentAddress;
                CurrentAddress = GetAddressBySymbolName(numbers[i].Value, ref SymbolsPointer);
                if (CurrentAddress == 0)
                {
                    if (IsHexadecimal(numbers[i].Value))
                    {
                        CurrentAddress = reserved;
                        temp = StrToLong_16(numbers[i].Value);
                    }
                    else
                    {
                        temp = Memory.GetModuleBaseaddress(numbers[i].Value);
                        if (temp == 0)
                        {
                            value = 0;
                            return false;
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
            value = CurrentAddress;
            return true;
        //不涉及到多重运算
        label:
            CurrentAddress = GetAddressBySymbolName(numbers[0].Value, ref SymbolsPointer);
            if (CurrentAddress == 0)
            {
                if (IsHexadecimal(numbers[0].Value))
                {
                    CurrentAddress = reserved;
                    CurrentAddress = StrToLong_16(numbers[0].Value);
                }
                else
                {
                    CurrentAddress = Memory.GetModuleBaseaddress(numbers[0].Value);
                    if (CurrentAddress == 0)
                    {
                        value = 0;
                        return false;
                    }
                }
            }
            value = CurrentAddress;
            return true;
        }
        private AddressExpression LabelParse(string expression,ref List<Label> labels, long CurrentAddress)
        {
            long reserved = CurrentAddress;
            AddressExpression address = new AddressExpression
            {
                isVirtualLabel = false
            };
            AddressExpression tempAdexp;
            MemoryAPI.Number[] numbers = MemoryAPI.OperationParse(expression);
            if (numbers.Length == 1) goto label;
            for (int i = 0; i < numbers.Length; ++i)
            {
                long temp = 0;
                reserved = CurrentAddress;
                tempAdexp = GetLabelInfo(numbers[i].Value, ref labels);
                if (!tempAdexp.isLabel)
                {
                    if (tempAdexp.isVirtualLabel)
                    {
                        address.Label = numbers[i].Value;
                        address.Multiple = true;
                        address.isVirtualLabel = true;
                        return address;
                    }
                    if (IsHexadecimal(numbers[i].Value))
                    {
                        CurrentAddress = reserved;
                        temp = StrToLong_16(numbers[i].Value);
                    }
                    else
                    {
                        CurrentAddress = Memory.GetModuleBaseaddress(numbers[i].Value);
                        if (CurrentAddress == 0)
                        {
                            address.Label = numbers[i].Value;
                            address.isVirtualLabel = true;
                            address.Multiple = true;
                            return address;
                        }
                    }
                }
                else
                {
                    address.Label = numbers[i].Value;
                    CurrentAddress = tempAdexp.Address;
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
            address.Address = CurrentAddress;
            address.Multiple = true;
            return address;
        //不涉及到多重运算
        label:
            tempAdexp = GetLabelInfo(numbers[0].Value,ref labels);
            if (!tempAdexp.isLabel)
            {
                if (tempAdexp.isVirtualLabel)
                {
                    address.Label = numbers[0].Value;
                    address.isVirtualLabel = true;
                    return address;
                }
                if (IsHexadecimal(numbers[0].Value))
                {
                    CurrentAddress = reserved;
                    CurrentAddress = StrToLong_16(numbers[0].Value);
                }
                else
                {
                    CurrentAddress = Memory.GetModuleBaseaddress(numbers[0].Value);
                    if (CurrentAddress == 0)
                    {
                        address.Label = numbers[0].Value;
                        address.isVirtualLabel = true;
                        return address;
                    }
                }
            }
            else
            {
                CurrentAddress = tempAdexp.Address;
            }
            address.Label = numbers[0].Value;
            address.isVirtualLabel = false;
            address.Multiple = false;
            address.Address = CurrentAddress;
            return address;
        }
        private bool GuessVirtualLabel(ref List<Label> labels,ref string[] codes)
        {
            int loop = labels.Count;
            int loop2 = codes.Length;
            long temp;
            Label label = new Label();
            for(int i = 0; i < loop; ++i)
            {
                if (!labels[i].VirtualLabel)
                    continue;
                long LabelParentAddress = 0;
                for (int j = 0; j < loop2; ++j)
                {
                    if(codes[j][codes[j].Length-1] != ':')
                    {
                        continue;
                    }
                    if (labels[i].LabelName + ':' == codes[j])
                    {
                        if (LabelParentAddress == 0)
                        {
                            ErrorInfo = "Virtual label: " + labels[i].LabelName + " haven't valid parent label!";
                            return false;
                        }
                        label = labels[i];
                        label.GuessAddress = LabelParentAddress;
                        labels.RemoveAt(i);
                        labels.Insert(i, label);
                        break;
                    }
                    else
                    {
                        temp = LabelParse(Substring(codes[j], 0, codes[j].Length - 1), ref labels, 0).Address;
                        if (temp == 0)
                            continue;
                        LabelParentAddress = temp;
                        continue;
                    }
                }
            }
            return true;
        }
        struct AddressExpression
        {
            public long Address;
            public string Label;
            public bool isLabel;
            public bool isVirtualLabel;
            public bool Multiple;
        }
        private Assembled[] MergeAssembles(Assembled[] assembleds)
        {
            Assembled assembled;
            List<Assembled> assembledBlocks = new List<Assembled>();
            int BlockSize = 0;
            int loop = assembleds.Length;
            int Position = 0, Start;
            long cip;
            int i, j;
            while (Position < loop)
            {
                int count = 0;
                BlockSize = 0;
                Start = Position;
                for (i = Position; i < loop; ++i)
                {
                    cip = assembleds[i].CurrentAddress + assembleds[i].AssembledBytes.Length;
                    if (Position + 1 == loop)
                    {
                        BlockSize += assembleds[i].AssembledBytes.Length;
                        Position++;
                        count++;
                        break;
                    }
                    if (cip == assembleds[i + 1].CurrentAddress)
                    {
                        BlockSize += assembleds[i].AssembledBytes.Length;
                        Position++;
                        count++;
                        continue;
                    }
                    else if (Position == loop)
                    {
                        break;
                    }
                    else
                    {
                        BlockSize += assembleds[i].AssembledBytes.Length;
                        Position++;
                        count++;
                        break;
                    }
                }
                assembled = new Assembled()
                {
                    AssembledBytes = new byte[BlockSize],
                    CurrentAddress = assembleds[Start].CurrentAddress
                };
                int accumulate = 0;
                i = Start;
                for (j = 0; j < count; ++j)
                {
                    ByteCopy(assembleds[i].AssembledBytes, assembled.AssembledBytes, accumulate, assembleds[i].AssembledBytes.Length);
                    accumulate += assembleds[i].AssembledBytes.Length;
                    ++i;
                }
                assembledBlocks.Add(assembled);
            }
            return assembledBlocks.ToArray();
        }
        private int FindLabelIndex(string name,ref List<Label> labels)
        {
            int count = labels.Count;
            for (int i = 0; i < count; ++i)
            {
                if(labels[i].LabelName == name)
                {
                    return i;
                }
            }
            return -1;
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
        public bool FreeAllocMemory(string AllocName,List<AllocedMemory> alloceds)
        {
            for (int i = 0; i < alloceds.Count; i++)
            {
                if (AllocName == alloceds[i].AllocName)
                {
                    bool ok = MemoryAPI.VirtualFreeEx(Memory.ProcessHandle, alloceds[i].Address, alloceds[i].Size,MEM_DECOMMIT);
                    alloceds.RemoveAt(i);
                    return ok;
                }
            }
            return false;
        }
        private AddressExpression GetLabelInfo(string LabelName, ref List<Label> Labels)
        {
            AddressExpression address = new AddressExpression();
            foreach(Label label in Labels)
            {
                if (LabelName == label.LabelName)
                {
                    if (label.VirtualLabel)
                    {
                        address.isLabel = false;
                        address.isVirtualLabel = true;
                        address.Address = label.GuessAddress;
                    }
                    else
                    {
                        address.isLabel = true;
                        address.isVirtualLabel = false;
                        address.Address = label.Address;
                    }
                    return address;
                }
            }
            address.isLabel = false;
            return address;
        }
        private long GetAddressByLabelName(string LabelName,ref List<Label> Labels)
        {
            int x = Labels.Count;
            for(int i = 0; i < x; ++i)
            {
                if(LabelName == Labels[i].LabelName && Labels[i].VirtualLabel != true)
                {
                    return Labels[i].Address;
                }
            }
            return 0;
        }
        private long GetAddressBySymbolName(string SymbolName, ref List<Label> Labels)
        {
            int x = Labels.Count;
            for (int i = 0; i < x; ++i)
            {
                if (SymbolName == Labels[i].LabelName)
                {
                    if (Labels[i].VirtualLabel)
                    {
                        Label label = Labels[i];
                        Reference reference = new Reference()
                        {
                            CurrentAddress = CurAddress,
                            CodeReflect = (short)CodeNumber,
                            AssembledsReflect = (short)AssembledsNumber
                        };
                        label.references.Add(reference);
                        return Labels[i].GuessAddress + 0x7f;
                    }
                    return Labels[i].Address;
                }
            }
            return 0;
        }
        private bool LabelExist(string Labelname,ref List<Label> Labels)
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
