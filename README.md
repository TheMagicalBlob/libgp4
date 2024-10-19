An open-source alternative to Sony's gengp4.exe written in C#, rather than Delphi.

Made to avoid dealing with some of the more infuriating aspects of gengp4.exe which often end up producing broken .gp4 projects,
such as:
- Writing to the param.sfo and allegedly stripping any remaining symbols from executables (Former is obvious, but unsure about the latter. Someone confirm)
- Building useless gp4's if the folder name isn't formatted "correctly" _(TITLEID-category1.00 == good, but TITLEID-1.00category == bad)._
- Outputting in two seperate formats to two seperate log windows at the same time and taking it's sweet-ass time as a result, especially when there are thousands of files
- Being written in fking Delphi for some reason instead of C# like the rest of the sdk tools
