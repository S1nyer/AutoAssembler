# 历史更新
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