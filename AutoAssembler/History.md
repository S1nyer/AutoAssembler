# 历史更新
## 更新内容:2020.8.8
* 1.修复了重汇编函数在校正地址时,会错误地调整其它代码块地址的bug.
* 2.修复了在 LastReassemble 过程中,自动汇编引擎无法识别 `Label+Offset:` 格式的错误.
* 3.修复了关于内存分配的一个错误
## 更新内容:2020.7.7
* 1.在自动汇编引擎添加了Script类,现在支持以这种方式执行脚本:(当然,`AutoAssemble(ScriptCode,EnableType);`这种执行脚本的方式仍然支持)
```c#
	AutoAssembler Assembler = new AutoAssembler(ProcessName);
	if(!Assembler.ok)
	{
		Console.WriteLine(Assembler.ErrorInfo);
		Console.ReadKey();
	}
	Assembler.AddScript(ScriptName, ScriptCode);
	if (!Assembler.RunScript(ScriptName)) 
    {
			Console.WriteLine(Assembler.ErrorInfo);
			Console.ReadKey();
			return;
    };	
```
* 2.添加到自动汇编引擎的脚本,会有独立的内存分配堆,并且脚本之间不会相互干扰;在A脚本中申请内存`newmem`,然后再在B脚本中申请内存`newmem`也是可以的。但在同一个脚本中,仍然不允许申请重复的内存符号.<br>
* 3.上面曾说:`AutoAssemble(ScriptCode,EnableType);`这种执行脚本的方式仍然支持.那么,这种匿名脚本所申请的内存实际是储存在自动汇编引擎中`TempScriptAlloceds`成员里的,同样,匿名脚本不允许申请重复的内存符号.<br>
* 4.添加了新的命令Define,它在自动汇编脚本中可以这样使用:
```assembly
	#Define FullHealth (float)100.0
	......
	mov rax,FullHealth
```
* 5.修复了脚本独立内存堆无法释放的bug.
* 6.优化了合并汇编代码函数`MergeAssembles`,使分散代码合并成区块效率提高.
## 更新内容:2020.6.14
* 使用了新的重汇编模式:
	* 当所有非虚拟标签的地址值被确定以后(即自动汇编引擎命令执行完毕后),会通过寻找所有虚拟标签的父标签来估测所有虚拟标签的地址值<br>
	* 所有引用了虚拟标签的代码都会被记录到标签的`references`成员中,当虚拟标签拥有实地址值时,重汇编所有引用此标签的代码<br>
	* 若重汇编的代码大小与预测大小不符，会将所有标签以及代码进行地址对齐(在`Reassemble`函数中通过递归算法实现)<br>
	* 如果进行了地址对齐,为了避免 call, jmp 等指令因为地址的改变而导致错误,所以会在最后会将所有代码重汇编一次(别担心,因为所有的变量都已确定,这一步消耗的时间不会超过5ms).而且若脚本执行过程中没有进行过字节对齐,这一步会跳过.<br>
	* 因为新的重汇编模式具有如上特性,所以它在处理复杂的自动汇编脚本时不会像之前那样经常发生错误,且脚本执行效率和之前基本无差别.<br>
## 更新内容:2020.5.24
* 1.使用了XEDParse提供的回调函数指针(当XEDParse遇到未知标识符时,给调用者用于修复错误),现在已支持如下的语法
```assembly
	jmp label + 486
	mov rax,22*4 + Index
```
* 2.特征码扫描使用双线程,提高了当脚本中有多个AOBScanModule命令时的执行速度
* 3.现在AOBscanmodule指令中模块已可以用双引号进行大小写区分,例如:`AOBscanmodule(Symbol,"xxxx.dll",12 34 56 78 90)`是区分模块名大小写,`AOBscanmodule(Symbol,xxxx.dll,12 34 56 78 90)`是不区分大小写.
* 4.规范了格式:脚本命令(如:`Alloc`)中的数字默认为10进制(除`AOBScanModule`的第三个参数`AobString`外),脚本中的汇编指令(如:`mov rax,12345678`)默认为16进制.但是可以通过添加前缀来表示数字进制,如:`alloc(newmem,$100)`中`$100`表示16进制数字100.`mov mov rax,#12345678`中`#12345678`表示10进制数字12345678.
* 5.当重汇编的指令与原指令的字节长度不同时,会按重汇编的指令的字节长度进行地址对齐
## 更新内容:2020.5.16
* 1.优化了自动汇编引擎的整体架构
* 2.不再使用类Var来处理各种值,类Var中的静态成员全部变成了MemoryAPI类和AutoAssemble类中的数据成员.
## 更新内容:2020.4.30
* 1.自动汇编引擎已支持加法减法乘法偏移,GetAddress函数同样也支持,如:`GetAddress("Label + 100 * 2")`.在自动汇编脚本中的应用如下:<br>
```assembly
Label + offset * offset:
	......
	......
```
* 2.修复了虚标签可以添加偏移的bug,如下是错误示例:<br>
```assembly
alloc(Memory,$100)
Label(VirtualLabel)
Memory:
	......
	VirtualLabel + xxx: //这种语法是不支持的,因为VirtualLabel此时并没有实值,但它添加了偏移
		......
	......
```
* 3.修复了一些bug