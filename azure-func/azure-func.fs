namespace Recumbent.Demo.Azure

open System
open System.IO


open Microsoft.AspNetCore.Mvc
open Microsoft.Azure.WebJobs
open Microsoft.Azure.WebJobs.Extensions.Http

open Azure.Storage.Blobs

open Recumbent.Demo.QuoteFetch

module storage =
    let Write (blobClient : BlobServiceClient) containerName blobName (content : string) =

        let bytes = System.Text.Encoding.UTF8.GetBytes content
        use stream = new MemoryStream(bytes)

        let container = blobClient.GetBlobContainerClient containerName
        let blob = container.GetBlobClient blobName

        (blob.UploadAsync stream) |> Async.AwaitTask |> ignore

    let Exists (blobClient : BlobServiceClient) containerName blobName =
        async {
            let container = blobClient.GetBlobContainerClient containerName
            let blob = container.GetBlobClient blobName

            let! result = blob.ExistsAsync() |> Async.AwaitTask
            return result.Value
        } |> Async.RunSynchronously

    let Read (blobClient : BlobServiceClient) containerName blobName =
        async {
            let container = blobClient.GetBlobContainerClient containerName
            let blob = container.GetBlobClient blobName

            let! result = blob.DownloadAsync() |> Async.AwaitTask
            use sr = new StreamReader(result.Value.Content)
            let content = sr.ReadToEnd()
            return content
        } |> Async.RunSynchronously
        
type AzureFunction(config: FuncConfig) =

    let makeFileKey clientId quoteId =
        sprintf "%i/%i.json" clientId quoteId

    let blob = BlobServiceClient(config.connectionString)

    let readFromCache containerName clientId quoteId = 
        let fn = makeFileKey clientId quoteId
        match storage.Exists blob containerName fn with
        | false -> None
        | true  -> 
            storage.Read blob containerName fn
            |> Response.Parse
            |> Some

    let writeToBlob containerName (quote:Response.Root) =
        let fn = makeFileKey quote.ClientId quote.QuoteId
        storage.Write blob containerName fn (quote.ToString())

    [<FunctionName("lookup")>]
    member _.Handler([<HttpTrigger (AuthorizationLevel.Anonymous, "post", Route = null)>] req : Request) =
        async {
            let handler = logic.Handler (readFromCache config.container) (quoteApi.FetchFromApi config.host) (writeToBlob config.container)
            let response = handler req
            return (OkObjectResult (response.ToString())) :> IActionResult
        } |> Async.StartAsTask