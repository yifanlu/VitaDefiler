# Compiling
You will need PSM Studio and Visual Studio installed.

## VitaDefilerClient
Open `cmd.exe` as Administrator and execute `setx MSBUILDENABLEALLPROPERTYFUNCTIONS 1`.

Run "Developer Command Prompt for VS20XX", navigate to the `VitaDefilerClient` folder and execute `ilasm /output:NativeFunctions.dll /dll NativeFunctions.cil`.

Open `VitaDefilerClient.sln` in PSM Studio and hit F5.

If you get error about `NativeFunctions` not being resolved (e.g. `Error CS0246: The type or namespace name 'NativeFunctions' could not be found [...]`):

* Right click `VitaDefilerClient / References` on the left, select `Edit References...`
* `Edit References` window will open. On the right select `NativeFunctions` and press Remove (trash icon in top right corner).
* Switch to the `.Net Assembly` tab, navigate to `VitaDefiler/VitaDefilerClient`, select `NativeFunctions.dll` and press `Add`. The project should build now.

## VitaDefiler
Open `VitaDefiler.sln` in Visual Studio. In `Solution Explorer` right click `VitaDefiler / Properties`. Select `Reference Paths`, then add `C:\Program Files (x86)\SCE\PSM\tools\PsmStudio\AddIns\MonoDevelop.Debugger.Soft\` and `C:\Program Files (x86)\SCE\PSM\tools\PsmStudio\bin\`. You can now press F5 to build the project.

# Usage
First, copy library dependencies to the `VitaDefiler/bin/Debug` folder.

* Copy `Mono.Cecil.dll` from `C:\Program Files (x86)\SCE\PSM\tools\PsmStudio\bin`
* Copy `Mono.Debugger.Soft.dll` from `C:\Program Files (x86)\SCE\PSM\tools\PsmStudio\AddIns\MonoDevelop.Debugger.Soft`
* Copy all files from `C:\Program Files (x86)\SCE\PSM\tools\lib`

Open `PSM Dev` application on the Vita. Run `cmd.exe` and navigate to `VitaDefiler` folder. Execute `bin\Debug\VitaDefiler.exe VitaDefilerClient\bin\Release\VitaDefilerClient.psdp`. Vita should now run the `VitaDefilerClient` app and after a few seconds you will get a RPC shell.
