namespace Recumbent.Demo.QuoteFetch

open FSharp.Data

[<CLIMutable>]
type Request =
    {
        ClientId : int
        QuoteId : int
    }

type Response = JsonProvider<""" { "clientId": 1, "quoteId": 2, "text": "this is a quote" } """>

type Lookup = int -> int -> Response.Root option

module quoteApi =

    type ApiResponse = JsonProvider<""" { "author": "Douglas Adams", "quote": "this is a quote" } """>

    let MakeApiRequest quoteHost quoteId = 

        let uri = sprintf "%s/quote/dna/%i" quoteHost quoteId // Should be function, server should be from config
        let content = HttpRequestHeaders.Accept HttpContentTypes.Json // Ditto

        let data = 
            Http.RequestString
                ( uri,
                  headers = [ content ]
                )

        ApiResponse.Parse data

    let MapToQuote clientId quoteId (apiQuote:ApiResponse.Root) =
        Response.Root(clientId, quoteId, apiQuote.Quote)

    let FetchFromApi quoteHost clientId quoteId = 
        let quote = 
            MakeApiRequest quoteHost quoteId
            |> MapToQuote clientId quoteId

        Some(quote)

module logic =

    let Handler (readFromCache:Lookup) (fetchFromApi:Lookup) writeToCache (lookupRequest:Request) =
        match readFromCache lookupRequest.ClientId lookupRequest.QuoteId with
        | Some(quote) -> quote
        | None ->
            let newQuote = fetchFromApi lookupRequest.ClientId lookupRequest.QuoteId
            writeToCache newQuote.Value
            newQuote.Value

