function Edit-XmlNodes {
param (
    [xml] $doc = $(throw "doc is a required parameter"),
    [string] $xpath = $(throw "xpath is a required parameter"),
    $namespace = $(throw "namespace is a required parameter"),
    [string] $value = $(throw "value is a required parameter"),
    [bool] $condition = $true
)    
    if ($condition -eq $true) {
        $nodes = $doc.SelectNodes($xpath, $namespace)
         
        foreach ($node in $nodes) {
            if ($node -ne $null) {
                if ($node.NodeType -eq "Element") {
                    $node.InnerXml = $value
                }
                else {
                    $node.Value = $value
                }
            }
        }
    }
}

function Switch-FrameworkReference {
param (
    [String] $sourcePrj = $(throw "sourcePrj is a required parameter")
    #[string] $destinationDir = $(throw "= is a required parameter")
)    
    
    $frameworkLocation = (get-item $sourcePrj ).parent.FullName
    Get-ChildItem "$sourcePrj" -Filter *.csproj -Recurse | 
    Foreach-Object {

        if ($_.FullName.Contains("Jarvis.Web"))
        {
             Write-Output 'Modification of project $_.FullName'
            $xml = [xml](Get-Content $_.FullName)
            $ns = new-object Xml.XmlNamespaceManager $xml.NameTable
            $ns.AddNamespace("msb", "http://schemas.microsoft.com/developer/msbuild/2003")
    
            Edit-XmlNodes -doc $xml -namespace $ns `
                -xpath "//msb:Reference[starts-with(@Include, 'Jarvis.Framework.Shared,')]" `
                -value "<SpecificVersion>False</SpecificVersion>
          <HintPath>$frameworkLocation\Jarvis.Framework\Jarvis.Framework.Bus.Rebus.Integration\bin\Debug\net461\Jarvis.Framework.Shared.dll</HintPath>
          "
            Edit-XmlNodes -doc $xml -namespace $ns `
                -xpath "//msb:Reference[starts-with(@Include, 'Jarvis.NEventStoreEx,')]" `
                -value "<SpecificVersion>False</SpecificVersion>
          <HintPath>$frameworkLocation\Jarvis.Framework\Jarvis.Framework.Bus.Rebus.Integration\bin\Debug\net461\Jarvis.NEventStoreEx.dll</HintPath>
          "
            Edit-XmlNodes -doc $xml -namespace $ns `
                -xpath "//msb:Reference[starts-with(@Include, 'Jarvis.Framework.Kernel,')]" `
                -value "<SpecificVersion>False</SpecificVersion>
          <HintPath>$frameworkLocation\Jarvis.Framework\Jarvis.Framework.Bus.Rebus.Integration\bin\Debug\net461\Jarvis.Framework.Kernel.dll</HintPath>
          "
           Edit-XmlNodes -doc $xml -namespace $ns `
                -xpath "//msb:Reference[starts-with(@Include, 'Jarvis.Framework.Bus.Rebus.Integration,')]" `
                -value "<SpecificVersion>False</SpecificVersion>
          <HintPath>$frameworkLocation\Jarvis.Framework\Jarvis.Framework.Bus.Rebus.Integration\bin\Debug\net461\Jarvis.Framework.Bus.Rebus.Integration.dll</HintPath>
          "
           Edit-XmlNodes -doc $xml -namespace $ns `
                -xpath "//msb:Reference[starts-with(@Include, 'Jarvis.Framework.TestHelpers,')]" `
                -value "<SpecificVersion>False</SpecificVersion>
          <HintPath>$frameworkLocation\Jarvis.Framework\Jarvis.Framework.TestHelpers\bin\Debug\net461\Jarvis.Framework.TestHelpers.dll</HintPath>
          "
            $xml.Save($_.FullName)
        }
        else
        {
            Write-Output 'Modification of project $_.FullName'
            $xml = [xml](Get-Content $_.FullName)

            $referenceNode = $xml.SelectSingleNode("//ItemGroup/PackageReference[@Include='Jarvis.Framework']")
            if ($referenceNode -ne $null) { $referenceNode.ParentNode.RemoveChild($referenceNode)}
            $referenceNode = $xml.SelectSingleNode("//ItemGroup/PackageReference[@Include='Jarvis.Framework.Shared']")
            if ($referenceNode -ne $null) { $referenceNode.ParentNode.RemoveChild($referenceNode)}
            $referenceNode = $xml.SelectSingleNode("//ItemGroup/PackageReference[@Include='Jarvis.Framework.Rebus']")
            if ($referenceNode -ne $null) { $referenceNode.ParentNode.RemoveChild($referenceNode)}

            $firstChild = $xml
            $itemGroup = $xml.CreateElement("ItemGroup")
            
            $kernelReference =  $xml.CreateElement("Reference")
            $attrib = $xml.CreateAttribute("Include")
            $attrib.Value = "Jarvis.Framework.Kernel"
            $kernelReference.Attributes.Append($attrib)
            $kernelHintPath = $xml.CreateElement("HintPath")
            $kernelHintPath.InnerText = "$frameworkLocation\Jarvis.Framework\Jarvis.Framework.Bus.Rebus.Integration\bin\Debug\net461\Jarvis.Framework.Kernel.dll" 
            $kernelReference.AppendChild($kernelHintPath)
            $itemGroup.AppendChild($kernelReference)

            $sharedReference =  $xml.CreateElement("Reference")
            $sharedattrib = $xml.CreateAttribute("Include")
            $sharedattrib.Value = "Jarvis.Framework.Shared"
            $sharedReference.Attributes.Append($sharedattrib)
            $sharedHintPath = $xml.CreateElement("HintPath")
            $sharedHintPath.InnerText = "$frameworkLocation\Jarvis.Framework\Jarvis.Framework.Bus.Rebus.Integration\bin\Debug\net461\Jarvis.Framework.Shared.dll" 
            $sharedReference.AppendChild($sharedHintPath)
            $itemGroup.AppendChild($sharedReference)

            $rebusReference =  $xml.CreateElement("Reference")
            $rebusattrib = $xml.CreateAttribute("Include")
            $rebusattrib.Value = "Jarvis.Framework.Bus.Rebus.Integration"
            $rebusReference.Attributes.Append($rebusattrib)
            $rebusHintPath = $xml.CreateElement("HintPath")
            $rebusHintPath.InnerText = "$frameworkLocation\Jarvis.Framework\Jarvis.Framework.Bus.Rebus.Integration\bin\Debug\net461\Jarvis.Framework.Bus.Rebus.Integration.dll" 
            $rebusReference.AppendChild($rebusHintPath)
            $itemGroup.AppendChild($rebusReference)
         
            $xml.DocumentElement.AppendChild($itemGroup)
        
            $xml.Save($_.FullName)
        }
    }
}

$runningDirectory = Split-Path -Parent -Path $MyInvocation.MyCommand.Definition
Switch-FrameworkReference -sourcePrj $runningDirectory