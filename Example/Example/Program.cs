﻿using System;
using AutoAssembler;
using System.Text;
using System.Collections;
using System.Collections.Generic;

namespace Example
{
    class Program
    {
        public struct StrustA
        {
            public bool Boolean;
            public int Value;
            public string Nmae;
        }
        static void Main(string[] args)
        {
            //初始化MemoryAPI类和AutoAssembler类
            //Initialization MemoryAPI & AutoAssembler
            MemoryAPI MemoryAPI = new MemoryAPI("Explorer");
            //AutoAssembler类有另一种构造方法是AutoAssembler(ProcessName)
            //AutoAssembler class have another construction function: AutoAssembler(ProcessName)
            //For example,  AutoAssembler Assembler = new AutoAssembler("Explorer");
            AutoAssembler.AutoAssembler Assembler = new AutoAssembler.AutoAssembler(MemoryAPI);
            if (!MemoryAPI.ok)
            {
                Console.WriteLine("OpenProcess Failed!");
                Console.ReadKey();
                Assembler.Close();
                return;
            }
            Console.WriteLine("OpenProcess successed!Press anykey to run script which below...");
            Console.ReadKey(true);
            //这个脚本用到了大部分的自动汇编引擎命令
            //This script used almost all AutoAssembler Commands that support
            string AAScript = "[enable]\r\naobscanmodule(INJECT,Explorer.exe,48 B9 FF FF FF FF FF FF 00 00) // should be unique\r\nalloc(ThreadMemory,256)\r\nalloc(newmem,1000,Explorer.exe)\r\nlabel(code)\r\nlabel(return)\r\nnewmem:\r\ncode:\r\n  mov rcx,0000FFFFFFFFFFFF\r\n  nop 9\r\n  jmp return\r\nINJECT:\r\n  jmp newmem\r\n  nop 5\r\nreturn:\r\nThreadMemory:\r\nmov rax,12345678\r\npush rax\r\nsub rax,rax\r\npop rax\r\nret\r\nThreadMemory + 100:\r\ndd 100.25 0\r\nCreateThread(ThreadMemory)\r\nregistersymbol(INJECT)\r\nregistersymbol(ThreadMemory)\r\n[DISABLE]\r\nINJECT:\r\n  db 48 B9 FF FF FF FF FF FF 00 00\r\nunregistersymbol(INJECT)\r\nunregistersymbol(ThreadMemory)\r\ndealloc(newmem)\r\ndealloc(ThreadMemory)";
            //将脚本加入到自动汇编引擎中
            //Add the script to AutoAssembler
            Console.WriteLine(AAScript);
            Assembler.AddScript("Test", AAScript);
            //现在启用这个脚本
            //now enable this script
            if (!Assembler.RunScript("Test")) 
            {
                Console.WriteLine(Assembler.ErrorInfo);
                Console.ReadKey();
                Assembler.Close();
                return;
            };
            //获取符号ThreadMemroy和INJECT的地址。注意：符号名大小写敏感！
            //Get registered symbol ThreadMemory and INJECT address.Warning:Case sensitive!
            long ThreadMemory = Assembler.GetAddress("ThreadMemory+100");
            float temp = 0;
            MemoryAPI.ReadMemoryFloat(ThreadMemory, ref temp);
            Console.WriteLine("Address:{0:X},Value:{1}", ThreadMemory,temp);
            long inject = Assembler.GetAddress("INJECT");
            Console.WriteLine("INJECT's Address:{0:X}\nInput word you want write to ThreadMemory's address:", inject);
            string s = Console.ReadLine();
            MemoryAPI.WriteMemoryString(ThreadMemory, s,Encoding.Unicode);
            //ReadMemoryString 函数的参数length表示要读取的字符的长度，若参数值为0则自动读取到字符串的00结尾，但自动读取的字符串不能超过1024个字节，否则将读取不完整！
            //WriteMemoryString function parameter length express the length you wanna read,if length = 0,then auto read to string 0 end.but string cannot more than 1024 byte, otherwise cannot read completely!
            s = MemoryAPI.ReadMemoryString(ThreadMemory, 0, Encoding.Unicode);
            Console.WriteLine("ThreadMemory's address:{0:X},content:{1}",ThreadMemory, s);
            Console.WriteLine("script execute over,press anykey to disable script...");
            Console.ReadKey(true);
            if (!Assembler.RunScript("Test"))
            {
                Console.WriteLine(Assembler.ErrorInfo);
                Console.ReadKey();
                Assembler.Close();
                return;
            };
        }
    }
}
