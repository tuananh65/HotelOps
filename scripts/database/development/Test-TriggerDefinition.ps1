$ErrorActionPreference='Stop'
$tokens=$null;$parseErrors=$null
$ast=[System.Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot 'Install-Fe04Foundation.ps1'),[ref]$tokens,[ref]$parseErrors)
if($parseErrors.Count){throw 'Installer parse failed'}
# Evaluate only the pure comparison function; never invoke the installer or connect to SQL.
$function=$ast.Find({param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'TriggerDefinition'},$true)
if(!$function){throw 'Comparison function missing'}
Invoke-Expression $function.Extent.Text
$expected='CREATE TRIGGER dbo.guard ON dbo.events AFTER INSERT AS SELECT 1;'
$alter='ALTER TRIGGER dbo.guard ON dbo.events AFTER INSERT AS SELECT 1;'
if((TriggerDefinition $alter) -cne (TriggerDefinition $expected)){throw 'SQL Server header equivalence failed'}
foreach($drift in @($alter.Replace('SELECT 1','SELECT 2'),$alter.Replace('dbo.guard','dbo.other'),$alter.Replace('dbo.events','dbo.other'),$alter.Replace('SELECT','select'))){
 if((TriggerDefinition $drift) -ceq (TriggerDefinition $expected)){throw 'Source drift was accepted'}
}
'PASS: equivalent DDL header; body, trigger name, table name and case drift rejected'
