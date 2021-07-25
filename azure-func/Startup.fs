namespace Recumbent.Demo.Azure

open System
open Microsoft.Azure.Functions.Extensions.DependencyInjection
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection

type FuncConfig =
    {
        host: string
        connectionString: string
        container: string
    }

type Startup() =
    inherit FunctionsStartup ()

    override __.Configure(builder: IFunctionsHostBuilder) =

        let config =
            let cb = new ConfigurationBuilder()
            cb.AddEnvironmentVariables()
            |> ignore
            cb.Build()

        let funcConfig = 
            { 
                host = config.["QuoteServerHost"]
                connectionString = config.["DataConnectionString"]
                container = config.["DataContainer"]
            }

        builder.Services.AddSingleton<FuncConfig>(funcConfig) |> ignore
        ()

[<assembly: FunctionsStartup(typeof<Startup>)>]
()