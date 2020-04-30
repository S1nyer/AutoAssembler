using System;
using System.Text;

namespace Example
{
    class Program
    {
        static void Main(string[] args)
        {
            AutoAssembler.AutoAssembler Assembler = new AutoAssembler.AutoAssembler();
            AutoAssembler.MemoryAPI MemoryAPI = new AutoAssembler.MemoryAPI();
            //OpenProcess的作用是获取程序句柄和程序的模块信息
            //The role of OpenProcess function is get the process's handle and get process module info
            if (!Assembler.OpenProcess("explorer", true))
            {
                Console.WriteLine(AutoAssembler.Var.AutoAssemble_Error);
                Console.ReadKey();
                return;
            }
            Console.WriteLine("OpenProcess successed!Press anykey to run script which below...");
            Console.ReadKey(true);
            //这个脚本用到了大部分的自动汇编引擎命令
            //This script used almost all AutoAssembler Commands that support
            string AAScript = "[enable]\r\naobscanmodule(INJECT,Explorer.EXE,48 B9 FF FF FF FF FF FF 00 00) // should be unique\r\nalloc(ThreadMemory,256)\r\nalloc(newmem,1000,Explorer.exe)\r\nlabel(code)\r\nlabel(return)\r\nnewmem:\r\ncode:\r\n  mov rcx,0000FFFFFFFFFFFF\r\n  nop 9\r\n  jmp return\r\nINJECT:\r\n  jmp newmem\r\n  nop 5\r\nreturn:\r\nThreadMemory:\r\nmov rax,12345678\r\npush rax\r\nsub rax,rax\r\npop rax\r\nret\r\nThreadMemory + 100:\r\ndb 00 00 00 80\r\nCreateThread(ThreadMemory)\r\nregistersymbol(INJECT)\r\nregistersymbol(ThreadMemory)\r\n[DISABLE]\r\nINJECT:\r\n  db 48 B9 FF FF FF FF FF FF 00 00\r\nunregistersymbol(INJECT)\r\nunregistersymbol(ThreadMemory)\r\ndealloc(newmem)\r\ndealloc(ThreadMemory)";
            //现在启用这个脚本
            //now enable this script
            if (!Assembler.AutoAssemble(AAScript, true)) 
            {
                Console.WriteLine(AutoAssembler.Var.AutoAssemble_Error);
                Console.ReadKey();
                return;
            };
            //获取符号ThreadMemroy和INJECT的地址。注意：符号名大小写敏感！
            //Get registered symbol ThreadMemory and INJECT address.Warning:Case sensitive!
            long ThreadMemory = MemoryAPI.GetAddress("ThreadMemory+100");
            int temp = 0;
            MemoryAPI.ReadMemoryInt32(ThreadMemory, ref temp);
            Console.WriteLine("Address:{0},Value:{1}", ThreadMemory,temp.ToString("x"));
            long inject = MemoryAPI.GetAddress("INJECT");
            Console.WriteLine("INJECT's Address:{0}\nInput word you want write to ThreadMemory's address:", inject.ToString("x"));
            string s = Console.ReadLine();
            MemoryAPI.WriteMemoryString(ThreadMemory, s,Encoding.Unicode);
            //ReadMemoryString 函数的参数length表示要读取的字符的长度，若参数值为0则自动读取到字符串的00结尾，但自动读取的字符串不能超过1024个字节，否则将读取不完整！
            //WriteMemoryString function parameter length express the length you wanna read,if length = 0,then auto read to string 0 end.but string cannot more than 1024 byte, otherwise cannot read completely!
            s = MemoryAPI.ReadMemoryString(ThreadMemory, 0, Encoding.Unicode);
            Console.WriteLine("ThreadMemory's address:{0},content:{2}",ThreadMemory.ToString("x"), s);
            Console.WriteLine("script execute over,press anykey to disable script...");
            Console.ReadKey(true);
            if (!Assembler.AutoAssemble(AAScript, false))
            {
                Console.WriteLine(AutoAssembler.Var.AutoAssemble_Error);
                Console.ReadKey();
                return;
            };
        }
    }
}
