<#
.SYNOPSIS
Build and run tests 

.DESCRIPTION
Build, runs giversion test and everything is needed to publish the software

.PARAMETER Configuration
Debug or release

.PARAMETER BuildName
If running inside an Azure DevOps pipeline, this is the name of the
build that will be set along with the gitversion suffix.

.PARAMETER forceInstallPackage
If $false avoid installing BuildUtils, to save time if you are sure
that the package was already installed in the machine

.PARAMETER buildArtifactsDirectory
If different from null it must contain a valid directory where the
script will publish all data, if empty the script will generate
a subdirectory of the current directory to put all artifacts.

.PARAMETER CreateNugetPackages
If $true the script will create nuget packages.

#>

Param
(
    [string]  $Configuration = "release",
    [string] $BuildName = "",
    [Boolean] $ForceInstallPackage = $false,
    [string] $BuildArtifactsDirectory = "",
    [Boolean] $CreateNugetPackages = $false,
    [Boolean] $SkipTest = $false
)

# Variable used by build utils to interact with the continuous integration system
$ci_engine = "azdo"

# Step 1, install some basic utilities that are needed to create the build.
if ($forceInstallPackage -eq $true) {
    Install-package BuildUtils -Confirm:$false -Scope CurrentUser -Force
}
Import-Module BuildUtils

# Step 2: load some variables that are useful for the script 
$runningDirectory = Split-Path -Parent -Path $MyInvocation.MyCommand.Definition
$publishDirectory = $buildArtifactsDirectory

if ($publishDirectory -eq "") {
    $publishDirectory = [System.IO.Path]::Combine($runningDirectory, "BuildOutput")
}

$solutionName = [System.IO.Path]::Combine($runningDirectory, "Jarvis.Framework.sln")

Write-Host "All artifacts of the build will be included in folder $publishDirectory"

# Step 3: Ensure directory for the build was correctly created.
Remove-Item -Recurse -Force -Path $publishDirectory -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $publishDirectory -Force -ErrorAction SilentlyContinue

# Step 4: Running GitVersion
Write-Host "Restoing dotnet tool explicitly"
dotnet tool restore
Assert-LastExecution -message "Unable to restore git-version." -haltExecution $true

$gitversion = Invoke-GitVersion -ConfigurationFile "$runningDirectory/.config/GitVersion.yml"

Write-Host "Assembly version is $($gitversion.assemblyVersion)"
Write-Host "File version is $($gitversion.assemblyFileVersion)"
Write-Host "Nuget version is $($gitversion.nugetVersion)"
Write-Host "Informational version is $($gitversion.assemblyInformationalVersion)"

# assign gitversion variable to plain variables
$assemblyVersion = $gitversion.assemblyVersion
$assemblyFileVersion = $gitversion.assemblyFileVersion
$nugetVersion = $gitversion.nugetVersion
$assemblyInformationalVersion = $gitversion.assemblyInformationalVersion

# These commands are used in azure DevOps pipeline, no need to be run in standard buil
# but it is convenient to check if the commands are correct
Write-Host "##vso[build.updatebuildnumber]$BuildName - $($gitversion.fullSemver)"
Write-Host "##vso[task.setvariable variable=assemblyVersion;]$assemblyVersion"
Write-Host "##vso[task.setvariable variable=assemblyFileVersion;]$assemblyFileVersion"
Write-Host "##vso[task.setvariable variable=assemblyInformationalVersion;]$assemblyInformationalVersion"
Write-Host "##vso[task.setvariable variable=nugetVersion;]$nugetVersion"

Write-Host "Changing all assembly info files to include new GitVersion version"

# Step 5: Build the solution 
Write-Host "Restoring solution dependencies"
dotnet restore $solutionName
Assert-LastExecution -message "Unable to restore dependencies of the project." -haltExecution $true

Write-Host "Building solution"
dotnet build $solutionName -p:IncludeSymbols=true --configuration $Configuration /p:assemblyVersion=$assemblyVersion /p:FileVersion=$assemblyFileVersion /p:InformationalVersion=$assemblyInformationalVersion 
Assert-LastExecution -message "Build solution failed." -haltExecution $true

Write-Host "Test Phase: Skip test = $SkipTest"
if (!$SkipTest) {

    # Step 6: Running tests
    Write-Host "Running tests"

    $testProject = Get-ChildItem $runningDirectory -Recurse -Filter "*.tests.csproj"
    foreach ($file in $testProject) {
        $fileName = $file.Name
        Write-Host "Run test for $($file.FullName) to file result $publishDirectory/$fileName.trx"
        dotnet test $file.FullName --no-build --configuration $Configuration --results-directory $publishDirectory --logger "trx;" /p:PackageVersion=$nugetVersion /p:AssemblyVersion=$assemblyVersion /p:FileVersion=$assemblyFileVersion /p:InformationalVersion=$assemblyInformationalVersion
    }
}

if ($CreateNugetPackages) {
    # Step 7: publish nuget package, important the --no-build will reduce time avoiding to rebuild the solution because it was build before.
    Write-Host "AssemblyVersion: $assemblyVersion FileVersion: $assemblyFileVersion"

    # we need to publish all packages.
    dotnet pack "Jarvis.Framework.Shared/Jarvis.Framework.Shared.csproj" -o $publishDirectory --configuration $Configuration /p:PackageVersion=$nugetVersion /p:assemblyVersion=$assemblyVersion /p:FileVersion=$assemblyFileVersion /p:InformationalVersion=$assemblyInformationalVersion -p:IncludeSymbols=true -p:SymbolPackageFormat=snupkg
    Assert-LastExecution -message "Unable to create nuget packages for Jarvis.Framework.Shared" -haltExecution $true

    dotnet pack "Jarvis.Framework/Jarvis.Framework.csproj" -o $publishDirectory --configuration $Configuration /p:PackageVersion=$nugetVersion /p:assemblyVersion=$assemblyVersion /p:FileVersion=$assemblyFileVersion /p:InformationalVersion=$assemblyInformationalVersion -p:IncludeSymbols=true -p:SymbolPackageFormat=snupkg
    Assert-LastExecution -message "Unable to create nuget for Jarvis.Framework.Kernel" -haltExecution $true

    dotnet pack "Jarvis.Framework.Rebus/Jarvis.Framework.Rebus.csproj" -o $publishDirectory --configuration $Configuration /p:PackageVersion=$nugetVersion /p:assemblyVersion=$assemblyVersion /p:FileVersion=$assemblyFileVersion /p:InformationalVersion=$assemblyInformationalVersion -p:IncludeSymbols=true -p:SymbolPackageFormat=snupkg
    Assert-LastExecution -message "Unable to create nuget packages for Bus.Rebus.Integration.csproj" -haltExecution $true
}

exit (0)