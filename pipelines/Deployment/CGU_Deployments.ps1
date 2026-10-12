[CmdletBinding()]
Param (
  [String] $ResourceGroupName,
  [String] $StorageAccountName,
  [String] $ContainerName = 'cgu',
  [String] $Path,
  [String] $SqlServer = '',
  [String] $SqlDatabase = '',
  [string] $Env = ''
)

function Connect-StorageAccount{
  param (
    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [String]$resourceGroupName,
    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [String]$storageAccountName,
    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [String]$containerName
  )

  # Check if the storage account exists
  Write-Output 'Check if the storage account exists'
  $storageAccount = Get-AzStorageAccount -ResourceGroupName $resourceGroupName -Name $storageAccountName 

  if ($null -eq $storageAccount) {
    Write-Error "Storage account '$storageAccountName' does not exist in resource group '$resourceGroupName'. Please create the storage account first."
    exit -1
  }

  # Get the storage account context
  $storageContext = $storageAccount.Context

  # Check if the container exists
  $container = Get-AzStorageContainer -Context $storageContext -Name $containerName -ErrorAction SilentlyContinue

  if ($null -eq $container) {
    # Create the container if it does not exist
    New-AzStorageContainer -Context $storageContext -Name $containerName
    Write-Output "Container '$containerName' created in storage account '$storageAccountName'."
  } else {
    Write-Output "Container '$containerName' already exists in storage account '$storageAccountName'."
  }
}

function Get-FilesNameFromStorageAccount{
  [CmdletBinding(SupportsShouldProcess)]
  param (
    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [String]$containerName,
    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [System.Object]$context
  )

  return Get-AzStorageBlob -Container $containerName -Context $context | ForEach-Object { $_.Name }
}

function Send-FilesToStorageAccount{
  [CmdletBinding(SupportsShouldProcess)]
  param (
    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [String]$fileName,
    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [String]$containerName,
    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [System.Object]$context
  )

  Set-AzStorageBlobContent -File $fileName -Container $ContainerName -Context $context
}

function Save-FilesToSqlServer{
  param (
    [String]$fileName,
    [String]$sqlServer,
    [String]$sqlDatabase
  )

  $token = (Get-AzAccessToken -ResourceUrl "https://database.windows.net/").Token

  $escapedFileName = $fileName -replace "'", "''"
  $query = @"
IF NOT EXISTS (SELECT * FROM [Amend].[Terms] WHERE LinkFr = '$escapedFileName')
BEGIN
 INSERT INTO [Amend].[Terms] ([LinkFr]) VALUES ('$escapedFileName')
END
"@

  Invoke-SqlCmd -ServerInstance "$sqlServer.database.windows.net,1433" -Database $sqlDatabase -AccessToken $token -Query $query

  Write-Output "The '$fileName' is inserted in $sqlDatabase."
}


if ($Null -eq (Get-PackageProvider -Name NuGet -ErrorAction Ignore)) {
  Install-PackageProvider -Name NuGet -Force -Scope CurrentUser;
}

Install-Module SqlServer -Scope CurrentUser -Force -AllowClobber | Out-Null 
Import-Module SqlServer

Connect-StorageAccount -resourceGroupName $ResourceGroupName -storageAccountName $StorageAccountName -containerName $ContainerName
$context = New-AzStorageContext -StorageAccountName $StorageAccountName 
$blobs = Get-FilesNameFromStorageAccount -containerName $ContainerName -context $context

if ($null -eq $blobs) {
  # Create the container if it does not exist
  Write-Output "The '$containerName' contain not files."
  $blobs = @()
} 

# Files from the folder containing
$files = Get-ChildItem -Path $path  -Recurse -Include *.pdf | ForEach-Object { $_.Name }

# Files not upload to the blob storage
$newFiles = Compare-Object -ReferenceObject $files -DifferenceObject $blobs -PassThru | Where-Object { $_.SideIndicator -eq "<=" } 

Foreach ($newFile in $newFiles) 
{
  $newFile
  $fileName = $path + $newFile

  Write-Output "Ajout du fichier '$fileName' dans le container '$ContainerName'"
  Send-FilesToStorageAccount -fileName $fileName -containerName $ContainerName -context $context

  Write-Output "Sauvegarde du fichier '$newFile' dans la base de données '$SqlDatabase' (serveur '$SqlServer')"
  Save-FilesToSqlServer -fileName $newFile -sqlServer $SqlServer -sqlDatabase $SqlDatabase
}