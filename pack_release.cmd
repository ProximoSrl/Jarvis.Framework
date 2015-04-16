rmdir .\publish /s /q

xcopy .\Jarvis.Framework.Bus.Rebus.Integration\bin\release\Jarvis.Framework.Bus.Rebus.Integration.* .\publish\ /S /Y
xcopy .\Jarvis.Framework.Kernel\bin\release\Jarvis.Framework.Kernel.* .\publish\ /S /Y
xcopy .\Jarvis.Framework.Shared\bin\release\Jarvis.Framework.Shared.* .\publish\ /S /Y
xcopy .\Jarvis.NEventStoreEx\bin\release\Jarvis.NEventStoreEx.* .\publish\ /S /Y
xcopy .\Jarvis.Framework.Tests\bin\release\Jarvis.Framework.TestHelpers.* .\publish\ /S /Y

pause