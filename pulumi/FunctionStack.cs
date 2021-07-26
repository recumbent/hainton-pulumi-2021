using System.Threading.Tasks;
using Pulumi;
using Pulumi.AzureNative.Insights;
using Pulumi.AzureNative.Resources;
using Pulumi.AzureNative.Storage;
using Pulumi.AzureNative.Storage.Inputs;
using Pulumi.AzureNative.Web;
using Pulumi.AzureNative.Web.Inputs;

class FunctionStack : Stack
{
    public FunctionStack()
    {
        var stack = Pulumi.Deployment.Instance.StackName;

        // Create an Azure Resource Group
        var resourceGroup = new ResourceGroup("resourceGroup", new ResourceGroupArgs()
        {
            ResourceGroupName = $"rg-{stack}"
        });

        // pd04 Storage account
        
        // pd03 App Service Plan
        
        // pd05 Code Container
        
        // pd08  Data Container
        
        // pd06 Code blob
        
        // pd07 Application insights
        
        // pd01 Function App
        
        // Set outputs
        ResourceGroupName = resourceGroup.Name;
        Endpoint = Output.Create("You forgot to fix me"); // Output.Format($"https://{app.DefaultHostName}/api/lookup");
    }
    
    [Output]
    public Output<string> ResourceGroupName { get; set; }
    
    [Output]
    public Output<string> Endpoint { get; set; }
    
    // Magic helpers (I copied these from Pulumi examples...)
    private static Output<string> SignedBlobReadUrl(Blob blob, BlobContainer container, StorageAccount account, ResourceGroup resourceGroup)
    {
        return Output.Tuple<string, string, string, string>(
            blob.Name, container.Name, account.Name, resourceGroup.Name).Apply(t =>
        {
            (string blobName, string containerName, string accountName, string resourceGroupName) = t;

            var blobSAS = ListStorageAccountServiceSAS.InvokeAsync(new ListStorageAccountServiceSASArgs
            {
                AccountName = accountName,
                Protocols = HttpProtocol.Https,
                SharedAccessStartTime = "2021-01-01",
                SharedAccessExpiryTime = "2030-01-01",
                Resource = SignedResource.C,
                ResourceGroupName = resourceGroupName,
                Permissions = Permissions.R,
                CanonicalizedResource = "/blob/" + accountName + "/" + containerName,
                ContentType = "application/json",
                CacheControl = "max-age=5",
                ContentDisposition = "inline",
                ContentEncoding = "deflate",
            });
            return Output.Format($"https://{accountName}.blob.core.windows.net/{containerName}/{blobName}?{blobSAS.Result.ServiceSasToken}");
        });
    }

    private static Output<string> GetConnectionString(Input<string> resourceGroupName, Input<string> accountName)
    {
        // Retrieve the primary storage account key.
        var storageAccountKeys = Output.All<string>(resourceGroupName, accountName).Apply(t =>
        {
            var resourceGroupName = t[0];
            var accountName = t[1];
            return ListStorageAccountKeys.InvokeAsync(
                new ListStorageAccountKeysArgs
                {
                    ResourceGroupName = resourceGroupName,
                    AccountName = accountName
                });
        });
        return storageAccountKeys.Apply(keys =>
        {
            var primaryStorageKey = keys.Keys[0].Value;

            // Build the connection string to the storage account.
            return Output.Format($"DefaultEndpointsProtocol=https;AccountName={accountName};AccountKey={primaryStorageKey}");
        });
    }

}
