namespace Recumbent.Demo.Aws

open System

open Amazon.Lambda.Core
open Amazon.S3

open Microsoft.Extensions.Configuration

open Recumbent.Demo.QuoteFetch
open Amazon.S3.Model
open System.IO

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[<assembly: LambdaSerializer(typeof<Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer>)>]
()

type FuncConfig =
    {
        host: string
        bucket: string
    }

[<CLIMutable>]
type IntegrationRequest =
    {
        body : string
    }

module configuration =
    let cfg = 
        ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build()

    let GetConfig =
        {
            host = cfg.["QuoteServerHost"]
            bucket = cfg.["DataBucket"]
        }

module aws =
    let s3Config = 
        AmazonS3Config()
    
    let existsInS3 bucket key =
        let s3 = new AmazonS3Client(s3Config)
        let req = new ListObjectsRequest(BucketName = bucket, Prefix = key)
        let resp = s3.ListObjectsAsync(req) |> Async.AwaitTask |> Async.RunSynchronously
        resp.S3Objects.Count = 1

    let readS3 bucket key =
        let s3 = new AmazonS3Client(s3Config)
        let req = GetObjectRequest( BucketName = bucket, Key = key)
        let s3Object = s3.GetObjectAsync(req) |> Async.AwaitTask |> Async.RunSynchronously
        use sr = new StreamReader(s3Object.ResponseStream)
        let content = sr.ReadToEnd()
        content

    let writeS3 bucket key content = 
        let s3 = new AmazonS3Client(s3Config)
        let req = PutObjectRequest( BucketName = bucket, Key = key, ContentBody = content)
        s3.PutObjectAsync(req) |> Async.AwaitTask|> Async.RunSynchronously |> ignore

type AwsLambda() =

    let config = configuration.GetConfig

    let makeFileKey clientId quoteId =
        sprintf "%i/%i.json" clientId quoteId

    let readFromCache clientId quoteId = 
        let key = makeFileKey clientId quoteId
        if (aws.existsInS3 config.bucket key) then
            let quote = Response.Parse (aws.readS3 config.bucket key)
            Some(quote)
        else
            None        

    let writeToCache (quote:Response.Root) =
        let key = makeFileKey quote.ClientId quote.QuoteId
        aws.writeS3 config.bucket key (quote.ToString())

    // This is the lambda handler, the business logic is handled by the same code as for Azure
    member __.FunctionHandler (input: IntegrationRequest) (lc: ILambdaContext) =

        lc.Logger.LogLine(sprintf "Body: %s" input.body)

        // If we run an integration then we get something evil from the integration request
        // This is a hack to get something useful quickly
        let decoded = System.Convert.FromBase64String(input.body)
        let body = System.Text.Encoding.UTF8.GetString(decoded)
        let request = System.Text.Json.JsonSerializer.Deserialize<Request> body
        
        let handler = logic.Handler readFromCache (quoteApi.FetchFromApi config.host) writeToCache
        (handler request).ToString()
