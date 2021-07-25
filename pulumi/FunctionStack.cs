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

        var storageAccount = new StorageAccount("sa", new StorageAccountArgs
        {
            ResourceGroupName = resourceGroup.Name,
            Sku = new SkuArgs
            {
                Name = SkuName.Standard_LRS,
            },
            Kind = Pulumi.AzureNative.Storage.Kind.StorageV2
        });
        
        var appServicePlan = new AppServicePlan("FuntionAppServicePlan", new AppServicePlanArgs()
        {
            ResourceGroupName = resourceGroup.Name,

            Kind = "FunctionApp",

            // Consumption plan SKU
            Sku = new SkuDescriptionArgs
            {
                Tier = "Dynamic",
                Name = "Y1"
            }
        });
        
        var codeContainer = new BlobContainer("code-container", new BlobContainerArgs()
        {
            ResourceGroupName = resourceGroup.Name,
            AccountName = storageAccount.Name,
            PublicAccess = PublicAccess.None,
            ContainerName = "code"
        });
        
        var dataContainer = new BlobContainer("data", new BlobContainerArgs
        {
            ResourceGroupName = resourceGroup.Name,
            AccountName = storageAccount.Name,
            PublicAccess = PublicAccess.None,
            ContainerName = "data"
        });
        
        var codeBlob = new Blob("functionCode", new BlobArgs()
        {
            AccountName = storageAccount.Name,
            ContainerName = codeContainer.Name,
            ResourceGroupName = resourceGroup.Name,
            Type = BlobType.Block,
            Source = new FileArchive("../azure-func/publish")
        });

        var codeBlobUrl = SignedBlobReadUrl(codeBlob, codeContainer, storageAccount, resourceGroup);
                
        // Application insights
        var appInsights = new Component("appInsights", new ComponentArgs
        {
            ApplicationType = ApplicationType.Web,
            Kind = "web",
            ResourceGroupName = resourceGroup.Name,
        });
        
        var app = new WebApp("FunctionApp", new WebAppArgs()
        {
            Kind = "FunctionApp",
            ResourceGroupName = resourceGroup.Name,
            ServerFarmId = appServicePlan.Id,
            SiteConfig = new SiteConfigArgs()
            {
                AppSettings = new[]
                {
                    // Azure settings
                    new NameValuePairArgs() 
                    {
                        Name = "AzureWebJobsStorage", 
                        Value =  GetConnectionString(resourceGroup.Name, storageAccount.Name) 
                    },
                    new NameValuePairArgs() 
                    {
                        Name = "runtime",
                        Value = "dotnet"
                    },
                    new NameValuePairArgs() 
                    {
                        Name = "FUNCTIONS_EXTENSION_VERSION",
                        Value = "~3"
                    },
                    new NameValuePairArgs() 
                    { 
                        Name = "WEBSITE_RUN_FROM_PACKAGE", 
                        Value = codeBlobUrl 
                    },
                    new NameValuePairArgs()
                    {
                        Name = "APPLICATIONINSIGHTS_CONNECTION_STRING",
                        Value = Output.Format($"InstrumentationKey={appInsights.InstrumentationKey}"),
                    },
                    
                    // App settings
                    new NameValuePairArgs()
                    { 
                        Name = "QuoteServerHost",
                        Value = "https://0rogaeco5b.execute-api.eu-west-2.amazonaws.com/Prod" },
                    new NameValuePairArgs()
                    { 
                        Name = "DataConnectionString",
                        Value = GetConnectionString(resourceGroup.Name, storageAccount.Name) 
                    },
                    new NameValuePairArgs()
                    { 
                        Name = "DataContainer",
                        Value = dataContainer.Name
                    }
                }
            }
        });

        // Set outputs
        ResourceGroupName = resourceGroup.Name;
        Endpoint = Output.Format($"https://{app.DefaultHostName}/api/lookup");
    }
    
    [Output]
    public Output<string> ResourceGroupName { get; set; }
    [Output]
    public Output<string> Endpoint { get; set; }
    
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
