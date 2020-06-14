# 历史更新
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