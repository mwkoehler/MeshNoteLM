# Load the assets file
$assets  = Get-Content .\obj\project.assets.json -Raw | ConvertFrom-Json
$targets = $assets.targets.PSObject.Properties.Name
$pkgName = 'Microsoft.VisualStudio.Telemetry'

# 1) Which targets include the package (keys look like 'Package/Version')?
$hits = foreach ($t in $targets) {
  $target = $assets.targets.$t
  $has = $target.PSObject.Properties.Name | Where-Object { $_ -like "$pkgName/*" }
  if ($has) { [PSCustomObject]@{ Target = $t; PackageKey = $has } }
}
$hits | Format-Table

# 2) For each hit, who directly depends on it in that target?
$parents = foreach ($hit in $hits) {
  $t = $hit.Target
  $target = $assets.targets.$t
  $parents = $target.PSObject.Properties | Where-Object {
    $_.Value.dependencies -ne $null -and
    ($_.Value.dependencies.PSObject.Properties.Name -contains $pkgName)
  } | Select-Object -ExpandProperty Name
  [PSCustomObject]@{ Target = $t; Parents = ($parents -join ', ') }
}
$parents | Format-Table -AutoSize
