# AutoAssembler
A C# Class library like CE's AutoAssembler<br>
* 此自动汇编引擎的设计参考了Dark Byte 的[cheat engine](https://github.com/cheat-engine/cheat-engine)并在一定程度上进行了模仿.<br>
* 此自动汇编引擎使用的汇编指令解析器是x64dbg的[XEDParse](https://github.com/x64dbg/XEDParse).<br>
`感谢这些优秀的开发者们,如有侵权请联系删除!`<br>
* 目前此自动汇编引擎支持的脚本命令有
  * AOBscanmodule,Alloc,Dealloc,Registersymbol,unRegistersymbol,Label,CreateThread<br>
    关于这些指令如何使用,您可以去参考 https://wiki.cheatengine.org/index.php?title=Auto_Assembler:Commands<br>
如果你觉得缺少什么命令或者哪个功能有问题可以联系我,我看情况进行修改/添加.<br>
## 更新内容:2020.6.14
* 使用了新的重汇编模式:
	* 1.当所有非虚拟标签的地址值被确定以后(即自动汇编引擎命令执行完毕后),会通过寻找所有虚拟标签的父标签来估测所有虚拟标签的地址值<br>
	* 2.所有引用了虚拟标签的代码都会被记录到标签的references成员中,当虚拟标签拥有实地址值时,重汇编所有引用此标签的代码<br>
	* 3.若重汇编的代码大小与预测大小不符，会将所有标签以及代码进行地址对齐(在Reassemble函数中通过递归算法实现)<br>
	* 4.如果进行了地址对齐,为了避免call,jmp等指令因为地址改变而产生错误,所以会在最后会将所有代码重汇编一次(别担心,因为所有的变量都已确定,这一步消耗的时间不会超过5ms).而且若脚本执行过程中没有进行过字节对齐,这一步会跳过.<br>
	* 5.因为新的重汇编模式具有如上特性,所以它在处理复杂的自动汇编脚本时不会像之前那样经常发生错误,且脚本执行效率和之前基本无差别.<br>
## 下面是注意事项:
* XEDParse汇编指令解析器有一些不支持的指令!比如,代码<br>`7FFFFFFE8BFF:`<br>`   jmp 4000000`<br>因为它是远距离跳转,所以自动汇编引擎无法处理此命令.除此之外,还有其它一些指令也不支持,这些需要你们自己去发现.<br>
* `AOBscanmodule(SymbolName,ModuleName,AOBString)`中参数`AOBString`的通配符可以是`??`或`**`,但不支持半字节通配。例如:`AOBscanmodule(SymbolName,ModuleName,48 B9 FF FF FF F* FF FF 00 00)`或`AOBscanmodule(SymbolName,ModuleName,48 B9 FF FF FF ?f FF FF 00 00)`是不支持的!
	* 正确的示范:`AOBscanmodule(SymbolName,ModuleName,48 B9 FF FF FF ?? FF FF 00 00)`
* 自动汇编脚本中的涉及到的符号,包括全局符号、分配的内存、标签全部区分大小写!<br>
* 分配内存符号(AllocedMemorys)是不能起冲突的,如果您在之前的汇编脚本中使用了Alloc(newmem,128)命令,然后在没有释放`newmem`的情况下,在另一个脚本中又使用了Alloc(newmem,128)命令,此时汇编引擎将提示命名冲突,且脚本将不会被执行!<br>
* 当全局内存符号(RegisteredSymbols)出现重复时,自动汇编引擎并不会提示冲突,而是全局内存符合的值覆盖成新申请的值.<br>
* 获取地址优先级:全局符号 > 模块 > 静态地址<br>
* 自动汇编引擎的加减乘法是没有运算优先级的,它是线性运算,所以`Label + 2 * 8`相当于`(Label + 2) * 8`,无论是在自动汇编脚本中还是GetAddress函数中都是如此.<br>
* 当汇编脚本执行失败时,具体可以通过`GetErrorInfo`函数获取详细错误信息.
## 以下是一段如何使用此汇编引擎的示范代码:<br>
```c#
            MemoryAPI MemoryAPI = new MemoryAPI("Explorer");
            //AutoAssembler class have another construction function: AutoAssembler(ProcessName)
            //For example,  AutoAssembler Assembler = new AutoAssembler("Explorer");
            AutoAssembler.AutoAssembler Assembler = new AutoAssembler.AutoAssembler(MemoryAPI);
            if (!MemoryAPI.ok)
            {
                Console.WriteLine("OpenProcess Failed!");
                Console.ReadKey();
                return;
            }
			string AAScript = "[enable]\r\naobscanmodule(INJECT,Explorer.EXE,48 B9 FF FF FF FF FF FF 00 00) // should be unique\r\nalloc(ThreadMemory,256)\r\nalloc(newmem,1000,Explorer.exe)\r\nlabel(code)\r\nlabel(return)\r\nnewmem:\r\ncode:\r\n  mov rcx,0000FFFFFFFFFFFF\r\n  nop 9\r\n  jmp return\r\nINJECT:\r\n  jmp newmem\r\n  nop 5\r\nreturn:\r\nThreadMemory:\r\nmov rax,12345678\r\npush rax\r\nsub rax,rax\r\npop rax\r\nret\r\nThreadMemory + 100:\r\ndb 00 00 00 80\r\nCreateThread(ThreadMemory)\r\nregistersymbol(INJECT)\r\nregistersymbol(ThreadMemory)\r\n[DISABLE]\r\nINJECT:\r\n  db 48 B9 FF FF FF FF FF FF 00 00\r\nunregistersymbol(INJECT)\r\nunregistersymbol(ThreadMemory)\r\ndealloc(newmem)\r\ndealloc(ThreadMemory)";
			if (!Assembler.AutoAssemble(AAScript, true)) 
            {
                Console.WriteLine(Assembler.AutoAssemble_Error);
                Console.ReadKey();
                return;
            };
			Console.WriteLine("Enable script success!")
			Console.ReadKey();
```
如果想要更加详细的了解此自动汇编引擎如何使用,目录下的Example文件夹将告诉您如何使用它!<br>
<br>
# 您可以自由传播和修改此源码，在遵照下面的约束条件的前提下：
  ``1.若他人使用此源码用于违法犯罪行为,本人概不负责,亦不承担任何法律责任!``<br>
  ``2.一切因使用此源码而引致之任何意外、疏忽、合约毁坏、诽谤、版权或知识产权侵犯及其所造成的损失(包括在非官方站点下载此源码而感染电脑病毒)，本人概不负责，亦不承担任何法律责任!``<br>
  ``3.请保留原作者信息``<br>
## 下面是我的联系方式<br>
* 邮箱:`1149548291@qq.com`  注:大部分时间都不会回复,因为我还要上学.
