Place legacy runtime DLLs here to enable no-admin launch prerequisites.

Required files:
- msvcp100.dll
- msvcr100.dll

At runtime, the launcher copies these into Bin/ before starting Nksp.exe.
If these files are present, VC++ installer elevation is not required.
