# AutoAssembler
A C# Class library like CE's AutoAssembler<br>
* 此自动汇编引擎的设计参考了Dark Byte 的[cheat engine](https://github.com/cheat-engine/cheat-engine)并在一定程度上进行了模仿.
* 此自动汇编引擎使用的汇编指令解析器是x64dbg的[XEDParse](https://github.com/x64dbg/XEDParse).
    感谢这些优秀的开发者们,如有侵权请联系删除!<br>
目前此自动汇编引擎支持的脚本命令有 AOBscanmodule,Alloc,Dealloc,Registersymbol,unRegistersymbol,Label,CreateThread<br>
关于这些指令如何使用,您可以去参考 https://wiki.cheatengine.org/index.php?title=Auto_Assembler:Commands<br>
如果你觉得还有缺少什么命令或者哪个功能有问题可以联系我,我看情况进行修改/添加.<br>
## 一下是一段如何使用此汇编引擎的示范代码:<br>
```c#
AutoAssembler.AutoAssembler Assembler = new AutoAssembler.AutoAssembler();
AutoAssembler.MemoryAPI MemoryAPI = new AutoAssembler.MemoryAPI();
Assembler.OpenProcess("explorer", true);
//This script used almost all AutoAssembler Commands that support
string AAScript = "[enable]\r\naobscanmodule(INJECT,Explorer.EXE,48 B9 FF FF FF FF FF FF 00 00) // should be unique\r\nalloc(ThreadMemory,256)\r\nalloc(newmem,1000,Explorer.exe)\r\nlabel(code)\r\nlabel(return)\r\nnewmem:\r\ncode:\r\n  mov rcx,0000FFFFFFFFFFFF\r\n  nop 9\r\n  jmp return\r\nINJECT:\r\n  jmp newmem\r\n  nop 5\r\nreturn:\r\nThreadMemory:\r\nmov rax,12345678\r\npush rax\r\nsub rax,rax\r\npop rax\r\nret\r\ncreatethread(ThreadMemory)\r\nregistersymbol(INJECT)\r\nregistersymbol(ThreadMemory)\r\n[DISABLE]\r\nINJECT:\r\n  db 48 B9 FF FF FF FF FF FF 00 00\r\nunregistersymbol(INJECT)\r\nunregistersymbol(ThreadMemory)\r\ndealloc(newmem)\r\ndealloc(ThreadMemory)";
Assembler.AutoAssemble(AAScript, true);
long inject = MemoryAPI.GetAddress("INJECT");
Console.WriteLine("INJECT's Address:{0}", inject.ToString("x"));
Console.ReadKey();
```
如果想要更加具体的了解这个自动汇编引擎如何使用，目录下的Example将详细告诉您如何使用它!<br>
<br>
# 您可以自由传播和修改此源码，在遵照下面的约束条件的前提下：
  1.若他人使用此源码用于违法犯罪行为,本人概不负责,亦不承担任何法律责任!<br>
  2.一切因使用此源码而引致之任何意外、疏忽、合约毁坏、诽谤、版权或知识产权侵犯及其所造成的损失(包括在非官方站点下载此源码而感染电脑病毒)，本人概不负责，亦不承担任何法律责任!<br>
  3.请保留原作者信息<br>
## 下面是我的联系方式<br>
* 邮箱:1149548291@qq.com  注:我可能大部分时间都不会回复,因为我还在上学.
