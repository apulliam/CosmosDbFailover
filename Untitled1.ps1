
$pwd = Read-Host -Prompt "Enter password" -AsSecureString

# Create the service principal (use a strong password)
$sp = New-AzureRmADServicePrincipal -DisplayName "apulliam-cosmosdb-manager" -Password $pwd

# Give it the permissions it needs...
New-AzureRmRoleAssignment -ServicePrincipalName $sp.ApplicationId -RoleDefinitionName Contributor

# Display the Application ID, because we'll need it later.
$sp | Select DisplayName, ApplicationId