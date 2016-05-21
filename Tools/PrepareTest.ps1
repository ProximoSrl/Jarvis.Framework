param(
    [string] $baseMongoConnection = "mongodb://admin:123456##localhost/{0}",
    [string] $connectionQueryString = "?authSource=admin",
    [string] $configuration = "debug"
)

##Logging tests
$configFileName = "..\Logging\Jarvis.Framework.LoggingTests\bin\$configuration\Jarvis.Framework.LoggingTests.dll.config"
Write-Output "Config File Name Is: $configFileName"

$xml = [xml](Get-Content $configFileName)
 
Edit-XmlNodes $xml -xpath "/configuration/connectionStrings/add[@name='testDb']/@connectionString" -value "$baseMongoConnection$connectionQueryString"

$xml.save($configFileName)

##main tests
$configFileName = "..\Jarvis.Framework.Tests\bin\$configuration\Jarvis.Framework.Tests.dll.config"
Write-Output "Config File Name Is: $configFileName"

$xml = [xml](Get-Content $configFileName)
 
Edit-XmlNodes $xml -xpath "/configuration/connectionStrings/add[@name='eventstore']/@connectionString" -value ($baseMongoConnection -f "jarvis-framework-es-test" + $connectionQueryString)
Edit-XmlNodes $xml -xpath "/configuration/connectionStrings/add[@name='saga']/@connectionString" -value ($baseMongoConnection -f "jarvis-framework-saga-test" + $connectionQueryString)
Edit-XmlNodes $xml -xpath "/configuration/connectionStrings/add[@name='readmodel']/@connectionString" -value ($baseMongoConnection -f "jarvis-framework-readmodel-test" + $connectionQueryString)
Edit-XmlNodes $xml -xpath "/configuration/connectionStrings/add[@name='system']/@connectionString" -value ($baseMongoConnection -f "jarvis-framework-system-test" + $connectionQueryString)
Edit-XmlNodes $xml -xpath "/configuration/connectionStrings/add[@name='engine']/@connectionString" -value ($baseMongoConnection -f "jarvis-framework-engine-test" + $connectionQueryString)
Edit-XmlNodes $xml -xpath "/configuration/connectionStrings/add[@name='rebus']/@connectionString" -value ($baseMongoConnection -f "jarvis-rebus-test" + $connectionQueryString)

$xml.save($configFileName)

function Edit-XmlNodes {
param (
    [xml] $doc = $(throw "doc is a required parameter"),
    [string] $xpath = $(throw "xpath is a required parameter"),
    [string] $value = $(throw "value is a required parameter"),
    [bool] $condition = $true
)    
    if ($condition -eq $true) {
        $nodes = $doc.SelectNodes($xpath)
         
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

