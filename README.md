# AutoAssembler
A C# Class library like CE's AutoAssembler<br>
* 此自动汇编引擎的设计参考了Dark Byte 的[cheat engine](https://github.com/cheat-engine/cheat-engine)并在一定程度上进行了模仿.<br>
* 此自动汇编引擎使用的汇编指令解析器是x64dbg的[XEDParse](https://github.com/x64dbg/XEDParse).<br>
`感谢这些优秀的开发者们,如有侵权请联系删除!`<br>
* 目前此自动汇编引擎支持的脚本命令有
  * AOBScan,AOBscanmodule,Alloc,Assert,Dealloc,Registersymbol,unRegistersymbol,Label,CreateThread,Define<br>
    关于这些指令如何使用,您可以去参考 https://wiki.cheatengine.org/index.php?title=Auto_Assembler:Commands<br>
如果你觉得缺少什么命令或者哪个功能有问题可以联系我,我看情况进行修改/添加.<br>
## 更新内容:2020.1.1
* 新的内存管理机制
	* 在 MemoryAPI 类中引入了一个类似于堆内存管理的的机制:<br>当请求分配内存时,会与当前堆列表进行匹配,如果存在一个堆满足条件,会将内存分配到此堆下.若不存在一个堆满足此条件,自动汇编引擎将会创建一个新的堆来满足条件并将内存分配到此堆下。
	* 堆默认有32个内存单元,每个内存单元的大小为 1024字节.
	* 举个例子:首先脚本A中执行指令`alloc(newmem,1024,gamedll.dll)`,假设堆列表中不存在一个地址距`gamedll.dll`较接近的堆,那么自动汇编引擎会在距`gamedll.dll`较近的空闲内存中创建一个新的堆,然后将`newmem`分配到此堆下.之后脚本B执行指令`alloc(newmem,1024,gamedll.dll)`,堆列表中存在满足条件的堆,所以`newmem`将会被分配到匹配的堆的空闲位置.<br>显然,这将更利于内存的充分利用以及提高脚本的执行速度.
	* 如果无特殊情况,建议分配的内存大小不要超过1024个字节.因为这样分配的速度更快,对堆的负荷也更小,而且通常来说注入的代码不会超过1024个字节.
* 新增指令 [aobscan](https://wiki.cheatengine.org/index.php?title=Auto_Assembler:aobScan),支持通配符 `??` `**` `*`,不支持半字节通配,仅扫描可读写的内存.
* 新增指令 [assert](https://wiki.cheatengine.org/index.php?title=Auto_Assembler:assert),支持通配符 `??` `**` `*`.
* 扩展了定义数据伪指令,现在支持`db`、`dw`、`dd`、`dq`指令,它们分别是定义字节、字、双字、四字数据.定义的数据用逗号或空格隔开,支持定义字符串.注意,定义字节数据时,字符串采用ASCII编码；定义字数据时,字符串采用Unicode编码.以下是指令示范(16进制):
	* db 44 39 79 08 0F 84 C3 03 00 00 字节数组:44 39 79 08 0F 84 C3 03 00 00
	* db 44,39,79,08,0F,84,C3,03,00,00 字节数组:44 39 79 08 0F 84 C3 03 00 00
	* 字符串声明可以是`""`也可以是`''`,支持转义符`\`,`\t`水平定位符号,`\r\n`一般连用,表示回车换行.使用指令dd dq时无法定义字符串.
	* db "Hello World!",0 字节数组:48 65 6C 6C 6F 20 57 6F 72 6C 64 21 00
	* dw '你好 世界!',0 字节数组:60 4F 7D 59 20 00 16 4E 4C 75 21 00 00 00
	* 假如字符串中要用到'号或"号请使用转义符`\"` `\'`,否则会出现错误
	* dw "这是\"转义符\"!",0 字节数组:D9 8F 2F 66 22 00 6C 8F 49 4E 26 7B 22 00 21 00 00 00
	* 逗号和空格可以混合使用,但不推荐这样
	* db 44,39 79 08,0F 84 C3,03 00 00 字节数组:44 39 79 08 0F 84 C3 03 00 00
	* db "Hello World!" 0 字节数组:48 65 6C 6C 6F 20 57 6F 72 6C 64 21 00
* 汇编代码支持直接写浮点数,自动汇编引擎会将浮点数转成字节码.在浮点数结尾中添加`f`结尾表示单精度浮点数,否则默认为双精度浮点数(数字注意要添加小数点,否则会被视为16进制数处理).此外,类似于`(float)100`和`(double)100`这样的指令已不支持!
	* 例如:`mov rax,100.0f` -> `mov rax,42C80000`,字节码:`48 C7 C0 00 00 C8 42`
	* `mov rax,100.0` -> `mov rax,4059000000000000`,字节码:`48 B8 00 00 00 00 00 00 59 40`.注意不要忘记小数位`.0`!
* 优化了符号解析
* 修正`Define`指令的格式.与CE的语法一致.
* 添加了几个调试函数.`GetAddressTest`是获取地址表达式解析过程,`GetHeapInfo`获取堆列表信息.
## 下面是注意事项:
* XEDParse汇编指令解析器有一些不支持的指令!比如,代码<br>`7FFFFFFE8BFF:`<br>`   jmp 4000000`<br>因为它是远距离跳转,所以自动汇编引擎无法处理此命令.除此之外,还有其它一些指令也不支持,不一一列举了.<br>
* `cmp [rcx] ,0`这句汇编代码将无法被XEDParse解析,它会提示:`Ambiguous memory size`(模糊的操作数大小),因为您没有指定对`[rcx]`的操作大小,所以应该在`[rcx]`前加上操作数大小 `byte/word/dword/qword ptr`,如:`cmp dword ptr [rcx]`.为什么CE支持`cmp [rcx],0`这样的语法?因为ce对没用限定操作数大小的内存操作默认会编译成 `cmp dword ptr [address],0`.
* `AOBscanmodule(SymbolName,ModuleName,AOBString)`中参数`AOBString`的通配符可以是`??`或`**`和`*`,但不支持半字节通配。例如:`AOBscanmodule(SymbolName,ModuleName,48 B9 FF FF FF F* FF FF 00 00)`或`AOBscanmodule(SymbolName,ModuleName,48 B9 FF FF FF ?f FF FF 00 00)`是不支持的!
	* 正确的示范:`AOBscanmodule(SymbolName,ModuleName,48 B9 FF FF FF ?? FF FF 00 00)`
* `Assert`指令也支持通配符`??`或`**`和`*`,但不支持半字节通配。(￣.￣)
* 自动汇编脚本中的涉及到的符号,包括脚本名称、全局符号、分配的内存、标签全部区分大小写!<br>
* 当全局内存符号(RegisteredSymbols)出现重复时,自动汇编引擎并不会提示冲突,而是全局内存符号的值覆盖成新申请的值.<br>
* XEDParse的内存操作数不支持 符号+偏移,例如`[newmem+1000]`.但你或许可以考虑下面这种方式
![image](https://github.com/S1nyer/AutoAssembler/tree/master/image\pic1.png)
* 获取地址优先级:全局符号 > 模块 > 静态地址<br>
* 自动汇编引擎的加减乘法是没有运算优先级的,它是线性运算,所以`Label + 2 * 8`相当于`(Label + 2) * 8`,无论是在自动汇编脚本中还是GetAddress函数中都是如此.<br>
* 当汇编脚本执行失败时,具体可以通过`GetErrorInfo`函数获取详细错误信息.
* 更新历史在项目[AutoAssemble/History.md](https://github.com/S1nyer/AutoAssembler/blob/master/AutoAssembler/History.md)内,若想了解可自行查看.
## 以下是一段如何使用此汇编引擎的示范代码:<br>
```c#
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
        return;
    }
    Console.WriteLine("OpenProcess successed!Press anykey to run script which below...");
    Console.ReadKey(true);
    //这个脚本用到了大部分的自动汇编引擎命令
    //This script used almost all AutoAssembler Commands that support
    string AAScript = "[enable]\r\naobscanmodule(INJECT,Explorer.EXE,48 B9 FF FF FF FF FF FF 00 00) // should be unique\r\nalloc(ThreadMemory,256)\r\nalloc(newmem,1000,Explorer.exe)\r\nlabel(code)\r\nlabel(return)\r\nnewmem:\r\ncode:\r\n  mov rcx,0000FFFFFFFFFFFF\r\n  nop 9\r\n  jmp return\r\nINJECT:\r\n  jmp newmem\r\n  nop 5\r\nreturn:\r\nThreadMemory:\r\nmov rax,12345678\r\npush rax\r\nsub rax,rax\r\npop rax\r\nret\r\nThreadMemory + 100:\r\ndb 00 00 00 80\r\nCreateThread(ThreadMemory)\r\nregistersymbol(INJECT)\r\nregistersymbol(ThreadMemory)\r\n[DISABLE]\r\nINJECT:\r\n  db 48 B9 FF FF FF FF FF FF 00 00\r\nunregistersymbol(INJECT)\r\nunregistersymbol(ThreadMemory)\r\ndealloc(newmem)\r\ndealloc(ThreadMemory)";
    //将脚本加入到自动汇编引擎中
    //Add the script to AutoAssembler
    Assembler.AddScript("Test", AAScript);
    //现在启用这个脚本
    //now enable this script
    if (!Assembler.RunScript("Test")) 
    {
        Console.WriteLine(Assembler.ErrorInfo);
        Console.ReadKey();
        return;
    };
```
如果想要更加详细的了解此自动汇编引擎如何使用,目录下的Example文件夹将告诉您如何使用它!<br>
<br>
# 您可以自由传播和修改此源码，在遵照下面的约束条件的前提下：
  ``1.若他人使用此源码用于违法犯罪行为,本人概不负责,亦不承担任何法律责任!``<br>
  ``2.一切因使用此源码而引致之任何意外、疏忽、合约毁坏、诽谤、版权或知识产权侵犯及其所造成的损失(包括在非官方站点下载此源码而感染电脑病毒)，本人概不负责，亦不承担任何法律责任!``<br>
  ``3.请保留原作者信息``<br>
## 下面是我的联系方式<br>
* 邮箱:`1149548291@qq.com`  注:大部分时间都不会回复,因为我还要上学.
